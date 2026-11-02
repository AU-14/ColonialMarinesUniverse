# FOB equipment presentation

All added feedback is gated by the round-latched, default-off FOB reserve flag. Deployment uses a
40-tile radius; shrinking the radius does not move authored relays or landing exclusions.

| Equipment | Feedback |
| --- | --- |
| HVT-7T relay | Original coil sparks, a service panel sliding open during work, assignment/work indicator, completion sparks and confirmation tone. |
| UE-09 terminal | Folded/off/on sprites, screen status, an opening service panel, power tones and repair feedback. |
| UH-02 platform | Outriggers extend during deployment with hydraulics; indicator follows its installed gun. |
| UH-46 sentry | Tracks and fires through 360 degrees. Uses the tank turret's directional-frame helper and continuous counter-rotation while the foundation stays still. Startup cycles the barrels separately; actual shots alternate barrel recoil with a 75 ms flash. Reload withdraws the barrels into a service position. |
| UH-99 launcher | Sensor-directed launches cover any direction. The vertical tubes recoil downward into the housing, with ignition at the top and exhaust below. The real rocket draws above machinery and homes at twelve tiles per second, keeping normal terrain collision and explosion damage. Lost guidance commits it to the last tracked position. The browser preview shows the missile leaving upward from the tube mouth; it does not simulate the game guidance rules. Reload tilts the tubes into their service position. |
| UE-92 illuminator | Telescoping mast and separate lamp head during deployment, reserve-controlled illumination, beacon and switch sounds. |
| UE-04 sensor | The powered head scans above a fixed tripod. It stops when power is lost. |
| UE-02 fences | Original connected electric animations with quiet intervals, contact-shock arcs, sparks and electrical crack. |
| UE-11 generator | No shaking. Charging indicator and short-range rumble; confirmation tone and depleted sprite after consumption. Cancellation stops the rumble. |
| Supply crates and ammunition | Lid folds up over a dark interior and remains open until closed; inherited storage sounds. Successful reloads retain their seating sound without creating extra ammunition. |

`Appearance` replicates status, phase start, cosmetic aim, target-lock duration and crate-open state.
Transient events go only to observers in PVS. A server shot sequence keeps barrel alternation consistent;
late observers do not replay old shots or deployment. Both weapons remove their acquisition marker
when tracking is cancelled or the firing delay finishes.

The client crops cached texture regions from the original RSI frames and moves those parts locally.
No image files or license metadata change. The heavy gun's server-validated arc is now 360 degrees;
the launcher acquires through the FOB's sensors in every direction. Animation never moves a collision fixture, authorizes a shot,
shortens a warning or affects reserve/ammunition. The enabled FOB guns suppress
their inherited muzzle flash because their own shot cue supplies the aligned flash or launcher effects.
Other guns retain their existing effects. Status lights replace the previous full-body tint pulses.

In-game review should check legibility against bright/dark terrain, directional recoil with camera
rotation, and sound levels around a fully assembled FOB. Colours supplement the existing terminal,
examine text, warning messages and sound cues; they are not the only way to identify a state.

## Verification

The focused runner builds Shared, Client and Server and exercises client frame updates:

```powershell
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Filter 'FullyQualifiedName~HeavyGunTracksAndFiresAroundItsFixedMount|FullyQualifiedName~GeometryConcealmentAndIffLossRequireFreshAcquisition|FullyQualifiedName~ModuleSwapPreservesAmmoAndSensorLossCancelsRocketWarning|FullyQualifiedName~TwoMountsWarnBeforeFiringAndExhaustFiniteAmmoWithoutRefills|FullyQualifiedName~UtilitiesFollowReserveAndBackupCannotBeReissued'
```

`HeavyGunTracksAndFiresAroundItsFixedMount` exercises targets to both sides and behind the foundation,
checking ammunition consumption and actual damage while the foundation retains its registered pose.
`ModuleSwapPreservesAmmoAndSensorLossCancelsRocketWarning` now checks that a sensed target behind the
launcher can be acquired while sensor loss during the warning still cancels firing. Existing checks cover obstructions, concealment, IFF, acquisition, finite ammo and
utility power. Client frame updates run during these integration checks.

Result: **Passed: 5, Failed: 0, Skipped: 0**, with Shared, Client and Server builds passing.

The browser preview was checked across 62 selectable states at multiple animation times, without
script errors or horizontal overflow at 375 pixels. It uses the imported artwork and matches the
client's part bounds and motion timings. The missile flight path is compressed to fit the stage, and
slow-motion playback allows inspection of the shot. It is not a rendered game capture. Bright/dark map lighting,
camera rotation and subjective audio balance still need a live game review.
