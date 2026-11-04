using Content.Server.Shuttles.Components;
using Content.Shared.CCVar;
using Content.Shared._RMC14.Construction;
using Content.Shared._RMC14.Dropship;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class DropshipBoardingMomentumTest
{
    [Test]
    public async Task NavigationConsoleCannotTurnTerrainIntoADropship()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.EntMan;
            var maps = em.System<SharedMapSystem>();
            var ground = maps.CreateMap(out var mapId);
            var groundGrid = em.EnsureComponent<MapGridComponent>(ground);
            em.EnsureComponent<ShuttleComponent>(ground);
            var floor = new Tile(pair.Server.ResolveDependency<ITileDefinitionManager>()["FloorSteel"].TileId);
            maps.SetTile(ground, groundGrid, Vector2i.Zero, floor);
            var coordinates = new EntityCoordinates(ground, 0.5f, 0.5f);
            em.SpawnEntity("CMComputerDropshipNavigation", coordinates);
            Assert.That(em.System<RMCConstructionSystem>().CanBuildAt(coordinates, (string) null, out _), Is.True,
                "Placing a navigation console must not veto construction across the planet.");

            var ship = maps.CreateGridEntity(mapId);
            maps.SetTile(ship, Vector2i.Zero, floor);
            var shipCoordinates = new EntityCoordinates(ship, 0.5f, 0.5f);
            em.SpawnEntity("CMComputerDropshipNavigation", shipCoordinates);
            Assert.That(em.HasComponent<DropshipComponent>(ship), Is.True);
            Assert.That(em.System<RMCConstructionSystem>().CanBuildAt(shipCoordinates, (string) null, out _), Is.False,
                "A real dropship must retain its construction restrictions.");
            maps.DeleteMap(mapId);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MapShuttlePhysicsCannotLaunchPeopleOrItemsOntoUnderside()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            var transform = entities.System<SharedTransformSystem>();
            var physics = entities.System<SharedPhysicsSystem>();
            var ground = maps.CreateMap(out var mapId);
            entities.EnsureComponent<MapGridComponent>(ground);
            entities.EnsureComponent<FixturesComponent>(ground);
            var groundBody = entities.EnsureComponent<PhysicsComponent>(ground);

            // Legacy planet maps, including Shepherds, contain dynamic shuttle
            // physics. Reproduce a saved body's velocity before Shuttle startup.
            physics.SetBodyType(ground, BodyType.Dynamic, body: groundBody);
            physics.SetCanCollide(ground, false, body: groundBody);
            physics.SetLinearVelocity(ground, new Vector2(4, -2), wakeBody: false, body: groundBody);
            entities.EnsureComponent<ShuttleComponent>(ground);

            var underside = maps.CreateGridEntity(mapId);
            entities.EnsureComponent<DropshipDeckComponent>(underside);
            var tiles = server.ResolveDependency<ITileDefinitionManager>();
            maps.SetTile(underside, Vector2i.Zero, new Tile(tiles["FloorSteel"].TileId));
            physics.SetBodyType(underside, BodyType.Kinematic);

            // Disabling atmos does not clear an already stored map velocity.
            server.CfgMan.SetCVar(CCVars.SpaceWind, false);
            server.CfgMan.SetCVar(CCVars.AtmosGridImpulse, false);
            foreach (var prototype in new EntProtoId[] { "CMMobHuman", "Wrench" })
            {
                var uid = entities.SpawnEntity(prototype, new EntityCoordinates(ground, 10, 10));
                var body = entities.GetComponent<PhysicsComponent>(uid);
                Assert.That(body.LinearVelocity, Is.EqualTo(Vector2.Zero));
                transform.SetCoordinates(uid, new EntityCoordinates(underside, 0.5f, 0.5f));
                Assert.That(entities.GetComponent<TransformComponent>(uid).ParentUid, Is.EqualTo(underside.Owner));
                Assert.That(body.LinearVelocity, Is.EqualTo(Vector2.Zero),
                    $"{prototype} must not inherit motion from the immovable terrain when boarding.");
            }
            Assert.That(groundBody.BodyType, Is.EqualTo(BodyType.Static));
            Assert.That(groundBody.LinearVelocity, Is.EqualTo(Vector2.Zero));
            maps.DeleteMap(mapId);
        });
        await pair.CleanReturnAsync();
    }
}
