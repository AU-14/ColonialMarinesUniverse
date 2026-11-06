using System.Linq;
using Content.Server.CMU14.Round.Antags.EarlyWarning;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Server.Decals;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Smoke;
using Content.Shared._RMC14.StatusEffect;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared.Actions.Components;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Weapons.GunSwitch;
using Content.Shared.CMU14.Weapons.Safety;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Doors.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Movement.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._CMU14.Round.Antags;

/// <summary>
/// Phase E5 (with the playtest-3 changes): Heat and Evolution, Overclock, Emergency Coolant, the Grand
/// Machine's Hand, Consecrate, Unwind, the Z-leap heat cost and Disrupt-II.
/// </summary>
[TestFixture]
public sealed class EWJoeHeatAbilitiesTest
{
    private const string ColonyJoe = "AU14MobWorkingJoeColony";
    private const string Human = "CMMobHuman";
    private const string Rifle = "AU14WeaponRifleM41AMK2Tactical";
    private const string Airlock = "Airlock";
    private const string Firelock = "Firelock";
    private const string BloodDecal = "CMUBloodDroplet1";

    [Test]
    public async Task HeatOverheatsRefusesSpendingAndCoolsDown()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid joe = default;
        await server.WaitAssertion(() =>
        {
            var heat = entities.System<EWJoeHeatSystem>();
            joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            entities.EnsureComponent<EWJoeComponent>(joe);
            var comp = entities.EnsureComponent<EWJoeHeatComponent>(joe);

            Assert.That(heat.TrySpendHeat(joe, 25), "a cold core pays");
            Assert.That(comp.Heat, Is.EqualTo(25f).Within(0.01f));

            heat.AddHeat((joe, comp), 100);
            Assert.That(comp.Overheated, "reaching the maximum latches the overheat");
            Assert.That(heat.TrySpendHeat(joe, 10), Is.False, "an overheated core refuses to pay");

            // the latch holds until heat falls below the clear line
            comp.Heat = comp.OverheatClear + 1;
        });

        await pair.RunTicksSync((int) Math.Ceiling(1.5 / timing.TickPeriod.TotalSeconds));

        await server.WaitAssertion(() =>
        {
            var comp = entities.GetComponent<EWJoeHeatComponent>(joe);
            Assert.That(comp.Heat, Is.LessThan(comp.OverheatClear + 1), "heat decays passively");
            Assert.That(comp.Overheated, Is.False, "the overheat clears below the line");
            Assert.That(entities.System<EWJoeHeatSystem>().TrySpendHeat(joe, 10), "and paying works again");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EvolutionArrivesWithLevelThreeAndLeavesOnStandDown()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var rule = entities.System<EWJoeRuleSystem>();
            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);
            var slow = entities.GetComponent<RMCSpeciesSlowdownModifierComponent>(joe);
            var originalSlow = slow.SlowModifier;

            rule.ApplyLevel((joe, comp), 2);
            Assert.That(entities.HasComponent<EWJoeHeatComponent>(joe), Is.False, "no heat before level 3");

            rule.ApplyLevel((joe, comp), 3);
            Assert.That(entities.TryGetComponent(joe, out EWJoeHeatComponent? heat), "level 3 grants the heat gauge");
            Assert.Multiple(() =>
            {
                Assert.That(heat!.EvolutionApplied);
                Assert.That(entities.GetComponent<RMCStunResistanceComponent>(joe).Resistance, Is.EqualTo(heat.EvolutionStunResistance));
                Assert.That(slow.SlowModifier, Is.EqualTo(heat.EvolutionSlowModifier));
                Assert.That(heat.RestoreSlowModifier, Is.EqualTo(originalSlow));
            });

            // stand-down clears the grants; the heat component's shutdown puts Evolution back
            entities.RemoveComponent<EWJoeComponent>(joe);
            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<EWJoeHeatComponent>(joe), Is.False, "heat gauge removed with the grants");
                Assert.That(slow.SlowModifier, Is.EqualTo(originalSlow), "slow modifier restored");
                Assert.That(entities.GetComponent<RMCStunResistanceComponent>(joe).Resistance, Is.Not.EqualTo(heat!.EvolutionStunResistance), "stun resistance restored");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OverclockSpeedResetsWhenItEndsAndWhenTheCoreOverheats()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid joe = default;
        var baseSprint = 0f;
        await server.WaitAssertion(() =>
        {
            joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            entities.EnsureComponent<EWJoeComponent>(joe);
            entities.EnsureComponent<EWJoeHeatComponent>(joe);
            var speed = entities.GetComponent<MovementSpeedModifierComponent>(joe);
            baseSprint = speed.SprintSpeedModifier;

            var toggle = new EWJoeOverclockActionEvent { Performer = joe };
            entities.EventBus.RaiseLocalEvent(joe, toggle);
            Assert.That(toggle.Handled);
            Assert.That(entities.HasComponent<EWJoeOverclockComponent>(joe), "overclock running");
            Assert.That(speed.SprintSpeedModifier, Is.EqualTo(baseSprint * 1.5f).Within(0.01f), "overclock speeds the Joe up");

            // turning it off must drop the bonus at once (a refresh during shutdown used to keep it)
            var off = new EWJoeOverclockActionEvent { Performer = joe };
            entities.EventBus.RaiseLocalEvent(joe, off);
            Assert.That(entities.HasComponent<EWJoeOverclockComponent>(joe), Is.False);
            Assert.That(speed.SprintSpeedModifier, Is.EqualTo(baseSprint).Within(0.01f), "speed back to normal when it ends");

            var on = new EWJoeOverclockActionEvent { Performer = joe };
            entities.EventBus.RaiseLocalEvent(joe, on);
        });

