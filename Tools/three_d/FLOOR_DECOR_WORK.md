# N14 floor-decoration subset: draft mappings installed

The skeleton/shoreline skeleton and book pile are now installed in `garrison_skeleton_books.yml`: three models / 258 parts, two classic placements / five combined. All ten types in this previously missing source subset now map explicitly, covering 37 classic / 102 combined instances. This is draft coverage, not completed fidelity or gameplay integration.

Source-traced skeletons use separate skull/jaw, ribs, vertebrae, pelvis and bent limbs. The `Over` variant is co-located with the same shallow-water corner on both map variants; its .014 datum clears the current .012 water top. That is a local art interpretation, not a generic draw-order height policy. The book pile is only on Redux level -1, #10425. Comparison corrected its two edge-on upright covers and lowered its rear book to ground height. Exact volume count, wear, hidden surfaces, skull anatomy and resting pose remain drafts.

All five saved placements have no nearby modeled part-bound overlaps. The 1,749 earlier definitions, all saved transforms/floors and unrelated scene records are unchanged. Current totals: 765 draft assemblies / 26,588 parts / 2,344,108 triangles; 420 classic types are still unmapped. Deterministic exports, 765 individual and 81 assembled GLBs, and the post-install native library-budget check pass. Renderer code is unchanged; the previous client build and Python suite were not rerun. See `SOURCES_SKELETON_BOOKS.md` and `generated/skeleton-books-*.json`. Three source/four-view cards and three saved browser contexts were inspected; native interactive and frame-time review remain undone.

Earlier paper contacts and three co-located carton/rock-border pairs remain unresolved. The guarded author/refinement/finalizer scripts for this batch have already been applied; do not replay them over current assets.

Continue the full mapping/fidelity backlog. The soda and CMB-equipment vendor follow-up is now installed (see SOURCES_VENDORS.md). Current prominent missing static furnishings include prison carpets (20 visible classic placements); the carpets require floor placement and adjacency/context review. Fog (186 markers) is an effect and should not be replaced by generic solid blocks. Mannequins (9) need their worn clothing layers, not just bare stands. Historical source-family checkpoints follow.

# Previous solid-debris batch (historical)

Eighteen direction-specific assemblies / 469 parts are installed in `garrison_solid_floor_decor.yml`: six visible arrangements each for brick rubble, cardboard and scrap wood. They cover three prototypes / twelve classic placements / 36 combined. Original 32-by-48 RSI frames have eight slots, but slots six and seven are transparent. Those slots remain unsupported markers; no invisible-entity policy is claimed. The existing native/offline direction selectors are reused unchanged.

All 364 palette samples match the source. Hollow cartons use inclined flaps, packing creases and the existing support contract; #7128 rests on pallet #7139 at 0.15 tiles. A 0.04-tile change to one West-frame brick's inferred depth clears the two confirmed tire contacts at #7117, preserving source X and saved map transforms. Three cartons (#7125, #7126, #7127) share exact saved positions with rock borders; 83 solid part contacts remain. Investigate source draw order, border/terrain representation and saved context before inventing elevated or relocated cartons.

Current totals: 762 drafts / 26,330 parts / 2,323,252 triangles; 422 classic types remain unmapped. All 136 Python checks, the native library-budget check, 762 deterministic model exports, 762 individual and 75 assembled glTF checks pass. No renderer code changed; the client build was not repeated. Eighteen source/four-view cards, five saved browser contexts and the full synthetic fixture were inspected. Native interactive and performance review remain undone. Evidence: `SOURCES_SOLID_FLOOR_DECOR.md`, `generated/solid-floor-decor-*.json`, `generated/review/solid-floor-decor/`.

Next source-family assets: skeleton/skeleton-over (two classic placements / four combined) and the redux-only book pile. Build real separated bones, preserving both uses' source offsets and draw context; do not reduce the skeleton to a decal. The wider mapping backlog and draft-fidelity work remain active. Wood stacking, carton deformation, fine broken edges and the three border contacts are still unfinished.

The author/refinement scripts for this batch are guarded one-time snapshots already applied. Do not force or replay them over current assets. Historical checkpoints follow.

# Previous paper batch (historical)

