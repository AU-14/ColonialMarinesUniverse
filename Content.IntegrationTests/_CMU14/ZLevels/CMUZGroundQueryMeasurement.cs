using System.Diagnostics;
using System.Numerics;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.ZLevels;

/// <summary>Opt-in Release subsystem measurement; this is not a whole-tick benchmark.</summary>
[TestFixture, Explicit("Run by exact fixture filter for controlled before/after ground-query measurements.")]
public sealed class CMUZGroundQueryMeasurement
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task GroundQuery(bool opening)
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            var z = entities.System<CMUZLevelsSystem>();
            var tiles = pair.Server.ResolveDependency<ITileDefinitionManager>();
            var lower = maps.CreateMap(runMapInit: true);
            var upper = maps.CreateMap(runMapInit: true);
            var bottomGrid = entities.EnsureComponent<MapGridComponent>(lower);
            var topGrid = entities.EnsureComponent<MapGridComponent>(upper);
            var floor = new Tile(tiles["Plating"].TileId);
            maps.SetTile(lower, bottomGrid, Vector2i.Zero, floor);
            maps.SetTile(upper, topGrid, opening ? Vector2i.One : Vector2i.Zero, floor);
            var network = z.CreateZNetwork();
            var body = entities.SpawnEntity(null, new EntityCoordinates(upper, new Vector2(0.5f)));
            var physics = entities.AddComponent<CMUZPhysicsComponent>(body);
            try
            {
                Assert.That(z.TryAddMapsIntoZNetwork(network, new() { [lower] = 0, [upper] = 1 }), Is.True);
                var position = new Vector2(0.5f);
                var sum = 0f;
                for (var i = 0; i < 4096; i++)
                    sum += z.DistanceToGroundAtWorldPosition((body, physics), position, out _);

                const int queries = 8192;
                var times = new double[17];
                var allocated = new long[17];
                for (var batch = 0; batch < times.Length; batch++)
                {
                    var before = GC.GetAllocatedBytesForCurrentThread();
                    var start = Stopwatch.GetTimestamp();
                    for (var i = 0; i < queries; i++)
                        sum += z.DistanceToGroundAtWorldPosition((body, physics), position, out _);
                    times[batch] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                    allocated[batch] = GC.GetAllocatedBytesForCurrentThread() - before;
                }
                TestContext.Out.WriteLine($"opening={opening} queriesPerBatch={queries} sum={sum}");
                TestContext.Out.WriteLine("milliseconds=" + string.Join(",", times.Select(t => t.ToString("F4", System.Globalization.CultureInfo.InvariantCulture))));
                TestContext.Out.WriteLine("allocatedBytes=" + string.Join(",", allocated));
            }
            finally
            {
                entities.DeleteEntity(body);
                entities.DeleteEntity(network);
                entities.DeleteEntity(upper);
                entities.DeleteEntity(lower);
            }
        });
        await pair.CleanReturnAsync();
    }
}
