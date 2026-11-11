# Broadleaf reconstruction checkpoint

Tree geometry is unchanged by the later floor-paper batch. Library totals and validation below describe the canopy checkpoint; `STATUS.md` records current overall totals.

The twelve canonical drafts now include the **installed crown-proportion refinement** in `Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_CANOPY_FIT.md`. Seventy-six groups fit original silhouettes at 32 pixels per tile and an inferred .55-radian viewing pitch. Full existing Y depth is retained. Capped cluster stretching and 76 foliage-core replacements remove exposed smooth masses without introducing blade-like leaves. Visible source-traced wood, all per-part colors, pivots, facing and random-state bindings remain unchanged. Only inferred internal supports reconnect to the moved crowns.

All twelve source/four-view cards and two fixed-scale before/after examples were inspected in `generated/review/canopy-fit-final/`; main library cards and overview tiles are updated. Actual mesh profiles improve mean closed-mask Dice .6492 to .7920 and green IoU .3798 to .4850. These measure an assumed source projection, not final fidelity or hidden depth. Several large variants still have overly open crown gaps; bark seams, roots, vines, side anatomy and wind remain unfinished.

Saved scene bytes and 1,658 unrelated definitions are unchanged. Conservative whole-model structural pairs fall 267 to 252. The four new broad-bound shutter flags have zero individual-part bound overlaps. Tree #10963 remains clear of bench #49173. Existing tree/wall contacts are not certified clear. Earlier containment and contact counts below are historical snapshots, not claims about current occupied volume.

Validation: 130 Python checks, the post-install native library-budget check, all 724 deterministic exports and all 724 individual / 63 assembled GLBs pass; glTF errors/warnings are zero. Eleven saved regions and the selected-state fixture are refreshed. Interactive browser review covers the twelve-model fixture and installed shutter/bench contexts at multiple angles, with no console errors/warnings. Native/browser renderer code is unchanged this pass, so earlier build, 119 native, 25 Node and GPU-parity results remain prior evidence. Native interactive review and performance profiling remain undone.

Library: 724 drafts / 25,820 parts / 2,317,132 triangles; 429 classic types remain unmapped. Normal gameplay rendering is unchanged. The goal remains active. Next work includes remaining mapped-art fidelity and the unmapped queue; no model is approved merely because a source projection improved.

Authoring snapshots `.codex/fit_tree_canopies.py` and `.codex/apply_canopy_fit.py` have already been applied. Do not force these or earlier scripts over later edits. Reports are `generated/canopy-fit-*.json`.

# Previous wood refinement checkpoint

The following section records the prior wood pass. Its descriptions of preserved crowns, containment and contact totals apply to that snapshot only.

The twelve canonical drafts now include the **installed wood refinement** in `SOURCES_TREE_WOOD.md`. They preserve the prior canopy parts and replace stepped wood with 554 locally tilted cylindrical stems plus 40 small vine-leaf sprays. The source paths and colors are retained in `generated/tree-wood-source-audit.json`; `.codex/author_tree_wood.py` is a guarded one-time authoring script and must not be forced over later edits.

Current implementation adds per-part `yaw` and `pitch`, centered locally and then composed with entity yaw, to Python/review/glTF, browser meshes and native CPU/GPU picking. Pitch uses otherwise unused untextured metadata bits; ordinary atlas metadata remains unchanged. Textured tilt and rotated connected/wall-mounted parts are explicitly unsupported. Scene bounds and surface-prop bottom heights account for rotation.

Validation: 128 Python / 25 Node / 119 native checks; post-install library budget passes. Client build has zero errors / 2,150 existing warnings. All 724 deterministic GLBs, 724 individual glTF validations and 63 assembled validations pass without glTF errors/warnings. Eleven affected map regions and the synthetic selected-state fixture are rebuilt. Both tested native shader variants compile and 5,408 tilt samples agree with CPU picking. All twelve source/four-view cards were inspected; no interactive browser/native review or performance profiling this pass.

Placement: original scene bytes and all 1,658 unrelated definitions remain unchanged. The new vine/bench contact at #10963/#49173 is cleared by changing inferred depth while retaining its source projection. There are no new structural entity pairs, but 91 confirmed cylinder/box intersections remain in eight previously flagged tree/structure pairs. These are unresolved, not approved. The earlier foliage containment proof covers only the preserved leaf parts, not new wood.

**Next art work:** correct canopy proportions and bare tier gaps (especially small 03/06), improve bark taper/seams and root/vine curvature, and address tree/wall context and raised terrain. The source colors are verified, but lighting/material response and hidden depth remain inferred. All 724 assets remain drafts and 429 classic types remain unmapped; normal gameplay rendering is unchanged. The goal remains active.

The following checkpoints are historical. Never apply `.codex/refine_broadleaf_trees.py --apply` or force the earlier authoring scripts over current canonical assets.

# Previous foliage refinement checkpoint

The canonical twelve tree drafts now include the **installed foliage refinement** described in `SOURCES_BROADLEAF_SPRAYS.md`. It replaces 718 existing ellipsoids with irregular open sprays (19 leaves each), samples the original green palettes and reduces shaded cores. Model count and part count stay unchanged. The all-new source-volume candidates described below remain rejected and uninstalled.

Current implementation:

