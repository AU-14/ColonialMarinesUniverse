# Structural and vehicle cloud art drafts

## Scope and status

Ten editable physical assemblies map the eleven assigned exact source prototype IDs. Two cargo-crane IDs share identical source artwork and one visual assembly; their different gameplay hitboxes are untouched. This family contains 541 parts, with no assembly above 124 parts, and 28 source-crop surfaces. All models are `status: draft`.

The saved-source inventory lists 15 Redux and 6 classic occurrences of these IDs. Those counts describe potential source scope only. No saved-map placement/contact test, native renderer admission, runtime behavior, performance, engine collision, terrain aperture, or fidelity acceptance is claimed. No runtime/engine edits or publication were made.

## Source ownership

All reference pixels were retrieved from `TheHellFireo/CMU-Garrison-3D`, ref `Chip/garrison-3d`, on 2026-10-06. PNG retrieval used the GitHub connector in base64 mode. Six original RSI metadata files are retained. Exact file SHA-256 and frame pixel hashes are recorded in `structural-vehicle-verification.json`; Git file identifiers and download paths are retained in `reference/structural-png-fetches.json`.

Every derived crop is an unchanged rectangular crop from its recorded source direction. The 58 crop uses deduplicate to 28 PNGs. These preserve the selected source pixels; they do not claim every pixel of every original frame has been transferred. All untextured paint colors are sampled from the actual source palette.

