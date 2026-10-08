# Direction-specific floor papers

Twenty draft assemblies cover `DecorFloorPaper`, `DecorFloorPaper1`, `DecorFloorPaper2` and `DecorFloorPaper3`: 23 saved classic placements and 61 across configured maps. Scattered paper has eight source directions; the other three have four each. The frames change visible page arrangements and sometimes page counts, so each has its own authored geometry instead of rotating one generic pile. All remain drafts.

## Source and geometry

Original art: `Content.CMU/Resources/Textures/CMU14/N14content/world.rsi`, states `scattered_papers`, `papers_1`, `papers_2` and `papers_3`. The authoritative RSI metadata specifies **CC-BY-NC-SA-3.0**, taken from mojave-sun-13 at [commit ffcecc82f28c796f8eff92ac46ff0f5e0d9b1ab6](https://github.com/Mojave-Sun/mojave-sun-13/blob/ffcecc82f28c796f8eff92ac46ff0f5e0d9b1ab6/mojave/icons/structure/miscellaneous.dmi). Retain this attribution and the original metadata with derived assets.

Forty-one disconnected opaque patches become thin textured volumes. Their crops preserve all 5,870 opaque source pixels, including colored scraps, printed markings and the spaces between patches. Crops are in `Content.CMU/Resources/Textures/CMU14/ThreeD/FloorPapers/`; prototypes and surfaces are in `Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_floor_papers.yml`. Surface slots 485–525 extend the atlas to 525 images / 4096 by 576 pixels / 9 MiB. Twenty more models use original surfaces, bringing that count to 188.

Ground XY comes from the source pixel footprint at 32 pixels per tile and the actual centered 32-by-48 frame. It is not an upright sprite silhouette projected into ground depth. Source padding, saved positions and rotations are retained. Thickness of .003/.006 tiles and the .002 floor clearance are inferred. Touching source pages remain grouped patches; individual curled pages, crumpled volume, hidden undersides and layered stack construction are unfinished.

## Direction selection

`directionalModels` lists assemblies in RSI order: South, North, East, West, SouthEast, SouthWest, NorthEast, NorthWest. Four-direction families use the first four slots. `referenceDirection` fixes each assembly's source comparison to its actual RSI slot. Reciprocal family/source references are validated; absent or inconsistent poses remain unsupported instead of borrowing another arrangement.

Native live and offline saved-map selection use the entity angle before layout, independent of the free camera. Each authored arrangement cancels that selected cardinal/diagonal turn through its explicit yaw correction; camera orbit does not switch poses. Exact/inherited provenance remains intact. Only four base source mappings are counted; the other sixteen assemblies do not inflate prototype coverage. The native inspector and GLB metadata retain the selected direction. Gameplay transforms and the normal gameplay viewport are unchanged.

## Context and remaining contacts

All 1,670 earlier model/material definitions, all saved transforms, floors and 46,538 unrelated visible scene records are unchanged. Sixteen additional presentation corrections bring adjusted facings to 619; other layout/support counts stay unchanged. Every new classic placement appears in at least one assembled export. One existing region is refreshed, six saved regions are added, and a synthetic twenty-pose fixture brings assembled exports to 70.

Individual bounds identify sixteen directed contacts. Tests of opaque texel prisms against oriented boxes clear the two platform flags (#7169/#12611 and #7170/#12616) and one bedroll flag (#7166/#940). Thirteen directed contacts remain, representing eleven unique entity pairs: four walls, two pallet runners, two bedrolls, one rubbish object and two duplicate paper-pile pairs. The duplicates are already saved at identical pivots. These buried/overlapping portions are recorded, not certified clear, and no placement or terrain elevation was invented to hide them. Source floor art draws below structures; that explains visible occlusion but does not prove physical intersections acceptable.

All twenty source/four-view cards were inspected in `Tools/three_d/generated/review/floor-papers/`; main library cards and overview tiles are updated. Interactive browser review covered camp, room, loading/platform, industrial-wall and refuse-pile contexts plus all twenty synthetic poses from top and angled views. An initial fixture JSON/report filename collision was found and fixed; the corrected fixture loads twenty objects with no missing models. No new browser errors were observed after that fix. Native interactive and performance review remain unperformed.

## Verification

The client builds with zero errors and 2,150 existing warnings. All 125 isolated native checks pass. The full 135-check Python suite passes, followed by six focused coverage checks including one additional regression (136 distinct passing tests). Every one of the 744 deterministic model exports, 744 individual GLBs and 70 assembled GLBs passes; glTF errors/warnings are zero. Native shader and browser geometry code are unchanged.

Evidence under `Tools/three_d/generated/`: `floor-papers-source-audit.json`, `floor-papers-verification.json`, `floor-papers-opaque-contacts.json`, `floor-papers-export-review.json` and the two glTF validation reports. `floor-paper-directions-fixture.json` is the scene; its export report is separately named `floor-paper-directions-fixture-export.json`. The fixture is synthetic and does not change Garrison's placement.

Current totals are 744 drafts / 25,861 parts / 2,317,624 triangles. Classic coverage has 724 exact draft types and 425 unmapped types. No model is promoted to reviewed art. Brick rubble, cardboard, scrap wood, skeletons, the off-map book pile, remaining asset families and full gameplay integration still need work.
