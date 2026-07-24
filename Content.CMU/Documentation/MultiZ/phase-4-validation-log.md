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

### MZ-001, MZ-045, and MZ-046 live client rendering

The client-rendering protocol used a Release OpenGL client on an NVIDIA GeForce RTX 4070 Ti SUPER
at 1280x720, VSync off, and no client FPS cap. The server retained all five `USSBushRedux` maps at
depths `[-1, 0, 1, 2, 3]`. An attached observer remained at map 1, `(-15.5, -4.5)`, and a runtime
`CMUMultiZStairs` at that position enabled the stair-preview path. Each distribution contains
3,600 rendered frames with the same positioning overlay and scene.

The baseline made 104 projected source-to-opening physics rays per frame. Candidate work was
0.2438/0.3579 ms p50/p95 of the 0.2661/0.3986 ms projected-light total, identifying the
result-materializing ray query as the measured projected-light bottleneck. A generic occluder
first-hit replacement was tested and rejected after it disagreed on 12 of 104 ray decisions every
frame. A CMU-owned first-hit traversal of the current physics broadphases then matched the old
result on all 374,400 comparisons (104 rays times 3,600 frames). The accepted predicate preserves
map, maximum distance, ignored source entity, opaque collision layer, and hard-fixture semantics.

Stair preview tested 349 intersecting sprite candidates and issued 349 LOS checks every frame.
Its LOS query now uses the allocation-free occluder first-hit API with the previous
endpoint-touching rule. The same exact replacement is used for current-view projected-light
opening visibility; that particular opening path was not active in this sampled scene.

Paired results were:

| Metric | Before p50 / p95 | After p50 / p95 | p50 / p95 change |
| --- | ---: | ---: | ---: |
| Client frame | 2.3597 / 3.5518 ms | 2.1859 / 3.2635 ms | -7.37% / -8.12% |
| Multi-Z render | 1.4533 / 2.1917 ms | 1.3485 / 2.0321 ms | -7.21% / -7.28% |
| Projected lighting | 0.2661 / 0.3986 ms | 0.2131 / 0.3307 ms | -19.92% / -17.03% |
| Projected candidates | 0.2438 / 0.3579 ms | 0.1896 / 0.2890 ms | -22.23% / -19.25% |
| Stair render | 0.8075 / 1.2290 ms | 0.7382 / 1.1056 ms | -8.58% / -10.04% |
| Stair culling | 0.4922 / 0.7479 ms | 0.4277 / 0.6569 ms | -13.10% / -12.17% |

Behavioral work counts were unchanged:

| Work per rendered frame | Before | After |
| --- | ---: | ---: |
| Projected rays | 104 | 104 |
| Projected candidates | 9 | 9 |
| Projected lights applied | 5 | 5 |
| Stair sprite candidates / checks | 349 / 349 | 349 / 349 |
| Stair LOS checks | 349 | 349 |
| Lower passes / stair composites | 1 / 1 | 1 / 1 |

The same baseline scene also captured blur on/off:

| Metric | Blur disabled p50 / p95 | Blur enabled p50 / p95 | Enabled p50 cost |
| --- | ---: | ---: | ---: |
| Client frame | 2.2633 / 3.3401 ms | 2.3597 / 3.5518 ms | +4.26% |
| Multi-Z render | 1.3755 / 2.0526 ms | 1.4533 / 2.1917 ms | +5.66% |
| Lower render | 0.0877 / 0.1350 ms | 0.0966 / 0.1534 ms | +10.15% |

The validated after capture recorded one lower and one blur pass per frame. This establishes a
measurable one-pass CPU/frame cost, not isolated GPU copy or shader time. A multi-depth scene can
apply the blur sequentially; consolidating those passes would change accumulated blur strength.
No blur production behavior was changed without an explicit visual contract and GPU timestamps.

Raw client logs are
`artifacts/multiz-phase4/working/mz-render-before-client.stdout.log`,
`artifacts/multiz-phase4/working/mz-render-compare-client.stdout.log`,
`artifacts/multiz-phase4/working/mz-render-exact-compare-client.stdout.log`, and
`artifacts/multiz-phase4/working/mz-render-after-client.stdout.log`. The checked-in
`evidence/mz-001-045-046-client-rendering.json` preserves exact distributions and SHA-256 hashes.

