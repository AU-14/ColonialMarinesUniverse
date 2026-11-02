# Contested evacuation

The default-off `cmu.fob.reserve_enabled` flag enables this policy only when the round selects
Distress Signal and the selected colony's authored FOB configuration validates. Changing the CVar during a round
does not switch the policy. An invalid/missing layout or another preset retains existing behavior.

## Player rules

- Hold a boarding route, move casualties aboard, and use the normal dropship navigation controls.
  Piloting access, startup, flight, boarding and applicable hijack rules continue to apply.
- The two FOB guns may cover withdrawal using their remaining reserve and ammunition. Power does
  not clear the ramp, eject boarded enemies, protect passengers from damage, close doors for them,
  teleport Marines or award an evacuation outcome.
- Empty reserve does not prevent normal takeoff. Repairing a relay does not accelerate departure.
- Routine departures leave the bank and relays at their selected site. Active support continues
  spending reserve and ammunition while vulnerable; there is no survivor-count cutoff. Marines
  who remain on the surface have not evacuated merely because a dropship left.
- Returning, using another LZ, using a second dropship or tactical landing does not grant another
  bank or gun pair. This remains the map selector's one-network-per-faction contract.

The local FOB panel explains physical boarding and departure with empty reserve. There is no new
evacuation button, protection window, reserve-dependent launch permission or automatic retreat timer.

## Server boundary

`CMUFobMapSystem` owns the validated round policy. Its evacuation partial vetoes the server-local
`CMUProtectedWithdrawalAttemptEvent`; validation can happen on the first request, before the first
selector update or friendly landing. The decision persists through bank/map deletion and reserve
depletion until round cleanup. It applies to the pilot round's protected withdrawal mechanism,
including round-wide stalemate finalization, rather than depending on a disposable console.

`WithdrawConsoleSystem` consults that veto for capability queries, withdrawal activation, active
countdowns, stalemate and finalization. The existing dropship return handler consumes the guarded
capability query; its special indefinite-flight path therefore remains unavailable, including for
stale UI messages. The dropship hijack-lock event also checks the veto before changing navigation
consoles. Seeded lock bits or an expired countdown cannot create protection or end the pilot.

This closes gaps around the existing Distress Signal UI restriction: preventing normal UI activation
alone did not protect capability queries, pre-existing countdown state or stalemate messages.
Flag-off behavior is deliberately preserved, including inherited behavior for seeded console state.
Ordinary `FlyTo`/FTL code, boarding, damage and hijack eligibility are not replaced.

## Outcome ownership

No FOB system declares evacuation success or ends the round on takeoff, depletion, a low Marine
count or a failed relay. `CMUFobReserveSystem` already closes networks on authoritative PostRound
and cleanup. Protected withdrawal cannot become an alternative outcome owner in the pilot.

The active CMU Distress Signal preset does not depend on the classic distress-rule entity.
The existing queen-decline request first checks queen identity and normal hijack eligibility.
With an active classic distress rule, `CMDistressSignalRuleSystem` owns the outcome. Otherwise
`CMUHijackExtrasSystem` handles the request while InRound and ends with its existing minor-xeno-victory
message. The FOB feature adds no second listener that can declare victory or count survivors as safe.
This owner selection was traced in source; the regression test injects the authoritative PostRound
notification to verify FOB cleanup without starting a full live round.

## Requirement evidence

| Requirement | Regression evidence |
| --- | --- |
| No protected return, hijack/dropdown lock, countdown result or forged stalemate in the enabled pilot | `ContestedEvacuationRejectsStaleWithdrawalLocksReturnAndStalemate` |
| Decision survives flag changes, bank/map destruction; cleanup releases it | Same test, across those state transitions |
| Default-off and other presets retain their inherited special-return path | `ContestedEvacuationLeavesLegacyReturnOutsidePilot` |
| Invalid configuration cannot activate the policy | `InvalidAuthoredLayoutRetainsLegacyWithoutPartialDeployment` |
| Active and exhausted banks permit ordinary authorized launch with the same startup | `ContestedEvacuationUsesNormalBoardingFlightAndReserve` |
| Navigation access, queen hijack UI, passenger damage and hostile boarding remain effective | Same test through the real flight/access/UI/damage systems |
| Boarded passengers travel; groundside Marines and the draining bank stay behind; authoritative round end closes support | Same test through actual FTL travel and the PostRound event |
| Alternate/repeat landing does not grant equipment or reserve | `FirstFriendlyLandingDeploysOneFobAndReplacesOnlyLegacy` |

## Verification

Verified on 2026-10-27: Shared, Client, Server and integration test projects built successfully.
The combined FOB, dropship sentry, inherited startup and multideck queen-hijack regressions passed:
**37 passed, 0 failed, 0 skipped**, with:

```powershell
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Filter 'FullyQualifiedName~CMUFob|FullyQualifiedName~DropshipSentryRegressionTest|FullyQualifiedName~ForceOnForceLaunchTest.ReturnsHijacksAndOtherModesKeepTheirNormalStartup|FullyQualifiedName~MohawkFlightFeaturesTest.QueenCanUseTheHijackPointFromTheCabinAboveAColony'
```

`git diff --check`, UTF-8/LF validation and FOB control localization references also passed.
The build reports repository analyzer and dependency advisory warnings.

Live multi-client combat, occupied Alamo/Mohawk arrivals and an entire queen-decline round outcome
still require manual verification before enabling the pilot. Automated extraction checks are not
evidence that either LZ is balanced against a coordinated assault.
