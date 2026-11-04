# Stable door-pose source references

49 additional draft assemblies; 39 existing assemblies revised or linked. Source sprites and stable open/closed frames were inspected. New geometry uses named solids, with no embedded sprite pixels.

The new geometric assembly is contributed under CC0-1.0 to the extent separately licensable. Source visual-design and derivative rights remain under their existing licenses; retain the attribution below.

## Mechanisms, pivots and limitations

Rolling shutters retract upward and keep a raised lower bar. Blast doors retract into the floor, leaving a low threshold recess. The legacy DoubleDoor family is one entity spanning local Y -0.49 through 1.49; its center lies half a tile away from the entity pivot. It is different from paired CMDoubleDoor one-tile leaves. Airlock open poses retain frames and retract the leaves; no transparent closed leaf is used as an open pose.

Alternate models are explicit reciprocal links for stable Open/Closed poses. Transitions, welding, damage and power overlays remain unsupported. State-only geometry does not claim a duplicate exact prototype mapping. Height, backs and hidden mechanisms are inferred. All assets remain drafts.

## Assemblies

| Model | Pose | Art reference | RSI state |
| --- | --- | --- | --- |
| CMU3DAlmayerBlastDoor | Closed | RMCPodDoorAlmayer | closed |
| CMU3DAlmayerBlastDoorOpen | Open | RMCPodDoorAlmayer | open |
| CMU3DBlackBlastDoor | Closed | RMCPodDoorAlmayerBlack | closed |
| CMU3DBlackBlastDoorOpen | Open | RMCPodDoorAlmayerBlack | open |
| CMU3DRedBlastDoor | Closed | RMCPodDoorHybrisaRed | closed |
| CMU3DRedBlastDoorOpen | Open | RMCPodDoorHybrisaRed | open |
| CMU3DUltraBlastDoor | Closed | RMCPodDoorHybrisaIndestructibleUltra | closed |
| CMU3DUltraBlastDoorOpen | Open | RMCPodDoorHybrisaIndestructibleUltra | open |
| CMU3DAlmayerRollingShutter | Closed | RMCShutterAlmayer | closed |
| CMU3DAlmayerRollingShutterOpen | Open | RMCShutterAlmayer | open |
| CMU3DLegacyAlmayerDoor | Closed | RMCDoubleDoorAlmayerSolid | closed |
| CMU3DLegacyAlmayerDoorOpen | Open | RMCDoubleDoorAlmayerSolid | open |
| CMU3DLegacyEngineerDoor | Closed | RMCDoubleDoorEngineerGlass | closed |
| CMU3DLegacyEngineerDoorOpen | Open | RMCDoubleDoorEngineerGlass | open |
| CMU3DSinglePersonalDoorOpen | Open | CMAirlock | open |
| CMU3DSingleCommandGovforDoorGlassOpen | Open | CMAirlockCommandGovforGlassLocked | open |
| CMU3DSingleCommandGovforDoorOpen | Open | CMAirlockCommandGovforLocked | open |
| CMU3DSingleComDoorOpen | Open | CMAirlockCommandLockedColony | open |
| CMU3DSingleEngineerGovforDoorGlassOpen | Open | CMAirlockEngineerGovforGlassLocked | open |
| CMU3DSingleEngineerGovforDoorOpen | Open | CMAirlockEngineerGovforLocked | open |
| CMU3DSingleEngiDoorOpen | Open | CMAirlockEngineerLockedColony | open |
| CMU3DSingleEngiDoorGlassOpen | Open | CMAirlockGlassEngineer | open |
| CMU3DSingleMediDoorGlassOpen | Open | CMAirlockGlassMedicalLockedColony | open |
| CMU3DSingleSecDoorGlassOpen | Open | CMAirlockGlassSecurityLockedColony | open |
| CMU3DSingleGovforDoorGlassOpen | Open | CMAirlockGovforGlassLocked | open |
| CMU3DSingleGovforDoorOpen | Open | CMAirlockGovforLocked | open |
| CMU3DSingleMediDoorOpen | Open | CMAirlockMedical | open |
| CMU3DSingleMedicalGovforDoorGlassOpen | Open | CMAirlockMedicalGovforGlassLocked | open |
| CMU3DSingleMedicalGovforDoorOpen | Open | CMAirlockMedicalGovforLocked | open |
| CMU3DSingleSecurityGovforDoorGlassOpen | Open | CMAirlockSecurityGovforGlassLocked | open |
| CMU3DSingleSecurityGovforDoorOpen | Open | CMAirlockSecurityGovforLocked | open |
| CMU3DPairedAlmayerGlassOpen | Open | CMDoubleDoorAlmayerGlass | door_open |
| CMU3DPairedCommandGlassOpen | Open | CMDoubleDoorColonyCommandGlassLocked | door_open |
| CMU3DPairedEngineerGlassOpen | Open | CMDoubleDoorColonyEngineerGlassLocked | door_open |
| CMU3DPairedEngineerSolidOpen | Open | CMDoubleDoorColonyEngineerSolidLocked | door_open |
| CMU3DPairedSecurityGlassOpen | Open | CMDoubleDoorColonySecurityGlassLocked | door_open |
| CMU3DPairedSecuritySolidOpen | Open | CMDoubleDoorColonySecuritySolidLocked | door_open |
| CMU3DPairedCommandGovforGlassOpen | Open | CMDoubleDoorCommandGovforGlassLocked | door_open |
| CMU3DPairedGovforGlassOpen | Open | CMDoubleDoorGovforGlassLocked | door_open |
| CMU3DPairedGovforSolidOpen | Open | CMDoubleDoorGovforLocked | door_open |
| CMU3DPairedPersonalSolidOpen | Open | CMDoubleDoorPersonalSolid | door_open |
| CMU3DPairedHybrisaMedicalGlassOpen | Open | RMCDoubleDoorHybrisaGlassMecical | door_open |
| CMU3DHybrisaAirlockOpen | Open | RMCAirlockHybrisa | open |
| CMU3DHybrisaPersonalAirlockOpen | Open | RMCAirlockHybrisaPersonal | open |
| CMU3DHybrisaDoubleGlassDoorOpen | Open | RMCDoubleDoorGlassHybrisa | door_open |
| CMU3DHybrisaGlassAirlockOpen | Open | CMAirlockGlassHybrisa | open |
| CMU3DMaintenanceAirlockOpen | Open | CMAirlockMaint | open |
| CMU3DSecurityAirlockOpen | Open | CMAirlockSecurity | open |
| CMU3DMedicalDoubleGlassDoorOpen | Open | CMDoubleDoorMedicalGlass | door_open |
| CMU3DHybrisaHeavyShutter | Closed | RMCShutterHybrisa | closed |
| CMU3DHybrisaHeavyShutterOpen | Open | RMCShutterHybrisa | open |
| CMU3DHybrisaWindowShutter | Closed | RMCShutterHybrisaWindow | closed |
| CMU3DHybrisaWindowShutterOpen | Open | RMCShutterHybrisaWindow | open |
| CMU3DSinglePersonalDoor | Closed | CMAirlock | closed |
| CMU3DSingleCommandGovforDoorGlass | Closed | CMAirlockCommandGovforGlassLocked | closed |
| CMU3DSingleCommandGovforDoor | Closed | CMAirlockCommandGovforLocked | closed |
| CMU3DSingleComDoor | Closed | CMAirlockCommandLockedColony | closed |
| CMU3DSingleEngineerGovforDoorGlass | Closed | CMAirlockEngineerGovforGlassLocked | closed |
| CMU3DSingleEngineerGovforDoor | Closed | CMAirlockEngineerGovforLocked | closed |
| CMU3DSingleEngiDoor | Closed | CMAirlockEngineerLockedColony | closed |
| CMU3DSingleEngiDoorGlass | Closed | CMAirlockGlassEngineer | closed |
| CMU3DSingleMediDoorGlass | Closed | CMAirlockGlassMedicalLockedColony | closed |
| CMU3DSingleSecDoorGlass | Closed | CMAirlockGlassSecurityLockedColony | closed |
| CMU3DSingleGovforDoorGlass | Closed | CMAirlockGovforGlassLocked | closed |
| CMU3DSingleGovforDoor | Closed | CMAirlockGovforLocked | closed |
| CMU3DSingleMediDoor | Closed | CMAirlockMedical | closed |
| CMU3DSingleMedicalGovforDoorGlass | Closed | CMAirlockMedicalGovforGlassLocked | closed |
| CMU3DSingleMedicalGovforDoor | Closed | CMAirlockMedicalGovforLocked | closed |
| CMU3DSingleSecurityGovforDoorGlass | Closed | CMAirlockSecurityGovforGlassLocked | closed |
| CMU3DSingleSecurityGovforDoor | Closed | CMAirlockSecurityGovforLocked | closed |
| CMU3DPairedAlmayerGlass | Closed | CMDoubleDoorAlmayerGlass | door_closed |
| CMU3DPairedCommandGlass | Closed | CMDoubleDoorColonyCommandGlassLocked | door_closed |
| CMU3DPairedEngineerGlass | Closed | CMDoubleDoorColonyEngineerGlassLocked | door_closed |
| CMU3DPairedEngineerSolid | Closed | CMDoubleDoorColonyEngineerSolidLocked | door_closed |
| CMU3DPairedSecurityGlass | Closed | CMDoubleDoorColonySecurityGlassLocked | door_closed |
| CMU3DPairedSecuritySolid | Closed | CMDoubleDoorColonySecuritySolidLocked | door_closed |
| CMU3DPairedCommandGovforGlass | Closed | CMDoubleDoorCommandGovforGlassLocked | door_closed |
| CMU3DPairedGovforGlass | Closed | CMDoubleDoorGovforGlassLocked | door_closed |
| CMU3DPairedGovforSolid | Closed | CMDoubleDoorGovforLocked | door_closed |
| CMU3DPairedPersonalSolid | Closed | CMDoubleDoorPersonalSolid | door_closed |
| CMU3DPairedHybrisaMedicalGlass | Closed | RMCDoubleDoorHybrisaGlassMecical | door_closed |
| CMU3DHybrisaAirlock | Closed | RMCAirlockHybrisa | closed |
| CMU3DHybrisaPersonalAirlock | Closed | RMCAirlockHybrisaPersonal | closed |
| CMU3DHybrisaDoubleGlassDoor | Closed | RMCDoubleDoorGlassHybrisa | door_closed |
| CMU3DHybrisaGlassAirlock | Closed | CMAirlockGlassHybrisa | closed |
| CMU3DMaintenanceAirlock | Closed | CMAirlockMaint | closed |
| CMU3DSecurityAirlock | Closed | CMAirlockSecurity | closed |
| CMU3DMedicalDoubleGlassDoor | Closed | CMDoubleDoorMedicalGlass | door_closed |

