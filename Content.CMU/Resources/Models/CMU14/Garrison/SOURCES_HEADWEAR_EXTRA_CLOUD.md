# Loose headwear and eyewear cloud art drafts

Source checkpoint: `6e37a4d0a7d9433838c393a82c02422cb704dd5a` from `TheHellFireo/CMU-Garrison-3D` (`Chip/garrison-3d`).

25 editable primitive assemblies, 23 exact prototype mappings, 30 recorded Redux placements. 677 total parts; maximum 53 per pose. Every model remains `draft`.

## Scope and boundaries

- Ground/world items only. No actor rig, head socket, worn, held or accessory attachment mapping is claimed.
- Canonical exporter `Tools/three_d/build_models.py` is unchanged. No engine, runtime, map, shader, gameplay or existing-art changes are part of this family.
- Models use closed editable convex primitive parts with positive-volume overlaps. The assembled object is not asserted to be a single watertight manifold.
- Hats have actual open underside paths. Fedora brims use 32 overlapping flat segments and do not have the earlier conspicuous inter-segment notches.
- The current primitive contract does not represent a smooth thin concave crown shell. Fedoralike crowns retain visible facets and roof seams; berets/cap tops use shallow continuous convex roof approximations. These are explicit fidelity limitations, not substituted bead chains.
- Party hats use an open eight-sided triangular-panel shell. The continuous rising ribbons approximate the source diagonal stripe wrap; exact conical curvature, stripe width and UV wrapping remain unfinished.
- Original palettes are retained. Opaque source eyewear colors are not silently converted to physically transparent glass. Optical transmission, reflections and display emission remain unverified.
- Source-facing previews use 448 pixels per world unit and 14 pixels per source pixel, then recenter only the preview image for silhouette comparison. Orbit and underside images are separate fixed-angle views. Preview recentering does not edit model pivots or saved placement.

## Inheritance and source-state findings

- All 23 target IDs occur both in `physicalModelingFamilies` and the Redux inventory. No inferred parent binding is counted as an exact source mapping.
- `ClothingHeadBase` supplies ground `Sprite.state: icon`; child definitions select their specific RSI. The RMC cap/beret chains retain inherited clothing/stain/accessory metadata, which is not rendered by these ground-only drafts.
- `CMHeadCapSPPUshanka` declares visible `icon`, hidden `icon-up`, `Foldable` and `FoldableClothing`. Its base model is unfolded. The state-only folded model has no duplicate source binding and uses a reciprocal `alternateFoldModel` link. Transition animation and native state admission remain untested.
- `RMCGlassesMedicalHUDGlasses` explicitly defaults `ItemToggle.activated: true`. Its GenericVisualizer selects `icon-on` when enabled and `icon` otherwise. The exact base draft therefore references `icon-on` frame 0. The separate off pose has no source binding. The four active source frames have durations 1.2, 0.2, 0.2, 0.2 seconds. Blinking and automatic runtime toggle selection are not implemented; no animation clip or collapsed hidden-frame nodes are exported.
- `ClothingHeadHatWelding` owns an `icon-up` resource but its inspected prototype/parent chain has no Foldable component. The base `icon` pose is modeled; a raised-state mapping is not invented.
- Orange goggles inherit accessory RSI references from `RMCGogglesBallistic`. Ground sprite ownership is the orange child RSI. Helmet/hat accessory overlay inheritance is recorded as an unsupported separate context.
- Brown RMC fedora PNGs have identical visible pixels but differ in transparent RGB bytes and have different RSI paths/attribution. Their two prototype models remain separate; this batch adds no cross-prototype exact aliases.

## Validation

- 25/25 GLBs reproduce byte-for-byte with the unchanged exporter; raw independent accessor checks pass finite values, bounds, indices and buffer ranges.
- 25/25 assemblies have one actual exported convex-mesh contact component. Every selected cavity has an unobstructed path from below to its empty test point. This is not a complete cloth/topology proof.
- Blender 4.3.2 imported all 25 GLBs with the expected mesh count and zero invalid-mesh repairs.
- All 8 source detail crops are pixel-exact with SHA-256 records. 63 fetched source files match their Git blob SHA-1.
- Atlas allocation uses only reserved `headwear_extra` indices: 1994, 1995, 1996, 1997, 1998, 1999, 2070, 2071. All loaded surface IDs/indices are unique.
- Khronos validator is unavailable and was not run. Native admission, live gameplay/state selection, saved-map fit, performance and fidelity approval remain untested.

## Reproduce

```sh
python Tools/three_d/author_headwear_extra_cloud.py
python Tools/three_d/verify_headwear_extra_cloud.py
python Tools/three_d/verify_headwear_extra_connections.py
blender -b --python Tools/three_d/verify_headwear_extra_blender.py
python Tools/three_d/document_headwear_extra_cloud.py
```

## Per-pose evidence

### CMU3DAU14CivBallCapBlackWorldCloud

- Prototype: `AU14CivBallCapBlack`; pose `icon` frame 0; 23 parts
- Definition: [Content.CMU/Resources/Prototypes/CMU14/Entities/Clothing/Civilian/hats.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Prototypes/CMU14/Entities/Clothing/Civilian/hats.yml)
- Resource: [Content.CMU/Resources/Textures/CMU14/Clothing/Civilian/Hats/blackballcap.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Textures/CMU14/Clothing/Civilian/Hats/blackballcap.rsi/meta.json)
- Source: [Content.CMU/Resources/Textures/CMU14/Clothing/Civilian/Hats/blackballcap.rsi/icon.png](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Textures/CMU14/Clothing/Civilian/Hats/blackballcap.rsi/icon.png); SHA-256 `996cf50beb33c90fa20e9967e1dc150cecd1ddfa5ee374b2ee7944ac32495b0a`
- Geometry: Small ball cap with offset projecting bill, open lower band, connected faceted sides and one shallow continuous roof. The trucker pale front panel follows the source. Roof underside and panel seams are inferred; a smooth thin concave shell is unavailable.
- Resource states: `icon` (1 direction), `equipped-HELMET` (4 directions)
- License: CC-BY-SA-3.0
- Original copyright, verbatim: Taken from CM-SS13 PvE at https://github.com/cmss13-devs/cmss13-pve/tree/master/icons/obj/items/clothing

