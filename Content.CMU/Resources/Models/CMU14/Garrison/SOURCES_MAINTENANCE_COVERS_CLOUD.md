# Maintenance hatch and Hybrisa sewer-cover drafts

Art-only source-grounded default covers at commit `6e37a4d0a7d9433838c393a82c02422cb704dd5a`. All models remain **draft**. The batch contains two physical designs, three exact prototype mappings, and five potentially covered Redux placements. This is not saved-map, native/runtime, animation, terrain-opening, underground-travel, or fidelity acceptance. No engine/runtime files, existing models, map tiles, repository commits, pushes, or publishing are part of this work.

## Models and source owners

| Model | Exact source prototypes | Redux placements | Editable parts |
|---|---|---:|---:|
| `CMU3DMaintenanceHatchCloud` | `XenoTunnelMaint` | 2 | 55 |
| `CMU3DHybrisaSewerCoverCloud` | `XenoTunnelMaintHybrisa`, `XenoTunnelMaintHybrisaNoXenoDesc` | 1 + 2 | 102 |

Both Hybrisa owners resolve to exactly the same `wymanhole` artwork. Their text-description difference does not justify different physical artwork. Only these three assigned IDs are mapped; `XenoTunnel`, `XenoTunnelMaintNoXenoDesc`, and unrelated tunnel appearances are not added as aliases.

The source parent chain is in [`xeno_tunnel.yml`](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Prototypes/_RMC14/Entities/Structures/Xeno/xeno_tunnel.yml). `Transform.anchored: true` and `Sprite.noRot: true` are inherited from `XenoTunnel`. The source RSI lists one default frame per selected state and no direction-specific or animated frames. The models declare `sourceDirections: 1` and do not introduce camera-selected poses, smoothing, or entity-rotation overrides.

## Source evidence and attribution

All actual PNG pixels and the parent YAML were inspected. Git blob SHA-1 was recomputed over the exact file bytes and checked against the pinned fetch receipts; SHA-256 is also recorded in `Tools/three_d/generated/maintenance-covers-cloud/source-and-geometry-proof.json`.

- [`maintenancehatch_alt.png`](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Xenos/xeno_tunnel.rsi/maintenancehatch_alt.png): 32 × 32 source frame, opaque extent `[2,15,30,31]`, git blob `33cea3c91d7dba342e3e281ef2640b808142ad55`
- [`wymanhole.png`](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Xenos/xeno_tunnel.rsi/wymanhole.png): 32 × 32 source frame, opaque extent `[6,15,27,31]`, git blob `bbc877609bfe0f9fb37ec2e933215407cf49d12c`
- [`meta.json`](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Xenos/xeno_tunnel.rsi/meta.json): git blob `19d7ef9e4fca45fd2e65ce740869387906e4369d`
- Parent YAML: git blob `7f0c07da50c00000b5ac273b4e11574c9e6f7928`

Source metadata states **CC-BY-SA-3.0** and credits: “Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/e1a270d04e95a2294c43ecb309f17c294fcfeb8d/icons/obj/structures/ladders.dmi, hole by github noctyrnal”. Preserve this attribution and the source license with the original pixel crops and these source-derived art drafts. The `hole` state is not used by these models. The original metadata is also retained in `maintenance_covers_cloud/source-attribution.json` beside the texture crops.

## Physical construction and limits

### Rectangular cover

The outer rim follows the 28-pixel source width. The source's compressed ground-plane depth is interpreted as a 22-pixel physical depth; that deprojection is an artistic inference, not a measured dimension. Four solid rails and four dark outer lips form a connected frame. Six small original rim-strip crops retain the local paint and highlight details. They are seated on physical rim geometry and are not a full-sprite billboard.

The inner field traces source x=7..24, y=18..26. Source rows 19, 21, 23 and 25 have four staggered rows of nine dark cells, giving **36 real through-apertures** in this construction. Five continuous transverse ribs and 36 staggered short ribs connect to the frame. The source dark cell interiors are interpreted as depth: no central black sheet or full backing plane is present. Thickness, underside and metal construction are inferred. The source does not clearly establish discrete screw heads on this rectangular form, so none are invented.

### Circular cover

