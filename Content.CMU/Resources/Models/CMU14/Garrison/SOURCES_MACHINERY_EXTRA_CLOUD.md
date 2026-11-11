# Station machinery extra: source-owned art-only drafts

Source repository: TheHellFireo/CMU-Garrison-3D, checkpoint `6e37a4d0a7d9433838c393a82c02422cb704dd5a`.

## Scope

17 assigned missing physical visual prototype types have 20 static assemblies. The three extra navigation direction studies do not add target types or saved placements. The historical Redux inventory has 20 occurrences of these 17 types (classic has five); these are potential art references, not verified map placements, native rendering admissions, complete gameplay-state coverage or fidelity approval. Every assembly remains `draft`. No engine/runtime file, map, entity prototype, source pixel, original export, game process, remote branch or published asset was modified.

Editable model definitions: `garrison_machinery_extra_cloud.yml`; decorative source surfaces: `garrison_machinery_extra_cloud_art.yml` and `Textures/CMU14/ThreeD/machinery_extra_cloud/`. All source-guided supported primitive parts are real volumes. Each model has 9–102 parts, below 128. Models use Box, CylinderX/Y/Z, Ellipsoid and supported part rotations. Small screen, keyboard, label, lamp and instrument crops are decorative surfaces on independent solid structure. The large tactical screen is one original map-schematic crop on an inset horizontal panel; it is not a whole-object sprite slab and does not show live map information.

Nineteen assemblies are one connected exact convex-mesh component. The water-cooler scene has one intentionally separate foreground spare-bottle component; its second spare touches the cabinet in the source-guided arrangement. Source spare props are not artificially joined. All lowest actual transformed mesh vertices are normalized to isolated floor Z=0. This datum does not establish in-map floor elevation, surface height or wall attachment. Horizontal source pixel scale is 1/32 tile; inferred vertical scale is either .038 or .03125 tile/pixel, with separately inferred horizontal planes for the grill, surgical bed and map table.

## Source interpretation and limitations

Source YAML and the materialized checkpoint inventory were both inspected. The resolved Sprite fields agree for every assigned type. All ancestry is present except the generic heater base and the two water-solution bases; those missing non-art base definitions are explicitly recorded in the source-contract audit. This is not a simulation of initialized component state. The compositions below are deliberately selected visible source layers at frame zero. Unshaded source pixels retain RGB but no emissive lighting is implemented. No source tint was present in these compositions. Subsequent entity tint, shader, light, transparent liquid, refraction, UI content and runtime appearance changes are unverified.

`sourceDirections` and explicit `referenceDirection` retain the RSI direction count. The navigation terminal's S/N/E/W views have distinct geometry and small detail surfaces; only South carries its prototype mapping. Each has the same reciprocal `directionalModels` list. This records intended static selection, without claiming native direction or animation approval. All remaining sources have one direction. Single-frame noRot/snapCardinals behavior, arbitrary entity rotation, held/worn overlays and camera-space elevation have not been verified.

Wide-source X pivots are authored for the chemical simulator (+0.5), fuel pump (-0.5), large map table (+1) and sensor tower (+0.5). Source Y sprite offsets (+0.5, +1 or +2) represent projection/elevation and were not blindly copied into world XY. Those cases, the wall recharger's mount height, the table's top plane, the sensor's guy anchor, and all actual floor/wall/neighbor contacts remain draft. Inferred rear casings, bowl concavity, fine curves, materials and hidden construction still need fidelity review.

No `spriteStates`, `chargerAppearance`, native appearance adapter or animation clips are authored in this family. Animated resources, alternative power/damage/fold/operation states and their original delays are inventoried and preserved in source metadata, but unbound. None is counted as completed gameplay coverage.

## Authored assemblies

