# Garrison woody shrubs and seed-head plants

Eleven source-specific draft assemblies / 682 parts cover twelve visual prototypes: 37 classic placements and 83 across configured maps. Canonical definitions are `garrison_stemmed_shrubs.yml`. No asset is approved art.

## Source interpretation

The original one-direction, 32-by-32 states are in `/Textures/Decals/Flora/flora_bushes.rsi`. D variants have forked brown stems and small olive leaves, E variants have basal green rosettes and three orange seed-bearing stalks, and H variants have sparse gray stems and small green leaves. The four D, four E and three H source states were inspected individually. `RMCBushh3` and `StalkyBush03` share exactly the same source state and one model; `StalkyBush01` uses bushh1.

Manual source-pixel paths retain branch and root groups. Small green regions retain inspected leaf positions, and E seed details follow original orange pixels. Comparison split oversized leaf disks, added cylindrical root bases, staggered the branches in depth and replaced horizontal connector rungs with rising branches. All part colors occur in their original source. No generated textures or new atlas images are used. The 484-image atlas is unchanged.

## Orientation, placement and unresolved context

The RMC sprites use `noRot`; the two upstream StalkyBush placements have saved yaw zero. All 37 render with zero yaw, including 33 nonzero RMC saved rotations correctly ignored for presentation. Corrected scene facings total 574. Every saved transform and every unrelated scene record is unchanged. No added entity ground offset is used.

The first draft incorrectly copied source root rows into world depth, embedding plants in a planter border. Local geometry now centers each root group around the entity pivot while preserving relative group spacing. The per-state correction is retained in `stemmed-shrubs-trace-audit.json`. Screen-vertical framing is not a world-depth offset.

The final conservative neighbor audit retains 52 directed contacts: 31 shoreline/water, 19 other plants and two platform entries; there are no wall entries. Water bounds include transparent cutout areas and do not establish actual water contact. A separate analytic solid check confirms seven residual stem/leaf part intersections: six between plant #9782 and platform #12904, and one between plant #9790 and platform #12906. These are real remaining draft errors, not AABB false positives. Root recentering fixed the large placement shift but did not resolve those parts. Platform elevation and terrain support still require source/context work. No simulation transform or collider was changed to conceal overlap.

Physical height, branch thickness/depth, hidden construction and side views remain inferred. Fine shading, wind, terrain elevation, dynamic interaction and final art fidelity remain unfinished. The standard gameplay viewport is unchanged.

## Evidence and checks

- `stemmed-shrubs-source-audit.json`, `stemmed-shrubs-trace-audit.json`, `stemmed-shrubs-palette-audit.json` retain hashes, source settings, branch paths and palette evidence.
- `stemmed-shrubs-placement-audit.json`, `stemmed-shrubs-before-pivot-audit.json` and `stemmed-shrubs-platform-audit.json` retain transform checks, conservative contacts and analytic contact witnesses.
- All eleven source/four-view cards and three oblique views were inspected. Final browser captures cover shoreline, platform and courtyard contexts. Five new `garrison-shrubs-*` regions join the assembled exports.
- 99 isolated native checks pass with the current 699-model library. All 699 deterministic individual exports and all 48 assembled exports pass Khronos validation with zero errors/warnings. Browser warning/error logs are empty.
- Renderer code did not change in this batch. The previous grass pass's 114 Python checks, 22 Node checks, 624 native GPU pixels and successful client build remain the latest renderer evidence; those were not rerun or claimed as fresh shader/build checks here.

## Source license

New geometric work is CC0-1.0 to the extent separately licensable. Derivative appearance and source references retain CC-BY-SA-3.0.

Taken from tgstation at commits https://github.com/tgstation/tgstation/commit/729d858807905263adab8b5a331c1d8a04982dd3, https://github.com/tgstation/tgstation/commit/79296e902cbdf2352c9303e4769ea39bf3b34e58

## Source states

| Prototype(s) | State | Parts | Classic objects |
| --- | --- | ---: | ---: |
| RMCBushd1 | bushd1 | 67 | 8 |
| RMCBushd2 | bushd2 | 57 | 2 |
| RMCBushd3 | bushd3 | 60 | 4 |
| RMCBushd4 | bushd4 | 73 | 3 |
| StalkyBush01 | bushh1 | 79 | 1 |
| RMCBushh2 | bushh2 | 65 | 4 |
| RMCBushh3, StalkyBush03 | bushh3 | 75 | 2 |
| RMCBushe1 | bushe1 | 53 | 4 |
| RMCBushe2 | bushe2 | 48 | 2 |
| RMCBushe3 | bushe3 | 52 | 3 |
| RMCBushe4 | bushe4 | 53 | 4 |

## Subsequent platform correction

The Hybrisa metal-platform pass refreshes `stemmed-shrubs-platform-audit.json`: #9790/#12906 clears, while #9782/#12904 still has eleven analytic solid intersections with subdivided source-colored platform parts. The earlier seven-part-pair count below describes the previous platform geometry, not the current result. Plant geometry and saved transforms are unchanged. See `SOURCES_HYBRISA_PLATFORMS.md`.
