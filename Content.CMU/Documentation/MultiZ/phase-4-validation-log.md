# Phase 4: Performance Validation Log

Status: in progress. The first headless baseline and corrected eight-viewer follow-up were
captured on 2026-07-23 UTC. Phase 4 is not complete; GPU, real-network, and two-client validation
remain outstanding.

This log records measured evidence for the Phase 3 implementation. It does not turn a single
headless run into a performance conclusion, and it does not replace live multiplayer or
graphics-device validation.

## Evidence set

The full evidence set is `artifacts/multiz-phase4/20260723-230657`:

- `evidence.json` contains the structured load, tick, PVS, profiler-counter, soak, and gate data.
- `evidence.log` contains the human-readable scenario and gate result.
- `manifest.json` records the configuration, machine, Git state, artifact hashes, and duration.
- `cpu.nettrace`, its converted ETLX and Speedscope files, and the inclusive/exclusive top-30
  reports contain one process-wide CPU trace.

The capture ran from `2026-07-23T23:06:57Z` to `2026-07-23T23:10:21Z` at Git
`fe82ed9d69caf523acf7e6c0c100f4a18458976c`. The worktree was dirty, and the manifest records
the exact modified and untracked files. Results must therefore be associated with the manifest,
not treated as a clean-commit release baseline.

The final eight-viewer follow-up is
`artifacts/multiz-phase4/20260724-final-profile300-v3/evidence.json`, captured at
`2026-07-23T23:46:51Z` with the same 60/300-tick, eight-player, 30-PVS-sample protocol. It omitted
the soak and process trace because those had already completed in the full evidence set. It uses
stable observer entities for the synthetic PVS viewers, profiles all 300 requested ticks, rejects
truncated profiles, verifies every viewer is on one of the measured scenario maps, and makes
viewer completeness a hard gate. Both control and Multi-Z retained all eight viewers at profile
start, throughout all 38 periodic PVS profile samples, and before explicit PVS measurement, with
zero reattachments. Unless a section says otherwise, load, tick, PVS, topology, and scoped-profile
values below use this final follow-up; soak and trace values use the full evidence set.

The raw artifacts remain ignored because the trace alone is approximately 95 MB. A checked-in,
lightweight metric and SHA-256 record is available at
[evidence/phase-4-baseline-summary.json](evidence/phase-4-baseline-summary.json), so the documented
baseline survives a fresh checkout and raw files can be matched when retained externally.

The host used an AMD Ryzen 5 7600 with 6 physical/12 logical cores, approximately 32 GB of RAM,
Windows `10.0.26200`, .NET SDK `10.0.301`, and .NET runtime `10.0.9`. Server GC was disabled.
The host had AMD integrated graphics and an NVIDIA RTX 4070 Ti SUPER, but this headless run did
not measure either GPU.

## Protocol

| Setting | Value |
| --- | ---: |
| JIT warm-up | Separate Multi-Z Bush scenario |
| Warm-up ticks per measured scenario | 60 |
| Profile/tick capture | 300 ticks |
| Requested synthetic players | 8 |
| Static and cycling PVS samples | 30 each |
| Multi-Z soak | 18,000 ticks |
| Soak checkpoint interval | 900 ticks |
| Random seed | 42 |
| Process CPU trace | Enabled |
| PVS execution | Enabled, synchronous (`net.pvs_async=false`), automatic worker count |

The measured control was the single base USS Bush Redux map with no Z-network. The measured
Multi-Z scenario was the complete five-map Bush network. This is a whole-scenario comparison,
not an isolated estimate of the Multi-Z systems' incremental cost: the scenarios have different
map and entity counts, ran sequentially, and share one test-process scheduling harness.

The runner disables asynchronous PVS and leaves `thread.parallel_count=0`, which asks the engine
to select its worker count automatically rather than disabling worker parallelism. Production
defaults can schedule PVS work differently, so the server-turnaround baseline includes
synchronous PVS work and must not be treated as a production threading comparison.

## Correctness gates

All hard headless correctness gates passed:

- The control loaded exactly one map and no Z-network.
- Multi-Z Bush loaded five maps at depths `[-1, 0, 1, 2, 3]`, with bounds `-1..3`.
- Scenario teardown restored the starting map and network counts.

