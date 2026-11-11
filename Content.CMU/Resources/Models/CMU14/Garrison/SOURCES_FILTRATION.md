# Water-treatment machines — 2026-09-25

Six source-guided drafts cover 16 saved Stable Garrison Redux placements (14 on level -1,
two on the surface) and five classic Garrison placements. All use the original saved
positions and angles. The current maps save only Transform position/parent overrides;
they do not save a different appearance, damage state, power state or animation frame.

| Source prototype | Source state | Redux | Classic | Physical layout |
| --- | --- | ---: | ---: | --- |
| `RMCFiltration` | `filtration` | 3 | 1 | Long left pressure drum, two right filter vessels, two colored overhead manifolds |
| `RMCFiltrationDisinfection` | `disinfection` | 4 | 2 | Five copper vessels: three rear yellow tops, two front blue tops |
| `RMCFiltrationSedimentation` | `sedimentation` | 3 | 0 | Three narrow rear blue tanks, front filter bed and right motor/service skid |
| `RMCFiltrationSedimentationAlt` | `sedimentation_A_1` | 1 | 1 | One wide left blue tank, rear-right filter bed and front-right motor skid |
| `RMCFiltrationDistribution` | `distribution` | 4 | 1 | Low deck, horizontal hazard rim, copper channels, recessed screen and hollow rusty intake hood |
| `RMCFiltrationDistributionDamaged` | `distribution-damaged` | 1 | 0 | Separate broken hood/collar and loose fragments; no invented runtime damage transition |

Original artwork: `Resources/Textures/_RMC14/Structures/Filtration/96x96.rsi`,
**CC-BY-SA-3.0**, taken from cmss13:
https://github.com/cmss13-devs/cmss13/blob/46d1d000640006d99b3ea99475fee4ba96890702/icons/obj/structures/props/96x96.dmi
The RSI also attributes its unused coagulation arm to:
https://github.com/cmss13-devs/cmss13/blob/46d1d000640006d99b3ea99475fee4ba96890702/icons/obj/structures/props/coagulation_arm.dmi
The RSI metadata remains authoritative. Derivative geometry and `CMU3DWaste*` texture
crops retain this attribution and license.

All referenced states are 96-by-96, one direction, one static frame. Their prototype
chains declare static physics, fixtures, a sprite, clickable presentation and meson
visibility restrictions; no power, damage appearance or animation controller is declared.
The damaged intake is a different saved prototype. This family adds no animation clips.
`RMCCoagulationArm` has a different one-tile footprint and source state, with no placements
in the configured maps. An inherited filtration candidate for it is not an authored arm.

Three generators own the editable models: `author_filtration_vessels.py`,
`author_sedimentation_tanks.py` and `author_waste_distribution.py` under `Tools/three_d/`.
Source/four-view comparisons are under `generated/review/filtration-vessels/`,
`sedimentation-tanks/` and `waste-distribution/`. The waste generator retains original
hazard stripes, grille, copper channels and fascia pixels in ten unmodified crops,
recorded in `generated/waste-distribution-crops.json`.

The source colliders constrain the plan envelope: normally X/Y -1.49 to +1.49 tiles.
Disinfection and alternate sedimentation instead end at Y +0.49, preserving their
south-shifted three-by-two footprint. The 96-pixel sprite height is not treated as a
measurement of physical height. Vessel sections, elevations, bore size, rear surfaces,
filter-bed depth and pipe connections are inferred. All six remain drafts without fidelity
approval. Original colors and preserved artwork alone do not prove a faithful reconstruction.

The surface distribution unit overlaps five saved Kutjevo rock-border cores. Its explicit
presentation cutout removes intersecting rock solids only within the local three-by-three
plan and Z 0 to 1.10 tiles, leaving the rock above as an overhang. That inferred clearance
exposes the low intake; it does not move the machine, change its footprint or alter game
collision. Eight adjoining rock models contribute protruding trim slivers, so 13 rock
models receive cuts in each surface map. The geometric audit records every affected
part, preserves the rock volume above the opening, and finds no remaining intersections
inside that opening. See `Tools/three_d/generated/filtration-terrain-audit.json`.
Other source contacts include platform fascia, water and overhead fuel lines;
these need context review rather than global resizing of the machine family.

Verification for this batch: all six solid envelopes stay within their source collision
footprints, and untextured colors occur in the corresponding original frame. Ten waste
intake crops preserve 4,843 exact RGBA pixels. All 21 saved placements retain their source
positions and angles. Twenty-one assembled regions plus a 24-pose rotation fixture
validate without glTF errors or warnings; their 1,725 entity roots and 33,695 part
transforms match the exported source data. These checks are recorded in
`filtration-source-audit.json` and `filtration-export-audit.json` under
`Tools/three_d/generated/`. They do not establish live gameplay or final fidelity.
