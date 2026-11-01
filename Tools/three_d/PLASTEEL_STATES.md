# Reinforced plasteel barricade state reference

`RMCBarricadeBrutePlasteel` has 56 saved placements, all on Stable Garrison
Redux +1. All 56 save only `Transform`. Their rotations are 0° (7), 90° (26),
180° (11) and 270° (12). These saved records do not prove live state coverage.
The prototype now maps to `CMU3DReinforcedPlasteelBarricade`, with four paired
damage poses, each with and without barbed wire. The library contains 791 drafts and 12 animation clips.
The barricade provides eight static damage/wire scenes and eight acid loop
scenes: one source sequence exported in eight damage/wire contexts.

The placed prototype is not foldable and has no `Door` component. Folding
plasteel art in the shared RSI belongs to another prototype family.

| Source appearance | Selection and behavior | 3D status |
| --- | --- | --- |
| Intact/damaged body and reinforcement | `DamageVisualsSystem` tracks total damage, divides by 56.25 and selects suffix 0, 4, 8 or 12. Nominal boundaries are 0, 225, 450 and 675 damage. Both layers change together; repair can select an earlier suffix. Zero remains visible. | Four draft poses; administrative live preview reads the original layers. Interactive native verification remains open. |
| Barbed wire | `SharedBarbedSystem` selects unwired or wired-closed. Installation and cutting complete after the source do-after; there is no declared installation animation strip. | Four wired damage compositions; native preview reads the existing barbWired layer. Wire section/hidden bends and native interaction remain unverified. |
| Spray acid | `SprayAcided` enables the four-direction `acid` strip: five 0.1-second frames. Its 0.5-second strip duration is separate from the effect's expiry. Further spray refreshes expiry; component removal or water clears the appearance. | Five frame poses in all eight damage/wire contexts (40 compositions). Native preview reads the existing acid frame/visibility; native gameplay interaction and physical side fidelity remain unverified. |
| Destruction | Base threshold is 900. The server adjusts the destruction trigger by the barbed component's default 50 when wire changes. Destruction removes the entity and spawns three plasteel sheets. | Missing runtime review and debris mapping |

The source body is the full `plasteel_barricade_cracks/DamageOverlay_*` image,
not an additional crack overlay over the generic metal barricade. Brute
reinforcement is a separate `AdditionalDamageOverlay_*` layer. The declared
acid slot precedes a wire slot dynamically reserved by `GenericVisualizer`;
the normal composition is body → reinforcement → acid → wire. Verify the
actual sprite owner's transitions interactively in the native client; the adapter
checks the wire RSI/state and order, and rejects unusual extra visible layers.

The collision fixture occupies the south edge of the tile:
`(-0.49, -0.45)` to `(0.49, -0.30)` before rotation. This supplies a physical
placement reference, not the barricade's height. Directional sprite offsets
must not be copied indiscriminately into ground translation.

Run `python Tools/three_d/barricade_state_review.py` to regenerate
`generated/plasteel-state-review/report.json` and four directional reference
sheets. It reads complete saved component blocks for this prototype and checks
counts against every configured map. The general scene reader now reads
`Barbed`, `Damageable`, `SprayAcided` and related saved data. The offline
barricade adapter accepts only the established dry intact defaults without
relevant overrides; other saved appearances remain explicit missing-state markers.

The 192 reference compositions are four directions × four damage levels × two
wire choices × six acid choices (absent plus five frames). They are **2D source
references, not 192 models or implemented states**. The report records original
image hashes, source-code hashes, full resolved components and saved records.
It retains source licensing in `LICENSE.txt`; all ten selected state resources
are CC-BY-SA-3.0, attributed to cmss13 in their RSI metadata.

The current model and 192-pose damage/wire/acid fixture retain front/rear source surfaces and
all saved transforms. Native layer ordering is guarded by the adapter; live
timing/interaction, side artwork, complete physical fidelity, wire/acid depth,
burning/corrosive melting, replacement entities
and destruction/debris remain unfinished. See `generated/plasteel-verification.json`
and `Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_PLASTEEL_BARRICADE.md`.

The subsequent Strata-grate pass replaces the inherited floor candidate and clears all five recorded catwalk overlaps. The subsequent prison hull refinement clears the remaining 14 wall-trim contacts. Barricade geometry and its four damage poses are unchanged. All 28 initially recorded dry-intact contacts are now cleared under the same-level mapped-neighbor audit; native visual and later-state fitting remain open. Current fitting evidence is `generated/prison-hull-contact-verification.json`.

The wire pass adds 23 solid members to each damage pose, preserving every dry
part. It follows the straight source strand and four red ties rather than
inventing a coiled barrier. Wired poses have 58/59/67/77 parts, under the native
128-part budget. Sixteen four-direction reference compositions match all 16,384
original RGBA samples; wire geometry is still an inferred physical reconstruction.
The new overlay clears 1,573 modeled neighbor pairs at all 56 actual positions
under a same-level, four-tile conservative SAT audit. The actual saved map remains
unchanged; the wired scene is a synthetic comparison. See
`generated/plasteel-wire-verification.json` and `generated/plasteel-wire-fixture.json`.

## Five-frame acid implementation

The effect is a small bubbling patch with rising flecks. Five front/rear solid
overlays have 42/46/50/52/50 colored members; the complete acid poses preserve
their dry damage/wire parts. Maximum active pose: 129 parts, checked against
the dedicated instanced effect budget of 160; static/default models stay at 128.
Front/rear source-plane samples all match, while extrusion depth, physical
height differences and side views remain inferred. No broad coating, timed
destruction or separate gameplay state owner is invented.

The native adapter reads `acided`, between the existing reinforcement and wire
layers. Blank reserved slots do not enable acid. Visible layers require the
expected RSI/state, frame 0..4, white tint and identity transforms. Acid removal
returns to the current damage/wire pose. Other effects remain unsupported.
The source owns expiration, refresh and water removal; native interaction still
needs verification. The offline reader continues to reject unknown saved overrides.

The portable GLB contains eight static scenes and eight acid scenes. Select the
matching acid scene and its named clip. Each clip loops 0, .1, .2, .3, .4, .5
seconds, returning to frame zero at .5; this is not a gameplay expiry timer.
`/viewer/barricade-acid.html` provides loop/pause, manual frame time, damage,
wire, facing, effect visibility and free camera rotation beside the source.
The 192 test placements include all 48 poses at four rotations. See
`generated/plasteel-acid-verification.json` and `plasteel-acid-comparison.png`.
