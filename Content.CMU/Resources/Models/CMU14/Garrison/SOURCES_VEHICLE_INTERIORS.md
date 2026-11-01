# Vehicle interior model sources

Editable CMU cabin drafts for all 27 VehicleEnter maps and the procedural fighter cockpit. One tile equals one model unit. Depth and hidden faces are authored interpretations. Models do not change vehicle collision, contents or travel.

Geometry uses structural boxes, rounded seats, rails and closed fittings, not sprite-pixel extrusion. Roof pieces are elevated above occupants; large chassis leave the playable cabin hollow. Optional assets are loaded only by the 3D client.

## Source artwork

Original RSI metadata and licenses remain authoritative. Generated PNGs are crops of these source sprites.

- `CMU14/Structures/portable_iv_drip.rsi` — CC-BY-SA-3.0; Sprites by shamblestf on discord.
- `CMU14/Structures/vehicles/Blackfoot/blackfoot_door_button.rsi` — CC-BY-SA-3.0; Taken from CM13 PR #10291 at https://github.com/cmss13-devs/cmss13/blob/040a7c240f53d077c67edc92e2e57a927a477a4a/icons/obj/structures/props/stationobjs.dmi
- `CMU14/Structures/vehicles/Blackfoot/blackfoot_peripherals.rsi` — CC-BY-SA-3.0; Taken from CM13 PR #10291 at https://github.com/cmss13-devs/cmss13/blob/040a7c240f53d077c67edc92e2e57a927a477a4a/icons/obj/vehicles/blackfoot_peripherals.dmi
- `CMU14/Structures/vehicles/Blackfoot/interiors/blackfoot.rsi` — CC-BY-SA-3.0; Taken from CM13 PR #10291 at https://github.com/cmss13-devs/cmss13/blob/040a7c240f53d077c67edc92e2e57a927a477a4a/icons/obj/vehicles/interiors/blackfoot.dmi
- `CMU14/Structures/vehicles/Blackfoot/interiors/blackfoot_64x64.rsi` — CC-BY-SA-3.0; Taken from CM13 PR #10291 at https://github.com/cmss13-devs/cmss13/blob/040a7c240f53d077c67edc92e2e57a927a477a4a/icons/obj/vehicles/interiors/blackfoot_64x64.dmi
- `CMU14/Structures/vehicles/Blackfoot/interiors/blackfoot_chassis.rsi` — CC-BY-SA-3.0; Taken from CM13 PR #10291 at https://github.com/cmss13-devs/cmss13/blob/040a7c240f53d077c67edc92e2e57a927a477a4a/icons/obj/vehicles/interiors/blackfoot_chassis.dmi
- `CMU14/Vehicles/Fighter/jetfighter.rsi` — CC-BY-SA-3.0; Original artwork by nzzy on Discord, supplied by the contributor. Folded-wing adaptation by CMU.
- `CMU14/Vehicles/Fighter/pilotseats.rsi` — CC-BY-SA-3.0; Original artwork by nzzy on Discord, supplied by the contributor.
- `Structures/Wallmounts/intercom.rsi` — CC-BY-SA-3.0; By MattFright, modified extensively AftrLite (GitHub)
- `_RMC14/Objects/Materials/Sheets/rmc_cardboard.rsi` — CC-BY-SA-3.0; Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/bec6653d487a49aa2b5a8e0c97bed9612f620211/icons/mob/humans/onmob/items_lefthand_0.dmi, https://github.com/cmss13-devs/cmss13/blob/6791699de749afc1c62bc65ec6f2326a00b3bb61/icons/obj/items/items.dmi, https://github.com/cmss13-devs/cmss13/blob/bec6653d487a49aa2b5a8e0c97bed9612f620211/icons/mob/humans/onmob/items_righthand_0.dmi
- `_RMC14/Objects/Medical/bodybags.rsi` — CC-BY-SA-3.0; Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/09a5191fb11aab8ddffe3f9be94292b53e4d96f6/icons/obj/bodybag.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/medical_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/medical_lefthand.dmi
- `_RMC14/Objects/Medical/defib.rsi` — CC-BY-SA-3.0; Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/05eaa3484b1a35aa759300bcfc8f0119f009daae/icons/obj/items/devices.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/medical_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/medical_lefthand.dmi
- `_RMC14/Objects/Medical/stasisbag.rsi` — CC-BY-SA-3.0; Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/a5990526c258405745260c0b2f8fac01c84261a3/icons/obj/cryobag.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/medical_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/medical_lefthand.dmi, holocard sprites by Vermidia
- `_RMC14/Objects/Storage/pizza_galaxy_box.rsi` — CC-BY-SA-3.0; Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/obj/items/food/pizza.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items/food_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items/food_righthand.dmi
- `_RMC14/Structures/Furniture/Tables/toc.rsi` — CC-BY-SA-3.0; taken from CMSS13 https://github.com/cmss13-devs/cmss13-pve/blob/master/icons/obj/structures/machinery/toc.dmi
- `_RMC14/Structures/Furniture/rollerbeds.rsi` — CC-BY-SA-3.0; Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/rollerbed.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/medical_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/medical_lefthand.dmi
- `_RMC14/Structures/Furniture/vehicle_seats.rsi` — CC-BY-SA-3.0; Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/0a35ccf2c3763697a7a53ba287488bc9fc3bf23a/icons/obj/vehicles/interiors/general.dmi, https://github.com/cmss13-devs/cmss13/blob/b5a595ce32f9bb4fc8abde0efe21196b3862d8bf/icons/obj/objects.dmi
- `_RMC14/Structures/Machines/VendingMachines/ColMarTech/guns.rsi` — CC-BY-SA-3.0; Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/09a5191fb11aab8ddffe3f9be94292b53e4d96f6/icons/obj/structures/machinery/vending.dmi
- `_RMC14/Structures/Machines/VendingMachines/GunRacks/t71_rack.rsi` — CC-BY-SA-3.0; Sprites taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/structures/gun_racks.dmi.
- `_RMC14/Structures/Machines/computer.rsi` — CC-BY-SA-3.0; Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/d5b119380250ea512db2a5319e36592c7f604250/icons/obj/structures/machinery/computer.dmi, edits to overwatch and register by github noctyrnal
- `_RMC14/Structures/Machines/map_table.rsi` — CC-BY-SA-3.0; Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/d5b119380250ea512db2a5319e36592c7f604250/icons/obj/structures/machinery/computer.dmi
- `_RMC14/Structures/Machines/rmc_groundside_communications_console.rsi` — CC-BY-SA-3.0; Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/d5b119380250ea512db2a5319e36592c7f604250/icons/obj/structures/machinery/computer.dmi
- `_RMC14/Structures/Machines/toc_spp.rsi` — CC-BY-SA-3.0; taken from CMSS13 https://github.com/cmss13-devs/cmss13-pve/blob/master/icons/obj/structures/machinery/toc_upp.dmi
- `_RMC14/Structures/Storage/Crates/construction.rsi` — CC-BY-SA-3.0; Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7cb618c69b75873f3ce893022fe08d1233b3152d/icons/obj/structures/crates.dmi
- `_RMC14/Structures/Storage/Crates/green.rsi` — CC-BY-SA-3.0; Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7cb618c69b75873f3ce893022fe08d1233b3152d/icons/obj/structures/crates.dmi
- `_RMC14/Structures/Vehicles/Interiors/apc.rsi` — CC-BY-SA-3.0; Taken from cmss13 at https://github.com/cmss13-devs/cmss13/tree/master/icons/obj/vehicles/interiors/apc
- `_RMC14/Structures/Vehicles/Interiors/box_van_interior.rsi` — CC-BY-SA-3.0; Taken from cmss13 at https://github.com/cmss13-devs/cmss13/tree/master/icons/obj/vehicles/interiors/box_van_interior
- `_RMC14/Structures/Vehicles/Interiors/general.rsi` — CC-BY-SA-3.0; Taken from cmss13 at https://github.com/cmss13-devs/cmss13/tree/master/icons/obj/vehicles/interiors/general
- `_RMC14/Structures/Vehicles/Interiors/general_humvee.rsi` — CC-BY-SA-3.0; Taken from CMSS13 https://github.com/cmss13-devs/cmss13/pull/11308
- `_RMC14/Structures/Vehicles/Interiors/humvee_chassis.rsi` — CC-BY-SA-3.0; Taken from CMSS13 https://github.com/cmss13-devs/cmss13/pull/11308
- `_RMC14/Structures/Vehicles/Interiors/pizza_van_interior.rsi` — CC-BY-SA-3.0; Taken from cmss13 at https://github.com/cmss13-devs/cmss13/tree/master/icons/obj/vehicles/interiors/pizza_van_interior
- `_RMC14/Structures/Vehicles/Interiors/sppapc.rsi` — CC-BY-SA-3.0; Taken from CMSS13 https://github.com/cmss13-devs/cmss13-pve/blob/master/icons/obj/vehicles/interiors/uppapc.dmi
- `_RMC14/Structures/Vehicles/Interiors/sppapc_chassis.rsi` — CC-BY-SA-3.0; Taken from CMSS13 https://github.com/cmss13-devs/cmss13-pve/blob/master/icons/obj/vehicles/interiors/uppapc_chassis.dmi
- `_RMC14/Structures/Vehicles/Interiors/spptank.rsi` — CC-BY-SA-3.0; Taken from CMSS13 https://github.com/cmss13-devs/cmss13-pve/blob/master/icons/obj/vehicles/interiors/upptank.dmi
- `_RMC14/Structures/Vehicles/Interiors/spptank_chassis.rsi` — CC-BY-SA-3.0; Taken from CMSS13 https://github.com/cmss13-devs/cmss13-pve/blob/master/icons/obj/vehicles/interiors/upptank_chassis.dmi
- `_RMC14/Structures/Vehicles/Interiors/sppvan.rsi` — CC-BY-SA-3.0; Taken from CMSS13 https://github.com/cmss13-devs/cmss13-pve/blob/master/icons/obj/vehicles/interiors/uppvan.dmi
- `_RMC14/Structures/Vehicles/Interiors/sppvan_chassis.rsi` — CC-BY-SA-3.0; Taken from CMSS13 https://github.com/cmss13-devs/cmss13-pve/blob/master/icons/obj/vehicles/interiors/uppvan_chassis.dmi
- `_RMC14/Structures/Vehicles/Interiors/tank.rsi` — CC-BY-SA-3.0; Taken from CMSS13 https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/vehicles/interiors/tank.dmi
- `_RMC14/Structures/Vehicles/Interiors/van.rsi` — CC-BY-SA-3.0; Taken from cmss13 at https://github.com/cmss13-devs/cmss13/tree/master/icons/obj/vehicles/interiors/van_interior
- `_RMC14/Structures/Wallmounts/LightingOffset/light_bulb.rsi` — CC-BY-SA-3.0; Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/884db783073c035b756c175b1bc75fb43279803e/icons/obj/items/lighting.dmi
- `_RMC14/Structures/Wallmounts/camera.rsi` — CC-BY-SA-3.0; Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/f7a0eff3793efe08e10ed0299c8c69b93d93b2c3/icons/obj/structures/machinery/monitors.dmi

## Rebuild and review

Run `python Tools/three_d/author_vehicle_interiors.py`, then export `garrison_vehicle_interiors.yml` with `build_models.py`. `Reviews/VehicleInteriors/coverage.json` records exact bindings and intentional invisible helpers. Review sheets include the source, front, rear and underside.
