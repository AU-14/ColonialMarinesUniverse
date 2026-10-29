# Client stutter, audio and sprite follow-up

This investigation uses the October 28 launcher log, a subsequent v294.0.1
session, and two October 27 `cmu_client_perf` captures. The captures describe
different sessions. Toolbox sources, submodules and the existing engine pin
remain unchanged by this follow-up.

## What the captures establish

- The original v290.0.2 client records a 6,424 ms frame with 34 applicable
  authoritative states waiting. That is a client hitch, but its blocking work
  is not identified by stdout.
- A separate episode advances the authoritative state only two ticks in
  2.759 seconds while the client advances 190 frames. The buffer empties and an
  observer subsequently corrects backward. This can be a server stall or delayed
  network delivery; the sampled 136-152 ms ping does not distinguish them.
- Of 30 logged position jumps, 22 keep the same seat parent and local position.
  Those are inherited shuttle motion, already classified separately by the
  preceding diagnostics change. Seven concern observers; one concerns a living
  character. These counts are rate-limited samples, not a count of every incident.
- The same original log contains 48 audio-source allocation failures, six
  OpenAL out-of-memory errors and 282 sprite errors, predominantly `ghost_*`
  states. The audio failures precede enabling the rubberband diagnostics, so
  they cannot be tied to the later hitch. OpenAL source exhaustion does not
  establish exhaustion of system RAM.
- The subsequent v294.0.1 session has a 4,784 ms connection/lobby frame before
  attachment, a pending trait-label style queue, a missing Venator gib RSI,
  further ghost state errors, and an exception in prediction contact reset
  immediately after possession of a drone.
- A separate 120-second client performance capture reaches 144 audio entities,
  77 on other maps. Gunshots, impacts and footsteps dominate. Its worst measured
  frame is about 85 ms, so it does not profile the multi-second hitch. Audio
  `Loaded` and `Playing` flags do not prove successful hardware source allocation
  or audibility.

## Content fixes

### Copied ghost sprites

Copying a body's sprite removes the original ghost layer map, but previously
left the ghost's `GenericVisualizer` active. A later damage appearance update
reserved a new `ghostVariant` layer against the copied body's RSI and requested
nonexistent states such as `ghost_Piercing2`. Remove that obsolete visualizer
when the body is copied. Ghosts without a copied body retain their normal
damage appearance.

The regression reproduced the exact missing-state error and an extra sprite
layer before the fix. It checks both the copied body and the fallback ghost.
The Venator gib separately pointed at the wrong directory; its path now matches
the existing asset and `venator_gib` state.

### Audio allocation

Direct cross-floor gunshots previously produced an unfiltered original plus
recipient-filtered projections. Vehicle/ramp direct sounds also made unfiltered
projections even on empty floors. Cross-floor PVS could expose a client to both
an audible projection and an inaudible original. Client muting happens after
source creation, so muting did not avoid that allocation.

Direct sounds now select listeners before creating each audio entity, skip
empty audiences, preserve predicted-user exclusion and resolve sound collections
once for all floors. Listener selection follows the eye target and offset,
including remote cameras. Ordinary non-Z playback uses the existing fallback.
Persistent automatically projected loops retain their existing owned lifecycle.

Local vehicle engine loops also previously allocated while inaudible. They now
start within hearing range, stop outside it or on another map, and restart on
return. Existing loops have a one-unit release margin to avoid repeated
allocation at the hearing boundary. This reduces unnecessary sources; it is not
a global audio-source limit or proof that all 48 hardware failures are resolved.

### Lobby trait controls

Hidden trait refreshes now invalidate the model and build the controls when the
tab is selected. Toggling a trait updates existing selectors, category counters
and affordability colors instead of replacing the entire control tree.
Profile changes still refresh from the current prototypes.

A temporary Debug integration harness measured ten hidden `RefreshTraits`
calls at **6,463,720 bytes before and 600 bytes after**, with 91 child controls
before and zero hidden controls after. It also opened the tab and exercised
selection/deselection while checking that controls were reused. Timing and
allocation measurements exclude layout, styling, rendering and the rest of
profile loading. They do not attribute or resolve the entire 4.78-second lobby
stall. The temporary harness is removed before submission.

## Verification

The affected content projects compiled. **18 distinct permanent checks passed**:
15 Z-audio lifecycle cases (run alongside the temporary trait harness), followed
by the three ghost, vehicle and client-sandbox cases below after removing that
harness. The ghost, direct-audio and vehicle regressions each failed before
their corresponding fix. All 13 explicit gib sprite path/state pairs validate
against the existing assets. `git diff --check` and UTF-8/LF checks pass.

To rerun the permanent coverage through the isolated repository wrapper:

```powershell
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Filter 'FullyQualifiedName~CMUZAudioLifecycleTest'
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Filter 'FullyQualifiedName~CMUGhostAppearanceClientTest|FullyQualifiedName~CMUVehicleAudioAdmissionTest|FullyQualifiedName~ClientAssemblyPassesSandboxWithoutLoadingPrototypes'
```

Meaningful regression coverage:

- `CopiedBodySurvivesLaterGhostDamageAppearanceUpdates`: a later appearance event
  cannot add an invalid ghost layer to a copied body; fallback ghosts still work.
- `DirectSoundOnlyAllocatesOnTheListenersFloor`: both direct playback paths send
  one audible source, tested on each floor through a connected client/server pair.
- `DirectPredictedSoundDoesNotAllocateForItsOnlyExcludedListener`: no unused
  source when the sole listener is the predicting user.
- `DirectSoundFollowsRemoteEyeInsteadOfTheBody`: remote listeners receive the
  correct floor's sound.
- `EngineLoopsAllocateOnlyWithinHearingRangeAndResumeOnReturn`: admission,
  release and restart across distance and map changes.
- Existing Z audio lifecycle checks protect source movement, audience filters,
  opening closure, pause/resume, stop/restart and propagated flags.

## Remaining limits

A [later content investigation and fix](client-stutter-content-fixes.md) reproduces
a climbing trigger for the prediction exception and addresses additional
appearance and audio work. The observations below describe the initial pass.

The v294.0.1 prediction exception reads a missing `TransformComponent` for
entity zero inside Toolbox's `PhysicsSystem.UpdateIsTouching`. Its contact
snapshot is processed while collision callbacks can invalidate contacts.
The stdout stack does not identify the responsible callback. Reviewed content
projectile, weed, teleporter and falling paths do not establish which one ran
in this incident, so no speculative movement or prediction changes are made.
The duplicate pull-joint warning in that session also lacks a reproduction.

No live graphical battle, hardware OpenAL exhaustion test or matched
server/client stall trace was performed. Confirm the content fixes in a live
round; collect `cmu_client_perf` during a reproducible remaining freeze and
matching server timestamps/ticks to separate rendering/resource/GC stalls from
delayed authoritative state delivery.
