# Scavenger server performance

Expedition decisions normally run every 150 ms. Large groups can become due together,
especially when gunfire interrupts that interval. The system processes the oldest due
deadlines first and leaves deferred deadlines intact. Hits, gunfire and equipment changes
bring a future deadline forward without moving an overdue agent to the back of the queue.

Two server-only CVars control decision work:

| CVar | Default | Meaning |
| --- | --- | --- |
| `cmu.expedition_ai.think_budget_ms` | 3 | Soft wall-clock budget per tick |
| `cmu.expedition_ai.thinks_per_tick` | 64 | Maximum decisions per tick |

At least one eligible decision completes per tick. A decision is synchronous and can
exceed the time budget by itself. Under sustained overload, decision latency increases;
the budget is not a guarantee on total server frame time. Native weapon execution and
its target/friendly-lane checks run for every active agent each tick. Squad planning,
hazard snapshots, gunfire events and native steering also remain outside this budget.

`cmu-expedition-ai-status here` (or a map ID) prints the last tick's global completed and
deferred counts and decision time before the individual agent diagnostics. Compare these
with server frame timings while spawning groups and during sustained firefights. A queue
that stays large means the requested AI workload exceeds the chosen decision budget.

The hot paths also avoid repeated work:

- Gunfire handlers reject unrelated lanes first and check the known shooter directly,
  rather than acquiring every nearby hostile for each observer on every bullet.
- Infantry and vehicle acquisition share one leased spatial-query result.
- Body and swept-passage queries stop at the first obstruction without collecting fixtures.
- Position reservations reuse map membership within one decision; coordinates, life state,
  hostility and reserved destinations are still checked against live components.
- A* skips already cheaper cells before terrain and passage callbacks, avoids per-cell
  neighbour arrays, and leases reusable cost/parent dictionaries. Walking and vault retries
  start with cleared search state. Concurrent/nested searches require separate leases.
- Firing-cone queries and fire-region buckets reuse cleared collections.

## Reproducible route microbenchmark

```text
dotnet run --project Content.CMU/Tools/ExpeditionRouteBenchmark -c Release
```

The harness links the actual route implementation and Robust priority queue. It searches
a 96 by 96 grid with a sealed wall, warms up, then performs 500 bounded failed searches.
The `reuse=True` rows represent the server's leased search dictionaries. The callbacks
are deliberately cheap; this measures search allocations and callback counts, not live
physics or an entire NPC update. The search bounds remain 256 and 2048 expanded cells.

Comparison against the route source at CMU master `1519e86d78` on the same local .NET 10
runtime, using the same harness and inputs:

| Search budget | Original bytes/search | Reused bytes/search | Original terrain checks | Current terrain checks | Original passage checks | Current passage checks |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 256 | 47,640 | 5,248 | 1,024 | 349 | 1,017 | 342 |
| 2048 | 395,592 | 5,248 | 8,164 | 2,190 | 8,120 | 2,146 |

This is approximately 89%/99% less search allocation and 66%/74% fewer passage callbacks.
No production server or real-player load was used. CPU timings from a concurrent local
build are not treated as representative firefight measurements.

Focused regression coverage lives in `CMUAgentPerformanceTest` (fair scheduling, firing
while decisions are deferred, faction/retaliation/ignore membership) and
`CMUTacticalRouteRegressionTest` (independent shortest-path comparison across weighted
grids, repeated workspace use, invalid costs, row edges and blocked passages).
