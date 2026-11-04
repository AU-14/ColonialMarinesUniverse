# Solid floor decoration — draft source record

The eighteen models in `garrison_solid_floor_decor.yml` derive from the `brickrubble`, `cardboard` and `woodscrap` states of `Content.CMU/Resources/Textures/CMU14/N14content/world.rsi`. Original metadata attributes the art to Mojave-Sun at commit `ffcecc82f28c796f8eff92ac46ff0f5e0d9b1ab6`, file `mojave/icons/structure/miscellaneous.dmi`. Retain the original **CC-BY-NC-SA-3.0** license and attribution with these derived assets. The source metadata remains authoritative.

Each state has eight RSI direction slots in a three-column sheet of 32-by-48 frames. Slots S, N, E, W, SE and SW contain different arrangements. NE and NW are transparent. Six assemblies per state use reciprocal `directionalModels`; empty final slots remain unsupported. Only each family's base model maps its prototype. Camera orbit does not select or rotate the source arrangement. All twelve classic placements use populated cardinal slots; combined configured maps contain 36 instances of the three types.

The assemblies contain 469 solid parts: individual fractured brick sections, separate wood planks with grain marks and asymmetric break ends, and hollow cartons with distinct walls, recessed bottoms, closed lids or inclined open flaps. Every one of 364 recorded palette samples matches an opaque original source pixel. No new textures are introduced. Source landmarks determine piece count, relative positions and axes; height, ground-depth decomposition, back faces, stacking and wall thickness remain inferred. A padded sprite canvas is not treated as a physical floor-depth offset.

The first comparisons exposed overly dark carton bodies and rigid flaps. Refinement resampled local source colors, inclined the open flaps and added fold creases and wood fracture teeth. The West rubble's first brick is moved 0.04 tiles in inferred local depth to clear a wreck tire at #7117. Its source X, piece scale and color are retained; the source-view vertical difference under the authoring projection is 0.896 pixels. Exact capped-cylinder/box checks clear both original tire contacts. All saved map positions and yaws remain unchanged.

Cartons use the existing `placement: surface` contract. At #7128, the exact co-located pallet #7139 supports them at 0.15 tiles, giving a render offset of 0.148. This clears all 42 prior carton/pallet part contacts. The other four classic piles have no authored support and remain at their floor datum. Three of those piles (#7125, #7126, #7127) occupy exactly the same saved positions as Kutjevo rock borders. Their 83 confirmed solid part contacts are unresolved. Source draw order and border/terrain representation need investigation before relocating or elevating them. These placements are not certified clear.

Validation for this batch: 136 Python checks and the native library-budget check pass. All 762 library models reproduce deterministically; 762 individual and 75 assembled GLBs pass Khronos validation with zero errors/warnings. All 1,731 earlier definitions, 46,549 unrelated visible scene records and floor tiles are unchanged. No renderer or gameplay code changed, so the prior client build was not repeated. These checks establish export/placement consistency, not fidelity approval.

All eighteen source/four-view cards and five saved browser contexts were inspected, along with a synthetic fixture of eighteen visible arrangements and six unsupported-direction markers. The fixture is not saved map placement. Native interactive review and frame-time profiling remain undone. Every model remains `draft`; carton deformation, fine break edges, wood stacking and inferred hidden construction need more work.

Evidence under `Tools/three_d/generated/`:

- `solid-floor-decor-source-audit.json`: source landmarks, per-pixel palette samples, depth and support refinements.
- `solid-floor-decor-verification.json`: unchanged definitions/transforms, direction checks, contacts and validation summary.
- `solid-floor-decor-initial-contacts.json`, `solid-floor-decor-initial-tire-contact.json` and `solid-floor-decor-tire-contact.json`: before/after contact evidence.
- `solid-floor-decor-export-review.json`: rebuilt supplies region and four added saved regions covering all twelve placements.
- `solid-floor-decor-directions-fixture.json`: synthetic scene; its export report is separately named `solid-floor-decor-directions-fixture-export.json`.
- `solid-floor-decor-glb-validation.json` and `solid-floor-decor-scene-glb-validation.json`: validator results.
- `review/solid-floor-decor/`: eighteen source/four-view comparisons and six family montages.

This is a batch checkpoint within the active full-modeling goal. The ordinary gameplay viewport has not been replaced, and no complete 3D reconstruction or production-ready gameplay claim is made.
