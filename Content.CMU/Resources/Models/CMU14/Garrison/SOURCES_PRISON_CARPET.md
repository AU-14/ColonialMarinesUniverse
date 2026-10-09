# Prison carpet source and placement record

Checkpoint: 2026-09-24. `CMU3DPrisonCarpet` is a draft for `CMCarpetPrison`. Stable Garrison Redux is the primary review map; classic remains a comparison map. No asset is fidelity-approved.

## Source and construction

The original art is `Resources/Textures/_RMC14/Structures/Furniture/Carpets/prison_carpet.rsi`, licensed CC-BY-SA-3.0. Its attribution names CMSS13 commit `9dd2c0d0a1c21e3c9bddbad1321c63fd886f61cf`, `icons/turf/floors/carpet_manual.dmi`. Attribution is preserved in `Content.CMU/Resources/Textures/CMU14/ThreeD/PrisonCarpet/LICENSE.txt` and the derived reference RSI metadata.

The model has four thin box quadrants in SE/NE/NW/SW order. Thirty-two source slots (eight `carpet_0` through `carpet_7` states, each with four directions) share thirteen original 16 x 16 crops. No source art is repainted. The isolated comparison reference composes the four state-zero corners. The .012-tile physical thickness, underside and material response are inferred, not measured from the sprite.

`CMCarpetPrison` inherits the upstream `CarpetBase` and its enabled `IconSmooth` corner mode, key `full`, and `carpet_` state prefix. The existing `Content.Client/IconSmoothing/IconSmoothSystem.cs` is the behavioral source. Counterclockwise, diagonal and clockwise neighbours contribute bits 1, 2 and 4 to each corner state. World SE/NE/NW/SW corners use original RSI slots S/E/N/W. Source entity cardinal rotation permutes the layers and their directions without rotating the composed floor pattern out of grid alignment.

The model's `cornerSurfaces` metadata is validated as exactly 32 known surfaces on four unrotated XY box quarters. Python layout and the native debug scene both use anchored, enabled, same-grid source neighbours, including `additionalKeys`, unmodeled neighbours and neighbours outside the export crop. Parts are copied for the selected state; shared prototypes are not mutated. Saved transforms are retained. The browser inspector composes the original corners for the selected saved neighbourhood rather than showing an isolated-tile reference for a connected tile.

## Saved map coverage and contacts

All 152 saved tiles have exact draft mappings: 107 Redux surface, 25 Redux level -2, and 20 classic surface. The inventory covers ten configured map files; the other seven contain no `CMCarpetPrison` placements. All saved positions, rotations, floor tiles and unrelated scene records are unchanged.

The new thin geometry introduces 32 conservative entity contact pairs / 105 solid pairs where the carpet previously had no model: 11/38 in classic, 20/63 in Redux surface, and 1/4 in Redux -2. These involve platform feet/edges, chair feet, handrails, a coat rack, a plant and an apple. They remain recorded mounting/clearance work. No arbitrary furniture lift is applied. The audit checks individual world-space solid bounds within two tiles on the same level and excludes unmapped neighbours; it is not an exhaustive collision or fidelity approval.

Three existing assembled regions are refreshed, nine saved regions and a synthetic comparison are added. Actual exported GLB node IDs verify coverage of all 152 saved tiles. The fixture has 28 carpet tiles in five arrangements: solid square, L shape, ring with an inner hole, diagonal pair and straight strip. The source/four-view card, fixture, source inspector and actual Redux surface/lower-level placements were visually checked. Browser console errors/warnings are zero. Native interactive and frame-time review remain undone.

## Verification and remaining work

- All 32 source slots / 13 unique crops are byte-verified. All 256 neighbour masks at all four cardinal entity orientations match the original composition: 1,024 comparisons.
- All 1,882 prior definitions, saved transforms/floor tiles and 114,220 unrelated scene records remain unchanged across the three affected snapshots.
- 140 Python checks, 140 isolated native checks and the client build pass (zero errors / 2,150 existing warnings).
- All 770 deterministic model exports, 770 individual GLBs and 278 assembled GLBs pass with zero glTF errors/warnings.
- The library has 26,795 solids / 2,346,592 triangles. The atlas has 664 images, 4096 x 704 pixels / 11 MiB; 198 models use original source surfaces.

Evidence: `Tools/three_d/generated/prison-carpet-source-audit.json`, `prison-carpet-placement-source.json`, `prison-carpet-verification.json`, `prison-carpet-export-review.json`, both `prison-carpet-*-glb-validation.json` reports, and `review/prison-carpet/`.

These are static, connected floor states. This batch adds no animation clips and does not complete other carpet families, dynamic damage/power states, character rigging, lighting, or the standard gameplay viewport.
