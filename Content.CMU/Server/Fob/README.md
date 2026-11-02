# FOB reserve and physical equipment

The reserve system owns the bank and paired relay-job lifecycle. The interaction system connects
physical relay repairs and local controls to it. The defense system supports two player-placed gun platforms.
The map system selects the deployment area on the first friendly landing at any configured primary colony LZ
in Distress Signal and schedules relay pairs. See [supported maps and authoring](MAPS.md).
The same validated pilot enforces [contested evacuation](EVACUATION.md): ordinary physical boarding
and flight, with the protected withdrawal path excluded for the round.
See [player information and trial reporting](INFORMATION.md) for private relay markers, reserve
warnings, gun status, the equipment guide and structured trial logs.
See [the CMSS13 content inventory](CONTENT-PORT.md) for the original artwork, deployable utilities,
finite ammunition, powerloader module swaps and deliberate CMU balance adaptations.
See [physical supply and free deployment](LOGISTICS.md) for the three shipside crates, 40-tile
deployment radius, platform relocation and recoverable fence stacks.

`cmu.fob.reserve_enabled` defaults to `false` and is sampled on `RoundStartingEvent`.
Changing it during a round only affects a subsequent round. A completed round closes its networks;
round cleanup removes them and permits a fresh starting reserve.

## Integration contract

- The approved landing/site selector calls `TryCreateNetwork(site, faction, out network)` once.
  The bank is a separate entity on the site's map, with one network per faction for the round.
  Use `TryGetNetwork` to reconnect replacement controls. Deleting a console or even the network does
  not permit another starting grant before round cleanup.
- Authenticated local controls call `TryActivate` and `TryDeactivate`. A broken, deleted or unanchored control calls
  `DisableControl`, which pays the unspent minimum activation and preserves the restart cooldown.
  `CMUFobInteractionSystem` validates access both when opening the UI and for each incoming message.
- The route/anchor selector supplies two distinct, same-map entities carrying `CMUFobRelayComponent`
  to `TryOfferRelays`. It remains responsible for route fairness, tier progression and choosing another
  pair when one is unavailable. The backend enforces job and anchor cooldowns and exclusive assignment.
  Keep the relay component on the persistent anchor when repairing/replacing its visible hardware;
  replacing an anchor entity would discard its location-specific reuse history.
- The interaction system captures the current job token (network identity and generation) when a
  repair DoAfter starts. It revalidates the operator, faction, held tool, reach and token throughout
  the action and on completion. Only then does it call `TryCompleteRelay`; clients cannot request rewards.
- The first successful completion awards up to the remaining capacity and consumes both alternatives.
  A full bank leaves the job available. An expired, moved, paused, unassigned or stale endpoint cannot
  redeem. Cancelling a repair simply does not call completion; it does not consume or reroll the job.
- The defense system subscribes to the server-local `CMUFobReserveChangedEvent` on the network. Only `Active`
  allows support equipment to fire; `Starting` consumes reserve without authorizing output. Ordinary sentries,
  barricades and dropships must not subscribe as reserve-controlled equipment.
- `CloseNetwork` is for an authoritative final outcome, not a routine dropship trip or a small survivor
  count. It is irreversible until the next round. The backend already handles round end and cleanup.

The hidden `CMUFobNetwork` prototype supplies the tunable durations. Runtime fields are owned by
`CMUFobReserveSystem`. Defaults are 8 minutes stored, 12 minutes capacity and up to 6 minutes per repair,
with a 10-second startup, 60-second minimum activation and 30-second restart delay. A successful job
permits another offer after 5 minutes; an uncompleted job expires after 8 minutes, with a 30-second
replacement delay. Both offered anchors have a 10-minute reuse delay after resolution.

Deadlines use game time and generated pause offsets. Reading the reserve while its map is paused
uses the pause instant. Delayed updates resolve depletion/expiry at the original deadline rather than
granting more reserve or extending cooldowns according to when the next frame happens.

## Physical equipment

`CMUFobRelay` is a persistent, nonblocking fixture. A conscious Marine of the owning faction can
repair an assigned endpoint with a screwdriver or lit welder held in the active hand. Construction skill 3 or
higher takes 30 seconds; other Marines take 45. Movement, tool loss, incapacitation, faction changes,
operator damage or relay damage interrupt the action. One worker can repair each endpoint at once;
completing either alternative cancels work at the other and transfers just one capped reward.
Damage does not delete the anchor or erase its reuse history.

