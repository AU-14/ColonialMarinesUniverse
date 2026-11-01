# Garrison dropped equipment and static display mannequin art

Eight draft assemblies cover the ten explicitly assigned prototype IDs. They are original solid-part art studies based on inspected repository definitions and actual RSI pixels from `TheHellFireo/CMU-Garrison-3D`, ref `Chip/garrison-3d`. This batch does not change source game prototypes, maps, engine/runtime code, character bodies, worn clothing, multiplayer behavior or publishing state.

## Model/reference scope

### CMU3DM4RArmorCloud
- Exact source IDs: `RMCArmorM4RMedium`
- RSI: `/Textures/_RMC14/Objects/Clothing/OuterClothing/CMB/cmb_heavy_armor.rsi`; state `icon`; source directions 1
- Dropped armor art study with separate chest/groin panels, shoulder plates, webbing and the source-default South lamp-off composition. Base icon has one direction; inherited lamp-off has four (E is transparent). This layered owner is not single-layer-adapter compatible. Only this default South composition is modeled; other lamp directions, lamp-on/blink, stains, accessories, contents and worn art remain unsupported. Panel curvature, thickness and rear are inferred.
- 24 editable primitive parts; status `draft`

### CMU3DRiotArmorCloud
- Exact source IDs: `CMArmorRiot`
- RSI: `/Textures/_RMC14/Objects/Clothing/OuterClothing/Armor/riot.rsi`; state `icon`; source directions 1
- Clean loose icon only, with open neck yoke, distinct chest/abdominal/arm plates, brown retaining straps and two source-separated shin guards. Source icon one direction. Curvature, underside and physical thickness inferred. Stains, accessories, all equipped appearance and animation unsupported.
- 26 editable primitive parts; status `draft`

### CMU3DRiotHelmetCloud
- Exact source IDs: `ArmorHelmetRiot`
- RSI: `/Textures/_RMC14/Objects/Clothing/Head/Helmets/riot.rsi`; state `icon`; source directions 1
- Dropped icon with open underside, physically hollow shell rings, cap, separate ear guards and original white/red brow stripes. FoldableClothing changes hidden hair layers, not a second world RSI icon; no invented folded helmet or actor geometry. Interior shell thickness/back inferred. Smooth patch boundaries remain slightly scalloped and unresolved. Helmet accessories, worn visibility and live interaction unsupported.
- 94 editable primitive parts; status `draft`

### CMU3DCombatBootsCloud
- Exact source IDs: `RMCShoesCombat`
- RSI: `/Textures/_RMC14/Objects/Clothing/Shoes/Boots/swat.rsi`; state `icon`; source directions 1
- Empty dropped icon only. Actual CMInventorySystem hides filled for empty item slots; no startingItem is inherited. Filled overlay, concealed item, worn appearance and live fill selection unsupported. Two soles, toe boxes, separate quarter walls, open padded collars and laces. Dimensions/depth inferred.
- 62 editable primitive parts; status `draft`

### CMU3DLaceupShoesCloud
- Exact source IDs: `RMCShoesLaceup`, `RMCShoesLaceupDress`
- RSI: `/Textures/_RMC14/Objects/Clothing/Shoes/laceup.rsi`; state `icon`; source directions 1
- Both exact IDs inherit identical one-direction icon. Separate two shoe volumes with soles, quarters, polished toes, laces and open leather-lined collars. Concealed contents and all worn clothing unsupported. Physical thickness, hidden surfaces and paired depth inferred.
- 54 editable primitive parts; status `draft`

### CMU3DBlackShoesCloud
- Exact source IDs: `ClothingShoesColorBlack`
- RSI: `/Textures/Clothing/Shoes/color.rsi`; state `icon`; source directions 1
- Dropped source is icon tinted #3f3f3f plus unchanged soles-icon; worn state uses a different tint and is excluded. Two physical shoe volumes with separate bright toe/sole inserts. Multi-layer source composition is not a single-layer spriteStates binding. Side/back and physical thickness inferred.
- 32 editable primitive parts; status `draft`

### CMU3DGenericHeadsetCloud
- Exact source IDs: `AU14CMBHeadset`, `CMHeadsetColony`
- RSI: `/Textures/_RMC14/Objects/Clothing/headsets.rsi`; state `generic_headset`; source directions 1
- Exact generic_headset shared by both requested source IDs. The AU14CMBHeadset definition does not use cmb_headset. Source-proven monaural cup, open curved upper band and lower microphone boom modeled as separate volumes. Hidden thickness and band construction inferred; encryption key contents, equipped overlays and wearer unsupported.
- 23 editable primitive parts; status `draft`

