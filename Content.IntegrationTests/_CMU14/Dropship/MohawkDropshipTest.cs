using System.Linq;
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.CMU14.Dropship.MultiDeck;
using Content.Server.CMU14.ZLevels.Core;
using Content.Server.Shuttles.Systems;
using Content.Server.Shuttles.Components;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Vehicles;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared.Atmos;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared._RMC14.Marines.Skills;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class MohawkDropshipTest
{
    [TestCase("omaha")]
    [TestCase("midway")]
    [TestCase("omaha_navy")]
    [TestCase("midway_navy")]
    public async Task CabinStartsWithBreathableAir(string variant)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var map = entities.System<SharedMapSystem>().CreateMap(out var mapId);
            Assert.That(entities.System<MapLoaderSystem>().TryLoadGrid(mapId,
                new ResPath($"/Maps/CMU14/ShuttlesDropships/Mohawk/{variant}.yml"), out var loaded), Is.True);
            var air = entities.System<AtmosphereSystem>().GetTileMixture(loaded!.Value.Owner, map, Vector2i.Zero);
            Assert.That(air, Is.Not.Null);
            Assert.That(air!.Pressure, Is.InRange(90f, 110f), "Cabin air must not depend on the landing map's atmosphere.");
            Assert.That(air.GetMoles(Gas.Oxygen), Is.GreaterThan(20f));
            entities.DeleteEntity(loaded.Value.Owner);
        });
        await pair.RunTicksSync(2);
        await pair.CleanReturnAsync();
    }

    [TestCase("omaha", 0)]
    [TestCase("midway", 90)]
    public async Task CabinFloorsBlockOpenAirShotsOnGeneratedLevels(string variant, int degrees)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            var map = maps.CreateMap(out var mapId);
            Assert.That(entities.System<MapLoaderSystem>().TryLoadGrid(mapId,
                new ResPath($"/Maps/CMU14/ShuttlesDropships/Mohawk/{variant}.yml"), out var loaded), Is.True);
            var ship = loaded!.Value;
            var transform = entities.System<SharedTransformSystem>();
            transform.SetWorldRotation(ship, Angle.FromDegrees(degrees));
            var position = transform.ToMapCoordinates(new EntityCoordinates(ship, 0.5f, 0.5f)).Position;
            var zLevels = entities.System<CMUZLevelsSystem>();
            Assert.That(zLevels.IsZShotPathOpen(map, position, position), Is.False,
                "A generated map still has a solid floor where its child cabin grid is present.");
            maps.SetTile(ship, ship.Comp, Vector2i.Zero, Tile.Empty);
            Assert.That(zLevels.IsZShotPathOpen(map, position, position), Is.True,
                "A hole in the cabin must retain the normal open-air shooting behavior.");
            entities.DeleteEntity(ship);
        });
        await pair.RunTicksSync(2);
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AdjacentOmahaAndMidwayPadsMayTouchWithoutOverlapping()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            maps.CreateMap(out var mapId);
            var loader = entities.System<MapLoaderSystem>();
            Assert.That(loader.TryLoadGrid(mapId, new ResPath("/Maps/CMU14/ShuttlesDropships/Mohawk/omaha.yml"), out var omaha), Is.True);
            Assert.That(loader.TryLoadGrid(mapId, new ResPath("/Maps/CMU14/ShuttlesDropships/Mohawk/midway.yml"), out var midway), Is.True);
            var ground = maps.CreateMap();
            var markerCoordinates = new EntityCoordinates(ground, -9.5f, 15.5f);
            var reserved = entities.SpawnEntity("CMUMohawkTestOffsetDestination", markerCoordinates);
            entities.System<SharedDropshipSystem>().SetDestinationShip(reserved, omaha!.Value.Owner);
            var assembly = entities.System<MultiDeckDropshipSystem>();
            Assert.That(assembly.GetLandingOrigin(omaha.Value.Owner, markerCoordinates, reserved).Position,
                Is.EqualTo(new Vector2(-9f, 15f)), "Use the pad's large-hull offset before snapping the grid.");
            Assert.That(assembly.GetLandingOrigin(ground, markerCoordinates, reserved), Is.EqualTo(markerCoordinates),
                "An ordinary ship must keep its existing landing coordinates.");
            Assert.That(assembly.IsLandingClear(midway!.Value.Owner, new EntityCoordinates(ground, 7f, 15f), Angle.Zero), Is.True,
                "The two USS Bush pads are 16 tiles apart; the cabin envelopes meet at their edges.");
            Assert.That(assembly.IsLandingClear(midway.Value.Owner, new EntityCoordinates(ground, 6.9f, 15f), Angle.Zero), Is.False,
                "Moving inside the other ship's reserved envelope must still fail.");
            entities.DeleteEntity(omaha.Value.Owner);
            entities.DeleteEntity(midway.Value.Owner);
        });
        await pair.RunTicksSync(2);
        await pair.CleanReturnAsync();
    }

    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  id: CMUMohawkTestOffsetDestination
  parent: CMDropshipDestination
  components:
  - type: DropshipDestination
    multiDeckOffset: 1,0