## Source attribution

### Content.CMU/Resources/Textures/CMU14/Structures/Doors/Airlocks/Double/command_govfor_glass.rsi

- Metadata: `Content.CMU/Resources/Textures/CMU14/Structures/Doors/Airlocks/Double/command_govfor_glass.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/78b741a92f09a787c9daf94bb750f98ffaa421cd/icons/obj/structures/doors/2x1personaldoor_glass.dmi

### Content.CMU/Resources/Textures/CMU14/Structures/Doors/Airlocks/Double/govfor_glass.rsi

- Metadata: `Content.CMU/Resources/Textures/CMU14/Structures/Doors/Airlocks/Double/govfor_glass.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/78b741a92f09a787c9daf94bb750f98ffaa421cd/icons/obj/structures/doors/2x1personaldoor_glass.dmi

### Content.CMU/Resources/Textures/CMU14/Structures/Doors/Airlocks/Double/govfor_solid.rsi

- Metadata: `Content.CMU/Resources/Textures/CMU14/Structures/Doors/Airlocks/Double/govfor_solid.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/78b741a92f09a787c9daf94bb750f98ffaa421cd/icons/obj/structures/doors/2x1personaldoor.dmi

### Content.CMU/Resources/Textures/CMU14/Structures/Doors/Airlocks/command_govfor_door.rsi

- Metadata: `Content.CMU/Resources/Textures/CMU14/Structures/Doors/Airlocks/command_govfor_door.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/f7b9367cb5c083a8c052a40dece9417031e28ffe/icons/obj/structures/doors/personaldoor.dmi

