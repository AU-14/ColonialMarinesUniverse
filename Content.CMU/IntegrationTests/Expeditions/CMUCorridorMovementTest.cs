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
    [TestCase(3)]
    [TestCase(6)]
    [TestCase(12)]
    public async Task GuardsWalkThroughOneTileZigzagCorridor(int count)
    {
        var map = await Pair.CreateTestMap();
        var guards = new List<EntityUid>();
        var destinations = new List<EntityCoordinates>();
        var trace = new Queue<string>();
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var tiles = new List<(Vector2i, Tile)>();
            var entrance = count == 12 ? -6 : 1;
            var farWall = count == 12 ? 14 : 11;
            for (var x = entrance - 1; x <= 8; x++)
            for (var y = 0; y <= farWall; y++)
                tiles.Add((new Vector2i(x, y), map.Tile.Tile));
            maps.SetTiles(map.Grid, tiles);
            // A sealed zigzag forces the squad to follow each other around all three corners.
            for (var x = entrance - 1; x <= 8; x++)
            for (var y = 0; y <= farWall; y++)
            {
                if (y == 2 && x >= entrance && x <= 6
                    || x == 6 && y is >= 2 and <= 5
                    || y == 5 && x is >= 3 and <= 6
                    || x == 3 && y is >= 5 and <= 8
                    || y >= 8 && y < farWall && x is >= 1 and <= 6)
                    continue;
                SEntMan.SpawnEntity("CMUExpeditionHull", new EntityCoordinates(map.Grid, x + 0.5f, y + 0.5f));
            }
            // Rear guards reserve first. Their older tickets must not make the leader
            // yield back into the queue when its heading changes at a corner.
            for (var i = 0; i < count; i++)
            {
                var guard = SEntMan.SpawnEntity("CMUExpeditionScavenger",
                    new EntityCoordinates(map.Grid, entrance + 0.5f + i * (count == 3 ? 1.5f : 0.9f), 2.5f));
                var destination = new EntityCoordinates(map.Grid, 2f + i % 3 * 1.5f, 9f + i / 3);
                guards.Add(guard);
                destinations.Add(destination);
                Assert.That(Server.System<CMUExpeditionAgentSystem>().OrderAssault(guard, destination), Is.True);
            }
        });

        var arrived = false;
        // The twelve-member fixture has a longer entrance and twice the queue.
        // Allow its rear member to clear the exit and the yielding guard to return.
        var sampleLimit = count == 12 ? 200 : 160;
        var sample = 0;
        for (; sample < sampleLimit && !arrived; sample++)
        {
            await Pair.RunSeconds(0.25f);
            await Server.WaitAssertion(() =>
            {
                arrived = true;
                for (var i = 0; i < guards.Count; i++)
                {
                    var position = SEntMan.GetComponent<TransformComponent>(guards[i]).Coordinates;
                    var agent = SEntMan.GetComponent<CMUExpeditionAgentComponent>(guards[i]);
                    arrived &= Server.System<SharedTransformSystem>().InRange(position, destinations[i], 0.5f);
                    trace.Enqueue($"{sample}, guard {i}: {position}, traffic={agent.TrafficDecision}, " +
                        $"yield={agent.TrafficYieldTo}, failures={agent.OrderFailures}, route={string.Join(';', agent.OrderRoute)}");
                    if (trace.Count > 24)
                        trace.Dequeue();
                }
            });
        }
        TestContext.Out.WriteLine($"{count} guards: {(arrived ? "arrived" : "unfinished")} after {sample * 0.25f}s.");
        await Server.WaitAssertion(() => Assert.That(arrived, Is.True,
            $"Every guard must physically cross the one-tile zigzag corridor.\n{string.Join('\n', trace)}"));
    }
}
