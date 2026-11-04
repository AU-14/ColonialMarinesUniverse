# Garrison van and ambulance sources

Eight editable static drafts explicitly map eleven classic objects and eighteen across configured maps: two ambulance facings and six box-van variants. All remain drafts. Canonical definitions are `garrison_vans.yml` and `garrison_van_art.yml`; do not replay one-time scratch authoring scripts over refined YAML.

New geometry is CC0-1.0 to the extent separately licensable. Source visual designs and original/derived artwork retain CC-BY-SA-3.0. Keep these attributions with exported art.

## Original source and states

RSI: `/Textures/_RMC14/Structures/Vehicles/vehicles3.rsi`, with 128-by-64 frames. Original attribution:

> Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/vehicles

References use `ambulance`, `ambulance_f`, `box_van_bluegrey`, `box_van_maintenanceblue`, `box_van_maintenanceblue_f`, `box_van_maintenancewhite_f`, `box_van_red` and `box_van_white`. Each state has one direction. The three maintenance variants contain two fan frames; these drafts preserve the first frame. Roof-fan animation is unfinished.

The blue-gray van faces east despite lacking an `_f` suffix. Flipped ambulances and maintenance vans also face east; the other states face west. These choices are based on the actual source images, not a suffix-only heuristic.

Thirty-seven original PNGs provide 83 crop uses in atlas slots 364–400. Original side doors, recessed cargo panels, hazard markings, glazing, sill vents, roof panels, medical stripes and medical insignia are retained. Source normalization takes the first frame's occupied 63-pixel van width or 75-pixel ambulance width, mirrors east-facing artwork to a canonical west-facing frame, then applies the recorded crop and optional roof quarter-turn. RGBA values are unchanged after those permutations. Pixel and crop audits record each operation and its hash. There is no resampling or generated replacement artwork.

## Geometry and comparison refinements

The six vans have a cab-over body, sloped front glazing, separate cab doors, cargo side walls, inset painted panels, ventilated lower sill, access steps, rear doors and four cylindrical wheels. The three maintenance versions have a separate raised roof-fan housing. Review removed duplicated fan imagery by giving the vent and fan disjoint source crops and footprints.

The two ambulances have their own geometry: a low hood and cab, taller medical compartment, longitudinal roof ribs, service-door panels and step, emergency-lamp lenses, original side windows/medical symbols and a distinct rear closure. Their inferred rear has paired doors, glazing and a red stripe. Cabin floors, seats, dashboard, chassis and axles give the models a physical interior and underside; tires occupy genuine gaps between lower skirts rather than sitting on a full hidden block.

Each wheel has a tire, recessed dark rim, hub and four fasteners. Final wheel colors are sampled from the source rather than reusing the body paint or an overly bright generic rim. Mirror positions and mounting arms were corrected to connect to the actual cab doors. There are 716 parts in this batch: 106 per ambulance, 85 per maintenance van and 83 per plain van. Every model fits the native 128-part budget. Source/four-view cards and an orbit comparison are under `generated/review/vans-*`.

## Pivot, facing and map context

All eleven saved positions and rotations are unchanged, as is every unrelated scene record. Explicit -90/+90-degree model-axis corrections match west/east source facings. There are 482 adjusted facings in the complete classic scene, eleven more than the car checkpoint. Other layout, door and support counts are unchanged; 803 supported props and 74 without an exact support remain.

The original screen-X offset is 1.5 tiles, but the source artwork occupies only the left side of a 128-pixel frame. The occupied-width center is therefore .484375 tiles from the entity origin for vans and .671875 for ambulances: `1.5 - 128/64 + occupiedWidth/64`. Copying the full 1.5 into world X would shift the body too far. Those explicit map-X corrections are independent of facing. A .25-tile map-Y correction and transverse widths are inferred physical presentation choices; a single projected side image does not uniquely determine physical depth. The screen-Y offset is not copied automatically. Simulation transforms and colliders are untouched.

Conservative transformed-part bounds find no intersections with nearby modeled objects for any of the eleven saved placements. This check covers actual modeled neighbors, not missing-object markers. Browser context review includes maintenance vans, the ambulance bay, flipped ambulance, blue-gray van, red van and white flipped maintenance van. Three new assembled map exports preserve entity hierarchy and saved transform metadata.

## Validation and limits

All 83 crop uses are byte-exact after their recorded permutations. The isolated C# harness passes 93 tests against the current canonical model library, including native packing/budgets for every model. All 651 deterministic models and atlas/reference outputs match regeneration. Khronos validation reports zero errors and warnings for 651 individual models and 25 assembled exports. Further evidence is recorded in `Tools/three_d/STATUS.md`. This batch changes assets and documentation; it uses the already-tested box, cylinder and slope renderer without renderer-code changes. Normal server-dependent tests remain unavailable due to the unrelated tactical-map compilation errors documented there.

Dimensions, hidden front/rear design, internal equipment, mirror construction and physical depth are inferred. Curved body-panel bevels, finer wheel geometry, transparent glazing, interior medical equipment, realistic materials, operating lights, fan animation, damage and movement are unfinished. These are static prop replacements; no driving behavior is added. None is marked reviewed, and the normal gameplay viewport has not been replaced.