| Source prototype | Model / static source composition | Parts | Remaining state/placement limitations |
|---|---|---:|---|
| SeedExtractor | `CMU3DSeedExtractorCloud` / seedextractor-off + seedextractor-unlit, direction 0 | 39 | Power and extraction loops; pod depth/rings and output mechanism inferred |
| KitchenElectricGrill | `CMU3DElectricGrillCloud` / icon, direction 0 | 31 | Low/medium/high overlays, heat, placed food and surface mounting unbound |
| WallWeaponCapacitorRecharger | `CMU3DWallCapacitorRechargerCloud` / empty + light-off, direction 0 | 15 | Light loops, inserted device overlay and wall elevation unbound; full PNG equals empty |
| CMArmylathe | `CMU3DArmyLatheCloud` / armylathe + armylathe_u, direction 0 | 37 | Closed maintenance panel selected; maintenance/running/material/power changes unbound |
| CMUXRFScanner | `CMU3DXRFScannerCloud` / base, direction 0 | 49 | Sample/processing/error/failed/finished states and sample items unbound |
| CMUChemSimulator | `CMU3DChemSimulatorCloud` / modifier, direction 0 | 78 | Off/running/ready/reading/printing; source +0.5,+0.5 offset and actual UI unverified |
| VehicleSupplyConsole | `CMU3DVehicleSupplyConsoleCloud` / off + on, direction 0 | 23 | Power, broken state and actual vehicle-order UI unbound |
| CMComputerDropshipNavigationPlanetside | `CMU3DDropshipNavigationCloudSouth` / on, direction 0 | 16 | Four explicit direction studies, on frame zero; five-frame on cycle/off/broken/UI unbound |
| CMComputerDropshipNavigationPlanetside | `CMU3DDropshipNavigationCloudNorth` / on, direction 1; extra direction study, no mapping | 21 | Four explicit direction studies, on frame zero; five-frame on cycle/off/broken/UI unbound |
| CMComputerDropshipNavigationPlanetside | `CMU3DDropshipNavigationCloudEast` / on, direction 2; extra direction study, no mapping | 21 | Four explicit direction studies, on frame zero; five-frame on cycle/off/broken/UI unbound |
| CMComputerDropshipNavigationPlanetside | `CMU3DDropshipNavigationCloudWest` / on, direction 3; extra direction study, no mapping | 21 | Four explicit direction studies, on frame zero; five-frame on cycle/off/broken/UI unbound |
| RMCFuelPump | `CMU3DFuelPumpCloud` / fuelpump_off, direction 0 | 102 | Unpowered fuelpump_off only; fill fractions/powered ten-frame cycles and pipe hookups unbound |
| RMCCanisterBlue | `CMU3DBlueCanisterCloud` / blue, direction 0 | 35 | Single blue decorative state; separate destroyed prototype and valve/gas behavior untouched |
| TwoWayLever | `CMU3DTwoWayLeverCloud` / switch-off, direction 0 | 10 | Middle only; switch-fwd/switch-rev and device behavior unbound |
| CMPortableSurgicalBed | `CMU3DPortableSurgicalBedCloud` / surgical_down, direction 0 | 21 | Unfolded surgical_down only; surgical_up/folded/held/worn/transitions unbound |
| RMCWaterCoolerStacks | `CMU3DWaterCoolerStacksCloud` / water_cooler_2, direction 0 | 36 | Three source bottle props; liquid/refraction/fill, dispensing and bin contents unbound |
| ANPRCMastAntenna | `CMU3DANPRCMastAntennaCloud` / antenna_m, direction 0 | 9 | Loose mast attachment, not deployed tower; equipped radio/held/worn appearances unbound |
| CMUTacticalMapTableLargeGovfor | `CMU3DTacticalMapLargeGovforCloud` / maptable + maptable-on, direction 0 | 34 | Static source map schematic; power/UI/live tactical content and top-plane elevation unbound |
| AU14CommsMastGovfor | `CMU3DCommsMastGovforCloud` / comm_tower_off, direction 0 | 62 | Intact off artwork only; comm_tower_destroyed and mounting unbound |
| AU14SensorTower | `CMU3DSensorTowerCloud` / sensor_off, direction 0 | 80 | Off only; on loop/broken/Weld/Wire/Wrench and actual guy attachment/elevation unbound |

## Evidence and reproduction

