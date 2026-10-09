# Garrison hardware source references

22 new draft assemblies and one revised APC. The source directions, prototype layering and available colliders were inspected. Editable solids contain no sprite pixels.

New geometric work is contributed under CC0-1.0 to the extent separately licensable. Source visual-design and derivative rights retain their existing licenses and attribution.

## Placement and fidelity

APCs, intercoms, ATM and vents use physical wall faces, including room-side mounting when the pivot is next to the wall. APC 96px padding is not copied as a displacement. The shower uses a 180-degree mounting-axis correction: its pipe is against the wall opposite the default curtain edge. Fax and register rest on actual table supports. Photocopiers, fridge, disposal and lights stand on their own bases. Floodlight aim follows saved rotation despite single-frame billboard art. Traffic signals retain their four directional frames. The wide sink has an open bowl and north-side rear mount.

Static light/control colors do not emit light or follow power, locks or damage. The cabinet represents saved filled cabinet #391, including stored entity #392; empty/open runtime states are unfinished. Other dynamic overlays and contents are not certified. Heights, hidden surfaces and small round forms remain inferred. All assets remain drafts.

## Models

| Model | Exact source references | Source state |
| --- | --- | --- |
| CMU3DWallAPC | CMApcConstructed, CMApcHighCapacity, CMApcLowCapacity, CMApcMediumCapacity | base |
| CMU3DWallIntercom | AUColonyIntercom, CMIntercomEngineering | intercom-p |
| CMU3DWallVentDuct | RMCMachinePropSmallVent | smallwallvent1 |
| CMU3DWallVentMesh | RMCMachinePropSmallVent2 | smallwallvent2 |
| CMU3DWallShower | CMShower | shower |
| CMU3DFaxMachine | CMFax, CMFaxCMB, RMCFaxLiaison | icon |
| CMU3DCashRegister | RMCCashRegisterOn | register_on |
| CMU3DPhotocopier | RMCPhotocopier, RMCPhotocopierSmallHitbox | bigscanner |
| CMU3DPhotocopierPro | RMCPhotocopierPro | bigscannerpro |
| CMU3DWallATM | AUColonyATM | atm |
| CMU3DSmartMedicalFridge | RMCSmartFridge | smartfridge |
| CMU3DDisposalUnit | CMDisposalUnit | disposal |
| CMU3DFireExtinguisher | CMFireExtinguisher | fire_extinguisher0 |
| CMU3DHybrisaExtinguisherCabinet | AU14HybrisaExtinguisherCabinet | frame |
| CMU3DSmallFloodlight | RMCSmallFloodlight | smallfloodon |
| CMU3DTallFloodlight | AU14TallFloodlight | bigfloodon |
| CMU3DTrafficSignal | RMCTrafficLight, colonysiren | trafficlight |
| CMU3DBrokenStreetlight | AU14StreetlightBroken | broken |
| CMU3DHandLantern | RMCFlashlightLantern | lantern |
| CMU3DRedGroundBeacon | RMCLightStickRedSmall | lightstick_red_variant1 |
| CMU3DRedBeaconPost | RMCLightStick | lightstick_spoke1 |
| CMU3DFloorVentPump | CMVentPump | on |
| CMU3DWideWashbasin | SinkWide | sink_wide |

## Source attribution

### Content.CMU/Resources/Textures/CMU14/Structures/Machines/atm.rsi

- Metadata: `Content.CMU/Resources/Textures/CMU14/Structures/Machines/atm.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Made by Mert Can Şahingeri for use in CMU14 or anything else u want

### Content.CMU/Resources/Textures/CMU14/fireextingushercabinet.rsi

- Metadata: `Content.CMU/Resources/Textures/CMU14/fireextingushercabinet.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from tgstation at https://github.com/tgstation/tgstation/commit/d0d81185f09ca30d3b0856d476544240dba0de53. Sprite modified by fattygarfield for use in AU14.

### Resources/Textures/Structures/Furniture/sink.rsi

- Metadata: `Resources/Textures/Structures/Furniture/sink.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: 'sink' and 'sink_stem' derived from tgstation https://github.com/tgstation/tgstation/commit/f01afc7edd39b28dd718407d5bbfca3a5dfe995f#diff-378d1b8f0f0a73185e7c82e4ccfdb65102561992a7abb306090ce851f8419780 and edited by Emisse for ss14, sink-fill-1 and sink_wide-fill-1 made by Topy for SS14

### Resources/Textures/_RMC14/Objects/Misc/Lights/floodlight.rsi

- Metadata: `Resources/Textures/_RMC14/Objects/Misc/Lights/floodlight.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cms13 at https://github.com/cmss13-devs/cmss13/blob/8cb02763e1dec491c90406db2124642d1275192a/icons/obj/structures/machinery/floodlight.dmi