- `Tools/three_d/foliage.py`: deterministic closed leaflet meshes, each analytically contained within the former parent ellipsoid. `generate_foliage.py` maintains browser/native transform tables. Python/export, browser geometry/picking and native CPU/GPU solids/normals support `Foliage` (enum 11).
- `.codex/author_broadleaf_sprays.py`: authoring from the preserved original baseline. Its `--apply` guard deliberately rejects replay over the now-refined canonical definitions. Do not force that guard or rebaseline over later work.
- `.codex/audit_broadleaf_sprays.py`: verifies source colors, all unchanged non-art metadata, all unrelated definitions, unchanged scene bytes and the full-surface containment proof. Reports: `generated/foliage-verification.json`, `broadleaf-spray-containment.json`, `broadleaf-spray-source-audit.json`.
- `generated/review/broadleaf-sprays/`: current twelve source/four-view cards and four montages. The per-model cards in the main review directory are also refreshed. `broadleaf-irregular-crowns.png` is an earlier two-model experiment and is not the final current evidence.
- All 724 deterministic GLBs, 724 individual and 63 assembled glTF checks pass with zero errors/warnings. Eleven affected Garrison regions plus the synthetic selected-state fixture are refreshed. 123 Python / 23 Node / 106 isolated native checks pass. The actual shader compiles both tested native GL variants; 1,014 foliage pixel samples agree with CPU picking. Browser/export geometry agrees over all 16,416 values. Build has zero errors / 2,150 existing warnings.
- No interactive native review or frame-time profiling was done. The in-app browser could not attach a hidden tab; opening the normal reviewer is queued for when this task is visible. Do not claim an interactive browser review this pass.

**Next art work:** rebuild source-specific twisting trunks, exposed roots, branches and hanging vines, while retaining saved pivots and source-facing rules. The source/four-view cards reveal stepped box construction and some overly separated crown tiers, especially small 03/06. Foliage detail is improved, but silhouette proportions and inferred hidden depth remain unfinished. Existing contacts and the raised-soil questions also remain unresolved. No model has final art approval and the standard gameplay viewport is unchanged. Coverage remains 724 drafts / 429 unmapped classic types; the goal remains active.

The following section records the previous rejected investigation. Claims there that canonical definitions were unchanged apply to that earlier experiment only. **Never apply `.codex/refine_broadleaf_trees.py --apply` over the new foliage refinement.**

# Historical whole-tree reconstruction experiments

The twelve current broadleaf models remain canonical. The replacement candidates in `generated/broadleaf-candidates.yml` are **not accepted or installed**. They cover the same twelve fixed RMC variants / 131 classic placements and preserve the conditional RandomSprite bindings added earlier.

Source inspection found incorrect large smooth crowns, generic trunk/root construction and guessed color swatches. All twelve sprites have now been segmented and sampled directly. Candidate geometry has 128 parts per tree / 1,536 total; colors come from original opaque pixels. `generated/broadleaf-trace-audit.json` retains the source samples, branch paths and inferred depth. No gameplay/map coordinates are edited.

Several tested constructions were rejected:

- Distributing small source-profile ellipsoids at unrelated depths produced floating beads and insufficient connected foliage.
- Extruding those profile groups coherently improved front outlines but produced flattened crowns and stacked plates in side views.
- Clustering a sampled 3D volume restored depth but retained excessively smooth lumps. A stronger foliage mask was necessary because gray-brown trunk/branch pixels were being mistaken for leaves.
- A scratch eight-leaflet geometry experiment adds smaller tapered silhouettes. It still produces repeated patterns and does not adequately preserve the source's crown gaps. This geometry exists only in `.codex/prototype_tree_leaflets.py`; **no new native/browser shape has been added**.

The current 2D silhouette metric is not an acceptance test. It excludes the lowest root region and cannot establish correct depth, branching or a faithful rotating view. Its report is `generated/broadleaf-profile-audit.json`. The first root-height attempt incorrectly used frame-center plus Sprite offset as physical height; current comparisons instead align the lowest opaque root pixel, with physical height still explicitly inferred.

Placement checks also prevent promotion. Conservative candidate contacts fall from 267 to 179, but two new pairs occur at tree #10957 against window shutters #15119 and #15133. Exact convex checks find six leaf-part/shutter intersections across those pairs. The evidence is in `generated/broadleaf-placement-audit.json` and `generated/broadleaf-new-contact-check.json`. Existing contacts have not been certified clear. Moving the saved tree or suppressing the contacts would not resolve this fidelity problem.

Continue by preserving the major crown groups and their branching in full volume, refining small foliage geometry, and resolving the tree/window relationship using consistent physical height/depth. Before adding a new primitive, establish that its front, oblique and side appearance is actually better. Any accepted shape must then be implemented consistently in Python export/comparison meshes, browser geometry/picking and native CPU/GPU intersection/normal handling, with appropriate validation.

Reproducible scratch tools:

- `.codex/refine_broadleaf_trees.py`: creates candidates and source trace; `--apply` exists but has **not** been used. Do not apply the current candidates.
- `.codex/review_broadleaf_trees.py`: twelve comparison cards, or two representative candidates with `--sample`.
- `.codex/profile_broadleaf_trees.py`: projection diagnostics; does not certify fidelity.
- `.codex/audit_broadleaf_placement.py`: conservative contacts for the current candidates.
- `.codex/check_broadleaf_new_contacts.py`: exact checks for their newly introduced window contacts.
- `.codex/prototype_tree_leaflets.py`: experimental leaflet mesh rendering for two representatives only.

The final experiment comparison is `generated/review/broadleaf-leaflet-experiment.png`. Earlier `.codex/broadleaf-review/cards-1.png` through `cards-3.png` belong to the rejected first iteration; they are not current-candidate evidence. `.codex/broadleaf-review/cards-0.png` currently contains two representative solid-volume candidates. No native or export validation from the previous completed state-selection pass should be represented as validation of these unfinished replacements.

All 1,670 canonical model/material definitions and the saved scene are byte/definition-equivalent to the pre-experiment baseline. Library coverage remains 724 drafts and 429 unmapped classic visual types. The goal remains active.
