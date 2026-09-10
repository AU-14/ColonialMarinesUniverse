# Server performance diagnostics

## Purpose

CMU's automatic server performance diagnostics focus on gameplay stalls and client synchronization failures, with bounded incident reports in the `cmu.server-performance` sawmill.

The monitor runs at `InputPostEngine`, before catch-up simulation ticks. The previous frame has been finalized by then, so its profile can be read before catch-up work overwrites it. This hook also runs while simulation is paused. A hard frame is checked every completed server frame; broader rates are sampled on a configurable interval.

It never automatically invokes `serverperf deep` or retains per-entity details. The hot path consists of scalar reads, fixed-time scalar windows, event counters keyed only by prototype/component/map identifiers, and the engine's fixed profiler ring.

## Default behavior

- Diagnostics and healthy one-minute heartbeats are enabled.
- Log output is enabled by default (`cmu.server_performance.log_enabled = true`). An explicit saved false value still mutes it; `cmuperf status` and runtime configuration should be checked when deploying to an existing server.
- A real frame of 250 ms opens an incident immediately; 1,000 ms is critical.
- TPS or average FPS below 80% of target for three seconds opens an incident.
- Recovery requires all triggers to remain healthy for ten seconds; low-TPS/FPS signals must first reach 95% of target.
- Network throughput and main-thread allocation can independently open an incident. ECS growth/churn stays in scalar telemetry but does not open incidents by default; opt in with `churn_incidents = true`. Detailed ECS rankings remain available in manual reports.
- The flight-recorder profiler is enabled during startup so the completed frames before a trigger are still in its ring.
- Disabling the monitor unhooks its ECS event handlers and disables the profiler only when the monitor still owns that enablement; an administrator-owned profiler is left alone.
- Detailed reports have a two-minute cooldown. Incident opening/updates/recovery are never hidden by that cooldown.
- Gameplay stalls (250 ms, after the first 30 round seconds) and critical lifecycle stalls can force a fresh detailed report during an existing incident, at most once every 30 seconds. New client sync incidents have a separate 30-second capture allowance even when TPS is healthy.
- `scope=gameplay`, `paused`, `round-start`, `post-round`, `startup`, or `lobby-or-loading` separates gameplay from lifecycle work. `stall` rows identify individual frames (at most one row per second); incident close includes all observed stall counts, summed stalled-frame wall time, and suppressed row counts. Incident duration includes recovery and is not frozen time.
- The server profiler defaults to at least 65,536 events and 512 indices. Explicit config values and larger existing buffers are preserved. Startup logs show effective capacities.
- Error counts and representative existing errors are attached to detailed reports and summarized every 30 seconds when nonzero. At most 32 sources are retained and four samples are emitted, each capped at 2,048 characters. These identify coincident failures, not proof of CPU/GC attribution.
- Healthy churn baselines refresh every five minutes and at startup/round boundaries.

## Log records

Search the named sawmill or the stable prefix:

```text
cmu.server-performance
[CMU-PERF]
```

The principal records are:

| Record | Meaning |
| --- | --- |
| `startup` | Effective startup state, profiler state, metrics state, and main thresholds. |
| `runtime-metrics-disabled` | Content cannot see retained heap/RSS/GC/thread-pool counters; enable external metrics. |
| `tracking-reset` / `epoch-reset` | Bounded ECS counters and rate windows were safely re-anchored. |
| `heartbeat` | Healthy/warmup scalar snapshot. Absence is externally alertable. |
| `baseline-refresh` | New healthy prototype/component churn comparison point. |
| `incident-open` | First coalesced trigger with all scalar context. |
| `incident-reasons-changed` | Reasons were added or cleared while the incident remained active. |
| `incident-escalated` | Existing reasons worsened enough to change severity to critical. |
| `incident-update` | Periodic state while an incident continues. |
| `incident-close` | Duration, prior reasons, worst TPS/FPS/frame/allocation, and suppressed detail count. |
| `detail-begin` / `detail-end` | Bounds one profiler/ECS/network attribution report. |
| `profile-frame` | Selected slow, allocation-heavy, or tick-bearing frame and its main-thread allocation. |
| `profile-coverage` | Available/partial/overwritten history, effective capacities, unindexed events, and event-budget truncation. |
| `profile-unavailable` | Actual reason and appropriate action, rather than a generic enable-profiler hint. |
| `profile-retry` | One follow-up capture after an unavailable current frame has been finalized; bypasses the normal cooldown. |
| `stall` | Frame wall time, scope, latest input-to-input phase maximum and tick, including whether that phase contains idle time. |
| `profile-sample` | Ranked system/engine timing and allocation aggregate. |
| `profile-counter` | Integer profiler counters, including frame GC collection deltas when present. |
| `ecs-churn` | Top prototype/component net growth or map creation since the healthy baseline. |
| `inbound-network-message` | Top decoded inbound message types during the latest sample interval. |
| `detail-suppressed` | Scalar incident was recorded, but detailed parsing was still on cooldown. |
| `error-window` / `error-source` | Error rate, source counts, and bounded samples, including failures on PVS worker threads. |
| `phase-window` | Wall time between module callbacks, with the worst tick: simulation/timers/tasks, post-tick work and state sending, engine frame work, and frame tail/wait/input. The last category includes idle time and is not a CPU measurement. Windows accumulate since the previous detailed report. |

Every incident line has a stable `incidentId`. Group all rows with the same ID before drawing conclusions.
Client synchronization captures also carry `syncIncidentId`, linking to `cmu.client_state` reports. Profiler
`index` is the unique ring index; use retained tick numbers for correlation, since the engine frame counter
can remain constant on the server. GC generation counters are always included when retained, even when zero;
collection counts do not measure pause duration.

## Missing or partial profiles

The engine publishes a frame index only after its frame-post callbacks return. The older reporter ran inside
those callbacks: a busy unfinished frame could overwrite all preceding frames, leaving nothing readable.
Moving capture before catch-up fixes that ordering. Slow input work can still cause the same situation, so
an empty capture is retried once after the frame completes.

A frame can also overwrite its own start. The reader now preserves the retained tail, full frame timing and
allocation totals, and completed scopes. `partial=true` means system rankings cover only retained events;
they cannot be treated as a complete attribution of that frame. Parsing remains capped by `profile_max_events`.

- `disabled`: enable `prof.enabled` before reproduction.
- `no-completed-frames`: startup/enablement has not yet produced an indexed frame; inspect the follow-up retry.
- `history-overwritten`: increase `prof.buffer_size` and inspect `unindexedEvents` and the retry.
- `invalid-frame-data`: inspect the profiler frame/index data.

The old logs did not record buffer capacities or rejected-frame counts, so they cannot establish exactly
which loss mechanism occurred in every old incident. A larger buffer cannot recover previously overwritten events.

## Interpreting an incident

1. Start with `incident-open`: determine whether the trigger was frame/TPS/FPS, allocation, ECS growth/churn, or network throughput.
2. Compare `achievedTps` and `fps` with `targetTps`. A low achieved TPS is more useful than client FPS reports.
3. Inspect `profile-frame`, then `profile-sample category=system-time` and `system-allocation`.
4. Check `engine-time`/`engine-allocation` when entity systems do not account for the frame. Game state, PVS, network, and engine groups can dominate outside a content system.
5. Treat profiler allocation as **main-thread allocation churn**, not retained memory. A large allocator is a lead; it does not prove that its objects survive GC.
6. Inspect positive `ecs-churn net=` rows. Repeated positive prototype/component net growth is direct evidence of ECS retention and supplies the likely content type.
7. Inspect send/receive rates. Message-type attribution is decoded **inbound** traffic only; the engine does not expose per-type outbound totals through this API.
8. Correlate the incident window with external heap/RSS, GC pause, CPU, and thread-pool metrics.
9. If memory continues rising, take two dumps several minutes apart under comparable load and compare surviving type/root growth.

Use `phase-window` to distinguish a long simulation tick from a long post-tick/state-send interval when the
profiler has no finer PVS scope. `post-tick-and-state-send` includes content post-tick callbacks and game-state
generation/serialization/sending; it does not separate those substeps or identify GC pauses. Pair it with
`error-source` and external runtime metrics before attributing a stall to a particular method.
`content-frame-callbacks` now separates content service updates from `frame-tail-wait-and-input`.
`phase-window` spans reports; its maxima can belong to different ticks. The `stall` row instead uses the
latest input-to-input window, which includes the next frame's input work and does not exactly equal `frameMs`.