### Content.CMU/Resources/Textures/CMU14/Structures/Doors/Airlocks/command_govfor_door_glass.rsi

- Metadata: `Content.CMU/Resources/Textures/CMU14/Structures/Doors/Airlocks/command_govfor_door_glass.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/f7b9367cb5c083a8c052a40dece9417031e28ffe/icons/obj/structures/doors/personaldoor.dmi

### Content.CMU/Resources/Textures/CMU14/Structures/Doors/Airlocks/engineer_govfor_door.rsi

- Metadata: `Content.CMU/Resources/Textures/CMU14/Structures/Doors/Airlocks/engineer_govfor_door.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/f7b9367cb5c083a8c052a40dece9417031e28ffe/icons/obj/structures/doors/personaldoor.dmi

### Content.CMU/Resources/Textures/CMU14/Structures/Doors/Airlocks/engineer_govfor_door_glass.rsi

- Metadata: `Content.CMU/Resources/Textures/CMU14/Structures/Doors/Airlocks/engineer_govfor_door_glass.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/f7b9367cb5c083a8c052a40dece9417031e28ffe/icons/obj/structures/doors/personaldoor.dmi

### Content.CMU/Resources/Textures/CMU14/Structures/Doors/Airlocks/govfor_door.rsi

- Metadata: `Content.CMU/Resources/Textures/CMU14/Structures/Doors/Airlocks/govfor_door.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/f7b9367cb5c083a8c052a40dece9417031e28ffe/icons/obj/structures/doors/personaldoor.dmi

