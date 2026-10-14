# Garrison upright, woody and low olive bushes

Nine source-specific draft assemblies / 879 parts cover 25 classic placements and 68 across configured maps. Canonical definitions are `garrison_low_bushes.yml`. No asset is approved art.

## Source interpretation and refinements

The original single-direction 32-by-32 states in `/Textures/Decals/Flora/flora_bushes.rsi` are A1–A3, B1–B3 and C1–C3. A variants have upright olive blades and separate tufts. B variants have short woody trunks, spreading leaf crowns and separate ground clusters. C variants have low drooping crowns. Each state retains its own inspected arrangement: 62 root groups and 17 woody crowns, plus visible blade/branch paths. `low-bushes-source-traces.png` marks those inspected roots, paths and motif rectangles.

Initial comparison showed overly uniform, flat satellite tufts. They were rebuilt as upright or spreading groups according to their source motif, with fuller woody crowns. Leaf-tip targets now follow occupied green pixels rather than rectangular motif corners. Nine samples that moved onto brown pixels during a ground-clearance adjustment were corrected; all 504 final tuft tip samples pass the original-green-pixel check. Further comparison caught unintended upright succulent-like blades in the C family and replaced them with descending fronds.

All part colors occur in the original art, whose decoded RGBA hashes are retained. No generated textures or new atlas images are used. The 484-image atlas remains unchanged. Exact pixels constrain palette and visible arrangement; they do not establish the correctness of inferred 3D anatomy.

## Orientation and placement

All sources use single-frame `noRot`, zero sprite offset and noncolliding static physics. The 25 saved objects contain only Transform overrides. All render with zero physical yaw, correctly ignoring eleven saved nonzero rotations; the scene's corrected facings total 591. Every saved position/yaw and every unrelated scene record is unchanged. There are no added entity ground offsets.

Root-row relationships place the separate groups around their shared pivot. Depth remains inferred; the original screen row is not copied into the entity's world position. The first neighbor audit found eight directed contacts, including one prison wall and five border objects. Source-pixel tip refinement and keeping inferred edge-canopy depth inside the source tile remove those six wall/border contacts without moving roots or entities. The final conservative audit retains two grass contacts involving bush #9757 with #11066 and #11086 (9 and 69 potential part pairs). These are bound contacts, not exact solid-intersection claims.

The seven previously confirmed solid intersections between older D shrubs and two platform borders remain unresolved and are not counted as fixed by this batch. A separate source inspection also found incorrect older Hybrisa platform construction; see `hybrisa-platform-source-followup.json`. It records two visible support feet and an open underside in the source, compared with the current three teeth and filled underside, across 834 classic placements. That platform geometry was not edited in this pass.

## Limitations

Physical height/depth, leaf thickness, hidden branching and side views remain inferred. Leaf curvature, fine source shading, crown density and final fidelity require further work. Terrain elevation, water depth, wind and dynamic interaction remain unfinished. All assets remain drafts, and the standard gameplay viewport is unchanged.

## Evidence and checks

- `low-bushes-source-audit.json`, `low-bushes-trace-audit.json`, `low-bushes-palette-audit.json` retain hashes, source components, roots, branches, leaf-tip samples and palette checks.
- `low-bushes-initial-placement-audit.json` and `low-bushes-placement-audit.json` retain the before/after contacts and unchanged-transform checks for every new placement.
- All nine source/four-view cards and three oblique models were inspected. Five final browser captures cover the prison wall, prison border, courtyard, southern bush and remaining grass overlap. Browser warning/error logs are empty.
- Three new `garrison-low-bushes-*` regions join the refreshed exports: prison (97 objects / 156 floors / 2 omissions), courtyard (67 / 342 / 6), and south (140 / 169 / 6).
- 99 isolated native checks pass with the current library. All 712 deterministic model exports and all 52 assembled exports pass Khronos validation with zero errors/warnings.
- Renderer code did not change. The preceding grass pass remains the latest Python/Node/shader/client-build evidence; those checks were not rerun or claimed as fresh here. Connected gameplay and performance validation remain outstanding.

## License

New geometric work is CC0-1.0 to the extent separately licensable. Derivative appearance and source references retain CC-BY-SA-3.0.

Taken from tgstation at commits https://github.com/tgstation/tgstation/commit/729d858807905263adab8b5a331c1d8a04982dd3, https://github.com/tgstation/tgstation/commit/79296e902cbdf2352c9303e4769ea39bf3b34e58

## Source states

| Prototype | State | Root groups | Parts | Classic objects | Combined objects |
| --- | --- | ---: | ---: | ---: | ---: |
| RMCBusha1 | busha1 | 7 | 91 | 5 | 12 |
| RMCBusha2 | busha2 | 8 | 102 | 4 | 9 |
| RMCBusha3 | busha3 | 6 | 77 | 3 | 11 |
| RMCBushb1 | bushb1 | 4 | 101 | 3 | 7 |
| RMCBushb2 | bushb2 | 6 | 112 | 4 | 8 |
| RMCBushb3 | bushb3 | 4 | 99 | 2 | 6 |
| RMCBushc1 | bushc1 | 10 | 110 | 1 | 3 |
| RMCBushc2 | bushc2 | 10 | 110 | 1 | 7 |
| RMCBushc3 | bushc3 | 7 | 77 | 2 | 5 |