## Admin commands

```text
cmuperf status
cmuperf report
cmuperf reset
```

- `status` prints the latest scalar observation and active incident.
- `report` writes a bounded profiler/ECS/network report immediately, even during the automatic detail cooldown.
- `reset` re-anchors rate windows and healthy baselines without retaining entity IDs.

Existing deeper tools remain available:

```text
lagprofile start
lagprofile report 300 25
lagprofile stop 0 25
serverperf baseline 30
serverperf deep 40 120
serverperf clear
```

Run `serverperf deep` after recovery or during a controlled reproduction. It performs full-world scans and can make an already overloaded server worse. Its comparison snapshot is intentionally retained until replaced or `serverperf clear` is used.

## Configuration

All automatic-monitor CVars are server-only and archived. In the table, the first row is the full name and every following suffix is under the same `cmu.server_performance.*` prefix.

| CVar | Default | Effect |
| --- | ---: | --- |
| `cmu.server_performance.enabled` | `true` | Master switch. |
| `churn_incidents` | `false` | Enable standalone ECS growth/churn incidents and automatic ECS rankings. |
| `log_enabled` | `true` | Write diagnostic logs; does not control metrics or collection. |
| `sample_interval` | `1` s | Full observation cadence; hard stalls are still checked every frame. |
| `warmup` | `30` s | Suppresses rate/growth triggers after startup or a new round. Hard stalls/allocation/network remain active. |
| `heartbeat_interval` | `60` s | Healthy log heartbeat; `0` disables it. |
| `incident_update_interval` | `30` s | Sustained-incident update cadence. |
| `baseline_interval` | `300` s | Healthy churn baseline cadence. |
| `stall_ms` | `250` ms | Immediate hard-frame trigger; `0` disables it. |
| `critical_stall_ms` | `1000` ms | Critical severity threshold. |
| `low_tps_ratio` | `0.80` | Sustained achieved-TPS trigger fraction. |
| `low_fps_ratio` | `0.80` | Sustained average-FPS trigger fraction. |
| `breach_duration` | `3` s | Required sustained TPS/FPS breach. ECS rolling rates, network, allocation, and hard stalls trigger immediately once their own thresholds are met. |
| `recovery_ratio` | `0.95` | TPS/FPS fraction required before recovery can start. |
| `recovery_duration` | `10` s | Continuous healthy period required to close. |
| `entity_growth_per_minute` | `1000` | Rolling net entity-growth trigger. |
| `entity_churn_per_minute` | `5000` | Rolling creates+deletes trigger. |
| `component_growth_per_minute` | `10000` | Rolling net component-growth trigger. |
| `component_churn_per_minute` | `50000` | Rolling adds+removes trigger. |
| `send_mib_per_second` | `25` | Outbound traffic trigger. |
| `receive_mib_per_second` | `10` | Inbound traffic trigger. |
| `allocation_mib_per_frame` | `32` | Profiled main-thread allocation trigger. |
| `enable_profiler` | `true` | Enables the profiler during diagnostics startup if it is off. |
| `profile_frames` | `8` | Maximum selected frames in a detail report, mixing slowest, highest-allocation, recent tick-bearing, and newest frames. |
| `profile_max_events` | `20000` | Hard profiler parse bound. |
| `report_top` | `10` | Rows per detail category, clamped to 1–25. |
| `detail_cooldown` | `120` s | Minimum spacing between automatic detail reports. |

Example server configuration:

```toml
[cmu.server_performance]
enabled = true
log_enabled = true
churn_incidents = false
sample_interval = 1
heartbeat_interval = 60
stall_ms = 250
critical_stall_ms = 1000
low_tps_ratio = 0.80
low_fps_ratio = 0.80
allocation_mib_per_frame = 32
detail_cooldown = 120

[prof]
# These explicit values also replace any smaller saved settings on an existing deployment.
enabled = true
buffer_size = 65536
index_size = 512

[cmu.diagnostics]
client_state_enabled = true
client_state_health_enabled = true
```

