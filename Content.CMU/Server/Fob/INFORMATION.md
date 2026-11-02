# Player information and trial reporting

All runtime behavior follows the existing round-latched, default-off `cmu.fob.reserve_enabled` flag.
The validated Distress Signal pilot enables one shipside supply delivery. First friendly landing selects the 40-tile deployment area and spawns remote relay objectives; players transport and assemble the support equipment.

## Player information

The owning faction's tactical map shows both offered relay alternatives with the maximum repair
reward. Completion, expiry and network closure remove both alternatives, including stale published
views. The local panel provides relay names, coordinates, expiry and the actual capped reward.
An offered relay stays assigned when the bank is full; completion waits for available capacity.
When no legal pair exists, the panel explicitly reports unavailable relay routes and a retry countdown.
Server diagnostics include eligible endpoints, candidate pairs and per-endpoint rejection reasons.
The scheduler retries after 30 seconds without granting reserve and clears the warning on recovery.

Relay contacts never enter the map's shared live or published sensor buckets. Personal map data is
owner-only and checks the Marine's server-side faction. Computer markers use private messages to
authorized faction viewers, not the console's PVS-replicated component. Reconstruction contacts use
the same faction check and the relay's actual ground map. Xenos and other factions receive no mission
overlay, even if they have sensors or a foreign map-view flag.

Reserve warnings occur at or below 120 seconds, 60 seconds and zero. Each threshold is consumed once;
only replenishment to at least 135/75 seconds rearms the corresponding warning. Any positive repair
rearms the next depletion warning. A delayed update crossing several thresholds sends only the most
urgent warning. Pauses freeze warning timing. Startup and standby toggles do not reset the latches.
Nearby owning-faction Marines receive a popup, equipment emits a local beep, and the normal faction
command radio attempts delivery. Tower failures, range limits, jamming and normal radio reception
still apply. There is no clear-transmission exemption or new global announcement channel.

The local control displays each registered gun slot's condition, ammunition and accumulated damage.
Missing/displaced weapons, broken guns, reloads, restart requirements, empty drums, startup and active
support have distinct states. These are read from the actual server equipment; the display grants no
remote control or repair permission.
Nearby examination exposes standby, startup, energized, empty or offline support power to both sides,
without exposing the reserve timer.

Enabled equipment exposes a public help verb for both sides, and the local control has an instructions
button. The standalone `CMUFobGuide` entry contains localized Marine and xeno rules, repair/reload
instructions, the warning policy and contested evacuation. It also explains the three shipside crates,
free placement within 40 tiles, fence stacks and recovery, and screwdriver or lit/fueled welder repairs. It is not added to the ordinary guide
tree. An initial trial notice uses normal command radio when the bank is created.

## Trial logs

Search normal server logs for `CMU_FOB `. Each line contains a JSON object. No separate file writer,
external analytics service, player identifier or new round-outcome policy is introduced.

Records cover creation, activation, shutdown, relay offers/completions/expiry, warnings, actual FTL
departures, groundside landings, one-minute samples and a single final closure. The identity fields include round,
network, faction, map and site. Counters include:

- Offered, completed and expired jobs; granted reserve and potential reward lost to the capacity cap.
- Startup and active seconds, excluding pauses and standby; minimum-use charge after control loss.
- Actual shots, incoming positive gun damage, genuine gun breaks and current slot condition/ammo/damage.
- Reserve remaining at observation/closure, landing and departure counts.

Departure records count living/critical faction Marines aboard, living/critical xenos aboard, and
living/critical faction Marines still on the network's ground map, with the existing hijack flag.
They describe the instant of entry into FTL, not an evacuation success or final survivor classification.
Landing records include the destination site where configured, separately from the initially selected FOB.
Closure records the current round run level; correlate the record with the mode's existing outcome
logs. An administrator deleting a bank also finalizes its report, without ending the round.

The logs support observer review, not an automatic balance verdict. For each trial, also record
population, loadouts, equipped walking times, whether attackers could contest a powered position,
casualties during boarding, and defender/patrol/xeno feedback. Compare 6–10 observed ordinary and
low-pop rounds before deciding whether to enable the pilot or tune routes, coverage or reserve values.

## Observer report for each trial

Record the round ID, selected LZ, approximate population at deployment/retreat, loadouts, round outcome
and the matching final `CMU_FOB` record. Add these observations alongside the machine counters:

- Relay patrol: chosen route, equipped walking time, patrol size/time away, interruptions and whether
  the earned support justified leaving the FOB.
- Holdout: whether attackers could flank, damage or pass the guns while powered; whether defenders
  could still hold, treat casualties and counterattack after depletion.
- Evacuation: contested approach/ramp, boarding casualties, people left behind, enemies aboard and
  the actual flight/hijack outcome. A departure record alone does not establish successful evacuation.
- Feedback: a short note from defenders, the patrol and attackers, with any repeatable failure.

Revise the pilot if powered positions force passive waiting, relay denial routinely ends ordinary
holdouts, or attackers cannot meaningfully contest an opposed evacuation. Review those observations
before tuning reserve values or enabling the feature for ordinary rounds.

## Verification

The unavailable-relay state, retry recovery, corrected repair/tool guidance and new supply/deployment
instructions are included in the [2026-11-01 completion verification](LOGISTICS.md#verification).

| Requirement | Evidence |
| --- | --- |
| Warning thresholds, pause, hysteresis, no repeats and normal comms rejection | `InformationWarningsRespectPauseRearmAndCommsWithoutRepeating` |
| Faction privacy, clean sensor sources, no PVS console leak and immediate removal | `InformationRelayMarkersStayPrivateAndBothDisappearOnResolution` |
| Real gunfire/damage, breakage and empty replacement reflected in status/report | `InformationGunStatusAndTrialReportFollowShotsBreakageAndReplacement` |
| Actual flight generates an observation without closing the bank | `ContestedEvacuationUsesNormalBoardingFlightAndReserve` |
| Standalone localized guide parses with the real client document parser | Real `DocumentParsingManager` and Fluent loader in a temporary client harness (removed after validation) |

Live readability, radio audibility and crowded multi-client map views still need playtesting before
the default-off flag is enabled for ordinary rounds.

Validation on 2026-10-27: the affected Shared, Server and Client projects built, and the focused
FOB/tactical-map regression run passed **41 tests, zero failed or skipped**:

```powershell
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Filter 'FullyQualifiedName~CMUFob|FullyQualifiedName~DropshipSentryRegressionTest|FullyQualifiedName~CMUCommsTowerVisibilityTest|FullyQualifiedName~CMUSensorTowerVisionTest|FullyQualifiedName~CMUReconstructionZLevelContactsTest'
```

Log: `.codex/logs/test-20261027-143322-025.log`. Its TRX contains no runtime error lines; 168 structured
trial records parsed successfully, including both selected LZs and two real departures. The preceding
focused run passed the three new behavior cases and the temporary guide-parser check (four passed,
zero skipped): `.codex/logs/test-20261027-142756-497.log`. The temporary harness was removed from the
compiled test tree. Existing repository build warnings remain.

After giving the evacuation fixture's humans their real faction, both powered/empty-bank flight
cases passed again with the filter `FullyQualifiedName~ContestedEvacuationUsesNormalBoardingFlightAndReserve`.
Log: `.codex/logs/test-20261027-143919-273.log`. A temporary TRX check verified each departure reported
one faction Marine aboard, one xeno aboard and one faction Marine left groundside, with no runtime errors.
