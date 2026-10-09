using System.Linq;
using System.Numerics;
using Content.Server.CMU14.Dropship.MultiDeck;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Vehicles;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class MohawkRampVehicleTest
{
    [TestCase(-3.5f)]
    public async Task MidwayTankCanDriveAfterLowering(float cabinY)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        EntityUid ship = default;
        EntityUid tank = default;
        EntityUid driver = default;
        EntityUid ground = default;
        Vector2 landed = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            maps.CreateMap(out var mapId);
            Assert.That(entities.System<MapLoaderSystem>().TryLoadGrid(mapId,
                new ResPath("/Maps/CMU14/ShuttlesDropships/Mohawk/midway.yml"), out var loaded), Is.True);
            ship = loaded!.Value.Owner;
            var lower = entities.GetComponent<MultiDeckDropshipComponent>(ship).Decks[-1];
            ground = entities.GetComponent<TransformComponent>(lower).MapUid!.Value;
            var landingGrid = maps.CreateGridEntity(ground);
            var floor = maps.GetAllTiles(lower, entities.GetComponent<MapGridComponent>(lower)).First().Tile;
            for (var x = -20; x <= 20; x++)
            for (var y = -20; y <= 20; y++)
                maps.SetTile(landingGrid, new Vector2i(x, y), floor);
            tank = entities.SpawnEntity("VehicleTank", new EntityCoordinates(ship, 0.5f, cabinY));
            driver = entities.SpawnEntity("CMMobHuman", new EntityCoordinates(ground, 15, 15));
            Assert.That(entities.System<Content.Shared.Vehicle.Systems.VehicleSystem>().TrySetOperator(
                (tank, entities.GetComponent<VehicleComponent>(tank)), driver), Is.True);
            Assert.That(entities.System<MohawkSystem>().SetRampDeployed(ship, true), Is.True);
        });
        await pair.RunSeconds(6);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.GetComponent<TransformComponent>(tank).MapUid, Is.EqualTo(ground));
            landed = entities.System<SharedTransformSystem>().GetWorldPosition(tank);
            var canRun = new VehicleCanRunEvent((tank, entities.GetComponent<VehicleComponent>(tank)));
            entities.EventBus.RaiseLocalEvent(tank, ref canRun);
            Assert.That(canRun.CanRun, Is.True, "Lowering must leave the tank operational.");
#pragma warning disable RA0002 // Supply held driving input without a connected player.
            entities.EnsureComponent<InputMoverComponent>(driver).HeldMoveButtons = MoveButtons.Up;