The correct logging key is `cmu.server_performance.log_enabled`; the old
`log.cmu.server_performance.log_enabled` key is invalid. These content changes must be deployed to both
server and clients for application-progress reports. Runtime metrics below still need external scraping;
the diagnostic logger does not claim to measure GC pauses or retained heap itself.

Increasing profiler rings preserves more pre-trigger history but consumes more fixed memory and makes a report scan larger. The automatic parser still caps frames and events.

## Runtime and process telemetry

The content sandbox does not allow direct calls to `GC`, `Process`, or `ThreadPool`. The automatic logs therefore cannot honestly report retained managed heap, process working set/private bytes, CPU, handles, thread count, or thread-pool starvation.

Use the existing engine metrics endpoint for that layer:

```toml
[metrics]
enabled = true
# Keep this on loopback or a private monitoring network; do not expose it publicly without access controls.
host = "127.0.0.1"
port = 44880
runtime = true
runtime_gc = "Counters"
runtime_thread_pool = "Counters"
runtime_contention = "Counters"
```

Verify the exact names on `/metrics`; exporter versions can rename or suffix instruments. Relevant families normally include:

- `process_working_set_bytes`, `process_private_memory_bytes`, CPU, threads, handles;
- `dotnet_total_memory_bytes` and `dotnet_gc_heap_size_bytes`;
- GC allocation totals, collection counts, pause ratio, finalization and pinned-object signals;
- thread-pool queue length, throughput, and worker/I/O thread counts;
- engine tick/frame histograms and `robust_entity_systems_update_usage`;
- `cmu_server_performance_*` gauges/counters from this monitor.

## External alerts

An in-process monitor cannot execute while the main thread is permanently deadlocked, starved, or terminated by OOM. Alert on its wall-clock heartbeat from outside the process:

```yaml
groups:
  - name: cmu-server-performance
    rules:
      - alert: CMUServerPerformanceIncident
        expr: cmu_server_performance_incident_active > 0
        for: 15s

      - alert: CMUServerMetricsDown
        expr: up{job="cmu-server"} == 0
        for: 1m

      - alert: CMUServerMainLoopStale
        expr: (time() - cmu_server_performance_last_update_unix_seconds > 30) and (cmu_server_performance_enabled == 1)
        for: 15s

      - alert: CMUServerLowTPS
        expr: (cmu_server_performance_tps / cmu_server_performance_target_tps < 0.8) and (cmu_server_performance_enabled == 1)
        for: 2m

      - alert: CMUServerWorkingSetGrowing
        expr: deriv(process_working_set_bytes[15m]) > 1048576
        for: 15m
```

Tune growth thresholds to normal round loading and player count. A positive working-set slope alone is not proof of a managed leak; compare it with managed heap, GC behavior, ECS net growth, and dumps.

## Capture escalation

When the monitor opens a sustained memory/performance incident:

1. Preserve the server log and scrape history around the incident ID.
2. Run `cmuperf report`; avoid repeated `serverperf deep` during active collapse.
3. Record process/container CPU, RSS/private bytes, managed heap, allocation rate, GC pauses, and thread-pool queue.
4. For CPU/TPS collapse, collect a bounded `dotnet-trace` or equivalent sample during the incident.
5. For suspected retention, collect two `dotnet-dump`/gcdump artifacts several minutes apart under similar player/load conditions.
6. For a hard hang, have the external watchdog collect a dump before restart. The in-process logger cannot do this while its own thread is stuck.
7. Store dumps securely: they can contain player data, secrets, chat, and other live process contents.

## Overhead and safety properties

- No per-entity UID/name collections are retained.
- Prototype/component/map dictionaries have content-defined key cardinality, not event cardinality.
- Rate windows store only capped scalar points.
- Profiler reads are capped and use the existing fixed ring; automatic reports do not clone the whole ring.
- Metrics use no player, UID, prototype, component, map, round, or incident labels.
- Player identities are never written by this monitor.
- A one-time world count seed occurs at startup/re-enable/entity flush; incident capture itself does not scan the world.
