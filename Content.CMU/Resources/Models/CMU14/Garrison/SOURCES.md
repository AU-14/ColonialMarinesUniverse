# Garrison draft solid models

This starter catalog contains 44 manually authored models built from colored cuboid parts. These are editable three-dimensional solids, not reconstructed meshes or sprite billboards. All assets are **draft**.

## Coordinates and use

- X and Y are horizontal; Z is up. One unit equals one game tile.
- Ground is Z = 0. Furniture and cabinet fronts face -Y; authored door width follows X. Paired-door modules have a 90-degree presentation offset to match their source pairing axis.
- Walls are 2.8 units high. The marine scale mannequin is 1.72 units high.
- The rifle is a separate unrigged prop with its barrel pointing +X.
- Source prototype mappings are exact. No wildcard or inherited family mapping is asserted.
- The marine mannequin intentionally has no entity mappings: using MobHuman would misrepresent player appearance and equipment.
- These models capture a single static state. Doors, lockers, folding furniture, damage, powered displays, ammunition states, connected walls/tables and character animation require further work.

## Authorship and licensing

The cuboid coordinates and part assembly in `garrison_models.yml` were newly authored for this project with Codex assistance, using the referenced sprites as visual guides. No sprite pixels or existing 3D geometry are embedded in the models. The newly authored geometry contributions are dedicated under **CC0-1.0**, to the extent those contributions can be separately licensed. This does not relicense upstream visual designs, sprites, names, or any derivative appearance rights. Preserve the upstream **CC-BY-SA-3.0** attribution and share-alike requirements wherever they apply to derived assets. The referenced RSI `meta.json` files are the authoritative existing attribution records; their contents remain unchanged.