- `Tools/three_d/author_machinery_extra_cloud.py`: deterministic original pipeline GLBs, editable YAML, extracted decorative crops and source/front/orbit/rear previews
- `Tools/three_d/audit_machinery_extra_sources.py`: actual inherited source appearance contracts, inventoried source Sprite cross-checks and missing generic ancestors
- `Tools/three_d/check_machinery_extra_contacts.py`: exact transformed convex primitive hull contact graph; not a broad AABB-only proof
- `Tools/three_d/verify_machinery_extra_cloud.py`: original Git blob SHA checks, exact RGBA source-crop comparison, reserved atlas allocation, source direction checks, finite GLB buffer/accessor/index/node values, embedded PNG validation, deterministic byte repeat, eight sampled aperture checks, source palette and ground/contact requirements, and read-only global duplicate checks
- `Tools/three_d/check_machinery_extra_blender.py`: independent Blender 4.3.2 imports, no re-export
- `Tools/three_d/generated/machinery-extra-cloud-{verification,integrity,contact-checks,source-contracts,blender-import}.json`: resulting evidence
- `Tools/three_d/generated/review/machinery-extra-cloud/`: individual comparisons, source compositions and orbit/rear previews
- `Tools/three_d/generated/machinery-extra-cloud-files.json`: file manifest with SHA-256 checksums

The eight aperture checks sample spaces above the grill tray and through its handle, the valve-crown mouth, XRF carry handle, seed output recess, lathe fabrication bay, recharger insertion slot and comms rack bay. They establish those sample points are clear of actual transformed mesh hulls; they are not a full collision/fit test. No map, native, complete state or fidelity approval is implied. Khronos glTF validator is unavailable and was not run; no equivalent certification is claimed. All 20 GLBs imported in independent Blender 4.3.2.

Source PNGs, UTF-8 YAML and RSI metadata are retained byte-for-byte under `reference/machinery-extra-source/` with their repository-relative paths; `reference/machinery-extra-fetches.json` records verified source URLs and original Git blob SHA values. Use the specific source license below for each derivative; do not strip attribution or substitute a blanket project license. Decorative crops are exact source pixels and geometry is a new source-guided draft.

## Source attribution

### ANPRCMastAntenna

- Resource: `/Textures/CMU14/Objects/Radio/anprc117g.rsi`
- License: CC-BY-SA-3.0
- Original attribution: AU14 contributors
- Source metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Textures/CMU14/Objects/Radio/anprc117g.rsi/meta.json

### AU14CommsMastGovfor

- Resource: `/Textures/_RMC14/Structures/communications_tower_alt.rsi`
- License: CC-BY-SA-3.0
- Original attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/e77c994c8b3fcf97b13886de7c56c6b407108598/icons/obj/structures/machinery/comm_tower.dmi
- Source metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/communications_tower_alt.rsi/meta.json

### AU14SensorTower

- Resource: `/Textures/_RMC14/Structures/Machines/sensor_tower.rsi`
- License: CC-BY-SA-3.0
- Original attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/f7b9367cb5c083a8c052a40dece9417031e28ffe/icons/obj/structures/machinery/motion_sensor_v2.dmi
- Source metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Machines/sensor_tower.rsi/meta.json

### CMArmylathe

- Resource: `/Textures/_RMC14/Structures/Machines/armylathe.rsi`
- License: CC-BY-SA-3.0
- Original attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/structures/machinery/autolathe.dmi
- Source metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Machines/armylathe.rsi/meta.json

### CMComputerDropshipNavigationPlanetside

- Resource: `/Textures/_RMC14/Structures/Machines/dropship_nav_computer.rsi`
- License: CC-BY-SA-3.0
- Original attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/d5b119380250ea512db2a5319e36592c7f604250/icons/obj/structures/machinery/computer.dmi
- Source metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Machines/dropship_nav_computer.rsi/meta.json

### CMPortableSurgicalBed

- Resource: `/Textures/_RMC14/Structures/Furniture/rollerbeds.rsi`
- License: CC-BY-SA-3.0
- Original attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/rollerbed.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/medical_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/medical_lefthand.dmi
- Source metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Furniture/rollerbeds.rsi/meta.json

### CMUChemSimulator

