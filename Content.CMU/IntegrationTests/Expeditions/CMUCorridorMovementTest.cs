using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Expeditions;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Expeditions;

[TestFixture]
public sealed class CMUCorridorMovementTest : GameTest
{
    [TestCase(6)]
    [TestCase(12)]
    public async Task WholeSquadReceivesAssaultOrderInNarrowObjective(int count)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var tiles = new List<(Vector2i, Tile)>();
            var length = count == 6 ? 9 : 17;
            for (var x = 0; x < length; x++)
            for (var y = 0; y < 3; y++)
                tiles.Add((new Vector2i(x, y), map.Tile.Tile));
            Server.System<SharedMapSystem>().SetTiles(map.Grid, tiles);
            for (var x = 0; x < length; x++)
            {
                SEntMan.SpawnEntity("CMUExpeditionHull", new EntityCoordinates(map.Grid, x + 0.5f, 0.5f));
                SEntMan.SpawnEntity("CMUExpeditionHull", new EntityCoordinates(map.Grid, x + 0.5f, 2.5f));
            }
            var reserved = new List<EntityCoordinates>();
            var center = new EntityCoordinates(map.Grid, length / 2 + 0.5f, 1.5f);
            for (var i = 0; i < count; i++)
            {
                var guard = SEntMan.SpawnEntity("CMUExpeditionScavenger", new EntityCoordinates(map.Grid, i + 0.5f, 1.5f));
                Assert.That(Server.System<CMUExpeditionAgentSystem>().OrderSquadPoint(guard,
                    center, "assault", reserved), Is.True,
                    $"Member {i} must receive the squad order even when normal formation spacing cannot fit.");
            }
        });
    }
}
