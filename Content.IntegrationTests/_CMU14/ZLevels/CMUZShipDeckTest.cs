using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Atmos;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Maps;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.CMU14.ZLevels;

[TestFixture]
public sealed class CMUZShipDeckTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Dirty = true };

    private (EntityUid Map, Entity<MapGridComponent> Grid) CreateDeck(Vector2 origin, bool participates = true)
    {
        var maps = Server.System<SharedMapSystem>();
        var map = maps.CreateMap(out var mapId, runMapInit: true);
        SEntMan.EnsureComponent<MapGridComponent>(map);
        var grid = maps.CreateGridEntity(mapId);
        Server.System<SharedTransformSystem>().SetWorldPosition(grid, origin);
        Server.System<SharedPhysicsSystem>().SetBodyType(grid, BodyType.Static);
        if (participates)
            SEntMan.AddComponent<CMUZLevelDeckComponent>(grid);
        return (map, grid);
    }

    private EntityUid CreateBody(EntityUid grid, Vector2 position)
    {
        var body = SEntMan.SpawnEntity(null, new EntityCoordinates(grid, position));
        SEntMan.AddComponent<PhysicsComponent>(body);
        Server.System<SharedPhysicsSystem>().SetBodyType(body, BodyType.Dynamic);
        SEntMan.AddComponent<CMUZPhysicsComponent>(body);
        return body;
    }

    [Test]
    public async Task FixedDeckRampClimbsAndBreachFallsToTheDeckBelow()
    {
        EntityUid fallingBody = default;
        EntityUid lowerMap = default;
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var transforms = Server.System<SharedTransformSystem>();
            var z = Server.System<CMUZLevelsSystem>();
            var lower = CreateDeck(new Vector2(10, 20));
            lowerMap = lower.Map;
            var upper = CreateDeck(new Vector2(10, 20));
            var network = z.CreateZNetwork();
            Assert.That(z.TryAddMapsIntoZNetwork(network, new() { [lower.Map] = 0, [upper.Map] = 1 }), Is.True);
            var tile = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["CMFloorPlating"].TileId);
            for (var y = -1; y <= 1; y++)
                maps.SetTile(lower.Grid, lower.Grid.Comp, new Vector2i(0, y), tile);
            maps.SetTile(upper.Grid, upper.Grid.Comp, new Vector2i(0, 1), tile);
            maps.SetTile(upper.Grid, upper.Grid.Comp, new Vector2i(1, 1), tile);
            SEntMan.SpawnEntity("CMUMultiZStairsAlmayerTop", new EntityCoordinates(lower.Grid, new Vector2(.5f, .5f)));
            var body = CreateBody(lower.Grid, new Vector2(.5f, -.5f));
            fallingBody = body;

            // Crossing the high edge of a real ramp must work while parented to
            // the hull, with world coordinates offset from the background map.
            transforms.SetCoordinates(body, new EntityCoordinates(lower.Grid, new Vector2(.5f, .99f)));
            z.Update(.05f);
            Assert.That(SEntMan.GetComponent<TransformComponent>(body).MapUid, Is.EqualTo(upper.Map));
            Assert.That(transforms.GetWorldPosition(body).X, Is.EqualTo(10.5f).Within(.01f));

            transforms.SetCoordinates(body, new EntityCoordinates(upper.Grid, new Vector2(.5f, 1.5f)));
            z.SetZLocalPosition(body, 0);
            z.SetZVelocity(body, 0);
            z.Update(.05f);
            Assert.That(SEntMan.GetComponent<TransformComponent>(body).MapUid, Is.EqualTo(upper.Map));
            maps.SetTile(upper.Grid, upper.Grid.Comp, new Vector2i(0, 1), Tile.Empty);
            z.WakeZPhysics(body);
            Assert.That(z.DistanceToGround(body, out _), Is.EqualTo(1).Within(.01f));
            Assert.That(SEntMan.HasComponent<CMUZFallingComponent>(body), Is.True);
        });
        // Advance real ticks: the production transition budget is per game tick,
        // not per manual Update call, and the grid traversal must also run.
        await RunSeconds(2);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<TransformComponent>(fallingBody).MapUid, Is.EqualTo(lowerMap)));
    }

    [Test]
    public async Task DeckFloorsBlockCrossZShotsAndProjectionAfterRelocation()
    {
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var z = Server.System<CMUZLevelsSystem>();
            var lower = CreateDeck(new Vector2(100, 200));
            var upper = CreateDeck(new Vector2(100, 200));
            var network = z.CreateZNetwork();
            Assert.That(z.TryAddMapsIntoZNetwork(network, new() { [lower.Map] = 0, [upper.Map] = 1 }), Is.True);
            var tile = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["CMFloorPlating"].TileId);
            maps.SetTile(lower.Grid, lower.Grid.Comp, Vector2i.Zero, tile);
            maps.SetTile(upper.Grid, upper.Grid.Comp, Vector2i.Zero, tile);
            var body = CreateBody(lower.Grid, new Vector2(.5f));
            var world = new Vector2(100.5f, 200.5f);
            Assert.That(z.HasTileAbove(body), Is.True);
            Assert.That(z.IsZShotPathOpen(upper.Map, world, world), Is.False);
            Assert.That(z.TryFindZShotOpening(lower.Map, upper.Map, 1, world, world, out _), Is.False);
            Assert.That(z.TryProjectToGround(new EntityCoordinates(upper.Grid, new Vector2(.5f)), out var projection), Is.True);
            Assert.That(projection.EntityId, Is.EqualTo(upper.Grid.Owner), "Ground effects must resolve against the supporting hull grid.");
            Assert.That(Server.System<SharedTransformSystem>().ToMapCoordinates(projection).MapId,
                Is.EqualTo(SEntMan.GetComponent<MapComponent>(upper.Map).MapId));

            maps.SetTile(upper.Grid, upper.Grid.Comp, Vector2i.Zero, Tile.Empty);
            Assert.That(z.HasTileAbove(body), Is.False);
            Assert.That(z.IsZShotPathOpen(upper.Map, world, world), Is.True);
            Assert.That(z.TryFindZShotOpening(lower.Map, upper.Map, 1, world, world, out _), Is.True);
        });
    }

    [Test]
    public async Task DelayedFireStopsWhenItsDeckIsDeleted()
    {
        await Server.WaitAssertion(() =>
        {
            var deck = CreateDeck(Vector2.Zero);
            var maps = Server.System<SharedMapSystem>();
            var tile = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["CMFloorPlating"].TileId);
            for (var x = -3; x <= 3; x++)
                for (var y = -3; y <= 3; y++)
                    maps.SetTile(deck.Grid, deck.Grid.Comp, new Vector2i(x, y), tile);
            Server.System<SharedRMCFlammableSystem>().SpawnFireDiamond("RMCHijackPipeFire",
                new EntityCoordinates(deck.Grid, new Vector2(.5f)), 3);
            SEntMan.DeleteEntity(deck.Map);
        });
        // The next propagation timer must stop without reading the deleted grid's transform.
        await RunSeconds(1);
    }

    [Test]
    public async Task OrdinaryShuttlePassengersDoNotEnterDeckZPhysics()
    {
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var z = Server.System<CMUZLevelsSystem>();
            var lower = CreateDeck(Vector2.Zero);
            var shuttle = CreateDeck(Vector2.Zero, participates: false);
            var network = z.CreateZNetwork();
            Assert.That(z.TryAddMapsIntoZNetwork(network, new() { [lower.Map] = 0, [shuttle.Map] = 1 }), Is.True);
            maps.SetTile(shuttle.Grid, shuttle.Grid.Comp, Vector2i.Zero, new Tile(1));
            var body = CreateBody(shuttle.Grid, new Vector2(.5f));
            z.WakeZPhysics(body);
            z.Update(.1f);
            Assert.That(SEntMan.GetComponent<TransformComponent>(body).MapUid, Is.EqualTo(shuttle.Map));
            Assert.That(SEntMan.HasComponent<CMUZFallingComponent>(body), Is.False);
        });
    }
}
