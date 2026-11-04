# Inherited structural source audit and draft corrections

Pinned source: [TheHellFireo/CMU-Garrison-3D, 6e37a4d](https://github.com/TheHellFireo/CMU-Garrison-3D/tree/6e37a4d0a7d9433838c393a82c02422cb704dd5a).

## Scope and counts

All 39 assigned inherited structural candidates are audited exactly once. They are a separate queue from the original 473 missing-type queue. No fidelity-approved or reviewed status is introduced.

- 35 source-mismatched inherited types receive 34 source-bound draft assemblies
- 3 integration-unresolved inherited types receive 2 explicitly unbound studies
- 1 access-only engineer-glass variant remains compatible with an existing draft, with no duplicated geometry or exact binding
- Total new editable assemblies: 36; one static pose each; zero animation clips
- Maximum assembly: 64 supported primitive parts, below the 128-part limit
- 21 new source-detail PNG surfaces, all within the exact inherited_structure reservation list; unused reservation gaps are untouched

Fuel states 12 and 13 have identical visible pixels and alpha masks. Only fully transparent RGB bytes differ. They share one new assembly rather than inflating the model count.

Instance totals come from the pinned inherited inventory and are descriptive saved-map counts, not native-placement or visibility verification:
{
  "newDraftTypes": {
    "classic": 49,
    "redux": 271
  },
  "unresolved": {
    "classic": 0,
    "redux": 8
  },
  "compatibleCandidate": {
    "classic": 0,
    "redux": 3
  }
}

## What changed

Fuel lines are individually shaped wide ducts: straight, left/right elbow, tee, terminal, label and valve arrangements. They are not narrow scrubber-pipe substitutes. Separate structural runs, seams, collars and controls remain editable. Height and hidden section are inferred.

Glazed Hybrisa and prison doors have their own port/jamb/lockwheel construction. Shiva and Hybrisa catwalks have actual square/diamond openings. Brick stacks use bonded masonry courses. The stacked washer has two drums and its source +0.09Y offset. White chair, green loader, beige table and Soro windoor use source-owned palettes/tints. Brown couch ends are handed, with the source rear metal sheet; bar booths have tufted red upholstery. Stairs, Kutjevo rails, psych bedding, Strata diagonal cap and elevator faces have source-specific structure/details.

## Compatible candidate, without duplication

RMCDoubleDoorEngineerGlassEngineering adds AccessReader and text fields to RMCDoubleDoorEngineerGlass. The resolved Sprite, four directions, RSI closed state, layers, offset, tint and fixture geometry match. CMU3DLegacyEngineerDoor remains a compatible inherited draft, not a newly exact/reviewed asset. Its opening/lock fidelity limitations remain.

## Exact art/integration boundary

- CMUZLevelLadderThroughDown2: the source is bare ladder00, while the listed candidate uses ladder11 hazard feet. The unbound ladder00 study has separate bowed rungs and open gaps. It does not replace CMU3DLadderThroughDown2 or its native compound contract
- CMUZLevelHatchThroughDown and RMCLadderHatch: both use maintenancehatch_alt with Sprite offset +0.2Y. One unbound grille/frame study represents that shared art. Neither is bound to a fake ladder or a fabricated floor aperture

Pinned CMU3DLadderCompound.cs requires owner prototype CMUZLevelLadderThroughDown2, owner model CMU3DLadderThroughDown2, one FloorOpening, no CeilingOpening and exactly two companions: DecorFloorPallet/CMU3DTimberPallet and DecorFloorCardboard/CMU3DDecorFloorCardboardEast. Selection verifies identity, same grid/pivot, yaw, source frame, transform, tint and source art. The whole encoded transaction must fit. This contract and existing source assets are untouched.

CMU3DSlabOpening allows bounded rectangular apertures within [-0.5, 0.5] on one unit tile. TrySlabPlan also requires an anchored unit-grid entity, zero GroundOffset, floor placement, one sprite state and one source direction, and cardinal relative yaw. A general Z curve, remote landing, arbitrary multi-tile cutout or native map context is not established by these art studies. No FloorOpening, CeilingOpening, floor companions or terrain-cutout metadata was added to this family.

## Source and review method

All 142 original canonical model/surface YAML files were read and their Git blob bytes rechecked. Original surface registry entries guided isolated parent rendering; original surface validation copies stay under reference/inherited-structure-parent-validation, outside canonical assets.

144 pinned source files were byte-verified: 27 prototype owner/ancestor YAMLs, 27 RSI metadata files, 81 state PNGs and 9 original model-detail PNGs. The 59 child/direct-parent reference inventories resolve from this self-contained prototype set; Sprite defaults agree with the checkpoint inventory. Audit rows retain the complete relevant parent chain, declared layers, tint, offset and fixture evidence.

All declared default Sprite layers were inspected. Door base closed artwork is the static modeling reference; runtime-managed bolt/emergency/maintenance/weld overlays are recorded but not claimed as a complete default live composition. Alternate source states and all direction sheets are retained for review, without claiming gameplay state support.

Original small PNG crops are preserved byte-for-pixel, with explicit Sprite tint multiplication once for tinted variants. BakedSpriteTint and ReferenceTint mark the two tinted drafts. Untextured colors are selected from actual default opaque source pixels after that tint. No invented wear textures are used.

Each new assembly has source/fixed-scale/two-orbit cards. Fixed views use 128 pixels per tile, while original source frames use nearest-neighbor 4x enlargement (32 source pixels per tile). Free-orbit thumbnails fit the view and do not establish scale. Each of the 39 IDs also has a source/original-candidate/new-result audit card. Inferred 3D elevations do not necessarily match the oblique 2D projected height; these are review comparisons, not silhouette/fidelity passes.

## Targeted source-QA corrections

Four source-facing corrections were applied after independent review. FuelLine10 and FuelLine11 now have exactly two red terminal bands in the source pixel rectangles, RGB #641515, rather than the generic three-gray-fin treatment. The closed Hybrisa leaves meet through an opaque center seam. Elevator fixed panel 2 now has one upper brace pair descending outward from an upper central joint, with straight lower framing and no invented lower diagonals.

The targeted QA report proves exactly four GLBs changed and the other 32 remained byte-identical. Overall part geometry bounds and the atlas YAML are unchanged. Nine additional exported-mesh probes verify the closed center seam. Source red-band pixels/counts/placement and exported elevator brace endpoints are checked explicitly. All general checks and independent Blender imports were rerun on final bytes. This is a correction pass, not a review/fidelity approval.

## Checks and limits

- 36 deterministic GLBs equal regeneration by the unchanged exporter implementation
- Exporter code is identical to the pinned source apart from the pre-existing terminal-newline difference; it was not edited
- GLB headers, buffers, finite transforms/accessors and accessor bounds checked
- Independent Blender 4.3.2 imports: all 36 files, all editable part objects, zero invalid-mesh repairs, zero actions
- All 36 exported local assemblies form one connected contact graph using convex primitive mesh intersections or tangency within 1e-7 tile
- 32 exact exported-mesh point probes confirm selected grate cells, rail/couch/desk spaces, ladder gaps, duct elbows/tees, short-cap extent, loader cage/claws and metal-free glazed ports
- Connectivity/probes concern local part geometry only. They do not prove manifold unions, all empty volume, complete material transparency, saved-map contacts, native admission, frame rate or gameplay

No game/server launch, runtime/engine edit, map transform change, push, publication, Khronos-validation claim, native/full-map review or fidelity approval is included. All static poses, hidden sides, exact depth, material response and integration remain drafts.

## Every assigned ID

| ID | Outcome | Result | Evidence |
|---|---|---|---|
| CMAirlockGlassHybrisaPersonal | source_mismatch | CMU3DInheritedStructureCMAirlockGlassHybrisaPersonalCloud | Different RSI, dark olive leaves and two elongated glazed ports; generic personal candidate is unglazed light gray |
| CMAirlockPrison | source_mismatch | CMU3DInheritedStructureCMAirlockPrisonCloud | Different RSI and closed silhouette: heavy lintel, circular locking wheel, red jamb bands and caution strip |
| CMCatwalkShiva | source_mismatch | CMU3DInheritedStructureCMCatwalkShivaCloud | Same RSI but shiva_catwalk has thick rim/coarse square grid rather than narrow rim/diamond mesh |
| CMChairOfficeWhite | source_mismatch | CMU3DInheritedStructureCMChairOfficeWhiteCloud | Officechair_white default differs from dark blue upholstery; four directional silhouettes share construction |
| CMTableWoodenGambling | source_mismatch | CMU3DInheritedStructureCMTableWoodenGamblingCloud | Gambling RSI has curved wood perimeter and green felt; inherited steel table has square metal construction |
| CMUZLevelHatchThroughDown | unresolved | CMU3DInheritedStructureCMUZLevelHatchThroughDownCloudStudy | maintenancehatch_alt uses offset +0.2Y grille, not ladder11; Z-level transit/floor-cutout source context is unverified |
| CMUZLevelLadderThroughDown2 | unresolved | CMU3DInheritedStructureCMUZLevelLadderThroughDown2CloudStudy | ladder00 is bare bowed-rung segment, not ladder11 with hazard feet. Native compound requires exact special owner model and two named same-pivot companions |
| CMWashingMachineDouble | source_mismatch | CMU3DInheritedStructureCMWashingMachineDoubleCloud | 48x48 source double_closed is stacked two-drum tower; candidate has one squat drum |
| DecorFloorBrickStack | source_mismatch | CMU3DInheritedStructureDecorFloorBrickStackCloud | brickpile contains bonded clay bricks and irregular upper course; candidate contains slats/fork cavities of four pallets |
| RMCAlmayerStairs | source_mismatch | CMU3DInheritedStructureRMCAlmayerStairsCloud | rampbottom has four wide treads and dark nosings unlike the narrow p_stair_full gray treads |
| RMCBarricadeHandrailKutjevo | source_mismatch | CMU3DInheritedStructureRMCBarricadeHandrailKutjevoCloud | hr_kutjevo has brown three-post/two-rail design unlike green-gray candidate framing |
| RMCBedPsych | source_mismatch | CMU3DInheritedStructureRMCBedPsychCloud | psychbed uses orange diamond-quilt upholstery rather than white-sheet bed; +X pillow arrangement retained |
| RMCCatwalkHybrisa | source_mismatch | CMU3DInheritedStructureRMCCatwalkHybrisaCloud | solidgrate1 is borderless fine diamond mesh; candidate has perimeter rails/coarser square bar structure |
| RMCCouchEndLowerBrown | source_mismatch | CMU3DInheritedStructureRMCCouchEndLowerBrownCloud | Brown lower end is right-armed in South frame with metal rear sheet; candidate is armless generic mid |
| RMCCouchEndUpperBrown | source_mismatch | CMU3DInheritedStructureRMCCouchEndUpperBrownCloud | Brown upper end is left-armed in South frame with metal rear sheet; candidate is armless generic mid |
| RMCCouchLeftBar | source_mismatch | CMU3DInheritedStructureRMCCouchLeftBarCloud | Red tufted booth upholstery and wooden left side shell unlike metal-framed wood bench |
| RMCCouchMidBrown | source_mismatch | CMU3DInheritedStructureRMCCouchMidBrownCloud | Different brown RSI; source rear is solid silver sheet and front panel/seat arrangement differs from four-slat generic mid |
| RMCCouchRightBar | source_mismatch | CMU3DInheritedStructureRMCCouchRightBarCloud | Red tufted booth upholstery and wooden right side shell unlike metal-framed wood bench |
| RMCDoubleDoorEngineerGlassEngineering | compatible_existing_candidate | CMU3DLegacyEngineerDoor | Child declares only AccessReader plus name/description. Resolved Sprite/Fixtures, RSI closed state, layers, offset 0.5/0.5, no tint, four directions and model two-cell pivot geometry agree with direct RMCDoubleDoorEngineerGlass parent. Locks/open transitions remain draft. No geometry duplicated or exact binding added. |
| RMCLadderHatch | unresolved | CMU3DInheritedStructureCMUZLevelHatchThroughDownCloudStudy | maintenancehatch_alt uses offset +0.2Y grille, not ladderdown; ladder transit/floor aperture source context is unverified |
| RMCMechPowerLoaderGreen | source_mismatch | CMU3DInheritedStructureRMCMechPowerLoaderGreenCloud | powerloader_open_jd has green construction, yellow safety bars and red beacon rather than orange-yellow open loader |
| RMCOverheadFuelLine1 | source_mismatch | CMU3DInheritedStructureRMCOverheadFuelLine1Cloud | Different overhead_fuel RSI and one-direction source flammable_pipe_1 has wide source-specific duct/branch/control geometry rather than narrow eight-direction overhead_pipes scrubber segment |
| RMCOverheadFuelLine10 | source_mismatch | CMU3DInheritedStructureRMCOverheadFuelLine10Cloud | Different overhead_fuel RSI and one-direction source flammable_pipe_10 has wide source-specific duct/branch/control geometry rather than narrow eight-direction overhead_pipes scrubber segment |
| RMCOverheadFuelLine11 | source_mismatch | CMU3DInheritedStructureRMCOverheadFuelLine11Cloud | Different overhead_fuel RSI and one-direction source flammable_pipe_11 has wide source-specific duct/branch/control geometry rather than narrow eight-direction overhead_pipes scrubber segment |
| RMCOverheadFuelLine12 | source_mismatch | CMU3DInheritedStructureRMCOverheadFuelLine12Cloud | Different overhead_fuel RSI and one-direction source flammable_pipe_12 has wide source-specific duct/branch/control geometry rather than narrow eight-direction overhead_pipes scrubber segment |
| RMCOverheadFuelLine13 | source_mismatch | CMU3DInheritedStructureRMCOverheadFuelLine12Cloud | Different overhead_fuel RSI and one-direction source flammable_pipe_13 has wide source-specific duct/branch/control geometry rather than narrow eight-direction overhead_pipes scrubber segment |
| RMCOverheadFuelLine14 | source_mismatch | CMU3DInheritedStructureRMCOverheadFuelLine14Cloud | Different overhead_fuel RSI and one-direction source flammable_pipe_14 has wide source-specific duct/branch/control geometry rather than narrow eight-direction overhead_pipes scrubber segment |
| RMCOverheadFuelLine15 | source_mismatch | CMU3DInheritedStructureRMCOverheadFuelLine15Cloud | Different overhead_fuel RSI and one-direction source flammable_pipe_15 has wide source-specific duct/branch/control geometry rather than narrow eight-direction overhead_pipes scrubber segment |
| RMCOverheadFuelLine16 | source_mismatch | CMU3DInheritedStructureRMCOverheadFuelLine16Cloud | Different overhead_fuel RSI and one-direction source flammable_pipe_16 has wide source-specific duct/branch/control geometry rather than narrow eight-direction overhead_pipes scrubber segment |
| RMCOverheadFuelLine17 | source_mismatch | CMU3DInheritedStructureRMCOverheadFuelLine17Cloud | Different overhead_fuel RSI and one-direction source flammable_pipe_17 has wide source-specific duct/branch/control geometry rather than narrow eight-direction overhead_pipes scrubber segment |
| RMCOverheadFuelLine3 | source_mismatch | CMU3DInheritedStructureRMCOverheadFuelLine3Cloud | Different overhead_fuel RSI and one-direction source flammable_pipe_3 has wide source-specific duct/branch/control geometry rather than narrow eight-direction overhead_pipes scrubber segment |
| RMCOverheadFuelLine8 | source_mismatch | CMU3DInheritedStructureRMCOverheadFuelLine8Cloud | Different overhead_fuel RSI and one-direction source flammable_pipe_8 has wide source-specific duct/branch/control geometry rather than narrow eight-direction overhead_pipes scrubber segment |
| RMCOverheadPipeCap | source_mismatch | CMU3DInheritedStructureRMCOverheadPipeCapCloud | cap South is short lower-third segment rather than a full-tile intact-scrubbers run; four rather than eight directions |
| RMCPlatformStrataThreeCornerSmall | source_mismatch | CMU3DInheritedStructureRMCPlatformStrataThreeCornerSmallCloud | strata_metalplatform_deco3 is a diagonal triangular cap with blue stripe rather than square brown-marked platform_deco block |
| RMCTablePrisonBeige | source_mismatch | CMU3DInheritedStructureRMCTablePrisonBeigeCloud | Sprite #ffe6e6 changes gray prison palette. Prison candidate construction is closest; reinforced/steel candidates differ in RSI and construction |
| RMCWallElevatorArrivals | source_mismatch | CMU3DInheritedStructureRMCWallElevatorArrivalsCloud | wall_arrivals is legend-bearing front fascia rather than fixed panel1 side columns |
| RMCWallElevatorButtonDorm | source_mismatch | CMU3DInheritedStructureRMCWallElevatorButtonDormCloud | wall_button_dorm contains controller/call-button fascia rather than fixed panel1 side columns |
| RMCWallElevatorNoConnect2 | source_mismatch | CMU3DInheritedStructureRMCWallElevatorNoConnect2Cloud | elevator_2 center spine/outward braces differ from elevator_1 side-column arrangement |
| RMCWindoorSoro | source_mismatch | CMU3DInheritedStructureRMCWindoorSoroCloud | Exact windoor RSI/state/layers but source Sprite #98A3AB tint is not baked into inherited untinted model |

## Attribution

Source metadata is retained unchanged in the package. License/copyright per RSI:

### Resources/Textures/_RMC14/Structures/Doors/Windoors/windoor.rsi/meta.json

- License: CC-BY-SA-3.0
- Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/f7b9367cb5c083a8c052a40dece9417031e28ffe/icons/obj/structures/doors/windoor.dmi
- Source: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Doors/Windoors/windoor.rsi/meta.json

### Resources/Textures/_RMC14/Structures/Furniture/chairs.rsi/meta.json

- License: CC-BY-SA-3.0
- Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/9dd2c0d0a1c21e3c9bddbad1321c63fd886f61cf/icons/obj/objects.dmi, https://github.com/cmss13-devs/cmss13/blob/6a955a3c180f3efcf3b997c230fff4d634eb0629/icons/obj/structures/machinery/yautja_machines.dmi, ai_interface_chair taken from cmss13-pve at https://github.com/cmss13-devs/cmss13-pve/blob/master/icons/obj/objects.dmi
- Source: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Furniture/chairs.rsi/meta.json

### Resources/Textures/_RMC14/Structures/Doors/Airlocks/DoubleDoor/engineer_glass.rsi/meta.json

- License: CC-BY-SA-3.0
- Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/78b741a92f09a787c9daf94bb750f98ffaa421cd/icons/obj/structures/doors/2x1engidoor_glass.dmi
- Source: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Doors/Airlocks/DoubleDoor/engineer_glass.rsi/meta.json

### Resources/Textures/_RMC14/Structures/Furniture/Couches/couch_bar.rsi/meta.json

- License: CC-BY-SA-4.0
- Copyright: Sprites by github noctyrnal
- Source: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Furniture/Couches/couch_bar.rsi/meta.json

### Resources/Textures/_RMC14/Objects/power_loader.rsi/meta.json

- License: CC-BY-SA-3.0
- Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/bf507b617175e0838cb80714ae003325540ba037/icons/obj/vehicles/powerloader.dmi
- Source: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/power_loader.rsi/meta.json

### Resources/Textures/_RMC14/Structures/Doors/Airlocks/hybrisa_personal_door_glass.rsi/meta.json

- License: CC-BY-SA-3.0
- Copyright: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/doors/hybrisa_personaldoor_glass.dmi
- Source: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Doors/Airlocks/hybrisa_personal_door_glass.rsi/meta.json

### Resources/Textures/_RMC14/Structures/Furniture/washing_machine.rsi/meta.json

- License: CC-BY-SA-3.0
- Copyright: Taken from CM-SS13 at commit https://github.com/cmss13-devs/cmss13/blob/5a2359bab582e18b3b432539733be04204148a5e/
- Source: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Furniture/washing_machine.rsi/meta.json

### Resources/Textures/_RMC14/Structures/Walls/elevator.rsi/meta.json

- License: CC-BY-SA-3.0
- Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/09a5191fb11aab8ddffe3f9be94292b53e4d96f6/icons/turf/elevator.dmi
- Source: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Walls/elevator.rsi/meta.json

### Resources/Textures/_RMC14/Structures/ladder.rsi/meta.json

- License: CC-BY-SA-3.0
- Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7cb618c69b75873f3ce893022fe08d1233b3152d/icons/obj/structures/structures.dmi, https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/dropship/dropship_equipment.dmi
- Source: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/ladder.rsi/meta.json

### Resources/Textures/_RMC14/Structures/stairs.rsi/meta.json

- License: CC-BY-SA-3.0
- Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/8e8d26bbb4f1617ea1b1ffc17ffffca552ce8c11/icons/obj/structures/stairs, rampbottom taken from https://github.com/cmss13-devs/cmss13/blob/8e8d26bbb4f1617ea1b1ffc17ffffca552ce8c11/icons/obj/structures/structures.dmi
- Source: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/stairs.rsi/meta.json

### Resources/Textures/_RMC14/Structures/catwalk.rsi/meta.json

- License: CC-BY-SA-3.0
- Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/09a5191fb11aab8ddffe3f9be94292b53e4d96f6/icons/turf/almayer.dmi, https://github.com/cmss13-devs/cmss13/blob/8e8d26bbb4f1617ea1b1ffc17ffffca552ce8c11/icons/turf/floors/ice_colony/shiva_floor.dmi, https://github.com/cmss13-devs/cmss13/blob/8e8d26bbb4f1617ea1b1ffc17ffffca552ce8c11/icons/turf/floors/prison.dmi, https://github.com/cmss13-devs/cmss13/blob/8e8d26bbb4f1617ea1b1ffc17ffffca552ce8c11/icons/obj/structures/structures.dmi, https://github.com/cmss13-devs/cmss13/blob/8e8d26bbb4f1617ea1b1ffc17ffffca552ce8c11/icons/obj/structures/props/mining.dmi, https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/hybrisa/grates.dmi, https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/hybrisa/piping_wiring.dmi, https://github.com/cmss13-devs/cmss13/blob/29bfb8501be93dde3c2eececfcf60ae82b0e32df/icons/turf/floors/aicore.dmi, https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/turn/floors/hybrisafloors.dmi
- Source: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/catwalk.rsi/meta.json

### Resources/Textures/_RMC14/Structures/Doors/Airlocks/personal_door.rsi/meta.json

- License: CC-BY-SA-3.0
- Copyright: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/f7b9367cb5c083a8c052a40dece9417031e28ffe/icons/obj/structures/doors/personaldoor.dmi
- Source: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Doors/Airlocks/personal_door.rsi/meta.json

### Resources/Textures/_RMC14/Structures/Walls/elevator_single.rsi/meta.json

- License: CC-BY-SA-3.0
- Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/09a5191fb11aab8ddffe3f9be94292b53e4d96f6/icons/turf/elevator.dmi
- Source: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Walls/elevator_single.rsi/meta.json

### Resources/Textures/_RMC14/Structures/overhead_fuel.rsi/meta.json

- License: CC-BY-SA-3.0
- Copyright: Taken from cmss13-pve at https://github.com/cmss13-devs/cmss13-pve/blob/53b8f7f634ef16422e463bd75eeaa16dccab25ab/icons/obj/structures/props/overhead_ducting.dmi
- Source: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/overhead_fuel.rsi/meta.json

### Resources/Textures/_RMC14/Structures/Furniture/bed.rsi/meta.json

- License: CC-BY-SA-3.0
- Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/9dd2c0d0a1c21e3c9bddbad1321c63fd886f61cf/icons/obj/objects.dmi, https://github.com/cmss13-devs/cmss13/blob/6a955a3c180f3efcf3b997c230fff4d634eb0629/icons/obj/structures/machinery/yautja_machines.dmi, https://github.com/cmss13-devs/cmss13/blob/39a39f5df6c4b32708e50ed711dc5b1bebe313b6/icons/obj/structures/props/furniture/chairs.dmi
- Source: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Furniture/bed.rsi/meta.json

### Resources/Textures/_RMC14/Structures/Walls/Barricades/barricade.rsi/meta.json

- License: CC-BY-SA-3.0
- Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/5cf465e72efb6beccd2b78bf263072816a2a60ad/icons/obj/structures/barricades.dmi
- Source: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Walls/Barricades/barricade.rsi/meta.json

### Resources/Textures/_RMC14/Structures/Furniture/Tables/reinforced.rsi/meta.json

- License: CC-BY-SA-3.0
- Copyright: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/5c70b0d01cc16865b7a000c3a74e0d4f729661f6/icons/obj/structures/tables.dmi
- Source: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Furniture/Tables/reinforced.rsi/meta.json

### Resources/Textures/_RMC14/Structures/Xenos/xeno_tunnel.rsi/meta.json

- License: CC-BY-SA-3.0
- Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/e1a270d04e95a2294c43ecb309f17c294fcfeb8d/icons/obj/structures/ladders.dmi, hole by github noctyrnal
- Source: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Xenos/xeno_tunnel.rsi/meta.json

### Resources/Textures/_RMC14/Structures/Furniture/Tables/prison.rsi/meta.json

- License: CC-BY-SA-3.0
- Copyright: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/5c70b0d01cc16865b7a000c3a74e0d4f729661f6/icons/obj/structures/tables.dmi
- Source: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Furniture/Tables/prison.rsi/meta.json

### Resources/Textures/_RMC14/Structures/overhead_pipes.rsi/meta.json

- License: CC-BY-SA-3.0
- Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/f7b9367cb5c083a8c052a40dece9417031e28ffe/icons/obj/pipes/pipes.dmi
- Source: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/overhead_pipes.rsi/meta.json

### Resources/Textures/_RMC14/Structures/platforms.rsi/meta.json

- License: CC-BY-SA-3.0
- Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/platforms.dmi, https://github.com/cmss13-devs/cmss13/blob/48e570bd697f2476e28d89cd255d0539a5228228/icons/obj/structures/props/platforms.dmi
- Source: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/platforms.rsi/meta.json

### Resources/Textures/_RMC14/Structures/Furniture/Tables/gambling.rsi/meta.json

- License: CC-BY-SA-3.0
- Copyright: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/5c70b0d01cc16865b7a000c3a74e0d4f729661f6/icons/obj/structures/tables.dmi
- Source: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Furniture/Tables/gambling.rsi/meta.json

### Resources/Textures/_RMC14/Structures/Doors/Airlocks/prison_door.rsi/meta.json

- License: CC-BY-SA-3.0
- Copyright: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/f7b9367cb5c083a8c052a40dece9417031e28ffe/icons/obj/structures/doors/celldoor.dmi
- Source: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Doors/Airlocks/prison_door.rsi/meta.json

### Resources/Textures/_RMC14/Structures/Furniture/Couches/couch.rsi/meta.json

- License: CC-BY-SA-3.0
- Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/055c4127d76facec463411169bd2bc513980b0e1/icons/obj/structures/props/sofas.dmi
- Source: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Furniture/Couches/couch.rsi/meta.json

### Resources/Textures/_RMC14/Structures/Furniture/Couches/brown_couch.rsi/meta.json

- License: CC-BY-SA-3.0
- Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/39a39f5df6c4b32708e50ed711dc5b1bebe313b6/icons/obj/structures/props/furniture/chairs.dmi, edited by SharkSnake98 on GitHub
- Source: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Furniture/Couches/brown_couch.rsi/meta.json

### Content.CMU/Resources/Textures/CMU14/N14content/world.rsi/meta.json

- License: CC-BY-NC-SA-3.0
- Copyright: Taken from mojave-sun-13 at https://github.com/Mojave-Sun/mojave-sun-13/blob/ffcecc82f28c796f8eff92ac46ff0f5e0d9b1ab6/mojave/icons/structure/miscellaneous.dmi
- Source: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Textures/CMU14/N14content/world.rsi/meta.json

### Resources/Textures/_RMC14/Structures/Furniture/Tables/standard.rsi/meta.json

- License: CC-BY-SA-3.0
- Copyright: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/5c70b0d01cc16865b7a000c3a74e0d4f729661f6/icons/obj/structures/tables.dmi
- Source: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Furniture/Tables/standard.rsi/meta.json
