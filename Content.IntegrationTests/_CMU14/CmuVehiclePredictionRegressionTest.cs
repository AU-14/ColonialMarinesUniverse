#nullable enable annotations
#pragma warning disable RA0002 // Arrange authoritative vehicle/operator state without performing gameplay setup.

using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Robust.Client.GameStates;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14;

[TestFixture]
public sealed class CmuVehiclePredictionRegressionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: CMUVehiclePredictionChassis
          components:
          - type: Vehicle
            transferDamage: false
            requiresHands: false

        - type: entity
          id: CMUVehiclePredictionTurret
          components:
          - type: VehicleTurret
            rotateToCursor: true
            rotationSpeed: 90
            reverseDirectionDelay: 0.5

        - type: entity
          id: CMUVehiclePredictionSurface
          components:
          - type: VehicleRideSurface
            bounds:
            - -2,-2,2,2
        """;

    [Test]
    public async Task PredictiveAimAndReversalAreRestoredAndReplayedBeforeServerReply()
    {
        var map = await Pair.CreateTestMap();
        NetEntity turretNet = default;
        await Server.WaitAssertion(() =>
        {
            var vehicle = SEntMan.SpawnEntity("CMUVehiclePredictionChassis", map.GridCoords);
            var turret = SEntMan.SpawnEntity("CMUVehiclePredictionTurret", map.GridCoords);
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var containers = Server.System<SharedContainerSystem>();
            Assert.That(containers.Insert(turret, containers.EnsureContainer<ContainerSlot>(vehicle, "turret")), Is.True);
            var operatorComp = SEntMan.EnsureComponent<VehicleWeaponsOperatorComponent>(user);
            operatorComp.Vehicle = vehicle;
            operatorComp.SelectedWeapon = turret;
            SEntMan.Dirty(user, operatorComp);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, user);
            turretNet = SEntMan.GetNetEntity(turret);
        });
        await Pair.RunUntilSynced();

        await Client.WaitAssertion(() =>
        {
            var turret = CEntMan.GetEntity(turretNet);
            var component = CEntMan.GetComponent<VehicleTurretComponent>(turret);
            var turrets = Client.System<VehicleTurretSystem>();
            var transform = Client.System<SharedTransformSystem>();
            var states = (ClientGameStateManager) Client.ResolveDependency<IClientGameStateManager>();
            Assert.That(turrets.TryGetTurretOrigin(turret, out var origin), Is.True);
            var originMap = transform.ToMapCoordinates(origin);
            // Robust world angles use south as zero, so east is 90 degrees and southwest is -45.
            var forward = transform.ToCoordinates(originMap.Offset(new Vector2(4, 0)));
            var reverse = transform.ToCoordinates(originMap.Offset(new Vector2(-4, -4)));
            var tick = CGameTiming.CurTick;

            CEntMan.RaisePredictiveEvent(new VehicleTurretRotateEvent
            {
                Turret = turretNet,
                Coordinates = CEntMan.GetNetCoordinates(forward),
            });
            AssertAngle(component.TargetRotation, 90, "A predictive event must change local aim before any server reply.");
            Assert.That(component.LastAppliedDirectionSign, Is.EqualTo(1));

            CEntMan.RaisePredictiveEvent(new VehicleTurretRotateEvent
            {
                Turret = turretNet,
                Coordinates = CEntMan.GetNetCoordinates(reverse),
            });
            Assert.That(component.PendingTargetRotation, Is.Not.Null);
            AssertAngle(component.PendingTargetRotation!.Value, -45);
            AssertAngle(component.TargetRotation, 90, "Reversing must retain the current target until the delay elapses.");
            var deadline = component.PendingTargetApplyAt;
            Assert.That(deadline, Is.GreaterThan(CGameTiming.CurTime));
            Assert.That(component.PendingDirectionSign, Is.EqualTo(-1));

            states.ResetPredictedEntities();
            Assert.Multiple(() =>
            {
                AssertAngle(component.TargetRotation, 0);
                Assert.That(component.PendingTargetRotation, Is.Null, "Rollback must discard the unacknowledged reversal.");
                Assert.That(component.PendingTargetApplyAt, Is.EqualTo(TimeSpan.Zero));
                Assert.That(component.PendingDirectionSign, Is.Zero);
                Assert.That(component.LastAppliedDirectionSign, Is.Zero);
            });

            // Replay the queued commands through the engine, without delivering either packet to the server.
            CGameTiming.CurTick = tick - 1;
            states.PredictTicks(tick);
            AssertAngle(component.TargetRotation, 90);
            Assert.That(component.PendingTargetRotation, Is.Not.Null);
            AssertAngle(component.PendingTargetRotation!.Value, -45);
            Assert.Multiple(() =>
            {
                Assert.That(component.PendingTargetApplyAt, Is.EqualTo(deadline), "Replay must preserve the original reversal deadline.");
                Assert.That(component.PendingDirectionSign, Is.EqualTo(-1));
                Assert.That(component.LastAppliedDirectionSign, Is.EqualTo(1));
            });
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task AuthoritativeRiderStateRestoresCarryWithoutAClimb(bool attachAfterState)
    {
        var (vehicleNet, riderNet, otherNet) = await CreateAuthoritativeRider(attachAfterState);
        if (attachAfterState)
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession!, SEntMan.GetEntity(riderNet)));
            await Pair.RunUntilSynced();
        }

        await Client.WaitAssertion(() => AssertCarries(vehicleNet, riderNet, Vector2.UnitX));

        // A new authoritative membership must remove the old vehicle's index entry as well as add the new one.
        await Server.WaitPost(() =>
        {
            var rider = SEntMan.GetEntity(riderNet);
            var other = SEntMan.GetEntity(otherNet);
            var component = SEntMan.GetComponent<VehicleRideSurfaceRiderComponent>(rider);
            component.Vehicle = other;
            component.LocalPosition = Vector2.Zero;
            Server.System<SharedTransformSystem>().SetCoordinates(rider, SEntMan.GetComponent<TransformComponent>(other).Coordinates);
            SEntMan.Dirty(rider, component);
        });
        await Pair.RunUntilSynced();

        await Client.WaitAssertion(() =>
        {
            var transform = Client.System<SharedTransformSystem>();
            var rider = CEntMan.GetEntity(riderNet);
            var before = transform.GetWorldPosition(rider);
            var oldVehicle = CEntMan.GetEntity(vehicleNet);
            transform.SetLocalPosition(oldVehicle, CEntMan.GetComponent<TransformComponent>(oldVehicle).LocalPosition + Vector2.UnitY);
            Client.System<VehicleRideSurfaceSystem>().Update(0f);
            Assert.That(transform.GetWorldPosition(rider), Is.EqualTo(before), "The previous vehicle must no longer carry this rider.");
            AssertCarries(otherNet, riderNet, Vector2.UnitY);
        });
    }

    [Test]
    public async Task RiderRestoredAfterPredictedRemovalIsCarried()
    {
        var (vehicleNet, riderNet, _) = await CreateAuthoritativeRider(false);
        await Client.WaitAssertion(() =>
        {
            var rider = CEntMan.GetEntity(riderNet);
            CEntMan.RemoveComponent<VehicleRideSurfaceRiderComponent>(rider);
            Assert.That(CEntMan.HasComponent<VehicleRideSurfaceRiderComponent>(rider), Is.False);
        });
        await Pair.RunTicksSync(2);
        await Client.WaitAssertion(() =>
        {
            Assert.That(CEntMan.HasComponent<VehicleRideSurfaceRiderComponent>(CEntMan.GetEntity(riderNet)), Is.True);
            AssertCarries(vehicleNet, riderNet, Vector2.UnitX);
        });
    }

    private async Task<(NetEntity Vehicle, NetEntity Rider, NetEntity Other)> CreateAuthoritativeRider(bool attachAfterState)
    {
        var map = await Pair.CreateTestMap();
        NetEntity vehicleNet = default, riderNet = default, otherNet = default;
        await Server.WaitPost(() =>
        {
            var vehicle = SEntMan.SpawnEntity("CMUVehiclePredictionSurface", map.GridCoords);
            var other = SEntMan.SpawnEntity("CMUVehiclePredictionSurface", map.GridCoords.Offset(new Vector2(0, 5)));
            var rider = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var player = attachAfterState ? SEntMan.SpawnEntity("CMMobHuman", map.GridCoords) : rider;
            Server.PlayerMan.SetAttachedEntity(ServerSession!, player);
            var component = SEntMan.EnsureComponent<VehicleRideSurfaceRiderComponent>(rider);
            component.Vehicle = vehicle;
            component.LocalPosition = Vector2.Zero;
            SEntMan.Dirty(rider, component);
            vehicleNet = SEntMan.GetNetEntity(vehicle);
            riderNet = SEntMan.GetNetEntity(rider);
            otherNet = SEntMan.GetNetEntity(other);
        });
        await Pair.RunUntilSynced();
        return (vehicleNet, riderNet, otherNet);
    }

    private void AssertCarries(NetEntity vehicleNet, NetEntity riderNet, Vector2 movement)
    {
        var vehicle = CEntMan.GetEntity(vehicleNet);
        var rider = CEntMan.GetEntity(riderNet);
        var transform = Client.System<SharedTransformSystem>();
        var before = transform.GetWorldPosition(rider);
        transform.SetLocalPosition(vehicle, CEntMan.GetComponent<TransformComponent>(vehicle).LocalPosition + movement);
        Client.System<VehicleRideSurfaceSystem>().Update(0f);
        Assert.That(Vector2.Distance(transform.GetWorldPosition(rider), before + movement), Is.LessThan(0.001f),
            "A rider received from authoritative state must participate in local carry prediction.");
    }

    private static void AssertAngle(Angle actual, double expectedDegrees, string? message = null)
    {
        Assert.That(Math.Abs(Angle.ShortestDistance(actual, Angle.FromDegrees(expectedDegrees)).Degrees), Is.LessThan(0.001), message);
    }
}
