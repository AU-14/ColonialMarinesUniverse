# Broadleaf trunks, roots and hanging branches

This records the wood-refinement checkpoint. The subsequent [crown-proportion pass](SOURCES_CANOPY_FIT.md) moves foliage and reconnects inferred supports while preserving visible source-traced wood. Counts and contact results below describe this earlier snapshot; its preserved-foliage containment claim is not a claim about current crown placement.

The twelve broadleaf drafts retain the installed leaf canopies and replace their stepped wooden parts with 554 locally oriented round stems and 40 small vine-leaf sprays. Trunk bends, root paths and hanging branches follow inspected landmarks in each original sprite. Every new part uses a color sampled from that state. Hidden connections, depth, source viewing pitch and physical thickness remain inferred.

The source art and CC-BY-SA-3.0 attribution are recorded in [SOURCES_BROADLEAF_SPRAYS.md](SOURCES_BROADLEAF_SPRAYS.md), with the original RSI metadata authoritative. Preserve those attributions with these derived assets. No third-party reconstruction service was used.

## Orientation and placement

Parts rotate about their own centers: local pitch lifts +X toward +Z, then yaw rotates +X toward +Y. Entity rotation still rotates each part's center once. Browser meshes, comparison rendering, GLB nodes and native CPU/GPU picking use this convention. Native tilt has a separate packed metadata flag and cannot alias a texture slot; textured tilt and connected/wall-mounted rotated assemblies are rejected by the exporter.

All saved transforms, source-facing rules and conditional RandomSprite bindings remain unchanged: 131 fixed classic placements / 266 across configured maps. All 1,658 unrelated prototype/material definitions and the saved scene bytes are unchanged. The preserved foliage's earlier containment proof remains valid for those leaves; it does not cover the new wood.

The first candidate added two real vine/bench intersections at tree #10963 / bench #49173. Adjusting the inferred depth of one hanging path clears that pair while retaining its source-angle projection. The final new wood has 94 conservative structural part contacts in eight previously flagged tree/structure pairs; 91 cylinder/box intersections are confirmed by constrained quadratic minimization. These wall/border intersections remain unresolved. No new structural entity pair is introduced, but this is not a claim of unchanged occupied volume or cleared clipping. Curved foliage contacts remain conservative.

## Verification and review

`Tools/three_d/generated/tree-wood-source-audit.json` records sampled source pixels and landmarks. `tree-wood-verification.json`, `tree-wood-placement-audit.json` and `tree-wood-solid-contacts.json` record preservation and contact checks. Twelve source/four-view cards are in `generated/review/tree-wood/`; the corresponding main library cards are refreshed.

All 724 deterministic model exports, 724 individual GLBs and 63 assembled GLBs pass; glTF errors/warnings are zero. Eleven affected map regions and the synthetic selected-state fixture are rebuilt. The client builds with zero errors and 2,150 existing warnings. 128 Python, 25 Node and 119 isolated native checks pass, including the library budget after resource installation. Both tested native GL variants compile; 5,408 tilted-solid GPU samples agree with CPU picking. The shader test harness also corrects leading-dot PowerShell casts and rotates the cylinder test camera's up axis consistently.

Interactive browser/native review and frame-time profiling were not performed this pass. These remain drafts: crown proportions, bare gaps between tiers, bark taper and seams, finer root anatomy, hanging curvature, hidden depth, existing clipping and wind remain unfinished. The normal gameplay viewport is unchanged.