        await pair.RunTicksSync((int) Math.Ceiling(1.5 / timing.TickPeriod.TotalSeconds));

        await server.WaitAssertion(() =>
        {
            var heat = entities.GetComponent<EWJoeHeatComponent>(joe);
            var speed = entities.GetComponent<MovementSpeedModifierComponent>(joe);
            Assert.That(heat.Heat, Is.GreaterThan(0f), "overclock builds heat");
            Assert.That(entities.Count<EvenSmokeComponent>(), Is.GreaterThan(0), "smoke was spawned");

            // force the overheat: overclock dies with it, and its speed with it
            entities.System<EWJoeHeatSystem>().AddHeat((joe, heat), heat.Max);
            Assert.That(heat.Overheated);
            Assert.That(entities.HasComponent<EWJoeOverclockComponent>(joe), Is.False, "overheat forces overclock off");
            Assert.That(speed.SprintSpeedModifier, Is.LessThan(baseSprint), "no overclock bonus left, only the overheat slow");

            var toggle = new EWJoeOverclockActionEvent { Performer = joe };
            entities.EventBus.RaiseLocalEvent(joe, toggle);
            Assert.That(entities.HasComponent<EWJoeOverclockComponent>(joe), Is.False, "and it cannot be restarted while overheated");
        });

        // the overheat slow lasts 4 s; afterwards the Joe moves at its normal speed again
        await pair.RunTicksSync((int) Math.Ceiling(5 / timing.TickPeriod.TotalSeconds));