### Content.CMU/Resources/Textures/CMU14/Structures/Doors/Airlocks/govfor_door_glass.rsi

- Metadata: `Content.CMU/Resources/Textures/CMU14/Structures/Doors/Airlocks/govfor_door_glass.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/78b741a92f09a787c9daf94bb750f98ffaa421cd/icons/obj/structures/doors/personalglass.dmi

### Content.CMU/Resources/Textures/CMU14/Structures/Doors/Airlocks/medical_govfor_door.rsi

- Metadata: `Content.CMU/Resources/Textures/CMU14/Structures/Doors/Airlocks/medical_govfor_door.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/f7b9367cb5c083a8c052a40dece9417031e28ffe/icons/obj/structures/doors/personaldoor.dmi

### Content.CMU/Resources/Textures/CMU14/Structures/Doors/Airlocks/medical_govfor_door_glass.rsi

- Metadata: `Content.CMU/Resources/Textures/CMU14/Structures/Doors/Airlocks/medical_govfor_door_glass.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/f7b9367cb5c083a8c052a40dece9417031e28ffe/icons/obj/structures/doors/personaldoor.dmi

### Content.CMU/Resources/Textures/CMU14/Structures/Doors/Airlocks/security_govfor_door.rsi

- Metadata: `Content.CMU/Resources/Textures/CMU14/Structures/Doors/Airlocks/security_govfor_door.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/f7b9367cb5c083a8c052a40dece9417031e28ffe/icons/obj/structures/doors/personaldoor.dmi

### Content.CMU/Resources/Textures/CMU14/Structures/Doors/Airlocks/security_govfor_door_glass.rsi

