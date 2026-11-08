using Content.Shared.Actions.Components;
using Content.Shared.Actions;
using System.Collections.Generic;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Xenonids.Leap;
using Content.Shared._RMC14.Xenonids.Lunge;
using Content.Shared._RMC14.Xenonids.Projectile.Spit.Charge;
using Content.Shared._RMC14.Xenonids.Sweep;
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

    /// <summary>
    /// FLOW-18 / RB-27: Steady refuses the pounce and the lunge outright, but a knockdown or paralyze only
    /// right after living xenofauna damaged the Joe or while a nearby one is mid tail attack
    /// (XenoSweepingComponent). A living drone beside it is no longer enough.
    /// </summary>
    [Test]
    public async Task OverclockSteadyRefusesPounceLungeAndXenoHitKnockdownOnly()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid joe = default;
        await server.WaitAssertion(() =>
        {
            var kit = entities.System<EWJoeXenoKitSystem>();
            var stun = entities.System<SharedStunSystem>();
            var damageable = entities.System<DamageableSystem>();
            var protos = server.ResolveDependency<IPrototypeManager>();
            var claw = new DamageSpecifier(protos.Index<DamageTypePrototype>("Slash"), 5);

            joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            entities.EnsureComponent<EWJoeComponent>(joe);
            entities.EnsureComponent<EWJoeSteadyComponent>(joe);
            var floored = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            entities.EnsureComponent<EWJoeComponent>(floored);
            entities.EnsureComponent<EWJoeSteadyComponent>(floored);
            entities.EnsureComponent<EWJoeOverclockComponent>(floored);
            var human = entities.SpawnEntity(Human, EWJoeTestHelpers.TileEast(entities, map, -1));
            var xeno = entities.SpawnEntity(Xeno, EWJoeTestHelpers.TileEast(entities, map, 1));

            var idle = new XenoLeapHitAttempt(xeno);
            entities.EventBus.RaiseLocalEvent(joe, ref idle);
            Assert.That(idle.Cancelled, Is.False, "without Overclock the pounce lands");

            entities.EnsureComponent<EWJoeOverclockComponent>(joe);
            Assert.That(kit.IsSteady(joe), "Overclock is running with Steady");
            Assert.That(kit.IsSteady(floored), "the second Joe is Steady too");

            var leap = new XenoLeapHitAttempt(xeno);
            entities.EventBus.RaiseLocalEvent(joe, ref leap);
            var lunge = new XenoLungeHitAttempt(xeno);
            entities.EventBus.RaiseLocalEvent(joe, ref lunge);

            // a living drone stands beside both Joes; only a person has hit this one
            damageable.TryChangeDamage(floored, claw, origin: human, tool: human);
            var otherKnockdown = stun.TryKnockdown(floored, TimeSpan.FromSeconds(2));

            // the drone's own hit is the tell: the knockdown and paralyze that come with it are refused
            damageable.TryChangeDamage(joe, claw, origin: xeno, tool: xeno);
            var xenoKnockdown = stun.TryKnockdown(joe, TimeSpan.FromSeconds(2));
            var xenoParalyze = stun.TryParalyze(joe, TimeSpan.FromSeconds(2), true);

            // a tail trip or lash deals no damage: the drone mid tail attack is the tell instead
            var swept = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            entities.EnsureComponent<EWJoeComponent>(swept);
            entities.EnsureComponent<EWJoeSteadyComponent>(swept);
            entities.EnsureComponent<EWJoeOverclockComponent>(swept);
            entities.EnsureComponent<XenoSweepingComponent>(xeno);
            var sweepKnockdown = stun.TryKnockdown(swept, TimeSpan.FromSeconds(2));
            entities.RemoveComponent<XenoSweepingComponent>(xeno);
            var afterSweepKnockdown = stun.TryKnockdown(swept, TimeSpan.FromSeconds(2));

            Assert.Multiple(() =>
            {
                Assert.That(leap.Cancelled, "the pounce is refused");
                Assert.That(lunge.Cancelled, "the lunge is refused");
                Assert.That(otherKnockdown, "a non-xeno knockdown beside a living drone still floors a Steady Joe");
                Assert.That(xenoKnockdown, Is.False, "a knockdown right after the drone's hit is refused");
                Assert.That(xenoParalyze, Is.False, "and so is a paralyze");
                Assert.That(sweepKnockdown, Is.False, "a knockdown while the drone is mid tail attack is refused");
                Assert.That(afterSweepKnockdown, "once the drone stops sweeping, with no xeno hit, the knockdown lands");
            });
        });

        // past the 0.25 s hit window the drone's old hit no longer covers the Joe
        await pair.RunTicksSync((int) Math.Ceiling(0.5 / timing.TickPeriod.TotalSeconds));

        await server.WaitAssertion(() =>
        {
            Assert.That(entities.System<EWJoeXenoKitSystem>().IsSteady(joe), "still Overclocked");
            Assert.That(entities.System<SharedStunSystem>().TryKnockdown(joe, TimeSpan.FromSeconds(2)),
                "a knockdown after the window lands");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>RB-28: one Overclock activation ends by itself at its 15 s cap, taking Steady with it.</summary>
    [Test]
    public async Task OverclockEndsAtItsCap()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid joe = default, quitter = default;
        EntityUid? joeAction = null, quitterAction = null;
        var cap = TimeSpan.Zero;
        await server.WaitAssertion(() =>
        {
            var actions = entities.System<SharedActionsSystem>();
            joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            entities.EnsureComponent<EWJoeSteadyComponent>(joe);
            var overclock = entities.EnsureComponent<EWJoeOverclockComponent>(joe);
            cap = overclock.MaxDuration;
            Assert.That(cap, Is.EqualTo(TimeSpan.FromSeconds(15)));
            joeAction = actions.AddAction(joe, "CMUActionEWJoeOverclock");
            overclock.Action = joeAction;

            // a second Joe that switches it off by hand after a few seconds
            quitter = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            quitterAction = actions.AddAction(quitter, "CMUActionEWJoeOverclock");
            entities.EnsureComponent<EWJoeOverclockComponent>(quitter).Action = quitterAction;
        });

        await pair.RunTicksSync((int) Math.Ceiling(3 / timing.TickPeriod.TotalSeconds));

        await server.WaitAssertion(() =>
        {
            // RB-28: toggling off by hand recharges for as long as it ran, so off-and-on cannot keep it up
            entities.RemoveComponent<EWJoeOverclockComponent>(quitter);
            var cooldown = entities.GetComponent<ActionComponent>(quitterAction!.Value).Cooldown;
            Assert.That(cooldown, Is.Not.Null, "a manual stop starts a recharge");
            Assert.That((cooldown!.Value.End - timing.CurTime).TotalSeconds, Is.EqualTo(3).Within(0.5), "as long as it ran");
        });

        await pair.RunTicksSync((int) Math.Ceiling((cap.TotalSeconds - 4) / timing.TickPeriod.TotalSeconds));

        await server.WaitAssertion(() =>
        {
            Assert.That(entities.HasComponent<EWJoeOverclockComponent>(joe), "still running just short of the cap");
        });

        await pair.RunTicksSync((int) Math.Ceiling(2 / timing.TickPeriod.TotalSeconds));

        await server.WaitAssertion(() =>
        {
            Assert.That(entities.HasComponent<EWJoeOverclockComponent>(joe), Is.False, "the cap throttled it back");
            Assert.That(entities.System<EWJoeXenoKitSystem>().IsSteady(joe), Is.False, "and Steady with it");
            var cooldown = entities.GetComponent<ActionComponent>(joeAction!.Value).Cooldown;
            Assert.That(cooldown, Is.Not.Null, "the cap starts a recharge");
            Assert.That((cooldown!.Value.End - timing.CurTime).TotalSeconds, Is.GreaterThan(cap.TotalSeconds - 2),
                "of the full run, so it cannot be switched straight back on");
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
