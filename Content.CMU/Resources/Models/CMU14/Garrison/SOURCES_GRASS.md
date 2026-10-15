# Garrison short grass and flowering ground cover

Twenty source-specific draft assemblies / 950 parts cover 75 classic placements and 334 across configured maps. The canonical definitions are `garrison_grass.yml`. They retain 161 manually inspected tuft/stalk groups across the twenty source states. None is approved art.

## Source interpretation and refinement

The original one-direction 32-by-32 states are in `/Textures/Decals/Flora/flora_grass.rsi`. A/B patches contain olive upright tufts and spreading rosettes; C patches contain thin branching stalks with small purple buds; D/E patches contain dark forked grass with leaves at different stem heights. Each patch keeps its inspected group positions and sparse gaps. Root coordinates and source motif rectangles are recorded in `grass-trace-audit.json`; `review/grass-source-traces.png` marks the inspected roots.

The initial segmented ellipsoids made the leaves look like beads or cactus stems. They were replaced with continuous tapered leaves. A second comparison widened the olive leaves and replaced three identical V blades in D/E clumps with upright stems and staggered forks. The resulting 950 parts comprise 635 slanted leaves/branches, 235 ellipsoids and 80 capped cylinders. All colors occur in the original source images, whose decoded RGBA hashes are retained. No generated textures or new atlas slots are used. The 484-image atlas remains unchanged.

## Continuous leaf geometry

`SlantedX`, `SlantedXReverse`, `SlantedY` and `SlantedYReverse` are closed sheared ellipsoids within their authored bounds. For a unit sphere, the selected horizontal coordinate becomes `sqrt(1-k*k) * coordinate + k*z`, with `k=0.85` or `-0.85` for Reverse. The other coordinates are unchanged. Normal vectors use the inverse transpose. Each exported mesh has 224 triangles; its glTF accessor bounds describe the actual sampled vertices.

Browser and offline comparison meshes use the same construction. Native CPU picking and GPU rendering invert that transform before solving the ellipsoid intersection, then compute the corresponding surface gradient. The packed shape codes are 7 through 10; existing shape codes and texture layout remain unchanged. Texturing these curved shapes is rejected. These are solid geometric leaves, not camera-facing sprite cards.

## Placement and unresolved context

These source sprites have `noRot: true`, one direction and no sprite offset. All 75 placements use zero physical yaw and zero added offset; 41 nonzero saved rotations are correctly ignored for presentation. The scene's total corrected facings rises from 500 to 541. Every saved position/yaw and every unrelated scene record remains unchanged. Group roots stay above ground at their source-derived patch coordinates.

The neighbor audit reports 28 directed conservative part-bound contacts: eight grass/grass entries (four pairs), four wall entries, four tree entries and twelve platform entries. These are potential intersections of part bounds, not proof of intersecting leaf surfaces. Three wall-adjacent source frames already extend horizontally over the neighboring wall tile, but that alone does not establish the correctness of inferred 3D depth or height. Existing platform borders still lack matching raised terrain. Those placement limitations remain open; plants and neighboring objects were not translated to conceal them. Tree-root and overlapping grass contacts are recorded rather than treated as isolated plant models.

Physical height, blade thickness, depth and hidden construction remain inferred from a single view. Side views are simplified, the fine source shading is not reproduced, and wind/bending, source shadows, terrain elevation and dynamic interaction are unfinished. The normal gameplay viewport is unchanged.

## Evidence and checks

- `grass-source-audit.json`, `grass-trace-audit.json`, `grass-palette-audit.json`, `grass-placement-audit.json`: original palettes/hashes, traced groups, saved transforms, facing and conservative neighbor checks.
- `review/grass-comparisons-0.png` through `grass-comparisons-3.png`, `grass-orbit.png`: all twenty source/four-view cards and three additional oblique views were inspected.
- Browser captures cover garden, flowers, prison wall, western platform and eastern wall contexts. Five corresponding `garrison-grass-*` GLBs preserve the original map placements.
- 114 Python checks, 22 Node checks and 99 isolated C# checks pass. The latest affected client build passes with 0 errors / 693 warnings. Native GPU validation passes 624 pixels across two configurations, including all four leaf orientations at two yaws. The earlier full client rebuild also passed with 0 errors / 2,151 warnings.
- All 688 individual exports and all 43 assembled exports pass Khronos validation with zero errors/warnings. The model manifest, references, browser data and GLBs match deterministic regeneration. Connected gameplay verification remains outstanding.

## Source license

New geometric work is CC0-1.0 to the extent separately licensable. The derivative appearance and source references retain CC-BY-SA-3.0.

Taken from tgstation at commits https://github.com/tgstation/tgstation/commit/729d858807905263adab8b5a331c1d8a04982dd3, https://github.com/tgstation/tgstation/commit/79296e902cbdf2352c9303e4769ea39bf3b34e58

## Source states

| Prototype | State | Groups | Parts | Classic objects |
| --- | --- | ---: | ---: | ---: |
| RMCGrassa1 | grassa1 | 12 | 53 | 7 |
| RMCGrassa2 | grassa2 | 12 | 53 | 4 |
| RMCGrassa3 | grassa3 | 9 | 40 | 8 |
| RMCGrassa4 | grassa4 | 11 | 46 | 7 |
| RMCGrassa5 | grassa5 | 10 | 43 | 5 |
| RMCGrassb1 | grassb1 | 5 | 22 | 1 |
| RMCGrassb2 | grassb2 | 5 | 22 | 4 |
| RMCGrassb3 | grassb3 | 6 | 27 | 1 |
| RMCGrassb4 | grassb4 | 6 | 25 | 2 |
| RMCGrassb5 | grassb5 | 5 | 22 | 1 |
| RMCGrassc1 | grassc1 | 8 | 64 | 4 |
| RMCGrassc2 | grassc2 | 8 | 64 | 2 |
| RMCGrassc3 | grassc3 | 10 | 80 | 6 |
| RMCGrassc4 | grassc4 | 11 | 88 | 3 |
| RMCGrassd1 | grassd1 | 6 | 42 | 1 |
| RMCGrassd2 | grassd2 | 5 | 35 | 3 |
| RMCGrassd3 | grassd3 | 5 | 35 | 4 |
| RMCGrasse1 | grasse1 | 9 | 63 | 3 |
| RMCGrasse2 | grasse2 | 9 | 63 | 3 |
| RMCGrasse3 | grasse3 | 9 | 63 | 6 |
