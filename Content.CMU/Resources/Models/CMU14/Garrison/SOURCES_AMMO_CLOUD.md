# Ammunition-package visual prop drafts

This art-only cloud batch adds 13 editable assemblies for eight exact source IDs. It does not change gameplay, weapon function, engine/runtime code, source prototype behavior, or saved maps. All assemblies remain `draft`.

## Source and license

Sources were read from `TheHellFireo/CMU-Garrison-3D`, ref `Chip/garrison-3d`, through the connected repository. Original PNG pixels, RSI metadata, exact source IDs and relevant inherited YAML were inspected. File paths, repository blob SHAs and verified links are recorded in `Tools/three_d/generated/ammo-cloud-source-files.json`. All six RSI resources are CC-BY-SA-3.0. Preserve these attributions with the GLBs and derived crops.

- [Resources/Textures/Objects/Weapons/Guns/Ammunition/Casings/ammo_casing.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/Objects/Weapons/Guns/Ammunition/Casings/ammo_casing.rsi/meta.json): Taken from https://github.com/vgstation-coders/vgstation13/blob/0b3ab17dbad632ddf738b63900ef8df1926bba47/icons/obj/ammo.dmi, modified by Topy
- [Resources/Textures/_RMC14/Objects/Weapons/Guns/Ammunition/Magazines/mp5.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Weapons/Guns/Ammunition/Magazines/mp5.rsi/meta.json): Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/168c8a79a3d858a8c531ff7ed0bc3033e47d1d16/icons/obj/items/weapons/guns/ammo_by_faction/colony.dmi; modified by rando for CMU
- [Resources/Textures/_RMC14/Objects/Weapons/Guns/Ammunition/Magazines/hunting.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Weapons/Guns/Ammunition/Magazines/hunting.rsi/meta.json): Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/0ef24bde18ace14dea68fe274523857144e3a480/icons/obj/items/weapons/guns/ammo_by_faction/colony.dmi
- [Resources/Textures/_RMC14/Objects/Weapons/Guns/Ammunition/Magazines/abr40.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Weapons/Guns/Ammunition/Magazines/abr40.rsi/meta.json): Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7e3da429d0abfe55f0d948bb689bd7b49f3f304f/icons/obj/items/weapons/guns/ammo_by_faction/colony/marksman_rifles.dmi
- [Resources/Textures/_RMC14/Objects/Storage/packets.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Storage/packets.rsi/meta.json): Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/items/storage/packets.dmi
- [Resources/Textures/_RMC14/Objects/Clothing/Pouches/large_ammo_mag.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Clothing/Pouches/large_ammo_mag.rsi/meta.json): Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/274dffcf8574897b6b8fbcad07d81aa0f28292bb/icons/obj/items/clothing/pouches.dmi

## Geometry and exact mappings

- `CMCartridgePistol9mm`: brass case, rim, rounded copper cap, and a separate spent casing pose. The existing single-visible-layer sprite-state path has the original `base` and `base-spent` states, one static frame each. The unused `tip` resource is not falsely introduced as a layer. No animation clips are added.
- `CMMagazineSMGMP5`: curved segmented shell with blended bend junctions, a raised collar, folded lips, stamped trough and separate base shoe.
- `RMCMagazineRifleHunting`: short stepped shell, rounded lower shoulder, two horizontal flutes and a separate bottom plate.
- `RMCMagazineRifleABR40`: short asymmetric body, offset heel, horizontal rib and three lower pressed flutes.
- `AU14PacketGrenadeTearGasFilled` and `AU14PacketGrenadeTearGasFilledAirburst`: separate cardboard walls, bottom, closure flaps, fold seams and original top/front printed label crops. Open-empty studies contain actual shallow interiors and released flaps. Each exact ID retains its own label art; the airburst front accent is different.
- `AU14PouchMagazineLargeFilledM16` and `RMCPouchMagazineLargeMP5`: one shared identical-world-art assembly with four separate rounded fabric pockets, folded closure caps, compressed fabric bands, seam bindings and inferred rear loops. The explicit open study retains the right-hand pair of closed caps because the original `open` overlay does exactly that. The two source IDs differ in contents, which are not visible in this art and are not invented.

The three magazines have two static studies each: composed `base + mag-1` loaded art, and `base` empty art. Empty alternates have no source mappings. The two packets have closed and open-empty studies; the pouch has closed/open studies. These are not runtime-linked states. MagazineVisuals and CMStorageVisualizer own multiple layers, so those families intentionally have no single-layer `spriteStates` adapter. Contents, worn/held appearance and gameplay-trigger verification remain unsupported.

## Crops, editable assets and evidence

Canonical authored files: `garrison_ammo_cloud.yml` and `garrison_ammo_cloud_art.yml` under `Content.CMU/Resources/ThreeD/Prototypes/World`. Six original unresampled label crops occupy atlas slots 2409–2414 under `Textures/CMU14/ThreeD/ammo_cloud`. No whole-sprite slab textures are used. The models contain 284 individually named solid parts, using boxes, cylinders, ellipsoids and folded wedges. Physical depths, underside/reverse construction and material behavior are visual inferences, not engineering dimensions.

`Tools/three_d/author_ammo_cloud.py` regenerates this batch without changing the exporter. `Tools/three_d/verify_ammo_cloud.py` performs the art-specific checks. The unchanged `build_models.py` exports 13 real GLBs and deterministically verifies them with their manifest and viewer references. Total exported triangle count is 14,360.

- `Tools/three_d/generated/cloud-ammo/*-source-and-orbit.png`: thirteen original composed-source comparisons, source-facing solids and two orbit views
- `ammo-cloud-overview.jpg`: compact family contact sheet
- `ammo-cloud-verification.json`: 13 deterministic binary checks, finite buffers, actual accessor min/max checks, unique source mappings, six byte-identical crop comparisons and atlas collision audit
- `ammo-cloud-source-audit.json`: exact source prototypes, inherited reference inventory, source links and full RSI attributions
- `ammo-cloud-blender-import.json`: all thirteen GLBs imported successfully in Blender 4.3.2
- `deterministic-check.txt`: final unchanged-exporter result

Source inventory mentions 43 Redux and 23 classic instances across these eight IDs. Those are potential source inventory counts only. They are not admitted native objects, proven visible placements or map-contact checks. No map fit, native rendering, live state playback or fidelity completion is claimed.

## Remaining work

The source-facing comparisons establish inspectable drafts, not fidelity approval. Refine curved shell seams, fabric silhouette/creases, packet edge thickness, inferred backs and undersides; review scale and placement against supported tables/floors in the native map. Multi-layer magazine/container states require their existing owners to be audited and supported separately. No animation, weapon function, physical simulation, cartridge internals or live state controller has been implemented.


## Cumulative atlas assignment
This cumulative snapshot uses centrally allocated, nonconflicting atlas slots. Any authoring-time numeric range in this historical family note is superseded by the canonical surface YAML and `Tools/three_d/generated/cloud-review/atlas-allocation-current.json`. Source IDs, original PNG pixels and modeled geometry are unchanged by atlas-index remapping.