- Metadata: `Content.CMU/Resources/Textures/CMU14/Structures/Doors/Airlocks/security_govfor_door_glass.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/f7b9367cb5c083a8c052a40dece9417031e28ffe/icons/obj/structures/doors/personaldoor.dmi

### Resources/Textures/_RMC14/Structures/Doors/Airlocks/Double/almayer_glass.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Airlocks/Double/almayer_glass.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/78b741a92f09a787c9daf94bb750f98ffaa421cd/icons/obj/structures/doors/2x1almayerdoor_glass.dmi

### Resources/Textures/_RMC14/Structures/Doors/Airlocks/Double/command_glass.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Airlocks/Double/command_glass.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/f7b9367cb5c083a8c052a40dece9417031e28ffe/icons/obj/structures/doors/2x1comdoor.dmi

### Resources/Textures/_RMC14/Structures/Doors/Airlocks/Double/engineer_glass.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Airlocks/Double/engineer_glass.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/78b741a92f09a787c9daf94bb750f98ffaa421cd/icons/obj/structures/doors/2x1engidoor_glass.dmi

### Resources/Textures/_RMC14/Structures/Doors/Airlocks/Double/engineer_solid.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Airlocks/Double/engineer_solid.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/78b741a92f09a787c9daf94bb750f98ffaa421cd/icons/obj/structures/doors/2x1engidoor.dmi

### Resources/Textures/_RMC14/Structures/Doors/Airlocks/Double/hybrisa_glass.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Airlocks/Double/hybrisa_glass.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/doors/hybrisa/hybrisa_2x1personaldoor_glass.dmi

### Resources/Textures/_RMC14/Structures/Doors/Airlocks/Double/hybrisa_medical_glass.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Airlocks/Double/hybrisa_medical_glass.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/doors/hybrisa/hybrisa_2x1personaldoor_glass.dmi

### Resources/Textures/_RMC14/Structures/Doors/Airlocks/Double/medical_glass.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Airlocks/Double/medical_glass.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/f7b9367cb5c083a8c052a40dece9417031e28ffe/icons/obj/structures/doors/2x1medidoor.dmi

### Resources/Textures/_RMC14/Structures/Doors/Airlocks/Double/personal_solid.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Airlocks/Double/personal_solid.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/78b741a92f09a787c9daf94bb750f98ffaa421cd/icons/obj/structures/doors/2x1personaldoor.dmi

### Resources/Textures/_RMC14/Structures/Doors/Airlocks/Double/security_glass.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Airlocks/Double/security_glass.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/78b741a92f09a787c9daf94bb750f98ffaa421cd/icons/obj/structures/doors/2x1secdoor_glass.dmi

### Resources/Textures/_RMC14/Structures/Doors/Airlocks/Double/security_solid.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Airlocks/Double/security_solid.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/78b741a92f09a787c9daf94bb750f98ffaa421cd/icons/obj/structures/doors/2x1secdoor.dmi

### Resources/Textures/_RMC14/Structures/Doors/Airlocks/DoubleDoor/almayer_solid.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Airlocks/DoubleDoor/almayer_solid.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/78b741a92f09a787c9daf94bb750f98ffaa421cd/icons/obj/structures/doors/2x1almayerdoor.dmi

### Resources/Textures/_RMC14/Structures/Doors/Airlocks/DoubleDoor/engineer_glass.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Airlocks/DoubleDoor/engineer_glass.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/78b741a92f09a787c9daf94bb750f98ffaa421cd/icons/obj/structures/doors/2x1engidoor_glass.dmi

### Resources/Textures/_RMC14/Structures/Doors/Airlocks/com_door.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Airlocks/com_door.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/f7b9367cb5c083a8c052a40dece9417031e28ffe/icons/obj/structures/doors/comdoor.dmi

### Resources/Textures/_RMC14/Structures/Doors/Airlocks/engi_door.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Airlocks/engi_door.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/78b741a92f09a787c9daf94bb750f98ffaa421cd/icons/obj/structures/doors/engidoor.dmi

### Resources/Textures/_RMC14/Structures/Doors/Airlocks/engi_door_glass.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Airlocks/engi_door_glass.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/78b741a92f09a787c9daf94bb750f98ffaa421cd/icons/obj/structures/doors/engiglass.dmi

### Resources/Textures/_RMC14/Structures/Doors/Airlocks/hybrisa_door.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Airlocks/hybrisa_door.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/doors/personaldoor.dmi

### Resources/Textures/_RMC14/Structures/Doors/Airlocks/hybrisa_door_glass.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Airlocks/hybrisa_door_glass.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/doors/hybrisa/hybrisa_generic.dmi

### Resources/Textures/_RMC14/Structures/Doors/Airlocks/hybrisa_personal_door.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Airlocks/hybrisa_personal_door.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/doors/hybrisa_personaldoor.dmi

### Resources/Textures/_RMC14/Structures/Doors/Airlocks/maint_door.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Airlocks/maint_door.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/f7b9367cb5c083a8c052a40dece9417031e28ffe/icons/obj/structures/doors/maintdoor.dmi

### Resources/Textures/_RMC14/Structures/Doors/Airlocks/medi_door.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Airlocks/medi_door.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/78b741a92f09a787c9daf94bb750f98ffaa421cd/icons/obj/structures/doors/medidoor.dmi

### Resources/Textures/_RMC14/Structures/Doors/Airlocks/medi_door_glass.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Airlocks/medi_door_glass.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/f7b9367cb5c083a8c052a40dece9417031e28ffe/icons/obj/structures/doors/mediglass.dmi

### Resources/Textures/_RMC14/Structures/Doors/Airlocks/personal_door.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Airlocks/personal_door.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/f7b9367cb5c083a8c052a40dece9417031e28ffe/icons/obj/structures/doors/personaldoor.dmi

### Resources/Textures/_RMC14/Structures/Doors/Airlocks/sec_door.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Airlocks/sec_door.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/78b741a92f09a787c9daf94bb750f98ffaa421cd/icons/obj/structures/doors/secdoor.dmi

### Resources/Textures/_RMC14/Structures/Doors/Airlocks/sec_door_glass.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Airlocks/sec_door_glass.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/78b741a92f09a787c9daf94bb750f98ffaa421cd/icons/obj/structures/doors/secglass.dmi

### Resources/Textures/_RMC14/Structures/Doors/Shutters/Almayer/almayer_poddoor.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Shutters/Almayer/almayer_poddoor.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/6d78d51911aed7ab1afa4cee84d5207dee05dfc3/icons/obj/structures/doors/blastdoors_shutters.dmi

### Resources/Textures/_RMC14/Structures/Doors/Shutters/Almayer/poddoor.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Shutters/Almayer/poddoor.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/6d78d51911aed7ab1afa4cee84d5207dee05dfc3/icons/obj/structures/doors/blastdoors_shutters.dmi

### Resources/Textures/_RMC14/Structures/Doors/Shutters/Almayer/shutter.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Shutters/Almayer/shutter.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/6d78d51911aed7ab1afa4cee84d5207dee05dfc3/icons/obj/structures/doors/blastdoors_shutters.dmi

### Resources/Textures/_RMC14/Structures/Doors/Shutters/Hybrisa/hybrisa_shutter.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Shutters/Hybrisa/hybrisa_shutter.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/doors/hybrisashutters.dmi

### Resources/Textures/_RMC14/Structures/Doors/Shutters/Hybrisa/redpoddoor.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Shutters/Hybrisa/redpoddoor.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/doors/hybrisashutters.dmi

### Resources/Textures/_RMC14/Structures/Doors/Shutters/Hybrisa/ultra_reinforced.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Shutters/Hybrisa/ultra_reinforced.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/doors/hybrisashutters.dmi

### Resources/Textures/_RMC14/Structures/Doors/Shutters/Hybrisa/window_shutter.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Shutters/Hybrisa/window_shutter.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/doors/hybrisashutters.dmi
