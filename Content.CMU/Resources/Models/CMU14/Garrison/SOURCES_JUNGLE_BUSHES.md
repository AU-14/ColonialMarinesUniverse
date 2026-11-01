# Garrison large jungle bushes

Three source-specific drafts / 328 parts cover twelve Classic Garrison placements and 38 across configured maps. Canonical definitions are `garrison_jungle_bushes.yml`; successive scratch authoring scripts are not regeneration sources. No model is approved art.

## Source and geometry

The original 64-by-64 single-frame `bush1`, `bush2` and `bush3` states are in `/Textures/_RMC14/Structures/Flora/bush.rsi`. They have distinct curling leaves and central stalk groups. The reconstruction traces 15, 16 and 14 visible leaf paths, plus three stalk paths per model. Four occupied inner foliage regions per model supply inferred front/back leaves.

Comparison rejected early rounded segments, detached midrib overlays and overly broad sideways leaf spreads. Near-collinear source paths now form continuous blades; curved sections join at overlapping interior points. Narrow segmented basal stalks connect each leaf to the common root without producing false broad leaves. Tips stay inside the opaque source top/side bounds. Source-pixel sampling and palette validation cover 193 recorded samples and all 328 part colors. No new textures are used; the 484-image atlas is unchanged.

The aligned XZ silhouette diagnostic yields intersection-over-union values of .7211, .6630 and .6446. It maps authored geometry back to original pixel scale around each manually traced root. It exposes missing and excess silhouette regions; it does not prove complete fidelity or recover hidden geometry. Side views, fine curvature, lamina thickness, density, veining and source shading still need refinement.

## Facing, offset and placement

All three source sprites use `noRot`, Overdoors draw depth and offset `0.1, 0.35`. The horizontal .1-tile offset is retained as `groundOffset: 0.1, 0`; the vertical sprite offset is not copied into map Y. Vertical geometry is anchored around the inspected root rows. Actual physical height/depth remains inferred.

All twelve raw saved objects have Transform and empty Fixtures overrides, with no saved Sprite/state override. Their saved positions/yaws remain unchanged. Four saved nonzero yaws are ignored for the one-frame source facing; scene adjusted facings total 603. All 721 pre-existing model definitions and every unrelated scene record are unchanged. Support, wall mounting, table connectivity and door counts are unchanged.

The initial exact surface check found seven leaf/wall intersections: five at #9827/#16713 and two at #9832/#16712. Five leaves now use different inferred depth planes, retaining their source XZ curves; their basal stalks reconnect to unchanged roots. The final neighbor audit has no wall, platform, stair or machine contacts. Fourteen directed conservative foliage contacts remain, involving nearby grasses, bushes and trees; these bounds contacts are not blanket claims of exact plant-surface intersection or clearance.

Older D2 shrub/border and M1 conifer/border intersections remain unresolved. `planter-elevation-source-review.json` records that the platform explicitly describes a raised area, while the inspected platform/soil definitions supply no physical soil height. `planter-elevation-followup.json` retains the bounded soil patches. No terrain height or individual plant elevation was changed.

## Verification and artifacts

- `jungle-bushes-source-audit.json`, `jungle-bushes-trace-audit.json`, `jungle-bushes-sample-audit.json`, `jungle-bushes-palette-audit.json` and `jungle-bushes-profile-audit.json` retain source hashes, metadata, paths, samples, successive refinements and outline diagnostics.
- `jungle-bushes-initial-placement-audit.json` / `jungle-bushes-initial-structure-audit.json` preserve the two wall-contact cases; the final `jungle-bushes-placement-audit.json` / `jungle-bushes-structure-audit.json` record their clearance and all twelve placements.
- All three source/four-view cards and all three oblique models were inspected. Six browser captures cover both wall contacts, open northern terrain, a rotated view, the dense riverside grouping and the southern planter. Browser warning/error logs are empty.
- Four new `garrison-jungle-bushes-*` regions join the refreshed actual scene exports, bringing assembled exports to 62 including the isolated pose fixture. Unmapped/inherited omissions remain listed in each region report.
- All 724 deterministic model exports match regeneration. Khronos validation passes 724 individual and 62 assembled GLBs with zero errors/warnings. All 99 isolated native checks pass with the final canonical geometry.
- No renderer code changed. Previous Python/Node/GPU/client-build evidence remains historical. Connected gameplay, dynamic appearances and final performance remain unverified; the standard gameplay viewport is unchanged.

## License

New geometric work is CC0-1.0 to the extent separately licensable. Derivative appearance and original source references retain CC-BY-SA-3.0.

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/structures/props/natural/vegetation/colorable_junge_bush.dmi

## Source states

| Prototype | State | Traced leaves | Parts | Classic objects | Combined objects |
| --- | --- | ---: | ---: | ---: | ---: |
| RMCBushJungle1 | bush1 | 15 | 107 | 3 | 10 |
| RMCBushJungle2 | bush2 | 16 | 113 | 5 | 16 |
| RMCBushJungle3 | bush3 | 14 | 108 | 4 | 12 |
