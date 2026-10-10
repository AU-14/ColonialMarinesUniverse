# Garrison consoles and machinery

21 new draft assemblies. Geometry is authored from inspected source views and saved placement; no assembly is art-approved.

New geometric work is contributed under CC0-1.0 to the extent separately licensable. Source visual-design and derivative rights retain their existing licenses and attribution.

## Placement and source states

Desktop terminals, laptops, beverage dispensers, grinder and TV use exact authored surfaces at their saved pivots. ASRS, analyzer, recorder, telecom cabinet, chemical machines and technology mapping console stand on the floor. The 64px chemical storage uses local X -.5..+1.5 and the left tile pivot indicated by Sprite.offset .5,0. The analyzer instead has a centered two-tile collider; it is not shifted .5 tiles.

Overwatch has reversed east/west RSI frames compared with the other terminals. The saved seats beside #12189, #12193 and #12195 confirm the reversed sides. swapEastWest corrects those sides while leaving north/south, residual rotation and simulation transforms unchanged. The terminal1_old and comm PNG files have identical SHA256 hashes and share one explicit geometry mapping. Other terminal displays stay distinct.

Powered ASRS and monitor frames contain complete case artwork. Telecom maintenance overlay is hidden for this closed pose; powered server lamps and chemical-dispenser light were inspected separately. General references for those two layered machines show their uncomposited base. Clock-like animated displays, server power, dispenser contents, panel opening and live appearance are unfinished.

Monitor banks mount at the wall face and follow cutaways. Browser review corrected four displays sharing wall tiles and one green bank on a room tile. Their explicit wallFacingTargets select the adjacent workstation direction, including static unanchored computers. The backing prison windows at #9616/#12119 remain unmapped, and platform elevation remains unfinished, so those contexts still require review. Source monitor frames retain a frontal screen in all directions, with different baked shading; back and side construction is inferred. Depths, heights, key thickness and hidden structure remain drafts.

Comparison refinements reduced five machines' height/depth, widened the eight-screen banks' proportions and exposed the recorder straps over its cap. Directionless soda/bottle dispensers opt into faceAwayFromWall; this corrects five saved contexts where a raw rotation pointed along or into a wall. Unambiguous single-wall orientation and occupied corner-facing correction leave ambiguous layouts unchanged.

The current native adapter and offline export share these rules. The audit confirms all 76 saved transforms unchanged, 54 exact supports and two deliberately unsupported floor TVs. Model surfaces and runtime power states remain drafts.

## Models

| Model | Explicit source prototypes | Reference state |
| --- | --- | --- |
| CMU3DOldGreenCRTTerminal | AU14AdminConsole, AU14AmbassadorConsoleIS, AU14AmbassadorConsoleTWE, AU14AmbassadorConsoleUA, AU14CorporateConsole, RMCComputerIntel, RMCPropCommunicationsConsole | terminal1_old |
| CMU3DAmberDepartmentTerminal | AUColonyCommsConsole, AUDepartmentConsoleCivilian, AUDepartmentConsoleCommand, AUDepartmentConsoleCorporate, AUDepartmentConsoleEngineering, AUDepartmentConsoleLabor, AUDepartmentConsoleMedical, AUDepartmentConsoleSecurity, AUDepartmentConsoleServices, AUbudgetConsole | terminal |
| CMU3DIdentityCRTTerminal | RMCIdentificationComputer | engineering_terminal |
| CMU3DSecurityCRTTerminal | RMCSecurityCameraConsole | security_cam |
| CMU3DOverwatchCRTTerminal | RMCOverwatchConsoleGovforRotating | overwatch |
| CMU3DOpenBlackLaptop | AU14ObjectiveItemHackableComputer, AU14PropItemLaptop | laptop_off |
| CMU3DStandingASRSTerminal | CMASRSConsole, CMASRSConsoleGovfor | on |
| CMU3DBurgundyBlackBoxRecorder | AU14BlackBoxRecorder | blackbox |
| CMU3DWideAnalyzerMachine | AU14AnalyzerMachine | analyzer_idle |
| CMU3DBlueTelecomServer | CMTelecomServerFilled | comm_server_off |
| CMU3DBlueFourMonitorBank | RMCBlueMultiMonitorBig | bluemultimonitorbig_on |
| CMU3DBlueQuadSplitMonitor | RMCBlueMultiMonitorMedium | bluemultimonitormedium_on |
| CMU3DBlueEightMonitorBank | RMCBlueMultiMonitorSmall | bluemultimonitorsmall_on |
| CMU3DGreenEightMonitorBank | RMCMultiMonitorSmall | multimonitorsmall_on |
| CMU3DGroundChemicalDispenser | RMCChemDispenserGround | base |
| CMU3DWideChemicalStorage | RMCChemStorageGround | chemstorage |
| CMU3DCounterBoozeDispenser | CMDispenserBooze | booze_dispenser |
| CMU3DCounterSodaDispenser | CMDispenserSoda | soda_dispenser |
| CMU3DOpenHopperReagentGrinder | RMCKitchenReagentGrinder | juicer0 |
| CMU3DTechTreeMappingConsole | RMCTechTreeConsoleGovfor | techweb |
| CMU3DBlueSignalCRTTV | RMCTelevision | security_det |

