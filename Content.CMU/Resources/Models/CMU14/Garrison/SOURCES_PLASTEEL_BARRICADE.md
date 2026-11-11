# Reinforced plasteel barricade

`CMU3DReinforcedPlasteelBarricade` maps `RMCBarricadeBrutePlasteel`, placed
56 times on Stable Garrison Redux +1. The editable source is
`Content.CMU/Resources/ThreeD/Prototypes/World/garrison_plasteel_barricade.yml`.
All saved transforms are retained, including 7 south, 26 east, 11 north and
12 west facings. No source map is edited.

The assembly contains a plasteel panel, reinforcement posts, cap blocks,
foot shoes and lower rails. Distinct front/rear artwork is retained on
separate solid surface members. Source colors on cap and foot surfaces are
kept separate. The south-edge section center is -0.375 tiles, matching the
source fixture's center. The one-tile width, .625-tile height, quarter-tile
cap depth and hidden construction are inferred. Source artwork does not
establish a unique physical reconstruction; side artwork remains unfinished.

Four paired static damage poses use suffixes 0, 4, 8 and 12, with 35, 36,
44 and 54 parts respectively. Four wired counterparts add 23 parts each.
The current GLB has eight dry scenes plus eight acid loop scenes, defaulting
to dry intact. Its eight acid clips represent one source loop across contexts. The native
administrative live-scene preview reads both original sprite layers and
selects matching geometry, including repair back to an earlier pose. Unknown,
mismatched, tinted or transformed layers and additional effects remain
unsupported markers. Known wire and acid layers now select authored compositions. There is no second gameplay event
owner, damage counter or animation timer.

Saved scenes establish only the dry intact pose when defaults match the
reviewed source and no relevant component overrides exist. Damage, wire,
acid and other saved components are read and rejected for review instead
of being silently discarded. The normal gameplay viewport is unchanged.
Native interactive damage/repair and destruction have not been verified.

## Verification and remaining fit work

All 5,120 isolated front/rear pixel and occupancy samples match the source
compositions. Complete orthographic solids retain all source-occupied pixels,
but hidden members add 0–23 silhouette pixels depending on pose and side.
This does not prove a complete sprite projection match or physical fidelity.

Reducing the initial .314-tile cap depth to .25 clears nine of 28 mapped,
same-level contact pairs, without introducing any. Nineteen remain: fourteen
with existing prison-wall base trim and five with co-located catwalks.
Wall-end fitting and floor seating remain unfinished. Cross-level and
unmapped neighbors are outside this contact audit.

The 16-object fixture covers four poses at four rotations. All 56 saved
placements and fixture poses are verified in actual GLB nodes. The browser
uses paired source compositions for the selected damage pose and map facing.
Source/four-view and damage comparison PNGs were visually reviewed; browser
screenshot capture was unavailable during this pass.

Evidence is under `Tools/three_d/generated/plasteel-*`, with source rules in
`Tools/three_d/PLASTEEL_STATES.md`. No model is fidelity-approved. Wire,
spray acid, burning, corrosive melting, upgrades, destruction/debris,
remaining placement fits and normal gameplay integration remain open.

## Attribution

The 98 surface PNGs under
`Content.CMU/Resources/Textures/CMU14/ThreeD/Garrison/PlasteelBarricade/`
are crops of original body/reinforcement compositions. They retain original
RGBA; physical depths, the rear UV reflection and cap/foot mapping are
reconstruction choices. Source and derivatives are **CC-BY-SA-3.0**.

- `Resources/Textures/_RMC14/Structures/Walls/Barricades/plasteel_barricade_cracks.rsi`: taken from cmss13, https://github.com/cmss13-devs/cmss13/blob/5cf465e72efb6beccd2b78bf263072816a2a60ad/icons/obj/structures/barricades.dmi
- `Resources/Textures/_RMC14/Structures/Walls/Barricades/brute_barricade_cracks.rsi`: taken from cmss13, https://github.com/cmss13-devs/cmss13/blob/41109aa6eebf87892e41b61418b7b06b279d04b6/icons/obj/structures/barricades.dmi and the revision above.

Additional wire/acid reference sheets are source studies only; their full
attribution is retained in `generated/plasteel-state-review/LICENSE.txt`.

Final checks: all 790 deterministic library GLBs and 568 assembled GLBs validate with zero errors/warnings. Native tests pass 231 cases, Python 174 and JavaScript 35; the native model-budget check passes. The client builds with zero errors and 692 warnings. Saved entity #2814 selects the paired reference in the browser without console errors/warnings. These checks do not establish complete visual fidelity or native interactive playback.

Follow-up: the Strata grate model clears all five original catwalk contacts without changing this barricade geometry. Fourteen wall-trim contacts remain. See `SOURCES_STRATA_GRATE.md` and `Tools/three_d/generated/strata-grate-verification.json`; the 19-contact count above describes the initial barricade checkpoint.

Prison-hull follow-up: the remaining 14 recorded barricade/wall contacts are cleared by refining the wall inside its original tile footprint. Earlier contact counts above are historical. See `SOURCES_PRISON_HULL.md` and `Tools/three_d/generated/prison-hull-contact-verification.json`. Native visual, later-state and whole-map fidelity review remain open.