### CMU3DAU14CivBallCapBlueTruckerWorldCloud

- Prototype: `AU14CivBallCapBlueTrucker`; pose `icon` frame 0; 25 parts
- Definition: [Content.CMU/Resources/Prototypes/CMU14/Entities/Clothing/Civilian/hats.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Prototypes/CMU14/Entities/Clothing/Civilian/hats.yml)
- Resource: [Content.CMU/Resources/Textures/CMU14/Clothing/Civilian/Hats/bluetruckercap.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Textures/CMU14/Clothing/Civilian/Hats/bluetruckercap.rsi/meta.json)
- Source: [Content.CMU/Resources/Textures/CMU14/Clothing/Civilian/Hats/bluetruckercap.rsi/icon.png](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Textures/CMU14/Clothing/Civilian/Hats/bluetruckercap.rsi/icon.png); SHA-256 `a36731f39dc873741e554e0254ac393a979124cb870bab6c7d8e7b1796697b7c`
- Geometry: Small ball cap with offset projecting bill, open lower band, connected faceted sides and one shallow continuous roof. The trucker pale front panel follows the source. Roof underside and panel seams are inferred; a smooth thin concave shell is unavailable.
- Resource states: `equipped-HELMET` (4 directions), `icon` (1 direction)
- License: CC-BY-SA-3.0
- Original copyright, verbatim: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/mob/humans/onmob/clothing/head/soft_caps.dmi, https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/items/clothing/hats/soft_caps.dmi

### CMU3DAU14CivBallCapRedTruckerWorldCloud

- Prototype: `AU14CivBallCapRedTrucker`; pose `icon` frame 0; 25 parts
- Definition: [Content.CMU/Resources/Prototypes/CMU14/Entities/Clothing/Civilian/hats.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Prototypes/CMU14/Entities/Clothing/Civilian/hats.yml)
- Resource: [Content.CMU/Resources/Textures/CMU14/Clothing/Civilian/Hats/redtruckercap.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Textures/CMU14/Clothing/Civilian/Hats/redtruckercap.rsi/meta.json)
- Source: [Content.CMU/Resources/Textures/CMU14/Clothing/Civilian/Hats/redtruckercap.rsi/icon.png](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Textures/CMU14/Clothing/Civilian/Hats/redtruckercap.rsi/icon.png); SHA-256 `4857e9f0e03f8d89801af61ac06833cd0e41875f055d9fc848f7db0f7b4939b0`
- Geometry: Small ball cap with offset projecting bill, open lower band, connected faceted sides and one shallow continuous roof. The trucker pale front panel follows the source. Roof underside and panel seams are inferred; a smooth thin concave shell is unavailable.
- Resource states: `equipped-HELMET` (4 directions), `icon` (1 direction)
- License: CC-BY-SA-3.0
- Original copyright, verbatim: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/mob/humans/onmob/clothing/head/soft_caps.dmi, https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/items/clothing/hats/soft_caps.dmi, https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/items/clothing/hats/soft_caps.dmi

### CMU3DAU14CivFedoraGrayWorldCloud

- Prototype: `AU14CivFedoraGray`; pose `icon` frame 0; 53 parts
- Definition: [Content.CMU/Resources/Prototypes/CMU14/Entities/Clothing/Civilian/hats.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Prototypes/CMU14/Entities/Clothing/Civilian/hats.yml)
- Resource: [Content.CMU/Resources/Textures/CMU14/Clothing/Civilian/Hats/fedoragray.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Textures/CMU14/Clothing/Civilian/Hats/fedoragray.rsi/meta.json)
- Source: [Content.CMU/Resources/Textures/CMU14/Clothing/Civilian/Hats/fedoragray.rsi/icon.png](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Textures/CMU14/Clothing/Civilian/Hats/fedoragray.rsi/icon.png); SHA-256 `5e33ed1dcb2520c3ae6d08609e3fe91255c464589fc29147759e852b6cbdbb0e`
- Geometry: Fedora with broad planar annular brim, eight connected crown panels, dark separate ribbon and a recessed center roof crease. Crown concavity is faceted, not a smooth thin felt shell.
- Resource states: `equipped-HELMET` (4 directions), `icon` (1 direction)
- License: CC-BY-SA-3.0
- Original copyright, verbatim: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/e7e2fe8c7d4dc0d322745ae5c2fa190b2e010897/icons/mob/humans/onmob/clothing/head/formal_hats.dmi, https://github.com/cmss13-devs/cmss13/blob/e7e2fe8c7d4dc0d322745ae5c2fa190b2e010897/icons/obj/items/clothing/hats/formal_hats.dmi

### CMU3DAU14CivFedoraTanWorldCloud