        await server.WaitAssertion(() =>
        {
            var speed = entities.GetComponent<MovementSpeedModifierComponent>(joe);
            Assert.That(speed.SprintSpeedModifier, Is.EqualTo(baseSprint).Within(0.01f), "the overheat slow ends cleanly");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CoolantAbsorbsBloodDecalsAndPuddles()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var heatSystem = entities.System<EWJoeHeatSystem>();
            var decals = entities.System<DecalSystem>();
            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);
            var heat = entities.EnsureComponent<EWJoeHeatComponent>(joe);

            Assert.That(heatSystem.Coolant((joe, comp)), Is.EqualTo(0f), "nothing to absorb yet");

            Assert.That(decals.TryAddDecal(BloodDecal, map.GridCoords, out var decalId), "blood decal placed");
            var puddle = entities.SpawnEntity(EWJoeHeatSystem.BloodPuddlePrototype, map.GridCoords);
            heat.Heat = 50f;

            var cooled = heatSystem.Coolant((joe, comp));
            Assert.Multiple(() =>
            {
                Assert.That(cooled, Is.GreaterThanOrEqualTo(comp.CoolantPerDecal), "the decal alone is worth five");
                Assert.That(heat.Heat, Is.EqualTo(Math.Max(0f, 50f - cooled)).Within(0.01f));
                Assert.That(decals.GetDecalsInRange(map.Grid.Owner, map.GridCoords.Position, comp.CoolantRange).Any(d => d.Index == decalId), Is.False, "the decal is gone");
                Assert.That(entities.IsQueuedForDeletion(puddle) || entities.Deleted(puddle), "the puddle is gone");
            });

            // the cap: ten decals would give fifty, one use sheds forty and leaves the rest of the blood
            for (var i = 0; i < 10; i++)
            {
                decals.TryAddDecal(BloodDecal, map.GridCoords, out _);
            }

            heat.Heat = 100f;
            var capped = heatSystem.Coolant((joe, comp));
            Assert.Multiple(() =>
            {
                Assert.That(capped, Is.EqualTo(comp.CoolantCap).Within(0.01f), "one use sheds at most the cap");
                Assert.That(heat.Heat, Is.EqualTo(100f - comp.CoolantCap).Within(0.01f));
                Assert.That(decals.GetDecalsInRange(map.Grid.Owner, map.GridCoords.Position, comp.CoolantRange).Count, Is.EqualTo(2),
                    "absorption stops once the cap is met");
            });

            // a cool core takes nothing
            heat.Heat = 0f;
            Assert.That(heatSystem.Coolant((joe, comp)), Is.EqualTo(0f));
            Assert.That(decals.GetDecalsInRange(map.Grid.Owner, map.GridCoords.Position, comp.CoolantRange).Count, Is.EqualTo(2),
                "no blood is wasted on a cool core");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MacheteIsSummonedIntoAHandAndDismissed()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        EntityUid joe = default, machete = default;
        await server.WaitAssertion(() =>
        {
            var arsenal = entities.System<EWJoeArsenalSystem>();
            var hands = entities.System<SharedHandsSystem>();
            joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);

            // the colony loadout fills a hand; empty them so the summon has somewhere to go
            foreach (var held in hands.EnumerateHeld(joe).ToArray())
            {
                hands.TryDrop(joe, held);
            }

            var summon = new EWJoeMacheteActionEvent { Performer = joe };
            entities.EventBus.RaiseLocalEvent(joe, summon);
            Assert.That(summon.Handled);
            Assert.That(arsenal.FindMachete(joe), Is.Not.Null, "machete in hand");
            machete = arsenal.FindMachete(joe)!.Value;
            Assert.That(entities.GetComponent<EWJoeMacheteComponent>(machete).Joe, Is.EqualTo(joe));

            var dismiss = new EWJoeMacheteActionEvent { Performer = joe };
            entities.EventBus.RaiseLocalEvent(joe, dismiss);
        });

        await pair.RunTicksSync(3);

