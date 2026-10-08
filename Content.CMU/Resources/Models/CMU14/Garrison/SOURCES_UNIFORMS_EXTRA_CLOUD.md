# Additional loose uniform cloud drafts

## Scope and delivery

Eighteen editable, source-specific draft assemblies for the assigned missing Redux prototype IDs. They model the static dropped/world base `icon` only, laid out face-up as the source depicts. The shirt, folded trousers, cuffs, collars, seam folds and small details have real overlapping volumes. There is no actor, rig, humanoid attachment, full-sprite slab, alpha-mask extrusion or per-pixel voxel body.

All models use `placement: surface` and preserve entity rotation. At 32 source pixels per tile, cloth thickness, reverse construction and hidden joins are inferred. Strong source highlights are simplified into broad cloth materials and very shallow folded facets; the low-resolution source is not a sewing pattern. The current primitive silhouettes and tailoring remain drafts.

Canonical assets:

- `garrison_uniforms_extra_cloud.yml`: 18 models, 945 named physical parts, 42–58 parts per model
- `garrison_uniforms_extra_cloud_art.yml`: 14 exact three-pixel waist-clasp crops, atlas indices 2751–2764
- `Textures/CMU14/ThreeD/uniforms_extra_cloud/`: only those small original clasp crops
- Adjacent `CMU3D<prototype>WorldCloud.glb` files: direct unchanged `build_models.py` output, never edited after export

The source inventory records 29 Redux and 27 classic placements for these 18 types. These are inventory counts only. No saved map, live renderer, character appearance or engine file was edited, launched or verified. No fidelity approval is claimed.

## Source and default visual ownership

