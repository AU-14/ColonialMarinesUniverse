# Final loose clothing ground-prop drafts

Source checkpoint: `TheHellFireo/CMU-Garrison-3D` at `6e37a4d0a7d9433838c393a82c02422cb704dd5a`. Art-only cloud batch dated 2026-10-06.

## Deliverable and scope

Twenty editable solid-part assemblies cover twenty exact prototype IDs. All remain `status: draft`. The source inventory has twenty Redux and sixteen classic records for these IDs; these historical counts are not visible-instance, native-admission or map-fit coverage.

Canonical art definitions: `garrison_clothing_final_cloud.yml` and `garrison_clothing_final_cloud_art.yml`. The models have 701 parts total, with 10–57 parts per pose, under the unchanged 128-part limit. Eight unchanged small source-detail crops use reserved atlas entries 2543–2550 from the exact `clothing_final` allocation list. The other 32 reserved entries are unused. Every surface is at most fifteen source pixels; none is a whole garment, boot, belt or pouch icon.

The work changes only new family art definitions, derived detail textures, direct GLB exports, review images, source snapshots and local evidence scripts. It does not alter engine/runtime code, maps, gameplay prototype definitions, characters, held/worn art, rigs, adapters or publishing state. Existing outerwear, uniform, medical and equipment files remain unchanged.

## Actual default-layer ownership

- CCAF and USASF uniforms use the base icon plus an unpopulated resource-less WebbingVisualLayers.Base placeholder. No initial webbing/accessory is invented. Source uniform folding prefixes refer to worn/held presentation; they do not establish a second modeled ground icon.
- CMBootsBlackFilled, CMBootsBrownFilled and RMCBootsVanBandolier inherit RMCItemSlotM5Bayonet startingItem RMCM5Bayonet. The inspected CMInventorySystem UpdateSlotSprite shows the Fill layer only when an item slot is occupied. Each default source composition is icon + filled. Only the visible silver bayonet detail is modeled; a concealed blade is not invented.
- AU14PouchIFAKFill and RMCPouchToolsFill inherit RMCPouchOpenClosed and nonempty StorageFill contents. SharedStorageSystem sets StorageVisuals.Open from UI.IsUiOpen. CMStorageVisualizerSystem selects closedLayer for ordinary nonempty idle storage. Both references are icon + closed, with hidden contents omitted.
- CMBeltMarineAR10 and CMBeltMarineHunting each spawn five magazines. CMBeltBaseStorage maps closedLayer to full, so both use the same icon + full default artwork. Separate exact IDs are retained; their hidden ammunition type does not warrant invented exterior differences.
- RMCBeltHolsterPistolTSEFilledL54 spawns six L54 magazines plus an L54 pistol. The pistol definition explicitly has Sidearm and RMCWeaponPistolL54 tags. SharedItemMapperSystem selects layers with matching contained entities, with minCount 1 and maxCount int.MaxValue; the client shows only those selected named layers. The actual default composition is icon + gun_underlays/l54 + icon-front + full. Unrelated pistol-underlay templates are excluded. The six magazines keep storage-used nonzero after the holster-only adjustment.
- RMCLabcoatCMOOpened inherits RMCBaseJacketButtonableOpened, which explicitly sets Foldable.folded=true and selects icon-open, hiding icon. BaseFoldable GenericVisualizer confirms the foldedLayer choice. Only the opened ground form is modeled. Its gray central pixels are opaque lining, not a transparent cutout.
- Other coats, the TSEPA vest, webbing and chest rig use their one-direction clean ground icons. Dynamic stains, accessories, camouflage, live tint, source-selected changes and saved per-instance contents/overrides remain unsupported.

The pinned owner audit contains all 74 prototype definitions named by the twenty roots and inventory ancestor lists, together with exact controller paths and selected layers. Fetches use GitHub fetch_file at the fixed commit, UTF-8 for code/definitions/metadata and base64 for PNGs. All successful source receipts are checked against Git blob SHA-1; whole source images and tiny crop pixels are additionally checked without resampling.

## Physical construction and inference

