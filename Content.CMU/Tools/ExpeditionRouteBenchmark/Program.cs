using System.Diagnostics;
using Content.Shared.CMU14.Expeditions;

// Synthetic route-search cost only. Terrain callbacks here do not run engine physics.
foreach (var budget in new[] { 256, 2048 })
foreach (var reuse in new[] { false, true })
{
    var workspace = reuse ? new CMUTacticalRoute.Workspace() : null;
    const int size = 96;
    const int iterations = 500;
    var start = 48 * size + 8;
    var end = 48 * size + 88;
    var walkChecks = 0;
    var passageChecks = 0;
    bool Walkable(int cell)
    {
        walkChecks++;
        // A sealed wall forces a bounded unsuccessful search rather than an early goal.
        return cell % size != 48;
    }
    bool Passage(int from, int to)
    {
        passageChecks++;
        return true;
    }
    for (var warmup = 0; warmup < 100; warmup++)
        CMUTacticalRoute.Find(size, start, end, Walkable, _ => 0, Passage, out _, budget, workspace);
    walkChecks = passageChecks = 0;
    var allocated = GC.GetAllocatedBytesForCurrentThread();
    var began = Stopwatch.GetTimestamp();
    for (var iteration = 0; iteration < iterations; iteration++)
        CMUTacticalRoute.Find(size, start, end, Walkable, _ => 0, Passage, out _, budget, workspace);
    var milliseconds = Stopwatch.GetElapsedTime(began).TotalMilliseconds;
    allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
    Console.WriteLine($"budget={budget}; reuse={reuse}; searches={iterations}; ms/search={milliseconds / iterations:F4}; " +
        $"bytes/search={allocated / iterations}; terrain/search={walkChecks / iterations}; passage/search={passageChecks / iterations}");
}
