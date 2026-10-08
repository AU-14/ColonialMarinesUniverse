# Stable Garrison Redux: first-person walk-around

The world region supports first-person rendering through the controlled actor. Existing HUD,
inventory, chat, windows, movement, collision and server gameplay owners remain in use. This is
an opt-in renderer for **Stable Garrison Redux**; the original renderer remains the game default.
Administrator privileges are not required for first person on Redux.

## Enable and configure

While controlling a character or observer on Redux, open the client console (tilde by default)
and run `cmu3d`. Run `cmu3d` again to restore the normal world viewport. The introductory popup
shows your current capture key. **Change hotkey** opens the Controls settings; **Start mouse look**
captures the mouse once the console has closed. Closing the popup leaves the cursor released.

The map prototype applies `CMU3DMap` to every Redux floor. Both client and server enforce this
restriction, including after map changes. The old `cmu_3d_firstperson [off]` alias follows the
same rule. `cmu_3d_live` remains a separate debug-administrator workbench.

## Controls and clarity

- WASD and the existing movement bindings move the controlled actor.
- **Alt+F8** toggles capture by default. Rebind **3D: Toggle mouse capture** under
  **Options → Controls → Camera**. This shortcut is independent of ordinary camera rotation.
- Move the captured mouse to look. Use your custom binding or run `cmu_3d_capture` and close the console with its toggle key (tilde by default) or Escape. A request made while console/chat/modal UI is open waits until that UI and its closing Escape are released. `cmu_3d_capture off` releases the mouse; Escape still cancels a pending request made over the world.
- Only visible popups/text fields block capture; cached hidden menus no longer prevent or immediately cancel it.
- **Escape**, focusing chat/console, opening a modal, or leaving the game window releases capture.
  Use Alt+F8 to capture again. The released cursor works with the existing HUD and windows.
- Existing use, alternative use, examine, attack, shooting, reload and inventory bindings remain.
  Captured aiming uses the center crosshair; released aiming uses the pointer over the world.

Console settings apply while the view is open and are saved in the client configuration:

```text
cvar cmu.3d.fov 85
cvar cmu.3d.distance 18
cvar cmu.3d.resolution 1
cvar cmu.3d.brightness 1
```

FOV is **vertical**, default 75 degrees, clamped to 40-110. Distance defaults to 24 tiles and accepts
4-24; actual content also depends on replication and renderer budgets. Resolution 1 renders at the
world panel's pixel dimensions, bounded by `cmu.3d.max_pixels` (1920×1080 by default). For performance, use 0.75
or 0.5 (allowed 0.25-1). Brightness is a lighting multiplier, default 1, clamped to 0.25-2.

## Current implementation

- Geometry snapshots are assembled incrementally; the camera and live sprite poses follow the
  predicted actor each frame. Sprite artwork refreshes at up to 30 Hz. The actor's own sprite is omitted.
  Eye height is 1.65 units. Recoil uses the existing predicted camera kick and screen-shake preference.
- The extended spatial grid supports 1,536 model candidates and 32,768 solid parts. A first pass
  preserves the existing 128-part cell admission order and picking IDs; a second pass admits deferred
  complete objects up to 192 parts per cell. Dense scenes can still exceed that bound. The extra grid
  texture storage is 512 KiB for the extended view; whole-scene frame-time impact is not yet measured.
- Ceilings follow replicated explicit roof masks, entity roofs, RMC area coverage and implicit grid roofs.
  Multi-Z coverage already feeds those masks. Empty floor tiles are queried too, so a ceiling can span
  an opening. Outdoor tiles without roof coverage remain open. Ceiling underside is provisionally 2.75
  units with a neutral material; architectural height and surface detail still need source review.
- Up to 256 nearby live sprites share an atlas, prioritizing characters and effects. Mobs and transient
  effects face the camera; unsupported world fixtures retain their orientation. SpriteSystem supplies
  source layers, directional frames, typing indicators and animations. Clothing and held items remain
  part of the mob sprite; there are no attached or first-person equipment models. Dropped-item sprite
  fallbacks lie flat at their physical elevation. Walls and ceilings occlude sprites and their alpha cutouts.
- Projectiles and effect entities are collected independently of static geometry publication. Muzzle
  flashes and other combat effects use weapon height on their map. Stretched hitscan artwork follows
  the shot axis and turns its plane toward the viewer.
  Combat effects retain unshaded visibility and fading, and cannot intercept interaction/aiming rays.
- Overhead speech uses the sender's sprite height and actual floor in the perspective camera. Existing
  chat formatting, stacking and lifetime remain in use. Solid geometry and the camera's near plane
  hide obscured or behind-camera labels.
- Discrete viewport actions, continuous gunfire and melee receive 3D ray coordinates and source entity
  IDs. Sprite targeting uses the existing click maps. Prediction, damage, range, access checks, reloads
  and authoritative collision remain owned by normal gameplay. Camera pitch does not add vertical
  ballistics to the underlying 2D combat simulation.
- Lighting samples the game's light map, including powered lights, roof shadows and occluders. It is a
  projection of existing horizontal lighting, not a new volumetric light simulation. Existing positional
  audio, footsteps, door and weapon sounds retain their normal owners and camera-relative listener.

