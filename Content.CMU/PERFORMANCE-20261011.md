# Crowded movement: HUD cache retention and footprint work

## Scope

This pass targets walking around the map with many players nearby, following
the user's reported freeze scenario. It builds on the merged performance PR
#2135, using CMU master `1881202130ac29be7f2c448e3c6c686727b4ac9a`.
The tracked RobustToolbox revision is
`fee0dd647ebe95ad62d8945440450fb6cd4749a7`; no engine sources or dependency
revisions are changed.

## Changes

- Marine icons, status icons and health progress previously cleared their
  entire entity cache on overflow. A new arriving entity could therefore make
  the remaining visible crowd rebuild its snapshots within the same frame.
  Each cache now evicts just its least recently used entry and stays bounded
  at 512 entries. Recently drawn entities remain cached. Replacement reuses
  the bookkeeping node, but never reuses another entity's payload object.
- Existing 0.25-second status/health and 0.5-second marine expiry rules remain
  in their callers, as do health-state/damage invalidation and faction,
  spectator and visibility checks. This does not remove normal expiry work.
  A working set larger than 512 entries can still cause repeated misses.
- The xenomorph visualizer reuses the nine existing bloody RSI state names
  instead of concatenating a new string on every normal visual update.
  Custom/future state names retain the previous suffix behavior.
- The server checks whether a bloody footprint stride is due before locating
  the entity's grid. Short translations and rotation-only movement below the
  stride therefore skip that spatial lookup. Grid validation still precedes
  footprint mutation; walking, dragging, blood fading and decals retain their
  previous behavior. Clean feet already skipped this work before this pass.

## Local verification (2026-10-11)

Environment: Windows, .NET 10.0.8, Release, i7-12700F, approximately 32 GiB RAM.
Checks ran sequentially against the same checkout.

- Affected integration-test project and client/server dependencies built with
  zero errors. Final incremental build: 75 warnings, including existing
  package advisory and obsolete-API warnings.
- Unit tests: **19/19 passed**, including existing xenomorph HUD visibility
  coverage. The cache test retained 96 recently visible entries while 2,000
  new entries arrived, with capacity remaining bounded.
- Integration tests: initial run **13/14 passed**. The new critical-movement
  test fixture lacked `Damageable`, which the actual mob-state event requires;
  that test prototype was corrected without changing production behavior.
  The walking/dragging fixture then passed **2/2** on rerun, verifying all
  **14 distinct integration cases** across the runs. The full 14-case filter
  was not repeated after that test-only correction.
- Actual client `StatusIconSystem` events: after warming 512 entries and
  adding two more, a recently visible entity had **zero extra rebuilds**;
  after advancing the clock beyond expiry it rebuilt **once** and returned
  its updated icon. The former whole-cache clear fails this scenario.
- Existing footprint and blood-contact coverage passed: walking and dragging
  decals, wound tiers, treatment and floor-contact staining.
- Warmed single-thread allocation measurements:

  | Operation | Iterations | Allocated bytes |
  | --- | ---: | ---: |
  | Full-cache bookkeeping replacement and lookup | 10,000 | 0 |
  | Known xenomorph bloody-state name lookup | 90,000 | 0 |

  These measure only those operations. Creating cache payloads, rebuilding
  expired icons, drawing sprites and the overall client still allocate.
- Source whitespace and UTF-8/LF checks passed.

The initial quiet restore/build was interrupted after stalling. Subsequent
builds used restored dependencies and completed with `--no-restore`.

## Reproduce

From the repository root, after restoring the current dependencies:

```powershell
dotnet build Content.IntegrationTests/Content.IntegrationTests.csproj -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false --verbosity quiet -clp:ErrorsOnly
dotnet test Content.Tests/Content.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~CMUOverlayCacheTest|FullyQualifiedName~CMUXenoBloodyStateTest|FullyQualifiedName~XenoHudOverlayTest' --logger 'console;verbosity=normal'
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~CMUStatusIconCacheTest|FullyQualifiedName~CMUFootprintStrideTest|FullyQualifiedName~FootPrintsSystemTest|FullyQualifiedName~BloodContactFootprintsTest' --logger 'console;verbosity=normal'
```

No production YAML/prototype files change in this pass. The embedded movement
test prototype is loaded by the integration tests.

## Baseline CI and remaining evidence

The unchanged base master's [CI run 38158328235](https://github.com/AU-14/ColonialMarinesUniverse/actions/runs/38158328235)
already fails two map-related groups:

- `cmu_yautja_maps`: `PlayablePlanetMapsHaveGroundRelayMarkers` reports
  `/Maps/CMU14/Planets/FortMcNeil/FortMcNeilMultiZ0.yml` missing a ground relay.
- `map_loading`: `PlanetLoadsWithoutErrorsTest` reports the Fort McNeil brown
  cargo truck using `Dynamic` where its movement handler expects
  `KinematicController`, plus an invalid entity reference in `UserInterface`.

These map failures are outside this focused pass; local checks do not establish
that the full repository CI is green.

No live multiplayer FPS, frame-time percentiles or server tick-time comparison
has been captured for this pass. To validate the reported scenario, record
`cmu_client_perf start 300 20`, close the console, and walk through the same
crowded route before and after the change with matching camera/settings.
Use `cmu_client_perf open` to retrieve the logs, and collect `cmuperf report`
on the server during the same incident window. Compare frame spikes, GC and
server timings; isolated allocation results do not prove freeze-free play.