### CMU3DDisplayMannequinCloud
- Exact source IDs: `RMCMannequin`
- RSI: `/Textures/_RMC14/Structures/Furniture/mannequin.rsi`; state `mannequin`; source directions 4
- Static pale display mannequin base from all four source mannequin directions. Source is a humanoid-shaped furniture object with feet, rigid segmented limbs and dark eye marks; no pedestal is present. All clothing inventory slots default empty. No live character body, actor rig, clothing attachments or animations are created. Broad silhouette follows source; absolute scale, upright height, curved volumes, joint section and hidden surfaces inferred. Empty resource-less clothing layers are not treated as a supported mannequin adapter.
- 41 editable primitive parts; status `draft`

## Source-owner and appearance findings

- RMCArmorM4RMedium inherits three layers from RMCBaseMarineArmorLightNoAccessory: `icon`, visible `armor_overlays.rsi/lamp-off`, and initially hidden `lamp-on`. Lamp-off has four source frames in RSI order South/North/East/West; East is entirely transparent. The exported art is the default South off-lamp study. It deliberately has no spriteStates binding. The base icon reference remains one direction, and the overlay direction mismatch remains explicitly unsupported. No lamp-on/blink or emission behavior is asserted.
- CMArmorRiot uses one icon plus possible dynamic stain/accessory owners. The clean, unadorned icon has a bounded static spriteStates contract; any actual added layer must keep the existing fallback. Two detached shin guards are present in the dropped pixels and are modeled separately.
- ArmorHelmetRiot inherits FoldableClothing. Its source metadata has only `icon` and `equipped-HELMET`; the foldable settings change hidden hair/head layers. No second world icon or physical fold was invented. The hollow shell and attached curved source brow stripes are a physical interpretation, not a claim of unseen exact construction.
- RMCShoesCombat inherits `icon` and `filled`, but inherits no startingItem. Content.Client/_RMC14/Inventory/CMInventorySystem.cs UpdateSlotSprite hides Fill when the actual item slots are empty, and shows it only with a contained entity. The exported boots are empty-icon art. Filled appearance and native selection remain unsupported.
- ClothingShoesColorBlack is explicitly icon multiplied by #3f3f3f followed by unchanged soles-icon. Its worn state uses #1d1d1d and is excluded. The comparison image composes those actual source layers. The solid model interprets their dark uppers and bright sole/toe inserts, with no single-layer binding.
- RMCShoesLaceupDress changes NoSlip only and inherits RMCShoesLaceup artwork. Both receive the same single-icon paired shoe draft. Hidden holstered contents are not visualized.
- AU14CMBHeadset inherits AU14HeadsetGovforMarine which selects generic_headset. It is not the distinct AU14CMBHeadsetOnlyCMB/cmb_headset appearance. CMHeadsetColony has the same generic_headset world state, so both share the same source-shaped monaural cup, open band and microphone model.
- RMCMannequin is a static furniture entity. Its inherited inventory defaults all clothing slots to empty. Its four-direction base sprite depicts a pale articulated humanoid-shaped display form standing on its own feet, with no pedestal. This batch creates that static furniture only; it creates no actor rig or wearable body. The empty clothing placeholders do not establish a supported layered mannequin adapter. Clothing attachments, saved contents and live appearance remain unsupported.

## Geometry and review limits

Armor uses independent plates, physical straps and textile carriers; the M5 shin guards remain separate as in its icon. The helmet has a true open underside, finite-thickness shell patches and separate ear guards. Each footwear pair contains two physical soles, toe volumes, quarter walls and open collars. The headset is not a sprite slab: rods form its band/boom, and the cup/grille has depth. The mannequin has separate static head, torso, limbs, joints and feet.

All physical thicknesses, many reverse surfaces, shoe depths, helmet construction and mannequin absolute dimensions are inferred. Source details constrain the designs but do not establish faithful unseen geometry. No whole-source billboard or full-sprite slab is used. The exact crop surfaces are small chest/brow/lamp details, not replacements for the solid assembly.

## Reproduction and bounded checks

`python Tools/three_d/author_equipment_cloud.py` writes only batch art YAML, derived textures, GLBs, previews and its evidence report through the unchanged build_models.py functions. `python Tools/three_d/verify_equipment_cloud.py` validates the authored models, static source timings, source direction metadata, finite GLB accessors and bounds, deterministic GLB bytes, exact crop bytes, unique atlas/model/source IDs and unmodified source input Git blob hashes.

The report is Tools/three_d/generated/equipment-cloud-verification.json. Eight source-and-orbit cards are under generated/cloud-review; supplemental views inspect the actual helmet underside and all four mannequin directions.