Garments are laid out on the ground: broad joined thin cloth panels, sewn shoulder connections, continuous V-shaped collar leaves, bound cuff rings, restrained paired wedge folds, hems and folded trousers. These are semantic garment components, not a per-pixel extrusion or separate bead-like panels. The collar and cuff openings are physical negative space. The lower CCAF center remains joined dark cloth; the USASF source lower-trouser notch stays open.

Webbing uses separate continuous straps around a genuinely open central region and joined low waist web, with attached small pouch bodies/flaps. The compact chest rig and police vest use continuous low carriers. Pouches have distinct bag volumes, backing, upper flaps and binding strips. Ammo belts have four joined pouch bodies and a real buckle opening; the buckle hole is a physical-construction inference from the source silver clasp, whose pixels do not directly resolve its void. The L54 holster has a leather sleeve, side magazine pouches, waist web and the selected visible gun grip/slide.

Boot pairs are upright loose objects with shaped soles and toes, broad hollow leather quarters, joined polygonal heel walls, a padded open ankle rim and one visible slot-content detail. The two boots are intentionally separate physical objects. Collar rim faceting, boot shape, hidden construction, physical height/depth and material response remain approximate.

The source scale is one tile per 32 source pixels for laid-out proportions. Thickness, folds, unseen undersides, pocket depth, curved boot volumes and back surfaces are inferred. Source-facing cards use source-bound framing; orbit and underside panels are independently fit to keep complete geometry visible. No projection score or source fidelity approval is claimed. The assembled parts overlap at seams; they are editable solids, not a proven unified watertight fabric mesh.

## Checks performed

- Direct, deterministic GLB bytes from the existing build_models.py exporter, with no post-processing. The local exporter differs from the pinned file only by one pre-existing trailing blank line; both Git hashes are recorded. The batch did not modify it.
- Twenty exact target IDs verified in physicalModelingFamilies and Redux inventory; own model/source IDs and atlas slots have no collisions with other canonical files.
- All untextured paints belong to each actual composed source palette; white is permitted only as the unchanged crop carrier. All source compositions exactly recompute from selected original layers. All eight small crops retain their original RGBA pixels.
- Raw GLB buffer ranges, finite accessors and accessor bounds, primitive indices, scene membership and part counts checked. No animation or scene-clone coverage is claimed.
- Direct GLB default-scene convex meshes checked with linear-programming positive-volume contact witnesses. All garments, straps, pouches and belts each form one expected component. Each boot pair forms exactly two internally connected components. No default-scene clone nodes are double-counted.
- All 41 selected collar, cuff, web, buckle, holster and boot-cavity rays are clear. This checks the selected openings, not global manifoldness, real sewing or every possible optical path.
- Independent Blender 4.3.2 imports all twenty GLBs, with finite vertices and zero mesh-validation repairs. This is not a Khronos validator result.

## Reproduce and inspect

Run from the repository root:

```
python Tools/three_d/audit_clothing_final_sources.py
python Tools/three_d/author_clothing_final_cloud.py
python Tools/three_d/verify_clothing_final_connections.py
python Tools/three_d/verify_clothing_final_cloud.py
blender -b -t 2 --python Tools/three_d/check_clothing_final_blender.py
python Tools/three_d/document_clothing_final_cloud.py
```

The review directory is `Tools/three_d/generated/cloud-review/clothing-final/`. Each model has source-facing, orbit, underside and composed-reference comparison PNGs. Four complete review sheets are named clothing-final-source-orbit-1.png through -4.png.

Evidence: source-and-geometry-proof.json; source-owner-audit.json; connection-mesh-witnesses.json; offline-verification.json; blender-import-validation.json. The family handoff manifest is Tools/three_d/generated/clothing-final-cloud-manifest.json.

No native GPU/client, browser runtime, saved-map contact, full-state, worn/held, gameplay, multiplayer, performance or fidelity test was done. No engine integration, push or publication is claimed.

## Exact models and source compositions

### AU14CCAFUniform

- Model: `CMU3DAU14CCAFUniformWorldCloud`
- Family: uniform; 57 editable parts
- Default layers: icon.png
- Laid-out uniform with joined shoulders, cloth front and folded trousers; physical collar and cuff notches. Dark green CCAF camouflage tones.

### AU14ExternalWebbing