        await server.WaitAssertion(() =>
        {
            Assert.That(entities.Deleted(machete), "dismissed machete deleted");
            Assert.That(entities.System<EWJoeArsenalSystem>().FindMachete(joe), Is.Null);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ConsecrateChainsThroughNearbyBodiesAndCostsHeat()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var arsenal = entities.System<EWJoeArsenalSystem>();
            var damageable = entities.System<DamageableSystem>();
            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);
            var heat = entities.EnsureComponent<EWJoeHeatComponent>(joe);
            var first = entities.SpawnEntity(Human, map.GridCoords);
            var second = entities.SpawnEntity(Human, map.GridCoords);

            var ev = new EWJoeConsecrateActionEvent { Performer = joe, Target = first };
            entities.EventBus.RaiseLocalEvent(joe, ev);
            Assert.Multiple(() =>
            {
                Assert.That(ev.Handled, "consecrate handled");
                Assert.That(heat.Heat, Is.EqualTo(comp.ConsecrateHeat).Within(0.01f), "the arc cost its heat");
                Assert.That(damageable.GetTotalDamage(first), Is.GreaterThan(FixedPoint2.Zero), "the clicked target burned");
                Assert.That(damageable.GetTotalDamage(second), Is.GreaterThan(FixedPoint2.Zero), "the arc jumped to the second body");
                Assert.That(damageable.GetTotalDamage(joe), Is.EqualTo(FixedPoint2.Zero), "the Joe is not in its own chain");
            });

            heat.Overheated = true;
            var refused = new EWJoeConsecrateActionEvent { Performer = joe, Target = first };
            entities.EventBus.RaiseLocalEvent(joe, refused);
            Assert.That(refused.Handled, Is.False, "an overheated core cannot consecrate");

            var self = new EWJoeConsecrateActionEvent { Performer = joe, Target = joe };
            entities.EventBus.RaiseLocalEvent(joe, self);
            Assert.That(self.Handled, Is.False, "the Joe cannot target itself");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UnwindReachesHeldSlungAndLooseGunsWithoutLockingTheSafety()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var arsenal = entities.System<EWJoeArsenalSystem>();
            var safety = entities.System<CMUGunSafetySystem>();
            var containers = entities.System<SharedContainerSystem>();
            var hands = entities.System<SharedHandsSystem>();
            var inventory = entities.System<InventorySystem>();
            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);
            var bystander = entities.SpawnEntity(Human, map.GridCoords);

            var held = entities.SpawnEntity(Rifle, map.GridCoords);
            Assert.That(hands.TryPickupAnyHand(bystander, held), "the bystander holds a rifle");
            var slung = entities.SpawnEntity(Rifle, map.GridCoords);
            Assert.That(inventory.TryEquip(bystander, slung, "back", silent: true, force: true), "and slings another on the back");
            var loose = entities.SpawnEntity(Rifle, map.GridCoords);

            var guns = new[] { held, slung, loose };
            foreach (var gun in guns)
            {
                Assert.That(containers.TryGetContainer(gun, SharedGunSystem.MagazineSlot, out var magazine), "rifle has a magazine slot");
                Assert.That(magazine!.ContainedEntities, Is.Not.Empty, "rifle spawns loaded");
                Assert.That(safety.IsOn(gun), Is.False, "safety off to begin with");
            }

            var (count, _) = arsenal.Unwind((joe, comp));
            Assert.That(count, Is.EqualTo(3), "held, slung and loose rifles are all unwound");

            foreach (var gun in guns)
            {
                containers.TryGetContainer(gun, SharedGunSystem.MagazineSlot, out var magazine);
                Assert.Multiple(() =>
                {
                    Assert.That(magazine!.ContainedEntities, Is.Empty, $"magazine ejected from {entities.ToPrettyString(gun)}");
                    Assert.That(safety.IsOn(gun), "safety switched on");
                    Assert.That(entities.TryGetComponent(gun, out GunJammedComponent? jam) && jam.RackFailChance == 0f && jam.RackTime == comp.UnwindRackTime, "chamber jammed, racks clear in one try");
                });
            }

