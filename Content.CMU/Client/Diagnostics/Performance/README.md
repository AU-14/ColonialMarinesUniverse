# Client FPS capture

Open the in-game console and run `cmu_client_perf`. Close the console and reproduce the FPS drop.
It records for 120 seconds, then stops automatically. Run `cmu_client_perf open` to open the output
folder and share the `client-perf-*.log` file. No admin permissions or server command is required.

Commands:

- `cmu_client_perf start 300 20`: record five minutes, count frames at or above 20 ms as spikes.
- `cmu_client_perf status`: remaining time and output filename.
- `cmu_client_perf report`: detailed checkpoint with a fresh entity inventory; capture continues.
- `cmu_client_perf stop`: finish early and flush the report.
- `cmu_client_perf open`: open the client user-data `/client-performance` folder.

Capture a healthy scene and the bad scene in the same recording when possible. Include what you
were doing and roughly when FPS fell when sharing the report. Closing the console matters because
rendering console text is real UI work, and the report also goes to the `cmu.client-performance` sawmill.

## Evidence recorded

- Five-second frame windows: measured FPS, mean/p50/p95/p99/max wall frame time, spike counts,
  window focus, and the frame number of the worst wall interval.
- Every available completed profiler frame, with separate worst-work, worst-allocation and worst-wall snapshots.
  Nested paths distinguish prediction, tick updates, frame updates, UI, render passes and buffer swaps.
  Rows are selected by cumulative time, worst call and allocated bytes so a one-off stall is retained.
- Engine counters with their full parent path: draw calls, batches, lights, occluders, GC collections,
  state application, PVS transitions, prediction and other counters the engine actually emitted.
- Network traffic/packet deltas, ping, state-buffer sizes, tick backlog and entity/component churn.
- Multi-Z opening/LOS work, pass timings, culling, stair previews, projected-light candidates,
  raycasts, portal queries, cleanup, grace and relevant rendering settings.
- Loaded entity/component/prototype/map inventories and changes, at startup, every 15 seconds,
  on manual reports and at the end. Inventories include paused entities. They are not visible draw counts.
- Profiler data loss, truncation, capture-reader cost and report-generation cost.

## Interpretation and bounds

Times are milliseconds. Timing scopes are **inclusive** and overlap; never sum parents and children.
Allocations are bytes allocated on the sampled main thread, not retained memory. Root `Frame` work
excludes the engine sleep/FPS limiter, but render/swap scopes can contain GPU/driver waiting. Wall
frame time includes the interval between frames. GC counters show collections, not pause duration.
This does not measure GPU utilization/VRAM, process memory, background-thread allocations or call
stacks. Use the scope names and scene counters to choose a targeted follow-up investigation.

Viewport and projected-light counters are labeled with their own sample sequence and observation
frame: they are the latest available samples and may come from different phases/viewports. A low
FPS window with substantial swap time can reflect VSync, GPU/driver waiting or frame limiting; it
does not by itself prove a CPU rendering bottleneck. Missing profiler samples are reported as missing,
not interpreted as zero workload.
Each report includes `lostFramesWindow` and `allocationRatePartial`. When the latter is true,
`bytesPerSecond` is a lower bound from retained frames, not a complete allocation rate.

Disabled by default. Only the active capture subscribes to churn events. Parsing reuses an interned
scope tree, scans at most eight newly completed frames per update and 50,000 events per frame, and
retains at most 4,096 distinct paths. Oversized frames retain root timing with details marked missing.
Periodic reports retain separate slowest-work, slowest-wall and highest-allocation frames within each window;
they are not a full raw trace of every frame. Percentiles use up to 4,096 wall samples per window,
with omitted samples reported. Inventories stop at 50,000 entities or 300,000 components, explicitly
mark truncation and suppress comparisons against incomplete inventories.

Startup/report frames are excluded from both profiler aggregation and wall statistics to avoid
diagnostic dumps creating their own incidents. The per-frame reader and profiling instrumentation
still add measurable overhead. Reports flush to disk every five seconds. The default capture ends
after two minutes; durations are limited to 5–1,800 seconds. Captures temporarily reserve at least
262,144 profiler log entries so busy frames survive until the reader runs. A larger existing buffer
is preserved. The command restores the previous buffer size and profiler and Z
diagnostic settings it enabled, preserves settings that were already on, and yields ownership when
the user changes those settings during a capture. File/diagnostic errors stop collection and release
the hooks. No engine-submodule changes are required.
