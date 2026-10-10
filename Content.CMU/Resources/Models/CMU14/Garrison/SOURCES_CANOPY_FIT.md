# Broadleaf crown proportions

This records the crown-refinement checkpoint. The later [floor-paper batch](SOURCES_FLOOR_PAPERS.md) increases the overall library counts; the tree geometry described here is unchanged by that batch.

The twelve broadleaf drafts now fit their original crown silhouettes at a fixed 32 pixels per tile, with an inferred .55-radian source-view pitch. Seventy-six crown groups are repositioned and resized in local X/Z while retaining their existing Y depth. Individual leaf-cluster stretching is capped; the former 76 smooth inner cores are replaced with the same closed foliage sprays used elsewhere. This retains volume without the exposed smooth blobs or excessively elongated leaves found in earlier candidates.

Source attribution remains [SOURCES_BROADLEAF_SPRAYS.md](SOURCES_BROADLEAF_SPRAYS.md), including both original RSI metadata files and their CC-BY-SA-3.0 credits. Retain that attribution with derived assets. Per-part colors, visible source-traced wood/vine paths, source pivots, facing rules and all twelve conditional RandomSprite bindings are unchanged. Only inferred hidden supports and crown attachments are reconnected to the moved groups. No reconstruction service was used.

## Comparison and placement

All twelve source/four-view cards were inspected in `Tools/three_d/generated/review/canopy-fit-final/`; the main library cards and overview tiles are refreshed. The `-aligned.png` comparisons retain common source scale and ground datum instead of fitting each image independently. Actual exported-mesh profiles improve mean closed-mask Dice from .6492 to .7920 and mean green IoU from .3798 to .4850. Every variant improves the first measure. These diagnose one assumed projection; they do not establish correct depth, anatomy or final fidelity.

Saved scene bytes, all 131 fixed classic placements, 266 combined placements and 1,658 unrelated definitions remain unchanged. Conservative whole-model structural pairs fall from 267 to 252. Four newly flagged pairs involve tree #10957 and shutters #15119/#15124/#15131/#15133. Checking individual part bounds finds no overlap in those pairs: the whole-model bounds include empty spaces. Tree #10963 also remains clear of bench #49173 at part-bound level. Existing tree/wall contacts are not certified clear; the previous wood pass's count of 91 confirmed contacts is historical, not a fresh total for this geometry. The earlier foliage-only containment proof does not apply to these moved crowns.

Interactive browser review covered all twelve candidates in a synthetic fixture, multiple angles at the installed shutter context and the installed tree/bench context. No browser errors or warnings were logged. This is not an exhaustive review of every placement. Native interactive review and frame-time profiling remain unperformed.

## Verification

- All 724 deterministic model exports and 724 individual / 63 assembled GLBs pass, with zero glTF errors or warnings. Eleven affected saved regions and the selected-state fixture were rebuilt.
- 130 Python checks and the post-install native library-budget check pass. The fixed-scale comparison renderer adds two projection/argument regressions. Native/browser renderer code is unchanged in this pass; the previous build, 119 native checks, 25 Node checks and GPU parity checks remain prior evidence.
- `canopy-fit-verification.json`, `canopy-fit-profile-audit.json`, `canopy-fit-placement-audit.json`, `canopy-fit-new-contacts.json` and `canopy-fit-bench-contact.json` record the checks under `Tools/three_d/generated/`.
- The library remains 724 drafts / 25,820 parts / 2,317,132 triangles. No art is marked reviewed and 429 classic visual types remain unmapped. Normal gameplay rendering is unchanged.

Canopy density, exposed gaps in large variants, bark taper/seams, fine roots and branches, side anatomy, existing structure contacts and wind still need refinement. `.codex/fit_tree_canopies.py` and `.codex/apply_canopy_fit.py` belong to this guarded authoring snapshot; do not force them over later edits. Earlier `review/canopy-fit/` smooth-core and stretched-leaf experiments are not final evidence.