            // playtest 3: no lock, the owner can flick it straight back
            safety.SetSafety(held, false, bystander);
            Assert.That(safety.IsOn(held), Is.False, "the safety is not locked");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ZLeapCostsHeatAndDisruptTwoOpensDoors()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid joe = default, door = default;
        await server.WaitAssertion(() =>
        {
            var rule = entities.System<EWJoeRuleSystem>();
            joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);
            var heat = entities.EnsureComponent<EWJoeHeatComponent>(joe);

            // playtest 3: the yautja leap runs on the Joe's own component, no YautjaComponent needed
            entities.EnsureComponent<CMUZLeapComponent>(joe);
            Assert.That(entities.HasComponent<YautjaComponent>(joe), Is.False);
            var action = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var actionComp = entities.EnsureComponent<ActionComponent>(action);

            // the action itself is free (a refused or broken windup must cost nothing) ...
            var leap = new YautjaLeapActionEvent
            {
                Performer = joe,
                Action = (action, actionComp),
                Target = map.GridCoords.Offset(new System.Numerics.Vector2(3, 0)),
            };
            entities.EventBus.RaiseLocalEvent(joe, leap);
            Assert.That(leap.Handled, "the leap started on a non-yautja");
            Assert.That(entities.GetComponent<DoAfterComponent>(joe).DoAfters.Values.Any(d => d.Args.Event is YautjaLeapDoAfterEvent),
                "the yautja windup is running");
            Assert.That(heat.Heat, Is.EqualTo(0f).Within(0.01f), "starting the windup costs nothing");

            // ... the completed windup is what is charged
            var windup = new YautjaLeapDoAfterEvent(entities.GetNetCoordinates(map.GridCoords.Offset(new System.Numerics.Vector2(3, 0))));
            windup.DoAfter = new DoAfter(0, new DoAfterArgs(entities, joe, TimeSpan.Zero, windup, joe), TimeSpan.Zero);
            entities.EventBus.RaiseLocalEvent(joe, windup);
            Assert.That(heat.Heat, Is.EqualTo(comp.ZLeapHeat).Within(0.01f), "the leap cost its heat on completion");
            Assert.That(entities.HasComponent<YautjaLeapingComponent>(joe), "and the Joe is in the air");

            heat.Overheated = true;
            var blocked = new YautjaLeapActionEvent
            {
                Performer = joe,
                Action = (action, actionComp),
                Target = map.GridCoords.Offset(new System.Numerics.Vector2(3, 0)),
            };
            entities.EventBus.RaiseLocalEvent(joe, blocked);
            Assert.That(blocked.Handled, "an overheated Joe cannot leap; the wrapper swallows the event");
            Assert.That(heat.Heat, Is.EqualTo(comp.ZLeapHeat).Within(0.01f), "nothing more was charged");
            heat.Overheated = false;

            // Disrupt-II: level 2 opens doors; level 1 does not
            door = entities.SpawnEntity(Airlock, map.GridCoords);
            Assert.That(entities.GetComponent<DoorComponent>(door).State, Is.EqualTo(DoorState.Closed));

            rule.ApplyLevel((joe, comp), 1);
            var weak = new EWJoeDisruptActionEvent { Performer = joe };
            entities.EventBus.RaiseLocalEvent(joe, weak);
            Assert.That(entities.GetComponent<DoorComponent>(door).State, Is.EqualTo(DoorState.Closed), "level 1 leaves doors alone");

            rule.ApplyLevel((joe, comp), 2);
            var pulse = new EWJoeDisruptActionEvent { Performer = joe };
            entities.EventBus.RaiseLocalEvent(joe, pulse);
            Assert.That(entities.GetComponent<DoorComponent>(door).State, Is.AnyOf(DoorState.Opening, DoorState.Open), "level 2 throws the door open");
            Assert.That(entities.HasComponent<EWJoeDoorHeldOpenComponent>(door), "and holds it");
            Assert.That(entities.GetComponent<EWJoeDoorHeldOpenComponent>(door).Bolted, Is.False, "an unpowered airlock could not be bolted, and expiry will not touch its bolts");

            // a firelock is never touched (upstream firelocks spawn open and close on a hazard; the pulse must change nothing)
            var firelock = entities.SpawnEntity(Firelock, map.GridCoords);
            var firelockState = entities.GetComponent<DoorComponent>(firelock).State;
            var again = new EWJoeDisruptActionEvent { Performer = joe };
            entities.EventBus.RaiseLocalEvent(joe, again);
            Assert.That(entities.GetComponent<DoorComponent>(firelock).State, Is.EqualTo(firelockState), "firelocks are left as they were");
            Assert.That(entities.HasComponent<EWJoeDoorHeldOpenComponent>(firelock), Is.False);

            entities.GetComponent<EWJoeDoorHeldOpenComponent>(door).Until = TimeSpan.Zero;
        });

        await pair.RunTicksSync((int) Math.Ceiling(1.5 / timing.TickPeriod.TotalSeconds));

        await server.WaitAssertion(() =>
        {
            Assert.That(entities.HasComponent<EWJoeDoorHeldOpenComponent>(door), Is.False, "the hold is released");
        });

        await pair.CleanReturnAsync();
    }
}