- Prototype: `AU14CivFedoraTan`; pose `icon` frame 0; 53 parts
- Definition: [Content.CMU/Resources/Prototypes/CMU14/Entities/Clothing/Civilian/hats.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Prototypes/CMU14/Entities/Clothing/Civilian/hats.yml)
- Resource: [Content.CMU/Resources/Textures/CMU14/Clothing/Civilian/Hats/fedoratan.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Textures/CMU14/Clothing/Civilian/Hats/fedoratan.rsi/meta.json)
- Source: [Content.CMU/Resources/Textures/CMU14/Clothing/Civilian/Hats/fedoratan.rsi/icon.png](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Textures/CMU14/Clothing/Civilian/Hats/fedoratan.rsi/icon.png); SHA-256 `d08a84dcd794f2251798c2fc7cf60d6e6de4c305511ddbb0901c1344fbe94432`
- Geometry: Fedora with broad planar annular brim, eight connected crown panels, dark separate ribbon and a recessed center roof crease. Crown concavity is faceted, not a smooth thin felt shell.
- Resource states: `icon` (1 direction), `equipped-HELMET` (4 directions)
- License: CC-BY-SA-3.0
- Original copyright, verbatim: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/e7e2fe8c7d4dc0d322745ae5c2fa190b2e010897/icons/mob/humans/onmob/clothing/head/formal_hats.dmi, https://github.com/cmss13-devs/cmss13/blob/e7e2fe8c7d4dc0d322745ae5c2fa190b2e010897/icons/obj/items/clothing/hats/formal_hats.dmi, https://github.com/cmss13-devs/cmss13/blob/e7e2fe8c7d4dc0d322745ae5c2fa190b2e010897/icons/obj/items/clothing/hats/formal_hats.dmi, https://github.com/cmss13-devs/cmss13/blob/e7e2fe8c7d4dc0d322745ae5c2fa190b2e010897/icons/obj/items/clothing/hats/formal_hats.dmi

### CMU3DAU14GogglesM1A1OrangeBallisticWorldCloud

- Prototype: `AU14GogglesM1A1OrangeBallistic`; pose `icon` frame 0; 21 parts
- Definition: [Content.CMU/Resources/Prototypes/CMU14/Entities/Clothing/Factions/USCM-USARMY/goggles.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Prototypes/CMU14/Entities/Clothing/Factions/USCM-USARMY/goggles.yml)
- Resource: [Content.CMU/Resources/Textures/CMU14/Clothing/Goggles/m1a1orange.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Textures/CMU14/Clothing/Goggles/m1a1orange.rsi/meta.json)
- Source: [Content.CMU/Resources/Textures/CMU14/Clothing/Goggles/m1a1orange.rsi/icon.png](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Textures/CMU14/Clothing/Goggles/m1a1orange.rsi/icon.png); SHA-256 `e2feffa551c2dbb52047c9d4da8675004f0be3656001456c775f53a71793da6f`
- Geometry: Orange ballistic goggles face-up with a connected angular wrap frame, wide joined upper amber lens, split lower lenses leaving nose clearance and strap ends. Opaque source colors retained; optical transmission, worn and helmet accessory overlays are not inferred.
- Resource states: `icon` (1 direction), `equipped-MASK` (4 directions), `equipped-EYES` (4 directions), `helmet-down` (4 directions), `helmet` (4 directions)
- License: CC-BY-SA-3.0
- Original copyright, verbatim: Taken from cmss13-pve at https://github.com/cmss13-devs/cmss13-pve/blob/3a8fe887279b11a6de4dfef2ffb58779e2a26876/icons/obj/items/clothing/glasses.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/3a8fe887279b11a6de4dfef2ffb58779e2a26876/icons/mob/humans/onmob/eyes.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/9c26d36fdcba53dc47dd2007d4532bf9806c9e79/icons/mob/humans/onmob/helmet_garb.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/3a8fe887279b11a6de4dfef2ffb58779e2a26876/icons/mob/humans/onmob/eyes.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/3a8fe887279b11a6de4dfef2ffb58779e2a26876/icons/mob/humans/onmob/eyes.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/9c26d36fdcba53dc47dd2007d4532bf9806c9e79/icons/mob/humans/onmob/helmet_garb.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/3a8fe887279b11a6de4dfef2ffb58779e2a26876/icons/mob/humans/onmob/eyes.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/9c26d36fdcba53dc47dd2007d4532bf9806c9e79/icons/mob/humans/onmob/helmet_garb.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/3a8fe887279b11a6de4dfef2ffb58779e2a26876/icons/mob/humans/onmob/eyes.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/9c26d36fdcba53dc47dd2007d4532bf9806c9e79/icons/mob/humans/onmob/helmet_garb.dmi

### CMU3DAU14HatUSArmyWorldCloud

- Prototype: `AU14HatUSArmy`; pose `icon` frame 0; 34 parts
- Definition: [Content.CMU/Resources/Prototypes/CMU14/Entities/Clothing/Factions/USCM-USARMY/hats.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Prototypes/CMU14/Entities/Clothing/Factions/USCM-USARMY/hats.yml)
- Resource: [Content.CMU/Resources/Textures/CMU14/Clothing/US_Army/Hats/cavalry.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Textures/CMU14/Clothing/US_Army/Hats/cavalry.rsi/meta.json)
- Source: [Content.CMU/Resources/Textures/CMU14/Clothing/US_Army/Hats/cavalry.rsi/icon.png](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Textures/CMU14/Clothing/US_Army/Hats/cavalry.rsi/icon.png); SHA-256 `8269af96045e4b1c819ed0208f6a06a647260bf06d73683ad2ba58b319d48063`
- Geometry: Small dark cavalry hat with connected open crown, turned annular brim, gold cord and original gold front badge. Faceted sides and a shallow closed roof are a primitive approximation.
- Resource states: `equipped-HELMET` (4 directions), `icon` (1 direction)
- License: CC-BY-SA-3.0
- Original copyright, verbatim: Taken from CM-SS13 at https://github.com/Steelpoint/cmss13/blob/1c026464019b7baae4975e4b1470a18b37dd1d1c/icons/obj/items/clothing/hats/hats_by_faction/UA.dmi, https://github.com/Steelpoint/cmss13/blob/1c026464019b7baae4975e4b1470a18b37dd1d1c/icons/mob/humans/onmob/clothing/head/hats_by_faction/UA.dmi

