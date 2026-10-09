# Garrison utility asset sources

This batch contains 40 draft models, 695 colored cuboids and 49 exact saved-map prototype references. No mapping duplicates the preceding furniture/environment catalogs.

## Modeling contract

X/Y horizontal, Z up, one tile per unit, front -Y. Tabletop props use local z=0 so the scene adapter can place them on supports. Wall hardware carries approximate mounting heights requiring map-transform review.

Source sprite sheets and relevant layered states were inspected before authoring. Every model remains **draft**. Solids capture structure, color areas and recognizable features; these are not pixel-matched reconstructions or final rigged meshes.

## Authorship and licensing

Coordinates and assemblies were newly authored for CMU with Codex assistance. No sprite pixels or existing 3D meshes are embedded. New geometry contributions are dedicated under **CC0-1.0** to the extent independently licensable. Source sprites, visual designs and derivative appearance rights retain their existing licenses; this does not relicense upstream artwork. Preserve applicable original attribution/share-alike terms. Unmodified RSI metadata remains authoritative.

[CC0-1.0](https://creativecommons.org/publicdomain/zero/1.0/) and [CC-BY-SA-3.0](https://creativecommons.org/licenses/by-sa/3.0/) license references.

## Exact references

| Model | Entity prototypes | Source metadata |
| --- | --- | --- |
| CMU3DSolarTracker | SolarTracker | `Resources/Textures/Structures/Power/Generation/solar_panel.rsi/meta.json` |
| CMU3DConveyorBelt | ConveyorBelt | `Resources/Textures/Structures/conveyor.rsi/meta.json` |
| CMU3DDeskLamp | RMCLamp | `Resources/Textures/_RMC14/Objects/Tools/Light/lamp.rsi/meta.json` |
| CMU3DBankerLamp | RMCLampGreen | `Resources/Textures/_RMC14/Objects/Tools/Light/green_lamp.rsi/meta.json` |
| CMU3DTripodLamp | RMCLampTripod | `Resources/Textures/_RMC14/Objects/Tools/Light/tripod_lamp.rsi/meta.json` |
| CMU3DFilingCabinet | CMFilingCabinet | `Resources/Textures/_RMC14/Structures/Storage/filing_cabinet.rsi/meta.json` |
| CMU3DTallFilingCabinet | CMFilingCabinetTall | `Resources/Textures/_RMC14/Structures/Storage/filing_cabinet.rsi/meta.json` |
| CMU3DWallAPC | CMApcMediumCapacity | `Resources/Textures/_RMC14/Structures/Power/apc.rsi/meta.json` |
| CMU3DMicrowave | CMMicrowave | `Resources/Textures/_RMC14/Structures/Machines/microwave.rsi/meta.json` |
| CMU3DFuelTank | RMCTankReagentFuel | `Resources/Textures/_RMC14/Structures/Storage/reagent_tank.rsi/meta.json` |
| CMU3DWaterTank | RMCTankReagentWater | `Resources/Textures/_RMC14/Structures/Storage/reagent_tank.rsi/meta.json` |
| CMU3DIVStand | CMIV | `Resources/Textures/_RMC14/Structures/Machines/Medical/iv_drip.rsi/meta.json` |
| CMU3DOperatingTable | CMOperatingTable | `Resources/Textures/_RMC14/Objects/Medical/Surgery/operating_table.rsi/meta.json` |
| CMU3DChemMaster | CMChemMaster | `Resources/Textures/_RMC14/Structures/Machines/Science/chem_master.rsi/meta.json` |
| CMU3DChemDispenser | RMCChemDispenserResearch, RMCChemDispenserMedbay | `Resources/Textures/_RMC14/Structures/Machines/Science/dispenser.rsi/meta.json` |
| CMU3DPortableDefibrillator | AU14CMDefibrillator | `Content.CMU/Resources/Textures/CMU14/Items/audefib.rsi/meta.json` |
| CMU3DDefibrillatorCabinet | AU14DefibrillatorCabinet, CMUDefibrillatorCabinetFilled | `Content.CMU/Resources/Textures/CMU14/Structures/audefibcabinet.rsi/meta.json` |
| CMU3DMedicalVendor | AU14GeneralMedicalVendor | `Resources/Textures/_RMC14/Structures/Machines/VendingMachines/med.rsi/meta.json` |
| CMU3DBloodVendor | AU14VendorBlood | `Resources/Textures/_RMC14/Structures/Machines/VendingMachines/blood.rsi/meta.json` |
| CMU3DMedicalEquipmentCase | RMCSecureCaseMedicalBig, RMCSecureCaseMedicalBigIV | `Resources/Textures/_RMC14/Structures/Storage/Crates/chestwhite.rsi/meta.json` |
| CMU3DSurgicalTray | RMCSurgicalTray | `Resources/Textures/_RMC14/Objects/Storage/surgical_tray.rsi/meta.json` |
| CMU3DHospitalRollerBed | RMCRollerBedHospital | `Resources/Textures/_RMC14/Structures/Furniture/rollerbeds.rsi/meta.json` |
| CMU3DSecureSafe | RMCLockerSecureSafe, RMCLockerSecureSafeColony, RMCLockerSecureSafeLiaison, RMCLockerSecureSafeLiaisonFilled | `Resources/Textures/_RMC14/Structures/Storage/Lockers/safe.rsi/meta.json` |
| CMU3DRotaryPhone | RMCRotaryPhone | `Resources/Textures/_RMC14/Structures/rotary_phone.rsi/meta.json` |
| CMU3DWallPhone | RMCRotaryPhoneWallmount, RMCRotaryPhoneWallmountAdmin | `Resources/Textures/_RMC14/Structures/wallmount_phone.rsi/meta.json` |
| CMU3DWallTelevision | RMCTelevisionWallMount | `Resources/Textures/_RMC14/Structures/Machines/computer.rsi/meta.json` |
| CMU3DGroundsideConsole | ComputerCriminalRecords, ComputerObjectivesGovfor | `Resources/Textures/_RMC14/Structures/Machines/rmc_groundside_communications_console.rsi/meta.json` |
| CMU3DMappingComputer | RMCBlackMappingComputer | `Resources/Textures/_RMC14/Structures/hybrisa_computer_props.rsi/meta.json` |
| CMU3DMPSComputer | RMCBlackMPSComputer | `Resources/Textures/_RMC14/Structures/hybrisa_computer_props.rsi/meta.json` |
| CMU3DSensorComputerWide | RMCBlackSensorComputer2 | `Resources/Textures/_RMC14/Structures/hybrisa_computer_props.rsi/meta.json` |
| CMU3DHybrisaHeavyShutter | RMCShutterHybrisa | `Resources/Textures/_RMC14/Structures/Doors/Shutters/Hybrisa/hybrisa_shutter.rsi/meta.json` |
| CMU3DHybrisaHeavyShutterOpen | CMUShutterLamentOpen | `Resources/Textures/_RMC14/Structures/Doors/Shutters/Hybrisa/hybrisa_shutter.rsi/meta.json` |
| CMU3DHybrisaGlassAirlock | CMAirlockGlassHybrisa | `Resources/Textures/_RMC14/Structures/Doors/Airlocks/hybrisa_door_glass.rsi/meta.json` |
| CMU3DMaintenanceAirlock | CMAirlockMaint | `Resources/Textures/_RMC14/Structures/Doors/Airlocks/maint_door.rsi/meta.json` |
| CMU3DSecurityAirlock | CMAirlockSecurity, CMAirlockSecurityLockedColony | `Resources/Textures/_RMC14/Structures/Doors/Airlocks/sec_door.rsi/meta.json` |
| CMU3DMedicalDoubleGlassDoor | CMDoubleDoorMedicalGlass | `Resources/Textures/_RMC14/Structures/Doors/Airlocks/Double/medical_glass.rsi/meta.json` |
| CMU3DExtinguisherCabinet | RMCExtinguisherCabinetAltFilled | `Resources/Textures/_RMC14/Structures/Wallmounts/extinguisher_cabinet_alt.rsi/meta.json` |
| CMU3DNeonBarSign | RMCOverheadSignNeonBar | `Resources/Textures/_RMC14/Structures/overhead_neon_signs.rsi/meta.json` |
| CMU3DCigaretteVendor | CMVendorCigs | `Resources/Textures/_RMC14/Structures/Machines/VendingMachines/cigs.rsi/meta.json` |
| CMU3DPowerLoaderDraft | RMCMechPowerLoader | `Resources/Textures/_RMC14/Objects/power_loader.rsi/meta.json` |

## Preserved upstream attribution

### CMU14/Items/audefib.rsi

- Metadata: `Content.CMU/Resources/Textures/CMU14/Items/audefib.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Normal defib sprites by aleksh on discord. Advanced Defib Sprites Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/05eaa3484b1a35aa759300bcfc8f0119f009daae/icons/obj/items/devices.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/medical_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/medical_lefthand.dmi

### CMU14/Structures/audefibcabinet.rsi

- Metadata: `Content.CMU/Resources/Textures/CMU14/Structures/audefibcabinet.rsi/meta.json`
- License: CC0-1.0
- Existing attribution: Created by aleksh on discord for AU14

### Structures/Power/Generation/solar_panel.rsi

- Metadata: `Resources/Textures/Structures/Power/Generation/solar_panel.rsi/meta.json`
- License: CC-BY-SA-4.0
- Existing attribution: KalimbaMachine (github) & CaasGit (github) for Space Station 14

### Structures/conveyor.rsi

- Metadata: `Resources/Textures/Structures/conveyor.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from tgstation at commit https://github.com/tgstation/tgstation/commit/8e33113cfe8b5da0e64d74c842bec0e6059d992d and modified by Swept and Peperos, switch-fwd and switch-rev modified by RedBookcase (Github)

### _RMC14/Objects/Medical/Surgery/operating_table.rsi

- Metadata: `Resources/Textures/_RMC14/Objects/Medical/Surgery/operating_table.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/f2cb08d6e44acb66becfc0bfea88b25c08c5102b/icons/obj/structures/machinery/surgery.dmi

### _RMC14/Objects/Storage/surgical_tray.rsi

- Metadata: `Resources/Textures/_RMC14/Objects/Storage/surgical_tray.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/ca94d2e8715b73103fa9f213be53d343359b4107/icons/obj/items/storage.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/medical_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/medical_lefthand.dmi Modified by Vermidia

### _RMC14/Objects/Tools/Light/green_lamp.rsi

- Metadata: `Resources/Textures/_RMC14/Objects/Tools/Light/green_lamp.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/884db783073c035b756c175b1bc75fb43279803e/icons/obj/items/lighting.dmi, https://github.com/cmss13-devs/cmss13/blob/bec6653d487a49aa2b5a8e0c97bed9612f620211/icons/mob/humans/onmob/items_righthand_0.dmi, https://github.com/cmss13-devs/cmss13/blob/bec6653d487a49aa2b5a8e0c97bed9612f620211/icons/mob/humans/onmob/items_lefthand_0.dmi

### _RMC14/Objects/Tools/Light/lamp.rsi

- Metadata: `Resources/Textures/_RMC14/Objects/Tools/Light/lamp.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/884db783073c035b756c175b1bc75fb43279803e/icons/obj/items/lighting.dmi, https://github.com/cmss13-devs/cmss13/blob/bec6653d487a49aa2b5a8e0c97bed9612f620211/icons/mob/humans/onmob/items_righthand_0.dmi, https://github.com/cmss13-devs/cmss13/blob/bec6653d487a49aa2b5a8e0c97bed9612f620211/icons/mob/humans/onmob/items_lefthand_0.dmi

### _RMC14/Objects/Tools/Light/tripod_lamp.rsi

- Metadata: `Resources/Textures/_RMC14/Objects/Tools/Light/tripod_lamp.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/884db783073c035b756c175b1bc75fb43279803e/icons/obj/items/lighting.dmi

### _RMC14/Objects/power_loader.rsi

- Metadata: `Resources/Textures/_RMC14/Objects/power_loader.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/bf507b617175e0838cb80714ae003325540ba037/icons/obj/vehicles/powerloader.dmi

### _RMC14/Structures/Doors/Airlocks/Double/medical_glass.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Airlocks/Double/medical_glass.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/f7b9367cb5c083a8c052a40dece9417031e28ffe/icons/obj/structures/doors/2x1medidoor.dmi

### _RMC14/Structures/Doors/Airlocks/hybrisa_door_glass.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Airlocks/hybrisa_door_glass.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/doors/hybrisa/hybrisa_generic.dmi

### _RMC14/Structures/Doors/Airlocks/maint_door.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Airlocks/maint_door.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/f7b9367cb5c083a8c052a40dece9417031e28ffe/icons/obj/structures/doors/maintdoor.dmi

### _RMC14/Structures/Doors/Airlocks/sec_door.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Airlocks/sec_door.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/78b741a92f09a787c9daf94bb750f98ffaa421cd/icons/obj/structures/doors/secdoor.dmi

### _RMC14/Structures/Doors/Shutters/Hybrisa/hybrisa_shutter.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Shutters/Hybrisa/hybrisa_shutter.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/doors/hybrisashutters.dmi

### _RMC14/Structures/Furniture/rollerbeds.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Furniture/rollerbeds.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/rollerbed.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/medical_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/medical_lefthand.dmi

### _RMC14/Structures/Machines/Medical/iv_drip.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Machines/Medical/iv_drip.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/f7b9367cb5c083a8c052a40dece9417031e28ffe/icons/obj/structures/machinery/iv_drip.dmi

### _RMC14/Structures/Machines/Science/chem_master.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Machines/Science/chem_master.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/80da71e3cab8bb1ba03cea07cd4f5c57fd71b7f3/icons/obj/structures/machinery/science_machines.dmi

### _RMC14/Structures/Machines/Science/dispenser.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Machines/Science/dispenser.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/80da71e3cab8bb1ba03cea07cd4f5c57fd71b7f3/icons/obj/structures/machinery/science_machines.dmi

### _RMC14/Structures/Machines/VendingMachines/blood.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Machines/VendingMachines/blood.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/09a5191fb11aab8ddffe3f9be94292b53e4d96f6/icons/obj/structures/machinery/vending.dmi

### _RMC14/Structures/Machines/VendingMachines/cigs.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Machines/VendingMachines/cigs.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/09a5191fb11aab8ddffe3f9be94292b53e4d96f6/icons/obj/structures/machinery/vending.dmi

### _RMC14/Structures/Machines/VendingMachines/med.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Machines/VendingMachines/med.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/09a5191fb11aab8ddffe3f9be94292b53e4d96f6/icons/obj/structures/machinery/vending.dmi

### _RMC14/Structures/Machines/computer.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Machines/computer.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/d5b119380250ea512db2a5319e36592c7f604250/icons/obj/structures/machinery/computer.dmi, edits to overwatch and register by github noctyrnal

### _RMC14/Structures/Machines/microwave.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Machines/microwave.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/b35c10dcb6f0ef8961243ad6477233fd886a4f35/icons/obj/structures/machinery/kitchen.dmi

### _RMC14/Structures/Machines/rmc_groundside_communications_console.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Machines/rmc_groundside_communications_console.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/d5b119380250ea512db2a5319e36592c7f604250/icons/obj/structures/machinery/computer.dmi

### _RMC14/Structures/Power/apc.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Power/apc.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/structures/machinery/power.dmi, https://github.com/cmss13-devs/cmss13/blob/275837de2cbb13c0708b32c7b8b29815d598b5d4/icons/obj/structures/machinery/apc.dmi

### _RMC14/Structures/Storage/Crates/chestwhite.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Storage/Crates/chestwhite.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7cb618c69b75873f3ce893022fe08d1233b3152d/icons/obj/structures/crates.dmi

### _RMC14/Structures/Storage/Lockers/safe.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Storage/Lockers/safe.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/obj/structures/safes.dmi

### _RMC14/Structures/Storage/filing_cabinet.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Storage/filing_cabinet.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/256d75e9420a19e4407318f81d9761c5ac3ac4cc/icons/obj/structures/props/misc.dmi

### _RMC14/Structures/Storage/reagent_tank.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Storage/reagent_tank.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/obj/structures/liquid_tanks.dmi

### _RMC14/Structures/Wallmounts/extinguisher_cabinet_alt.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Wallmounts/extinguisher_cabinet_alt.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/structures/closet.dmi, modified by github noctyrnal

### _RMC14/Structures/hybrisa_computer_props.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/hybrisa_computer_props.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/structures/props/hybrisa/computers.dmi

### _RMC14/Structures/overhead_neon_signs.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/overhead_neon_signs.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/structures/props/wall_decorations/hybrisa64x64_signs.dmi, miscvert7 edited by github noctyrnal

### _RMC14/Structures/rotary_phone.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/rotary_phone.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/f165d66c3a8b4ab02e6af02035c28e344d0bac69/icons/obj/structures/structures.dmi

### _RMC14/Structures/wallmount_phone.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/wallmount_phone.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/obj/structures/phone.dmi

## Remaining fidelity work

Static default poses only. Doors/shutters, drawers, locks, vendor contents, item removal, fluid levels, conveyor movement, powered screens and light emission are not connected to simulation. Tubing, lamp arms, dials and hydraulics use stepped boxes. Transparent glass needs sorting review. The loader has no pilot animation, gait or independently moving claws. BAR dot lettering is newly assembled geometry. Tabletop heights now apply where an exact authored support exists; remaining sprite offsets and unsupported surfaces still need review. Exact references measure authoring coverage, not completed gameplay replacement.

## First comparison correction pass

A read-only review of eight generated sheets identified source-specific layout errors. This revision corrects the medical vendor dispensing recess and lower crosses, ChemMaster cyan vessel, chemical-dispenser readout/dispensing bays, wall-phone receiver side and controls, conveyor V seams/color, loader arm/claw spread and foot width, and rotary handset/dial proportions. Conveyor, medical-vendor, phone and loader palettes were checked against source pixels. The APC was not changed: its selected 96x96 padded source frame is not useful for face comparison without directional/layer composition.

All statuses remain draft. Normalized sheets cannot establish world dimensions, hidden-surface fidelity or complete directional agreement. The revised models still require regenerated comparison sheets and scene review; no approval is implied by this correction pass.

## Orientation correction pass — 2026-09-24

Every utility reference now records its source RSI direction count for source-aware scene rotation. Fixed single-frame props retain their source facing; directional references follow their saved facing. Mounted utilities follow wall cutaways, and the source comparison shows the saved sprite direction. Sprite padding and screen-space offsets do not move physical ground pivots. Full source layer composition, powered states and animation remain outstanding.

## Context correction pass — 2026-09-24

The ChemMaster now has the reference's cyan left cap/window and separate pale right cap. The power loader's narrowed seat/counterweight and open cage separate cockpit space from its arms and frame. Five wall fixtures (APC, telephone, television and both equipment cabinets) sit outside the local south wall face rather than embedded in the tile. Desk/banker lamps, rotary phone, portable defibrillator, surgical tray and microwave declare surface placement, supported only by an exact authored tabletop under their pivot. The desk lamp was rebuilt with a low rounded base, thin curved stem and diagonal shallow shade after the assembled-room comparison exposed the previous blocky silhouette. All remain unlit static drafts with inferred depth/back faces. Current comparison sheets were regenerated and selected models inspected in the scene; complete directional/state fidelity remains open.

The earlier CMU3DMedicalEquipmentCase assembly was superseded by CMU3DMedicalStorageChest in garrison_interiors.yml after source review. Its duplicated prototype references were removed to make scene and native catalog selection unambiguous. Source attribution above remains applicable.
