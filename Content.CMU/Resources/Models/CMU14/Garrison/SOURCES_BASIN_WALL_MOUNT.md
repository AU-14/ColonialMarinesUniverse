# Basin rear-wall clearance

Draft checkpoint: 2026-09-25. Stable Garrison Redux remains primary.

The source basin is a directional wall fixture. Several saved pivots lie
inside the modeled wall volume; moving the basin's rear edge to its pivot
alone did not expose it in those rooms. This pass adds an explicit
`backWallMountTargets` rule to `CMU3DSink` in the offline scene builder and
native debug workbench. Its 25-part geometry, source palette, four source
directions, saved position and saved facing are unchanged.

Source art, physical-dimension limits and CC-BY-SA-3.0 attribution remain
documented in `SOURCES_SINK_FIT.md`. The source prototype is
`Resources/Prototypes/_RMC14/Entities/Structures/Furniture/sink.yml`.
No gameplay movement, collision or original sprite behavior is changed.

## How the clearance is selected

Only the five explicitly named exact wall families can provide support:
Hybrisa, Hybrisa medical, prison hull, reinforced prison and Strata.
The wall must be anchored on the same grid, in the surrounding nine tiles,
and have an exact static model. Support lookup includes source records
outside a cropped export. A basin itself need not be anchored: the saved
sink records override only Transform and do not serialize anchoring.

The saved front selects the direction. Wall solids are transformed into
the basin's frame; only parts overlapping its frontage and height supply
a clearance plane. Overhead trim and low foundations do not move a sink.
The rear must clear the support by .01 tiles. The rule preserves spacing
along the wall and only moves forward; it does not pull an already clear
fixture back toward a wall. The largest required offset handles a basin
spanning adjacent wall pieces.

Front-side walls, unrelated grids, missing/inherited supports, non-cardinal
relative wall axes, dynamic connected supports and offsets exceeding .75
tiles remain unsupported. Curved and textured parts do not provide a
mounting plane. Walls are evaluated at their full geometry, independently
of the review camera and wall-cutaway setting. The current named wall
families use solid cardinal boxes. Their inferred 3D construction and the
.01-tile assembly clearance remain draft assumptions.

## Saved-map result

| Scene | Basins | Clearance offsets | Contact pairs before / after |
| --- | --- | --- | --- |
| Redux surface | 43 | 33 | 32 / 7 |
| Redux -1 | 2 | 2 | 2 / 0 |
| Redux -2 | 12 | 0 | 7 / 7 |
| Redux +1 | 23 | 12 | 26 / 14 |
| Classic comparison | 37 | 33 | 27 / 2 |

There are 47 Redux adjustments and 33 classic adjustments. The audit clears
64 entity contact pairs (39 Redux, 25 classic), introduces none and leaves
30 unresolved. All 80 selected basin/support pairs are clear. Part contacts
fall from 2,267 to 630. This is a same-level, four-tile-neighborhood audit;
curved/textured neighbors use conservative bounds and unmodeled neighbors
are excluded.

The remaining pairs involve counters, toilets, one operating table, one
sink pair and a repeated sink facing into a wall (classic #2854, Redux
#5767). That facing is preserved for a separate source-context review.
Correction: the grey SPP wall already had an exact draft. This historical
pass did not enroll it as a basin support. The later `SOURCES_SPP_WALL.md`
refines that existing model and adds eleven .01-tile basin clearances. Counter cutouts, support/plumbing construction, liquid appearance
and all sink animation work remain open.

All saved transforms, 196,518 unrelated entity records, floors, connected
geometry and prior shutter offsets are unchanged. All 1,903 unrelated
definitions and 777 other viewer models are unchanged. The library stays
at 778 drafts / 27,109 parts / 2,350,360 triangles; no asset is approved.

## Verification

The 152-test Python suite and six focused mounting checks pass (153 distinct
checks). All 186 isolated native checks pass, including the model budget.
The client build passes with zero errors and 2,150 existing warnings.
All 778 deterministic individual and 503 assembled GLBs validate without
glTF errors/warnings. Seventy-three saved regions are refreshed and a
16-entity embedded/fitted comparison is added. Actual exported node IDs,
translations, rotations and 25-part basin geometry verify all 117 saved
placements, including the 80 adjustments.

The corrected Redux prison room and all four before/after mounting sides
were visually inspected. The browser reports no console errors/warnings.
Native interactive behavior and frame-time measurements are not verified.
The ordinary gameplay viewport is unchanged.

Evidence: `Tools/three_d/generated/basin-wall-*`. This pass resolves wall
embedding, not the broader state/animation, asset-coverage or fidelity goal.