## Source attribution

### Content.CMU/Resources/Textures/CMU14/Items/laptop.rsi

- Metadata: `Content.CMU/Resources/Textures/CMU14/Items/laptop.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13-pve at https://github.com/cmss13-devs/cmss13-pve/blob/c5f1cac3400d1f2975fca7257dd6202a2d1c1052/icons/obj/structures/props/server_equipment.dmi

### Content.CMU/Resources/Textures/CMU14/Structures/analyzermachine.rsi

- Metadata: `Content.CMU/Resources/Textures/CMU14/Structures/analyzermachine.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Sprites by Nzzy on Discord.

### Content.CMU/Resources/Textures/CMU14/Structures/blackbox.rsi

- Metadata: `Content.CMU/Resources/Textures/CMU14/Structures/blackbox.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13-pve at https://github.com/cmss13-devs/cmss13-pve/blob/b3d28c8f696cb6fbee0f3fc2a165d60807db91ef/icons/obj/structures/props/stationobjs.dmi

### Resources/Textures/_RMC14/Structures/Machines/Science/dispenser.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Machines/Science/dispenser.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/80da71e3cab8bb1ba03cea07cd4f5c57fd71b7f3/icons/obj/structures/machinery/science_machines.dmi

### Resources/Textures/_RMC14/Structures/Machines/Science/small_dispensers.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Machines/Science/small_dispensers.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/80da71e3cab8bb1ba03cea07cd4f5c57fd71b7f3/icons/obj/structures/machinery/science_machines.dmi

### Resources/Textures/_RMC14/Structures/Machines/asrs_console.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Machines/asrs_console.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/d5b119380250ea512db2a5319e36592c7f604250/icons/obj/structures/machinery/computer.dmi

### Resources/Textures/_RMC14/Structures/Machines/chemical_storage.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Machines/chemical_storage.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/6791699de749afc1c62bc65ec6f2326a00b3bb61/icons/obj/structures/machinery/science_machines_64x32.dmi

### Resources/Textures/_RMC14/Structures/Machines/computer.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Machines/computer.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/d5b119380250ea512db2a5319e36592c7f604250/icons/obj/structures/machinery/computer.dmi, edits to overwatch and register by github noctyrnal

### Resources/Textures/_RMC14/Structures/Machines/juicer.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Machines/juicer.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/4c03b0ed78e7565ec0fb4112f4f2e59421e239cc/icons/obj/structures/machinery/kitchen.dmi

### Resources/Textures/_RMC14/Structures/Machines/telecomms.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Machines/telecomms.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/c67c5213507a19e10e252ef2898d94354c0709bf/icons/obj/structures/props/stationobjs.dmi

### Resources/Textures/_RMC14/Structures/hybrisa_computer_props.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/hybrisa_computer_props.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/structures/props/hybrisa/computers.dmi