The provisional tick, topology, soak-memory, and soak-stability alerts also passed. The original
full run exposed an inadequate synthetic-viewer lifecycle check. The final follow-up passed the
hard attachment gate at `8/8/8` for profile start, pre-PVS capture, and requested viewers in both
scenarios, retained a periodic profile range of exactly `8..8`, and required no reattachment at
any reconciliation or cycling point. Both 300-tick profiler buffers were present and untruncated.

## Load and server-turnaround baseline

| Measurement | Single-level control | Five-level Multi-Z |
| --- | ---: | ---: |
| Load wall time | 3,347.3025 ms | 3,705.7980 ms |
| Load thread allocation | 224,080,912 bytes | 430,617,112 bytes |
| Entities immediately after load | 7,773 | 15,057 |
| Loaded maps / Z-networks | 1 / 0 | 5 / 1 |
| Capture entity count | 7,877 | 15,188 |
| Profile ticks / log entries | 300 / 165,244 | 300 / 170,130 |
| Tick-turnaround p50 | 0.6452 ms | 1.0781 ms |
| Tick-turnaround p95 | 0.8738 ms | 2.1247 ms |
| Tick-turnaround p99 | 1.0796 ms | 2.5952 ms |
| Tick-turnaround maximum | 1.8863 ms | 2.9955 ms |

The whole five-level scenario took 358.4955 ms more wall time to load (`+10.71%`) and
206,536,200 more thread-allocated bytes (`+92.17%`) than the one-map control. Those deltas include
four additional maps and approximately twice as many loaded entities; they do not isolate topology
construction or any one Multi-Z system.

The measured Multi-Z tick-turnaround percentiles were higher than the control in this run, while
remaining below the provisional 33.333 ms 30-TPS deadline. A single order-sensitive, non-isolated
harness run is insufficient for an overhead conclusion, and turnaround includes harness
signaling. The runner deliberately omits an allocation distribution from this asynchronous
turnaround measurement: a current-thread allocation counter cannot validly span an `await`.
Allocation evidence instead comes from same-thread load/PVS measurements and the scoped server
profiler.

## PVS and topology capture

| Measurement | Single-level control | Five-level Multi-Z |
| --- | ---: | ---: |
| Static PVS p50 / p95 / p99 | 0.2897 / 0.3680 / 0.4340 ms | 1.0460 / 14.7738 / 15.4559 ms |
| Static PVS calling-thread allocation p50 | 5,664 bytes | 5,752 bytes |
| Cycling PVS p50 / p95 / p99 | 0.7915 / 1.1185 / 24.5216 ms | 1.0447 / 4.5586 / 13.8971 ms |
| Cycling PVS calling-thread allocation p50 | 42,504 bytes | 52,880 bytes |
| Warm-up / pre-PVS reconciliations | 0 / 0 | 0 / 0 |
| Later cycling reattachments | 0 | 0 |

The corrected follow-up explicitly counted attachments instead of treating the requested session
count as proof. Both scenarios recorded:

- eight requested viewers;
- eight attached observer viewer components at profile start;
- exactly eight viewers in every one of the 38 periodic profile samples;
- eight attached viewer components immediately before PVS measurement; and
- zero reattachments during the 30 cycling samples.

The final runner uses `MobObserver` rather than mortal human entities for synthetic viewers. This
keeps the PVS population stable without periodically repairing bodies that can be removed by
ordinary gameplay. Cycling still moves the observers through the Bush warp coordinates. Static
Multi-Z PVS had two large tail samples in this 30-operation distribution, so its p95/p99 must not be
discarded simply because the median is lower.

Each periodic Multi-Z profile sample recorded eight viewers, eight existing/reused probe eyes,
968 stair tiles, 620 anchored stair entities, 154 visible-opening candidates, 78 visible-opening
LOS checks, and eight opening-path steps. Created/removed probe eyes and subscriber adds remained
zero.

During the final 300-tick steady capture, topology counters summed 700 direct network hits and 914
direct neighbour-offset hits. Recovery scans, recovered hits, and misses remained zero. This is
evidence for the steady Bush fast path only; initialization, late join, reconnect, topology
mutation, and client-side replication still need separate captures.

Selected scoped timings were:

