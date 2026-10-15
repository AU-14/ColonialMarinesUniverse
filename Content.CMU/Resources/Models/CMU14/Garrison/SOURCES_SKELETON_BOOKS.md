# Skeletal remains and scattered books — draft source record

The three assemblies in `garrison_skeleton_books.yml` derive from `skeleton` and `bookpile_1` in `Content.CMU/Resources/Textures/CMU14/N14content/world.rsi`. Original metadata attributes the art to Mojave-Sun, commit `ffcecc82f28c796f8eff92ac46ff0f5e0d9b1ab6`, file `mojave/icons/structure/miscellaneous.dmi`. Retain its **CC-BY-NC-SA-3.0** license and attribution with these derived assets. Both states contain one 32-by-48 frame; their inherited `noRot: true` keeps the source-facing pose independent of entity and camera rotation.

The ground and shoreline skeletons each contain 104 parts: a side-turned cranium, recessed eye/nasal regions, jaw pieces, vertebrae, separate curved ribs, a pelvic ring and the source's asymmetric bent limbs. They share geometry apart from floor clearance. Bone axes follow recorded source landmarks, with closed cylinders and joint ellipsoids supplying actual depth. Radii, hidden anatomy, skull construction and the physical resting pose are inferred and remain drafts. The visible teeth/socket shape and smooth joints still need fidelity work.

`DecorFloorSkeleton` has a .004-tile ground datum. `DecorFloorSkeletonOver` differs in the source only by `drawdepth: Mobs`; both its configured placements share a position with `RMCEntityDesertWaterShallowCornerEdge`. Its .014-tile datum clears the existing authored water top of .012. This is a local shoreline interpretation, not a general conversion of sprite draw order into physical elevation. Saved transforms are unchanged and no gameplay physics or depth rule is modified.

The 50-part book pile represents eight bound volumes with separate covers, page blocks, spines and page-edge marks. Its initial upright books faced edge-on. Refinement turns both covers toward the source view, lowers the rear book to the floor, compensates inferred depth for that height change, and adds its inset cover panel. Exact volume count, material wear, stacking, cover detail and hidden surfaces remain inferred. The only configured book-pile placement is on Redux level -1, near a bedroll; it is absent from classic Garrison.

All 232 recorded palette samples match opaque original pixels. No textures or atlas changes were added. Ground compositions are recentered independently of the padded source canvas, with uniform scaling preserving tilted-part proportions. The source projection and normalization are recorded; a source reference does not establish exact physical dimensions.

All five saved placements were audited across classic, Redux surface and Redux level -1. No new part-bound overlaps with nearby modeled entities were found, including water. This excludes unmapped neighbors and does not certify gameplay collision geometry. All saved transforms, floor tiles, 1,749 prior definitions and unrelated scene records remain unchanged: 46,559 classic, 58,936 Redux surface and 24,497 Redux lower-level records.

All 765 library models reproduce deterministically. Khronos validation passes for 765 individual and 81 assembled GLBs with zero errors/warnings. The native library-budget check passes after installation. There are no renderer/gameplay code changes; the previous client build and 136-check Python suite were not repeated as new claims. Three source/four-view cards and saved mine, shoreline and book-pile browser contexts were inspected. Native interactive and frame-time review remain undone.

Evidence under `Tools/three_d/generated/`:

- `skeleton-books-source-audit.json`: source landmarks, samples, normalization and book orientation refinement.
- `skeleton-books-verification.json`: palette, unchanged-state and placement checks with validation summary.
- `skeleton-books-export-review.json`: five added saved-map regions and the three-model synthetic fixture.
- `skeleton-books-redux-scene.json`, `skeleton-books-redux-below-scene.json`: current saved Redux contexts; corresponding baseline files preserve the previous scene state.
- `skeleton-books-fixture.json`: isolated scene; `skeleton-books-fixture-export.json` is its separate export report.
- `skeleton-books-glb-validation.json`, `skeleton-books-scene-glb-validation.json`: validator reports.
- `review/skeleton-books/`: the three source/four-view cards.

All ten types in the previously missing N14 floor-decor subset now have explicit draft mappings, covering 37 classic placements / 102 combined instances. This is mapping coverage, not art approval. Earlier paper and rock-border contacts remain unresolved; the wider full-modeling goal and ordinary-gameplay integration remain unfinished.