### Resources/Textures/_RMC14/Objects/Misc/Lights/floodlightbig.rsi

- Metadata: `Resources/Textures/_RMC14/Objects/Misc/Lights/floodlightbig.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cms13 at https://github.com/cmss13-devs/cmss13/blob/ca94d2e8715b73103fa9f213be53d343359b4107/icons/obj/structures/machinery/big_floodlight.dmi

### Resources/Textures/_RMC14/Objects/Misc/Lights/lightstick.rsi

- Metadata: `Resources/Textures/_RMC14/Objects/Misc/Lights/lightstick.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7cb618c69b75873f3ce893022fe08d1233b3152d/icons/obj/items/lighting.dmi

### Resources/Textures/_RMC14/Objects/Misc/Lights/streetlight.rsi

- Metadata: `Resources/Textures/_RMC14/Objects/Misc/Lights/streetlight.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cms13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/streetlights.dmi

### Resources/Textures/_RMC14/Objects/Misc/Lights/trafficlights.rsi

- Metadata: `Resources/Textures/_RMC14/Objects/Misc/Lights/trafficlights.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cms13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/streetlights.dmi

### Resources/Textures/_RMC14/Objects/Tools/Light/lantern.rsi

- Metadata: `Resources/Textures/_RMC14/Objects/Tools/Light/lantern.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/884db783073c035b756c175b1bc75fb43279803e/icons/obj/items/lighting.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items/lighting_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items/lighting_righthand.dmi

### Resources/Textures/_RMC14/Objects/fire_extinguisher.rsi

- Metadata: `Resources/Textures/_RMC14/Objects/fire_extinguisher.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/106c92cdf232ebc12c9d7a2feb23956c6755496f/icons/obj/items/items.dmi, https://github.com/cmss13-devs/cmss13/blob/a1079f4912a96473dae51cceaa1d74eafb3939e2/icons/mob/humans/onmob/belt.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/equipment/tools_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/equipment/tools_righthand.dmi

### Resources/Textures/_RMC14/Structures/Furniture/shower.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Furniture/shower.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from CM-SS13 at commit https://github.com/cmss13-devs/cmss13/commit/cd8ab082e9c3de33652ce5cbf730026baefb6e96

### Resources/Textures/_RMC14/Structures/Machines/computer.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Machines/computer.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/d5b119380250ea512db2a5319e36592c7f604250/icons/obj/structures/machinery/computer.dmi, edits to overwatch and register by github noctyrnal

### Resources/Textures/_RMC14/Structures/Machines/fax_machine.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Machines/fax_machine.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/85bcc1a23e09e240ed086ac502c856b29a27e4a6/icons/obj/structures/machinery/library.dmi, inserting_mouse modified using https://github.com/space-wizards/space-station-14/commit/24e7653c984da133283457da2089e629161a7ff2 mouse-1 by KalimbaMachine

### Resources/Textures/_RMC14/Structures/Machines/photocopier.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Machines/photocopier.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/85bcc1a23e09e240ed086ac502c856b29a27e4a6/icons/obj/structures/machinery/library.dmi

### Resources/Textures/_RMC14/Structures/Machines/smart_fridge.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Machines/smart_fridge.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/5f522ad80e0d57ba86dd2ed43fc896a431ea44ab/icons/obj/structures/machinery/vending.dmi

### Resources/Textures/_RMC14/Structures/Piping/Atmospherics/vent_pump.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Piping/Atmospherics/vent_pump.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/6655c18d732a9e80ec918f444dabd5c824fdc111/icons/obj/pipes/vent_pump.dmi

### Resources/Textures/_RMC14/Structures/Piping/disposal.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Piping/disposal.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/icons/obj/pipes/disposal.dmi, https://github.com/cmss13-devs/cmss13/blob/icons/obj/structures/props/hybrisa/trash_bins.dmi

### Resources/Textures/_RMC14/Structures/Power/apc.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Power/apc.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/structures/machinery/power.dmi, https://github.com/cmss13-devs/cmss13/blob/275837de2cbb13c0708b32c7b8b29815d598b5d4/icons/obj/structures/machinery/apc.dmi

### Resources/Textures/_RMC14/Structures/Wallmounts/intercom.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Wallmounts/intercom.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/a767d448e1a73e7f96dea6bebc07deee8d54bdc2/icons/obj/items/radio.dmi , modified by Hyenh#6078 (313846233099927552)

### Resources/Textures/_RMC14/Structures/hybrisa_machine_wall_props.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/hybrisa_machine_wall_props.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/structures/props/hybrisa/piping_wiring.dmi
