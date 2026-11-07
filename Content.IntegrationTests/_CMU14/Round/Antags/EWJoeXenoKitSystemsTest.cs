using System.Collections.Generic;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Xenonids.Leap;
using Content.Shared._RMC14.Xenonids.Lunge;
using Content.Shared._RMC14.Xenonids.Projectile.Spit.Charge;
using Content.Shared.CMU14.Dropship.Cas;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using NewStatusEffectsSystem = Content.Shared.StatusEffectNew.StatusEffectsSystem;

namespace Content.IntegrationTests._CMU14.Round.Antags;

/// <summary>
/// ECS-03 (node N10): the anti-xeno kit, one test per ability. Each checks the effect on a xeno and, for
/// the xeno-only effects, that a human is left alone. Not covered here: 1A's Scour do-after path (the
/// public <see cref="EWJoeScourSystem.ScourTorch"/> is used), 5A's pheromone strip (with no emitter RMC
/// clears receiver components on its own, so a test could not tell the strip apart), and the machete's
/// Machine Grip forward (owned by the grab node's tests).
/// </summary>
[TestFixture]
public sealed class EWJoeXenoKitSystemsTest
{
    private const string ColonyJoe = "AU14MobWorkingJoeColony";
    private const string Human = "CMMobHuman";
    private const string Xeno = "CMXenoDrone";
    private const string ArmoredXeno = "CMXenoDefender";
    private const string Weeds = "XenoWeeds";
    private const string ResinWall = "WallXenoResin";
    private const string Welder = "CMWelder";

    [Test]
    public async Task AcidPlatingReducesXenoAcidAndShedsItEarly()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid plated = default, bare = default;
        await server.WaitAssertion(() =>
        {
            var protos = server.ResolveDependency<IPrototypeManager>();
            var damageable = entities.System<DamageableSystem>();
            plated = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            entities.EnsureComponent<EWJoeComponent>(plated);
            entities.EnsureComponent<EWJoeAcidPlatingComponent>(plated);
            bare = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            entities.EnsureComponent<EWJoeComponent>(bare);
            var xeno = entities.SpawnEntity(Xeno, EWJoeTestHelpers.TileEast(entities, map, 2));
            var human = entities.SpawnEntity(Human, EWJoeTestHelpers.TileEast(entities, map, -2));

            var acid = new DamageSpecifier(protos.Index<DamageTypePrototype>("Heat"), 20);

            damageable.TryChangeDamage(plated, acid, origin: xeno);
            damageable.TryChangeDamage(bare, acid, origin: xeno);
            var platedAcid = damageable.GetTotalDamage(plated);
            var bareAcid = damageable.GetTotalDamage(bare);

            damageable.TryChangeDamage(plated, acid, origin: human);
            damageable.TryChangeDamage(bare, acid, origin: human);
            var platedHuman = damageable.GetTotalDamage(plated) - platedAcid;
            var bareHuman = damageable.GetTotalDamage(bare) - bareAcid;

            Assert.Multiple(() =>
            {
                Assert.That(bareAcid, Is.GreaterThan(FixedPoint2.Zero), "xeno acid burns a bare casing");
                Assert.That(platedAcid, Is.LessThan(bareAcid), "the plating takes less from xeno acid");
                Assert.That(platedHuman, Is.EqualTo(bareHuman), "a person's heat is not reduced");
            });

            entities.EnsureComponent<UserAcidedComponent>(plated);
            entities.EnsureComponent<UserAcidedComponent>(bare);
        });

        // the plating sheds acid after 5 s; acid on a bare casing lasts its full 20 s
        await pair.RunTicksSync((int) Math.Ceiling(6.5 / timing.TickPeriod.TotalSeconds));

