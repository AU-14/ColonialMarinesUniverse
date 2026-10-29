# Telemetry-driven content fixes

The October 28, 2026 telemetry capture identified large allocation bursts during
third-party deployment, weed-killer smoke creation and `serverperf` snapshots.
These changes use content APIs on stock RobustToolbox v294.0.1. They do not modify
Toolbox, its submodules, or the engine pin selected earlier in this PR.

## Resulting behavior

- Queued third parties prepare once, then spawn at most one body or equipment
  entity per server update. One deployment runs at a time. Missing prerequisites
  can retry after 30 seconds; partial deployments and exceptions are not retried
  because doing so could duplicate bodies. Round cleanup cancels pending steps.
  Explicit immediate and round-start calls still complete synchronously. Legacy
  `QueueForce` callbacks, including threat spawning, remain synchronous per step.
- A shuttle loaded for rejected third-party preparation is deleted together with
  its generated map and children. Repeated marker-validation failures previously
  retained those loads. Accepted deployments keep their shuttle.
- `serverperf` snapshots retain component counts instead of three name arrays per
  entity. Names are gathered and sorted only for the selected detail rows. Churn
  comparisons and the detail output remain available.
- Weed killing and area suppression remain authoritative on the server. Smoke
  travels as batches of up to 1,024 tile coordinates per grid. Clients use the
  existing smoke sprite prototype, creating at most 32 nearby effects per rendered
  frame and expiring them against the original 1.5-second server lifetime. Work
  outside the current eye's range may expire without creating an effect. Completed
  weed-killer work releases its tile references.
- Heartbeats continue at the configured cadence during incidents, carrying their
  incident ID and severity. Healthy baselines remain frozen during an incident.
  Error samples escape line breaks so filtered `CMU-PERF` logs retain exception
  details; the existing bounded sample length still applies.
- Client rubberband diagnostics compare player displacement relative to an
  unchanged parent. Inherited shuttle movement is retained as an informational
  `parent-motion` record with `worldDist`, separately from player snap warnings.
  Physics, movement and relay descriptions are formatted only when a record is
  emitted, instead of allocating strings on every rendered frame.
- Lemon-drop boxes now accept only Tiny candy, allowing the filled box to nest
  inside the Small-limited ration packs instead of being rejected by StorageFill.

## Measurements

Temporary harnesses used the same isolated Debug integration environment and
`GC.GetAllocatedBytesForCurrentThread`. They were removed after measurement.
Values below are decimal MB/KB, not a live-round benchmark.

| Operation | Before | After | Scope |
| --- | ---: | ---: | --- |
| Entity snapshot collection | 25.31 MB | 5.75 MB | Median of three runs over 10,025 mostly empty entities; about 77% fewer allocations |
| Weed-killer smoke on 1,024 tiles | 11.70 MB / 112.1 ms | 25.36 KB / 0.38 ms | Server-only harness with no connected clients; old smoke spawns versus the new weed-killer update on empty tiles |

The smoke measurement excludes serialization to clients and client rendering.
The snapshot measurement does not cover the entire `serverperf deep` report.
The tests verify deployment pacing, not a measured improvement in live server TPS.

## Verification

The isolated wrapper compiled the affected shared, client and server projects.
The final combined integration run passed **15 cases, zero failures or skips**:
existing force-interest behavior, deployment pacing/cancellation/retry, immediate
deployment, rejected shuttle cleanup, manual shuttle launch, snapshot churn/detail
output, authoritative weed killing and client smoke delivery/expiry.

```powershell
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Filter 'FullyQualifiedName~ServerPerformanceSnapshotTest|FullyQualifiedName~ForceInterestTest|FullyQualifiedName~ForceDeploymentBudgetTest|FullyQualifiedName~WeedKillerDeploymentTest|FullyQualifiedName~RmcErtThirdPartyDropshipMapTest.ThirdPartyShuttleSpawnWaitsForManualLaunch'
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.Tests -Filter 'FullyQualifiedName~CMUPerformanceErrorTrackerTest|FullyQualifiedName~CMUPerformanceLogFormattingTest'
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Filter 'FullyQualifiedName~CMURubberbandFollowupTest.PredictionRewindsDoNotHidePostReplaySnaps'
```

The focused diagnostics unit run passed **four cases**. A temporary real-spawn
check verified that a box still fills with eight candies and inserts into a
cleared TWE ration pack. It was removed rather than retaining a prototype-default
test. Existing analyzer warnings remain. No graphical playthrough or deployment
was performed.

After the final client smoke collection change, its two integration cases passed
again. The extended rubberband regression also passed, checking real transform
changes to separate parent movement while preserving post-replay correction reports.

## Remaining limits

Shuttle preparation still uses synchronous engine map loading, and an individual
body may still be expensive to spawn. The capture's multi-second GC pauses and
large within-round heap growth are not proven resolved by these fixes. Rejected
shuttle loads were a verified retention path, not an explanation for all growth.
The capture lacked full exception stacks for many runtime errors; subsequent
filtered logs will retain more useful details. The documentation-generation
system named in telemetry was unavailable in this checkout and is unchanged.
