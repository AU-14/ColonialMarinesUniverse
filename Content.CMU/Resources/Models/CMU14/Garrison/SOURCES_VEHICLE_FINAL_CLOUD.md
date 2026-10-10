# Final Garrison vehicle and canopy cloud art drafts

Nine source-specific editable assemblies cover nine exact Redux prototype IDs / nine saved Redux placements and six classic placements. There are **zero additional directional or state partners** and zero animation clips. All remain drafts; no native scene, full-map fit, engine collision, runtime animation, Khronos validation or fidelity approval is claimed.

Checkpoint: `TheHellFireo/CMU-Garrison-3D` / `Chip/garrison-3d` at `6e37a4d0a7d9433838c393a82c02422cb704dd5a`. The fetched owning prototypes, complete ancestor chain for the loader, source metadata, PNGs and relevant movement visualizer code are SHA-verified. All nine assigned IDs were found in the checkpoint physicalModelingFamilies and Redux inventory.

## Editable art and source evidence

- Definitions: `garrison_vehicle_final_cloud.yml` and `garrison_vehicle_final_cloud_art.yml`
- Source-crop folder: `/Textures/CMU14/ThreeD/vehicle_final_cloud/`
- 708 individual supported solid parts, maximum 122 per assembly
- 33 byte-exact crop PNGs / 73 uses, all at most 64 pixels per dimension
- Atlas slots follow the exact reserved `vehicle_final` sequence; gaps are intentional and untouched
- Untextured paint is chosen exclusively from the matching default source-state palette (plus the explicitly inherited stationary wheel layer for the vans)
- Crops carry printed marks, small windows, vents or wheel-hub detail only. Silhouettes are real wheels, chassis, shell walls, roof slopes, cab glazing volumes, poles and tensioned fabric bands; no whole-sprite slab is used

## Source-specific construction

| Exact prototype | Default source state | Parts | Construction and limits |
|---|---|---:|---|
| AU14LargeTruckLongBlue | longtruck_blue | 122 | Five axles / ten tires, source wheel centers, separate cab window recesses, hollow closed cargo shell and physical upper-panel/roof ribs. S/E source slots are identical, N/W are identical. Existing source-cardinal alias field records those two views. |
| AU14LargeTruckLongRed | longtruck_red_damage_2 | 122 | Red hauler uses the actual default damage_2 artwork. The name does not trigger or imply a damage animation. Same source alias pattern, distinct paint and weathering. |
| AU14PropAPCMedical | apc_base_med | 71 | Four large tires, distinct sloping front armor and recessed driver glass, side service panels, medical roof cross and raised rear hatch and twelve recessed wheel fasteners. The default base already includes wheels; wheels_0 is not an inherited layer and was not added. |
| AU14PropTent5 | small_tent | 55 | Transparent lower source bays support an open canopy with poles and joined tensioned roof bands. Hanging valances are opaque canvas. Tiny isolated source pixel at top is documented as source artifact; no invented hardware. |
| AU14PropUSCMBison3 | base_open | 102 | Compared base_closed and base_open pixels. Open default has an extended rear panel and exposed dark side bay. Real upper side apertures expose recessed interior bench members; 32 supported tire lugs preserve the chunky wheel profile. Hidden door mechanism and exact interior remain inferred. |
| AU14propstockcar | stockcar | 49 | Low numbered coupe with four wheels, long vented hood, sloped windshield/rear glazing, roof livery, side number crops and raised rear spoiler. Corrected both-side printed-number orientation. |
| RMCPropVehicleLoaderTruckBlack | armored_truck_wy_black | 51 | Distinct empty security loader. Recessed channel deck has no loaded variant roof device. Separate sloping cab, open front guard and ladder gaps. Rims/hubs now use source-specific dark wheel colors. SECURITY lettering reads normally on both sides. |
| VehicleCMBPoliceVan | van_base | 69 | Inspected inherited visible van_base frame 0 + stationary wheels_1, hidden damaged_frame. Physical box shell, recessed cab glazing, roof fan/ribs, marshal lettering/shields and rear door marks, plus raised lower side-vent ribs. |
| VehiclePizzaVan | van_base | 67 | Inspected inherited visible van_base frame 0 + stationary wheels_1, hidden damaged_frame. Distinct red/light paint and original pizza branding on physically separate shell panels; physical lower vent ribs and source-dark rims/hubs. |

## Coordinates and state limits

The isolated origin is centered on the authored body with ground Z=0. `groundOffset: 0, 0` is a **provisional modeling origin**, not an assertion that Redux map pivots already fit. Source screen offsets are preserved in the source audit: most CMU props `(0.5, 0.5)`, loader `(1.5, 0.5)`, vans `(0, 0.5)`, stock car `(0, 0)`. None is blindly copied into physical depth or a saved map position. Width/length use source pixel spans and inspected neighboring checkpoint assets where meaningful; exact height/depth and hidden construction remain hypotheses.

The blue/red long-hauler sheet has only two unique views despite four RSI direction slots. `sourceCardinalFacings: [0, 0, 2, 2]` is the existing S/E/N/W turn map with west-facing baseline correction. APC/Bison/vans retain four-direction source evidence and standard facing metadata, without adding direction-partner target counts. The single-frame stock car also retains its existing entity-rotation opt-in because its Sprite does not set noRot. Native direction selection was not tested.