### CMU3DAU14UNISCBeretWorldCloud

- Prototype: `AU14UNISCBeret`; pose `icon` frame 0; 15 parts
- Definition: [Content.CMU/Resources/Prototypes/CMU14/Entities/Clothing/Factions/UNISC/hats.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Prototypes/CMU14/Entities/Clothing/Factions/UNISC/hats.yml)
- Resource: [Content.CMU/Resources/Textures/CMU14/Clothing/UNISC/beret.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Textures/CMU14/Clothing/UNISC/beret.rsi/meta.json)
- Source: [Content.CMU/Resources/Textures/CMU14/Clothing/UNISC/beret.rsi/icon.png](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Textures/CMU14/Clothing/UNISC/beret.rsi/icon.png); SHA-256 `9218ab0624b5c682c04e257e6f0183a7d559df24b63526208148b79f62f6c48e`
- Geometry: Loose beret with one smooth slumped convex crown and a connected open lower band. The upper crown is a shallow solid approximation; a complete thin concave cloth shell is outside the primitive contract. Original tiny insignia retained where present.
- Resource states: `icon` (1 direction), `equipped-HELMET` (4 directions)
- License: CC-BY-SA-3.0
- Original copyright, verbatim: Taken from cmss13 at https://github.com/psyendrocronologicalwarfare/cmss13/blob/c59a3a9110af0ee0a6f2ffcc5a2257826df455ce/icons/mob/humans/onmob/clothing/head/hats_by_faction/UNISC.dmi, https://github.com/psyendrocronologicalwarfare/cmss13/blob/97da1564412aec3acc18976c131b0866f8178f2a/icons/obj/items/clothing/suits/suits_by_faction/UNISC.dmi, https://github.com/psyendrocronologicalwarfare/cmss13/blob/97da1564412aec3acc18976c131b0866f8178f2a/icons/obj/items/clothing/suits/suits_by_faction/UNISC.dmi, https://github.com/psyendrocronologicalwarfare/cmss13/blob/97da1564412aec3acc18976c131b0866f8178f2a/icons/obj/items/clothing/suits/suits_by_faction/UNISC.dmi, https://github.com/psyendrocronologicalwarfare/cmss13/blob/97da1564412aec3acc18976c131b0866f8178f2a/icons/obj/items/clothing/suits/suits_by_faction/UNISC.dmi

### CMU3DAU14USASFSECFORBeretWorldCloud

- Prototype: `AU14USASFSECFORBeret`; pose `icon` frame 0; 15 parts
- Definition: [Content.CMU/Resources/Prototypes/CMU14/Entities/Clothing/Factions/LACN-USASFSECFOR/hats.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Prototypes/CMU14/Entities/Clothing/Factions/LACN-USASFSECFOR/hats.yml)
- Resource: [Content.CMU/Resources/Textures/CMU14/Clothing/USASFSecurityForces/beret.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Textures/CMU14/Clothing/USASFSecurityForces/beret.rsi/meta.json)
- Source: [Content.CMU/Resources/Textures/CMU14/Clothing/USASFSecurityForces/beret.rsi/icon.png](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Textures/CMU14/Clothing/USASFSecurityForces/beret.rsi/icon.png); SHA-256 `5d1c9855ffcf30365275c70c44fa92896e5933f19403e04f480b0159131ab567`
- Geometry: Loose beret with one smooth slumped convex crown and a connected open lower band. The upper crown is a shallow solid approximation; a complete thin concave cloth shell is outside the primitive contract. Original tiny insignia retained where present.
- Resource states: `icon` (1 direction), `equipped-HELMET` (4 directions)
- License: CC-BY-SA-3.0
- Original copyright, verbatim: Made by patogrone on discord for AU14

### CMU3DCMHeadCapSPPUshankaWorldCloud

- Prototype: `CMHeadCapSPPUshanka`; pose `icon` frame 0; 14 parts
- Definition: [Resources/Prototypes/_RMC14/Entities/Clothing/Head/spp.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Prototypes/_RMC14/Entities/Clothing/Head/spp.yml)
- Resource: [Resources/Textures/_RMC14/Objects/Clothing/Head/SPP/ushanka.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Clothing/Head/SPP/ushanka.rsi/meta.json)
- Source: [Resources/Textures/_RMC14/Objects/Clothing/Head/SPP/ushanka.rsi/icon.png](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Clothing/Head/SPP/ushanka.rsi/icon.png); SHA-256 `ae7e2e306689e7393c0e5eb11b345697b088650fffc42046ab8b86594eed5c4a`
- Geometry: UPP ushanka with a hollow connected cap band, broad front fur facing, soft roof and lowered side earflaps leaving a real central opening. Exact default unfolded pose; the reciprocal icon-up partner is separate. Fur texture and full concave shell are simplified.
- Resource states: `icon` (1 direction), `icon-up` (1 direction), `equipped-HELMET` (4 directions), `up-equipped-HELMET` (4 directions)
- License: CC-BY-SA-3.0
- Original copyright, verbatim: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/06d35efb20e830eacc57d3c77fea047dadbcdee3/icons/obj/items/clothing/cm_hats.dmi, https://github.com/cmss13-devs/cmss13/blob/06d35efb20e830eacc57d3c77fea047dadbcdee3/icons/mob/humans/onmob/head_1.dmi

### CMU3DCMHeadCapSPPUshankaFoldedWorldCloud