The circular footprint follows the 21-pixel source width. A 24-sector connected dark annulus has separate narrow metallic outer and inner bevel bands. Five longitudinal and five transverse ribs fill the central opening. There are **16 complete central cells with checked clear vertical aperture paths**, plus boundary gaps that are visible but not used to claim a total aperture count.

The source center is opaque checker-shaded art rather than an alpha hole mask. Its physical through-grid interpretation is explicitly **inferred and not source-proven topology**. The source directly shows four very dark cardinal recesses. They are modeled as four-sided blind wells with metal bottoms and four tiny original pixel crops. Their purpose as screw/fixing or lifting sockets is not established by the source, so no literal screw heads are invented. The four recesses are not tunnel openings.

### Floor and context gap

These deliver the physical covers only. No `slabOpening` or other floor-opening metadata is authored, no floor tiles are cut or replaced, and no generic dark floor, pit or false tunnel void is provided. The existing single-tile rectangular opening contract does not establish a general circular or multi-tile terrain cutout. A rectangular cutout under the circular cover would invent visible corner gaps.

On an uncut floor, the ordinary slab can remain visible through the physical cover apertures. If a see-through underground entrance is required, that context needs separately authorized topology/terrain work and map-specific acceptance. This batch does not claim underground travel, native floor cutting, collision behavior, live placement or saved-map fit. The horizontal pivot, model thickness and underside remain inferred until that context review.

## Surfaces and palette

`garrison_maintenance_covers_cloud_art.yml` owns exactly ten new surfaces in the reserved `maintenance_covers` atlas slots **3179–3188**. The six rectangular rim strips and four small circular recess crops preserve source RGBA bytes exactly. No crop contains a complete source silhouette or supplies an aperture mask. The per-crop source rectangle, raw RGBA hash and PNG hash are recorded in the proof JSON. All untextured model paints are exact RGB values sampled from the corresponding source PNG. White texture paint is a neutral multiplier.

## Checks completed

- Four pinned source files pass their receipt git blob SHA checks
- Ten texture crops pass independent pixel-for-pixel comparison to their original source rectangles
- All untextured paints belong to the correct source palette
- Reserved atlas slots are exact and checked against the current authored surface files for collisions
- Both models pass the unchanged `build_models.validate_model` contract, contain supported editable `Box` parts, remain at or below 128 parts, and retain `draft` status
- Re-exported GLB bytes match the delivered files exactly; hashes of the unchanged exporter, primitives and surface modules are included in the verification record
- Contact tests read actual GLB vertices and node transforms, then use convex-hull planes and linear programming for strictly positive common-volume witnesses: each entire model, including source-detail parts, has one connected component
- All 36 rectangular and 16 circular checked aperture rays remain clear from Z=-0.10 through Z=0.20 against every exported solid
- All four circular recesses are clear from above and have a verified metal bottom
- Blender 4.3.2 independently imports both unchanged final GLBs with the expected 55 and 102 mesh objects, finite vertices, zero mesh repairs and zero animation actions; it does not re-export or modify the files

These checks are not a Khronos validator run, global manifold certification, mechanical engineering proof, source-fidelity approval, or game/native/map test.

## Review and reproducibility

Previews in `Tools/three_d/generated/review/maintenance-covers-cloud/` include actual source comparisons, source-facing projection, top, orbit, rear orbit, underside and a common **450 pixels per tile** fixed-scale view. The review projection is explicitly an art comparison; hidden surfaces and physical deprojection are inferred.

Evidence lives in `Tools/three_d/generated/maintenance-covers-cloud/`:

- `source-and-geometry-proof.json`: sources, crop rectangles, palette pixels, model hashes and scope limits
- `verification.json`: unchanged-export checks, actual-mesh contact witnesses, aperture rays and blind recess checks
- `blender-import.json`: independent import and zero-repair results
- `manifest.json`: complete owned output list and hashes

Rebuild only this family from the repository root:

```sh
python Tools/three_d/authoring/author_maintenance_covers_cloud.py
python Tools/three_d/authoring/verify_maintenance_covers_cloud.py
blender -b -t 1 --python Tools/three_d/authoring/check_maintenance_covers_blender.py
```

The scripts write only this family’s art, previews and verification files and use the existing exporter unchanged. The shared atlas allocation document is read, not rewritten.
