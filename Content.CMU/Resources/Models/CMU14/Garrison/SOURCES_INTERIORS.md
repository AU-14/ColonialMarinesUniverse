# Garrison interior expansion sources

44 new draft models and a revision to the existing coffee vendor. Geometry was built from named solid parts after inspecting source images and available directional frames. Chair upholstery and folder colors were sampled from the source pixels. No sprite pixels are embedded in exported meshes.

The new geometric assembly is contributed under CC0-1.0 to the extent separately licensable. This does not change upstream visual-design or derivative rights: retain each source license and attribution below. All existing RSI metadata is unchanged.

## Coordinates and assumptions

One horizontal unit equals one map tile. Furniture fronts face local -Y; directional references declare their actual RSI direction count. The windoor occupies the source south-edge fixture strip. Hydroponics and the long planter use their source fixture footprints. Small paper goods, books, cups and the desktop computer use authored surface placement. Wall vents use the existing wall-context placement rule.

The source sheets supply silhouettes and color, not complete depth or measured height. Backs, undersides and real dimensions remain inferred. Glass is an opaque proxy. Door/tray movement, plant growth, writing, container contents, vendor power and light emission are not implemented. Sources with identical static art can share an exact model; this does not imply equivalent gameplay states.

## Model references

| Model | Source prototypes |
| --- | --- |
| CMU3DPaperSheet | CMPaper |
| CMU3DPen | CMPen |
| CMU3DFolderManila | CMFolderBase |
| CMU3DFolderBlack | CMFolderBlack, CMFolderBlackEmpty |
| CMU3DFolderBlue | CMFolderBlue, CMFolderBlueEmpty |
| CMU3DFolderRed | CMFolderRed, CMFolderRedEmpty |
| CMU3DFolderWhite | CMFolderWhite, CMFolderWhiteEmpty |
| CMU3DFolderYellow | CMFolderYellow, CMFolderYellowEmpty |
| CMU3DPaperBin | CMPaperBin10, CMPaperBin20, CMPaperBin30, CMPaperBin5 |
| CMU3DGuidebook | RMCGuidebookBase |
| CMU3DReinforcedTable | TableReinforced |
| CMU3DComfyChairBrown | CMChairComfy |
| CMU3DComfyChairAlpha | CMChairComfyAlpha |
| CMU3DComfyChairBravo | CMChairComfyBravo |
| CMU3DComfyChairBlue | CMChairComfyBlue |
| CMU3DComfyChairTeal | CMChairComfyTeal |
| CMU3DComfyChairCharlie | CMChairComfyCharlie |
| CMU3DComfyChairLime | CMChairComfyLime |
| CMU3DComfyChairARES | CMChairComfyARES |
| CMU3DSteelBench | SteelBench |
| CMU3DWoodCouchMiddle | RMCCouchMid |
| CMU3DWoodCouchEnd | RMCCouchEnd |
| CMU3DGrayLoungeChair | ComfyChair |
| CMU3DSecureCase | RMCSecureCase |
| CMU3DSecureCaseDouble | RMCSecureCaseDouble |
| CMU3DSecureCaseSmall | RMCSecureCaseSmall |
| CMU3DStrappedCargoCase | RMCCrateFlares200, RMCCrateFloodlightX4, RMCCratePowerLoaderBlue, RMCCratePowerLoaderGreen, RMCCrateTablesAndRacks, RMCSecureCaseStrapped, RMCSecureCaseStrappedFlare, RMCSecureCaseStrappedMRE, RMCSecureCaseStrappedMetal, RMCSecureCaseStrappedSandbag, RMCSecureCaseStrappedTripod, RMCSecureCaseStrappedWater |
| CMU3DMedicalStorageChest | AU14CrateMedicalBloodVendor, RMCSecureCaseMedicalBig, RMCSecureCaseMedicalBigIV |
| CMU3DCoffeeGrounds | RMCDrinkCoffeeGrind |
| CMU3DTakeawayCoffee | RMCDrinkCoffee |
| CMU3DWarningCone | CMWarningCone |
| CMU3DFullTrashBag | RMCPropTrashFull |
| CMU3DFusionFuelCell | RMCGeneratorFusionCell |
| CMU3DBookcase | RMCBookcase |
| CMU3DCoffeeVendor | AU14CashVendorHotDrinks, CMVendorCoffee, RMCVendorCoffeeSimple |
| CMU3DPersonalDesktop | RMCPersonalDesktop |
| CMU3DMorgueUnit | CMMorgue |
| CMU3DWindoor | CMWindoor |
| CMU3DHospitalPrivacyScreen | RMCHospitalDivider |
| CMU3DSmallWallVent | RMCMachinePropSmallVent3 |
| CMU3DLandingFloodlight | RMCLZFloodlight |
| CMU3DHydroponicsTray | CMHydroponicsTray |
| CMU3DConcretePlanter | RMCPlanter |
| CMU3DEngineeringReinforcedWall | RMCWallHybrisaEngiReinforced |
| CMU3DSolarisMineableRock | mineablesolarisrock, mineablesolarisrocksteel |