- Prototype: `CMHeadCapSPPUshanka`; pose `icon-up` frame 0; 14 parts
- Definition: [Resources/Prototypes/_RMC14/Entities/Clothing/Head/spp.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Prototypes/_RMC14/Entities/Clothing/Head/spp.yml)
- Resource: [Resources/Textures/_RMC14/Objects/Clothing/Head/SPP/ushanka.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Clothing/Head/SPP/ushanka.rsi/meta.json)
- Source: [Resources/Textures/_RMC14/Objects/Clothing/Head/SPP/ushanka.rsi/icon-up.png](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Clothing/Head/SPP/ushanka.rsi/icon-up.png); SHA-256 `df017dbc33ffbb7a344c1d9b32fe8a4dffe68597bc96b474d8c97084496041a1`
- Geometry: UPP ushanka with a hollow connected cap band, broad front fur facing, soft roof and raised earflaps in the actual icon-up folded pose. Fur texture and full concave shell are simplified.
- Resource states: `icon` (1 direction), `icon-up` (1 direction), `equipped-HELMET` (4 directions), `up-equipped-HELMET` (4 directions)
- License: CC-BY-SA-3.0
- Original copyright, verbatim: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/06d35efb20e830eacc57d3c77fea047dadbcdee3/icons/obj/items/clothing/cm_hats.dmi, https://github.com/cmss13-devs/cmss13/blob/06d35efb20e830eacc57d3c77fea047dadbcdee3/icons/mob/humans/onmob/head_1.dmi

### CMU3DCMHeadDressBluesWorldCloud

- Prototype: `CMHeadDressBlues`; pose `icon` frame 0; 17 parts
- Definition: [Resources/Prototypes/_RMC14/Entities/Clothing/Head/Helmets/hats.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Prototypes/_RMC14/Entities/Clothing/Head/Helmets/hats.yml)
- Resource: [Resources/Textures/_RMC14/Objects/Clothing/Head/Hats/DressBlues/enlisted.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Clothing/Head/Hats/DressBlues/enlisted.rsi/meta.json)
- Source: [Resources/Textures/_RMC14/Objects/Clothing/Head/Hats/DressBlues/enlisted.rsi/icon.png](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Clothing/Head/Hats/DressBlues/enlisted.rsi/icon.png); SHA-256 `8de014f7acee263011fef4450b74bb4d1daa54d8c19d999a1f583274e262e594`
- Geometry: Dress-blues cap with an open dark headband, broad single white crown, separate black visor and original gold insignia. Thin cap roof curvature and hidden construction are inferred.
- Resource states: `icon` (1 direction), `equipped-HELMET` (4 directions)
- License: CC-BY-SA-3.0
- Original copyright, verbatim: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/05eaa3484b1a35aa759300bcfc8f0119f009daae/icons/mob/humans/onmob/head_1.dmi and https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/items/clothing/cm_hats.dmi

### CMU3DClothingHeadFishCapWorldCloud

- Prototype: `ClothingHeadFishCap`; pose `icon` frame 0; 16 parts
- Definition: [Resources/Prototypes/Entities/Clothing/Head/hats.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Prototypes/Entities/Clothing/Head/hats.yml)
- Resource: [Resources/Textures/Clothing/Head/Hats/fishcap.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/Clothing/Head/Hats/fishcap.rsi/meta.json)
- Source: [Resources/Textures/Clothing/Head/Hats/fishcap.rsi/icon.png](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/Clothing/Head/Hats/fishcap.rsi/icon.png); SHA-256 `1c587c7e3ec107030a849413de24853e5cbb711cecc70ccf621755343785ccf0`
- Geometry: Fish cap resting on its side: broad green crown, thin connected bill and source-exact printed white front panel. No invented wording or fish motif. The convex crown top and hidden shell are simplified.
- Resource states: `icon` (1 direction), `equipped-HELMET` (4 directions), `inhand-left` (4 directions), `inhand-right` (4 directions)
- License: CC-BY-SA-3.0
- Original copyright, verbatim: Taken from tgstation closed pull at https://github.com/tgstation/tgstation/pull/66796 and modified by emisse for ss14

### CMU3DClothingHeadHatBeretMedicWorldCloud

- Prototype: `ClothingHeadHatBeretMedic`; pose `icon` frame 0; 14 parts
- Definition: [Resources/Prototypes/Entities/Clothing/Head/hats.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Prototypes/Entities/Clothing/Head/hats.yml)
- Resource: [Resources/Textures/Clothing/Head/Hats/beret_medic.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/Clothing/Head/Hats/beret_medic.rsi/meta.json)
- Source: [Resources/Textures/Clothing/Head/Hats/beret_medic.rsi/icon.png](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/Clothing/Head/Hats/beret_medic.rsi/icon.png); SHA-256 `440683d853e5166d97db10f53f5de4643156964c3fdf9b979246367bf274244e`
- Geometry: Loose beret with one smooth slumped convex crown and a connected open lower band. The upper crown is a shallow solid approximation; a complete thin concave cloth shell is outside the primitive contract. Original tiny insignia retained where present.
- Resource states: `icon` (1 direction), `equipped-HELMET` (4 directions), `inhand-left` (4 directions), `inhand-right` (4 directions)
- License: CC-BY-SA-3.0
- Original copyright, verbatim: Sprited by Hülle#2562 (Discord), resprite by icekot8

### CMU3DClothingHeadHatPartyBlueWorldCloud