Targeted gates passed: Release `Content.Client` built with zero warnings/errors, and the DebugOpt
`ZLevelBlurOverlayTest | StairPreviewVisibilityTest | StairPreviewOriginTest` filter passed 14/14
tests. The representative stair scene exercised sprite/LOS work but no nonempty FOV-mask tiles.
Tile-heavy stair preview, isolated GPU copy/shader timestamps, multi-depth blur, receiving-map
projected-light shadows, and secondary-viewport lighting remain explicit validation risks.

### MZ-014 and MZ-040 topology, roof, and power bursts

The paired protocol used the Release evidence runner, `USSBushRedux`, the same five depths
`[-1, 0, 1, 2, 3]`, 30 warm-up ticks, 30 profile ticks, one viewer, one PVS sample, no soak, and
seed 42. New load profiling starts before lifecycle construction and ends after two server ticks;
teardown profiling brackets bottom-up deletion of all five maps. Both captures passed all hard
map-count, depth, teardown, profiler, viewer, and PVS lifecycle gates.

The baseline showed two different costs:

| Initial-load work | Baseline |
| --- | ---: |
| Required roof rebuild | 1.1536 ms; 322,640 bytes |
| Roof maps / tiles visited / tiles written | 5 / 20,095 / 20,095 |
| Redundant global power requeue | 0.1284 ms; 0 bytes |
| APCs / receivers / reactor groups enumerated | 47 / 1,174 / 23 |
| Already-pending APC/receiver area updates | 1,221 |
| Legitimate area refresh max / allocation | 1.7006 ms / 92,136 bytes |

The 1,221 pending updates exactly equal the 47 APCs plus 1,174 receivers, establishing that the
global topology requeue duplicated `MapInit` scheduling rather than creating required work.
Initial roof state, by contrast, requires the full top-down traversal.

The modernization adds an event delta for rebuilt versus removed map/depth, resolves removals
through the removed map's direct network owner with the previous scan as recovery, writes roofs
only below the removed depth, and queues only the affected network's reactor power group. It adds
no cap, speculative cache, delayed visibility, changed roof rule, or new power-domain policy.

After the change, initial roof work remained five maps and 20,095 visited/written tiles. Its single
sample was 0.8409 ms and 323,168 bytes, but that host-timing difference is not claimed as an
improvement because the behavior and work are unchanged. The global APC/receiver/reactor requeue
was absent, while all 1,221 legitimate area updates and their 92,136 scoped bytes remained.

Bottom-up teardown produced the stable affected-path result:

| Metric | Before | After | Change |
| --- | ---: | ---: | ---: |
| Roof rebuilds | 4 | 0 | -100% |
| Roof maps visited | 10 | 0 | -100% |
| Roof tiles visited / written | 38,298 / 38,298 | 0 / 0 | -100% / -100% |
| Direct ownership hits | 0 | 5 | all removals |
| Fallback networks scanned | 5 | 0 | -100% |
| Topology removal p50 | 0.2816 ms | 0.0279 ms | -90.09% |
| Topology removal maximum | 0.8716 ms | 0.1358 ms | -84.42% |
| Topology removal sum | 1.7023 ms | 0.2145 ms | -87.40% |
| Maximum scoped allocation | 8,400 bytes | 8,400 bytes | unchanged |

Bottom-up deletion is a specific no-lower-level case, not a claim that every removal is free. The
focused DebugOpt Bush filter passed 2/2 in 1 minute 17 seconds; its lifecycle case creates aligned
lower and middle tiles, verifies the middle deck roofs the lower one, removes the middle map, and
verifies that the lower roof contribution is cleared. The Release `Content.Benchmarks` build
passed with zero warnings and errors.

Raw evidence is
`artifacts/multiz-phase4/20260724-mz014-040-before-v3/evidence.json` and
`artifacts/multiz-phase4/20260724-mz014-040-after/evidence.json`. The checked-in
`evidence/mz-014-040-topology-bursts.json` preserves exact samples, hashes, validation, and risks.

Bush had no reactor-powered-light entities, so the remaining whole light query needs a populated
topology-mutation capture. The required initial 20,095-tile build remains synchronous and
approximately 323 KB. Representative large middle/upper removals, `znetwork-variantize`, and bulk
mapping edits also remain unmeasured; none receives a speculative cache, cap, or work budget here.
The existing shared RMC Z-network power domain is preserved pending the separate MZ-064 gameplay
decision.

