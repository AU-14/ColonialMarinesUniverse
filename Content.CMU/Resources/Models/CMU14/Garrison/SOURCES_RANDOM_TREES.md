# Random tree state selection

Twelve existing broadleaf tree drafts now explicitly support the `random` layer of `FloraTree` and `FloraTreeLarge`. These bindings are conditional on the selected state; they are not unconditional `sourcePrototypes` mappings or approved art.

| Exact prototype | Source RSI under `Resources/Textures/` | States | Existing draft IDs |
| --- | --- | --- | --- |
| FloraTree | Objects/Decoration/Flora/flora_trees.rsi | tree01–tree06 | CMU3DBroadleafTree01–06 |
| FloraTreeLarge | Objects/Decoration/Flora/flora_treeslarge.rsi | treelarge01–treelarge06 | CMU3DBroadleafTreeLarge01–06 |

Both families use the same original RSI states, single-frame `noRot`, draw depth and sprite offsets as their corresponding fixed RMC tree prototypes. `generated/random-trees-source-audit.json` records effective components, all twelve PNG hashes and the seven raw saved entity records. The conditional bindings live in `garrison_environment.yml` and `garrison_outdoors.yml`; no geometry, part palette, pivot or static RMC mapping changes in this pass.

Retain the original RSI `meta.json` files and the attribution already documented in `SOURCES_ENVIRONMENT.md` and `SOURCES_OUTDOORS.md`. Both source RSIs declare CC-BY-SA-3.0. Their recorded tgstation commits are `e00cae8d065f9cf520688cc0dd0e15ba5bef12a9` (small trees) and `d388dee8b7b6d854f6f0d844988552acf5962b1f` (large trees). The original images are reused for comparison, not newly generated textures.

The native debug scene reads the existing replicated `RandomSpriteComponent.Selected` state before model facing and placement. The offline exporter reads the serialized `selected` tuple in either Robust-supported form: `[state, color]` or `{state: color}`. Only one explicit layer with null/white color is supported. Missing choices, unknown states, extra layers, nonwhite colors and ambiguous bindings do not borrow a default or inherited tree model.

Classic entities 7494–7500 do not serialize a selected random state. They remain unmapped and now display an explanatory amber marker; the viewer suppresses the misleading default tree01 reference. No saved position or rotation is changed. Static coverage therefore stays at 724 drafts, 720 exact classic types and 429 unmapped types. `coverage.json` lists twelve conditional state bindings separately.

`random-tree-state-fixture.json` and its GLB are explicitly synthetic review artifacts: all twelve known choices, plus unknown, colored and multilayer cases. They do not assert which trees were chosen in a live round or modify the saved map.

Visual review of all twelve source/four-view cards confirms that the existing drafts still have broad, smooth canopy clusters and simplified trunks. Fine branch structure, leaf density, crown silhouette and side-view detail need refinement. This pass fixes state selection; it does not certify tree fidelity, add wind animation, resolve raised-planter terrain or replace the normal gameplay viewport.
