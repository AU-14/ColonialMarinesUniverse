# Broadleaf foliage refinement

This records the historical foliage-only refinement. Later work is documented in [SOURCES_TREE_WOOD.md](SOURCES_TREE_WOOD.md) and [SOURCES_CANOPY_FIT.md](SOURCES_CANOPY_FIT.md). The containment proof below applies only to this earlier snapshot: current crowns have moved, and neither their occupied volume nor later wood is covered by that proof.

The twelve `CMU3DBroadleafTree01`–`06` and `CMU3DBroadleafTreeLarge01`–`06` assemblies retain their distinct crown arrangements, root pivots, source facing and conditional RandomSprite bindings. They cover 131 fixed classic placements / 266 across the configured maps. Unknown saved random choices remain unsupported.

718 smooth outer crown parts are replaced by open sprays containing nineteen individually rotated, closed tapered leaves each (13,642 leaflets). Smaller shaded interior cores retain volume without filling the spaces between separate crown groups. Greens are sampled from opaque pixels in each corresponding original state. The source-pixel/part record is `Tools/three_d/generated/broadleaf-spray-source-audit.json`.

## Attribution

- `Resources/Textures/Objects/Decoration/Flora/flora_trees.rsi`, `tree01`–`tree06`: CC-BY-SA-3.0, taken from tgstation at commit [e00cae8d065f9cf520688cc0dd0e15ba5bef12a9](https://github.com/tgstation/tgstation/commit/e00cae8d065f9cf520688cc0dd0e15ba5bef12a9).
- `Resources/Textures/Objects/Decoration/Flora/flora_treeslarge.rsi`, `treelarge01`–`treelarge06`: CC-BY-SA-3.0, taken from tgstation at commit [d388dee8b7b6d854f6f0d844988552acf5962b1f](https://github.com/tgstation/tgstation/commit/d388dee8b7b6d854f6f0d844988552acf5962b1f).

The source RSI metadata remains authoritative. Retain these source attributions with derived assets. No third-party reconstruction service was used.

## Geometry and placement

`Tools/three_d/foliage.py` defines the irregular leaf arrangement. `generate_foliage.py` generates browser and native analytic transform tables; `--check` detects stale copies. Export, comparison rendering and the browser use closed meshes; native CPU/GPU intersection uses the corresponding analytic ellipsoids and transformed normals. Gaps pass through both rendering and picking.

Every leaf is contained in its previous ellipsoid: the length of its center plus its largest semi-axis is less than the parent's normalized radius. Shrunk cores also remain contained, including rounding. The proof covers entire analytic leaves, not only mesh vertices. Combined with unchanged saved transforms and all other solids, this introduces no new occupied volume or new intersections. It does **not** clear existing intersections. See `generated/broadleaf-spray-containment.json`.

No surface images, map positions, offsets, facings, gameplay fixtures, model counts or part budgets change. The seven unknown classic RandomSprite choices are not assigned guessed models. The rejected full-volume candidates in `generated/broadleaf-candidates.yml` remain uninstalled.

## Remaining work

These are improved drafts, not final art. Trunks and roots still have visibly stepped construction; detailed source branching, hanging vines, silhouette proportions, hidden foliage depth, wind and material response need further work. Fine leaf positions and physical depth are inferred. Native GPU smoke tests establish shader/picking behavior, not full-scene frame-time performance or an interactive native visual review. The standard gameplay viewport is unchanged.
