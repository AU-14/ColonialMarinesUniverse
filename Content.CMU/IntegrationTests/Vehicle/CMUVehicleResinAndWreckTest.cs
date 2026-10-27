#pragma warning disable RA0002 // Arrange damage protection and collision state at the system boundary.

using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.Vehicle;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.Vehicle;

[TestFixture]
public sealed class CMUVehicleResinAndWreckTest : GameTest
{
    [Test]
    public async Task ResinContactRollsOnceAndClearingRestoresBothDriveDirections()
    {
        var map = await Pair.CreateTestMap();
        EntityUid vehicle = default;
        float forward = 0;
        float reverse = 0;
        await Server.WaitAssertion(() =>
        {
            vehicle = SEntMan.SpawnEntity("VehicleHumvee", map.GridCoords);
            var wall = SEntMan.SpawnEntity("WallXenoResinThick", map.GridCoords.Offset(new Vector2(0.5f, 0)));
            var resin = SEntMan.GetComponent<CMUVehicleStickyResinComponent>(wall);
            var mover = SEntMan.GetComponent<GridVehicleMoverComponent>(vehicle);
            mover.CurrentSpeed = 2;
            forward = Speed(false);
            reverse = Speed(true);
            var system = SEntMan.System<GridVehicleMoverSystem>();
            var collision = typeof(GridVehicleMoverSystem).GetMethod("CanOccupyTransform", BindingFlags.Instance | BindingFlags.NonPublic)!;
            void Contact(bool effects) => collision.Invoke(system,
                [vehicle, mover, map.Grid.Owner, Server.Transform(vehicle).LocalPosition, Angle.Zero, 0f, effects, false, null, null, null, null]);

            resin.Chance = 1;
            Contact(false);
            Assert.That(SEntMan.HasComponent<CMUVehicleResinComponent>(vehicle), Is.False,
                "An occupancy probe must not roll resin fouling.");
            resin.Chance = 0;
            Contact(true);
            resin.Chance = 1;
            for (var i = 0; i < 10; i++) Contact(true);
            Assert.That(SEntMan.HasComponent<CMUVehicleResinComponent>(vehicle), Is.False,
                "Holding against the same wall must not reroll a failed chance.");

            var transform = SEntMan.System<SharedTransformSystem>();
            transform.SetCoordinates(vehicle, map.GridCoords.Offset(new Vector2(5, 0)));
            transform.SetCoordinates(vehicle, map.GridCoords);
            mover.CurrentSpeed = 2;
            Contact(true);
            Assert.That(Speed(false), Is.EqualTo(forward * 0.4f).Within(0.001f));
            Assert.That(Speed(true), Is.EqualTo(reverse * 0.4f).Within(0.001f));

            mover.CurrentSpeed = 0;
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1.2f, 0)));
            var fouled = SEntMan.GetComponent<CMUVehicleResinComponent>(vehicle);
            var cancelled = new CMUVehicleClearResinDoAfterEvent();
            cancelled.DoAfter = new DoAfter(0, new DoAfterArgs(SEntMan, user, TimeSpan.Zero,
                cancelled, vehicle, vehicle), SGameTiming.CurTime);
            cancelled.DoAfter.CancelledTime = SGameTiming.CurTime;
            SEntMan.EventBus.RaiseLocalEvent(vehicle, cancelled);
            Assert.That(fouled.Fouled, Is.True, "An interrupted clearing attempt must leave the slowdown in place.");

            SEntMan.DeleteEntity(wall);
            fouled.ClearTime = TimeSpan.FromSeconds(0.2);
            var verbs = new GetVerbsEvent<InteractionVerb>(user, vehicle, null, null, true, true, true, []);
            SEntMan.EventBus.RaiseLocalEvent(vehicle, verbs);
            verbs.Verbs.Single(v => v.Text == Loc.GetString("cmu-vehicle-resin-clear")).Act!();
            Assert.That(SEntMan.GetComponent<DoAfterComponent>(user).DoAfters.Count, Is.EqualTo(1),
                "The outside clearing verb must start a real do-after.");
        });
        await Pair.RunTicksSync(30);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<CMUVehicleResinComponent>(vehicle).Fouled, Is.False);
            Assert.That(Speed(false), Is.EqualTo(forward).Within(0.001f));
            Assert.That(Speed(true), Is.EqualTo(reverse).Within(0.001f));
        });

        float Speed(bool reversing) => (float) typeof(GridVehicleMoverSystem)
            .GetMethod(reversing ? "GetModifiedMaxReverseSpeed" : "GetModifiedMaxSpeed", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(SEntMan.System<GridVehicleMoverSystem>(), [vehicle, SEntMan.GetComponent<GridVehicleMoverComponent>(vehicle)])!;
    }

    [TestCase("VehicleHumvee")]
    [TestCase("CMUVehicleAPCWolfpack")]
    public async Task LastProtectedModuleCanReachZeroAndPendingRepairsCannotReviveTheHull(string prototype)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var vehicle = SEntMan.SpawnEntity(prototype, map.GridCoords);
            var hardpoints = SEntMan.System<HardpointSystem>();
            var parts = SEntMan.System<VehicleTopologySystem>().GetMountedSlots(vehicle)
                .Where(slot => slot.Item != null).Select(slot => slot.Item!.Value).Distinct().ToArray();
            var protectedPart = parts.First(part => SEntMan.HasComponent<HardpointItemComponent>(part));
            SEntMan.GetComponent<HardpointItemComponent>(protectedPart).DamageMultiplier = 0;
            foreach (var part in parts.Where(part => part != protectedPart))
                hardpoints.DamageHardpoint(vehicle, part, SEntMan.GetComponent<HardpointIntegrityComponent>(part).Integrity);

            var frame = SEntMan.GetComponent<HardpointIntegrityComponent>(vehicle);
            var remaining = SEntMan.GetComponent<HardpointIntegrityComponent>(protectedPart);
            Assert.That(frame.Integrity, Is.GreaterThan(0));
            var hit = new DamageSpecifier();
            hit.DamageDict["Blunt"] = 20;
            var damage = SEntMan.System<DamageableSystem>();
            for (var i = 0; i < 1000 && frame.Integrity > 0; i++)
                damage.TryChangeDamage(vehicle, hit);
            Assert.That(frame.Integrity, Is.Zero, "Protected launchers must not hold the hull above zero forever.");
            Assert.That(frame.DestroyedBeyondRepair, Is.True);

            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var welder = SEntMan.SpawnEntity("RMCWelderPVE", map.GridCoords);
            SEntMan.System<ItemToggleSystem>().TryActivate(welder, user);
            var repair = new HardpointRepairDoAfterEvent { RepairAmount = 50 };
            repair.DoAfter = new DoAfter(0, new DoAfterArgs(SEntMan, user, TimeSpan.Zero,
                repair, protectedPart, vehicle, welder), SGameTiming.CurTime);
            SEntMan.EventBus.RaiseLocalEvent(protectedPart, repair);
            Assert.That(remaining.Integrity, Is.Zero, "Even a repair queued before destruction must fail.");
            Assert.That(frame.Integrity, Is.Zero);
            var canRun = new VehicleCanRunEvent((vehicle, SEntMan.GetComponent<VehicleComponent>(vehicle)));
            SEntMan.EventBus.RaiseLocalEvent(vehicle, ref canRun);
            Assert.That(canRun.CanRun, Is.False);
        });
    }
}
