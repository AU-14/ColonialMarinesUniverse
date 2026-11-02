# Physical supply and free deployment

The validated, default-off Distress Signal pilot grants one set of three crates through the friendly carrier's normal requisitions elevator. Raise the lift to receive them. The allocation waits for space if the lift already has too many orders. Existing requisitions access applies. Replacing an elevator, cycling it, making another landing or selecting the alternate LZ cannot reissue the free allocation.

The FOB catalog additionally sells **two weaponry crates at 4,000 points each** and **three support crates at 1,500 each** per friendly faction per round. These purchases contain the same hardware and ammunition as the starter crates. The monitoring crate remains a one-time grant. Allowances are shared across carrier terminals, survive console replacement/catalog rebuilding, never replenish, and reset at round cleanup. Failed checkouts do not spend an allowance. Round-limited kits stay whole crates rather than entering the itemized catalog. The two deployed-platform limit and single backup charge remain unchanged.

| Crate | Contents |
| --- | --- |
| Weaponry | Two folded UH-02 platforms, two loaded UH-46 heavy modules, one loaded UH-99 rocket module, two spare heavy-gun drums and one spare rocket cassette |
| Support | Two UE-92 illuminators, three stacks of ten UE-02 fence sections, wrench, screwdriver, cutters, welder, tuner and cable |
| Monitoring | Folded UE-09 terminal, one UE-11 generator and six UE-04 sensors |

Transport the real crates and equipment aboard the dropship. The first friendly landing chooses the LZ and creates the round-owned reserve, but creates no functioning defenses or groundside supplies. All deployable FOB hardware may be placed on valid ground **within 40 tiles of the selected LZ center**, outside the reserved landing footprint. There are no fixed equipment positions. The source of the radius is `CMUFobNetworkComponent.DeploymentRange`; moving the terminal does not move the area.

Unpack and wrench down the terminal and platforms. Use a powerloader to install a heavy gun or rocket module on either empty platform. The two-platform budget belongs to the network, and folding or moving a registered platform preserves its slot. A third platform cannot register. Move an existing platform while support is off and its weapon is removed; redeployment within the area updates its registered position without issuing ammunition. Machinery moved outside the area cannot operate. Changing modules still requires a fresh warned activation.

Combat technicians already have powerloader training. Loading does not require an occupied Foxtrot leadership slot. Terminal, assembly and utility operation use the existing owning-faction engineer/squad-leader checks. Cargo operators can transport the equipment for the engineering team. No general job skill or access grants are changed.

Put a fence bundle down and use a wrench to deploy one section. Normal stack splitting/merging applies to unused sections. Placement spends a section only after the work succeeds; blocked ground, the landing footprint and out-of-range positions reject deployment. Recovered sections remain individual objects and retain their damage. Support must be off to recover them with cutters. Powered fences always remain destructible, and destruction opens a breach.

Remote transformer relays remain authored objectives outside this deployment area. They are not movable support equipment and cannot be built beside the terminal for free reserve. The two chosen alternatives still share one capped reward.

The reserve starts with eight minutes and holds twelve. Completing either relay adds up to six minutes; the one-use backup generator adds up to two. Startup, repair durations, minimum activation, restart delays and relay scheduling retain their existing timings.

Free placement deliberately replaces the original fixed-sector layout. Map validation establishes usable construction ground and landing/relay separation; it cannot guarantee uncovered assault lanes for every player-built arrangement. Finite ammunition, startup/acquisition warnings, destructible guns/fences/sensors and ordinary contested boarding continue to provide counterplay. Combined defenses require live combat testing before the flag is enabled.

## Verification

On 2026-11-02, the additional-purchase and homing update built Shared, Client and Server. All ten relevant regression cases passed across focused runs: both default-off/enabled purchase cases, four missile cases, warning cancellation, disabled/unregistered firing, finite ammunition and 360-degree heavy tracking. The purchase scenarios exercise rejected payment, real checkout charges, shared limits across replacement consoles and catalog rebuilds. The missile scenarios launch from a distant sensor behind the gun, retain the original target through blocked sight, apply direct plus blast damage, and commit permanently to the last position after range, roofing or sensor loss.

```powershell
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Filter 'FullyQualifiedName~SensorRocketKeepsOriginalTargetThenCommitsToLastPosition'
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Filter 'FullyQualifiedName~AdditionalCratePurchasesShareRoundLimitsAcrossReplacementConsoles|FullyQualifiedName~ModuleSwapPreservesAmmoAndSensorLossCancelsRocketWarning|FullyQualifiedName~DisabledAndUnregisteredWeaponsRejectForcedFiring|FullyQualifiedName~TwoMountsWarnBeforeFiringAndExhaustFiniteAmmoWithoutRefills|FullyQualifiedName~HeavyGunTracksAndFiresAroundItsFixedMount'
```

Missile results: `.codex/test-results/Content.IntegrationTests-20261102-031555-580/` (4 passed, none failed/skipped). The other six cases passed in `.codex/test-results/Content.IntegrationTests-20261102-030623-399/`; that earlier combined run also exposed a roof test setup failure resolved in the final missile run. See [the measured damage and DPS breakdown](DAMAGE.md), including armor, explosion vulnerability and fence cooldown assumptions. Repository warnings remain; occupied multiplayer combat still needs a live trial.

On 2026-11-01, Shared, Client, Server and integration tests built. The focused run passed **43 tests, zero failed or skipped**, with no engine error lines in the TRX:

```powershell
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Filter 'FullyQualifiedName~CMUFob|FullyQualifiedName~DropshipSentryRegressionTest'
```

The new cases exercise actual requisitions delivery and assembly at both Nexus and Robotics, rejected alternate-faction/default-off delivery, duplicate shipment prevention, terminal range rejection, platform relocation and the two-slot budget, blocked fence deployment, and damage-preserving recovery. Existing flight tests verify that the logical bank stays groundside during departure. Modules use entity-relative magazine fills so cargo preparation in nullspace cannot deliver empty weapons.

Log: `.codex/logs/test-20261101-125911-430.log`. The geometry recheck found clear candidate space at both LZs and confirmed relay separation. Sprite-state/guide references, all 168 imported source frames, UTF-8/LF formatting and `git diff --check` passed. Repository build warnings remain. Occupied live multiplayer layouts and combat balance are not verified by these checks; `cmu.fob.reserve_enabled` remains false by default.

The subsequent 2026-11-01 balance adjustment doubled the starting reserve (4→8 minutes), capacity (6→12), relay reward (3→6) and one-use backup (1→2). The five-minute successful-mission interval is unchanged, so consistently successful patrols can sustain support power; finite ammunition and destructible hardware remain independent limits. The player guide and equipment descriptions match the new values.

Shared, Client and Server built again, and the focused drain/cap/backup regression run passed **3 tests, zero failed or skipped**, with no engine error lines. Log: `.codex/logs/test-20261101-131737-331.log`.

```powershell
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Filter 'FullyQualifiedName~ReserveDrainsThroughStartupButFreezesInStandbyAndDepletes|FullyQualifiedName~AlternativesAwardOnceAndFullBankDoesNotConsumeNextJob|FullyQualifiedName~UtilitiesFollowReserveAndBackupCannotBeReissued'
```