- Model: `CMU3DAU14ExternalWebbingWorldCloud`
- Family: webbing; 18 editable parts
- Default layers: icon.png
- Brown external webbing: genuinely open tall shoulder loop, connected waist web and three attached small pouches; no transparent-region backing slab.

### AU14PouchIFAKFill

- Model: `CMU3DAU14PouchIFAKFillWorldCloud`
- Family: closed pouch; 10 editable parts
- Default layers: icon.png + closed.png
- Filled idle IFAK pouch shown closed by its actual storage owner. Joined bag sides, folded upper flap and original tiny blue medical marking. Hidden contents are not visible geometry.

### AU14USASFSECFORChestrig

- Model: `CMU3DAU14USASFSECFORChestrigWorldCloud`
- Family: chest rig; 19 editable parts
- Default layers: icon.png
- Compact tan SECFOR chest rig, two open shoulder loops and four physical joined pouch faces on a connected low web foundation.

### AU14USASFSecForUniform

- Model: `CMU3DAU14USASFSecForUniformWorldCloud`
- Family: uniform; 55 editable parts
- Default layers: icon.png
- Laid-out uniform with joined shoulders, cloth front and folded trousers; physical collar and cuff notches. USASF gray-green cloth and unchanged small sleeve insignia crops.

### CMBeltMarineAR10

- Model: `CMU3DCMBeltMarineAR10WorldCloud`
- Family: ammo belt; 31 editable parts
- Default layers: icon.png + full.png
- Source-filled M276 laid-out ammo belt: four physical closed pockets, continuous side web and genuinely open buckle frame. AR10 and hunting contents share the same visible idle artwork.

### CMBeltMarineHunting

- Model: `CMU3DCMBeltMarineHuntingWorldCloud`
- Family: ammo belt; 31 editable parts
- Default layers: icon.png + full.png
- Source-filled M276 laid-out ammo belt: four physical closed pockets, continuous side web and genuinely open buckle frame. AR10 and hunting contents share the same visible idle artwork.

### CMBootsBlackFilled

- Model: `CMU3DCMBootsBlackFilledWorldCloud`
- Family: filled boots; 54 editable parts
- Default layers: icon.png + filled.png
- Source-filled paired combat boots with separate joined soles, rounded toes, hollow ankle walls, genuinely open padded collars and the single visible bayonet tip. The concealed blade is not invented; two boots are intentionally separate objects.

### CMBootsBrownFilled

- Model: `CMU3DCMBootsBrownFilledWorldCloud`
- Family: filled boots; 54 editable parts
- Default layers: icon.png + filled.png
- Source-filled paired combat boots with separate joined soles, rounded toes, hollow ankle walls, genuinely open padded collars and the single visible bayonet tip. The concealed blade is not invented; two boots are intentionally separate objects.

### CMCoatDressBluesSenior

- Model: `CMU3DCMCoatDressBluesSeniorWorldCloud`
- Family: dress coat; 43 editable parts
- Default layers: icon.png
- Senior dress-blues coat with source red edging, short sleeves, white belt and tiny original gold clasp; no invented awards.

### RMCArmorVestTSEPA

- Model: `CMU3DRMCArmorVestTSEPAWorldCloud`
- Family: armor vest; 20 editable parts
- Default layers: icon.png
- TSE police vest with joined low carrier panels, V-shaped open shoulder yoke, raised continuous chest bands and source dangling adjustment strap.

### RMCBeltHolsterPistolTSEFilledL54

- Model: `CMU3DRMCBeltHolsterPistolTSEFilledL54WorldCloud`
- Family: belt holster; 33 editable parts
- Default layers: icon.png + l54.png + icon-front.png + full.png
- Filled L54 pistol holster with the actual selected brown grip/gray slide, front holster cover and closed side magazine pockets. Leather sleeve, walls and retaining bands have physical depth; unrelated gun-underlay layers are excluded.

### RMCBootsVanBandolier

- Model: `CMU3DRMCBootsVanBandolierWorldCloud`
- Family: filled boots; 54 editable parts
- Default layers: icon.png + filled.png
- Source-filled paired polished hiking jackboots with separate joined soles, rounded toes, hollow ankle walls, genuinely open padded collars and the single visible bayonet tip. The concealed blade is not invented; two boots are intentionally separate objects.

