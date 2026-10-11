# Garrison reed, branching, rounded and conifer bushes

Nine source-specific draft assemblies / 929 parts cover 13 classic placements and 40 across configured maps. Canonical definitions are `garrison_remaining_bushes.yml`. No asset is approved art.

## Source interpretation and refinements

The original single-direction 32-by-32 states in `/Textures/Decals/Flora/flora_bushes.rsi` are I1–I4, K1/K3, L2 and M1/M4. The four I variants preserve 18, 18, 17 and 17 upright source stalk columns. K variants have four separate woody leaf crowns. L2 has a rounded leafy branch scaffold. Each M variant has six source-specific conifer tiers and its own trunk shape.

Initial comparison exposed overly flat reeds, regular disk-like crowns, cup-like leaves and long palm-like conifer boughs. Successive refinements stagger the reed stalks in depth, replace round reed nodes with slender ribs, build irregular overlapping crown patches, close bare crown centers and shorten the conifer twigs. Thin slanted leaf/needle clusters replace the roundest patches. Horizontal leaf bounds follow occupied source foliage. The two reed edge-tip curls move less than one source pixel and clear a border and stair without moving the stalk roots or entities.

All part colors occur in the original source art. The 1,123 recorded wood/green sample pixels pass their source checks; hashes and exact sample coordinates are retained. These checks constrain color and traced features, not full 3D fidelity. The 484-image atlas is unchanged; there are no new textures.

## Orientation and placement

All nine sources use single-frame `noRot`, zero sprite offset, FloorObjects draw depth and noncolliding static physics. All 13 raw saved objects have Transform overrides and empty Fixtures overrides, with no saved sprite-state changes. Eight saved nonzero yaws are correctly ignored for source facing; the scene now has 599 adjusted facings. Every saved transform and every unrelated scene record is unchanged. Added ground offsets are zero.

The final conservative audit has 15 directed contacts: fourteen with plants, grass, trees or stumps and one with a platform. The initial reed/border, reed/stair and oversized K3-leaf/border contacts are cleared. Exact analytic surface checks confirm 23 part intersections between conifer #9838 and border #12368. The older D2 shrub #9782 still has eleven part intersections with border #12904 after the previous platform correction. Neither unresolved pair is claimed fixed.

Both plants occupy bounded `CMPlanetGrassDirt` patches: one soil tile beneath #9838 and three connected tiles beneath #9782. `planter-elevation-followup.json` retains their tiles and neighboring platforms. Raised soil is a hypothesis requiring consistent native/browser/GLB terrain support and source/context review; no elevation adjustment was made in this batch. Map tile coordinates denote lower-left tile corners, not centers.

## Evidence and checks

- `remaining-bushes-source-audit.json`, `remaining-bushes-trace-audit.json`, `remaining-bushes-sample-audit.json` and `remaining-bushes-palette-audit.json` retain source components, hashes, traced features, sample checks and refinements.
- `remaining-bushes-placement-audit.json` verifies saved fixtures/transforms, unchanged unrelated records and all nearby modeled contacts. `remaining-bushes-structure-audit.json` distinguishes conservative bounds from actual curved-solid/platform intersections.
- All nine source/four-view cards and nine oblique models were inspected. Seven browser contexts cover reed/border clearance, stairs, both branching shrubs, northern reeds and both conifers. Browser warning/error logs are empty.
- Three new `garrison-remaining-bushes-*` regions join all refreshed actual scene exports. There are now 58 assembled exports, including the isolated pose fixture.
- All 721 deterministic model exports match regeneration. Khronos reports zero errors/warnings for 721 individual and 58 assembled GLBs. The isolated native suite passes all 99 checks.
- No renderer code changed. Earlier Python/Node/GPU/client-build evidence is historical, not rerun here. Connected gameplay remains unverified.

## Limitations

Physical height, depth, hidden branching and side/back geometry remain inferred. Reed side views remain narrow, crown density and fine needle detail remain simplified, and original shading is approximated by source-colored solids. Terrain elevation, wind, interaction and animated states are unfinished. Standard gameplay rendering is unchanged.

## License

New geometric work is CC0-1.0 to the extent separately licensable. Derivative appearance and references retain CC-BY-SA-3.0.

Taken from tgstation at commits https://github.com/tgstation/tgstation/commit/729d858807905263adab8b5a331c1d8a04982dd3, https://github.com/tgstation/tgstation/commit/79296e902cbdf2352c9303e4769ea39bf3b34e58

## Source states

| Prototype | State | Parts | Classic objects | Combined objects |
| --- | --- | ---: | ---: | ---: |
| RMCBushi1 | bushi1 | 108 | 1 | 5 |
| RMCBushi2 | bushi2 | 108 | 3 | 8 |
| RMCBushi3 | bushi3 | 102 | 1 | 9 |
| RMCBushi4 | bushi4 | 102 | 3 | 10 |
| RMCBushk1 | bushk1 | 84 | 1 | 2 |
| RMCBushk3 | bushk3 | 82 | 1 | 2 |
| RMCBushl2 | bushl2 | 107 | 1 | 1 |
| RMCBushm1 | bushm1 | 118 | 1 | 2 |
| RMCBushm4 | bushm4 | 118 | 1 | 1 |
