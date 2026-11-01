# Garrison fern and broad-leaf sprigs

Four source-specific draft assemblies / 350 parts cover six classic placements and fourteen across configured maps. Canonical definitions are `garrison_foliage_sprigs.yml`. None is approved art.

## Source interpretation and refinement

The original single-direction 32-by-32 states in `/Textures/Decals/Flora/flora_bushes.rsi` are bushf2, bushf3, bushg2 and bushg4. F variants have green curling stems and repeated downward leaf crowns. G variants have one woody base, forked branches and broader green leaves. Source paths, root groups and leaf regions are retained in `foliage-sprigs-trace-audit.json`.

Initial comparison exposed sparse, flat fern leaves and disconnected broad-leaf bases. The fern crowns now have six downward fronds each, including inferred front/back fronds, with petioles joining the source-traced stems. G foliage uses occupied original green regions with short connections to the nearest branch. A second comparison removed oversized light-relief disks and elongated the G leaves slightly. All part colors occur in the original source pixels. The source hashes and attribution are retained. No generated texture or new atlas slot is used.

All six saved placements have single-frame `noRot` and nonzero saved rotations. Physical yaw is zero, raising the scene's corrected facings to 580. Every saved transform and every unrelated scene record is unchanged. Added entity offsets are zero. Local roots center around the entity pivot; source root rows are not copied into global depth.

## Context and limitations

The conservative neighboring-part audit finds nine directed contacts: five shoreline bounds and four foliage entries (three distinct plant pairs). No walls or platform borders intersect the new sprig bounds. Shoreline boxes include transparent cutouts; these checks do not establish an exact water-surface collision. The two pre-existing stemmed-shrub/platform intersections remain separately recorded in `stemmed-shrubs-platform-audit.json` and were not changed by this batch.

Front/back construction, leaf thickness, depth and physical height remain inferred. Leaf surfaces, fine source shading, stem curvature and side-view fidelity need further refinement. Water depth, raised terrain, plant wind and interaction remain unfinished. These are solid-part drafts; the standard gameplay viewport is unchanged.

## Evidence and verification

- `foliage-sprigs-source-audit.json`, `foliage-sprigs-trace-audit.json`, `foliage-sprigs-palette-audit.json`, `foliage-sprigs-placement-audit.json` retain source evidence, palettes, roots, transforms and conservative contacts.
- All four source/four-view cards and three oblique models were inspected. Browser captures cover both fern placements and the broad-leaf shoreline group, with empty warning/error logs.
- The new `garrison-sprigs-east.glb` contains 61 objects / 72 floors and omits two unmapped/disabled objects. Existing affected exports were refreshed, including the shoreline and western planter contexts.
- All 703 deterministic individual GLBs and all 49 assembled GLBs pass Khronos validation with zero errors/warnings. The current library passes 99 isolated native checks. All palettes match their original source images.
- No renderer code changed in this batch. `grass-verification.json` remains the latest Python/Node/shader/client-build evidence. Connected gameplay and performance validation remain outstanding.

## License

New geometric work is CC0-1.0 to the extent separately licensable. Derivative appearance and source references retain CC-BY-SA-3.0.

Taken from tgstation at commits https://github.com/tgstation/tgstation/commit/729d858807905263adab8b5a331c1d8a04982dd3, https://github.com/tgstation/tgstation/commit/79296e902cbdf2352c9303e4769ea39bf3b34e58

## Source states

| Prototype | State | Parts | Classic objects | Combined objects |
| --- | --- | ---: | ---: | ---: |
| RMCBushf2 | bushf2 | 126 | 1 | 3 |
| RMCBushf3 | bushf3 | 108 | 1 | 2 |
| RMCBushg2 | bushg2 | 63 | 3 | 6 |
| RMCBushg4 | bushg4 | 53 | 1 | 3 |