- type: entity
  id: CMUMohawkTestPassenger
  components:
  - type: Physics
    bodyType: Dynamic
  - type: Fixtures
    fixtures:
      body:
        shape: !type:PhysShapeAabb
          bounds: '-0.2,-0.2,0.2,0.2'
        hard: true
        layer: 0
        mask: 0
  - type: CMUZPhysics
    bounciness: 0
""";

    [TestCase("omaha", 0)]
    [TestCase("omaha", 90)]
    [TestCase("midway", 0)]
    [TestCase("midway", 90)]
    public async Task TimedRampAllowsWalkingBetweenDecks(string variant, int degrees)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        EntityUid ship = default;
        EntityUid lower = default;
        EntityUid passenger = default;
        EntityUid vehicle = default;
        EntityUid cabinMap = default;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            cabinMap = maps.CreateMap(out var mapId);
            Assert.That(entities.System<MapLoaderSystem>().TryLoadGrid(mapId,
                new ResPath($"/Maps/CMU14/ShuttlesDropships/Mohawk/{variant}.yml"), out var loaded), Is.True);
            ship = loaded!.Value.Owner;
            var assembly = entities.GetComponent<MultiDeckDropshipComponent>(ship);
            lower = assembly.Decks[-1];
            var transform = entities.System<SharedTransformSystem>();
            transform.SetWorldRotation(ship, Angle.FromDegrees(degrees));
            Assert.That(entities.System<MultiDeckDropshipSystem>().Synchronize((ship, assembly)), Is.True);
            var shot = transform.ToMapCoordinates(new EntityCoordinates(ship, 0.5f, -3.5f)).Position;
            var lowerMap = entities.GetComponent<TransformComponent>(lower).MapUid!.Value;
            // Exercise boarding from an actual landing surface, including the
            // overlap between the terrain and the ship's supporting grid.
            var terrain = entities.EnsureComponent<MapGridComponent>(lowerMap);
            var tile = maps.GetAllTiles(lower, entities.GetComponent<MapGridComponent>(lower)).First().Tile;
            for (var x = -8; x <= 8; x++)
            for (var y = -8; y <= 8; y++)
                maps.SetTile(lowerMap, terrain, new Vector2i(x, y), tile);
            Assert.That(entities.System<CMUZLevelsSystem>().TryFindZShotOpening(cabinMap, lowerMap, -1,
                shot, shot, out _), Is.False, "The closed ramp must block shots between decks.");
            var mechanisms = entities.System<MohawkSystem>();
            Assert.That(mechanisms.SetHatchDeployed(ship, true), Is.True);
            Assert.That(mechanisms.SetRampDeployed(ship, true), Is.True);
        });

        await pair.RunSeconds(6);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var state = entities.GetComponent<MohawkMechanismsComponent>(ship);
            Assert.That(state.HatchDeployed, Is.True);
            Assert.That(state.RampDeployed, Is.True);
            Assert.That(entities.HasComponent<MohawkRampMovingComponent>(ship), Is.False);
            var shot = entities.System<SharedTransformSystem>().ToMapCoordinates(new EntityCoordinates(ship, 0.5f, -3.5f)).Position;
            var lowerMap = entities.GetComponent<TransformComponent>(lower).MapUid!.Value;
            var levels = entities.System<CMUZLevelsSystem>();
            Assert.That(levels.TryFindZShotOpening(cabinMap, lowerMap, -1, shot, shot, out _), Is.True);
            Assert.That(levels.TryFindZShotOpening(lowerMap, cabinMap, 1, shot, shot, out _), Is.True);
            var start = entities.System<SharedTransformSystem>()
                .ToMapCoordinates(new EntityCoordinates(lower, 0.5f, -7.5f)).Position;
            passenger = entities.SpawnEntity("CMUMohawkTestPassenger", new EntityCoordinates(lowerMap, start));
            vehicle = entities.SpawnEntity("CMUMohawkTestPassenger", new EntityCoordinates(lowerMap, start));
            entities.AddComponent<CMUVehicleZTraversalComponent>(vehicle);
        });
        await pair.RunTicksSync(3);
        // Small steps keep Z position continuous rather than teleporting the
        // passenger onto each ramp segment or directly between the two decks.
        for (var step = 1; step <= 56; step++)
        {
            var y = -7.5f + step * 0.125f;
            await server.WaitAssertion(() =>
            {
                var entities = server.EntMan;
                var transform = entities.System<SharedTransformSystem>();
                transform.SetWorldPosition(passenger,
                    transform.ToMapCoordinates(new EntityCoordinates(lower, 0.5f, y)).Position);
                // Wheeled and tracked vehicles use footprint sampling and must
                // remain below the stairs even when moved across their surface.
                transform.SetWorldPosition(vehicle,
                    transform.ToMapCoordinates(new EntityCoordinates(lower, 0.5f, y)).Position);
                Assert.That(entities.GetComponent<TransformComponent>(vehicle).MapUid,
                    Is.EqualTo(entities.GetComponent<TransformComponent>(lower).MapUid));
                Assert.That(entities.GetComponent<CMUZPhysicsComponent>(vehicle).LocalPosition,
                    Is.EqualTo(0f).Within(0.02f), "Vehicles must not rise on the boarding stairs.");
                if (y <= -4.125f)
                {
                    Assert.That(entities.GetComponent<TransformComponent>(passenger).MapUid,
                        Is.EqualTo(entities.GetComponent<TransformComponent>(lower).MapUid));
                    Assert.That(entities.GetComponent<CMUZPhysicsComponent>(passenger).LocalPosition, Is.EqualTo(0f).Within(0.02f),
                        "Walking beyond the bottom edge and across ramp 4-9 must stay at ground level.");
                }
            });
            await pair.RunTicksSync(3);
        }
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            Assert.That(entities.GetComponent<TransformComponent>(passenger).MapUid, Is.EqualTo(cabinMap),
                "Walking up the deployed ramp must reach the cabin, including after rotating the ship.");
            Assert.That(entities.System<SharedDropshipSystem>().TryGetGridDropship(passenger, out var owner), Is.True);
            Assert.That(owner.Owner, Is.EqualTo(ship));
            Assert.That(entities.GetComponent<TransformComponent>(vehicle).MapUid,
                Is.EqualTo(entities.GetComponent<TransformComponent>(lower).MapUid));
            entities.DeleteEntity(vehicle);
        });
        for (var step = 1; step <= 56; step++)
        {
            var y = -0.5f - step * 0.125f;
            await server.WaitAssertion(() =>
            {
                var transform = server.EntMan.System<SharedTransformSystem>();
                transform.SetWorldPosition(passenger,
                    transform.ToMapCoordinates(new EntityCoordinates(lower, 0.5f, y)).Position);
            });
            await pair.RunTicksSync(3);
        }
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            Assert.That(entities.GetComponent<TransformComponent>(passenger).MapUid,
                Is.EqualTo(entities.GetComponent<TransformComponent>(lower).MapUid),
                "Walking back onto the open ramp must descend to the lower deck.");
            Assert.That(entities.GetComponent<CMUZPhysicsComponent>(passenger).LocalPosition, Is.EqualTo(0f).Within(0.05f),
                "The foot of the ramp must return the passenger to ground level.");
            entities.DeleteEntity(ship);
        });
        await pair.RunTicksSync(2);
        await pair.CleanReturnAsync();
    }

    [TestCase("omaha", 0, "RMCMechPowerLoader")]
    [TestCase("midway", 90, "RMCMechPowerLoaderGreen")]
    [TestCase("omaha_navy", 180, "RMCMechPowerLoaderBlue")]
    [TestCase("midway_navy", 270, "RMCMechPowerLoader")]
    public async Task PowerLoadersWalkOverRampBulkhead(string variant, int degrees, string prototype)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        EntityUid ship = default;
        EntityUid lower = default;
        EntityUid loader = default;
        EntityUid pilot = default;
        EntityUid cabinMap = default;
        Direction forward = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            cabinMap = maps.CreateMap(out var mapId);
            Assert.That(entities.System<MapLoaderSystem>().TryLoadGrid(mapId,
                new ResPath($"/Maps/CMU14/ShuttlesDropships/Mohawk/{variant}.yml"), out var loaded), Is.True);
            ship = loaded!.Value.Owner;
            var assembly = entities.GetComponent<MultiDeckDropshipComponent>(ship);
            lower = assembly.Decks[-1];
            var transform = entities.System<SharedTransformSystem>();
            var rotation = Angle.FromDegrees(degrees);
            transform.SetWorldRotation(ship, rotation);
            Assert.That(entities.System<MultiDeckDropshipSystem>().Synchronize((ship, assembly)), Is.True);
            Assert.That(entities.System<MohawkSystem>().SetRampDeployed(ship, true, true), Is.True);
            var lowerMap = entities.GetComponent<TransformComponent>(lower).MapUid!.Value;
            var terrain = entities.EnsureComponent<MapGridComponent>(lowerMap);
            var tile = maps.GetAllTiles(lower, entities.GetComponent<MapGridComponent>(lower)).First().Tile;
            for (var x = -8; x <= 8; x++)
            for (var y = -8; y <= 8; y++)
                maps.SetTile(lowerMap, terrain, new Vector2i(x, y), tile);
            var start = transform.ToMapCoordinates(new EntityCoordinates(lower, 0.5f, -5.5f)).Position;
            loader = entities.SpawnEntity(prototype, new EntityCoordinates(lowerMap, start));
            pilot = entities.SpawnEntity("CMMobHuman", new EntityCoordinates(lowerMap, start));
            entities.System<SkillsSystem>().SetSkill(pilot, "RMCSkillPowerLoader", 1);
            Assert.That(entities.System<SharedBuckleSystem>().TryBuckle(pilot, null, loader, popup: false), Is.True);
            forward = Direction.North;
            var mover = entities.System<SharedMoverController>();
            var pilotInput = entities.GetComponent<InputMoverComponent>(pilot);
            var relativeRotation = pilotInput.RelativeEntity is { } relative
                ? transform.GetWorldRotation(relative)
                : Angle.Zero;
            mover.SetCameraRotation(pilot, rotation - relativeRotation, true);
            mover.SetCameraRotation(loader, rotation - relativeRotation, true);
            mover.SetVelocityDirection(
                (loader, entities.GetComponent<InputMoverComponent>(loader)), forward, ushort.MaxValue, true);
        });

        // Use real movement and solid fixtures: moving the transform directly would
        // hide a loader getting caught on the bulkhead before it reaches the cabin.
        await pair.RunSeconds(5);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            entities.System<SharedMoverController>().SetVelocityDirection(
                (loader, entities.GetComponent<InputMoverComponent>(loader)), forward, ushort.MaxValue, false);
            Assert.That(entities.GetComponent<TransformComponent>(loader).MapUid, Is.EqualTo(cabinMap),
                $"A piloted powerloader must climb above the bulkhead; stopped at {entities.GetComponent<TransformComponent>(loader).Coordinates}.");
            Assert.That(entities.GetComponent<TransformComponent>(pilot).MapUid, Is.EqualTo(cabinMap));
            Assert.That(entities.GetComponent<BuckleComponent>(pilot).BuckledTo, Is.EqualTo(loader));
            entities.DeleteEntity(ship);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("omaha")]
    [TestCase("midway")]
    [TestCase("omaha_navy")]
    [TestCase("midway_navy")]
    public async Task DecksLoadTravelAndRetractTogether(string variant)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        EntityUid travellingShip = default;
        EntityUid travellingRider = default;
        EntityUid finalGround = default;
        EntityCoordinates finalCabin = default;
        var controls = new Dictionary<EntityUid, (EntityUid Grid, Vector2 Position)>();
        var hull = new Dictionary<EntityUid, (EntityUid Grid, Vector2 Position)>();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            var loader = entities.System<MapLoaderSystem>();
            var transform = entities.System<SharedTransformSystem>();
            var multiDeck = entities.System<MultiDeckDropshipSystem>();
            var zLevels = entities.System<CMUZLevelsSystem>();
            var dropships = entities.System<SharedDropshipSystem>();
            var mechanisms = entities.System<MohawkSystem>();
            Assert.That(loader.TryLoadMap(new ResPath($"/Maps/CMU14/ShuttlesDropships/Mohawk/{variant}_deployment.yml"),
                out var deploymentMap, out var grids, DeserializationOptions.Default with { InitializeMaps = true }), Is.True);
            var map = deploymentMap!.Value.Owner;
            var loaded = grids!.Single();
            var ship = loaded.Owner;
            var assembly = entities.GetComponent<MultiDeckDropshipComponent>(ship);
            Assert.That(assembly.Initialized, Is.True, "Every declared deck must load successfully.");
            Assert.That(assembly.Decks.Keys, Is.EquivalentTo(new[] { -1, 1 }));
            Assert.That(zLevels.GetAllNetworkMaps(map), Has.Count.EqualTo(3));

            var lower = assembly.Decks[-1];
            var upper = assembly.Decks[1];
            foreach (var body in entities.EntityQuery<PhysicsComponent>())
            {
                var xform = entities.GetComponent<TransformComponent>(body.Owner);
                if (body.BodyType != BodyType.Static || !body.CanCollide || (xform.ParentUid != ship && xform.ParentUid != lower))
                    continue;
                Assert.That(xform.Anchored, Is.True, $"Ship fixture {entities.GetComponent<MetaDataComponent>(body.Owner).EntityPrototype?.ID} at {xform.LocalPosition} needs an anchoring tile.");
                hull.Add(body.Owner, (xform.ParentUid, xform.LocalPosition));
            }
            foreach (var control in entities.EntityQuery<MohawkControlComponent>())
            {
                var xform = entities.GetComponent<TransformComponent>(control.Owner);
                controls.Add(control.Owner, (xform.GridUid!.Value, xform.LocalPosition));
            }
            Assert.That(controls, Has.Count.EqualTo(variant.StartsWith("omaha") ? 5 : 3));
            var boardingTile = maps.GetAllTiles(lower, entities.GetComponent<MapGridComponent>(lower)).First();
            var rider = entities.SpawnEntity(null, new EntityCoordinates(ship, 0.5f, 0.5f));
            Assert.That(entities.GetComponent<TransformComponent>(rider).GridUid, Is.EqualTo(ship));
            Assert.That(dropships.TryGetGridDropship(rider, out var resolved), Is.True);
            Assert.That(resolved.Owner, Is.EqualTo(ship));
            Assert.That(entities.GetComponent<DropshipComponent>(ship).AttachmentPoints.Count,
                Is.EqualTo(variant.StartsWith("omaha") ? 10 : 12), "All servicing points must belong to the flight controller.");
            Assert.That(maps.GetAllTiles(lower, entities.GetComponent<MapGridComponent>(lower)).Count(),
                Is.EqualTo(variant.StartsWith("omaha") ? 38 : 39), "Gear footprints, disconnected mounts and the external button must stay in one grid.");

            Assert.That(mechanisms.SetHatchDeployed(ship, true), Is.True);
            Assert.That(mechanisms.SetRampDeployed(ship, true), Is.True);
            Assert.That(entities.HasComponent<MohawkHatchMovingComponent>(ship), Is.True);
            Assert.That(entities.HasComponent<MohawkRampMovingComponent>(ship), Is.True);
            mechanisms.SetHatchDeployed(ship, true, true);
            mechanisms.SetRampDeployed(ship, true, true);
            var hatch = entities.EntityQuery<MohawkHatchComponent>().Single();
            Assert.That(entities.HasComponent<CMUZLevelLadderComponent>(hatch.Owner), Is.True);
            var lowerLadder = entities.EntityQuery<MohawkLowerLadderComponent>().Single();
            Assert.That(entities.HasComponent<CMUZLevelLadderComponent>(lowerLadder.Owner), Is.True);

            var destination = maps.CreateMap();
            Assert.That(multiDeck.TryGetLandingCoordinates(ship,
                new EntityCoordinates(destination, new Vector2(37, -19)), out var target), Is.True);
            var groundTarget = new EntityCoordinates(destination, new Vector2(37, -19));
            Assert.That(multiDeck.IsLandingClear(ship, groundTarget, Angle.FromDegrees(90)), Is.True);
            // Terrain on every level is ignored for Mohawk landing clearance.
            Assert.That(zLevels.TryMapOffset(target.EntityId, 1, out var roofMap), Is.True);
            var obstruction = maps.CreateGridEntity(entities.GetComponent<MapComponent>(roofMap!.Value).MapId);
            var roofTile = maps.GetAllTiles(upper, entities.GetComponent<MapGridComponent>(upper)).First();
            var obstructionPosition = target.Position + Angle.FromDegrees(90).RotateVec(roofTile.GridIndices + new Vector2(0.5f));
            maps.SetTile(obstruction, obstruction.Comp,
                new Vector2i((int) MathF.Floor(obstructionPosition.X), (int) MathF.Floor(obstructionPosition.Y)), roofTile.Tile);
            Assert.That(multiDeck.IsLandingClear(ship, groundTarget, Angle.FromDegrees(90)), Is.True,
                "A floor above the cabin must not reject the landing pad.");
            entities.DeleteEntity(obstruction);
            var cabinObstruction = maps.CreateGridEntity(entities.GetComponent<MapComponent>(target.EntityId).MapId);
            var cabinTile = maps.GetAllTiles(ship, loaded.Comp).First();
            var cabinObstructionPosition = target.Position + Angle.FromDegrees(90).RotateVec(cabinTile.GridIndices + new Vector2(0.5f));
            var cabinObstructionTile = new Vector2i((int) MathF.Floor(cabinObstructionPosition.X), (int) MathF.Floor(cabinObstructionPosition.Y));
            var tileDefinitions = server.ResolveDependency<ITileDefinitionManager>();
            maps.SetTile(cabinObstruction, cabinObstruction.Comp,
                cabinObstructionTile, new Tile(tileDefinitions["CMFloorPlating"].TileId));
            Assert.That(multiDeck.IsLandingClear(ship, groundTarget, Angle.FromDegrees(90)), Is.True,
                "A floor at cabin height must not reject a Mohawk landing.");
            maps.SetTile(cabinObstruction, cabinObstruction.Comp,
                cabinObstructionTile, new Tile(tileDefinitions["CMShuttleTileInvisible"].TileId));
            Assert.That(multiDeck.IsLandingClear(ship, groundTarget, Angle.FromDegrees(90)), Is.True,
                "Open-air map anchors are not solid floors at cabin height.");
            entities.SpawnEntity("WallSolid", new EntityCoordinates(cabinObstruction, cabinObstructionTile + new Vector2(0.5f)));
            Assert.That(multiDeck.IsLandingClear(ship, groundTarget, Angle.FromDegrees(90)), Is.True,
                "A wall must not reject a Mohawk landing.");
            entities.DeleteEntity(cabinObstruction);
            Assert.That(multiDeck.IsLandingClear(ship, groundTarget, Angle.FromDegrees(90)), Is.True);
            // A ship still in transit must reserve its destination volume too.
            var transitMap = maps.CreateMap(out var transitId);
            var inbound = maps.CreateGridEntity(transitId);
            maps.SetTile(inbound, inbound.Comp, Vector2i.Zero, roofTile.Tile);
            var reservation = entities.SpawnEntity(null, groundTarget);
            entities.AddComponent<DropshipDestinationComponent>(reservation);
            dropships.SetDestinationShip(reservation, inbound.Owner);
            Assert.That(multiDeck.IsLandingClear(ship, groundTarget, Angle.FromDegrees(90)), Is.False);
            entities.DeleteEntity(reservation);
            entities.DeleteEntity(transitMap);
            transform.SetCoordinates((ship, entities.GetComponent<TransformComponent>(ship), entities.GetComponent<MetaDataComponent>(ship)), target, rotation: Angle.FromDegrees(90));
            Assert.That(multiDeck.Synchronize((ship, assembly)), Is.True);
            foreach (var grid in new[] { lower, upper })
            {
                Assert.That(transform.GetWorldPosition(grid), Is.EqualTo(new Vector2(37, -19)));
                Assert.That(transform.GetWorldRotation(grid), Is.EqualTo(Angle.FromDegrees(90)));
            }
            Assert.That(entities.GetComponent<TransformComponent>(lower).MapUid, Is.EqualTo(destination));
            Assert.That(entities.GetComponent<TransformComponent>(rider).GridUid, Is.EqualTo(ship));

            // Generated levels remain usable after departure, including ordinary
            // grids built there: an opaque floor must not become a shot opening.
            Assert.That(zLevels.TryMapOffset(map, -1, out var originalLowerMap), Is.True);
            var terrain = maps.CreateGridEntity(entities.GetComponent<MapComponent>(map).MapId);
            maps.SetTile(terrain, terrain.Comp, Vector2i.Zero, roofTile.Tile);
            var shot = new Vector2(0.5f);
            Assert.That(zLevels.TryFindZShotOpening(map, originalLowerMap!.Value, -1, shot, shot, out _), Is.False);
            entities.DeleteEntity(terrain);
            Assert.That(zLevels.TryFindZShotOpening(map, originalLowerMap.Value, -1, shot, shot, out _), Is.True);

            var beforeFlight = new Content.Server._RMC14.Shuttles.BeforeFTLStartedEvent();
            entities.EventBus.RaiseLocalEvent(ship, ref beforeFlight);
            var state = entities.GetComponent<MohawkMechanismsComponent>(ship);
            Assert.That(state.RampDeployed, Is.False);
            Assert.That(state.HatchDeployed, Is.False);
            Assert.That(maps.GetAllTiles(ship, loaded.Comp).Count(), Is.EqualTo(variant.StartsWith("omaha") ? 245 : 235));

            travellingShip = ship;
            travellingRider = rider;
            finalGround = maps.CreateMap();
            Assert.That(multiDeck.TryGetLandingCoordinates(ship,
                new EntityCoordinates(finalGround, new Vector2(-31, 17)), out finalCabin), Is.True);
            var shuttles = entities.System<ShuttleSystem>();
            shuttles.DefaultArrivalTime = 0.5f;
            var nav = entities.EntityQuery<DropshipNavigationComputerComponent>()
                .First(c => entities.GetComponent<TransformComponent>(c.Owner).GridUid == ship);
            // Authored pad markers sit at tile centers. The ship must still land
            // at the tile corner, including when it has been turned around.
            var marker = entities.SpawnEntity("CMUMohawkTestOffsetDestination",
                new EntityCoordinates(finalGround, finalCabin.Position + new Vector2(-0.5f, 0.5f)));
            transform.SetWorldRotation(marker, Angle.FromDegrees(180));
            Assert.That(dropships.FlyTo((nav.Owner, nav), marker, null, startupTime: 0.5f, hyperspaceTime: 2f), Is.True);
        });
        await pair.RunSeconds(8);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var assembly = entities.GetComponent<MultiDeckDropshipComponent>(travellingShip);
            var transform = entities.System<SharedTransformSystem>();
            Assert.That(entities.GetComponent<TransformComponent>(travellingShip).MapUid, Is.EqualTo(finalCabin.EntityId));
            Assert.That(entities.GetComponent<TransformComponent>(assembly.Decks[-1]).MapUid, Is.EqualTo(finalGround));
            foreach (var grid in assembly.Decks.Values)
            {
                Assert.That(transform.GetWorldPosition(grid), Is.EqualTo(finalCabin.Position));
                Assert.That(transform.GetWorldRotation(grid), Is.EqualTo(Angle.FromDegrees(180)));
                Assert.That(entities.HasComponent<ShuttleComponent>(grid), Is.False);
            }
            Assert.That(entities.GetComponent<TransformComponent>(travellingRider).GridUid, Is.EqualTo(travellingShip));
            foreach (var (control, expected) in controls)
            {
                var xform = entities.GetComponent<TransformComponent>(control);
                Assert.That(xform.Anchored, Is.True, "Controls must stay attached through FTL and landing.");
                Assert.That(xform.ParentUid, Is.EqualTo(expected.Grid));
                Assert.That(xform.LocalPosition, Is.EqualTo(expected.Position));
                Assert.That(entities.System<SharedDropshipSystem>().TryGetGridDropship(control, out var owner), Is.True);
                Assert.That(owner.Owner, Is.EqualTo(travellingShip));
            }
            foreach (var (part, expected) in hull)
            {
                var xform = entities.GetComponent<TransformComponent>(part);
                Assert.That(xform.Anchored, Is.True);
                Assert.That(xform.ParentUid, Is.EqualTo(expected.Grid));
                Assert.That(Vector2.Distance(xform.LocalPosition, expected.Position), Is.LessThan(0.001f),
                    "Hull, seats and landing gear must not drift into the cabin when the ship rotates or travels.");
            }
            entities.DeleteEntity(assembly.Decks[1]);
            Assert.That(entities.System<MultiDeckDropshipSystem>().TryGetLandingCoordinates(travellingShip,
                new EntityCoordinates(finalGround, Vector2.Zero), out _), Is.False,
                "A damaged assembly must not launch with a missing deck.");
            // Deleting the controller must not leave independently usable decks.
            entities.DeleteEntity(travellingShip);
        });
        await pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.EntityQuery<DropshipDeckComponent>().Any(), Is.False);
        });
        await pair.CleanReturnAsync();
    }
}