### MZ-008, MZ-037, MZ-052, and MZ-053 networking and replication

The deterministic protocol used the Release evidence runner, `USSBushRedux`, depths
`[-1, 0, 1, 2, 3]`, 30 warm-up ticks, 30 profile ticks, one viewer, one PVS sample, no soak, and
seed 42. Both captures passed every hard five-map, teardown, profiler, viewer, and PVS lifecycle
gate. The new replication section serializes the exact generated state returned for every
component instance; it is an isolated state-size measurement, not a prediction of compressed
packet size.

| Isolated state metric | Before | After | Change |
| --- | ---: | ---: | ---: |
| Canonical topology network | 49 bytes | 28 bytes | -21 bytes |
| Five map topology states | 57 bytes / 5 states | 0 bytes / 5 null states | -57 bytes |
| Total topology | 106 bytes | 28 bytes | -78 bytes (-73.58%) |
| Bush Z physics | 84,140 bytes / 6,010 states | 66,110 bytes / 6,010 states | -18,030 bytes (-21.43%) |
| Z physics per state | 14 bytes | 11 bytes | -3 bytes (-21.43%) |
| Control Z physics | 43,750 bytes / 3,125 states | 34,375 bytes / 3,125 states | -9,375 bytes (-21.43%) |
| Representative vehicle traversal | not captured | 0 bytes / 1 null state | marker-only proof |

The topology change replicates only depth-to-map data and derives reverse/neighbour indexes on the
client. The physics change stops serializing default immutable bounciness. Falling activation is
not removed: it moves from a networked add/remove marker to one explicit boolean on the already
replicated physics state, preserving client visuals and predicted vehicle drift. The server keeps
the marker solely as its active query set. Bush had zero falling markers and zero vehicle traversal
components at capture time, so neither receives a Bush burst-volume claim.

The real-network protocol ran the Release server and graphical client against the five-map Bush
scenario. A server diagnostic armed before connection recorded cumulative transport exactly when
the session entered `InGame`; after 40 seconds of client warm-up, a separate reset bracketed a
20-second steady window. Both paired processes exited 0 and the client reached `InGame`.

| Real late-join metric | Before | After | Observed change |
| --- | ---: | ---: | ---: |
| Client full-game-state size | 929,316 bytes | 929,192 bytes | -124 bytes (-0.013%) |
| Server sent bytes at `InGame` | 226,278 | 226,278 | 0 |
| Server sent packets at `InGame` | 346 | 347 | +1 |

The full-state observation is one whole-world pair and is smaller than cross-run world/timing
variation. Transport compression/framing produced no byte saving at the milestone, so no real
packet improvement is claimed. Across three identically configured 20-second windows, baseline
sent-byte deltas ranged from 25,611 to 53,816 and after deltas ranged from 26,706 to 76,726; this
noise is not attributed to the component changes.

The after build also ran with `net.fakelagmin=0.1` and zero random lag on both server and client.
The client completed a 929,243-byte full state, reached `InGame`, remained connected through the
20-second window, and both processes exited 0. This validates connection/state application under
the injected delay only; the logs provide no round-trip percentile and the scenario did not drive
a vehicle over an edge.

Raw deterministic evidence is
`artifacts/multiz-phase4/20260724-mz008-037-052-053-before/evidence.json` and
`artifacts/multiz-phase4/20260724-mz008-037-052-053-after-v2/evidence.json`. The comparable real
pair is under `20260724-mz-network-before-real-v4-armed` and
`20260724-mz-network-after-real-v4-armed`; the latency capture is
`20260724-mz-network-after-latency100ms`. The checked
`evidence/mz-008-037-052-053-replication.json` records hashes, exact counters, validation, and
deferred risks.

The combined DebugOpt focused filter passed 2/2 in 1 minute 1 second with no skips. It verifies
exact five-map client topology after initial connection and reconnect, falling true-to-false
replication without the server marker, and marker-only vehicle defaults. Release
`Content.Benchmarks` and `Content.Server` builds passed with zero warnings and errors.

The remaining risks are production vehicle/falling population, a driven edge-transition
prediction/correction capture under latency, populated-round packet distributions, and repeated
late-join trials with controlled world state. No visibility cap, replication throttle, or
speculative state cache was introduced.

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