Source repository: [TheHellFireo/CMU-Garrison-3D, Chip/garrison-3d](https://github.com/TheHellFireo/CMU-Garrison-3D/tree/Chip/garrison-3d). Original PNG and complete RSI metadata were retrieved on 2026-10-06 using GitHub `fetch_file`, with base64 for PNGs. They are retained at their original `Content.CMU/Resources/Textures/...` or `Resources/Textures/...` paths. The proof JSON records file checksums, original palettes, source bounds, full state metadata, license and copyright.

Inspected source definitions:

- `Content.CMU/Resources/Prototypes/CMU14/Entities/Clothing/Civilian/jumpsuits.yml`
- `Content.CMU/Resources/Prototypes/CMU14/Entities/Clothing/Factions/LACN-USASFSECFOR/jumpsuits.yml`
- `Resources/Prototypes/_RMC14/Entities/Clothing/Uniforms/{base,civilian,liaison,marines,dressblues}.yml`
- `Resources/Prototypes/Entities/Clothing/base_clothing.yml`
- `Resources/Prototypes/Entities/Objects/base_item.yml`
- `Resources/Prototypes/_RMC14/Entities/Clothing/Accessory/base.yml`

`RMCUniformBase` defines a first Sprite layer of `icon`, followed by the separate WebbingVisualLayers mapping. The inspected child and clothing/uniform parents do not supply a default Sprite color override. Colors here are sampled independently from every actual icon; the four CMU workwear colors and four boilersuit colors share their verified source cut, not an invented tint mapping. BaseItem supplies Items draw depth and `noRot: false`. Each authored `icon` is one direction with no frame delays.

Important exclusions:

- RMCJumpsuitSunRiders inherits the standard jungle icon and ItemCamouflage variations from JumpsuitMarine, and starts with RMCPatchSolarDevils. Only the base jungle icon is modeled. The starting patch, composed current appearance and desert/snow/classic/urban alternatives are not claimed
- RMCJumpsuitKhakiWorkwearJacketless sets Clothing.equippedPrefix and RMCClothingFoldable.activatedPrefix to `jacket`; it does not replace the dropped Sprite `icon`. This draft follows that ordinary icon and makes no jacketless worn-state claim
- Alternate folding/sleeve prefixes and the Colonist FoldableClothing worn prefix are not modeled. Metadata availability of equipped, hand or prefixed states does not establish support
- Webbing, accessories, stains, blood, reagent coloration, runtime tint, all actor clothing layers and any dynamically selected overlays remain unclaimed/existing sprites

## Source-specific cut and material interpretation

- `AU14CivilianBaristaClothes`: Taupe broad short-sleeve shirt with turned V collar and dark hem; longer black folded trousers. The one-pixel lower trouser split stays open. Source: `Content.CMU/Resources/Textures/CMU14/Clothing/Civilian/Uniforms/cuppajoesuniform.rsi/icon.png`
- `AU14CivilianBoilerSuitCyan`: Grey-cyan boilersuit, paired flat chest pockets, center placket, dark waistband, small silver source clasp and matching folded trousers. Source: `Content.CMU/Resources/Textures/CMU14/Clothing/Civilian/Uniforms/cyanboilersuit.rsi/icon.png`
- `AU14CivilianBoilerSuitDarkBlueSynth`: The actual dark navy support-synthetic boiler icon. No patch, robot body or inherited synthetic equipment is invented. Source: `Content.CMU/Resources/Textures/CMU14/Clothing/Civilian/Uniforms/darkblueboilersuit.rsi/icon.png`
- `AU14CivilianBoilerSuitGray`: Neutral grey boiler palette and matching trousers; same inspected boiler cut. Source: `Content.CMU/Resources/Textures/CMU14/Clothing/Civilian/Uniforms/grayboilersuit.rsi/icon.png`
- `AU14CivilianBoilerSuitWhite`: Source white boiler is warm ivory/khaki, not arbitrary pure white. Original warm palette retained. Source: `Content.CMU/Resources/Textures/CMU14/Clothing/Civilian/Uniforms/whiteboilersuit.rsi/icon.png`
- `AU14CivilianWorkwearBlue`: Blue-purple shirt with dark blue folded trousers, brown belt and small original grey clasp. Source: `Content.CMU/Resources/Textures/CMU14/Clothing/Civilian/Uniforms/blueworkwear.rsi/icon.png`
- `AU14CivilianWorkwearGreen`: Muted green shirt, same source dark-blue trousers and brown belt. Source: `Content.CMU/Resources/Textures/CMU14/Clothing/Civilian/Uniforms/greenworkwear.rsi/icon.png`
- `AU14CivilianWorkwearPink`: Muted rose/maroon shirt, same source dark-blue trousers and brown belt. Source: `Content.CMU/Resources/Textures/CMU14/Clothing/Civilian/Uniforms/pinkworkwear.rsi/icon.png`
- `AU14CivilianWorkwearYellow`: Khaki-yellow shirt, same source dark-blue trousers and brown belt. Source: `Content.CMU/Resources/Textures/CMU14/Clothing/Civilian/Uniforms/yellowworkwear.rsi/icon.png`
- `AU14LACNUniformCoat`: Long green cold-weather overcoat with pointed collar, paired chest pockets, dark waist and flared long tails. Black opaque tail pixels are represented by recessed dark lining, not mistaken for transparent air. Source: `Content.CMU/Resources/Textures/CMU14/Clothing/LACN/Uniforms/lacnuniformcoat.rsi/icon.png`
- `CMJumpsuitColonist`: Grey-green boiler shape with both original ochre shoulder bands; no dynamic webbing or worn folds. Source: `Resources/Textures/_RMC14/Objects/Clothing/Uniforms/Survivor/colonist.rsi/icon.png`
- `CMJumpsuitLiaisonField`: Dark blue shirt, dark brown slacks and the source gold waist clasp. Source: `Resources/Textures/_RMC14/Objects/Clothing/Uniforms/Liaison/corporate_field.rsi/icon.png`
- `RMCJumpsuitBlueWorkwear`: Lower-pivot folded dark blue shirt and brown canvas trousers. Its source begins two pixels lower than the CMU blue shirt. Source: `Resources/Textures/_RMC14/Objects/Clothing/Uniforms/Civilian/blue_workwear.rsi/icon.png`
- `RMCJumpsuitKhakiWorkwearJacketless`: Lower-pivot khaki shirt, indigo jeans and brown belt; dropped icon remains the ordinary khaki workwear image. Source: `Resources/Textures/_RMC14/Objects/Clothing/Uniforms/Civilian/khaki_workwear.rsi/icon.png`
- `RMCJumpsuitLiaisonGreenWorkwear`: Lower-pivot dark green shirt with brown canvas trousers and dark belt. Source: `Resources/Textures/_RMC14/Objects/Clothing/Uniforms/Liaison/green_workwear.rsi/icon.png`
- `RMCJumpsuitLiaisonGreyWorkwear`: Lower-pivot grey shirt and charcoal slacks; no recoloring of the green or blue source. Source: `Resources/Textures/_RMC14/Objects/Clothing/Uniforms/Liaison/grey_workwear.rsi/icon.png`
- `RMCJumpsuitSunRiders`: Source jungle-olive field uniform with twin chest pockets and broad cloth waistband. The special starting patch and camouflage changes remain unsupported. Source: `Resources/Textures/_RMC14/Objects/Clothing/Uniforms/Marine/standard/jungle.rsi/icon.png`
- `RMCMarineUniformDressGeneral`: White undershirt, small collar tips, two visible muted center buttons, folded black trousers and broad red Blood Stripes. No medals, ribbons or coat are added. Source: `Resources/Textures/_RMC14/Objects/Clothing/Uniforms/DressBlues/general.rsi/icon.png`

## Attribution and license

All 18 original RSI metadata files specify **CC-BY-SA-3.0**. These derivative geometric interpretations and original detail crops retain that license. Keep the complete original metadata and this attribution note with redistributed art. The per-resource metadata is the attribution authority; complete text is copied verbatim into the proof JSON.

The LACN uniform was made by **patogrone on Discord for AU14**. The civilian workwear/boilersuit sources name cmss13-pve. The remaining uniforms name cmss13, with the Colonist metadata additionally crediting GitHub noctyrnal for its unmodeled jacket-equipped state. Individual attribution follows:

### AU14CivilianBaristaClothes

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/mob/humans/onmob/clothing/uniforms/uniforms_by_department/service.dmi, https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/items/clothing/uniforms/uniforms_by_department/service.dmi

Original metadata: `Content.CMU/Resources/Textures/CMU14/Clothing/Civilian/Uniforms/cuppajoesuniform.rsi/meta.json`

### AU14CivilianBoilerSuitCyan

Taken from cmss13-pve at https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/mob/humans/onmob/uniform_0.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi

Original metadata: `Content.CMU/Resources/Textures/CMU14/Clothing/Civilian/Uniforms/cyanboilersuit.rsi/meta.json`

### AU14CivilianBoilerSuitDarkBlueSynth

Taken from cmss13-pve at https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/mob/humans/onmob/uniform_0.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi

Original metadata: `Content.CMU/Resources/Textures/CMU14/Clothing/Civilian/Uniforms/darkblueboilersuit.rsi/meta.json`

### AU14CivilianBoilerSuitGray

Taken from cmss13-pve at https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/mob/humans/onmob/uniform_0.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi

Original metadata: `Content.CMU/Resources/Textures/CMU14/Clothing/Civilian/Uniforms/grayboilersuit.rsi/meta.json`

### AU14CivilianBoilerSuitWhite

Taken from cmss13-pve at https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/mob/humans/onmob/uniform_0.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi

Original metadata: `Content.CMU/Resources/Textures/CMU14/Clothing/Civilian/Uniforms/whiteboilersuit.rsi/meta.json`

### AU14CivilianWorkwearBlue

Taken from cmss13-pve at https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/mob/humans/onmob/uniform_0.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi

Original metadata: `Content.CMU/Resources/Textures/CMU14/Clothing/Civilian/Uniforms/blueworkwear.rsi/meta.json`

### AU14CivilianWorkwearGreen

Taken from cmss13-pve at https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/mob/humans/onmob/uniform_0.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi

Original metadata: `Content.CMU/Resources/Textures/CMU14/Clothing/Civilian/Uniforms/greenworkwear.rsi/meta.json`

### AU14CivilianWorkwearPink

Taken from cmss13-pve at https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/mob/humans/onmob/uniform_0.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi

Original metadata: `Content.CMU/Resources/Textures/CMU14/Clothing/Civilian/Uniforms/pinkworkwear.rsi/meta.json`

### AU14CivilianWorkwearYellow

Taken from cmss13-pve at https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/mob/humans/onmob/uniform_0.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi

Original metadata: `Content.CMU/Resources/Textures/CMU14/Clothing/Civilian/Uniforms/yellowworkwear.rsi/meta.json`

### AU14LACNUniformCoat

Made by patogrone on discord for AU14

Original metadata: `Content.CMU/Resources/Textures/CMU14/Clothing/LACN/Uniforms/lacnuniformcoat.rsi/meta.json`

### CMJumpsuitColonist

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/e7e2fe8c7d4dc0d322745ae5c2fa190b2e010897/icons/obj/items/clothing/uniforms/uniforms_by_faction/WY.dmi, https://github.com/cmss13-devs/cmss13/blob/e7e2fe8c7d4dc0d322745ae5c2fa190b2e010897/icons/obj/items/clothing/uniforms/uniforms_by_faction/WY.dmi, jacket-equipped-INNERCLOTHING made by github noctyrnal

Original metadata: `Resources/Textures/_RMC14/Objects/Clothing/Uniforms/Survivor/colonist.rsi/meta.json`

### CMJumpsuitLiaisonField

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/64d528e554abea15df13cd2cce4918e04e45fcf5/icons/mob/humans/onmob/uniform_0.dmi, https://github.com/cmss13-devs/cmss13/blob/81c7806eb705f3a6b43085056cef1be0055d8ed2/icons/obj/items/clothing/uniforms.dmi

Original metadata: `Resources/Textures/_RMC14/Objects/Clothing/Uniforms/Liaison/corporate_field.rsi/meta.json`

### RMCJumpsuitBlueWorkwear

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/obj/items/clothing/uniforms/workwear.dmi, https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/mob/humans/onmob/clothing/uniforms/workwear.dmi, https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/mob/humans/onmob/clothing/uniforms/workwear.dmi, https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/mob/humans/onmob/clothing/uniforms/workwear.dmi, https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/mob/humans/onmob/clothing/uniforms/workwear.dmi

Original metadata: `Resources/Textures/_RMC14/Objects/Clothing/Uniforms/Civilian/blue_workwear.rsi/meta.json`

### RMCJumpsuitKhakiWorkwearJacketless

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/obj/items/clothing/uniforms/workwear.dmi, https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/mob/humans/onmob/clothing/uniforms/workwear.dmi, https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/mob/humans/onmob/clothing/uniforms/workwear.dmi, https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/mob/humans/onmob/clothing/uniforms/workwear.dmi

Original metadata: `Resources/Textures/_RMC14/Objects/Clothing/Uniforms/Civilian/khaki_workwear.rsi/meta.json`

### RMCJumpsuitLiaisonGreenWorkwear

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/64d528e554abea15df13cd2cce4918e04e45fcf5/icons/mob/humans/onmob/uniform_0.dmi, https://github.com/cmss13-devs/cmss13/blob/81c7806eb705f3a6b43085056cef1be0055d8ed2/icons/obj/items/clothing/uniforms.dmi

Original metadata: `Resources/Textures/_RMC14/Objects/Clothing/Uniforms/Liaison/green_workwear.rsi/meta.json`

### RMCJumpsuitLiaisonGreyWorkwear

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/64d528e554abea15df13cd2cce4918e04e45fcf5/icons/mob/humans/onmob/uniform_0.dmi, https://github.com/cmss13-devs/cmss13/blob/81c7806eb705f3a6b43085056cef1be0055d8ed2/icons/obj/items/clothing/uniforms.dmi

Original metadata: `Resources/Textures/_RMC14/Objects/Clothing/Uniforms/Liaison/grey_workwear.rsi/meta.json`

### RMCJumpsuitSunRiders

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/8d2ee2746ce1b8aa5a876266951a8d9f92499aff/icons/mob/humans/onmob/uniform_0.dmi, https://github.com/cmss13-devs/cmss13/blob/a50253802b9d56391f7b857684e345119a207f43/icons/mob/humans/onmob/items_lefthand_0.dmi, https://github.com/cmss13-devs/cmss13/blob/a50253802b9d56391f7b857684e345119a207f43/icons/mob/humans/onmob/items_righthand_0.dmi, https://github.com/cmss13-devs/cmss13/blob/06d35efb20e830eacc57d3c77fea047dadbcdee3/icons/obj/items/clothing/uniforms.dmi

Original metadata: `Resources/Textures/_RMC14/Objects/Clothing/Uniforms/Marine/standard/jungle.rsi/meta.json`

### RMCMarineUniformDressGeneral

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/05eaa3484b1a35aa759300bcfc8f0119f009daae/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13/blob/05eaa3484b1a35aa759300bcfc8f0119f009daae/icons/mob/humans/onmob/uniform_0.dmi

Original metadata: `Resources/Textures/_RMC14/Objects/Clothing/Uniforms/DressBlues/general.rsi/meta.json`

## Reproduction and checks

Run from the repository root:

1. `python Tools/three_d/author_uniforms_extra_cloud.py`
2. `python Tools/three_d/verify_uniforms_extra_cloud.py`
3. `python Tools/three_d/verify_uniforms_extra_connections.py`
4. `blender -b --python Tools/three_d/verify_uniforms_extra_blender.py`

Evidence lives in `Tools/three_d/generated/cloud-review/uniforms-extra/`:

- `source-and-geometry-proof.json`: source/asset checksums, original metadata, exact palettes, part/triangle counts and direct exporter provenance
- `offline-verification.json`: 18 unique references, positive finite contract bounds, part limits, draft status, one-frame icon ownership, palette membership, 14 byte-exact three-pixel crops, unique atlas indices and byte-equal fresh exports
- `raw-glb-accessor-verification.json`: independent binary parsing of finite vertex/normal/transform values, buffer bounds, accessor min/max and index ranges; Khronos validation was not run
- `connection-mesh-witnesses.json`: all exported parts tested with their actual GLB vertices and node transforms. Convex-hull halfspaces plus linear programming find strict positive-volume intersections; every garment has one connected part graph. Selected transparent neckline and Barista split samples are separately checked by vertical rays
- `blender-import-validation.json`: Blender 4.3.2 imports all 18 exact exports with expected part counts, finite vertices, no mesh repairs and no animation actions
- Three `uniforms-extra-source-orbit-*.png` sheets and 18 separate comparison cards: original icon, fixed-scale top projection and two opposing orbit views

The contact proof is about intersecting closed part volumes. It is not a sewn-topology, manifold-union, simulation or deformation proof. Fine fabric, exact contour matching, reverse tailoring, high-resolution materials, native admission, saved-map fitting and all gameplay-selected states remain unfinished. The current assembly is explicitly a loose-world-prop draft, not a completed clothing conversion.


## Cumulative atlas assignment
This cumulative snapshot uses centrally allocated, nonconflicting atlas slots. Any authoring-time numeric range in this historical family note is superseded by the canonical surface YAML and `Tools/three_d/generated/cloud-review/atlas-allocation-current.json`. Source IDs, original PNG pixels and modeled geometry are unchanged by atlas-index remapping.