The local session reconnects as an **observer**. Free flight and the absence of a weapon are observer
behavior. Control a living body through the existing admin/game tools to exercise walking and combat.

## Prior prototype verification and remaining limits

The following records describe validation of the standalone prototype before its transfer to CMU
master. They are historical evidence; the pull request records verification of the integrated version.

The human right-click crash reported on 2026-09-25 was caused by passing perspective hit points to
the engine's orthographic view-bounds calculation. `ScreenToMap` and the affine matrix now use the
original map viewport; `PixelToMap`/`Aim` retain 3D targeting. Eight focused regression cases call the
actual engine bounds routine across camera angles and capture states. The fix builds successfully.

Client build: zero errors (existing repository warnings remain). **385 native regression tests pass**,
including FOV edge rays, invalid settings, extended spatial encoding, distant picking, ceiling occlusion,
and billboard targeting behind solid geometry. Actual Robust-generated GLSL compiles and links on
the local GPU in both shader variants; pixel checks cover the extended grid, sprite cutouts, sprite/solid
occlusion and lighting darkness, alongside the existing geometry/surface checks.

The updated client passed runtime assembly sandbox verification, connected to Redux and opened the
first-person viewport (20,736 solids in the initial sample). Native visual review of input/capture,
character appearance, powered lighting and combat against a living target is still outstanding.

Replication is not player visibility: fog/blindness and all screen effects are not yet reproduced, so the
mode remains debug-admin only. Actual stacked upper/lower-floor geometry, full effect/overlay parity,
specialized targeting modes and complete animation/fidelity approval remain unfinished. Ceiling
coverage does not mean every upper floor has been reconstructed as a separate 3D story. Draft model
counts and state coverage are unchanged by this presentation pass.

On 2026-09-25, cached hidden popups were found to block capture because the old guard
counted attached controls. The guard now checks visible controls. Three focused tests cover
hidden/open popups, hidden/visible text focus, and window focus; the eight menu-bound tests
also pass. The restarted native client logs capture enable/release transitions without SDL
errors. End-to-end human input and combat acceptance remain separate live checks.

A second capture issue was found in the Escape path: closing the console with Escape after
`cmu_3d_capture` discarded the pending request. UI-issued requests now survive that closing key
and wait for its release. Escape still releases active capture without automatically recapturing.
Capture diagnostics now identify the blocking control or release reason, record the first relative
motion event, and report how many motion events reached the view before release. The previous
client log showed enable/release transitions with no SDL error; it did not record enough detail
to establish why the user's capture was released. Native confirmation is still required.

## Combat and overhead verification (2026-10-08)

`CMU3DLiveBillboardTest` exercises the real client collection and speech control:

- `ProjectileAppearsAndDisappearsWithoutPublishingAnotherMapSnapshot` reproduces the missed transient
  projectile before the fix, then verifies admission and removal without another map snapshot.
- `TurningAroundAMobKeepsItsSpriteFacingTheViewer` reproduces the edge-on mob before the fix and checks
  the sprite plane while moving the camera around it.
- `SpeechTracksTheHeadAcrossFloorsAndHidesBehindGeometry` checks the actual speech control across
  floors, behind a wall, after removing the wall, and behind the camera.

All 12 tests in the following integration selection passed, including prior movement/scene stability
checks and the assembly sandbox. The final focused billboard/sandbox selection also passed (4 tests).

```powershell
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Configuration Release -Filter 'FullyQualifiedName~CMU3DLiveBillboardTest|FullyQualifiedName~CMU3DLiveSceneTest|FullyQualifiedName~SandboxTest'
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.Tests -Configuration Release -Filter 'FullyQualifiedName~CMU3DFirstPersonExperienceTest|FullyQualifiedName~CMU3DViewportBoundsTest|FullyQualifiedName~CMU3DEquipmentTransformTest'
```

The unit selection passed all 27 tests. `validate_native_shader.py` compiled both native shader variants
and passed rendered-pixel checks on the RTX 4070 Ti SUPER, including tilted combat planes, emissive
effects in darkness, effects below half alpha, sprite/solid occlusion and rigid equipment occlusion.
Live weapon handling and visual acceptance remain manual checks; the existing visibility
and 2D ballistics limitations still apply.

### Camera-facing sprite rotation correction

Mob/effect planes face the camera's position. Turning the mouse in place does not rotate those planes
or change directional artwork. Portraits are rasterized upright, with directional frames selected from
the actor's facing relative to the viewer; sprite-local animation/lying rotation remains intact. Picking
uses the same frame and artwork transform, and speech height uses the upright portrait bounds.

`LookingAroundInPlaceDoesNotSpinTheMobArtwork` reproduced both the moving plane and doubled artwork
rotation before the correction. It exercises real sprite layer rendering and checks that turning the
actor still changes its frame. `PortraitPickingUsesTheFrameSeenByTheViewer` aims through the perspective
viewport at occupied and empty corners, covering both pixel masks and explicit directional bounds.

Release build and all 33 tests passed, including the unchanged 2D click tests and assembly sandbox:

```powershell
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Configuration Release -Filter 'FullyQualifiedName~CMU3DLiveBillboardTest|FullyQualifiedName~SandboxTest|FullyQualifiedName~Content.IntegrationTests.Tests.ClickableTest'
```
