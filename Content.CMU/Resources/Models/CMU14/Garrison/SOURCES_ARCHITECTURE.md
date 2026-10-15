# Garrison architectural source references

74 new manually authored draft assemblies and revisions to the security airlock and medical paired door. Named solid parts were constructed after inspection of source frames and resolved collider/component data. No source pixels are embedded in the meshes.

The new geometric assembly is contributed under CC0-1.0 to the extent separately licensable. This does not alter source visual-design or derivative rights. Retain all source licenses and attribution below.

## Placement and limitations

Railings use south-edge collider footprints. Platform corners use the actual east/south or northwest collider bounds. Decorative stair borders remain border strips. Overhead lattice geometry spans above the tile; projected sprite offsets are not blindly copied to the ground. Paired CMDoubleDoor modules occupy one tile each and follow their partner direction with yawOffset 90. Mirrors and controls use south-wall face mounting. Strata windows use anchored source smoothing connections.

Height, hidden surfaces, backs and depths remain inferred. Glass and mirrors are color proxies in the native reviewer. Door movement, power/lock overlays, rail retraction and floor elevation are not implemented by these static assets. Closed-door references exclude explicit open prototypes. No model has final art approval.

## Models

| Model | Source prototypes |
| --- | --- |
| CMU3DStandardHandrail | RMCBarricadeHandrail |
| CMU3DMedicalHandrail | RMCBarricadeHandrailMed |
| CMU3DStrataHandrail | RMCBarricadeHandrailStrata |
| CMU3DTurnstileHandrail | CMBarricadeTurnstile |
| CMU3DBlackRoadBarrier | RMCBarricadeHybrisaPlasticRoadBarrierBlack |
| CMU3DRetractableRailing | CMRailing |
| CMU3DFlightStairs | AU14FlightStairs |
| CMU3DAlmayerStairsTop | RMCAlmayerStairsTop |
| CMU3DHybrisaPerspectiveStairs | RMCHybrisaStairsPerspective |
| CMU3DSoroStairs | RMCSoroStairs |
| CMU3DGrayStairs | RMCStairs |
| CMU3DGrayCornerStairs | RMCStairsCorner |
| CMU3DPlatformCorner | CMPlatformCorner |
| CMU3DPlatformCornerSmall | RMCPlatformCornerSmall |
| CMU3DPlatformHybrisaEdge | RMCPlatformHybrisaEdge |
| CMU3DPlatformHybrisaEdgeCornerSmall | RMCPlatformHybrisaEdgeCornerSmall |
| CMU3DPlatformHybrisaRock | RMCPlatformHybrisaRock |
| CMU3DPlatformHybrisaThreeStair | RMCPlatformHybrisaThreeStair |
| CMU3DPlatformHybrisaThreeStairAlt | RMCPlatformHybrisaThreeStairAlt |
| CMU3DPlatformHybrisaTwo | RMCPlatformHybrisaTwo |
| CMU3DPlatformHybrisaTwoCornerSmall | RMCPlatformHybrisaTwoCornerSmall |
| CMU3DPlatformKutjevoRock | RMCPlatformKutjevoRock |
| CMU3DPlatformKutjevoRockCornerSmall | RMCPlatformKutjevoRockCornerSmall |
| CMU3DPlatformKutjevoSM | RMCPlatformKutjevoSM |
| CMU3DPlatformKutjevoSMCorner | RMCPlatformKutjevoSMCorner |
| CMU3DPlatformSandstone | RMCPlatformSandstone |
| CMU3DPlatformStairLeft | RMCPlatformStairLeft |
| CMU3DPlatformStairRight | RMCPlatformStairRight |
| CMU3DPlatformStrata | RMCPlatformStrata |
| CMU3DPlatformStrataRock | RMCPlatformStrataRock |
| CMU3DPlatformStrataRockCornerSmall | RMCPlatformStrataRockCornerSmall |
| CMU3DPlatformStrataStair | RMCPlatformStrataStair |
| CMU3DPlatformStrataThree | RMCPlatformStrataThree |
| CMU3DPlatformStrataTwoCornerSmall | RMCPlatformStrataTwoCornerSmall |
| CMU3DOverheadLatticeHorizontal4A | RMCOverheadLatticeHorizontal4A |
| CMU3DOverheadLatticeHorizontal5A | RMCOverheadLatticeHorizontal5A |
| CMU3DOverheadLatticeHorizontal6A | RMCOverheadLatticeHorizontal6A |
| CMU3DOverheadLatticeHorizontal6C | RMCOverheadLatticeHorizontal6C |
| CMU3DOverheadLatticeVertical1A | RMCOverheadLatticeVertical1A |
| CMU3DOverheadLatticeVertical2A | RMCOverheadLatticeVertical2A |
| CMU3DOverheadLatticeVertical3A | RMCOverheadLatticeVertical3A |
| CMU3DOvalWallMirror | Mirror |
| CMU3DRectangularWallMirror | RMCMirror |
| CMU3DOrangeDoorButton | RMCPodDoorButton, RMCPodDoorButtonCommand, RMCPodDoorButtonShip |
| CMU3DRedDoorButton | RMCPodDoorButtonBigRed, RMCPodDoorButtonBigRedShip |
| CMU3DStrataWindow | RMCWindowStrata, RMCWindowStrataReinforced |
| CMU3DSinglePersonalDoor | CMAirlock |
| CMU3DSecurityAirlock | CMAirlockSecurity, CMAirlockSecurityLockedColony, CMAirlockArmoryLocked, CMAirlockBrigLocked, RMCAirlockSecuritySPPLocked |
| CMU3DSingleCommandGovforDoorGlass | CMAirlockCommandGovforGlassLocked |
| CMU3DSingleCommandGovforDoor | CMAirlockCommandGovforLocked |
| CMU3DSingleComDoor | CMAirlockCommandLockedColony |
| CMU3DSingleEngineerGovforDoorGlass | CMAirlockEngineerGovforGlassLocked |
| CMU3DSingleEngineerGovforDoor | CMAirlockEngineerGovforLocked |
| CMU3DSingleEngiDoor | CMAirlockEngineerLockedColony |
| CMU3DSingleEngiDoorGlass | CMAirlockGlassEngineer, CMAirlockGlassEngineerLockedColony |
| CMU3DSingleMediDoorGlass | CMAirlockGlassMedicalLockedColony |
| CMU3DSingleSecDoorGlass | CMAirlockGlassSecurityLockedColony |
| CMU3DSingleGovforDoorGlass | CMAirlockGovforGlassLocked |
| CMU3DSingleGovforDoor | CMAirlockGovforLocked |
| CMU3DSingleMediDoor | CMAirlockMedical, CMAirlockMedicalLockedColony |
| CMU3DSingleMedicalGovforDoorGlass | CMAirlockMedicalGovforGlassLocked |
| CMU3DSingleMedicalGovforDoor | CMAirlockMedicalGovforLocked |
| CMU3DSingleSecurityGovforDoorGlass | CMAirlockSecurityGovforGlassLocked |
| CMU3DSingleSecurityGovforDoor | CMAirlockSecurityGovforLocked |
| CMU3DPairedAlmayerGlass | CMDoubleDoorAlmayerGlass |
| CMU3DPairedCommandGlass | CMDoubleDoorColonyCommandGlassLocked |
| CMU3DPairedEngineerGlass | CMDoubleDoorColonyEngineerGlassLocked |
| CMU3DPairedEngineerSolid | CMDoubleDoorColonyEngineerSolidLocked |
| CMU3DPairedSecurityGlass | CMDoubleDoorColonySecurityGlassLocked, CMDoubleDoorSecurityGlassLocked |
| CMU3DPairedSecuritySolid | CMDoubleDoorColonySecuritySolidLocked |
| CMU3DPairedCommandGovforGlass | CMDoubleDoorCommandGovforGlassLocked |
| CMU3DPairedGovforGlass | CMDoubleDoorGovforGlassLocked |
| CMU3DPairedGovforSolid | CMDoubleDoorGovforLocked |
| CMU3DPairedPersonalSolid | CMDoubleDoorPersonalSolid |
| CMU3DPairedHybrisaMedicalGlass | RMCDoubleDoorHybrisaGlassMecical |
| CMU3DMedicalDoubleGlassDoor | CMDoubleDoorMedicalGlass |

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