### RMCCoatBureauDeputy

- Model: `CMU3DRMCCoatBureauDeputyWorldCloud`
- Family: jacket; 35 editable parts
- Default layers: icon.png
- Bureau deputy short jacket; dark cloth, long facings, original small silver chest marking and ochre shoulder tab.

### RMCCoatSnowSurvivor

- Model: `CMU3DRMCCoatSnowSurvivorWorldCloud`
- Family: snow coat; 50 editable parts
- Default layers: icon.png
- Snow-survivor insulated coat with continuous folded collar, broad joined skirt panels, dark waist binding and real cuffs.

### RMCJacketCorporateBlue

- Model: `CMU3DRMCJacketCorporateBlueWorldCloud`
- Family: jacket; 33 editable parts
- Default layers: icon.png
- Short corporate jacket with connected cloth panels, turned front facings, V collar, thin drape ridges and hollow flattened cuffs.

### RMCJacketCorporateBrown

- Model: `CMU3DRMCJacketCorporateBrownWorldCloud`
- Family: jacket; 33 editable parts
- Default layers: icon.png
- Short corporate jacket with connected cloth panels, turned front facings, V collar, thin drape ridges and hollow flattened cuffs.

### RMCLabcoatCMOOpened

- Model: `CMU3DRMCLabcoatCMOOpenedWorldCloud`
- Family: open labcoat; 38 editable parts
- Default layers: icon-open.png
- Source-open CMO labcoat: long green front leaves, sleeve folds, dark green seams and lowered opaque gray inner lining. The source center is cloth rather than transparent air.

### RMCOuterClothingExternalWebbingBlack

- Model: `CMU3DRMCOuterClothingExternalWebbingBlackWorldCloud`
- Family: webbing; 18 editable parts
- Default layers: icon.png
- Black external webbing: genuinely open tall shoulder loop, connected waist web and three attached small pouches; no transparent-region backing slab.

### RMCPouchToolsFill

- Model: `CMU3DRMCPouchToolsFillWorldCloud`
- Family: closed pouch; 15 editable parts
- Default layers: icon.png + closed.png
- Closed filled tool pouch with two distinct joined pockets, continuous shared backing, turned tan flaps, sewn horizontal webbing and lower reinforcements; concealed tools omitted.

## Original artwork attribution

Keep this file and complete original RSI metadata with redistributed art and embedded texture adaptations. Source-specific metadata follows verbatim. New inferred geometry and unchanged detail-crop adaptations follow the corresponding source artwork license.

### Content.CMU/Resources/Textures/CMU14/Clothing/CCAF/ccafuniform.rsi

License: CC-BY-SA-3.0

Taken from cmss13-pve at https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/mob/humans/onmob/uniform_0.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/5f467f7ce029ecda028af82d3587424e7e1bb5ce/icons/obj/items/clothing/uniforms.dmi

Pinned metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Textures/CMU14/Clothing/CCAF/ccafuniform.rsi/meta.json

### Content.CMU/Resources/Textures/CMU14/Clothing/USASFSecurityForces/chestrig.rsi

License: CC-BY-SA-3.0

Made by patogrone on discord for AU14

Pinned metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Textures/CMU14/Clothing/USASFSecurityForces/chestrig.rsi/meta.json

### Content.CMU/Resources/Textures/CMU14/Clothing/USASFSecurityForces/uniform.rsi

License: CC-BY-SA-3.0

Made by patogrone on discord for AU14

Pinned metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Textures/CMU14/Clothing/USASFSecurityForces/uniform.rsi/meta.json

### Resources/Textures/_RMC14/Objects/Clothing/Belt/holster_pistol_tsepa.rsi

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/41b3798435ee57263bb67fa694ea944d6698c062/icons/obj/items/clothing/belts/belts_by_faction/TWE.dmi, https://github.com/cmss13-devs/cmss13/blob/41b3798435ee57263bb67fa694ea944d6698c062/icons/mob/humans/onmob/clothing/belts/belts_by_faction/TWE.dmi

