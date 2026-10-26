# Garrison structural models

20 new draft assemblies from inspected source states, colours, directions and saved context. None is approved as finished art.

New geometric work is contributed under CC0-1.0 to the extent separately licensable. Source visual-design and derivative rights retain their existing licenses and attribution.

Cell windows are full-depth observation-slit wall modules; broad department glazing remains a thinner framed panel. Directional blue panes retain their boundary fixtures and bake the actual #969696 tint exactly once. Connected window axes follow source neighbour keys. Wall crest quarter-tile details remain unfinished.

Elevator panels retain the fixed perimeter handedness and full-tile blocking footprint. Hidden faces and vertical relief are inferred from plan-view artwork. HESCO and reinforced barricades follow their offset fixtures. The reinforced barricade uses the complete intact metal DamageOverlay_0 plus AdditionalDamageOverlay_0; the general reference shows reinforcement only. Overhead pipes share a constant inferred 2.53-tile elevation and the elbow follows the source south/east legs; true cylindrical sections and supports remain unfinished.

The inspected beer billboard is deliberately not mapped by this batch: its exact graphic needs a textured face instead of an invented geometric logo. Animated water, terrain elevation, live damage and power state still need further work.

## Context review and refinements

- The 20 models explicitly cover 371 classic saved instances. Fourteen types (86 instances) were unmapped; six types (285 instances) previously used inherited candidates. All original positions and yaw remain unchanged. Detailed bounds and placement checks are in `Tools/three_d/generated/structures-placement-audit.json`.
- The overhead elbow has an explicit `sourceCardinalFacings: [0, 1, 3, 2]` permutation, indexed South/East/North/West. This differs from RSI sheet storage order. Its saved West frame (#12183) joins the North and West runs at (105.5, -45.0) and (105.0, -45.5). Both paths preserve residual entity rotation. Duplicate saved straight pipes #2319/#2320 remain represented without moving their pivots.
- Clear and tinted blue glazing share the source RGB palette but retain different alpha: the main clear pixels have 166/255 opacity and the tinted pixels are opaque. Geometric diagonal highlights approximate the art. Browser review of #48948/#2151 caught the pane piercing a hydroponics tray on the same tile. Glass now occupies local Y -.486..-.474 and the bottom rail remains outside -.467; the tray reaches -.46. All pairwise part bounds have zero intersections. Thick side jambs keep the source edge fixture. No entity displacement was added.
- Cell windows #48803 and #48805 now have full-depth slit surrounds, including physical backing for the prison monitor row. The surrounding platform elevation remains unresolved. Engineering/medical windows #48419/#48456/#48459 were reviewed with their walls and actual overlaid blinds.
- The elevator perimeter (#15527 and neighbouring panels) retains handed corner crowns and inward-facing panel details. HESCO #383 and reinforced barricade #1191 were checked at their offset tile edges. The intact barricade source composite is `generated/review/structures-brute-composite.png`.
- Bone-resin wall #15479 uses offset overlapping ribs and an olive bone ring surrounding a recessed crown. Browser review replaced the first crown's five disconnected lumps. Connected quarter-tile crests and detailed organic surface sculpting still need work; this assembly is not approved.
- Source/model comparison sheets cover all 20 models. Browser context captures use `Tools/three_d/generated/review/structures-*.png`. Canonical geometry is `garrison_structures.yml`; scratch authoring scripts are not the delivered asset source.

## Models

| Model | Explicit prototype | Reference state |
| --- | --- | --- |
| CMU3DHESCOBasketBarrier | AU14HESCOBarrier | sand_wall_tall_complete |
| CMU3DBruteReinforcedBarricade | CMBarricadeBrute | brute_upgrade |
| CMU3DOverheadScrubberPipe | CMOverheadPipe | intact-scrubbers |
| CMU3DOverheadScrubberElbow | RMCOverheadPipeCorner | intact-scrubbers_corners |
| CMU3DElevatorGearWall | RMCWallElevatorGear | wall_gear |
| CMU3DElevatorFixedPanel1 | RMCWallElevatorNoConnect1 | elevator_1 |
| CMU3DElevatorFixedPanel3 | RMCWallElevatorNoConnect3 | elevator_3 |
| CMU3DElevatorFixedPanel5 | RMCWallElevatorNoConnect5 | elevator_5 |
| CMU3DElevatorFixedPanel6 | RMCWallElevatorNoConnect6 | elevator_6 |
| CMU3DElevatorFixedPanel7 | RMCWallElevatorNoConnect7 | elevator_7 |
| CMU3DElevatorFixedPanel8 | RMCWallElevatorNoConnect8 | elevator_8 |
| CMU3DHybrisaEngineeringWall | RMCWallHybrisaEngi | strata_bare_outpost_ |
| CMU3DHybrisaMedicalWall | RMCWallHybrisaMedical | strata_bare_outpost_ |
| CMU3DHybrisaRibbedWall | RMCWallHybrisaReinforced | strata_ribbed_outpost_ |
| CMU3DHybrisaEngineeringWindow | RMCWindowHybrisaEngiReinforced | strata_window0 |
| CMU3DHybrisaMedicalWindow | RMCWindowHybrisaMedicalReinforced | strata_window0 |
| CMU3DPrisonCellObservationWindow | RMCWindowPrisonCell | prison_cellwindow0 |
| CMU3DBlueDirectionalWindow | RMCWindowDirectionalBlue | window |
| CMU3DBlueTintedDirectionalWindow | RMCWindowTintedDirectionalBlue | twindow |
| CMU3DBoneResinWall | RMCWallBoneResin | bone_resin |

## Source attribution

### Content.CMU/Resources/Textures/CMU14/Structures/hesco.rsi

- Metadata: `Content.CMU/Resources/Textures/CMU14/Structures/hesco.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/Steelpoint/cmss13/blob/1e1023cec5aa04ddfd142f3d7e32de2327bca245/icons/obj/structures/barricades.dmi

### Resources/Textures/_RMC14/Structures/Walls/Barricades/barricade.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Walls/Barricades/barricade.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/5cf465e72efb6beccd2b78bf263072816a2a60ad/icons/obj/structures/barricades.dmi

### Resources/Textures/_RMC14/Structures/Walls/bone_resin.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Walls/bone_resin.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/09a5191fb11aab8ddffe3f9be94292b53e4d96f6/icons/turf/walls/prison/bone_resin.dmi

### Resources/Textures/_RMC14/Structures/Walls/elevator.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Walls/elevator.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/09a5191fb11aab8ddffe3f9be94292b53e4d96f6/icons/turf/elevator.dmi

### Resources/Textures/_RMC14/Structures/Walls/elevator_single.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Walls/elevator_single.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/09a5191fb11aab8ddffe3f9be94292b53e4d96f6/icons/turf/elevator.dmi

### Resources/Textures/_RMC14/Structures/Walls/hybrisa_rwall.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Walls/hybrisa_rwall.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/turf/walls/hybrisa_colonywall.dmi

### Resources/Textures/_RMC14/Structures/Walls/hybrisa_wall_engineering.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Walls/hybrisa_wall_engineering.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/turf/walls/hybrisa_engineering_wall.dmi

### Resources/Textures/_RMC14/Structures/Walls/hybrisa_wall_medical.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Walls/hybrisa_wall_medical.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/turf/walls/hybrisa_colonywall_hospital.dmi

### Resources/Textures/_RMC14/Structures/Windows/directional.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Windows/directional.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/6c8f3153bb8846baa74e413b3ed8d3355aebcea3/icons/turf/walls/windows.dmi

### Resources/Textures/_RMC14/Structures/Windows/hybrisa_window_engineering.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Windows/hybrisa_window_engineering.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/turf/walls/hybrisa_hospital_colonywindows.dmi

### Resources/Textures/_RMC14/Structures/Windows/hybrisa_window_medical.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Windows/hybrisa_window_medical.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/turf/walls/hybrisa_hospital_colonywindows.dmi

### Resources/Textures/_RMC14/Structures/Windows/prison_cellwindow.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Windows/prison_cellwindow.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/09a5191fb11aab8ddffe3f9be94292b53e4d96f6/icons/turf/walls/windows.dmi

### Resources/Textures/_RMC14/Structures/overhead_pipes.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/overhead_pipes.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/f7b9367cb5c083a8c052a40dece9417031e28ffe/icons/obj/pipes/pipes.dmi