- Prototype: `ClothingHeadHatPartyBlue`; pose `icon` frame 0; 47 parts
- Definition: [Resources/Prototypes/Entities/Clothing/Head/hats.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Prototypes/Entities/Clothing/Head/hats.yml)
- Resource: [Resources/Textures/Clothing/Head/Hats/party_blue.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/Clothing/Head/Hats/party_blue.rsi/meta.json)
- Source: [Resources/Textures/Clothing/Head/Hats/party_blue.rsi/icon.png](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/Clothing/Head/Hats/party_blue.rsi/icon.png); SHA-256 `99597fcbdda96525ddb230ef4c20f51fe86824343cc9d05774f954abe0bb47ab`
- Geometry: Eight-sided hollow paper party cone built from connected triangular panels, with a real open base and white wrapping bands. The primitive contract has no true conical shell or UV-wrapped curved surface; angular spiral bands are an explicit approximation of source diagonal stripes.
- Resource states: `icon` (1 direction), `equipped-HELMET-hamster` (4 directions), `equipped-HELMET` (4 directions), `inhand-left` (4 directions), `inhand-right` (4 directions)
- License: CC-BY-SA-3.0
- Original copyright, verbatim: Created by netwy(583844759429316618), Inhands by Prole0 (GitHub)

### CMU3DClothingHeadHatPartyGreenWorldCloud

- Prototype: `ClothingHeadHatPartyGreen`; pose `icon` frame 0; 47 parts
- Definition: [Resources/Prototypes/Entities/Clothing/Head/hats.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Prototypes/Entities/Clothing/Head/hats.yml)
- Resource: [Resources/Textures/Clothing/Head/Hats/party_green.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/Clothing/Head/Hats/party_green.rsi/meta.json)
- Source: [Resources/Textures/Clothing/Head/Hats/party_green.rsi/icon.png](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/Clothing/Head/Hats/party_green.rsi/icon.png); SHA-256 `53d15a6486dfa7a325a624d46ead6de6e524016876cadbc4cbfb6474dee4a13e`
- Geometry: Eight-sided hollow paper party cone built from connected triangular panels, with a real open base and white wrapping bands. The primitive contract has no true conical shell or UV-wrapped curved surface; angular spiral bands are an explicit approximation of source diagonal stripes.
- Resource states: `icon` (1 direction), `equipped-HELMET-hamster` (4 directions), `equipped-HELMET` (4 directions), `inhand-left` (4 directions), `inhand-right` (4 directions)
- License: CC-BY-SA-3.0
- Original copyright, verbatim: Created by netwy(583844759429316618), Inhands by Prole0 (GitHub)

### CMU3DClothingHeadHatPartyRedWorldCloud

- Prototype: `ClothingHeadHatPartyRed`; pose `icon` frame 0; 47 parts
- Definition: [Resources/Prototypes/Entities/Clothing/Head/hats.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Prototypes/Entities/Clothing/Head/hats.yml)
- Resource: [Resources/Textures/Clothing/Head/Hats/party_red.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/Clothing/Head/Hats/party_red.rsi/meta.json)
- Source: [Resources/Textures/Clothing/Head/Hats/party_red.rsi/icon.png](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/Clothing/Head/Hats/party_red.rsi/icon.png); SHA-256 `caf8f83d32ff3dc4ee69c8e071fc324afbcd5872d71ec5ba7d7cacdc740aaf8b`
- Geometry: Eight-sided hollow paper party cone built from connected triangular panels, with a real open base and white wrapping bands. The primitive contract has no true conical shell or UV-wrapped curved surface; angular spiral bands are an explicit approximation of source diagonal stripes.
- Resource states: `icon` (1 direction), `equipped-HELMET-hamster` (4 directions), `equipped-HELMET` (4 directions), `inhand-left` (4 directions), `inhand-right` (4 directions)
- License: CC-BY-SA-3.0
- Original copyright, verbatim: Created by netwy(583844759429316618), Inhands by Prole0 (GitHub)

### CMU3DClothingHeadHatWeldingWorldCloud

- Prototype: `ClothingHeadHatWelding`; pose `icon` frame 0; 13 parts
- Definition: [Resources/Prototypes/Entities/Clothing/Head/welding.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Prototypes/Entities/Clothing/Head/welding.yml)
- Resource: [Resources/Textures/Clothing/Head/Welding/welding.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/Clothing/Head/Welding/welding.rsi/meta.json)
- Source: [Resources/Textures/Clothing/Head/Welding/welding.rsi/icon.png](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/Clothing/Head/Welding/welding.rsi/icon.png); SHA-256 `954afe9f7e947336cf8109a7b47f8587e253a07a0da9bfaf4063f1f6fea18cb8`
- Geometry: Welding shield lying face-up, with connected upper/lower shield plates and raised sides, rectangular optical bezel, original recessed tinted lens and two side-strap ends. icon-up exists in the RSI but this prototype has no Foldable component; no automatic raised-state binding is invented.
- Resource states: `icon` (1 direction), `icon-up` (1 direction), `equipped-HELMET` (4 directions), `up-equipped-HELMET` (4 directions), `equipped-HELMET-vox` (4 directions), `up-equipped-HELMET-vox` (4 directions), `equipped-HELMET-hamster` (4 directions), `up-equipped-HELMET-hamster` (4 directions), `inhand-left` (4 directions), `inhand-right` (4 directions), `up-inhand-left` (4 directions), `up-inhand-right` (4 directions), `equipped-HELMET-vulpkanin` (4 directions), `up-equipped-HELMET-vulpkanin` (4 directions)
- License: CC-BY-SA-3.0
- Original copyright, verbatim: Taken from /vg/station at commit https://github.com/vgstation-coders/vgstation13/commit/31d6576ba8102135d058ef49c3cb6ecbe8db8a79 | vulpkanin version taken from Paradise station at https://github.com/ParadiseSS13/Paradise/commit/f0fa4e1fd809482fbc104a310aa34cebf7df157d

### CMU3DRMCGlassesMedicalHUDGlassesWorldCloud

