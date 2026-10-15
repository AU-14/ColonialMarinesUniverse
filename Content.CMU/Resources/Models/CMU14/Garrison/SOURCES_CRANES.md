# Garrison cranes and Kelland mining van

Four source-specific drafts / 391 parts cover seven classic placements and fifteen across configured maps. Canonical definitions are `garrison_cranes.yml` and `garrison_crane_art.yml`. These complete draft coverage of the 43 vehicle types in the inspected saved-map inventory, not final vehicle art or gameplay behavior.

## Construction and source evidence

The crane has two physical crawler runs with six separate rollers per run, visible treads/hubs, an open grated deck, offset right-hand operator cab, source roof vent and door/glass panels, access ladder, side cylinder and rear-mounted telescoping boom. The loaded variant has two individual wooden crates at the sizes and arrangements visible in the source. The AU and RMC empty-crane sheets are byte-identical; their separately mapped drafts retain different source offsets. Initial comparison exposed a boom cap obscuring the original markings; the cap now clears those panels.

The Kelland van uses its saved North `box_van_kellandmining_damage_3` frame, mirrored over the full 64-pixel frame for local front -Y. Original broken branding, roof stripe, door and sill vent retain source RGBA. Four cylindrical tires have independent rims/hubs and real wheel openings. The front/rear and physical depth are inferred. The state name alone was not used to invent wreck holes. Glazing uses the source color #364141 at normalized North pixel 6,28.

Ten original PNGs / 21 verified crop uses occupy slots 475–484. The complete 484-image atlas is 4096 by 512 / 8 MiB. All images preserve RGBA after their documented crop, horizontal flip and roof quarter-turn.

## Nonstandard source directions

These are four-direction 64-by-64 source frames in 128-by-128 sheets, but they do not depict four rigid quarter turns. All cranes have byte-identical South/West frames and byte-identical North/East frames. The van South/West pair is also identical; North/East point the same way but differ at 1,187 pixel positions, including a one-pixel horizontal framing change. The saved North frame is the van's canonical art.

`sourceCardinalFacings: [0, 2, 2, 0]` is indexed South/East/North/West, not RSI sheet order. Cranes use zero axis correction; the van adds -90 degrees, yielding west for S/W and east for N/E. Source `noRot` still selects the directional frame. Residual rotation is retained when noRot is false. Native comparison selects the nearest available physical reference, preferring the requested source on ties. Permutation mappings such as the existing pipe elbows retain their inverse behavior.

The crane art mirrors cargo/cab left-right while still showing frontal details. It cannot fully specify a consistent hidden 3D rear. The saved crane facings all resolve to the original South/West arrangement; the inferred back remains an explicit draft limitation. These aliases are not appropriate for unrelated debris frames with genuinely different arrangements.

## Placement

AU crane and van use +.5 map-X to retain the horizontal centering of the 64-pixel sprite with its +.5 source offset. RMC cranes retain zero horizontal offset. No source screen-Y offset is copied into world depth. Depth and height are inferred from the source silhouette, fixtures and neighboring placements. All 46,561 saved XY/yaw transforms and all unrelated scene records are unchanged.

All seven placements were checked for neighboring exact and inherited model intersections. Only the cargo-crane duplicate pair #14356/#14357 intersects: both have the same original pivot (106.85263, -126.989944) and West transform. These remain two saved entities; neither is deleted nor shifted to hide the overlap. The other five placements have no conservative neighboring-model intersections. The scene now contains 500 corrected facings; support, door, connectivity and wall-placement counts are unchanged.

## Artifacts and verification

- `generated/cranes-source-audit.json`, `cranes-crop-audit.json`, `cranes-pixel-audit.json`, `cranes-placement-audit.json`: dimensions, source alias differences, exact panels, all saved transforms, physical facings and neighbor checks.
- `review/cranes-comparisons-0.png`, `cranes-orbit.png`, and browser map captures cover the variants and every distinct placement.
- Five new assembled regions: `garrison-cranes-yard`, `garrison-cranes-north`, `garrison-cranes-south`, `garrison-cranes-cargo`, `garrison-mining-van`. The existing `garrison-loaders-beds` region includes the RMC alternate-hitbox crane and has been refreshed.
- 111 Python checks and 95 isolated C# checks pass. The affected client build passed with 0 errors / 2,151 warnings. All 668 individual GLBs match deterministic regeneration and pass Khronos validation with no errors or warnings. All 38 assembled exports also pass with no errors/warnings, recorded in `scene-glb-validation.json`. Browser warning/error logs are empty; captures/check totals are in `cranes-verification.json`.

Every model remains a static draft. Hidden faces, physical proportions, material quality, mechanisms, tracks/wheels moving, crane articulation, damage transitions and connected gameplay rendering remain unfinished. The normal gameplay viewport is unchanged.

## Source licenses

New geometric work is CC0-1.0 to the extent separately licensable; derivative appearance and original PNGs retain the source licenses below.

### /Textures/CMU14/Structures/vehicles/boxvvanwhite.rsi

License: CC-BY-SA-3.0

Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/vehicles/box_van_white.dmi, https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/vehicles/vehicles.dmi, https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/vehicles/box_van_bluegrey.dmi, https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/vehicles/box_van_hyperdyne.dmi, https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/vehicles/box_van_kellandmining.dmi, https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/vehicles/box_van_maintenanceblue.dmi, https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/vehicles/box_van_pizza.dmi

### /Textures/_RMC14/Structures/Vehicles/vehicles2.rsi

License: CC-BY-SA-3.0

Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/vehicles/vehicles.dmi

## Source states

| Prototype | State | Parts | Classic objects |
| --- | --- | ---: | ---: |
| AU14PropVehicleCrane | crane | 108 | 3 |
| RMCPropVehicleCargoCraneAltHitboxWest | crane | 108 | 1 |
| RMCPropVehicleCargoCraneCargo | crane_cargo | 116 | 2 |
| AU14PropVehicleKellandMiningVan | box_van_kellandmining_damage_3 | 59 | 1 |
