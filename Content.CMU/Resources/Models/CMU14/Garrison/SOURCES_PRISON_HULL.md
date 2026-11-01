# Prison heavy hull wall

`CMU3DPrisonHullWall` maps `RMCWallPrisonHull` at all 4,223 saved placements:
3,049 Redux (1,292 surface; 607 +1; 312 +2; 419 +3; 419 +4) and 1,174 classic.
Its editable definition remains in `garrison_environment.yml`; source surfaces
are in `garrison_prison_hull_surfaces.yml`. No source map is edited.

The rebuilt model has 51 members. Original grey/red corner artwork replaces
the old generic olive cap. Four recessed sides use sampled original colors,
formed plates, red channels, jambs, ribs and center fasteners. All geometry
stays inside the source fixture (-.5,-.5) to (.5,.5). The previous trim extended
up to .06 tiles outside it. Height remains an inferred 2.8 tiles; side elevations,
recess depths, material response and hidden construction are still inferred.

## Connected source and orientation

The original `rwall0` through `rwall7` each have four RSI directions. Their
six-pixel upper / 26-pixel lower corner split is retained at local Y=.3125.
Twenty distinct crops fill 32 slots. All 256 eight-neighbor masks match the
original compositions at four rotations, checking 1,048,576 RGBA samples.
The derived `PrisonHullReferences.rsi/isolated` composes the four rwall0 corners;
it does not copy the green debug lettering in the unsmoothed rwall/hwall icons.

All 4,223 actual masks were independently checked against anchored, enabled
IconSmooth neighbors with keys walls, windows or doors. The maps contain 133
distinct masks. Eight hundred two wall artwork orientations now follow the grid;
all saved transforms remain unchanged. This source uses an invincible parent
without Damageable, Injurable, Destructible or a declared animated appearance.
Other prison wall families have separate behavior; this pass adds no clips.

## Context and exports

The new solids are contained in the former full-tile core at every saved
placement, so unchanged neighbors cannot gain wall intersections. Seventeen
sinks, five mirrors and twelve red door controls update their presentation
offsets against the new wall face. Separate SAT checks of those 34 fixtures and
all 56 plasteel barricades clear the remaining 14 recorded barricade/wall
contacts with no new intersections. The fixture checks had zero contacts both
before and after. Existing contacts elsewhere, unmapped neighbors, cross-level
geometry and native visual behavior are outside this targeted clearance claim.

Floors, prior geometry variants, 322,108 unrelated scene records, 2,096 other
definitions and 790 other viewer models are unchanged. Seventy-one regions
were refreshed and 146 added, plus a 260-pose source mask/orientation fixture.
Actual GLB nodes verify all saved walls/fixtures and test poses, including 7,980
root occurrences, 405,502 part transforms and 3,403 original image copies.
All 791 deterministic library GLBs and 716 assembled GLBs validate without
errors/warnings. The native model-budget test passes; runtime code is unchanged,
so prior full suites and builds were not rerun.

Source/four-view, old/new wall and four isolated saved-joint renders were
visually reviewed. Browser selection/focus and the saved-corner reference were
checked at #55975 with no console warnings/errors. Browser canvas screenshots
and native visual/interaction review remain unverified. No model is fidelity-
approved; the ordinary gameplay viewport is unchanged.

Evidence: `Tools/three_d/generated/prison-hull-verification.json` and related
`prison-hull-*` files. Current Redux +1 scene: `prison-hull-redux-plus1-scene.json`.
Redux surface and classic defaults are refreshed; Redux +2, +3 and +4 now also
have current review scenes.

## Attribution

The 20 surface PNGs in `Textures/CMU14/ThreeD/Garrison/PrisonHull/` and isolated
reference composition in `PrisonHullReferences.rsi` derive from
`Resources/Textures/_RMC14/Structures/Walls/prison_rwall.rsi/rwall0..7.png`.
Original RGBA is retained. Source and derivatives are **CC-BY-SA-3.0**.

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/09a5191fb11aab8ddffe3f9be94292b53e4d96f6/icons/turf/walls/prison.dmi