- Prototype: `RMCGlassesMedicalHUDGlasses`; pose `icon-on` frame 0; 21 parts
- Definition: [Resources/Prototypes/_RMC14/Entities/Clothing/Eyes/glasses.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Prototypes/_RMC14/Entities/Clothing/Eyes/glasses.yml)
- Resource: [Resources/Textures/_RMC14/Objects/Clothing/Eyes/Glasses/medicalhud.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Clothing/Eyes/Glasses/medicalhud.rsi/meta.json)
- Source: [Resources/Textures/_RMC14/Objects/Clothing/Eyes/Glasses/medicalhud.rsi/icon-on.png](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Clothing/Eyes/Glasses/medicalhud.rsi/icon-on.png); SHA-256 `5bd2e8b2382d236391c22d89a6a816676cbcc3ba37d077c2bbf44a1be485331d`
- Geometry: HealthMate optical HUD, activated icon-on first-frame study with original teal monocle pixels. Connected long temple, upright electronics, thin upper brackets and hooked lower support retain source gaps. Default activated=true and four icon-on frames are recorded; blinking and runtime toggle selection are not implemented.
- Resource states: `icon` (1 direction), `icon-on` (1 direction), `on-equipped-EYES` (4 directions), `equipped-EYES` (4 directions)
- License: CC-BY-SA-3.0
- Original copyright, verbatim: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/f2b3774f6ca9173e76e7783d88e3c2f765cd385f/icons/obj/items/clothing/glasses.dmi and https://github.com/cmss13-devs/cmss13/blob/0c23c26bc85b7ab45fdb212c84807f0215c0a53e/icons/mob/humans/onmob/eyes.dmi

### CMU3DRMCGlassesMedicalHUDGlassesOffWorldCloud

- Prototype: `RMCGlassesMedicalHUDGlasses`; pose `icon` frame 0; 18 parts
- Definition: [Resources/Prototypes/_RMC14/Entities/Clothing/Eyes/glasses.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Prototypes/_RMC14/Entities/Clothing/Eyes/glasses.yml)
- Resource: [Resources/Textures/_RMC14/Objects/Clothing/Eyes/Glasses/medicalhud.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Clothing/Eyes/Glasses/medicalhud.rsi/meta.json)
- Source: [Resources/Textures/_RMC14/Objects/Clothing/Eyes/Glasses/medicalhud.rsi/icon.png](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Clothing/Eyes/Glasses/medicalhud.rsi/icon.png); SHA-256 `05bedbec7b3af2c0f6dbd7ff0f6236e9e60494976f45d4675d8d621f69e25442`
- Geometry: HealthMate optical HUD, deactivated icon pose without the active monocle disc. Connected long temple, upright electronics, thin upper brackets and hooked lower support retain source gaps. Default activated=true and four icon-on frames are recorded; blinking and runtime toggle selection are not implemented.
- Resource states: `icon` (1 direction), `icon-on` (1 direction), `on-equipped-EYES` (4 directions), `equipped-EYES` (4 directions)
- License: CC-BY-SA-3.0
- Original copyright, verbatim: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/f2b3774f6ca9173e76e7783d88e3c2f765cd385f/icons/obj/items/clothing/glasses.dmi and https://github.com/cmss13-devs/cmss13/blob/0c23c26bc85b7ab45fdb212c84807f0215c0a53e/icons/mob/humans/onmob/eyes.dmi

### CMU3DRMCGlassesTriMaxYellowFakeWorldCloud

- Prototype: `RMCGlassesTriMaxYellowFake`; pose `icon` frame 0; 11 parts
- Definition: [Resources/Prototypes/_RMC14/Entities/Clothing/Eyes/glasses.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Prototypes/_RMC14/Entities/Clothing/Eyes/glasses.yml)
- Resource: [Resources/Textures/_RMC14/Objects/Clothing/Eyes/Glasses/trimax_yellow_glasses.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Clothing/Eyes/Glasses/trimax_yellow_glasses.rsi/meta.json)
- Source: [Resources/Textures/_RMC14/Objects/Clothing/Eyes/Glasses/trimax_yellow_glasses.rsi/icon.png](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Clothing/Eyes/Glasses/trimax_yellow_glasses.rsi/icon.png); SHA-256 `689bc5d61289b9ee7af0039c6b22a2a76429a4ba680255329502a2b0a1113cc0`
- Geometry: TriMax yellow glasses with paired broad angular yellow lenses, straight dark upper rims, a separate nose bridge and folded temple arm. Opaque source colors retained; lens transparency and worn shapes unverified.
- Resource states: `icon` (1 direction), `equipped-EYES` (4 directions), `equipped-MASK` (4 directions)
- License: CC-BY-SA-3.0
- Original copyright, verbatim: Taken from CM-SS13 at https://github.com/cmss13-devs/cmss13/tree/master/icons/mob/humans/onmob/clothing/glasses/glasses.dmi

### CMU3DRMCHeadBeretTSEWorldCloud

- Prototype: `RMCHeadBeretTSE`; pose `icon` frame 0; 15 parts
- Definition: [Resources/Prototypes/_RMC14/Entities/Clothing/Head/tse.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Prototypes/_RMC14/Entities/Clothing/Head/tse.yml)
- Resource: [Resources/Textures/_RMC14/Objects/Clothing/Head/TSE/tse_beret.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Clothing/Head/TSE/tse_beret.rsi/meta.json)
- Source: [Resources/Textures/_RMC14/Objects/Clothing/Head/TSE/tse_beret.rsi/icon.png](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Clothing/Head/TSE/tse_beret.rsi/icon.png); SHA-256 `d419cc5eb708943cff47cc445b201f3a5dbcd411c05470b170cb59054fe3cf9a`
- Geometry: Loose beret with one smooth slumped convex crown and a connected open lower band. The upper crown is a shallow solid approximation; a complete thin concave cloth shell is outside the primitive contract. Original tiny insignia retained where present.
- Resource states: `equipped-HELMET` (4 directions), `icon` (1 direction)
- License: CC-BY-SA-4.0
- Original copyright, verbatim: Sprites by github noctyrnal