| Profiler scope | Samples | p50 | p95 | Allocation p50 / p95 |
| --- | ---: | ---: | ---: | ---: |
| `CMU Z Movement` | 300 | 0.1778 ms | 0.3270 ms | 0 / 0 bytes |
| `CMU Z PVS Probes` | 38 | 0.8728 ms | 1.2066 ms | 53,200 / 53,200 bytes |
| `CMU Z PVS SyncViewerProbes` | 304 | 0.0998 ms | 0.2352 ms | 9,776 / 9,984 bytes |
| `CMU Z PVS BuildWantedDepths` | 304 | 0.0863 ms | 0.2252 ms | 4,800 / 9,040 bytes |
| `CMU Z PVS OpeningPath` | 304 | 0.0856 ms | 0.2225 ms | 4,800 / 9,040 bytes |
| `CMU Z PVS VisibleOpening` | 304 | 0.0855 ms | 0.2218 ms | 4,800 / 9,040 bytes |
| `CMU Z PVS VisibleOpeningSort` | 190 | 0.0026 ms | 0.0114 ms | 0 / 0 bytes |
| `CMU Z PVS StairPreview` | 304 | 0.0096 ms | 0.0268 ms | 1,024 / 5,184 bytes |

These scoped samples identify what to measure next. They are still one deterministic headless run
and are not sufficient by themselves to justify a new cap, cache, scheduling policy, or
architecture change.

### MZ-017 higher-viewer scaling and modernization

The same five-map, 60-warm-up-tick, 300-profile-tick, 30-PVS-sample protocol was repeated at 16,
32, and 64 attached observer viewers before changing production code. Every run recorded 38
periodic 4-Hz probe samples, retained every requested viewer, required zero reattachments, loaded
depths `[-1, 0, 1, 2, 3]`, and passed all headless correctness gates.

The baseline allocation scaled with viewers, while `VisibleOpeningSort` remained allocation-free
and small. Per-viewer `VisibleOpening` allocated 4,800 bytes at p50 and `StairPreview` allocated
1,024 bytes at p50. This identified the result-materializing examine LOS query as the smallest
measured bottleneck to address. Server PVS now uses the allocation-free occluder first-hit query
for visible openings and stair previews; the stair query composes the existing viewer/stair ignore
predicate with the existing endpoint-touching rule.

| Viewers | Probe p50 before / after | Probe p95 before / after | Allocation before / after | Allocation change |
| ---: | ---: | ---: | ---: | ---: |
| 16 | 1.2070 / 0.6432 ms | 1.5377 / 0.9047 ms | 106,192 / 30,528 bytes | -71.25% |
| 32 | 2.6280 / 2.4628 ms | 19.7458 / 3.0405 ms | 221,136 / 62,656 bytes | -71.67% |
| 64 | 6.2943 / 5.0146 ms | 7.6211 / 5.9572 ms | 442,064 / 127,552 bytes | -71.15% |

The 32-viewer baseline p95 contains a host-scheduling tail, so its large p95 delta is not treated
as a stable CPU improvement ratio. P50 improved by 46.71%, 6.29%, and 20.33% at 16, 32, and 64
viewers respectively; the deterministic allocation slope is the stronger result.

Behavioral work counts were unchanged in every pair:

| Viewers | Visible-opening candidates before / after | LOS checks before / after | Stair tiles before / after |
| ---: | ---: | ---: | ---: |
| 16 | 360 / 360 | 170 / 170 | 1,936 / 1,936 |
| 32 | 730 / 730 | 350 / 350 | 3,872 / 3,872 |
| 64 | 1,512 / 1,512 | 714 / 714 | 7,744 / 7,744 |

The raw before evidence is
`artifacts/multiz-phase4/20260724-002912`,
`artifacts/multiz-phase4/20260724-003227`, and
`artifacts/multiz-phase4/20260724-003522`; after evidence is
`artifacts/multiz-phase4/20260724-004457`,
`artifacts/multiz-phase4/20260724-004755`, and
`artifacts/multiz-phase4/20260724-005001`. The checked-in
`evidence/mz-017-pvs-scaling.json` preserves the exact values and SHA-256 hashes.

This closes the headless higher-viewer and server-LOS allocation work for MZ-017. Real packets,
bandwidth, late join, reconnect, and live populated-round scheduling remain deferred to the
networking group; approximately 1.9-2.0 KB per viewer per rebuild remains in candidate discovery
and surrounding work. No candidate cap, visibility cache, or scheduling change is justified by
this capture.