- Resource: `/Textures/CMU14/Structures/Machines/Science/chem_simulator.rsi`
- License: CC-BY-SA-3.0
- Original attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/6791699de749afc1c62bc65ec6f2326a00b3bb61/icons/obj/structures/machinery/science_machines_64x32.dmi
- Source metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Textures/CMU14/Structures/Machines/Science/chem_simulator.rsi/meta.json

### CMUTacticalMapTableLargeGovfor

- Resource: `/Textures/_RMC14/Structures/Machines/large_map_table.rsi`
- License: CC-BY-SA-4.0
- Original attribution: Sprites by github noctyrnal
- Source metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Machines/large_map_table.rsi/meta.json

### CMUXRFScanner

- Resource: `/Textures/_RMC14/Structures/Machines/Science/reagent_analyzer.rsi`
- License: CC-BY-SA-3.0
- Original attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/80da71e3cab8bb1ba03cea07cd4f5c57fd71b7f3/icons/obj/structures/machinery/science_machines.dmi
- Source metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Machines/Science/reagent_analyzer.rsi/meta.json

### KitchenElectricGrill

- Resource: `/Textures/Structures/Machines/electric_grill.rsi`
- License: CC0-1.0
- Original attribution: Original base by deltanedas (github) for SS14. Resprited by (DISCORD)@ps3moira#9488
- Source metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/Structures/Machines/electric_grill.rsi/meta.json

### RMCCanisterBlue

- Resource: `/Textures/_RMC14/Structures/atmos.rsi`
- License: CC-BY-SA-3.0
- Original attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/e8152b093efb4791bbdd77d0b111f45dac8e074a/icons/obj/structures/machinery/atmos.dmi
- Source metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/atmos.rsi/meta.json

### RMCFuelPump

- Resource: `/Textures/_RMC14/Structures/Power/fuel_pump.rsi`
- License: CC-BY-SA-3.0
- Original attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/69d202154a8b4e8315f7fe760f67664b25134735/icons/obj/structures/machinery/fuelpump.dmi
- Source metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Power/fuel_pump.rsi/meta.json

### RMCWaterCoolerStacks

- Resource: `/Textures/_RMC14/Structures/Furniture/water_cooler.rsi`
- License: CC-BY-SA-3.0
- Original attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/cf8d9e75c75c291e3d44f39bf4856075c077f509/icons/obj/structures/machinery/vending.dmi
- Source metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Furniture/water_cooler.rsi/meta.json

### SeedExtractor

- Resource: `/Textures/Structures/Machines/seed_extractor.rsi`
- License: CC-BY-SA-3.0
- Original attribution: Taken from tgstation at commit https://github.com/tgstation/tgstation/commit/c6e3401f2e7e1e55c57060cdf956a98ef1fefc24. Modified by potato1234x (github) for SS14
- Source metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/Structures/Machines/seed_extractor.rsi/meta.json

### TwoWayLever

- Resource: `/Textures/Structures/conveyor.rsi`
- License: CC-BY-SA-3.0
- Original attribution: Taken from tgstation at commit https://github.com/tgstation/tgstation/commit/8e33113cfe8b5da0e64d74c842bec0e6059d992d and modified by Swept and Peperos, switch-fwd and switch-rev modified by RedBookcase (Github)
- Source metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/Structures/conveyor.rsi/meta.json

### VehicleSupplyConsole

- Resource: `/Textures/_RMC14/Structures/Machines/asrs_console.rsi`
- License: CC-BY-SA-3.0
- Original attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/d5b119380250ea512db2a5319e36592c7f604250/icons/obj/structures/machinery/computer.dmi
- Source metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Machines/asrs_console.rsi/meta.json

### WallWeaponCapacitorRecharger

- Resource: `/Textures/Structures/Power/wall_recharger.rsi`
- License: CC-BY-SA-3.0
- Original attribution: https://github.com/discordia-space/CEV-Eris/raw/9ea3eccbe22e18d24653949067f3d7dd12194ea9/icons/obj/stationobjs.dmi
- Source metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/Structures/Power/wall_recharger.rsi/meta.json