### Resources/Textures/Structures/Wallmounts/mirror.rsi

- Metadata: `Resources/Textures/Structures/Wallmounts/mirror.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from /tg/station 13 at https://github.com/tgstation/tgstation/commit/a2c5a0f15bad5c46a828771bbba6ea5752a9d191. mirror and mirror-broke repositioned by K-Dynamic (github), modern-mirror and modern-mirror-broke by K-Dynamic.

### Resources/Textures/_RMC14/Objects/door_button.rsi

- Metadata: `Resources/Textures/_RMC14/Objects/door_button.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/c7b4d6bd868de669ad96f1d3e4dc3702a3404355/icons/obj/structures/props/stationobjs.dmi

### Resources/Textures/_RMC14/Objects/door_button_br.rsi

- Metadata: `Resources/Textures/_RMC14/Objects/door_button_br.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/c7b4d6bd868de669ad96f1d3e4dc3702a3404355/icons/obj/structures/props/stationobjs.dmi

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

### Resources/Textures/_RMC14/Structures/Wallmounts/mirror.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Wallmounts/mirror.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/structures/props/watercloset.dmi

### Resources/Textures/_RMC14/Structures/Walls/Barricades/barricade.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Walls/Barricades/barricade.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/5cf465e72efb6beccd2b78bf263072816a2a60ad/icons/obj/structures/barricades.dmi

### Resources/Textures/_RMC14/Structures/Windows/strata_window.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Windows/strata_window.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/6c8f3153bb8846baa74e413b3ed8d3355aebcea3/icons/turf/walls/strata_windows.dmi

### Resources/Textures/_RMC14/Structures/overhead_lattice_hybrisa.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/overhead_lattice_hybrisa.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/structures/props/industrial/hybrisa_lattice.dmi

### Resources/Textures/_RMC14/Structures/platforms.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/platforms.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/platforms.dmi, https://github.com/cmss13-devs/cmss13/blob/48e570bd697f2476e28d89cd255d0539a5228228/icons/obj/structures/props/platforms.dmi

### Resources/Textures/_RMC14/Structures/railing.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/railing.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/f7b9367cb5c083a8c052a40dece9417031e28ffe/icons/obj/structures/doors/railing.dmi

### Resources/Textures/_RMC14/Structures/stairs.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/stairs.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/8e8d26bbb4f1617ea1b1ffc17ffffca552ce8c11/icons/obj/structures/stairs, rampbottom taken from https://github.com/cmss13-devs/cmss13/blob/8e8d26bbb4f1617ea1b1ffc17ffffca552ce8c11/icons/obj/structures/structures.dmi

The previous Hybrisa large and small platform corners were corrected to the inherited CMPlatformCorner south/east bounds and RMCPlatformCornerSmall northwest bounds. Overhead lattice bases were raised above the 2.8-high supporting walls after map inspection. Paired door fronts use the East/West single-leaf source frames; the first South frame alone does not describe the whole assembled doorway.