Targeted gates passed: Release `Content.Server` and `Content.Benchmarks` builds reported zero
warnings/errors, and the DebugOpt `CMUZLevelViewerLifecycleTest` filter passed 2/2 integration
tests in 4 minutes 21 seconds.

## Soak result

The complete 18,000-tick Multi-Z soak ran to completion with 20 checkpoints:

| Measurement | Start | End / stable result |
| --- | ---: | ---: |
| Retained managed bytes | 1,530,197,208 | 1,496,596,200 |
| Entities | 15,332 | 15,270 |
| Maps | 5 | 5 |
| Z-networks | 1 | 1 |

Retained managed-memory growth was `-33,601,008` bytes against a provisional allowance of
76,509,860 bytes. Checkpoint memory oscillated instead of growing monotonically. Entity count was
15,288 at the first checkpoint and stable at 15,270 from the second checkpoint through tick
18,000. Map and network counts stayed at five and one, respectively. Teardown restored the
pre-scenario counts.

This rules out an obvious retained-growth or topology-count leak in this deterministic soak. It
does not cover long live rounds, connected-client churn, real packet queues, GPU resources, or
gameplay-driven entity growth.

## CPU trace interpretation

The process-wide trace is dominated by waiting and harness/runtime synchronization:

- `LowLevelLifoSemaphore.WaitForSignal` accounts for 58.88% exclusive samples.
- queued-completion, wait-handle, monitor, reader/writer-lock, and reset-event waits occupy most of
  the remaining leading exclusive entries.
- `PvsSystem.GetEntityState` is 0.20% exclusive in this trace.
- the leading non-wait inclusive stack is prototype/serialization load:
  `SerializationManager.Read` is 8.65%, `PrototypeManager.TryReadPrototype` is 8.64%, and entity
  prototype/component-registry reading is approximately 7.6%.

The trace spans harness startup, map/prototype loading, capture, and soak. It is not a focused
active-round or client-render trace, so it does not identify a CMU Z hot stack that justifies a
code change. A focused server-active window and separate client/GPU captures remain required.

## Placement-render regression

Phase 4 live use exposed a rendering correctness defect: while tile or object placement was
active, a lower Z-level rendered as base space rather than remaining visible through its opening.

The port had retained a pre-fix viewport gate that disabled every Multi-Z render pass whenever
`IPlacementManager.IsActive` was true. The fallback then rendered only the ordinary base pass, so
the lower deck disappeared and the normal space background showed through. The legacy branch had
already removed this gate in commit
`dc4fb34917addb6437802e31a97180ce491b4baf` (`Fix upper Z placement rendering`).

The current correction removes placement-manager state from
`ScalingViewport.ShouldUseZLevelRenderPasses`. Multi-Z pass eligibility now depends only on the
Multi-Z enable and render CVars, and `ZLevelRenderPassesUseRenderCVarsOnly` is the focused policy
regression. A live Bush session then activated entity placement over the depth-1 opening at map 3,
tile `19,-24`; the user confirmed that the lower level remained visible instead of reverting to
space. Tile and entity placement share the same placement-active render gate, so this closes the
reported regression. The finding is tracked as MZ-069.

## Remaining Phase 4 exit work

Phase 4 remains open until evidence covers, at minimum:

- PVS scaling runs beyond the corrected eight-viewer snapshot;
- a real two-client session covering server authority, prediction, reconciliation, late join,
  reconnect, remote cameras, immediate cross-Z interaction, and injected latency;
- packet and byte captures for topology state, PVS fan-out, component replication, level changes,
  falls, projectile traversal, and late join;
- client CPU, frame-time, allocation, and GPU captures for each view mode, projected lighting,
  stair preview, weather, blur, multiple visible depths, and secondary viewports;
- focused allocation and query captures for movement support, vehicles, opening searches, PVS
  recipients, stair LOS, projected-light rays, speech, ordnance, and rendering;
- burst captures for simultaneous falls, transitions, roof propagation, mapping edits, and
  topology lifecycle changes;
- live audio, atmosphere-isolation, AI non-traversal, power-domain, and long-round stability
  validation;
- repeated, order-controlled server baselines so load and tick conclusions use distributions
  rather than this single run.

No Phase 4 metric currently supports another behavioral rewrite. Optimization decisions remain
evidence-gated through `audit-log.md`.