Pinned metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Clothing/Belt/holster_pistol_tsepa.rsi/meta.json

### Resources/Textures/_RMC14/Objects/Clothing/Belt/marine/jungle-classic.rsi

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/168c8a79a3d858a8c531ff7ed0bc3033e47d1d16/icons/obj/items/clothing/belts.dmi, https://github.com/cmss13-devs/cmss13/blob/207f72c0f8ca3762632938b51fd1c41bca2c7747/icons/mob/humans/onmob/belt.dmi, https://github.com/cmss13-devs/cmss13/blob/168c8a79a3d858a8c531ff7ed0bc3033e47d1d16/icons/mob/humans/onmob/items_lefthand_1.dmi, https://github.com/cmss13-devs/cmss13/blob/168c8a79a3d858a8c531ff7ed0bc3033e47d1d16/icons/mob/humans/onmob/items_righthand_1.dmi

Pinned metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Clothing/Belt/marine/jungle-classic.rsi/meta.json

### Resources/Textures/_RMC14/Objects/Clothing/OuterClothing/Coats/Bureau/deputy.rsi

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/f4f08bd6ff5ff67172d60f00f522c62726f2b432/icons/obj/items/clothing/suits.dmi, https://github.com/cmss13-devs/cmss13/blob/64d528e554abea15df13cd2cce4918e04e45fcf5/icons/mob/humans/onmob/suit_0.dmi

Pinned metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Clothing/OuterClothing/Coats/Bureau/deputy.rsi/meta.json

### Resources/Textures/_RMC14/Objects/Clothing/OuterClothing/Coats/Corporate/corporate_blue_jacket.rsi

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/06d35efb20e830eacc57d3c77fea047dadbcdee3/icons/obj/items/clothing/cm_suits.dmi, https://github.com/cmss13-devs/cmss13/blob/06d35efb20e830eacc57d3c77fea047dadbcdee3/icons/mob/humans/onmob/suit_1.dmi

Pinned metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Clothing/OuterClothing/Coats/Corporate/corporate_blue_jacket.rsi/meta.json

### Resources/Textures/_RMC14/Objects/Clothing/OuterClothing/Coats/Corporate/corporate_brown_jacket.rsi

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/06d35efb20e830eacc57d3c77fea047dadbcdee3/icons/obj/items/clothing/cm_suits.dmi, https://github.com/cmss13-devs/cmss13/blob/06d35efb20e830eacc57d3c77fea047dadbcdee3/icons/mob/humans/onmob/suit_1.dmi

Pinned metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Clothing/OuterClothing/Coats/Corporate/corporate_brown_jacket.rsi/meta.json

### Resources/Textures/_RMC14/Objects/Clothing/OuterClothing/Coats/DressBlues/senior.rsi

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/f4f08bd6ff5ff67172d60f00f522c62726f2b432/icons/obj/items/clothing/suits.dmi, https://github.com/cmss13-devs/cmss13/blob/64d528e554abea15df13cd2cce4918e04e45fcf5/icons/mob/humans/onmob/suit_0.dmi

Pinned metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Clothing/OuterClothing/Coats/DressBlues/senior.rsi/meta.json

### Resources/Textures/_RMC14/Objects/Clothing/OuterClothing/Coats/Snowsuits/normal.rsi

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/b6f4841b5768599c0fcf58a21fd88536a9959c2c/icons/mob/humans/onmob/clothing/suits/coats_robes.dmi, https://github.com/cmss13-devs/cmss13/blob/b6f4841b5768599c0fcf58a21fd88536a9959c2c/icons/obj/items/clothing/suits/coats_robes.dmi

Pinned metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Clothing/OuterClothing/Coats/Snowsuits/normal.rsi/meta.json

### Resources/Textures/_RMC14/Objects/Clothing/OuterClothing/Labcoats/cmolabcoat.rsi

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/6885bdcc04edc239796f74ac325fc9bc98c0a8fc/icons/mob/humans/onmob/clothing/suits/coats_robes.dmi, https://github.com/cmss13-devs/cmss13/commits/6885bdcc04edc239796f74ac325fc9bc98c0a8fc/icons/obj/items/clothing/suits/coats_robes.dmi