#pragma warning restore RA0002
        });
        await pair.RunSeconds(2);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var position = entities.System<SharedTransformSystem>().GetWorldPosition(tank);
            var xform = entities.GetComponent<TransformComponent>(tank);
            var mover = entities.GetComponent<GridVehicleMoverComponent>(tank);
            Assert.That(position.Y, Is.LessThan(landed.Y - 1),
                $"Tank must drive away: {landed} -> {position}; parent={xform.ParentUid}, grid={xform.GridUid}, synced={mover.SyncedGrid}, speed={mover.CurrentSpeed}");
            entities.DeleteEntity(driver);
            entities.DeleteEntity(tank);
            entities.DeleteEntity(ship);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("omaha", "VehicleAPC")]
    [TestCase("midway", "VehicleAPC")]
    [TestCase("omaha", "VehicleBlackfoot")]
    [TestCase("midway", "VehicleBlackfoot")]
    public async Task TimedRampCarriesALargeVehicleThroughRepeatedCycles(string variant, string prototype)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        EntityUid ship = default;
        EntityUid vehicle = default;
        EntityUid lowerMap = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            maps.CreateMap(out var mapId);
            Assert.That(entities.System<MapLoaderSystem>().TryLoadGrid(mapId,
                new ResPath($"/Maps/CMU14/ShuttlesDropships/Mohawk/{variant}.yml"), out var loaded), Is.True);
            ship = loaded!.Value.Owner;
            var lower = entities.GetComponent<MultiDeckDropshipComponent>(ship).Decks[-1];
            lowerMap = entities.GetComponent<TransformComponent>(lower).MapUid!.Value;
            var terrain = entities.EnsureComponent<MapGridComponent>(lowerMap);
            var floor = maps.GetAllTiles(lower, entities.GetComponent<MapGridComponent>(lower)).First().Tile;
            for (var x = -10; x <= 10; x++)
            for (var y = -10; y <= 10; y++)
                maps.SetTile(lowerMap, terrain, new Vector2i(x, y), floor);
            entities.System<MohawkSystem>().SetRampDeployed(ship, true, true);
            vehicle = entities.SpawnEntity(prototype, new EntityCoordinates(lowerMap, 0.5f, -4.5f));
        });
        for (var cycle = 0; cycle < 3; cycle++)
        {
            // Unloading deliberately clears the ramp. Drive back onto it before
            // the next loading cycle instead of treating the landing site as a lift.
            await pair.Server.WaitAssertion(() => pair.Server.EntMan.System<SharedTransformSystem>()
                .SetCoordinates(vehicle, new EntityCoordinates(lowerMap, 0.5f, -4.5f)));
            await pair.Server.WaitAssertion(() => Assert.That(pair.Server.EntMan.System<MohawkSystem>()
                .SetRampDeployed(ship, false), Is.True));
            await pair.RunSeconds(6);
            await pair.Server.WaitAssertion(() =>
            {
                var entities = pair.Server.EntMan;
                Assert.That(entities.GetComponent<TransformComponent>(vehicle).GridUid, Is.EqualTo(ship));
                if (entities.TryGetComponent<CMUZPhysicsComponent>(vehicle, out var zPhysics))
                    Assert.That(zPhysics.LocalPosition, Is.EqualTo(0).Within(0.01f));
                Assert.That(entities.System<MohawkSystem>().SetRampDeployed(ship, true), Is.True);
            });
            await pair.RunSeconds(6);
            await pair.Server.WaitAssertion(() => Assert.That(pair.Server.EntMan.GetComponent<TransformComponent>(vehicle).MapUid,
                Is.EqualTo(lowerMap)));
        }
        await pair.Server.WaitAssertion(() =>
        {
            pair.Server.EntMan.DeleteEntity(vehicle);
            pair.Server.EntMan.DeleteEntity(ship);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("omaha", "VehicleAPC", 0)]
    [TestCase("omaha", "VehicleTank", 90)]
    [TestCase("omaha", "VehicleHumvee", 37)]
    [TestCase("midway", "VehicleAPC", 37)]
    [TestCase("midway", "VehicleTank", 0)]
    [TestCase("midway", "VehicleHumvee", 90)]
    public async Task FootprintClippingTheRampRidesBothWaysExactlyOnce(string variant, string prototype, int degrees)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            maps.CreateMap(out var mapId);
            Assert.That(entities.System<MapLoaderSystem>().TryLoadGrid(mapId,
                new ResPath($"/Maps/CMU14/ShuttlesDropships/Mohawk/{variant}.yml"), out var loaded), Is.True);
            var ship = loaded!.Value.Owner;
            var assembly = entities.GetComponent<MultiDeckDropshipComponent>(ship);
            var lower = assembly.Decks[-1];
            var lowerMap = entities.GetComponent<TransformComponent>(lower).MapUid!.Value;
            var transform = entities.System<SharedTransformSystem>();
            transform.SetWorldRotation(ship, Angle.FromDegrees(degrees));
            entities.System<MultiDeckDropshipSystem>().Synchronize((ship, assembly));
            var terrain = entities.EnsureComponent<MapGridComponent>(lowerMap);
            var floor = maps.GetAllTiles(lower, entities.GetComponent<MapGridComponent>(lower)).First().Tile;
            for (var x = -20; x <= 20; x++)
            for (var y = -20; y <= 20; y++)
                maps.SetTile(lowerMap, terrain, new Vector2i(x, y), floor);

            var mechanisms = entities.System<MohawkSystem>();
            mechanisms.SetRampDeployed(ship, true, true);
            var vehicle = entities.SpawnEntity(prototype, new EntityCoordinates(lowerMap, 15, 15));
            Assert.That(entities.HasComponent<CMUVehicleZTraversalComponent>(vehicle), Is.True);
            Assert.That(CMUVehicleSupportFootprint.TryGetFixtureLocalAabb(
                entities.GetComponent<FixturesComponent>(vehicle), out var footprint), Is.True);
            var rotation = transform.GetWorldRotation(ship);
            // The front bumper overlaps the visible rear edge by only 0.02 tiles;
            // the vehicle's origin is well outside every platform tile.
            var position = new Vector2(0.5f, -6f - footprint.Top + 0.02f);
            var world = transform.ToMapCoordinates(new EntityCoordinates(lower, position)).Position;
            var unloadOffset = entities.GetComponent<MohawkMechanismsComponent>(ship).VehicleUnloadOffset;
            var unloadedWorld = transform.ToMapCoordinates(new EntityCoordinates(lower, position + unloadOffset)).Position;
            transform.SetCoordinates(vehicle, new EntityCoordinates(lowerMap, world));
            transform.SetWorldRotation(vehicle, rotation);

            for (var cycle = 0; cycle < 3; cycle++)
            {
                transform.SetCoordinates(vehicle, new EntityCoordinates(lowerMap, world));
                Assert.That(mechanisms.SetRampDeployed(ship, false, true), Is.True);
                Assert.That(entities.GetComponent<TransformComponent>(vehicle).GridUid, Is.EqualTo(ship));
                Assert.That(Vector2.Distance(transform.GetWorldPosition(vehicle),
                    transform.ToMapCoordinates(new EntityCoordinates(ship, position + Vector2.UnitY)).Position), Is.LessThan(0.001f));
                Assert.That(transform.GetWorldRotation(vehicle).Theta, Is.EqualTo(rotation.Theta).Within(0.001));
                Assert.That(entities.GetComponent<CMUZPhysicsComponent>(vehicle).Velocity, Is.Zero);
                Assert.That(entities.GetComponent<GridVehicleMoverComponent>(vehicle).SyncedGrid, Is.EqualTo(ship));

                Assert.That(mechanisms.SetRampDeployed(ship, true, true), Is.True);
                Assert.That(entities.GetComponent<TransformComponent>(vehicle).MapUid, Is.EqualTo(lowerMap));
                Assert.That(Vector2.Distance(transform.GetWorldPosition(vehicle), unloadedWorld), Is.LessThan(0.001f),
                    "A footprint spanning several tiles must move once per lift and unload clear of the ramp.");
            }

            // A nearby vehicle with a clear gap must not be collected.
            var outside = transform.ToMapCoordinates(new EntityCoordinates(lower, position - new Vector2(0, 0.2f))).Position;
            transform.SetCoordinates(vehicle, new EntityCoordinates(lowerMap, outside));
            mechanisms.SetRampDeployed(ship, false, true);
            Assert.That(entities.GetComponent<TransformComponent>(vehicle).MapUid, Is.EqualTo(lowerMap));
            Assert.That(Vector2.Distance(transform.GetWorldPosition(vehicle), outside), Is.LessThan(0.001f));
            entities.DeleteEntity(vehicle);
            entities.DeleteEntity(ship);
        });
        await pair.CleanReturnAsync();
    }
}