`CMUFobControl` binds to one existing faction network within 40 tiles of the selected LZ center,
outside its landing footprint. Moving the control does not move this area.
It never creates a bank, and duplicate live controls cannot take over. Local access requires the
owning faction and construction skill 3, engineering skill 2, or squad leadership. The panel shows
reserve, mode, shutdown/restart locks, both relay coordinates, expiry and the currently available reward.
All activation requests go through the backend's startup, minimum-use and restart rules.

At 100 accumulated damage the control breaks and immediately disables support. A 20-second
screwdriver or lit-welder repair by an authorized operator restores the control without activating or replenishing
the network. A replacement at the same site reconnects to the surviving bank only after the old
control is removed. Moving or unanchoring a bound control also shuts support down. The devices use
the original CMSS13 transformer and terminal artwork and provide no hard collision or projectile cover.

For a developer fixture, enable the CVar before round start, place a control on a grid, call
`TryCreateNetwork(control, faction, out network)`, then call `TryOfferRelays` with two physical relays.
Spawning equipment alone intentionally creates no network or reward. Each colony's inert relay markers opt
into automatic objective deployment only when the round starts with the flag enabled in Distress Signal.

## Support guns

The normal delivery supplies two folded `CMUFobPlatformModule` entities. Operators wrench them down
on valid ground within the selected LZ's deployment area. `TryDeployPlatform` registers the physical
platform against the round's two-slot budget. Removing its gun and folding the platform permits
relocation within the area while support is off. Deleting a gun, its platform or the terminal does
not authorize another allocation. Directly moving deployed hardware invalidates it until properly
redeployed. No fixed equipment coordinates are authored. `TryRegisterMounts` remains a developer
fixture helper for the existing gun tests; the map selection path does not call it.

Delivered heavy modules contain one finite 100-round UH-46 drum and require powerloader installation. The guns fire
30mm projectiles at 2.5 rounds/second, out to six tiles, with full 360-degree tracking.
They have no homing, gauss ammunition, ammo regeneration, upgrade menu, or remote control ports.
Target selection uses the existing sentry faction/IFF and alliance checks and requires a live mob,
the same map, and a clear bullet path. Walls and closed doors block acquisition; range, faction,
burrowing, venting, container entry and map changes invalidate the current target.

The reserve's ten-second startup produces an audible local warning and amber gun indication. The
control panel displays the startup countdown. A newly acquired target gets another audible/visible
1.5-second warning before firing; losing the target resets it. Every actual shot revalidates the
deadline, operational local control, mount, target, IFF and geometry. Standard sentry power toggles
and direct/manual gun requests cannot authorize a shot. Reserve continues draining if a gun breaks
or runs out of ammunition.

An owning-faction engineer or squad leader can rotate a healthy gun with a screwdriver while the
network is off, or reload an empty gun with its dedicated drum through a seven-second interruptible
interaction. Reloading uses the supplied magazine and restarts target acquisition. Ammo and reserve
are never replenished by toggling, repairing or replacing hardware.

At the standard 200-damage threshold, a gun breaks using CMU's existing sentry break/repair sequence.
Its wreck loses movement and projectile collision. Restoration requires the existing cutters,
welding fuel, cable, screwdriver and tuner steps. A repaired gun remains off until a new reserve
activation and startup warning. If corrosion or another deletion removes the weapon, its persistent
mount installs an empty broken replacement that requires that same material repair sequence. The
initial magazine grant is not repeated. Only that slot's weapon is restored; no third gun is minted.

Original CMSS13 platforms, heavy-gun and rocket-module art replace the standard sentry placeholders.
The UH-99 module uses the same two slots, a finite rocket cassette, a three-second acquisition warning
and live UE-04 sensor coverage. See [equipment and logistics](CONTENT-PORT.md) for physical swapping,
powered utilities, backup use and the delivered supply allocation.

The small `CMUSentryStateUpdatedEvent` hook preserves the FOB wreck's collision contract after a
common sentry mode refresh. Repair authorization and broken/repaired events are additive hooks;
ordinary sentries retain their existing behavior.

## Regression coverage

`UtilitiesFollowReserveAndBackupCannotBeReissued` covers adjacent deployment, reserve/terminal power
loss, breaching a powered fence and duplicate backup prevention. `ModuleSwapPreservesAmmoAndSensorLossCancelsRocketWarning`
covers actual powerloader installation, preserved ammunition, forbidden live swaps and loss of sensor
coverage during launch acquisition. The authored-map landing cases also protect the single reserve
allocation across repeated or alternate LZ landings. Logistics cases exercise actual requisitions
delivery, physical assembly, relocation, blocked placement, the two-platform budget and fence recovery.

The focused `CMUFobReserveTest` integration fixture exercises the actual server system with short,
nonzero durations. It covers:

