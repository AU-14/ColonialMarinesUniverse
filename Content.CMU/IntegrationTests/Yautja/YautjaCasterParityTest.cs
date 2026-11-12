#pragma warning disable RA0002 // setup sets caster mode, gun cooldown/aim and projectile shooter directly
using System.Linq;
using System.Numerics;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Xenomorphs.Pathogen.MycotoxinInject;
using Content.Shared.CMU14.Xenomorphs.Pathogen.Walker;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using Robust.UnitTesting;

namespace Content.IntegrationTests.CMU14.Yautja;

// CMU14 Test: yautja bug batch, checked against cmss13-devs/cmss13.
//  - code/datums/ammo/energy.dm: aoe_lethal (eradicator) and lance (dual cannons) call cell_explosion from
//    on_hit_mob, on_hit_turf, on_hit_obj and do_at_max_range, so neither fizzles against terrain or open ground.
//    The BYOND projectile never enters the dense turf, so the blast starts on the open tile in front of a wall.
//  - lance on_hit_mob: ishuman_strict gets ex_act(300), above EXPLOSION_THRESHOLD_GIB (200), so it gibs.
//  - human decapitation leaves the player in their own dead body (no LOOC from a loose head).
//  - yautja are not pathogen hosts and cannot be raised as walkers.
[TestFixture]
public sealed class YautjaCasterParityTest
{
    private const int EradicatorMode = 3;