## Existing source attribution

### /Textures/Structures/Furniture/Tables/reinforced.rsi

- Metadata: `Resources/Textures/Structures/Furniture/Tables/reinforced.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from https://github.com/discordia-space/CEV-Eris/blob/0b3ab17dbad632ddf738b63900ef8df1926bba47/icons/obj/tables.dmi

### /Textures/Structures/Furniture/chairs.rsi

- Metadata: `Resources/Textures/Structures/Furniture/chairs.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from tgstation at commit https://github.com/tgstation/tgstation/commit/11402f6ae62facc2e8bcfa1f8ef5353b26663278, meat.png is CC0-1.0 by EmoGarbage404 (github) for Space Station 14. chair.png and its derrivatives taken from shiptest at commit https://github.com/shiptest-ss13/Shiptest/commit/f761c784812e827960a66cd10aac17ebc6edfac3, palette for chair.png, steel-bench.png and chair-greyscale.png taken from paradise equivalent chairs at commit https://github.com/ParadiseSS13/Paradise/commit/5ce5a66c814c4a60118d24885389357fd0240002, steel by SonicHDC, brass chair.png taken from tgstation at https://github.com/tgstation/tgstation/blob/b7e7779c19b76449c290aaf2150fb93545b1a79a/icons/obj/chairs.dmi, wooden bench by Ko4erga (discord), xeno-chair by juneszalkowska (discord)

### /Textures/Structures/Walls/stone.rsi

- Metadata: `Resources/Textures/Structures/Walls/stone.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: By AftrLite (GitHub). Based on the anywall format by 20nypercent and rye-rice (GitHub).

### /Textures/_RMC14/Objects/Consumable/Drinks/coffee.rsi

- Metadata: `Resources/Textures/_RMC14/Objects/Consumable/Drinks/coffee.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/icons/obj/items/drinks.dmi, https://github.com/cmss13-devs/cmss13/blob/99b0ad8ee1a07006991300f4b2728dcea7392624/icons/mob/humans/onmob/inhands/items/bottles_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/99b0ad8ee1a07006991300f4b2728dcea7392624/icons/mob/humans/onmob/inhands/items/bottles_lefthand.dmi

### /Textures/_RMC14/Objects/Consumable/Drinks/coffee_joe.rsi

- Metadata: `Resources/Textures/_RMC14/Objects/Consumable/Drinks/coffee_joe.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/icons/obj/items/drinks.dmi

### /Textures/_RMC14/Objects/Misc/Books/Other/book.rsi

- Metadata: `Resources/Textures/_RMC14/Objects/Misc/Books/Other/book.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/98e7e7e47ac177f77031e304da31a86689d4ae58/icons/obj/items/books.dmi, https://github.com/cmss13-devs/cmss13/blob/416abfba679334e9013bf485ad2a1dbba0579966/icons/mob/humans/onmob/inhands/items/books_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/416abfba679334e9013bf485ad2a1dbba0579966/icons/mob/humans/onmob/inhands/items/books_lefthand.dmi

### /Textures/_RMC14/Objects/Misc/Janitorial/cone.rsi

- Metadata: `Resources/Textures/_RMC14/Objects/Misc/Janitorial/cone.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/106c92cdf232ebc12c9d7a2feb23956c6755496f/icons/obj/janitor.dmi and https://github.com/cmss13-devs/cmss13/pull/7021/files#diff-f2c1dd5b4ac180c4cbb19cbb27bbb07377f5033020a53c2ad9134440256dd980, Originally made by TheManWithNoHands on github

### /Textures/_RMC14/Objects/Misc/Lights/floodlight.rsi

- Metadata: `Resources/Textures/_RMC14/Objects/Misc/Lights/floodlight.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cms13 at https://github.com/cmss13-devs/cmss13/blob/8cb02763e1dec491c90406db2124642d1275192a/icons/obj/structures/machinery/floodlight.dmi

### /Textures/_RMC14/Objects/Misc/paper.rsi

- Metadata: `Resources/Textures/_RMC14/Objects/Misc/paper.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/9ab207cd7ffba86a0411d7058645fb8f2a7895f3/icons/obj/items/paper.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/equipment/paperwork_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/equipment/paperwork_righthand.dmi, paper_stamp-provost, paper_stamp-provost-inspector, stamp-provost, paper_stamp-sea, and stamp-sea made by pursuitinashes (discord) based off of paper_stamp-marine and stamp-deny. paper_stamp-clf, paper_stamp-spp, paper_stamp-tse, and paper_stamp-free-press created by crazy1112345 (discord). stamp-clf, stamp-spp, stamp-tse, and stamp-free-press created by crazy1112345, based on stamp-marine. weya_pen made by SharkSnake98. Standard paper stamp overlays taken from tgstation at https://github.com/tgstation/tgstation/commit/e1142f20f5e4661cb6845cfcf2dd69f864d67432, with paper_stamp-syndicate by Veritius, paper_stamp-greytide by ubaser, paper_stamp-psychologist by clinux, and paper_stamp-wizard by brassicaprime69 (Discord), paper_stamp-cca by Oslo, https://github.com/cmss13-devs/cmss13/blob/52681260f1021befe2cdce04d8e21deee78e8b07/icons/obj/items/paper.dmi

### /Textures/_RMC14/Objects/Misc/prop_trash.rsi

- Metadata: `Resources/Textures/_RMC14/Objects/Misc/prop_trash.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/9ab207cd7ffba86a0411d7058645fb8f2a7895f3/icons/obj/items/paper.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/equipment/paperwork_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/obj/structures/props/hybrisa/misc_props.dmi