Both live vans have two 0.1-second base frames. Police differs in 16 south-facing roof-fan pixels; pizza differs in 16/17/16/16 pixels by S/N/E/W, including a north-frame stray pixel. Only frame zero is modeled. Existing SpriteMovement switches rmc-wheels between wheels_0 when moving and wheels_1 at rest; the single-layer model adapter is not treated as a valid multilayer vehicle adapter. Motion, tire rotation/removal, damage, doors, passengers, entry, light changes and working vehicle mechanisms remain unsupported. Bison closed/open switching and tent deployment remain unsupported. There are no generated animation clips.

## Verification and review

All evidence is under `Tools/three_d/generated/cloud-review/vehicle-final/`:

- `vehicle-final-source-and-orbit.png`: complete nine-model source-facing / three-quarter / opposite-side overview; auto-fit explicitly labeled
- `*-fixed-scale.png`: nine cards at fixed 128 px/tile and original pixels enlarged fourfold, ground baseline aligned. This is a scale review, not matched-projection certification
- `*-directions.png`: original composed S/N/E/W defaults and physical views for both vans, APC and Bison
- `*-default-source-dir*.png`: original default compositions, including both visible van layers
- `source-crops.json`: source path, RSI direction/frame, exact rectangle and pixel SHA-256 for every crop use
- `vehicle-final-verification.json`: source blob SHA proof, palette checks, source aliases, inheritance/layers, static-frame limits, inventory scope, GLB structure, byte-for-byte repeated unchanged-exporter proof and aperture sampling
- `vehicle-final-contact-checks.json`: exact transformed convex-mesh hull intersection graph. All nine models have one connected component at 0.000015-tile tolerance and lowest solid point at Z=0
- `vehicle-final-blender-import.json`: independent Blender import of final, unchanged-exporter bytes, object/part counts, finite vertices, zero mesh repairs and zero actions
- `vehicle-final-proof.json` and `vehicle-final-family-manifest.json`: model paths, counts, SHA-256 and deliverable files

Construction checks explicitly retain real canopy lower bays, Bison upper side apertures, empty loader channels/ladder gaps, hauler cargo interiors and cab bays. Wheel tread contact is Z=0. Convex intersection proves isolated joining only, not load-bearing engineering, entity collision, exact silhouettes or neighbor clearance.

The checkpoint long/loader family YAML and its SOURCE attribution were inspected as proportion/construction references, not replayed or altered. New authored geometry fixes support gaps within its own assemblies and does not change earlier models, runtime, renderer, server, maps, publication or deployment.

Reproduce from the root with the existing environment:

    python Tools/three_d/author_vehicle_final_cloud.py
    python Tools/three_d/check_vehicle_final_contacts.py
    python Tools/three_d/verify_vehicle_final_cloud.py
    blender -b --python Tools/three_d/check_vehicle_final_blender.py

## Source attribution

All eight source RSIs use **CC-BY-SA-3.0**. Retain the following original attribution and the source RSI metadata with exported art. Source PNGs/crops remain unchanged in color and native pixel resolution; 3D depth/shape construction is new draft adaptation.

### /Textures/CMU14/Structures/vehicles/largevehicles.rsi

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/vehicles/armored_truck_wy_white.dmi, https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/vehicles/armored_truck_blue.dmi, https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/vehicles/armored_truck_wy_black.dmi, https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/vehicles/armored_truck_teal.dmi, https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/vehicles/long_truck_blue.dmi, https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/vehicles/long_truck_brown.dmi, https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/vehicles/long_truck_donk.dmi, https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/vehicles/long_truck_kelland.dmi, https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/vehicles/long_truck_red.dmi, https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/vehicles/long_truck_wy_black.dmi, https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/vehicles/long_truck_wy_blue.dmi, https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/vehicles/ambulance.dmi

License: CC-BY-SA-3.0

### /Textures/CMU14/Structures/vehicles/uscmapc.rsi

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/0a35ccf2c3763697a7a53ba287488bc9fc3bf23a/icons/obj/vehicles/apc.dmi

License: CC-BY-SA-3.0

### /Textures/CMU14/Structures/tentprops.rsi

Taken from cmss13 at https://github.com/Steelpoint/cmss13/blob/e738da93a2ca8e3bc9c4d0635258343414f26f90/icons/obj/structures/props/large_tent_props.dmi

License: CC-BY-SA-3.0

### /Textures/CMU14/Structures/vehicles/bison.rsi

Taken from cmss13 at https://github.com/Steelpoint/cmss13/blob/6d21afd56eaac6d233eae76cf712a2016759509d/icons/obj/vehicles/bison_prop.dmi

License: CC-BY-SA-3.0

### /Textures/CMU14/Structures/vehicles/stockcar.rsi

Heavily edited Mono-Supron by gixer94

License: CC-BY-SA-3.0

### /Textures/_RMC14/Structures/Vehicles/vehicles3.rsi

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/vehicles

License: CC-BY-SA-3.0

### /Textures/CMU14/Structures/vehicles/marshalpaddywagon.rsi

Made by gixer94 for CMU14

License: CC-BY-SA-3.0

### /Textures/_RMC14/Structures/Vehicles/pizza_van.rsi

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/tree/master/icons/obj/vehicles/pizza_van

License: CC-BY-SA-3.0