The original inventory lists 38 Redux and 27 classic instances of these ten IDs. These are historical potential source counts only. No saved-map placement, visible entity count, map-fit clearance, native admission, live appearance, full-state coverage, speed/performance or fidelity approval is claimed. Blender independently imports the final SHA-checked GLB bytes with finite mesh geometry, zero invalid-mesh repairs and zero actions; the separate report is generated/equipment-cloud-blender-import.json. Parent-level package checks remain separate.

## Attribution

All nine RSI resources used here declare CC-BY-SA-3.0. Preserve this file, source metadata, and the original source artwork attribution with derived textures and exported art. Modeled forms and derived textures are draft adaptations of the credited pixel art; source-specific credits are reproduced below.

### Resources/Textures/_RMC14/Objects/Clothing/OuterClothing/CMB/cmb_heavy_armor.rsi/meta.json
License: CC-BY-SA-3.0
Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/05eaa3484b1a35aa759300bcfc8f0119f009daae/icons/obj/items/clothing/cm_suits.dmi, https://github.com/cmss13-devs/cmss13/blob/05eaa3484b1a35aa759300bcfc8f0119f009daae/icons/mob/humans/onmob/suit_1.dmi
Source metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Clothing/OuterClothing/CMB/cmb_heavy_armor.rsi/meta.json

### Resources/Textures/_RMC14/Objects/Clothing/OuterClothing/Armor/riot.rsi/meta.json
License: CC-BY-SA-3.0
Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/suit_0.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/items/clothing/suits.dmi
Source metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Clothing/OuterClothing/Armor/riot.rsi/meta.json

### Resources/Textures/_RMC14/Objects/Clothing/Head/Helmets/riot.rsi/meta.json
License: CC-BY-SA-3.0
Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/items/clothing/hats.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/head_0.dmi
Source metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Clothing/Head/Helmets/riot.rsi/meta.json

### Resources/Textures/_RMC14/Structures/Furniture/mannequin.rsi/meta.json
License: CC-BY-SA-3.0
Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/human/species/r_synthetic.dmi
Source metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Structures/Furniture/mannequin.rsi/meta.json

### Resources/Textures/Clothing/Shoes/color.rsi/meta.json
License: CC-BY-SA-3.0
Taken from tgstation at commit https://github.com/tgstation/tgstation/commit/c838ba21dae97db345e0113f99596decd1d66039 and modified by Flareguy for Space Station 14
Source metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/Clothing/Shoes/color.rsi/meta.json

### Resources/Textures/_RMC14/Objects/Clothing/Shoes/Boots/swat.rsi/meta.json
License: CC-BY-SA-3.0
Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/05eaa3484b1a35aa759300bcfc8f0119f009daae/icons/mob/humans/onmob/feet.dmi, https://github.com/cmss13-devs/cmss13/blob/05eaa3484b1a35aa759300bcfc8f0119f009daae/icons/obj/items/clothing/shoes.dmi, https://github.com/cmss13-devs/cmss13/blob/a15efa985114bf92a98cda275aa8319636ae5abe/icons/obj/items/clothing/shoes.dmi
Source metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Clothing/Shoes/Boots/swat.rsi/meta.json

### Resources/Textures/_RMC14/Objects/Clothing/Shoes/laceup.rsi/meta.json
License: CC-BY-SA-3.0
Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/05eaa3484b1a35aa759300bcfc8f0119f009daae/icons/obj/items/clothing/shoes.dmi, https://github.com/cmss13-devs/cmss13/blob/36ab9674a311463bb056f7fab5fe3f3079305569/icons/obj/items/clothing/shoes.dmi
Source metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Clothing/Shoes/laceup.rsi/meta.json

### Resources/Textures/_RMC14/Objects/Clothing/headsets.rsi/meta.json
License: CC-BY-SA-3.0
Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/a767d448e1a73e7f96dea6bebc07deee8d54bdc2/icons/obj/items/radio.dmi and https://github.com/cmss13-devs/cmss13/blob/05eaa3484b1a35aa759300bcfc8f0119f009daae/icons/mob/humans/onmob/head_1.dmi
Source metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Clothing/headsets.rsi/meta.json

### Resources/Textures/_RMC14/Objects/Clothing/OuterClothing/Armor/armor_overlays.rsi/meta.json
License: CC-BY-SA-3.0
Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/81c7806eb705f3a6b43085056cef1be0055d8ed2/icons/mob/humans/onmob/suit_1.dmi, snpr armor and pmc lights by github noctyrnal
Source metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Clothing/OuterClothing/Armor/armor_overlays.rsi/meta.json



## Cumulative atlas assignment
This cumulative snapshot uses centrally allocated, nonconflicting atlas slots. Any authoring-time numeric range in this historical family note is superseded by the canonical surface YAML and `Tools/three_d/generated/cloud-review/atlas-allocation-current.json`. Source IDs, original PNG pixels and modeled geometry are unchanged by atlas-index remapping.