Pinned metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Clothing/OuterClothing/Labcoats/cmolabcoat.rsi/meta.json

### Resources/Textures/_RMC14/Objects/Clothing/OuterClothing/Misc/external_webbing.rsi

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/obj/items/clothing/suits/misc_ert.dmi, https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/mob/humans/onmob/clothing/suits/misc_ert.dmi

Pinned metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Clothing/OuterClothing/Misc/external_webbing.rsi/meta.json

### Resources/Textures/_RMC14/Objects/Clothing/OuterClothing/Misc/external_webbing_black.rsi

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/obj/items/clothing/suits/misc_ert.dmi, https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/mob/humans/onmob/clothing/suits/misc_ert.dmi

Pinned metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Clothing/OuterClothing/Misc/external_webbing_black.rsi/meta.json

### Resources/Textures/_RMC14/Objects/Clothing/OuterClothing/TSE/TSEPA/tsepa_police_vest.rsi

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/41b3798435ee57263bb67fa694ea944d6698c062/icons/mob/humans/onmob/clothing/suits/suits_by_faction/TWE.dmi, https://github.com/cmss13-devs/cmss13/blob/41b3798435ee57263bb67fa694ea944d6698c062/icons/obj/items/clothing/suits/suits_by_faction/TWE.dmi

Pinned metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Clothing/OuterClothing/TSE/TSEPA/tsepa_police_vest.rsi/meta.json

### Resources/Textures/_RMC14/Objects/Clothing/Pouches/medical.rsi

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/274dffcf8574897b6b8fbcad07d81aa0f28292bb/icons/obj/items/clothing/pouches.dmi

Pinned metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Clothing/Pouches/medical.rsi/meta.json

### Resources/Textures/_RMC14/Objects/Clothing/Pouches/tools.rsi

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/274dffcf8574897b6b8fbcad07d81aa0f28292bb/icons/obj/items/clothing/pouches.dmi

Pinned metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Clothing/Pouches/tools.rsi/meta.json

### Resources/Textures/_RMC14/Objects/Clothing/Shoes/Boots/black.rsi

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/05eaa3484b1a35aa759300bcfc8f0119f009daae/icons/mob/humans/onmob/feet.dmi, https://github.com/cmss13-devs/cmss13/blob/05eaa3484b1a35aa759300bcfc8f0119f009daae/icons/obj/items/clothing/shoes.dmi, https://github.com/cmss13-devs/cmss13/blob/a15efa985114bf92a98cda275aa8319636ae5abe/icons/obj/items/clothing/shoes.dmi

Pinned metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Clothing/Shoes/Boots/black.rsi/meta.json

### Resources/Textures/_RMC14/Objects/Clothing/Shoes/Boots/brown.rsi

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/05eaa3484b1a35aa759300bcfc8f0119f009daae/icons/mob/humans/onmob/feet.dmi, https://github.com/cmss13-devs/cmss13/blob/05eaa3484b1a35aa759300bcfc8f0119f009daae/icons/obj/items/clothing/shoes.dmi, https://github.com/cmss13-devs/cmss13/blob/a15efa985114bf92a98cda275aa8319636ae5abe/icons/obj/items/clothing/shoes.dmi

Pinned metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Clothing/Shoes/Boots/brown.rsi/meta.json

### Resources/Textures/_RMC14/Objects/Clothing/Shoes/Boots/jackboots.rsi

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/36ab9674a311463bb056f7fab5fe3f3079305569/icons/obj/items/clothing/shoes.dmi, https://github.com/cmss13-devs/cmss13/blob/a15efa985114bf92a98cda275aa8319636ae5abe/icons/mob/humans/onmob/feet.dmi

Pinned metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Clothing/Shoes/Boots/jackboots.rsi/meta.json

### Resources/Textures/_RMC14/Objects/Weapons/Guns/gun_underlays.rsi

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/15d853a340792692e623bcd30f7d8cc058ad6343/icons/obj/items/clothing/belts/holstered_guns.dmi. Nailgun Sprites by VictorJob. warwick and warwick_alt by @TadJohnson00 (GitHub).

Pinned metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Weapons/Guns/gun_underlays.rsi/meta.json
