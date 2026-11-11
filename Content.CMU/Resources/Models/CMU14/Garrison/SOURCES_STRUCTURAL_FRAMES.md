# Charcoal structural lattice frames

Two static assemblies cover `RMCStructureLatticeC` and
`RMCStructureLatticeDoubleC`: 121 Redux placements and two classic placements.
The source's upper grid is reconstructed as a horizontal frame; its lower
members form the front, with inferred matching side and rear construction.
The resulting geometry has open spaces between solid steel members.

Both use the source's 28-pixel width at 32 pixels per tile (.875 tiles).
A square plan and the upper grid's 20-row projection imply a reference view
angle of asin(20/28). At that angle the 12- and 22-row front faces produce
heights of approximately .536 and .982 tiles. The square plan, member depth,
physical height, hidden surfaces and view angle are reconstruction assumptions.
They are not dimensions supplied by the engine. The tall image's 22 blank top
rows and `0,0.5` sprite offset do not become horizontal map translation.

Top/front regions retain original charcoal artwork on opaque member surfaces.
Back and side repetition is inferred; projection and placement checks do not
make these fidelity-approved assets. The two RSIs contain six static states;
only the two placed C states are modeled here. A/B are other source palettes.
No animated strips or animation clips are added.

The source Destructible component removes the entity and spawns `CMSheetMetal1`
at its 50-damage threshold. It declares no alternate damaged sprite. Native
destruction, spawned debris, interaction and performance review remain open.
The ordinary gameplay viewport is unchanged.

## Attribution

`Resources/Prototypes/_RMC14/Entities/Structures/Walls/structure_lattice.yml`

`Resources/Textures/_RMC14/Structures/structure_lattice.rsi`

`Resources/Textures/_RMC14/Structures/double_structure_lattice.rsi`

CC-BY-SA-3.0. Both original metadata files credit:
Taken from cmss13 at
https://github.com/cmss13-devs/cmss13/blob/7cb618c69b75873f3ce893022fe08d1233b3152d/icons/obj/structures/structures.dmi

Original RGBA crops are in
`Content.CMU/Resources/Textures/CMU14/ThreeD/Garrison/StructuralFrames/`.
Editable model and surface definitions are in `garrison_structural_frames.yml`.
Keep this attribution with exported assets.

## Verification and remaining context work

The final two-pixel member depth preserves all source-occupied pixels and reduces
full-projection extras from 56 to 44 per model. Separate top/front RGBA and masks
match all 2,368 samples. Full projection silhouette overlap is 94.88% / 96.14%.
Hidden construction remains inferred; no asset is fidelity-approved.

The same-level mapped-neighbor audit records 235 contacts, all at original
co-located positions: 141 platform edges, 76 gratings, 14 water surfaces, two
railings and two duplicate-frame pairs. Terrain elevation, foot seating and
structural attachment fitting remain open. Redux -1 retains exact source
duplicates #9610/#9611 and #9612/#9613. Cross-level and unmapped neighbors were
not tested. These contacts must not be described as resolved placements.

All 789 individual and 559 assembled GLBs validate without errors/warnings.
Actual nodes verify all 123 saved frames and eight fixture poses; embedded PNGs
retain source crop pixels. Native model-budget test passes. Native interactive,
destruction and performance review remain unfinished. Evidence is in
`Tools/three_d/generated/structural-frame-verification.json` and related files.
