# Performance reproduction tools

Run tools from the repository root. These utilities inspect logs or offline map records; they do not replace engine physics or change production maps.

## Capture and compare

- `cmuperf pvs` captures the available engine state-generation stage counters.
- `cmuperf physics` captures available physics-controller counters; body/contact counts alone are not CPU attribution.
- `lagprofile` controls the server profiler capture window. Its help describes start, report and stop arguments.
- `compare_pvs_logs.py` compares weighted stage observations. Use `--help` for its input and output options. Missing or reset counters are unavailable, rather than zero cost.
- `terrain_packing_audit.py` checks an offline encoding of selected canonical rock records and verifies round trips. It does not remove live entities or establish a runtime improvement.

The corresponding Python checks are in `test_performance_tools.py`:

```powershell
python -m unittest discover -s Tools/Performance -p test_performance_tools.py -v
```

## Cost and retention

`cmuperf` reports diagnostics' own elapsed work and allocation. Detail-report time is included in update time when emission happens during Update; do not add those measurements together. These are elapsed main-thread observations, not process CPU or asynchronous logging cost.

Automatic detailed reports are coalesced across trigger types. Scalar incident observations and omitted-detail counts remain available, and manual captures remain immediate.

The anchored-tile snapshot cache admits repeatedly queried occupied cells, retains at most 4,096 cells with at most 256 members each, and evicts individual residents. Empty and oversized results remain complete without being retained. Read-only support queries use the engine enumerator.

## Scope

The experimental physics tick, pool, broadphase and solver adapters were removed. Ordinary content uses the original engine. The experimental bulk content PVS selector, its build switch, CVar and research fixtures were also removed. Multi-Z views use the normal engine probe path. Falling-entity visibility uses a bounded content hook before state generation.

Synthetic timings and reduced work counts do not establish live-server TPS. Use comparable captures and inspect frame tails, allocations, retained memory and complete state-send costs before making a production speed claim.

Overhead visibility pools retain at most 64 buffers of each kind, each with capacity at most 1024 members. Large active results remain complete. Round cleanup and shutdown release retained capacity; diagnostics report active counts, pooled capacity and dictionary capacity.
