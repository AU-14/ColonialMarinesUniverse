# Garrison small cargo trucks and road-barrier corrections

Six new static drafts cover six classic objects and ten across configured maps. Two existing road-barrier models are also corrected across 64 classic placements. All remain drafts. Canonical truck definitions are `garrison_small_trucks.yml` and `garrison_small_truck_art.yml`; barriers remain in `garrison_environment.yml`, with original reflector artwork in `garrison_road_barrier_art.yml`. Do not replay one-time authoring scripts over the refined YAML.

New geometry is CC0-1.0 to the extent separately licensable. Original source artwork and derivative visual designs retain their source licenses. Keep these attributions with exported art.

## Truck sources and geometry

RSI: `/Textures/_RMC14/Structures/Vehicles/vehicles3.rsi`, 128-by-64 frames, CC-BY-SA-3.0. Attribution:

> Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/vehicles

The six source states are `small_truck_brown_cargo`, `small_truck_brown_cargobarrels`, `small_truck_garbage`, `small_truck_garbage_f`, `small_truck_green_f` and `small_truck_red_f`. Each has one direction and one frame. The occupied artwork lies within the leftmost 64 columns. First-frame normalization mirrors those 64 columns for `_f` states, then applies recorded crops and optional roof quarter-turns. Twenty-two original PNGs / 50 byte-exact crop uses occupy slots 401–422.

Each truck has three axles and six cylindrical wheels, wheel openings, separate rims/hubs/fasteners, cab floor and seats, dashboard, sloped front glazing, original cab roof/side panels, mirrors and steps. Tire/rim shades were refined against source wheel pixels. Empty green/red variants have actual recessed bed floors with separate walls and rails. The brown cargo variant has a tall strapped load with original side/top panels and raised retaining straps. Its alternate has a shallow strapped load and separate red/blue cylindrical barrels with bands, recessed lids, rims and bungs. Garbage variants have a closed body, rounded roof, six raised transverse ribs, original side panels and an inferred rear packer face. Barrel colors and dark lids follow source palette samples.

The six models contain 656 parts: 113 for the tall cargo load, 125 for the barrels, 114 per garbage variant and 95 per empty-bed variant. They fit the 128-part native preview budget. Hidden construction, physical depth, cabin interior, barrel support arrangement and rear faces remain inferred from a single projected side view.

## Truck facing and placement

Ordinary source states face west; `_f` states face east. Explicit -90/+90-degree model corrections preserve those facings. The half-tile map-X correction follows occupied artwork in the padded 128-pixel frame: `1.5 - 128/64 + 64/64`. Inferred transverse width and .25-tile map-Y correction follow source proportions and saved context; screen-Y is not copied automatically. Every saved position/rotation and every unrelated scene record is unchanged. The complete scene now has 488 adjusted facings; other layout, door and support counts are unchanged.

After correcting the barriers below, five of the six new placements have no conservative intersections with neighboring modeled objects. Garbage truck #14386 intersects inherited janitor-crate candidate #10329. Their original saved colliders already overlap by about .418 tiles in X and .500 in Y. `small-trucks-source-collision-audit.json` records both world-space source bounds. No new translation, hidden object or invented support removes that original overlap. The crate remains an inherited candidate and is omitted from default exact-only region exports; browser evidence includes it explicitly.

## Road-barrier source correction

RSI: `/Textures/_RMC14/Structures/Walls/Barricades/barricade.rsi`, 32-by-32 frames, CC-BY-SA-3.0. Attribution:

> Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/5cf465e72efb6beccd2b78bf263072816a2a60ad/icons/obj/structures/barricades.dmi

`plasticroadbarrierred` and `plasticroadbarrierblue` each have four directions. The old drafts were centered on the tile and 1.05 tiles tall, causing the barrel truck to pierce two blue barriers. The source fixture is instead at local Y -.45 to -.30. Revised body geometry centers on -.375, and entity rotation carries this offset around all four tile edges. Its inferred .551-tile height follows the short source silhouette, which has low legs, a framed slotted panel and alternating orange/gray reflectors. Four original South/North reflector crops occupy slots 423–426; generic tan patches were removed.

Global review caught oversized inferred feet against wall trim, rock borders and van tires. The feet now stay within the source fixture, with final Y bounds -.425 to -.31. All 64 classic barriers (52 blue / 12 red) retain their saved transforms. Conservative checks reduce ten neighbor contacts to three previously present cone contacts, with no newly introduced contacts. The barrel truck now clears both barrier assemblies; its original collider also has a .1035-tile horizontal gap from theirs. No truck or barrier entity was moved.

Barrier dimensions and feet remain inferred; damage, acid overlays and other live appearance states are unfinished. The comparison cards include both corrected barriers and all six trucks. Detailed source palettes, directional crops, prior/current contacts and original collider bounds are recorded under `generated/road-barriers-*` and `generated/small-trucks-*`.

## Validation and remaining work

All original truck/reflector crop uses match their source RGBA after the recorded permutations. The isolated native harness passes 93 checks against the current library, including packing and budgets for every model. All 657 deterministic model exports and atlas/reference outputs match regeneration. Khronos validation reports zero errors and warnings for 657 individual models and 28 assembled exports. Further evidence is recorded in `Tools/three_d/STATUS.md`. This pass changes assets and documentation; renderer code is unchanged.

Truck materials, curved cab panels, transparent glazing, wheel details, suspension, cargo restraints, moving/occupied/damaged states and compactor mechanisms remain unfinished. The original crate overlap remains explicit. None of these assets is approved, and the standard gameplay viewport is unchanged. Connected tests remain unavailable because of the unrelated tactical-map server compilation errors documented in the checkpoint.