### /Textures/_RMC14/Objects/Power/fusion_fuel_cell.rsi

- Metadata: `Resources/Textures/_RMC14/Objects/Power/fusion_fuel_cell.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/09a5191fb11aab8ddffe3f9be94292b53e4d96f6/icons/obj/structures/machinery/shuttle-parts.dmi

### /Textures/_RMC14/Structures/Doors/Windoors/windoor.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Windoors/windoor.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/f7b9367cb5c083a8c052a40dece9417031e28ffe/icons/obj/structures/doors/windoor.dmi

### /Textures/_RMC14/Structures/Furniture/Couches/couch.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Furniture/Couches/couch.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/055c4127d76facec463411169bd2bc513980b0e1/icons/obj/structures/props/sofas.dmi

### /Textures/_RMC14/Structures/Furniture/bookshelf.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Furniture/bookshelf.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7cb618c69b75873f3ce893022fe08d1233b3152d/icons/obj/structures/structures.dmi

### /Textures/_RMC14/Structures/Furniture/chairs.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Furniture/chairs.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/9dd2c0d0a1c21e3c9bddbad1321c63fd886f61cf/icons/obj/objects.dmi, https://github.com/cmss13-devs/cmss13/blob/6a955a3c180f3efcf3b997c230fff4d634eb0629/icons/obj/structures/machinery/yautja_machines.dmi, ai_interface_chair taken from cmss13-pve at https://github.com/cmss13-devs/cmss13-pve/blob/master/icons/obj/objects.dmi

### /Textures/_RMC14/Structures/Furniture/hospital_divider.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Furniture/hospital_divider.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/curtain.dmi

### /Textures/_RMC14/Structures/Furniture/hybrisa_planter.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Furniture/hybrisa_planter.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/hybrisa/64x64_props.dmi

### /Textures/_RMC14/Structures/Machines/VendingMachines/coffee.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Machines/VendingMachines/coffee.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/09a5191fb11aab8ddffe3f9be94292b53e4d96f6/icons/obj/structures/machinery/vending.dmi

### /Textures/_RMC14/Structures/Machines/computer.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Machines/computer.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/d5b119380250ea512db2a5319e36592c7f604250/icons/obj/structures/machinery/computer.dmi, edits to overwatch and register by github noctyrnal

### /Textures/_RMC14/Structures/Storage/Crates/case.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Storage/Crates/case.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7cb618c69b75873f3ce893022fe08d1233b3152d/icons/obj/structures/crates.dmi

### /Textures/_RMC14/Structures/Storage/Crates/casedouble.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Storage/Crates/casedouble.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7cb618c69b75873f3ce893022fe08d1233b3152d/icons/obj/structures/crates.dmi

### /Textures/_RMC14/Structures/Storage/Crates/casesmall.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Storage/Crates/casesmall.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7cb618c69b75873f3ce893022fe08d1233b3152d/icons/obj/structures/crates.dmi

### /Textures/_RMC14/Structures/Storage/Crates/chestwhite.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Storage/Crates/chestwhite.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7cb618c69b75873f3ce893022fe08d1233b3152d/icons/obj/structures/crates.dmi

### /Textures/_RMC14/Structures/Storage/Crates/securecrate.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Storage/Crates/securecrate.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7cb618c69b75873f3ce893022fe08d1233b3152d/icons/obj/structures/crates.dmi

### /Textures/_RMC14/Structures/Storage/morgue.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Storage/morgue.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/c7b4d6bd868de669ad96f1d3e4dc3702a3404355/icons/obj/structures/props/stationobjs.dmi

### /Textures/_RMC14/Structures/Walls/hybrisa_rwall_engineering.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Walls/hybrisa_rwall_engineering.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/turf/walls/hybrisa_engineering_wall.dmi

### /Textures/_RMC14/Structures/hybrisa_machine_wall_props.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/hybrisa_machine_wall_props.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/structures/props/hybrisa/piping_wiring.dmi

### /Textures/_RMC14/Structures/hydroponics.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/hydroponics.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/f5f8aeb056180d8385742caef806377ad1f82987/icons/obj/structures/machinery/hydroponics.dmi