        await server.WaitAssertion(() =>
        {
            Assert.That(entities.HasComponent<UserAcidedComponent>(plated), Is.False, "the plating shed the acid");
            Assert.That(entities.HasComponent<UserAcidedComponent>(bare), "the bare casing still burns");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CuttingTorchBurnsXenofaunaAndBreaksResin()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        EntityUid wall = default;
        await server.WaitAssertion(() =>
        {
            var scour = entities.System<EWJoeScourSystem>();
            var damageable = entities.System<DamageableSystem>();
            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            entities.EnsureComponent<EWJoeComponent>(joe);
            var welder = entities.SpawnEntity(Welder, map.GridCoords);

            Assert.That(scour.IsTorchFood(joe, welder), Is.False, "no torch without the level-1 grant");
            entities.EnsureComponent<EWJoeCuttingTorchComponent>(joe);
            Assert.That(scour.IsTorchFood(joe, welder), "a welder feeds the Cutting Torch");
            Assert.That(scour.ScourTorch(joe, welder), "the welder is scoured");
            Assert.That(scour.IsTorchLit(joe), "the torch is lit");

            var xeno = entities.SpawnEntity(Xeno, EWJoeTestHelpers.TileEast(entities, map, 1));
            var human = entities.SpawnEntity(Human, EWJoeTestHelpers.TileEast(entities, map, -1));
            wall = entities.SpawnEntity(ResinWall, EWJoeTestHelpers.TileEast(entities, map, 2));

            var hit = new MeleeHitEvent(new List<EntityUid> { xeno, human, wall }, joe, joe, new DamageSpecifier(), null);
            scour.ApplyCuttingTorch(joe, hit);

            Assert.Multiple(() =>
            {
                Assert.That(damageable.GetTotalDamage(xeno), Is.GreaterThan(FixedPoint2.Zero), "the torch burns the xeno");
                Assert.That(damageable.GetTotalDamage(human), Is.EqualTo(FixedPoint2.Zero), "and not the person");
            });
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entities.Deleted(wall), "the resin wall broke in one hit");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RendingEdgePiercesXenofaunaArmorOnly()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            entities.EnsureComponent<EWJoeComponent>(joe);
            var edge = entities.EnsureComponent<EWJoeRendingEdgeComponent>(joe);
            var machete = entities.SpawnEntity(null, map.GridCoords);
            entities.EnsureComponent<EWJoeMacheteComponent>(machete).Joe = joe;
            var xeno = entities.SpawnEntity(Xeno, map.GridCoords);
            var human = entities.SpawnEntity(Human, map.GridCoords);

            var onXeno = new CMGetArmorPiercingEvent(xeno);
            entities.EventBus.RaiseLocalEvent(machete, ref onXeno);
            var onHuman = new CMGetArmorPiercingEvent(human);
            entities.EventBus.RaiseLocalEvent(machete, ref onHuman);

            Assert.Multiple(() =>
            {
                Assert.That(onXeno.Piercing, Is.EqualTo(edge.ArmorPiercing), "the edge rends xeno armor");
                Assert.That(onHuman.Piercing, Is.Zero, "a person's armor is untouched");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OverclockSteadyRefusesPounceLungeAndXenoKnockdown()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var kit = entities.System<EWJoeXenoKitSystem>();
            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            entities.EnsureComponent<EWJoeComponent>(joe);
            entities.EnsureComponent<EWJoeSteadyComponent>(joe);
            var human = entities.SpawnEntity(Human, EWJoeTestHelpers.TileEast(entities, map, -1));
            var xeno = entities.SpawnEntity(Xeno, EWJoeTestHelpers.TileEast(entities, map, 1));

            var idle = new XenoLeapHitAttempt(xeno);
            entities.EventBus.RaiseLocalEvent(joe, ref idle);
            Assert.That(idle.Cancelled, Is.False, "without Overclock the pounce lands");

            entities.EnsureComponent<EWJoeOverclockComponent>(joe);
            Assert.That(kit.IsSteady(joe), "Overclock is running with Steady");

            var leap = new XenoLeapHitAttempt(xeno);
            entities.EventBus.RaiseLocalEvent(joe, ref leap);
            var lunge = new XenoLungeHitAttempt(xeno);
            entities.EventBus.RaiseLocalEvent(joe, ref lunge);
            var knockdown = new KnockDownAttemptEvent(true, true, TimeSpan.FromSeconds(2));
            entities.EventBus.RaiseLocalEvent(joe, ref knockdown);

            Assert.Multiple(() =>
            {
                Assert.That(leap.Cancelled, "the pounce is refused");
                Assert.That(lunge.Cancelled, "the lunge is refused");
                Assert.That(knockdown.Cancelled, "a knockdown beside a xeno is refused");
            });

            // with only a person nearby, Steady has no reason to refuse a knockdown
            entities.DeleteEntity(xeno);
            Assert.That(kit.XenofaunaNear(joe, entities.GetComponent<EWJoeSteadyComponent>(joe).XenoProximity), Is.False,
                "the human is not xenofauna");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PurgeGroundClearsWeedsAndBlocksRegrowth()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        EntityUid weeds = default, joe = default;
        await server.WaitAssertion(() =>
        {
            var kit = entities.System<EWJoeXenoKitSystem>();
            joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            entities.EnsureComponent<EWJoeComponent>(joe);
            weeds = entities.SpawnEntity(Weeds, map.GridCoords);

            Assert.That(kit.TryPurgeGround(joe), Is.EqualTo(-1), "no purge without the level-4 grant");
            entities.EnsureComponent<EWJoePurgeGroundComponent>(joe);
            Assert.That(kit.TryPurgeGround(joe), Is.EqualTo(1), "the weeds under the Joe are purged");
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var xforms = entities.System<SharedTransformSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(entities.Deleted(weeds), "the weeds are gone");
                Assert.That(entities.System<CMUCasFalloutSystem>().IsWeedKilled(xforms.GetMapCoordinates(joe)),
                    "and cannot grow back for now");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UnwindRecoilReflectsXenoMeleeOnly()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var kit = entities.System<EWJoeXenoKitSystem>();
            var protos = server.ResolveDependency<IPrototypeManager>();
            var damageable = entities.System<DamageableSystem>();
            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            entities.EnsureComponent<EWJoeComponent>(joe);
            entities.EnsureComponent<EWJoeUnwindRecoilComponent>(joe);
            var xeno = entities.SpawnEntity(Xeno, EWJoeTestHelpers.TileEast(entities, map, 1));
            var human = entities.SpawnEntity(Human, EWJoeTestHelpers.TileEast(entities, map, -1));
            var claw = new DamageSpecifier(protos.Index<DamageTypePrototype>("Slash"), 40);

            damageable.TryChangeDamage(joe, claw, origin: xeno, tool: xeno);
            Assert.That(damageable.GetTotalDamage(xeno), Is.EqualTo(FixedPoint2.Zero), "no recoil before Unwind");

            Assert.That(kit.StartUnwindRecoil(joe), "Unwind opens the recoil window");
            damageable.TryChangeDamage(joe, claw, origin: xeno, tool: xeno);
            damageable.TryChangeDamage(joe, claw, origin: human, tool: human);

            Assert.Multiple(() =>
            {
                Assert.That(damageable.GetTotalDamage(xeno), Is.GreaterThan(FixedPoint2.Zero), "the claw is thrown back");
                Assert.That(damageable.GetTotalDamage(human), Is.EqualTo(FixedPoint2.Zero), "a person's blow is not");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SearingLightBurnsXenofaunaOnly()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();

        await server.WaitAssertion(() =>
        {
            var kit = entities.System<EWJoeXenoKitSystem>();
            var damageable = entities.System<DamageableSystem>();
            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            entities.EnsureComponent<EWJoeComponent>(joe);
            var searing = entities.EnsureComponent<EWJoeSearingLightComponent>(joe);
            var xeno = entities.SpawnEntity(Xeno, EWJoeTestHelpers.TileEast(entities, map, 2));
            var human = entities.SpawnEntity(Human, EWJoeTestHelpers.TileEast(entities, map, -2));

            var seared = kit.Sear((joe, searing), 7f, timing.CurTime);

            Assert.Multiple(() =>
            {
                Assert.That(seared, Is.EqualTo(1), "only the xeno is seared");
                Assert.That(damageable.GetTotalDamage(xeno), Is.GreaterThan(FixedPoint2.Zero), "the light burns it");
                Assert.That(entities.HasComponent<EWJoeSearedComponent>(xeno), "and cuts it off from pheromones");
                Assert.That(damageable.GetTotalDamage(human), Is.EqualTo(FixedPoint2.Zero), "the person is not burnt");
                Assert.That(entities.HasComponent<EWJoeSearedComponent>(human), Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SaturateDazesAndShredsXenofaunaOnly()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var kit = entities.System<EWJoeXenoKitSystem>();
            var status = entities.System<NewStatusEffectsSystem>();
            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            entities.EnsureComponent<EWJoeComponent>(joe);
            var xeno = entities.SpawnEntity(ArmoredXeno, map.GridCoords);
            var human = entities.SpawnEntity(Human, map.GridCoords);

            Assert.That(kit.SaturateXeno(joe, xeno), Is.False, "no xeno branch without the level-5 grant");
            var saturate = entities.EnsureComponent<EWJoeSaturateXenoComponent>(joe);

            var before = new CMGetArmorEvent(SlotFlags.OUTERCLOTHING | SlotFlags.INNERCLOTHING);
            entities.EventBus.RaiseLocalEvent(xeno, ref before);

            Assert.That(kit.SaturateXeno(joe, xeno), "the xeno is saturated");
            Assert.That(kit.SaturateXeno(joe, human), Is.False, "a person is not");

            var after = new CMGetArmorEvent(SlotFlags.OUTERCLOTHING | SlotFlags.INNERCLOTHING);
            entities.EventBus.RaiseLocalEvent(xeno, ref after);

            Assert.Multiple(() =>
            {
                Assert.That(before.XenoArmor, Is.GreaterThan(0), "a defender has armor to lose");
                Assert.That(status.HasStatusEffect(xeno, RMCDazedSystem.StatusEffectDazed), "the xeno is dazed");
                Assert.That(status.HasStatusEffect(human, RMCDazedSystem.StatusEffectDazed), Is.False, "the person is not");
                Assert.That(after.XenoArmor, Is.EqualTo(Math.Max(0, before.XenoArmor - saturate.ArmorStrip)),
                    "the xeno's armor is shredded");
            });
        });

        await pair.CleanReturnAsync();
    }
}
