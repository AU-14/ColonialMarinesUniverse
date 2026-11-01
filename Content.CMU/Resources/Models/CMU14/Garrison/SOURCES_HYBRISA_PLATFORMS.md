# Garrison Hybrisa metal platforms

Nine existing drafts were revised in `garrison_environment.yml` and `garrison_architecture.yml`. Their 376 parts replace 53 earlier parts, covering 1,842 classic placements and 4,271 placements across configured maps. IDs, exact mappings, saved transforms, facing metadata and simulation fixtures are retained. The other 703 model definitions are unchanged. The library remains 712 drafts; this correction does not increase coverage.

New geometric work is CC0-1.0 to the extent separately licensable. Source visual design and derivative appearance retain the original CC-BY-SA-3.0 license and attribution below.

## Source construction

- Platform Three now has two feet under each full beam and actual open space beneath it. The old third tooth, solid lower fascia and full-width foot strip were unsupported by the source. The outside corner has joined south/east arms; the small return occupies its original northwest fixture.
- The plain metal edge has actual grille openings, narrow intermediate bars, a lower rail and three main posts. Its small return preserves the corresponding narrow opening.
- Platform Two retains the original ridged/checkered cap, two end feet and dark lower panel. The central panel is recessed .025 tiles from both faces, instead of painting an undifferentiated bar with invented colors.
- The two stair-end models retain distinct mirrored, three-level lower profiles from their North sprites. They are no longer identical plain bars. South/East/West source directions reuse straight-edge artwork, which does not define coherent complete camera views; the North construction is an explicitly inferred physical interpretation.
- All colors occur in the original opaque source pixels. Exact colored rectangles retain cap patterns and opaque front profiles as solids, including actual gaps. Semitransparent black cast shadows are excluded from geometry. No new image or atlas slot was added.

## Dimensions and placement

The south edge, southeast outer corner and northwest small return follow the source fixtures. Total height remains .39 tiles and is inferred. Front profiles retain a .13-tile structural depth. All caps use .15-tile physical depth, with a slight overhang around the .13 fixture. Small return bodies use .13-tile width. This depth is inferred from fixtures and placement clearance: source pixel shading is not copied directly into ground-space width.

The initial source-width interpretation added twelve conservative contacts with benches, bins, bags and a medical case. Narrowing caps removed all twelve. Directed neighboring part-bound contacts fall from 4,817 to 957, with 3,860 old pairs removed and zero new pairs. Counts include wall trim, joined platform entities, stairs, water and other co-located geometry. These are conservative bounds, not proof of exact surface intersections or a fully corrected scene. Existing wall/terrain joins and ground elevation still require work.

The earlier D-shrub check is refreshed against actual analytic leaf/stem solids. #9790/#12906 no longer intersects. #9782/#12904 still intersects in eleven part pairs (source front and subdivided cap pieces); part counts are not directly comparable to the old unsplit cap. This remaining overlap is not claimed fixed. No plant or map position was moved to hide it.

Every visible saved scene record, all diagnostics, all saved positions/rotations and unrelated model definitions are unchanged. Added offsets are zero. No gameplay or renderer code changed.

## Verification and artifacts

All 4,267 original source cap/profile pixel samples agree with final solid occupancy and palette under the documented physical projection. Checks retain all 1,842 exact classic instances, validate the model part limit and compare unchanged models/scene records. All nine source/four-view cards and nine directional/oblique sheets were inspected. Browser captures cover the grille, paired stair ends, ridged return, planter corners and bench clearance.

The deterministic export check verifies 712 GLBs, manifest and viewer assets. Khronos validation reports zero errors and zero warnings for all 712 model GLBs and 55 assembled GLBs (54 actual map regions plus the isolated pose fixture). The existing isolated native validation suite passes all 99 tests against the current asset library. No game build is needed for these YAML-only geometry changes.

Three added actual map exports are `garrison-platform-grille`, `garrison-platform-stair-ends` and `garrison-platform-ridged`; every existing map-region export was refreshed. Evidence is in `Tools/three_d/generated/hybrisa-platform-*`, `stemmed-shrubs-platform-audit.json`, model comparison cards and `review/hybrisa-platform-*.png`.

All nine assets remain drafts. Height, hidden faces, inferred physical depth, stepped-end interpretation, fine materials, raised terrain and dynamic game presentation remain unfinished. The dark rock edge inspected alongside this family is unchanged and remains a separate unfinished draft. The normal gameplay viewport is unchanged.

## Source attribution

- Source: `/Textures/_RMC14/Structures/platforms.rsi`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/platforms.dmi, https://github.com/cmss13-devs/cmss13/blob/48e570bd697f2476e28d89cd255d0539a5228228/icons/obj/structures/props/platforms.dmi

## Revised source states

| Prototype | State | Parts | Classic | Combined |
| --- | --- | ---: | ---: | ---: |
| RMCPlatformHybrisaEdge | hybrisametal | 29 | 13 | 26 |
| RMCPlatformHybrisaEdgeCornerSmall | hybrisametal_deco | 29 | 4 | 8 |
| RMCPlatformHybrisaThree | hybrisaplatform3 | 37 | 834 | 2165 |
| RMCPlatformHybrisaThreeCorner | hybrisaplatform3_corner | 73 | 213 | 443 |
| RMCPlatformHybrisaThreeCornerSmall | hybrisaplatform_deco3 | 19 | 580 | 1238 |
| RMCPlatformHybrisaThreeStair | hybrisaplatform3_stair | 19 | 37 | 78 |
| RMCPlatformHybrisaThreeStairAlt | hybrisaplatform3_stair_alt | 19 | 37 | 78 |
| RMCPlatformHybrisaTwo | hybrisaplatform2 | 105 | 97 | 181 |
| RMCPlatformHybrisaTwoCornerSmall | hybrisaplatform_deco2 | 46 | 27 | 54 |

## Platform Two packing correction, 2026-09-25

This correction supersedes the 105-part representation of **CMU3DPlatformHybrisaTwo** above. It changes only that
model to seven opaque textured volumes: the upper beam, recessed middle panel, two end blocks, two short feet,
and cap. The original solid occupancy, .39-tile height, .13 structural depth, .15 cap depth, .025 panel recess,
exact mapping and saved facing are retained. Other platform models remain unchanged.

The 105 coplanar color pieces could consume an entire renderer cell alongside existing water/floor geometry,
causing the newly modeled disinfection assembly or nearby railing to disappear. Replacing color subdivisions
with their original pixels preserves the source detail while reducing the spatial entries by 98.

`Tools/three_d/author_hybrisa_platform_two.py` generates seven unresampled RGBA crops from the original
`hybrisaplatform2.png` four-direction sheet. North-frame front crops are mirrored to retain the established
local-X projection; the South-frame cap remains unmirrored. All crop pixels are fully opaque. No cast-shadow
pixels or new artwork are introduced. The original **CC-BY-SA-3.0** license and both cmss13 attribution URLs
above apply to `CMU3DPlatformHybrisaTwo*.png` under `/Textures/CMU14/ThreeD/Surfaces/` (atlas IDs 1040–1046).

This remains a static draft. The correction does not complete its destruction/construction states or approve
the inferred dimensions. Source comparison is in `generated/review/filtration-platform/`.
