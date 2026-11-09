#pragma warning disable RA0002 // Build real overlapping terrain and moving deck geometry.
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.Performance;

[TestFixture]
public sealed class CMUGroundGridResolutionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [Test]
    public async Task GroundQueriesFollowLateRotatedDecksTileEditsAndRemoval()
    {
        var cleanup = new List<EntityUid>();
        EntityUid surface = default;
        Entity<MapGridComponent> deck = default;
        CMUZLevelsSystem z = default!;
        Entity<CMUZPhysicsComponent> body = default;
        var sample = Vector2.Zero;
        var support = EntityUid.Invalid;
        var floor = Tile.Empty;
        try
        {
            await Server.WaitAssertion(() =>
            {
                var maps = Server.System<SharedMapSystem>();
                var layers = new Dictionary<EntityUid, int>();
                floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
                for (var depth = -1; depth <= 0; depth++)
                {
                    var map = maps.CreateMap(runMapInit: true);
                    cleanup.Add(map);
                    var grid = SEntMan.EnsureComponent<MapGridComponent>(map);
                    for (var x = -4; x <= 4; x++)
                    for (var y = -4; y <= 4; y++)
                        maps.SetTile(map, grid, new Vector2i(x, y), floor);
                    layers.Add(map, depth);
                    if (depth == 0) surface = map;
                }
                z = Server.System<CMUZLevelsSystem>();
                var network = z.CreateZNetwork();
                cleanup.Add(network.Owner);
                Assert.That(z.TryAddMapsIntoZNetwork(network, layers), Is.True);
                var uid = SEntMan.SpawnAttachedTo(null, new EntityCoordinates(surface, -3.5f, -3.5f));
                body = (uid, SEntMan.AddComponent<CMUZPhysicsComponent>(uid));
                AssertGround(z, body, new Vector2(0.5f), 0, false);
                AssertGround(z, body, new Vector2(-0.5f), 0, false);

                deck = maps.CreateGridEntity(SEntMan.GetComponent<MapComponent>(surface).MapId);
                SEntMan.AddComponent<DropshipDeckComponent>(deck);
                maps.SetTile(deck, new Vector2i(-1, -1), floor);
                maps.SetTile(deck, new Vector2i(-1, 0), floor);
                var transform = Server.System<SharedTransformSystem>();
                transform.SetLocalPosition(deck, new Vector2(1.1f));
                transform.SetLocalRotation(deck, new Angle(0.37));
                sample = maps.LocalToWorld(deck, deck.Comp, new Vector2(-0.5f));
                support = SEntMan.SpawnAttachedTo(null, new EntityCoordinates(deck, new Vector2(-0.5f)));
                var highGround = SEntMan.AddComponent<CMUZLevelHighGroundComponent>(support);
                highGround.HeightCurve = [0.6f, 0.6f];
                highGround.Stick = true;
                Assert.That(transform.AnchorEntity((support, SEntMan.GetComponent<TransformComponent>(support)),
                    deck, new Vector2i(-1, -1)), Is.True);
            });
            await Pair.RunTicksSync(2);
            await Server.WaitAssertion(() =>
            {
                AssertGround(z, body, sample, -0.6f, true);
                var maps = Server.System<SharedMapSystem>();
                maps.SetTile(deck, new Vector2i(-1, -1), Tile.Empty);
                AssertGround(z, body, sample, 0, false);
                maps.SetTile(deck, new Vector2i(-1, -1), floor);
                var transform = Server.System<SharedTransformSystem>();
                Assert.That(transform.AnchorEntity((support, SEntMan.GetComponent<TransformComponent>(support)),
                    deck, new Vector2i(-1, -1)), Is.True);
                AssertGround(z, body, sample, -0.6f, true);
                transform.SetLocalPosition(deck, new Vector2(11.1f, 1.1f));
            });
            await Pair.RunTicksSync(2);
            await Server.WaitAssertion(() =>
            {
                AssertGround(z, body, sample, 0, false);
                var maps = Server.System<SharedMapSystem>();
                sample = maps.LocalToWorld(deck, deck.Comp, new Vector2(-0.5f));
                AssertGround(z, body, sample, -0.6f, true);
                SEntMan.DeleteEntity(deck);
            });
            await Pair.RunTicksSync(2);
            await Server.WaitAssertion(() =>
            {
                var maps = Server.System<SharedMapSystem>();
                maps.SetTile(surface, SEntMan.GetComponent<MapGridComponent>(surface), Vector2i.Zero, Tile.Empty);
                AssertGround(z, body, new Vector2(0.5f), 1, false);
            });
        }
        finally
        {
            for (var i = cleanup.Count - 1; i >= 0; i--)
                await Pair.DeleteEntityTreeLeafFirst(cleanup[i]);
        }
    }

    private static void AssertGround(CMUZLevelsSystem system, Entity<CMUZPhysicsComponent> body,
        Vector2 position, float expected, bool expectedSticky)
    {
        var distance = system.DistanceToGroundAtWorldPosition((body.Owner, body.Comp), position, out var sticky);
        Assert.That(distance, Is.EqualTo(expected).Within(0.0001f), $"Ground distance at {position}.");
        Assert.That(sticky, Is.EqualTo(expectedSticky), $"Sticky support at {position}.");
    }
}