| Requirement | Test |
| --- | --- |
| Round-latched feature flag | `DisabledRoundRejectsNetworksAndFlagChangesWaitForNextRound` |
| Startup drain, standby, depletion and restart | `ReserveDrainsThroughStartupButFreezesInStandbyAndDepletes` |
| One reward per pair, capacity and stale completions | `AlternativesAwardOnceAndFullBankDoesNotConsumeNextJob` |
| Expiry and relay reuse | `ExpiryRejectsLateRepairAndPreservesJobAndAnchorCooldowns` |
| Paused reserve and jobs | `MapPauseFreezesReserveAndOutstandingRelayDeadline` |
| Destruction/replacement cannot mint reserve | `ControlLossAndReplacementCannotRefundOrRecreateReserve` |
| Cross-map rejection and round teardown | `WrongMapRelayAndRoundEndCannotRedeemOrRestartSupport` |
| Held tool and capped physical completion | `PhysicalRelayRequiresHeldToolAndTransfersOneCappedReward` |
| Damage, movement, tool loss, faction and incapacitation interrupts | `InterruptedRepairsNeverAwardAndReleaseTheOperatorLock` |
| Worker lock and competing alternatives | `FasterAlternativeCancelsOtherRepairAndOperatorsCannotStack` |
| Actual client UI, training, faction and reach checks | `ControlUiEnforcesTrainingTimersAndRechecksFactionAndReach` |
| Broken control, repair and replacement preserve the bank | `BrokenControlStopsSupportAndRepairOrReplacementNeverRefillsIt` |
| Default-off equipment cannot open or repair | `DisabledEquipmentCannotCreateBanksOpenControlsOrStartRepairs` |
| Disabled/unregistered weapons cannot fire through forced modes | `DisabledAndUnregisteredWeaponsRejectForcedFiring` |
| Two-slot budget, startup/acquisition warning, real shots and ammo exhaustion | `TwoMountsWarnBeforeFiringAndExhaustFiniteAmmoWithoutRefills` |
| Range, wall/door LOS, burrow/vent, map and IFF loss restart acquisition | `GeometryConcealmentAndIffLossRequireFreshAcquisition` |
| Powered gun damage and nonblocking wreck geometry | `PoweredGunTakesProjectileDamageAndItsWreckAllowsShotsAndMovementThrough` |
| Material repair, physical magazine reload, replacement and restart requirement | `MaterialRepairReloadAndReplacementCannotMintAmmoOrBypassRestart` |
| Depletion and broken controls cancel pending fire | `DepletionOrBrokenControlCancelsAcquisition` |
| Existing dropship sentry remains shootable and inherits alliances | `DropshipSentryIsShootableAndInheritsStoredAllianceState` |
| Disabled/other-preset LV624 retains legacy defenses and no new equipment | `DisabledOrOtherPresetPreservesLegacyAndNeverDeploys` |
| Either authored LZ, early/late map loading, valid arrival, one bank/pair, selective legacy replacement | `FirstFriendlyLandingDeploysOneFobAndReplacesOnlyLegacy` |
| Invalid layout retains legacy protection without a partial grant | `InvalidAuthoredLayoutRetainsLegacyWithoutPartialDeployment` |
| Time tiers, live-job stability, reuse, unavailable routes and retry without free charge | `RelayProgressionKeepsLiveJobsAndUnavailableRoutesNeverAwardReserve` |
| Stale withdrawal states/messages cannot lock hijacking, unlock protected return or end the pilot | `ContestedEvacuationRejectsStaleWithdrawalLocksReturnAndStalemate` |
| Disabled and other-preset extraction behavior is preserved | `ContestedEvacuationLeavesLegacyReturnOutsidePilot` |
| Full/empty reserve, navigation access, ordinary startup, damage, hostile boarding and left-behind Marines | `ContestedEvacuationUsesNormalBoardingFlightAndReserve` |

Run through the repository wrapper:

```powershell
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Filter 'FullyQualifiedName~CMUFob|FullyQualifiedName~DropshipSentryRegressionTest'
```

Pre-submission verification on 2026-11-02, after rebasing onto current master: Shared, Client, Server
and integration projects built; the focused command above passed 72 tests, with zero failures or skips,
including every primary LZ and the homing, purchase-limit and rocket critical-damage regressions.
Map entity IDs/references, sprite assets, UTF-8/LF and final diff checks passed. Repository warnings remain.
The test harness also logged a caught antag-pity lookup error for its missing `cmu_antag_pity` table;
all cases passed. This feature does not change antag-pity or database code.

Visual and multi-client combat playtesting remains necessary
before enabling a playable pilot; automated geometry tests do not establish fair map coverage or balance.