## Wire state follow-up (2026-09-25)

Current model: four dry and four wired damage compositions, eight static GLB
scenes and 430 stored part occurrences. All dry parts are retained. The 23 new
members describe one straight strand, ten barb segments and four three-member
red ties. Wired poses contain 58, 59, 67 and 77 parts. The source shows a straight
strand, not a coil, and has one static frame per direction; no animation clip
was added. Earlier statements that wire geometry is missing describe the
initial checkpoint. Acid and other overlays remain unimplemented.

Reference: `Resources/Textures/_RMC14/Structures/Walls/Barricades/barricade.rsi`,
state `plasteel_wire`; four RSI directions S/N/E/W, no animation delays.
Source and derived wire geometry/reference compositions retain **CC-BY-SA-3.0**:
taken from cmss13 at
https://github.com/cmss13-devs/cmss13/blob/5cf465e72efb6beccd2b78bf263072816a2a60ad/icons/obj/structures/barricades.dmi.
PNG SHA-256: `0883060458a9716832d7f1fa734440d9dd902d93bd1a4086d38b00eb5c3f9b76`.
South strand bounds are columns 2..29 on row 11; the red tie columns are
3, 12, 19 and 28. Cross-section, barb depth/yaw, shared physical height across
stylized source views and hidden tie returns are reconstruction choices.

The existing sprite owner controls native wire visibility for installation and
cutting. The preview also tracks the owner's damage/repair layers; unsupported
wire resources/states/transforms and additional visible overlays remain markers.
No gameplay event, state counter or animation timer was introduced. The 56
actual Redux +1 records and default saved scene are unchanged and remain dry.

All sixteen wired source compositions match original layers (16,384 RGBA samples).
The new 32-pose fixture verifies 1,720 part transforms; all 56 potential wired
placements clear 1,573 same-level mapped-neighbor pairs under conservative SAT.
Unknown/cross-level neighbors and native interactions are not certified. Solid
four-view comparisons were inspected; source-reference matching does not prove
geometry fidelity. All 791 library / 717 scene GLBs validate, and 243 native,
176 Python and 36 browser module tests pass. Client build has zero errors and
2,150 warnings. Browser #9978 selects the wire/damage-12 composition without
console warnings/errors. Native interactive review and full fidelity remain open.

Evidence: `Tools/three_d/generated/plasteel-wire-verification.json` and
`plasteel-wire-source-audit.json`; comparison: `plasteel-wire-comparison.png`.
Review: `/viewer/?scene=../generated/plasteel-wire-fixture.json`.

## Bubbling acid follow-up (2026-09-25)

Current authored states: eight dry poses and forty acid frame compositions.
Five source frames repeat every 0.5 seconds in each of four damage levels with
or without wire. Eight portable clips repeat that one sequence in eight scenes;
they are not eight independent behaviors and do not implement effect expiry.
The matching acid scene must be selected before its clip is played. The original
sprite frame/visibility drives the native administrative preview; the source
system retains expiration, refresh, damage ticks and water removal ownership.
The saved map's 56 default dry records are unchanged. Earlier missing-acid
statements describe historical checkpoints; fire/corrosion/debris are still open.

Source: `Resources/Textures/_RMC14/Effects/xeno_spray_acid.rsi`, state `acid`,
five .1-second frames in each of four RSI directions. Source and derivative
geometry/reference compositions retain **CC-BY-SA-3.0**; cmss13 attribution:
https://github.com/cmss13-devs/cmss13/blob/ce39f048bf5eb25e2a93d7355327ccacc0504b01/icons/effects/status_effects.dmi.
PNG SHA-256: `0dbb5c96d608749be309841ef6fa27a693e1bdf655f0411db91ee2b31c4985d8`.

The compact patch and rising flecks are colored rectangular volumes, with
42/46/50/52/50 members per overlay. South is projected using sprite baseline
32 and north using the body baseline 20 with reflected X; all 10,240 source
plane RGBA/occupancy samples match. The physical thickness, different front/rear
heights and side projection remain inferred. East/west source silhouettes do
not change across the strip, and are not claimed to match the current 3D end
elevations. These are draft effect volumes, not finished fluid simulation.

There are 4,500 stored part occurrences across the 48 poses. The largest active
pose has 129 parts. The source-exact color partition is retained using an acid
composition budget of 160 in the instanced renderer; the static model budget
remains 128. All authored frame budgets pass. No atlas images were added.

All 160 source compositions match original layering; the 192-pose fixture
verifies 18,000 exported part transforms. Five acid overlays at all 56 actual
placements clear 1,573 same-level mapped-neighbor pairs under conservative SAT.
Unknown/cross-level neighbors, native gameplay interactions and complete visual
fidelity are not certified. All 791 library / 718 assembled GLBs validate,
with 267 native, 180 Python and 39 browser checks passing. Client build: zero
errors / 692 warnings. Browser loop/facing/frame controls and clearing acid
while preserving wire/damage were checked without console errors/warnings.
Offline four-view renders were inspected; native interaction remains unverified.

Evidence: `Tools/three_d/generated/plasteel-acid-verification.json`,
`plasteel-acid-source-audit.json`, and `plasteel-acid-comparison.png`.
Interactive comparison: `/viewer/barricade-acid.html`.
