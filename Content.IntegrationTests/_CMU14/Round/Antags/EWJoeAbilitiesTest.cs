using System.Linq;
using System.Numerics;
using Content.Server.CMU14.Round.Antags.EarlyWarning;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Fireman;
using Content.Shared._RMC14.Stamina;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._CMU14.Round.Antags;

/// <summary>
/// Phase E4: Drown, the Sculpted Hands moves, Repentance and Judgement, plus Scour's buffs.
/// </summary>
[TestFixture]
public sealed class EWJoeAbilitiesTest
{
    private const string ColonyJoe = "AU14MobWorkingJoeColony";
    private const string Human = "CMMobHuman";
    private const string Wall = "WallSolid";
    private const string Baton = "CMStunbaton";
    private const string Armor = "CMArmorM3Medium";
    private const string FuelCan = "RMCFuelCan";

    [Test]
    public async Task DrownNeedsAnAggressiveGrabAndLeavesTheVictimAsleepAndDeadIc()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid joe = default, victim = default, synth = default;
        await server.WaitAssertion(() =>
        {
            var combat = entities.System<EWJoeCombatSystem>();
            joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);
            victim = entities.SpawnEntity(Human, map.GridCoords);
            synth = entities.SpawnEntity(ColonyJoe, map.GridCoords);

            Assert.That(combat.CanDrown((joe, comp), victim, out var reason), Is.False, "no grab yet");
            Assert.That(reason, Is.EqualTo("cmu-ew-joe-needs-grab"));
            Assert.That(combat.CanDrown((joe, comp), synth, out reason), Is.False);
            Assert.That(reason, Is.EqualTo("cmu-ew-joe-drown-synthetic"), "synthetics do not breathe");
        });

        Assert.That(await EWJoeTestHelpers.AggressiveGrab(pair, joe, victim), "aggressive grab established");

        await server.WaitAssertion(() =>
        {
            var combat = entities.System<EWJoeCombatSystem>();
            var damageable = entities.System<DamageableSystem>();
            var protos = server.ResolveDependency<IPrototypeManager>();
            var status = entities.System<StatusEffectsSystem>();
            var comp = entities.GetComponent<EWJoeComponent>(joe);

            Assert.That(combat.CanDrown((joe, comp), victim, out _), "grabbed organic can be drowned");

            // hurt the Joe first so the heal is visible
            damageable.TryChangeDamage(joe, new DamageSpecifier(protos.Index<DamageTypePrototype>("Blunt"), 100));
            var before = damageable.GetTotalDamage(joe);

            combat.Drown((joe, comp), victim);

            Assert.Multiple(() =>
            {
                Assert.That(status.HasStatusEffect(victim, "StatusEffectForcedSleeping"), "victim forced asleep");
                Assert.That(entities.HasComponent<IsDeadICComponent>(victim), "victim counts as dead for objectives");
                Assert.That(damageable.GetTotalDamage(joe), Is.LessThan(before), "the Joe healed");
            });

            // the dead-IC flag is tied to the sleep; expire it early
            entities.GetComponent<EWJoeDrownedComponent>(victim).Until = TimeSpan.Zero;
        });

        await pair.RunTicksSync((int) Math.Ceiling(1.5 / timing.TickPeriod.TotalSeconds));

        await server.WaitAssertion(() =>
        {
            Assert.That(entities.HasComponent<IsDeadICComponent>(victim), Is.False, "dead-IC lifted when the drowning wears off");
            Assert.That(entities.HasComponent<EWJoeDrownedComponent>(victim), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SculptedHandsSweepSlamChokeAndThrow()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid joe = default, victim = default, wall = default;
        await server.WaitAssertion(() =>
        {
            var combat = entities.System<EWJoeCombatSystem>();
            joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);
            victim = entities.SpawnEntity(Human, map.GridCoords);

            // sweep needs no grab
            var sweep = new EWJoeSweepActionEvent { Performer = joe, Target = victim };
            entities.EventBus.RaiseLocalEvent(joe, sweep);
            Assert.That(sweep.Handled, "sweep handled");
            Assert.That(entities.HasComponent<KnockedDownComponent>(victim), "victim knocked down");

            // slam and throw refuse without a grab; a person is not a slam obstacle
            Assert.That(combat.IsSlamObstacle(victim), Is.False, "a person is not an obstacle");
            var throwEv = new EWJoeThrowActionEvent { Performer = joe, Target = victim };
            entities.EventBus.RaiseLocalEvent(joe, throwEv);
            Assert.That(throwEv.Handled, Is.False, "throw refused without a grab");

            // a wall east of the victim (the test grid is one tile, so give it a tile first)
            var maps = entities.System<SharedMapSystem>();
            var victimTile = maps.CoordinatesToTile(map.Grid.Owner, map.Grid.Comp, entities.GetComponent<TransformComponent>(victim).Coordinates);
            var wallTile = victimTile + new Vector2i(1, 0);
            maps.SetTile(map.Grid, wallTile, map.Tile.Tile);
            wall = entities.SpawnEntity(Wall, maps.GridTileToLocal(map.Grid.Owner, map.Grid.Comp, wallTile));
            Assert.That(combat.IsSlamObstacle(wall), "a wall is an obstacle");

            var slam = new EWJoeSlamActionEvent { Performer = joe, Target = wall };
            entities.EventBus.RaiseLocalEvent(joe, slam);
            Assert.That(slam.Handled, Is.False, "slam refused without a grab");
        });

        Assert.That(await EWJoeTestHelpers.AggressiveGrab(pair, joe, victim), "aggressive grab established");

        await server.WaitAssertion(() =>
        {
            // choke drains stamina every second while the grab holds
            var choke = new EWJoeChokeActionEvent { Performer = joe };
            entities.EventBus.RaiseLocalEvent(joe, choke);
            Assert.That(entities.HasComponent<EWJoeChokeComponent>(joe), "choke running");
        });

        await pair.RunTicksSync((int) Math.Ceiling(2.5 / timing.TickPeriod.TotalSeconds));

        await server.WaitAssertion(() =>
        {
            var stamina = entities.GetComponent<RMCStaminaComponent>(victim);
            Assert.That(stamina.Current, Is.LessThan(100), "choke drained stamina");

            // throw releases the grab and ends the choke
            var throwEv = new EWJoeThrowActionEvent { Performer = joe, Target = victim };
            entities.EventBus.RaiseLocalEvent(joe, throwEv);
            Assert.That(throwEv.Handled, "throw handled");
            Assert.That(entities.HasComponent<EWJoeChokeComponent>(joe), Is.False, "choke ended by the throw");
            Assert.That(entities.System<FiremanCarrySystem>().IsAggressivelyGrabbed(victim), Is.False, "grab released");
        });

        // slam: grab again, then hurl the victim into the chosen wall. The throw may have carried the
        // victim off the one-tile grid, so put it back beside the Joe first.
        await pair.RunTicksSync((int) Math.Ceiling(1.5 / timing.TickPeriod.TotalSeconds));
        await server.WaitAssertion(() =>
        {
            entities.System<SharedPhysicsSystem>().SetLinearVelocity(victim, Vector2.Zero);
            entities.System<SharedTransformSystem>().SetCoordinates(victim, map.GridCoords);
        });
        Assert.That(await EWJoeTestHelpers.AggressiveGrab(pair, joe, victim), "second aggressive grab established");

        await server.WaitAssertion(() =>
        {
            var damageable = entities.System<DamageableSystem>();
            var comp = entities.GetComponent<EWJoeComponent>(joe);

            var before = damageable.GetTotalDamage(victim);
            var slam = new EWJoeSlamActionEvent { Performer = joe, Target = wall };
            entities.EventBus.RaiseLocalEvent(joe, slam);
            Assert.That(slam.Handled, "slam handled with a grab and a wall");
            Assert.That(damageable.GetTotalDamage(victim), Is.GreaterThanOrEqualTo(before + comp.SlamDamage - 5), "the flat slam damage landed");
            Assert.That(entities.System<FiremanCarrySystem>().IsAggressivelyGrabbed(victim), Is.False, "the victim was let go to fly");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ScourBuffsApplyAndExpire()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid joe = default;
        await server.WaitAssertion(() =>
        {
            var scourSys = entities.System<EWJoeScourSystem>();
            var hands = entities.System<SharedHandsSystem>();
            joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);
            comp.ScourDuration = TimeSpan.FromSeconds(3);
            comp.ScourArmorDuration = TimeSpan.FromSeconds(3);

            var baton = entities.SpawnEntity(Baton, map.GridCoords);
            var armor = entities.SpawnEntity(Armor, map.GridCoords);
            var fuel = entities.SpawnEntity(FuelCan, map.GridCoords);
            var human = entities.SpawnEntity(Human, map.GridCoords);

            Assert.Multiple(() =>
            {
                Assert.That(scourSys.Classify(baton), Is.EqualTo(EWJoeScourKind.Baton));
                Assert.That(scourSys.Classify(armor), Is.EqualTo(EWJoeScourKind.Armor));
                Assert.That(scourSys.Classify(fuel), Is.EqualTo(EWJoeScourKind.Fuel));
                Assert.That(scourSys.Classify(human), Is.Null, "a person is not food");
            });

            // the action takes gear lying within reach; a person is refused
            var scourAction = new EWJoeScourActionEvent { Performer = joe, Target = baton };
            entities.EventBus.RaiseLocalEvent(joe, scourAction);
            Assert.That(scourAction.Handled, "scour starts on an item on the floor");
            var badScour = new EWJoeScourActionEvent { Performer = joe, Target = human };
            entities.EventBus.RaiseLocalEvent(joe, badScour);
            Assert.That(badScour.Handled, Is.False, "scour refuses a person");

            scourSys.Scour((joe, comp), baton, EWJoeScourKind.Baton);
            scourSys.Scour((joe, comp), armor, EWJoeScourKind.Armor);
            var scour = entities.GetComponent<EWJoeScourComponent>(joe);
            Assert.That(scour.PunchStamina, Is.EqualTo(comp.ScourPunchStamina));
            Assert.That(scour.BulletArmor, Is.EqualTo(comp.ScourBulletArmor));

            // the armor bonus reaches the shared armor event
            var ev = new CMGetArmorEvent();
            entities.EventBus.RaiseLocalEvent(joe, ref ev);
            Assert.That(ev.Bullet, Is.EqualTo(comp.Level * comp.BulletArmorPerLevel + comp.ScourBulletArmor), "bullet armor includes the Scour bonus");
            Assert.That(comp.ScourBulletArmor, Is.EqualTo(5), "RB-03: digested armor gives +5 bullet armor");
        });

        await pair.RunTicksSync((int) Math.Ceiling(4.5 / timing.TickPeriod.TotalSeconds));

        await server.WaitAssertion(() =>
        {
            var comp = entities.GetComponent<EWJoeComponent>(joe);
            Assert.That(entities.HasComponent<EWJoeScourComponent>(joe), Is.False, "buff expired");
            var ev = new CMGetArmorEvent();
            entities.EventBus.RaiseLocalEvent(joe, ref ev);
            Assert.That(ev.Bullet, Is.EqualTo(comp.Level * comp.BulletArmorPerLevel), "bonus gone");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Rebalance RB-04: a fuel can mends the Joe at 2 HP/s for 45 s, and the repair stops for 5 s after the
    /// Joe takes damage. The pause does not extend the window.
    /// </summary>
    [Test]
    public async Task FuelScourRepairPausesAfterDamage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();
        var protos = server.ResolveDependency<IPrototypeManager>();

        EntityUid joe = default;
        FixedPoint2 hurt = default;
        await server.WaitAssertion(() =>
        {
            var scourSys = entities.System<EWJoeScourSystem>();
            var damageable = entities.System<DamageableSystem>();
            joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);

            Assert.Multiple(() =>
            {
                Assert.That(comp.ScourRepairPerSecond, Is.EqualTo(2f), "RB-04: 2 HP/s");
                Assert.That(comp.ScourRepairDuration, Is.EqualTo(TimeSpan.FromSeconds(45)), "RB-04: for 45 s");
                Assert.That(comp.ScourRepairDamagePause, Is.EqualTo(TimeSpan.FromSeconds(5)), "RB-04: paused 5 s by damage");
                Assert.That(comp.ScourArmorDuration, Is.EqualTo(TimeSpan.FromMinutes(3)), "RB-03: body armor lasts 3 min");
                Assert.That(comp.ScourDuration, Is.EqualTo(TimeSpan.FromMinutes(5)), "baton and radio keep 5 min");
            });

            // hurt before the buff exists, so this damage does not pause anything
            damageable.TryChangeDamage(joe, new DamageSpecifier(protos.Index<DamageTypePrototype>("Blunt"), 60));
            scourSys.Scour((joe, comp), entities.SpawnEntity(FuelCan, map.GridCoords), EWJoeScourKind.Fuel);
            hurt = damageable.GetTotalDamage(joe);
            Assert.That(hurt, Is.GreaterThan(FixedPoint2.Zero), "the Joe is hurt");
        });

        await pair.RunTicksSync((int) Math.Ceiling(2.5 / timing.TickPeriod.TotalSeconds));

        FixedPoint2 paused = default;
        await server.WaitAssertion(() =>
        {
            var damageable = entities.System<DamageableSystem>();
            Assert.That(damageable.GetTotalDamage(joe), Is.LessThan(hurt), "the fuel mends the Joe");

            // a fresh hit pauses the repair
            damageable.TryChangeDamage(joe, new DamageSpecifier(protos.Index<DamageTypePrototype>("Blunt"), 10));
            paused = damageable.GetTotalDamage(joe);
            Assert.That(entities.GetComponent<EWJoeScourComponent>(joe).RepairPausedUntil, Is.GreaterThan(timing.CurTime),
                "damage pushes the pause forward");
        });

        await pair.RunTicksSync((int) Math.Ceiling(3 / timing.TickPeriod.TotalSeconds));

        await server.WaitAssertion(() =>
        {
            Assert.That(entities.System<DamageableSystem>().GetTotalDamage(joe), Is.EqualTo(paused),
                "no repair while paused");
        });

        await pair.RunTicksSync((int) Math.Ceiling(3.5 / timing.TickPeriod.TotalSeconds));

        await server.WaitAssertion(() =>
        {
            Assert.That(entities.System<DamageableSystem>().GetTotalDamage(joe), Is.LessThan(paused),
                "the repair resumes once the pause is over");
        });

        await pair.CleanReturnAsync();
    }
}
