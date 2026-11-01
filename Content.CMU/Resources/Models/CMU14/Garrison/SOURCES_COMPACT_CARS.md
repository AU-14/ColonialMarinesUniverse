# Garrison Mono-Supron compact car sources

Fifteen static drafts explicitly map eighteen classic saved objects and thirty-three across the configured maps. These include civilian colors and flipped states, police/taxi details and two destroyed variants. Canonical geometry and surface definitions are `garrison_compact_cars.yml` and `garrison_compact_car_art.yml`; one-time scratch authoring scripts must not overwrite later refinements.

New geometric work is CC0-1.0 to the extent separately licensable. Original artwork and derivative source designs retain CC-BY-SA-3.0. Keep source attribution with exported assets.

## Original source

RSI: `/Textures/_RMC14/Structures/Vehicles/vehicles.rsi`. The fifteen referenced states are single static 64-by-64 frames with transparent padding. The non-flipped art faces west; `_f` states face east. Source prototype inheritance, source states, palette samples, pixel bounds, model part counts and saved instance counts are recorded in `generated/compact-cars-source-audit.json`.

Original attribution:

> Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/vehicles

Fifty-nine unique original PNGs serve 109 crop uses. They occupy atlas slots 305–363. Every pixel is verified against its source after the explicitly recorded horizontal normalization of `_f` states and quarter-turn of roof panels. There is no color resampling. Roof, side, glazing, door and roof-pod details are projected on separate solids. Intact source panels on the unseen side of wrecks are explicitly inferred from the corresponding intact state.

## Geometry and source comparison

Each car has four real cylindrical tires, recessed rims and hubs, axles, separated lower skirts, upper rails, grille, lamps, bumpers, tailgate, a hollow cabin, dark seats/dashboard, sloping windshield and raked rear window. The source rear wheels are partly covered by bodywork; the model uses physical covers, leaving the lower tire segment exposed. Police cars retain their contrasting side panels, badge and roof lights. The taxi retains its checker pattern and original roof pod. An invented raised taxi sign in the initial draft was removed during comparison.

Two destroyed models have open visible-side door gaps, torn edges and a shadowed interior instead of a flat black door. Their side mirrors are absent. The opposite side and hidden interior are inferred. Front and rear slope primitives make continuous glazing surfaces. Comparison refinements lowered the initial tall body, narrowed the inferred transverse footprint, replaced duplicated windshield strips with one continuous pane and added the rear rake.

The fifteen final models contain 1,126 editable parts. Individual models remain below the 128-part native preview budget. Full four-view cards and three-page comparison montages are under `generated/review/compact-cars-*`.

## Facing, pivot and placement

All eighteen saved positions/yaws are retained, including fractional placements. An explicit -90-degree model-axis correction faces the ordinary states west; +90 faces flipped states east. This does not infer rotation from an already flipped image. The complete classic scene now reports 471 adjusted facings, eighteen more than the previous checkpoint; every other layout count and every unrelated scene record is unchanged. Support totals remain 803 placed / 74 without an exact support.

The half-tile map-X offset follows the source's horizontal sprite framing. Physical width and the depth pivot cannot be uniquely recovered from these single side-view sprites. Their authored map-Y correction is .25 tile, with .078125 for the purple wreck's narrow saved alcove. These are inferred 3D presentation adjustments based on source padding, actual walls, road dividers and nearby objects; they are not a direct copy of the source's .5 vertical screen offset or an asserted physical measurement. The inferred body width is about .76 tile, with mirrors/wheels extending beyond it. Simulation transforms and colliders remain unchanged.

Placement review checks all eighteen cars against nearby modeled objects using conservative transformed part bounds. The initial overlaps with dividers, wall trim and repair props prompted the footprint refinements. The final audit reports zero intersections against nearby modeled objects for all eighteen placements, without hiding objects or moving map entities. The source/pixel/placement audits and browser orbit/top captures are under `generated/compact-cars-*` and `generated/review/compact-cars-*`.

## Reverse slope primitive

`WedgeYReverse` mirrors `WedgeY` across local Y: its normalized solid is `z <= -y`. It uses shape byte 6, eighteen flat-normal vertices and eight outward-wound triangles. Mirroring reverses triangle winding and Y normals while retaining planar image coordinates. Native CPU/GPU clipping and shading use the corresponding opposite slope; entry/exit cutouts, mirrored U and UV clipping retain the existing sampling contract. This is a bounded primitive, not arbitrary mesh support.

## Validation and limits

The affected client builds with zero errors. The isolated native harness passes 93 checks, Python passes 110 and Node passes 18. Actual Robust-generated shader compilation/rendering passes 560 pixel checks in two configurations. The browser tests both slope directions, rotated silhouettes, source quadrants, mirrored U and cutout picking with 48 color/48 pick samples. All 643 deterministic model exports and atlas/reference outputs match regeneration. Khronos validation reports zero errors and warnings for 643 individual models and 22 assembled exports. Source crop and placement audits supplement renderer tests; they do not approve the art.

All fifteen assets remain drafts. Width, height, hidden front/rear details, light lenses, cabin, suspension and physical pivot are inferred from one view. Curved body panels, finer rims, shattered glass, lighting, realistic materials, occupants and damage animation are unfinished. These source prototypes are static props; no driving behavior is added. Normal connected tests remain blocked by unrelated tactical-map server compilation errors documented in `Tools/three_d/STATUS.md`. Live native placement/performance and the normal gameplay viewport conversion remain unfinished.
