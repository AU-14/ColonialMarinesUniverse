# Client memory diagnostics
cmu-cmd-client-memory-desc = Prints client entity, component, prototype, and map counts.
cmu-cmd-client-memory-help = Usage:
    cmu_client_memory snapshot [top=15]
    cmu_client_memory baseline [top=15]
    cmu_client_memory diff [top=15]

    Use baseline, wait while counts grow, then diff.
cmu-cmd-client-memory-baseline = Client memory baseline captured at tick { $tick }.
cmu-cmd-client-memory-unknown-mode = Unknown mode '{ $mode }'.
cmu-cmd-client-memory-title = == CMU Client Counts ==
cmu-cmd-client-memory-tick = Tick: { $tick }
cmu-cmd-client-memory-sandbox-note = Runtime memory, GC, process, and ThreadPool counters are omitted by the content sandbox.
cmu-cmd-client-memory-counts = Entities: { $entities }{ $entityDelta } | Components: { $components }{ $componentDelta }
cmu-cmd-client-memory-top-components = Top components
cmu-cmd-client-memory-top-prototypes = Top prototypes
cmu-cmd-client-memory-maps = Maps
cmu-cmd-client-memory-section = == { $title } ==
cmu-cmd-client-memory-none =   none
cmu-cmd-client-memory-row = { $index }. { $key } count={ $count }{ $delta }

# Client performance capture
cmu-cmd-client-perf-desc = Captures detailed client FPS, allocation, rendering and prediction diagnostics to a local log file.
cmu-cmd-client-perf-help = Usage: cmu_client_perf [start [seconds=120] [spike_ms=33.333]] | stop | report | status | open | help
    Run with no arguments, close the console and reproduce the FPS drop. Capture stops automatically.
    Use stop to finish early, report for a detailed checkpoint, and open to find the .log file to share.
    Duration: 5–1800s. Spike threshold: 1–10000ms (decimal point). Profiling adds overhead while enabled.
cmu-cmd-client-perf-join-server = Join a server before capturing client performance. Use cmu_client_perf open to find earlier captures.
cmu-cmd-client-perf-failed = Client performance diagnostics failed: { $error }

# Server performance diagnostics
cmu-cmd-server-perf-desc = Shows or manually captures CMU automatic server performance diagnostics.
cmu-cmd-server-perf-help = Usage: cmuperf status | report | reset
cmu-cmd-server-perf-report-written = Detailed CMU performance report written to the cmu.server-performance sawmill.
cmu-cmd-server-perf-report-unavailable = Performance diagnostics are disabled or have not produced their first sample.
cmu-cmd-server-perf-reset = CMU performance rate windows and healthy baselines were reset.
cmu-cmd-server-perf-reset-unavailable = Performance diagnostics are disabled or not initialized.

# Medical client diagnostics
cmu-cmd-medical-perf-desc = Reports nearby CMU medical client visibility and overlay candidate counts.
cmu-cmd-medical-perf-help = Usage: cmu_medical_perf [range=12]
cmu-cmd-medical-perf-no-entity = You must be attached to an entity.
cmu-cmd-medical-perf-nullspace = Attached entity is in nullspace.
cmu-cmd-medical-perf-heading = CMU medical perf around { $range }m:
cmu-cmd-medical-perf-toggles =   local toggles: statusIcons={ $statusIcons }, marineOverlay={ $marineOverlay }
cmu-cmd-medical-perf-hud =   local HUD comps: healthBars={ $healthBars }, healthIcons={ $healthIcons }, marineIcons={ $marineIcons }
cmu-cmd-medical-perf-nearby =   nearby visible entities: { $count }
cmu-cmd-medical-perf-bodies =   nearby CMU bodies: { $count }
cmu-cmd-medical-perf-internals =   visible attached CMU internals: { $count }
cmu-cmd-medical-perf-status-candidates =   status icon candidates: { $count }
cmu-cmd-medical-perf-health-candidates =   health bar candidates: { $count }
cmu-cmd-medical-perf-marine-candidates =   marine icon candidates: { $count }
