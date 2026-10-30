# Content fixes from the deeper client stutter investigation

This follows [the initial investigation](client-stutter-followup.md), using the
same captures and local reproductions. RobustToolbox, RSI.NET and their pins
are unchanged. These fixes address reproducible content problems; they do not
establish the cause of every reported freeze or network correction.

## Climbing during prediction reset

A human leaving a table corner with a second pending contact reproduced
`Entity 0 does not have a TransformComponent` in the engine's contact-reset
loop. The climb-end callback destroyed its fixture immediately, clearing other
contacts that the loop still held. Disabling that callback avoided the error.

Contact-driven climb cleanup now waits until contact dispatch has finished.
Before removing the fixture, it rechecks climbing state and current support.
Prediction reset is handled before the physics step; ordinary collision
callbacks are handled after the step. The original stop method still restores
collision masks and raises the normal end-climb event.

The permanent regression exercises real table geometry and the actual internal
prediction-reset entry point. It checks both loss of support and support gained
before deferred cleanup. Reflection is used only in that test.

## Clothing and stains

Unchanged appearance events no longer reconstruct every equipped slot. A
client cache tracks sex, species, displacement inputs and owned layer identity.
Equip/unequip, item visual events and inventory-template events still update
slots normally. Replacing or copying a sprite invalidates the cache even if its
layer keys are unchanged.

World stains retain their helper layers and unique shader while source layers
are unchanged. Source transforms, animation and stain color are synchronized;
source visibility/topology changes rebuild the helpers and cleaning removes
them. Worn stains continue through the normal clothing visual events.

A temporary Debug integration harness measured 100 unchanged appearance calls:

| Scenario | Before allocated bytes | After allocated bytes |
| --- | ---: | ---: |
| Human wearing five marine clothing items | 3,034,208 | 292,000 |
| Naked human control | 292,000 | 292,000 |
| Single-layer stained crowbar | 197,600 | 48,000 |

These are main-thread managed allocations in the harness, not production frame
rates. The unchanged clothing layer, stain layer and shader remain the same
objects. Rendering, layout and network overhead are outside these measurements.

## Audio admission

The engine's private per-file counter can undercount active sounds when a
rejected sound entity is deleted. Content cannot repair that private accounting
through a supported API. Instead, managed sounds now have independent admission
before an audio entity is created. Rejected requests reserve and release nothing.

Server admission counts live managed audio per listener and resolved file.
Local predicted admission counts existing audio entities and respects the lower
of the content limit and the client's engine limit. The replicated
`cmu.audio_transient_per_file` setting defaults to 16; zero suppresses managed
transients. Slots are released on entity deletion, conservatively including the
engine's despawn buffer. Loops keep their existing lifecycle.

Standard gunshots, footsteps, projectile impacts and ejected casings use the
new path. Original sounds select listeners on the audible map before creation.
Footsteps, impacts and casings project through nearby openings; existing direct
cross-floor gunshots retain their behavior. Predicted-user exclusion and remote
eye selection are preserved. Muffled vehicle-interior relays share admission
and still work when no original sound entity is needed outside.

This bounds the managed paths, not all audio in the engine. Unmanaged emitters
and incoming sounds overlapping a client's local prediction can still reach the
engine limiter. No hardware OpenAL stress test was performed, so this is a
content mitigation rather than proof that all allocation failures are resolved.

## Further metabolism investigation

The separate 120-second capture attributes 638,671,480 allocated bytes to
30,195 prediction metabolism updates. That scope includes downstream effects
and all processed organs; it does not identify an individual allocation site.
The deadline queue and reusable reagent snapshots already existed in that build.

A one-human client harness (five metabolizers, 22 solutions, 100 forced due
updates after warmup) measured 156,024 bytes with reactions enabled and 135,224
with reactions disabled. Reaction work was only part of that idle case.
Single-reagent stages also created a seeded random generator even when the only
reagent was excluded blood. Random initialization is now deferred until either
shuffling multiple reagents or drawing an effect probability is necessary.
The same harness after the change measured 108,824 bytes with reactions enabled
and 88,024 without them: a 30% reduction in the ordinary reactive case. This is
not an extrapolation to the whole captured round. Multi-reagent ordering,
probability draws, nested calls and prediction replay retain their existing semantics. Chemical updates remain enabled because
reaction eligibility can change without a reagent-list change.

## Verification

The affected content projects built and **45 distinct permanent integration
checks passed** across the focused runs. Coverage includes the two new contact
reset cases; existing table, barricade and falcon climbing; client audio
admission; 22 Z-audio and vehicle-relay cases; nine item-stain cases; six
metabolism snapshot/random-order cases; deadline scheduling; and the client
assembly sandbox check.

Temporary probes also verified clothing refresh after copying a sprite,
changing an equipped prefix and changing sex, plus stain source movement and
hide/show transitions. They supplied the allocation measurements above and were
removed from the test project before submission. Existing item visual-event
callers were inspected for camouflage and accessory refreshes.

Permanent coverage can be rerun with the isolated repository wrapper:

```powershell
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Filter 'FullyQualifiedName~CMUClimbContactResetTest|FullyQualifiedName~ClimbMergeRegressionTest|FullyQualifiedName~ClimbingTest'
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Filter 'FullyQualifiedName~CMUZAudioLifecycleTest|FullyQualifiedName~CMUClientTransientAudioTest|FullyQualifiedName~CMUItemStainTest'
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Filter 'FullyQualifiedName~MetabolismSnapshotReuseTest|FullyQualifiedName~MetabolizerScheduleTest|FullyQualifiedName~ClientAssemblyPassesSandboxWithoutLoadingPrototypes'
```

`git diff --check` and UTF-8/LF checks passed. Existing compiler/analyzer warnings
remain. Toolbox and RSI.NET source checkouts are clean and their pins unchanged.

## Remaining evidence needed

The reproduced climbing exception matches the logged failure class; it does
not prove climbing caused the specific drone-possession incident. The captures
still do not profile the separate multi-second freezes. A live battle capture
and matching server/client timestamps are needed to separate client stalls from
late authoritative-state delivery. Special-purpose audio emitters outside the
managed paths remain possible contributors to hardware source pressure.