Reference license links: [CC0-1.0](https://creativecommons.org/publicdomain/zero/1.0/) and [CC-BY-SA-3.0](https://creativecommons.org/licenses/by-sa/3.0/).

## Model references

The modeler inspected the mapped prototype definitions and representative sprite images before authoring. New surfaces hidden by the original sprite views are design inferences. Silhouette matching is preliminary visual review, not a measured image-comparison acceptance test.

| Model | Source RSI metadata |
| --- | --- |
| CMU3DFoldingChair | `Resources/Textures/_RMC14/Structures/Furniture/folding_chair.rsi/meta.json` |
| CMU3DOfficeChairDark | `Resources/Textures/_RMC14/Structures/Furniture/chairs.rsi/meta.json` |
| CMU3DComfyChairBlack | `Resources/Textures/_RMC14/Structures/Furniture/chairs.rsi/meta.json` |
| CMU3DWoodChair | `Resources/Textures/_RMC14/Structures/Furniture/chairs.rsi/meta.json` |
| CMU3DStool | `Resources/Textures/_RMC14/Structures/Furniture/chairs.rsi/meta.json` |
| CMU3DSteelTable | `Resources/Textures/_RMC14/Structures/Furniture/Tables/standard.rsi/meta.json` |
| CMU3DRequisitionDesk | `Resources/Textures/_RMC14/Structures/Furniture/Tables/requisition.rsi/meta.json` |
| CMU3DBlackDesk | `Resources/Textures/_RMC14/Structures/Furniture/Tables/black.rsi/meta.json` |
| CMU3DAlmayerTable | `Resources/Textures/_RMC14/Structures/Furniture/Tables/almayer.rsi/meta.json` |
| CMU3DFancyWoodTable | `Resources/Textures/_RMC14/Structures/Furniture/Tables/fancy_wood.rsi/meta.json` |
| CMU3DPoorWoodTable | `Resources/Textures/_RMC14/Structures/Furniture/Tables/poor_wood.rsi/meta.json` |
| CMU3DStorageRack | `Resources/Textures/_RMC14/Structures/Storage/rack.rsi/meta.json` |
| CMU3DBed | `Resources/Textures/_RMC14/Structures/Furniture/bed.rsi/meta.json` |
| CMU3DBunkBed | `Resources/Textures/_RMC14/Structures/Furniture/bed.rsi/meta.json` |
| CMU3DLocker | `Resources/Textures/_RMC14/Structures/Storage/Lockers/standard.rsi/meta.json` |
| CMU3DArmoryLocker | `Resources/Textures/_RMC14/Structures/Storage/Lockers/armory_locker.rsi/meta.json` |
| CMU3DFridge | `Resources/Textures/_RMC14/Structures/Storage/Lockers/fridge.rsi/meta.json` |
| CMU3DSteelCrate | `Resources/Textures/_RMC14/Structures/Storage/Crates/basic.rsi/meta.json` |
| CMU3DAmmoCrate | `Resources/Textures/_RMC14/Structures/Storage/Crates/ammo.rsi/meta.json` |
| CMU3DWoodCrate | `Resources/Textures/_RMC14/Structures/Storage/Crates/woodcrate.rsi/meta.json` |
| CMU3DSecureCrate | `Resources/Textures/_RMC14/Structures/Storage/Crates/secure_basic.rsi/meta.json` |
| CMU3DLargeWoodCrate | `Resources/Textures/_RMC14/Structures/Storage/Crates/densecrate.rsi/meta.json` |
| CMU3DSink | `Resources/Textures/_RMC14/Structures/Furniture/sink.rsi/meta.json` |
| CMU3DToilet | `Resources/Textures/_RMC14/Structures/Furniture/toilet.rsi/meta.json` |
| CMU3DWashingMachine | `Resources/Textures/_RMC14/Structures/Furniture/washing_machine.rsi/meta.json` |
| CMU3DCoffeeVendor | `Resources/Textures/_RMC14/Structures/Machines/VendingMachines/coffee.rsi/meta.json` |
| CMU3DSnackVendor | `Resources/Textures/_RMC14/Structures/Machines/VendingMachines/snack.rsi/meta.json` |
| CMU3DColaVendor | `Resources/Textures/_RMC14/Structures/Machines/VendingMachines/cola.rsi/meta.json` |
| CMU3DFusionGenerator | `Resources/Textures/_RMC14/Structures/Power/fusion_reactor.rsi/meta.json` |
| CMU3DColonyGenerator | `Resources/Textures/_RMC14/Structures/Power/geothermal_generator.rsi/meta.json` |
| CMU3DSensorComputer | `Resources/Textures/_RMC14/Structures/hybrisa_computer_props.rsi/meta.json` |
| CMU3DHybrisaWall | `Resources/Textures/_RMC14/Structures/Walls/hybrisa_wall.rsi/meta.json` |
| CMU3DStrataWall | `Resources/Textures/_RMC14/Structures/Walls/strata_wall.rsi/meta.json` |
| CMU3DReinforcedPrisonWall | `Resources/Textures/_RMC14/Structures/Walls/prison_rwall.rsi/meta.json` |
| CMU3DHybrisaAirlock | `Resources/Textures/_RMC14/Structures/Doors/Airlocks/hybrisa_door.rsi/meta.json` |
| CMU3DHybrisaPersonalAirlock | `Resources/Textures/_RMC14/Structures/Doors/Airlocks/hybrisa_personal_door.rsi/meta.json` |
| CMU3DHybrisaDoubleGlassDoor | `Resources/Textures/_RMC14/Structures/Doors/Airlocks/Double/hybrisa_glass.rsi/meta.json` |
| CMU3DBarrelGreen | `Resources/Textures/_RMC14/Structures/barrels.rsi/meta.json` |
| CMU3DBarrelRed | `Resources/Textures/_RMC14/Structures/barrels.rsi/meta.json` |
| CMU3DBarrelBlue | `Resources/Textures/_RMC14/Structures/barrels.rsi/meta.json` |
| CMU3DTrashBinGreen | `Resources/Textures/_RMC14/Structures/Piping/disposal.rsi/meta.json` |
| CMU3DPottedPlant6 | `Resources/Textures/_RMC14/Structures/Furniture/potted_plants.rsi/meta.json` |
| CMU3DMarineDraft | `Resources/Textures/_RMC14/Objects/Clothing/OuterClothing/Armor/m3/standard/padded/jungle.rsi/meta.json`<br>`Resources/Textures/_RMC14/Objects/Clothing/Head/Helmets/m10/standard/jungle.rsi/meta.json` |
| CMU3DPulseRifleDraft | `Content.CMU/Resources/Textures/CMU14/Weapons/Guns/USCM/m41mk2.rsi/meta.json` |

## Preserved reference attribution

### CMU14/Weapons/Guns/USCM/m41mk2.rsi

- Metadata: `Content.CMU/Resources/Textures/CMU14/Weapons/Guns/USCM/m41mk2.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing copyright / attribution: Created by Cephalopod222, equipped-SUITSTORAGE taken and modified from cmss13 at https://github.com/cmss13-devs/cmss13/commit/07455a5f3986b610e7ccdc3096cad1d914fca2f9, camouflage variants recolored by MACMAN2003

### _RMC14/Objects/Clothing/Head/Helmets/m10/standard/jungle.rsi

- Metadata: `Resources/Textures/_RMC14/Objects/Clothing/Head/Helmets/m10/standard/jungle.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing copyright / attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/06d35efb20e830eacc57d3c77fea047dadbcdee3/icons/mob/humans/onmob/head_1.dmi, https://github.com/cmss13-devs/cmss13/blob/a50253802b9d56391f7b857684e345119a207f43/icons/mob/humans/onmob/items_lefthand_0.dmi, https://github.com/cmss13-devs/cmss13/blob/a50253802b9d56391f7b857684e345119a207f43/icons/mob/humans/onmob/items_righthand_0.dmi, https://github.com/cmss13-devs/cmss13/blob/7d096e84864542952a2bb12487fd1327bd7e5bbb/icons/obj/items/clothing/cm_hats.dmi

### _RMC14/Objects/Clothing/OuterClothing/Armor/m3/standard/padded/jungle.rsi

- Metadata: `Resources/Textures/_RMC14/Objects/Clothing/OuterClothing/Armor/m3/standard/padded/jungle.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing copyright / attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/06d35efb20e830eacc57d3c77fea047dadbcdee3/icons/mob/humans/onmob/suit_1.dmi, https://github.com/cmss13-devs/cmss13/blob/a50253802b9d56391f7b857684e345119a207f43/icons/mob/humans/onmob/items_lefthand_0.dmi, https://github.com/cmss13-devs/cmss13/blob/a50253802b9d56391f7b857684e345119a207f43/icons/mob/humans/onmob/items_righthand_0.dmi, https://github.com/cmss13-devs/cmss13/blob/06d35efb20e830eacc57d3c77fea047dadbcdee3/icons/obj/items/clothing/cm_suits.dmi

### _RMC14/Structures/Doors/Airlocks/Double/hybrisa_glass.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Airlocks/Double/hybrisa_glass.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing copyright / attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/doors/hybrisa/hybrisa_2x1personaldoor_glass.dmi

### _RMC14/Structures/Doors/Airlocks/hybrisa_door.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Airlocks/hybrisa_door.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing copyright / attribution: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/doors/personaldoor.dmi

### _RMC14/Structures/Doors/Airlocks/hybrisa_personal_door.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Airlocks/hybrisa_personal_door.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing copyright / attribution: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/doors/hybrisa_personaldoor.dmi

### _RMC14/Structures/Furniture/Tables/almayer.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Furniture/Tables/almayer.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing copyright / attribution: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/5c70b0d01cc16865b7a000c3a74e0d4f729661f6/icons/obj/structures/tables.dmi

### _RMC14/Structures/Furniture/Tables/black.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Furniture/Tables/black.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing copyright / attribution: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/5c70b0d01cc16865b7a000c3a74e0d4f729661f6/icons/obj/structures/tables.dmi

### _RMC14/Structures/Furniture/Tables/fancy_wood.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Furniture/Tables/fancy_wood.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing copyright / attribution: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/5c70b0d01cc16865b7a000c3a74e0d4f729661f6/icons/obj/structures/tables.dmi

### _RMC14/Structures/Furniture/Tables/poor_wood.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Furniture/Tables/poor_wood.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing copyright / attribution: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/5c70b0d01cc16865b7a000c3a74e0d4f729661f6/icons/obj/structures/tables.dmi

### _RMC14/Structures/Furniture/Tables/requisition.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Furniture/Tables/requisition.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing copyright / attribution: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/5c70b0d01cc16865b7a000c3a74e0d4f729661f6/icons/obj/structures/tables.dmi

### _RMC14/Structures/Furniture/Tables/standard.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Furniture/Tables/standard.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing copyright / attribution: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/5c70b0d01cc16865b7a000c3a74e0d4f729661f6/icons/obj/structures/tables.dmi

### _RMC14/Structures/Furniture/bed.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Furniture/bed.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing copyright / attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/9dd2c0d0a1c21e3c9bddbad1321c63fd886f61cf/icons/obj/objects.dmi, https://github.com/cmss13-devs/cmss13/blob/6a955a3c180f3efcf3b997c230fff4d634eb0629/icons/obj/structures/machinery/yautja_machines.dmi, https://github.com/cmss13-devs/cmss13/blob/39a39f5df6c4b32708e50ed711dc5b1bebe313b6/icons/obj/structures/props/furniture/chairs.dmi

### _RMC14/Structures/Furniture/chairs.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Furniture/chairs.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing copyright / attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/9dd2c0d0a1c21e3c9bddbad1321c63fd886f61cf/icons/obj/objects.dmi, https://github.com/cmss13-devs/cmss13/blob/6a955a3c180f3efcf3b997c230fff4d634eb0629/icons/obj/structures/machinery/yautja_machines.dmi, ai_interface_chair taken from cmss13-pve at https://github.com/cmss13-devs/cmss13-pve/blob/master/icons/obj/objects.dmi

### _RMC14/Structures/Furniture/folding_chair.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Furniture/folding_chair.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing copyright / attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/9dd2c0d0a1c21e3c9bddbad1321c63fd886f61cf/icons/obj/objects.dmi,  https://github.com/cmss13-devs/cmss13/blob/9dd2c0d0a1c21e3c9bddbad1321c63fd886f61cf/icons/mob/humans/items/furniture_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/9dd2c0d0a1c21e3c9bddbad1321c63fd886f61cf/icons/mob/humans/items/furniture_righthand.dmi

### _RMC14/Structures/Furniture/potted_plants.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Furniture/potted_plants.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing copyright / attribution: Taken from cmss13 at https://github.com/Zenith00000/cmss13/blob/5eaab0124aa46008a9f0f5e2f74a41bdfc0c315f/icons/obj/structures/props/natural/vegetation/plants.dmi

### _RMC14/Structures/Furniture/sink.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Furniture/sink.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing copyright / attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/cd8ab082e9c3de33652ce5cbf730026baefb6e96/icons/obj/structures/props/watercloset.dmi

### _RMC14/Structures/Furniture/toilet.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Furniture/toilet.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing copyright / attribution: Taken from CM-SS13 at commit https://github.com/cmss13-devs/cmss13/commit/cd8ab082e9c3de33652ce5cbf730026baefb6e96

### _RMC14/Structures/Furniture/washing_machine.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Furniture/washing_machine.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing copyright / attribution: Taken from CM-SS13 at commit https://github.com/cmss13-devs/cmss13/blob/5a2359bab582e18b3b432539733be04204148a5e/

### _RMC14/Structures/Machines/VendingMachines/coffee.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Machines/VendingMachines/coffee.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing copyright / attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/09a5191fb11aab8ddffe3f9be94292b53e4d96f6/icons/obj/structures/machinery/vending.dmi

### _RMC14/Structures/Machines/VendingMachines/cola.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Machines/VendingMachines/cola.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing copyright / attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/09a5191fb11aab8ddffe3f9be94292b53e4d96f6/icons/obj/structures/machinery/vending.dmi

### _RMC14/Structures/Machines/VendingMachines/snack.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Machines/VendingMachines/snack.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing copyright / attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/09a5191fb11aab8ddffe3f9be94292b53e4d96f6/icons/obj/structures/machinery/vending.dmi

### _RMC14/Structures/Piping/disposal.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Piping/disposal.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing copyright / attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/icons/obj/pipes/disposal.dmi, https://github.com/cmss13-devs/cmss13/blob/icons/obj/structures/props/hybrisa/trash_bins.dmi

### _RMC14/Structures/Power/fusion_reactor.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Power/fusion_reactor.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing copyright / attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/edd9c65b095cb55b6739d94587a353a5b3049536/icons/obj/structures/machinery/fusion_eng.dmi

### _RMC14/Structures/Power/geothermal_generator.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Power/geothermal_generator.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing copyright / attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/edd9c65b095cb55b6739d94587a353a5b3049536/icons/obj/structures/machinery/geothermal.dmi

### _RMC14/Structures/Storage/Crates/ammo.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Storage/Crates/ammo.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing copyright / attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7cb618c69b75873f3ce893022fe08d1233b3152d/icons/obj/structures/crates.dmi

### _RMC14/Structures/Storage/Crates/basic.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Storage/Crates/basic.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing copyright / attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7cb618c69b75873f3ce893022fe08d1233b3152d/icons/obj/structures/crates.dmi

### _RMC14/Structures/Storage/Crates/densecrate.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Storage/Crates/densecrate.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing copyright / attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7cb618c69b75873f3ce893022fe08d1233b3152d/icons/obj/structures/crates.dmi , modified by Hyenh#6078(313846233099927552)

### _RMC14/Structures/Storage/Crates/secure_basic.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Storage/Crates/secure_basic.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing copyright / attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7cb618c69b75873f3ce893022fe08d1233b3152d/icons/obj/structures/crates.dmi

### _RMC14/Structures/Storage/Crates/woodcrate.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Storage/Crates/woodcrate.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing copyright / attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7cb618c69b75873f3ce893022fe08d1233b3152d/icons/obj/structures/crates.dmi

### _RMC14/Structures/Storage/Lockers/armory_locker.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Storage/Lockers/armory_locker.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing copyright / attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13

### _RMC14/Structures/Storage/Lockers/fridge.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Storage/Lockers/fridge.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing copyright / attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/c67c5213507a19e10e252ef2898d94354c0709bf/icons/obj/structures/closet.dmi

### _RMC14/Structures/Storage/Lockers/standard.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Storage/Lockers/standard.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing copyright / attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/c67c5213507a19e10e252ef2898d94354c0709bf/icons/obj/structures/closet.dmi

### _RMC14/Structures/Storage/rack.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Storage/rack.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing copyright / attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/9dd2c0d0a1c21e3c9bddbad1321c63fd886f61cf/icons/obj/objects.dmi

### _RMC14/Structures/Walls/hybrisa_wall.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Walls/hybrisa_wall.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing copyright / attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/turf/walls/hybrisa_colonywall.dmi

### _RMC14/Structures/Walls/prison_rwall.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Walls/prison_rwall.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing copyright / attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/09a5191fb11aab8ddffe3f9be94292b53e4d96f6/icons/turf/walls/prison.dmi

### _RMC14/Structures/Walls/strata_wall.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Walls/strata_wall.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing copyright / attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/09a5191fb11aab8ddffe3f9be94292b53e4d96f6/icons/turf/walls/strata_outpost.dmi

### _RMC14/Structures/barrels.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/barrels.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing copyright / attribution: https://github.com/cmss13-devs/cmss13/blob/7cb618c69b75873f3ce893022fe08d1233b3152d/icons/obj/structures/crates.dmi

### _RMC14/Structures/hybrisa_computer_props.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/hybrisa_computer_props.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing copyright / attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/structures/props/hybrisa/computers.dmi

## Context correction pass — 2026-09-24

Seventeen existing furniture/machine drafts were revised against their RSI frames and resolved prototypes: six table/desk families, hospital bed, bunk bed, three chairs, two lockers, refrigerator, washing machine, storage rack and colony generator. Beds now run along the source's horizontal axis with a right-hand pillow; the invented bunk ladder was removed. The black office chair uses a pedestal/caster base, and folding/wood chairs use their source frame shapes and colors. Tables use source palettes and a named real tabletop for prop placement. Closed cabinet faces, washer drum, refrigerator compressor and generator controls follow the reference layouts. Rear surfaces and dimensions remain inferred; this is not approval of all directions or states.

## Orientation correction pass — 2026-09-24

All model references now declare their RSI direction count. Scene placement respects fixed single-frame sprites, directional sprites and cardinal snapping instead of treating every saved rotation as a model rotation. The three wall-light drafts use the local south boundary. A fixture sharing a wall tile extends outside it; a fixture on the room tile beside a wall is reflected across that boundary to stay visible inside the room. Wall neighbours determine this placement; point-light emission offsets alone do not establish the fixture's physical depth. Padded sprite render offsets are not ground translations. Hybrisa and medical double-door modules occupy one tile each and apply a 90-degree authored offset, so paired entities fill their two-tile opening. Wall fixtures follow cutaways. Saved simulation transforms remain unchanged. These rules correct placement, not hidden-surface or animated-state fidelity.

## Remaining art validation

The interior expansion revises the coffee vendor and plant 6 in this file's original catalog. Their newer source notes are in `SOURCES_INTERIORS.md` and `SOURCES_PLANTS.md`; the other 53 additions are in separate prototype files. Existing source licenses below remain applicable.

Confirm silhouette and color from every available sprite direction, then inspect intermediate orbit angles. Round objects currently use stepped box silhouettes. Glass is an opaque tinted proxy. Rear surfaces, real dimensions, door animation, connected wall topology, held-item positioning and marine rigging have not been validated in game. Wall reference images are top-down connected icons, so matching a front elevation against them is not a meaningful fidelity measure; the model maintains a full tile footprint for later adjacent-wall review. Office chair upholstery colors were sampled from the original RSI. The starter catalog is not a complete Garrison conversion.
