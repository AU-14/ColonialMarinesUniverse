# RobustToolbox v294.0.1 integration

Implemented October 28, 2026, from content commit
`6043303a5c5a9e379a0346d58f498d63f38f5b24`.

## Engine inputs

- Previous release: v290.0.0, `fee0dd647ebe95ad62d8945440450fb6cd4749a7`.
- Stock release pin: v294.0.1, `cdf8d1258e8f9117b6a74fa80132143350250f16`.
- NetSerializer: `ae4c2543cda63173536aaff6c531f2d2e1e3bf26`.

RobustToolbox is unmodified, and no custom engine distribution is required.
Neither engine nor content was published as part of this implementation.

## Content changes

1. Mutable stump entries implement the engine clone contract. Client state
   correction no longer shares the live tourniquet clamp/refund objects.
2. Aircraft and seats send changed fields at the existing network cadence.
   The engine supports **32 delta fields**, so the aircraft's 46 values use
   three immutable scalar groups and 28 separate fields. Entity references
   retain the generator's network-ID conversion. Camera movement cadence is
   unchanged; action handlers can still request full state.
3. Weapon refresh compares deterministic snapshots. Idle loadouts and targets
   no longer dirty the component every scan; moving/expiring targets and
   server fire validation retain their refresh paths.
4. Ghost and vehicle rendering use indexed `LayerData` access.
5. Gunship HUD strings use measurement without transparent draw calls.
   Existing text baselines and font scale are preserved.
6. Only the XM99A's `AU14PlasmaShot` opts into fixture-accurate hitscan.
   Unsupported edge/chain geometry retains legacy blocking, and an origin
   inside a solid fixture blocks at distance zero. Other weapons retain their
   existing raycasts. Container and explicit-target rules remain in the shared
   hitscan handler.

The release migration also replaces removed reanchor hooks and anchored
setters, preserves docking across grid transfers, and updates a test event
subscription to the engine's by-ref signature.

## Deferred construction change

The stock `IGamePrototypeLoadManager.SendGamePrototype` API returns `void`;
validation rejection is handled internally. It provides no supported acceptance
result for the construction save path. The proposed save-acceptance change and
its custom-API test were removed, restoring the original construction code.
The content must not infer acceptance from the absence of an exception or
inspect private engine state. A future complete solution needs a supported
upstream API or a separately designed content protocol.

Build provenance and its related publishing changes are excluded from this
implementation. Packaging and publishing retain their original behavior.

## Verification

### Follow-up content system efficiency work

- Radio panels send revisioned telemetry for drift instead of rebuilding and
  transmitting the preset dictionaries, frequency directory, traffic log and
  sweep contacts. Expert readings remain live, and reopening sends full state.
- Relay coverage indexes channel membership. Pack configuration, mast keys,
  splicing and component lifecycle update that index; position, power, range
  and vertical reach are still read at lookup time. Fresh jammer bearings also
  supply the tick's cached intensity reading, avoiding a second query.
- Reconstruction reuses contact buffers and compares before allocating a wire
  snapshot. Completed geometry produces no empty patches. Background preload
  scheduling visits active transfers and retains the existing request throttle.
- Expedition fire and acid membership is gathered on demand; bounds and tile
  indexes are built only for queried maps and refreshed for the next update.
  Decision budgets and native weapon execution are unchanged.
- 3D viewers share one network topology snapshot per update; each viewer still
  checks access and updates probe positions and visibility every tick.
- The additional scan removed rotation-only stair/ladder work, unchanged-map
  prey-tracking dirties, and a duplicate jammer query during radio checks.
  Nearby atmosphere and random light update loops already had suitable cadence
  or unchanged-state guards and were left alone.

These changes affect content only. They do not change the engine or restore
build provenance. CPU and FPS gains require a representative live-round profile.

The follow-up client/server build passed. Two focused integration runs passed
20 and 17 cases respectively, with no failures or skips: 34 distinct cases and
three repeated after the final cache/measurement changes. The radio test measured
**1,201 bytes for full panel state and 113 bytes for telemetry**, approximately
91% smaller for that fixture. These are serialized payload sizes, not total
traffic or live-round CPU measurements.