Twenty direction-specific paper assemblies are installed in `garrison_floor_papers.yml`, covering four prototypes / 23 classic placements / 61 combined. Geometry is 41 thin original-art patches; all 5,870 opaque pixels and source frame pivots are preserved. Direction selection is implemented and tested in native/offline code, with `referenceDirection` comparisons and reciprocal `directionalModels` in RSI order. Camera rotation does not change the selected arrangement. Main comparisons, source attribution, deterministic models and all affected scene exports are refreshed.

Current totals: 744 drafts / 25,861 parts / 2,317,624 triangles; 425 classic types remain unmapped. Build has zero errors / 2,150 existing warnings. 125 native checks, 135 full Python checks plus the new focused coverage regression (136 distinct Python tests), all 744 individual and 70 assembled glTF checks pass. Browser review covers five saved contexts and all twenty synthetic poses. Native interactive and performance review remain undone. Detailed evidence is `SOURCES_FLOOR_PAPERS.md` beside the assets and `generated/floor-papers-*.json`.

The synthetic scene is `generated/floor-paper-directions-fixture.json`; its GLB report is `floor-paper-directions-fixture-export.json`. Do not overwrite the scene with the report. An initial collision was repaired during browser verification.

Paper limitations remain: touching sheets share a patch; curls, crumpled shapes, hidden layers and thickness need refinement. Thirteen directed opaque contacts / eleven unique entity pairs remain, including walls, pallets, bedrolls, a rubbish object and two deliberately co-located paper pairs. Three conservative bound flags were cleared. Do not claim all placements are collision-free or invent paper-only elevation to hide the raised-terrain issue.

Next asset work: brick rubble (6 classic placements), cardboard (5), scrap wood (1), skeleton/skeleton-over (2) and the redux-only book pile (1). These are fourteen remaining classic placements in this ten-type source family. Inspect their directional frames carefully: several eight-direction sheets have transparent later frames, so do not fabricate missing appearances. The skeleton is not a flat decal; it needs separate bones and inferred body depth. Paper fidelity/context remains an ongoing part of the full goal.

The following source checkpoint is historical. `.codex/author_floor_papers.py` has already been applied and is guarded against replay over later edits.

# Original source checkpoint

This is the next source/context investigation after the installed broadleaf crown refinement. No floor-decor geometry or mappings have been added at this checkpoint. Current totals remain 724 drafts and 429 unmapped classic types.

`generated/remaining-floor-decor-source-review.json` records ten prototypes / 102 configured-map instances, including 37 visible classic placements. The family comprises a book pile, brick rubble, cardboard, four paper arrangements, scrap wood and two uses of the skeleton sprite. The book pile has no classic placement. Attribution is CC-BY-NC-SA-3.0 from the original `Content.CMU/Resources/Textures/CMU14/N14content/world.rsi/meta.json`; retain it with derived assets.

`generated/review/remaining-floor-decor-sources.png` shows the first source frame for each type. Seven `DecorFloor*-source-directions.png` sheets retain all directional frames. The original PNGs are packed RSI sheets, not single large objects: frame size is 32 by 48 pixels. Brick rubble, cardboard, scattered paper and scrap wood have eight directions; paper 1/2/3 have four. Book pile and skeleton have one. States have no animation delay arrays. The first attempted scratch overview incorrectly displayed whole sheets; it is replaced by the frame-correct version.

The inspected scattered-paper frames contain distinct arrangements, not merely rotated views of the same pile. These inherited sprites also set `noRot: true`; that flag must not discard directional-frame selection. Saved classic placements use the four cardinal yaws, with the complete counts and original transforms retained in the JSON. Do not author one generic pile and assume arbitrary rotations match all source directions. Inspect every used frame, current selector/orientation behavior and saved context before choosing explicit directional geometry or an equivalent supported representation. Keep the sprites' padded 48-pixel frame and physical ground datum separate.

Next work: inspect all seven direction sheets and the relevant map locations, resolve how direction-specific arrangements select their model without changing saved gameplay transforms, then author source-matched parts. Paper should have thin sheets and readable original markings; rubble/cardboard/wood need distinct pieces and actual depth. Skeleton geometry needs separate bone anatomy. Compare front/oblique/side views, check neighboring placement and export validation before increasing coverage. No current evidence certifies these missing assets.