- Source: [Content.CMU/Resources/Textures/CMU14/Structures/vehicles/van.rsi](https://github.com/TheHellFireo/CMU-Garrison-3D/tree/Chip/garrison-3d/Content.CMU/Resources/Textures/CMU14/Structures/vehicles/van.rsi)
  - License: CC-BY-SA-3.0
  - Copyright / attribution: Taken from cmss13 at https://github.com/Steelpoint/cmss13/blob/2c86ac8909c70bd9992f52896dedb25b3b3beff0/icons/obj/vehicles/van_prop.dmi
- Source: [Resources/Textures/_RMC14/Structures/Vehicles/vehicles2.rsi](https://github.com/TheHellFireo/CMU-Garrison-3D/tree/Chip/garrison-3d/Resources/Textures/_RMC14/Structures/Vehicles/vehicles2.rsi)
  - License: CC-BY-SA-3.0
  - Copyright / attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/vehicles/vehicles.dmi
- Source: [Resources/Textures/_RMC14/Structures/concrete_structure.rsi](https://github.com/TheHellFireo/CMU-Garrison-3D/tree/Chip/garrison-3d/Resources/Textures/_RMC14/Structures/concrete_structure.rsi)
  - License: CC-BY-SA-3.0
  - Copyright / attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/structures/props/hybrisa/64x64_props.dmi, edited by github noctyrnal
- Source: [Resources/Textures/_RMC14/Structures/containers_vertical.rsi](https://github.com/TheHellFireo/CMU-Garrison-3D/tree/Chip/garrison-3d/Resources/Textures/_RMC14/Structures/containers_vertical.rsi)
  - License: CC-BY-SA-3.0
  - Copyright / attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/a69663ee8d9e3a980d486a1ee05efa44f79abc17/icons/obj/structures/props/containHorizont.dmi
- Source: [Resources/Textures/_RMC14/Structures/overhead_lattice_hybrisa.rsi](https://github.com/TheHellFireo/CMU-Garrison-3D/tree/Chip/garrison-3d/Resources/Textures/_RMC14/Structures/overhead_lattice_hybrisa.rsi)
  - License: CC-BY-SA-3.0
  - Copyright / attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/structures/props/industrial/hybrisa_lattice.dmi
- Source: [Resources/Textures/_RMC14/Structures/Vehicles/hardpoints/van.rsi](https://github.com/TheHellFireo/CMU-Garrison-3D/tree/Chip/garrison-3d/Resources/Textures/_RMC14/Structures/Vehicles/hardpoints/van.rsi)
  - License: CC-BY-SA-3.0
  - Copyright / attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/vehicles/van.dmi

The tracked-crane solid-part construction is adapted from the existing `CMU3DRMCPropVehicleCargoCraneAltHitboxWest` draft in [garrison_cranes.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_cranes.yml), inspected in the local source reference area. Its original cropped-surface roles are replaced with new crops from the same fetched `crane` frames. Cab lower panels, recessed glazing and opposite-side source details were separated/refined. This is a disclosed derivative of the neighboring crane model, not a claimed net-new crane design.

## Physical interpretation and remaining uncertainty

- Containers: three distinct `blue_b`, `blue_m`, `blue_t` modules, 45 source pixels across, one-tile module spacing, separate door leaves, four locking rods, physical raised roof ribs and side corrugations. Interior/side depth and the additional roof on the door module are inferred. The compound joint study is synthetic, with no claim that the saved map has been checked. Internal module ends remain open to join the contiguous shell; these are not terrain apertures
- Loose van wheel: circular barrel, front rim, hub and circumferential tread solids, with the axle rotated to match the diagonally receding source barrel about the existing pivot. Tire width, hidden rear rim and tread spacing remain inferred
- Cargo crane: exact source aliases S/W and N/E are byte-identical. `sourceCardinalFacings: [0, 2, 2, 0]` is preserved, indexed S/E/N/W as required by the asset contract. Crawler rollers, grated deck members, cab shell/glazing, hydraulic cylinder, stowed telescopic boom and ladder remain physical pieces. No crane motion, cargo or damaged variants are claimed
- Troop trucks: `truck_base` and `van_base`, four source directions each, three axles and six wheels, sloped hood and windscreen, roof, recessed cab glazing and distinct load bodies. The initial interpretation of the tarp lower bands as open was withdrawn: the actual source uses opaque dark pixels, and the final draft preserves them as recessed opaque material on both sides and rear. The image alone does not establish whether the real construction would be cloth, deep shadow or a recess, so that material interpretation is still provisional. No through-opening is claimed from those dark pixels
- Lattice C ends: actual left/right cropped frame extents, separate end cap, parallel rail pairs, straight center braces and two red inset strips. These differ from the ordinary diagonal-braced lattice family. Opaque dark source infill remains a recessed web, not a invented terrain opening. The 2.82-tile height and section depth are inherited draft conventions, not fit-tested elevations
- Sculpture: stepped relief-plaque pedestal, lower rectangular masses, tall central mass and the large right-descending diagonal wedge are separate solids. The source is an abstract sculpture, not a human figure. Exact unseen depth, side topology, bevels and material wear remain inferred

Source Sprite state names, original direction counts and prototype IDs are unchanged. Original Sprite offsets are recorded in the verification inventory: containers `(0.15, 0)`, troop trucks `(0.5, 0.5)`, crane `(0, 0)`, lattice/statue `(0, 0.5)`, wheel default `(0, 0)`. Existing source pivots are not edited. Authored model `groundOffset` keeps the source horizontal offset where relevant; source vertical screen offsets are not automatically translated into world depth. Physical pivot/depth interpretation still needs map-context review.

## Deliverables

- Editable models: `Content.CMU/Resources/ThreeD/Prototypes/World/garrison_structural_vehicle_cloud.yml`
- Source surfaces: `Content.CMU/Resources/ThreeD/Prototypes/World/garrison_structural_vehicle_cloud_art.yml`
- Texture directory: `Content.CMU/Resources/Textures/CMU14/ThreeD/structural_vehicle_cloud/`
- Ten direct, unchanged-exporter GLBs in `Content.CMU/Resources/Models/CMU14/Garrison/`
- Art authoring / verification: `Tools/three_d/author_structural_vehicle_cloud.py`, `verify_structural_vehicle_cloud.py`, `check_structural_vehicle_blender.py`
- Reviews/evidence: `Tools/three_d/generated/cloud-review/structural-vehicles/`

## Verification boundaries

The unchanged model loader validates all ten editable models. Final model GLBs are compared byte-for-byte against fresh `build_models.glb_bytes` results. Thirteen targeted solid-membership/structure checks verify deck grating gaps, unfilled cab interiors, the open truck bed, actual opaque tarp bands, canopy roof, sculpture slope material, lattice infill and container roof relief. These samples are not an exhaustive topology, intersection or collision audit.

Blender imports the final unchanged bytes and checks mesh object counts, finite coordinates, no invalid-mesh repairs and no animation actions. See `structural-vehicle-blender-import.json`. There are no new animation clips. A Khronos glTF-validator package was unavailable in this workspace, so no new Khronos validation result is claimed.

The central atlas assignment uses the exact prefix of `families.structural_vehicles.indices`, including the intentional gap after 1599. No old high-slot constants were reused.

Comparison sheets are explicitly auto-fit physical studies rather than quantitative source overlays. The separate twelve-direction comparisons disclose projection/depth differences. The full source-facing/orbit sheet and the synthetic three-section joint image are visual review evidence, not fidelity approval.