| Requirement | Regression evidence |
| --- | --- |
| Telemetry preserves configuration, survives reopen and rejects stale readings | `TelemetryPreservesConfigurationAndReopeningResynchronizes` |
| Channel index follows retuning, movement and component removal | `RelayRetuningMovementAndRemovalImmediatelyChangeCoverage` |
| Lazy hazards remain scoped to the right map and expire | `RoutesSeeHazardsOnEachMapAndRefreshAfterRemoval` |
| Reconstruction preloads and camera permissions remain correct | `PersonalPreloadOpensWithoutRetransmittingOrReuploadingTerrain`; six `MarineIconOpensOverwatchCameraAndReleasesIt` cases |
| 3D subscriptions follow access and map changes | `PlayerCanToggleReduxViewAndLeavingMapRevokesSubscription` |
| AI scheduling, radio techniques and vertical traversal remain intact | `DeferredDecisionStillRunsWeaponExecution`, `OverdueDecisionsAreCappedAndEveryAgentGetsATurn`, `ANPRCExpertTest`, `CMUSwarmStairsTest`, `HunterShipLadderTest` |

Both test commands use the repository's isolated build/test wrapper:

```powershell
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Filter 'FullyQualifiedName~CMURadioEfficiencyTest|FullyQualifiedName~CMUAgentHazardSnapshotTest|FullyQualifiedName~CMUAgentPerformanceTest|FullyQualifiedName~PersonalPreloadOpensWithoutRetransmittingOrReuploadingTerrain|FullyQualifiedName~MarineIconOpensOverwatchCameraAndReleasesIt|FullyQualifiedName~PlayerCanToggleReduxViewAndLeavingMapRevokesSubscription|FullyQualifiedName~ANPRCQuickSetupTest|FullyQualifiedName~AU14FieldMastTest'
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Filter 'FullyQualifiedName~CMURadioEfficiencyTest|FullyQualifiedName~CMUAgentHazardSnapshotTest|FullyQualifiedName~RadioJammerToggleTest|FullyQualifiedName~ANPRCExpertTest|FullyQualifiedName~CMUSwarmStairsTest|FullyQualifiedName~HunterShipLadderTest'
```

Existing analyzer warnings remain. Content whitespace, UTF-8/LF checks and the
unmodified engine checkout were verified. No live gameplay session was launched.

### Release integration checks

- Shared, client, server and packaging projects compiled successfully.
- Before the stock-pin correction, 56 retained integration cases passed:
  five new cases covering medical snapshots, fighter state, hitscan geometry
  and docking; plus 51 existing anchoring, atmosphere, fighter ground/laser
  and docking cases. The withdrawn custom-API test is excluded.
- The fighter scenario serialized **168 bytes for full component state versus
  17 bytes for a flight delta** (about 90% smaller). It also verified idle
  aircraft, seats and weapons stayed clean and that applying a delta to an
  earlier full state restored motion while preserving stable fields.
- Seven focused integration cases passed on the unmodified v294.0.1 release:
  the five retained new cases and two existing public-API prototype-upload cases.
- Whitespace checks passed for content changes.

The stock-release integration command used the repository wrapper:

```powershell
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Filter 'FullyQualifiedName~CMUToolboxMedicalStateTest|FullyQualifiedName~CMUFighterNetworkStateTest|FullyQualifiedName~CMUFixtureHitscanTest|FullyQualifiedName~CMUDockTransferTest|FullyQualifiedName~PaperPrototypeUploadTest'
```

All seven cases passed with none failed or skipped.

These are serialized component measurements, not total network traffic, FPS,
or live-server CPU benchmarks. Visual gameplay inspection, a full distributable
package and deployment remain outside the checks
performed here. The medical regression exercises client snapshot correction,
not a complete multiplayer tourniquet consumption/refund interaction.