    private static (EntityUid Hunter, EntityUid Caster) ArmHunter(IEntityManager entMan, EntityCoordinates at, int mode)
    {
        var hunter = entMan.SpawnEntity("CMMobHuman", at);
        entMan.EnsureComponent<YautjaComponent>(hunter);
        var bracer = entMan.SpawnEntity("CMUYautjaBracer", at);
        var caster = entMan.SpawnEntity("CMUYautjaPlasmaCaster", at);
        Assert.That(entMan.System<InventorySystem>().TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
        Assert.That(entMan.System<SharedHandsSystem>().TryPickupAnyHand(hunter, caster), Is.True);

        var stored = entMan.GetComponent<YautjaStoredGearComponent>(caster);
        stored.Bracer = bracer;
        stored.Deployed = true;
        entMan.GetComponent<YautjaBracerComponent>(bracer).Charge = 3000;
        var casterComp = entMan.GetComponent<YautjaCasterComponent>(caster);
        casterComp.CurrentMode = mode;
        casterComp.PowerCost = (FixedPoint2) 100;
        return (hunter, caster);
    }

    private static void Floor(TestMapData map, IEntityManager entMan, int to)
    {
        var maps = entMan.System<SharedMapSystem>();
        for (var x = -1; x <= to; x++)
        for (var y = -3; y <= 3; y++)
            maps.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
    }

    private static void Fire(RobustIntegrationTest.ServerIntegrationInstance server, EntityUid hunter, EntityUid caster,
        EntityCoordinates target)
    {
        var entMan = server.EntMan;
        var gun = entMan.GetComponent<GunComponent>(caster);
        gun.NextFire = server.ResolveDependency<IGameTiming>().CurTime;
        gun.ShootCoordinates = target;
        var shot = entMan.System<SharedGunSystem>().AttemptShoot(hunter, (caster, gun));
        Assert.That(shot, Is.Not.Null.And.Count.EqualTo(1), "Test setup: the caster must fire.");
    }

    private static FixedPoint2 Damage(IEntityManager entMan, EntityUid uid) =>
        entMan.GetComponent<DamageableComponent>(uid).TotalDamage;

    /// <summary>Spawns a projectile and sends it east, the way a gun would, so its max range is measured from here.</summary>
    private static EntityUid Launch(IEntityManager entMan, string proto, EntityCoordinates at, EntityUid shooter)
    {
        var projectile = entMan.SpawnEntity(proto, at);
        entMan.GetComponent<ProjectileComponent>(projectile).Shooter = shooter;
        var physics = entMan.System<SharedPhysicsSystem>();
        var body = entMan.GetComponent<PhysicsComponent>(projectile);
        physics.SetBodyStatus(projectile, body, BodyStatus.InAir);
        physics.SetLinearVelocity(projectile, new Vector2(20, 0), body: body);
        return projectile;
    }

    private static int CountNear(IEntityManager entMan, string proto, MapCoordinates around, float range)
    {
        var count = 0;
        foreach (var uid in entMan.System<EntityLookupSystem>().GetEntitiesInRange(around, range))
        {
            if (entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == proto)
                count++;
        }

        return count;
    }

    // cmss13 arrow/do_at_max_range drops the arrow's handful_type: the plain arrow, the dynamic arrow, the snare
    [TestCase("CMUYautjaArrowProjectile", "CMUYautjaArrow", 14f)]
    [TestCase("CMUYautjaDynamicArrowProjectile", "CMUYautjaDynamicArrow", 14f)]
    [TestCase("CMUYautjaSnareArrowProjectile", "CMUYautjaSnareArrow", 7f)]
    public async Task ArrowRunningOutOfRangeLandsAsItsArrowLikeCmss13(string projectile, string item, float range)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Floor(map, entMan, 20);
            var shooter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(-0.5f, -2.5f)));
            Launch(entMan, projectile, map.GridCoords.Offset(new Vector2(0.5f, 0.5f)), shooter);
        });

        await pair.RunTicksSync(120);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var end = new MapCoordinates(new Vector2(0.5f + range, 0.5f), map.MapId);
            Assert.That(CountNear(entMan, item, end, 1.5f), Is.EqualTo(1),
                $"{projectile} leaves one {item} where it ran out of range.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ArrowThatHitsSomethingBeforeItsRangeDropsNothingThere()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Floor(map, entMan, 20);
            var shooter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(-0.5f, -2.5f)));
            entMan.SpawnEntity("CMWallMetal", map.GridCoords.Offset(new Vector2(5.5f, 0.5f)));
            Launch(entMan, "CMUYautjaArrowProjectile", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)), shooter);
        });

        await pair.RunTicksSync(120);

        await server.WaitAssertion(() =>
        {
            Assert.That(CountNear(server.EntMan, "CMUYautjaArrow", new MapCoordinates(new Vector2(8, 0.5f), map.MapId), 12f),
                Is.EqualTo(0), "Only running out of range is a max-range drop.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ExplosiveArrowDetonatesAtMaxRangeLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid bystander = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Floor(map, entMan, 20);
            var shooter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(-0.5f, -2.5f)));
            bystander = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(14.5f, 1.5f)));
            Launch(entMan, "CMUYautjaExplosiveArrowProjectile", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)), shooter);
        });

        await pair.RunTicksSync(120);

        await server.WaitAssertion(() =>
        {
            Assert.That(Damage(server.EntMan, bystander), Is.GreaterThan(FixedPoint2.Zero),
                "cmss13 arrow/expl/do_at_max_range: the explosive arrow goes off where it runs out of range.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ImmobilizerStunsTheAreaAtMaxRangeLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid near = default;
        EntityUid far = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Floor(map, entMan, 30);
            var shooter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(-0.5f, -2.5f)));
            // The sphere's 20-tile range ends beside this one; the far control is well outside its stun radius.
            near = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(20.5f, 2.5f)));
            far = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0.5f, -40.5f)));
            Launch(entMan, "CMUYautjaCasterImmobilizerBolt", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)), shooter);
        });

        await pair.RunTicksSync(120);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<StunnedComponent>(near), Is.True,
                    "cmss13 aoe_stun/do_at_max_range: the sphere stuns around the point it ran out of range.");
                Assert.That(entMan.HasComponent<StunnedComponent>(far), Is.False, "Control: nobody far away is stunned.");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EradicatorShotIntoAWallDetonatesLikeCmss13OnHitTurf()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid bystander = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Floor(map, entMan, 8);
            var (hunter, caster) = ArmHunter(entMan, map.GridCoords.Offset(new Vector2(0.5f, 0.5f)), EradicatorMode);
            entMan.SpawnEntity("CMWallMetal", map.GridCoords.Offset(new Vector2(4.5f, 0.5f)));
            // Beside the impact, out of the bolt's path: only the blast can reach them.
            bystander = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(3.5f, 1.5f)));
            Fire(server, hunter, caster, map.GridCoords.Offset(new Vector2(7.5f, 0.5f)));
        });

        await pair.RunTicksSync(90);

        await server.WaitAssertion(() =>
        {
            Assert.That(Damage(server.EntMan, bystander), Is.GreaterThan(FixedPoint2.Zero),
                "An eradicator that strikes a wall explodes in front of it (cmss13 aoe_lethal/on_hit_turf).");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EradicatorShotIntoOpenGroundDetonatesAtMaxRangeLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid bystander = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Floor(map, entMan, 30);
            var (hunter, caster) = ArmHunter(entMan, map.GridCoords.Offset(new Vector2(0.5f, 0.5f)), EradicatorMode);
            // Off the bolt's line, where its 20-tile range runs out: only a range-end blast reaches them.
            bystander = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(20.5f, 2.5f)));
            Fire(server, hunter, caster, map.GridCoords.Offset(new Vector2(28.5f, 0.5f)));
        });

        await pair.RunTicksSync(240);

        await server.WaitAssertion(() =>
        {
            Assert.That(Damage(server.EntMan, bystander), Is.GreaterThan(FixedPoint2.Zero),
                "An eradicator that reaches its range detonates (cmss13 aoe_lethal/do_at_max_range).");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DeletingAShooterlessEradicatorDoesNotDetonate()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid bystander = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            bystander = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var bolt = entMan.SpawnEntity("CMUYautjaCasterEradicatorBolt", map.GridCoords.Offset(new Vector2(0.5f, 0)));
            entMan.DeleteEntity(bolt);
        });

        await pair.RunTicksSync(20);

        await server.WaitAssertion(() =>
        {
            Assert.That(Damage(server.EntMan, bystander), Is.EqualTo(FixedPoint2.Zero),
                "Only a bolt that was actually fired detonates when it goes away.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DualCannonLanceDetonatesAgainstAWallLikeCmss13()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid bystander = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Floor(map, entMan, 8);
            var shooter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(-0.5f, -2.5f)));
            entMan.SpawnEntity("CMWallMetal", map.GridCoords.Offset(new Vector2(4.5f, 0.5f)));
            bystander = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(3.5f, 1.5f)));

            var lance = entMan.SpawnEntity("CMUYautjaCasterLanceBolt", map.GridCoords.Offset(new Vector2(1.5f, 0.5f)));
            entMan.GetComponent<ProjectileComponent>(lance).Shooter = shooter;
            var physics = entMan.System<SharedPhysicsSystem>();
            var body = entMan.GetComponent<PhysicsComponent>(lance);
            physics.SetBodyStatus(lance, body, BodyStatus.InAir);
            physics.SetLinearVelocity(lance, new Vector2(20, 0), body: body);
        });

        await pair.RunTicksSync(90);

        await server.WaitAssertion(() =>
        {
            Assert.That(Damage(server.EntMan, bystander), Is.GreaterThan(FixedPoint2.Zero),
                "The plasma lance explodes on terrain too (cmss13 lance/on_hit_turf), unlike the caster's single-lethal bolt.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DualCannonLanceGibsAHumanItHits()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var shooter = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(-3, 0)));
            var human = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var lance = entMan.SpawnEntity("CMUYautjaCasterLanceBolt", map.GridCoords);
            var projectile = entMan.GetComponent<ProjectileComponent>(lance);
            var hit = new ProjectileHitEvent(new DamageSpecifier(projectile.Damage), human, shooter);
            entMan.EventBus.RaiseLocalEvent(lance, ref hit);

            Assert.That(entMan.Deleted(human) || entMan.IsQueuedForDeletion(human), Is.True,
                "cmss13 lance on_hit_mob: ex_act(300) on a strict human gibs them.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TargetingLaserCoversDualCannonsAndDropsWhenDownedOrCrit()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid hunter = default;
        EntityUid caster = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var casters = entMan.System<YautjaCasterSystem>();
            (hunter, caster) = ArmHunter(entMan, map.GridCoords, 0);
            var cannons = entMan.SpawnEntity("CMUYautjaDualPlasmaCannons", map.GridCoords);

            Assert.Multiple(() =>
            {
                Assert.That(casters.CanAimSight(hunter, caster), Is.True, "Control: a standing hunter aims the caster.");
                Assert.That(casters.IsSightWeapon(cannons), Is.True, "The dual cannons project the same laser.");
                Assert.That(casters.CanAimSight(hunter, cannons), Is.True);
            });

            var aim = entMan.EnsureComponent<CMUYautjaCasterAimComponent>(hunter);
            aim.Caster = caster;
            aim.ExpiresAt = server.ResolveDependency<IGameTiming>().CurTime + TimeSpan.FromSeconds(30);
            entMan.System<SharedStunSystem>().TryKnockdown(hunter, TimeSpan.FromSeconds(10), true);

            Assert.That(casters.CanAimSight(hunter, caster), Is.False, "A downed hunter cannot hold the laser up.");
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(entMan.HasComponent<CMUYautjaCasterAimComponent>(hunter), Is.False,
                "The laser other players see goes out as soon as the hunter is downed.");

            entMan.System<MobStateSystem>().ChangeMobState(hunter, MobState.Critical);
            Assert.That(entMan.System<YautjaCasterSystem>().CanAimSight(hunter, caster), Is.False,
                "A critical hunter cannot aim the laser.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DecapitatedPlayerStaysInTheirBodyNotTheHead()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var minds = entMan.System<SharedMindSystem>();
            var session = server.ResolveDependency<IPlayerManager>().Sessions.Single();
            var victim = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var mindId = minds.CreateMind(session.UserId);
            minds.TransferTo(mindId, victim);

            Assert.That(entMan.System<CMUMedicalBodyIndexSystem>().TryGetBodyPart(victim,
                new CMUMedicalBodyPartKey(BodyPartType.Head, BodyPartSymmetry.None), out var head), Is.True);
            var sever = new BodyPartSeverAttemptEvent(victim, head, BodyPartType.Head);
            entMan.EventBus.RaiseLocalEvent(head, ref sever, broadcast: true);
            Assert.That(sever.Succeeded, Is.True, "Test setup: the head must come off.");

            Assert.That(minds.TryGetMind(victim, out var bodyMind, out _), Is.True,
                "cmss13 keeps a decapitated player in their own body, not alive inside a loose head.");
            Assert.That(bodyMind, Is.EqualTo(mindId.Owner));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PathogenCannotRaiseAYautjaAsAWalker()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            entMan.SpawnEntity("CMUPathogenHive", map.GridCoords);
            var yautja = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            entMan.EnsureComponent<YautjaComponent>(yautja);
            entMan.System<MobStateSystem>().ChangeMobState(yautja, MobState.Dead);
            var xeno = entMan.SpawnEntity("CMU14XenoNeomorph", map.GridCoords);
            entMan.EnsureComponent<CMUXenoMycotoxinInjectComponent>(xeno);

            var action = new CMUXenoMycotoxinInjectActionEvent { Performer = xeno, Target = yautja };
            entMan.EventBus.RaiseLocalEvent(xeno, action);
            Assert.That(action.Handled, Is.False, "The reanimate channel refuses a yautja corpse up front.");

            entMan.EventBus.RaiseLocalEvent(yautja, new CMUMycotoxinInjectDoReanimateEvent(yautja, xeno), broadcast: true);
            Assert.That(entMan.HasComponent<CMUPathogenWalkerComponent>(yautja), Is.False,
                "A yautja is never turned into a walker.");
        });

        await pair.CleanReturnAsync();
    }
}
