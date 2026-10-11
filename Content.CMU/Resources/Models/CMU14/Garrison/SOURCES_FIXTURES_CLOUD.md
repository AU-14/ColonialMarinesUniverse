# White pod door, electrified mesh grille and status-display drafts

Date: 2026-10-06. Source repository: [TheHellFireo/CMU-Garrison-3D, Chip/garrison-3d](https://github.com/TheHellFireo/CMU-Garrison-3D/tree/Chip/garrison-3d).

## Deliverable and scope

Eight canonical draft assemblies add exact draft references for six previously missing Redux prototype IDs, representing 40 saved Redux records and four classic records:

- `RMCPodDoorAlmayerWhite`: 14 Redux; reciprocal closed/open assemblies
- `RMCGrilleElectrified`: 12 Redux; intact open-mesh assembly
- `RMCStationMap`: one Redux / one classic; frame plus first source map-screen pose
- `RMCStatusDisplay`: seven Redux; uninitialized physical housing only
- `RMCStatusDisplayProp`: four Redux / two classic; static prop housing
- `RMCStatusDisplayLarge`: two Redux / one classic; wide static housing

A seventh source prototype, `RMCGrilleElectrifiedBroken`, is an additional source-verified breakage counterpart, absent from the saved-map inventory. It adds no claimed Redux prototype or placement coverage. The state-only pod-door open partner has an empty `sourcePrototypes` list.

There are also 24 separate static source-frame study GLBs: map (two), bluealert (four), redalert (four) and evac (14). They are not additional canonical prototype mappings. Each study uses a complete physical monitor assembly and an original cropped screen image. They are stored in `Tools/three_d/generated/fixtures-cloud-static-studies/`, described by `studies.json`, and compared to the exact composed source in the review directory. Zero animation clips are added.

All models remain `status: draft`. An exact draft reference is not a live appearance adapter, a fidelity approval, a map-contact clearance, or complete gameplay-state coverage.

## Physical construction and source review

### White Almayer pod door

The original one-tile white/gray design is built from separate runners, three thick interlocking leaf sections, inset white armor rims, recessed armor faces, cross rails, and stepped diagonal hazard-paint patches on both faces. No full-sprite plane covers the door. The seven-part open pose exposes the recessed floor pocket and its raised threshold rim with the leaf retracted below floor height. The 2.5-tile height and 0.34-tile front-to-back floor-pocket thickness are inferred physical dimensions, consistent in scale with existing blast-door drafts; unseen mechanics are unfinished.

`doorState` and reciprocal `alternateDoorModel` use the existing stable-state contract. No new controller or timeline is authored. All four source directions, both stable poses, and all 48 transition frames were inspected. S/N are identical and E/W are identical for each inspected pose.

The source opening strip contains a significant directional anomaly. S/N opaque-pixel counts decrease `1024,1024,1024,896,640,320`; E/W counts increase `192,512,640,768,896,1024`. The E/W opening strip progresses exactly like its closing strip, toward a closed-looking image. The six source frames each hold 0.1 seconds, while the inherited shutter controller declares one-second opening/closing animations. The existing `doorSpriteStates` contract uses one physical four-direction composition per frame, so this batch does not pretend those contradictory source directions form an accepted transition. Opening/closing remain unsupported. No source sprite was altered. Welding, power/alert-control interaction, destruction and interruption remain unverified.

Prototype sources: `Resources/Prototypes/_RMC14/Entities/Structures/Doors/Shutters/poddoor.yml` and `shutters.yml`.

### Electrified grille and broken counterpart

The intact grille contains 71 separately editable solid parts, including crossing diagonal wires with actual through-openings, black perimeter rails, a ribbed red conductor casing, yellow warning stripes, and two cylindrical multi-ring coils. There is no mesh-opacity billboard. The 51-part broken design removes the center span and retains ragged wire remnants, fractured conductor sections, fallen coil segments and a bent loose conductor. The exact mechanical height, wire gauge, hidden bends and collapsed arrangement are inferred drafts.

The four source direction frames were inspected for both `grille` and `brokengrille`. S/N show the coil pair along the depth axis and E/W show the pair side by side. A 90-degree model-facing correction retains that source axis; `referenceDirection: 2` selects the face-on E artwork for review. Native facing and saved-map neighbor fitting remain unverified.

The source explicitly names `RMCGrilleElectrifiedBroken` and the corresponding broken construction node; the broken model maps that separate entity rather than inventing a generic damage-state selector. The subclass replaces the inherited sprite layers with one `grille` layer. An electrical spark layer is not present in that replaced composition, so no spark effect or invented powered state was added. Runtime breakage/replacement, power, shock effects and connectivity remain unverified. No `connectToNeighbours` or invented state mapping is claimed.

Prototype source: `Resources/Prototypes/_RMC14/Entities/Structures/Walls/grille.yml`.

### Wall displays

The small and large cases use stepped rear shells, mounting cleats, top bevel tiers, separate bezel rails, a recessed screen well, glass, lower service tabs, individual colored indicators, and ventilation slots. Only the inset screen is textured; the entire sprite is never a flat slab. The source horizontal scale is 32 pixels per tile. The 64-pixel large source with its explicit +0.5 horizontal source offset is authored as a two-tile span from local X -0.5 to +1.5. Height, wall depth and its saved-context fit remain inferred and unverified.

Source screen crops retain the original 28-by-18 region `[2,7,30,25]` without recoloring, resampling or changing alpha. All 24 loaded map/bluealert/redalert/evac frames were inspected and exported as separate static studies. Those resources retain their original delays in the study manifest for evidence only; the files have no animation clips or gameplay timer.

The station map has two visible source layers (frame and map). The status display has a frame, an alert screen and five separately controlled timer layers. GenericVisualizer maps Blue to bluealert, Red to redalert and Delta to evac; Green hides the alert screen and enables the four numeric layers plus separator. The current single-visible-layer `spriteStates` adapter is incompatible with this layer composition and wall mounting. A new runtime layer adapter or an explicit fallback guard is still required for these dynamic displays, and is outside this art-only batch. Merely adding these static `sourcePrototypes` references does not implement that adapter or guard. No fake single-layer binding, invented power controller, current time, alert state or live coverage is claimed.

`RMCStatusDisplayProp` and `RMCStatusDisplayLarge` declare only the static frame. Other unused resource artwork (security, entertainment, AI faces, etc.) and dynamic timer digits were not modeled. Native display/UI behavior, source flicker timing, material emission and live alert transitions remain unfinished.

Prototype sources: `Resources/Prototypes/_RMC14/Entities/Structures/Machines/status_display.yml` and `Resources/Prototypes/_RMC14/Entities/Structures/Wallmounts/station_map.yml`.

## Attribution and license

All referenced source RSIs declare **CC-BY-SA-3.0**. Retain their unchanged `meta.json` files and this notice with the derivative model/screen-crop distribution. The source-owned screen textures and source-derived appearance are distributed under the same CC-BY-SA-3.0 terms. No attribution has been removed.

- `white_almayer_poddoor.rsi`: “Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/6d78d51911aed7ab1afa4cee84d5207dee05dfc3/icons/obj/structures/doors/blastdoors_shutters.dmi”
- `electric_grille.rsi`: “Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/hybrisa/piping_wiring.dmi”
- `status_display.rsi`: “Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/machinery/status_display.dmi, map by github noctyrnal”
- `status_display_large.rsi`: “Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/machinery/status_display.dmi, modified by github noctyrnal”

The complete resource metadata, file hashes, directions, frame hashes, alpha coverage and source timings are recorded in `Tools/three_d/generated/fixtures-cloud-source-audit.json`. Screen crop provenance and SHA-256 hashes are in `fixtures-cloud-verification.json`.

## Build and verification

The reproducible art authoring script is `Tools/three_d/authoring/author_fixtures_cloud.py`. It calls the existing unchanged `build_models.load_models`, `validate_model`, `glb_bytes`, `write_reviews` and `render_model` functions directly. It does not monkeypatch the exporter, edit runtime/engine code, or run the aggregate pipeline on this incomplete workspace.

Canonical YAML: `garrison_fixtures_cloud.yml` and `garrison_fixtures_cloud_art.yml`. Canonical GLBs: eight `*Cloud.glb` models named in the verification report. Derived screen textures: `Content.CMU/Resources/Textures/CMU14/ThreeD/fixtures_cloud/`. All 24 surface indices, 2120–2143, are checked against the 142-file baseline registry and are collision-free.

The focused verification report checks positive finite model bounds, reciprocal stable door links, the 128-part limit (maximum 72), byte-exact source screen crops, unique baseline-free surface indices, deterministic direct GLB exports and zero clips. Independent Blender import results are in `fixtures-cloud-blender-import.json`. Review sheets and static composed-source comparisons are in `Tools/three_d/generated/review/fixtures-cloud/`.

No engine launch, native GPU test, gameplay state/interaction test, full-map placement export, same-tile contact audit, full-repository build/test suite or Khronos validation is claimed by this family report. The bounded connector-fetched workspace is an art-production subset, not a complete runnable checkout. Nothing was published.


## Cumulative atlas assignment
This cumulative snapshot uses centrally allocated, nonconflicting atlas slots. Any authoring-time numeric range in this historical family note is superseded by the canonical surface YAML and `Tools/three_d/generated/cloud-review/atlas-allocation-current.json`. Source IDs, original PNG pixels and modeled geometry are unchanged by atlas-index remapping.
