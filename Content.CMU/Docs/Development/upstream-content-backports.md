# RMC and SS14 content backports, 30 October 2026

This batch adapts selected upstream fixes to CMU rather than merging either upstream wholesale. The audit screened 80 recent RMC pull requests and 100 recent SS14 pull requests, then inspected older and related candidates. It is a prioritized source audit, not an exhaustive compatibility or performance certification.

Baseline: CMU `3d6f029ccae5d4b6bb7777ad6e0d04013c629604`; inspected RMC master `30df7cf0ec5fe9c24bac7b8d12768c72139fc04f` and SS14 master `b2834c5546f6269dd49bcfcc0d3bc5fe2f251972`. RobustToolbox, RSI.NET and dependency pins are unchanged.

## Applied changes

| Area | Result and CMU adaptation | Upstream source |
| --- | --- | --- |
| Blocked movement | Keep blocked movers active for relative/camera rotation updates while retaining CMU slide cancellation and blocked-input checks. | [SS14 45953](https://github.com/space-wizards/space-station-14/pull/45953) |
| Ballistic prediction | Predict-spawn rounds taken from unspawned magazines; retain CMU's one-item stack adjustment. | [SS14 46379](https://github.com/space-wizards/space-station-14/pull/46379) |
| Bullet boxes | Consume both unspawned and physical magazine rounds, credit only the actual transfer, and preserve CMU ammunition identity handling. | [RMC 10740](https://github.com/RMC-14/RMC-14/pull/10740) |
| Action state loading | Avoid granting actions while network state is only partially applied. | [SS14 46195](https://github.com/space-wizards/space-station-14/pull/46195) |
| Attachment actions | Clear stale action references on the server when the action belongs to a different container; clients await authoritative repair. Use the public container API supported by our engine. | [RMC 10467](https://github.com/RMC-14/RMC-14/pull/10467) |
| Sentry and death-audio ownership | Release the server watcher on laptop UI close, reject stale close messages from another laptop, skip terminating targets, and clear death-audio references only for the sound that owns them. | [RMC 10858](https://github.com/RMC-14/RMC-14/pull/10858) |
| Recycling | Reject items already queued for deletion before starting or completing another reclamation; retain CMU material conversion. | [SS14 46280](https://github.com/space-wizards/space-station-14/pull/46280) |
| FTL destinations | Reject invalid coordinates, nonfinite angles and destinations without a real map before any FTL setup. Preserve CMU startup timing and safety rules. The nullspace guard extends the upstream fix. | [SS14 46577](https://github.com/space-wizards/space-station-14/pull/46577) |
| Gun origins | Stop projectile creation when its origin map entity is invalid. | [SS14 45811](https://github.com/space-wizards/space-station-14/pull/45811) |
| Hijack pipe bursts | Process one queued pipe per map per tick, preserve the rest of the wave, and retain each warning until its pipe bursts or is cancelled/deleted. Preserve CMU's 30-second cadence and hull/deck behavior. | [RMC 10334](https://github.com/RMC-14/RMC-14/pull/10334) |
| Rockets | Replace the launcher timer workaround with fixed-distance-stop triggering on both CMU 70 mm and RMC 84 mm projectile families, including inherited variants. | [RMC 10420](https://github.com/RMC-14/RMC-14/pull/10420) |
| Job admission | Read preferences once per check and fail closed while the character profile is unavailable, including when role timers are disabled. Preserve loaded-profile military-height and excluded-job behavior. | [RMC 10428](https://github.com/RMC-14/RMC-14/pull/10428) |
| Atmos volumes | Displace gas when a tile closes and transfer, rather than copy, gas when it reopens. Mix temperature by heat capacity. CMU clears adjacency before displacement, so find recipients through neighboring tile coordinates and blocking faces. | [SS14 46482](https://github.com/space-wizards/space-station-14/pull/46482) |
| Guidebook | Install Home once, add Back/Forward, retain page search/scroll state, restore layout before scroll position, and preserve CRT styling and window cleanup. | [SS14 46442](https://github.com/space-wizards/space-station-14/pull/46442), [46443](https://github.com/space-wizards/space-station-14/pull/46443), [46515](https://github.com/space-wizards/space-station-14/pull/46515), [46512](https://github.com/space-wizards/space-station-14/pull/46512) |
| Lathe queue and animation | Preserve a paid, interrupted item when a new batch of the same recipe was queued while that item was in flight. Existing CMU refund and empty-queue fixes remain in place. Also import the exact upstream biofabricator insertion sprite correcting frame 3's one-pixel offset; retain the existing RSI metadata and license. | [SS14 46267](https://github.com/space-wizards/space-station-14/pull/46267) |
| Health scanner | Add localized critical-overdose marking and separate ordinary/critical colors. Keep CMU's existing reagent colors and bounded, skill-gated server projection. | [RMC 10821](https://github.com/RMC-14/RMC-14/pull/10821) |
| Ordinary handheld radios | Cycle transmit/receive, receive-only, and off through existing shared setters. Stop listening immediately even before deferred listener removal. Apply only to the ordinary handheld lineage; AN/PRC retains its independent EMCON implementation. | [RMC 10659](https://github.com/RMC-14/RMC-14/pull/10659) |
| Options | Refresh CVar-backed controls after external changes. Subscribe only while initialized controls are attached to the UI tree, and unsubscribe on removal. | [SS14 45827](https://github.com/space-wizards/space-station-14/pull/45827) |

## Further candidates investigated

- **Vendor restocking, [RMC 10351](https://github.com/RMC-14/RMC-14/pull/10351):** the patch relies on restock-entry, stack-entry and bulk-vending structures absent from CMU. Its independently useful refiller handled-event guard is already present. No compatible missing fix was established beyond a separate vending-feature migration.
- **Device networking, [SS14 44346](https://github.com/space-wizards/space-station-14/pull/44346) and [44990](https://github.com/space-wizards/space-station-14/pull/44990):** these touch 149 and 304 files respectively, with overlap. They replace payload structures and queued delivery semantics. CMU still uses the older `NetworkPayload`/`QueuePacket` contract. Adopting them requires a separate migration of producers and consumers plus ordering/reentrancy tests; they are not compatible isolated backports. No measured CMU performance benefit was established.
- **Chat attachment refresh, [SS14 46050](https://github.com/space-wizards/space-station-14/pull/46050), and action-button parent checks, [45872](https://github.com/space-wizards/space-station-14/pull/45872):** the relevant corrections are already present.
- **Xeno lag compensation, [RMC 9963](https://github.com/RMC-14/RMC-14/pull/9963):** CMU history includes the port and later prediction-related reversion. Reapplying an old upstream patch would revive superseded behavior.
- **Larva queue shutdown, [RMC 10509](https://github.com/RMC-14/RMC-14/pull/10509):** CMU has a different queue design and checks terminating entities when claiming them; the upstream patch does not map directly.
- **Fire-alert optimization:** CMU already filters entities without alert components. Further work needs profiling of CMU's acid/fire path, not upstream performance percentages.

## Verification and limits

All tests use the repository's isolated `run.ps1 test` runner, which also builds Content.Shared, Content.Server and Content.Client through the integration-test project. No permanent UI-only or prototype-default tests are added.

| Behavior | Evidence |
| --- | --- |
| Ammo conservation | `UnloadingMixedMagazineConservesRounds`: one unspawned plus two physical rounds become exactly three box rounds, with no magazine leftovers. |
| Single recycling yield | `ReclaimingPendingDeletionYieldsMaterialOnce`: two calls in one tick produce one CMU-converted material yield. |
| Watcher ownership | `LaptopCloseClearsOnlyItsOwnWatcher`, plus existing `SentryLaptopStateTest`: stale closes cannot release another laptop's watcher; deletion still clears links. |
| Hijack scheduling | `HijackQueueDrainsWithoutDroppingPendingWarnings` and existing `PipeBarrageRateTest`: remaining entries/warnings survive each update and CMU's next-wave cadence remains intact. |
| Radio transitions | `HandheldListenOnlyStopsTransmissionImmediatelyAndCyclesOff`: actual activation events cycle all three modes and cancel a same-tick speech-listening attempt. |
| Lathe payment and queue conservation | `PowerLossPreservesItemAheadOfNewSameRecipeBatch`, plus existing `LatheQueueMergeRegressionTest`: three paid items survive power loss and cancellation restores exactly their original material cost. |
| Atmos conservation | `BlockingAndReopeningTileConservesGasAndHeat`: three real wall placement/removal cycles conserve 120 moles and thermal energy with donors at different temperatures. |
| FTL validation | `InvalidDestinationCannotStartFtlButValidDestinationCan`: malformed, nullspace and nonfinite-angle destinations do not start flight; a valid destination does. |
| Movement compatibility | Existing relay tests retain input-forwarding, blocked-input and stale-link checks. The blocked-mover active-component expectation is updated to the intended upstream camera behavior. |
| Other CMU compatibility | Existing job-admission, scanner-projection and HE/AP jeep-rocket tests cover their existing gameplay paths. Rocket impact coverage does not reproduce every aimed-stop/proximity scenario. |
| UI lifecycle | Temporary headless checks exercised guidebook back/forward/search/branching and options detach/change/reattach. The temporary test file was removed after verification. |

The initial focused run passed 13 cases. An expanded run passed 23 cases and reported one relay test as skipped; inspection showed its dirty-dispose cleanup concealed the stale blocked-mover assertion. That assertion was corrected against the intended behavior, without weakening input rejection. The final FTL/relay run passed both cases with zero failures or skips. After removing the UI probes, the revised queue-lifetime check and existing `CyclingUnspawnedNonStackAmmoDoesNotLogMissingStackError` also passed (2/2, no skips). Together these runs cover 26 distinct passing cases, including the two temporary UI checks. The queue check verifies warning counts without prescribing queue order.

The imported sprite matches upstream Git blob `643a0fdea639266e3b87a6fed17a9646f97b9096`. Pixel comparison verified that only visible frame 3 moves one pixel right, with unchanged 96×96 sheet dimensions and matching insertion timings, frame size and license metadata. Differences in RGB values of fully transparent pixels are immaterial. CMU's unrelated `unlit` animation metadata is preserved.

Commands:

```powershell
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Filter 'FullyQualifiedName~CMUUpstream|FullyQualifiedName~CMUTileVolumeConservationTest|FullyQualifiedName~PipeBarrageRateTest|FullyQualifiedName~SentryLaptopStateTest|FullyQualifiedName~LatheQueueMergeRegressionTest|FullyQualifiedName~MovementMergeRegressionTest.RelayForwards|FullyQualifiedName~MovementMergeRegressionTest.AnchoredRelay|FullyQualifiedName~ServerJobExclusionDoesNotAffectAntagRequirementsAndFiltersDisallowedJobs|FullyQualifiedName~CMUJeepRocketTest.OneRocketWrecksAJeep|FullyQualifiedName~CMUHealthScannerProjectionTest'
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Filter 'FullyQualifiedName~CMUUpstreamLifecycleTest.InvalidDestinationCannotStartFtlButValidDestinationCan|FullyQualifiedName~MovementMergeRegressionTest.RelayForwardsOnceAndCleansBlockedMissingAndStaleMovers'
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Filter 'FullyQualifiedName~CMUUpstreamLifecycleTest.HijackQueueDrainsWithoutDroppingPendingWarnings|FullyQualifiedName~BallisticCycleStackRegressionTest'
git diff --check
```

No live high-latency multiplayer reproduction, graphical scroll-layout inspection, or before/after server-tick/allocation benchmark was performed. The changes address identified source defects; they do not establish that all earlier rubberbanding or freezes are resolved. Gas displacement into open space, fully sealed volumes with no recipients, and CMU's multi-deck atmos behavior are outside the conservation test's closed-grid scenario. Full-state action reconciliation and stale death-audio ownership were reviewed in source and built, without a dedicated live reproduction.

## CI integration fixes

Verification against merged head `3dbf17744a6334be76d7e07c325902117d8c96df` exposed two content defects and stale expectations from the merged gameplay changes:

- Round-start job requirements now receive the same character profile snapshot used to build the assignment candidates. This preserves Force-on-Force role mapping when a profile is supplied directly, while still rejecting admission when neither a supplied nor cached profile exists.
- Planned NPC movement now uses the action's existing 0.4-metre arrival tolerance. Previously movement required 0.25 metres even though the action accepted 0.4, allowing a guard already at its flank stance to time out. Cover safety and support checks remain in place.
- The lung conservation test measures its baseline after atmosphere initialization and gas displacement. It no longer assumes initialization deletes gas down to 100 moles.
- Existing Yautja tests now follow the merged rules: critical hunters cannot arm self-destruct, recovered hunters can, and military bracers grant their menu, translator and manual self-destruct actions alongside wrist blades.
- The mapping/station/shuttle/round CI shard runs two concurrent workers to reduce memory pressure. Its test selection and inactivity timeout are unchanged. The CI stall did not reproduce locally; the lower concurrency is a mitigation, not proof of its cause.

Local verification covered 33 distinct passing cases across focused runs: 19 station/lung/Yautja cases, 13 combat-continuity/squad/round-start/job-admission cases, and the new `FlankCompletesAtItsAcceptedStanceWithoutAnotherMovementStep` regression. The new regression failed on the original movement code and passed after the fix and fixture isolation from idle home-return orders. It checks ongoing travel, completion at the accepted stance, and steering cleanup through the real system update. Both six-guard coordination variants, `PrimaryDepartmentsTest`, and the `CMDistressSignal` round-start case passed.

The final isolated arrival check used:

```powershell
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Filter 'FullyQualifiedName~FlankCompletesAtItsAcceptedStanceWithoutAnotherMovementStep'
```

It passed 1/1 with no skips. `actionlint` 1.7.7 passed for the changed CI workflow; the complete workflow set also passed with its existing Blacksmith runner labels registered in a temporary local configuration. `git diff --check` passed. RobustToolbox, RSI.NET and dependency pins remain unchanged.
