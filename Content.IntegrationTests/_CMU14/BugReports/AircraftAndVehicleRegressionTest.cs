#pragma warning disable RA0002 // Arrange pilot state and attack sources at the system boundary.

using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Blackfoot;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared._RMC14.Vehicle;
using Content.Shared._RMC14.Xenonids.ClawSharpness;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.BugReports;

[TestFixture]
public sealed class AircraftAndVehicleRegressionTest : GameTest
{
    [TestCase(MobState.Critical)]
    [TestCase(MobState.Dead)]
    public async Task LexingtonRejectsActionsAndHeldFlightInputFromIncapacitatedPilot(MobState state)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var hull = SEntMan.SpawnEntity(null, map.GridCoords);
            var vehicle = SEntMan.EnsureComponent<VehicleComponent>(hull);
            var mover = SEntMan.EnsureComponent<GridVehicleMoverComponent>(hull);
            var flight = SEntMan.EnsureComponent<BlackfootFlightComponent>(hull);
            flight.State = BlackfootFlightState.Flight;
            var pilot = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            vehicle.Operator = pilot;
            var actions = SEntMan.EnsureComponent<BlackfootPilotActionComponent>(pilot);
            actions.Vehicle = hull;
            SEntMan.EnsureComponent<InputMoverComponent>(pilot).HeldMoveButtons = MoveButtons.Up;
            var door = SEntMan.EnsureComponent<BlackfootRearDoorComponent>(hull);
            var aliveAction = new BlackfootRearDoorToggleActionEvent { Performer = pilot };
            SEntMan.EventBus.RaiseLocalEvent(pilot, aliveAction);
            Assert.That(aliveAction.Handled, Is.True);
            Assert.That(door.Open, Is.True);
            SEntMan.System<MobStateSystem>().ChangeMobState(pilot, state);
            var action = new BlackfootRearDoorToggleActionEvent { Performer = pilot };
            SEntMan.EventBus.RaiseLocalEvent(pilot, action);
            Assert.That(action.Handled, Is.False);
            Assert.That(door.Open, Is.True);
            var getInput = typeof(GridVehicleMoverSystem).GetMethod("GetMoverInput", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var input = getInput.Invoke(SEntMan.System<GridVehicleMoverSystem>(),
                [hull, mover, vehicle, map.GridCoords.EntityId, false])!;
            Assert.That(input.GetType().GetProperty("Throttle")!.GetValue(input), Is.EqualTo(0f));
            Assert.That(input.GetType().GetProperty("Steering")!.GetValue(input), Is.EqualTo(0f));
            Assert.That(input.GetType().GetProperty("Direction")!.GetValue(input), Is.EqualTo(Vector2i.Zero));
            SEntMan.DeleteEntity(pilot);
            SEntMan.DeleteEntity(hull);
        });
    }

    [Test]
    public async Task AntivehicleProjectileMultiplierIsAppliedExactlyOnce()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var hull = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.EnsureComponent<HardpointSlotsComponent>(hull);
            var damageable = SEntMan.EnsureComponent<DamageableComponent>(hull);
            SEntMan.EnsureComponent<InjurableComponent>(hull);
            var rocket = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.EnsureComponent<VehicleDamageMultiplierComponent>(rocket).Multiplier = 3;
            var damage = new DamageSpecifier();
            damage.DamageDict["Blunt"] = 10;
            var hit = new ProjectileHitEvent(damage, hull);
            SEntMan.EventBus.RaiseLocalEvent(rocket, ref hit);
            SEntMan.System<DamageableSystem>().TryChangeDamage(hull, hit.Damage, tool: rocket);
            Assert.That(damageable.TotalDamage.Float(), Is.EqualTo(30f));
            SEntMan.DeleteEntity(hull);
            SEntMan.DeleteEntity(rocket);
        });
    }

    [TestCase(XenoClawType.Normal, 0)]
    [TestCase(XenoClawType.Sharp, 48)]
    [TestCase(XenoClawType.VerySharp, 48)]
    public async Task VehicleClawGatePreservesRealAttackDamage(XenoClawType strength, float expected)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var hull = SEntMan.SpawnEntity(null, map.GridCoords);
            var receiver = SEntMan.EnsureComponent<ReceiverXenoClawsComponent>(hull);
            receiver.MinimumClawStrength = XenoClawType.Sharp;
            receiver.UseWeaponDamage = true;
            var damageable = SEntMan.EnsureComponent<DamageableComponent>(hull);
            SEntMan.EnsureComponent<InjurableComponent>(hull);
            var claws = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.EnsureComponent<MeleeWeaponComponent>(claws);
            SEntMan.EnsureComponent<XenoClawsComponent>(claws).ClawType = strength;
            var damage = new DamageSpecifier();
            damage.DamageDict["Slash"] = 48;
            SEntMan.System<DamageableSystem>().TryChangeDamage(hull, damage, tool: claws);
            Assert.That(damageable.TotalDamage.Float(), Is.EqualTo(expected));
            SEntMan.DeleteEntity(hull);
            SEntMan.DeleteEntity(claws);
        });
    }
}