### CMU3DRMCHeadCapFedoraBrownWorldCloud

- Prototype: `RMCHeadCapFedoraBrown`; pose `icon` frame 0; 53 parts
- Definition: [Resources/Prototypes/_RMC14/Entities/Clothing/Head/Helmets/hats.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Prototypes/_RMC14/Entities/Clothing/Head/Helmets/hats.yml)
- Resource: [Resources/Textures/_RMC14/Objects/Clothing/Head/Hats/Fedora/brown.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Clothing/Head/Hats/Fedora/brown.rsi/meta.json)
- Source: [Resources/Textures/_RMC14/Objects/Clothing/Head/Hats/Fedora/brown.rsi/icon.png](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Clothing/Head/Hats/Fedora/brown.rsi/icon.png); SHA-256 `33936e16475998682df94021273da0246401cc200cc9200b454b94be389400cf`
- Geometry: Fedora with broad planar annular brim, eight connected crown panels, dark separate ribbon and a recessed center roof crease. Crown concavity is faceted, not a smooth thin felt shell.
- Resource states: `equipped-HELMET` (4 directions), `icon` (1 direction)
- License: CC-BY-SA-3.0
- Original copyright, verbatim: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/d369de9f7ff36055312d855e4473a3f1a3322540/icons/mob/humans/onmob/clothing/head/formal_hats.dmi, https://github.com/cmss13-devs/cmss13/blob/d369de9f7ff36055312d855e4473a3f1a3322540/icons/obj/items/clothing/hats/formal_hats.dmi

### CMU3DRMCHeadFedoraBrownWorldCloud

- Prototype: `RMCHeadFedoraBrown`; pose `icon` frame 0; 53 parts
- Definition: [Resources/Prototypes/_RMC14/Entities/Clothing/Head/Helmets/hats.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Prototypes/_RMC14/Entities/Clothing/Head/Helmets/hats.yml)
- Resource: [Resources/Textures/_RMC14/Objects/Clothing/Head/Hats/Fedora/brown_fedora.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Clothing/Head/Hats/Fedora/brown_fedora.rsi/meta.json)
- Source: [Resources/Textures/_RMC14/Objects/Clothing/Head/Hats/Fedora/brown_fedora.rsi/icon.png](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Clothing/Head/Hats/Fedora/brown_fedora.rsi/icon.png); SHA-256 `40e9d51661663e08dd644938b37bd522e1a2fdced1702e5276ca0786663ad128`
- Geometry: Fedora with broad planar annular brim, eight connected crown panels, dark separate ribbon and a recessed center roof crease. Crown concavity is faceted, not a smooth thin felt shell.
- Resource states: `icon` (1 direction), `equipped-HELMET` (4 directions)
- License: CC-BY-SA-3.0
- Original copyright, verbatim: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/e0f06e0a3383e56331e9315b3aed1c8c2041ebd0/icons/obj/items/clothing/hats/formal_hats.dmi and https://github.com/cmss13-devs/cmss13/blob/e0f06e0a3383e56331e9315b3aed1c8c2041ebd0/icons/mob/humans/onmob/clothing/head/formal_hats.dmi

### CMU3DRMCHeadUNMCHeadsetWorldCloud

- Prototype: `RMCHeadUNMCHeadset`; pose `icon` frame 0; 13 parts
- Definition: [Resources/Prototypes/_RMC14/Entities/Clothing/Head/faction_hats.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Prototypes/_RMC14/Entities/Clothing/Head/faction_hats.yml)
- Resource: [Resources/Textures/_RMC14/Objects/Clothing/Head/Hats/unmc_headset.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Clothing/Head/Hats/unmc_headset.rsi/meta.json)
- Source: [Resources/Textures/_RMC14/Objects/Clothing/Head/Hats/unmc_headset.rsi/icon.png](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Clothing/Head/Hats/unmc_headset.rsi/icon.png); SHA-256 `26a0a50dc0bf2767c34b0511cb50be756dc719785eba9fe088cc91608a1a48b7`
- Geometry: UNMC headset laid flat: open segmented metal bow, two separate padded earcups and connected downturned microphone arm. No head-sized solid fill; worn helmet/hat placements remain separate unsupported source art.
- Resource states: `icon` (1 direction), `equipped-HELMET` (4 directions), `helmet` (4 directions), `hat` (4 directions)
- License: CC-BY-SA-3.0
- Original copyright, verbatim: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/pull/10310. hat accessory added by SharkSnake98

## Copyright and redistribution

The original artwork and all cropped pixels retain their source attribution and license. Keep this file and the referenced RSI metadata with redistributed derivatives. Source provenance is not a license grant beyond those original terms. The TSE beret source is CC-BY-SA-4.0; the other referenced RSI sources in this batch declare CC-BY-SA-3.0.

## Files

- Canonical definitions: `garrison_headwear_extra_cloud.yml` and `garrison_headwear_extra_cloud_art.yml`
- Texture folder: `Content.CMU/Resources/Textures/CMU14/ThreeD/headwear_extra_cloud/`
- Portable GLBs: the 25 model IDs above under `Content.CMU/Resources/Models/CMU14/Garrison/`
- Review/evidence: `Tools/three_d/generated/cloud-review/headwear-extra/`
- Scoped hash manifest: `Tools/three_d/generated/cloud-review/headwear-extra/family-manifest.json`
