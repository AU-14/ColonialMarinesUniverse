using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using VehicleSystem = Content.Shared.Vehicle.Systems.VehicleSystem;

namespace Content.IntegrationTests.CMU14.Dropship;

[TestFixture]
public sealed class CMUMohawkRailingVehicleTest
{
    [Test]
    public async Task VehicleCrossesRaisedRampRailingButOrdinaryRailingStillBlocks()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true, Connected = true });
        var map = await pair.CreateTestMap();
        EntityUid vehicle = default, driver = default, railing = default, ordinary = default, loader = default;
        NetEntity vehicleNet = default, driverNet = default, railingNet = default, loaderNet = default;
        var integrity = 0f;
        var obstacleY = 0f;

        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            var tile = new Tile(pair.Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
            for (var x = -10; x <= 10; x++)
            for (var y = -40; y <= 15; y++)
                maps.SetTile(map.Grid, new Vector2i(x, y), tile);

            railing = entities.SpawnEntity("CMUMohawkOmahaRampRailing", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            entities.System<SharedRequisitionsSystem>().SetRailingMode(
                (railing, entities.GetComponent<RequisitionsRailingComponent>(railing)), RequisitionsRailingMode.Raised);
            vehicle = entities.SpawnEntity("VehicleAPC", map.GridCoords.Offset(new Vector2(0.5f, 5.5f)));
            var wheel = entities.SpawnEntity("RMCAPCWheel", map.GridCoords);
            var slots = entities.System<ItemSlotsSystem>();
            Assert.That(slots.TryGetSlot(vehicle, "wheel-1", out var wheelSlot), Is.True);
            Assert.That(slots.TryInsert(vehicle, wheelSlot!, wheel, null), Is.True);
            driver = entities.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(8.5f, 5.5f)));
            loader = entities.SpawnEntity("RMCMechPowerLoader", map.GridCoords.Offset(new Vector2(8.5f, 0.5f)));
            integrity = entities.GetComponent<HardpointIntegrityComponent>(vehicle).Integrity;
            vehicleNet = entities.GetNetEntity(vehicle);
            driverNet = entities.GetNetEntity(driver);
            railingNet = entities.GetNetEntity(railing);
            loaderNet = entities.GetNetEntity(loader);
        });
        await pair.RunUntilSynced();

        void CheckContacts(IEntityManager entities)
        {
            var rail = entities.GetEntity(railingNet);
            bool PreventsContact(NetEntity otherNet)
            {
                var other = entities.GetEntity(otherNet);
                var args = new PreventCollideEvent(rail, other,
                    entities.GetComponent<PhysicsComponent>(rail), entities.GetComponent<PhysicsComponent>(other),
                    entities.GetComponent<FixturesComponent>(rail).Fixtures.Values.First(f => f.Hard),
                    entities.GetComponent<FixturesComponent>(other).Fixtures.Values.First(f => f.Hard));
                entities.EventBus.RaiseLocalEvent(rail, ref args);
                return args.Cancelled;
            }

            Assert.That(PreventsContact(vehicleNet), Is.True, "Vehicle contacts must be filtered on both peers.");
            Assert.That(PreventsContact(loaderNet), Is.True, "Powerloaders use ordinary physics rather than grid driving.");
            Assert.That(PreventsContact(driverNet), Is.False, "Raised rails must still stop pedestrians.");
        }

        await pair.Server.WaitAssertion(() => CheckContacts(pair.Server.EntMan));
        await pair.Client.WaitAssertion(() => CheckContacts(pair.Client.EntMan));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.System<VehicleSystem>().TrySetOperator(
                (vehicle, entities.GetComponent<VehicleComponent>(vehicle)), driver), Is.True);
            var canRun = new VehicleCanRunEvent((vehicle, entities.GetComponent<VehicleComponent>(vehicle)));
            entities.EventBus.RaiseLocalEvent(vehicle, ref canRun);
            Assert.That(canRun.CanRun, Is.True, "The test APC must be operational before driving toward the rail.");
#pragma warning disable RA0002 // Supply driving input through the same mover used by an operator.
            entities.EnsureComponent<InputMoverComponent>(driver).HeldMoveButtons = MoveButtons.Up;
#pragma warning restore RA0002
        });
        await pair.RunSeconds(3);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var position = entities.System<SharedTransformSystem>().GetWorldPosition(vehicle);
            Assert.That(position.Y, Is.LessThan(-2.5f), "The whole vehicle must cross the raised railing.");
            Assert.That(entities.GetComponent<HardpointIntegrityComponent>(vehicle).Integrity, Is.EqualTo(integrity),
                "Passing through a ramp railing must not cause crash damage.");
            Assert.That(entities.GetComponent<RequisitionsRailingComponent>(railing).Mode, Is.EqualTo(RequisitionsRailingMode.Raised),
                "Vehicle passage must not lower the guardrail for pedestrians.");

            obstacleY = position.Y - 5;
            ordinary = entities.SpawnEntity("CMRailing", map.GridCoords.Offset(new Vector2(0.5f, obstacleY)));
        });
        await pair.RunSeconds(2);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.System<SharedTransformSystem>().GetWorldPosition(vehicle).Y, Is.GreaterThan(obstacleY),
                "Ordinary railings must still stop the vehicle.");
            entities.DeleteEntity(driver);
            entities.DeleteEntity(vehicle);
            entities.DeleteEntity(loader);
            entities.DeleteEntity(railing);
            entities.DeleteEntity(ordinary);
        });
        await pair.CleanReturnAsync();
    }
}
