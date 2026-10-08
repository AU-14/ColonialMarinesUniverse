# Garrison 3D asset production

**Playing on Stable Garrison Redux:** run `cmu3d` in the client console to toggle first person.
The opening popup shows your mouse-capture shortcut (Alt+F8 by default) and links to its
binding under **Options → Controls → Camera → 3D: Toggle mouse capture**.
See [player controls and current limits](FIRST_PERSON_WALK.md). First person is available
to players only on Redux; the separate live scene workbench still requires debug permission.

Generated review screenshots, scene exports and audit outputs are produced locally under
`Tools/three_d/generated/` and are omitted from this submission. The sections below retain
the prototype's production history; their counts and test results describe the original
snapshot, not a new audit of every placement in the current master maps.

This is the asset-production pipeline toward a freely rotating 3D presentation with CMU's existing gameplay. It contains an inventory, editable solid-part models, deterministic GLB export, sprite/model review sheets, mapping coverage, in-game asset and live-scene workbenches, and a local saved-map scene reviewer.

The primary target is Stable Garrison Redux, with first-person presentation and the existing UI retained. The library now contains 2,476 draft assemblies, including 799 worn-equipment models and an unloaded-rifle pose; see the [model alignment review](model-alignment/REVIEW.md). World coverage remains: Redux has 1,532 exact draft visual types, three inherited hatch/ladder candidates and 16 unmapped visual types. None are fidelity-approved. Batch 9 is integrated; 265 ordinary stair tiles now connect raised or recessed ground in both renderers. See [the integration review](art-batch-9/INTEGRATION_REVIEW.md) for verification and unresolved multi-Z/landing work, [STATUS.md](STATUS.md) for the history, and [FIRST_PERSON_WALK.md](FIRST_PERSON_WALK.md) for the debug prototype.

The preceding utility batch added 24 assemblies: a connected foam barrier, sixteen source-owned drinking vessels, and seven kitchen/garden tool designs. They cover 98 visible Redux placements and 18 classic placements. Source states, fill levels and edge visibility select the geometry. All eight scenes are listed in `generated/utility-batch-scenes.json`. Generation and validation are shared per batch.

Twelve existing tree drafts also have conditional mappings for the selected `RandomSprite` state of `FloraTree` and `FloraTreeLarge`. The native debug scene follows the replicated choice. The seven saved classic trees do not serialize that choice, so the offline scene keeps them marked as unknown; these bindings do not inflate static coverage. The [synthetic state fixture](http://127.0.0.1:8766/viewer/?scene=../generated/random-tree-state-fixture.json) compares all twelve choices and three unsupported cases. See `SOURCES_RANDOM_TREES.md` alongside the model exports.

The twelve broadleaf drafts use irregular leaf sprays and source-guided tilted round trunks, roots and hanging branches. A further 76-group crown refinement improves source-scale silhouettes, preserves full canopy depth and replaces exposed smooth cores. Saved transforms, source-traced wood and per-part colors remain unchanged. See [SOURCES_CANOPY_FIT.md](../../Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_CANOPY_FIT.md) and `generated/review/canopy-fit-final/` for current comparisons. Crown density, bark seams, finer curves, existing wall contacts and wind still need work; the earlier foliage-only containment proof is historical.

Twenty direction-specific paper assemblies add four prototype mappings / 23 classic placements. Each original RSI direction selects its own printed arrangement independently of camera orbit. Forty-one thin textured patches retain original frame pivots and all opaque source pixels. Touching pages, curl and eleven unique context-contact pairs remain unfinished. See [SOURCES_FLOOR_PAPERS.md](../../Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_FLOOR_PAPERS.md) and the [synthetic paper comparison](http://127.0.0.1:8766/viewer/?scene=../generated/floor-paper-directions-fixture.json).

Eighteen solid debris arrangements add three prototype mappings / twelve classic placements. Cartons now use the existing pallet support, and one inferred brick depth is refined to clear a wreck tire. Three co-located rock-border overlaps remain unresolved. See [SOURCES_SOLID_FLOOR_DECOR.md](../../Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_SOLID_FLOOR_DECOR.md) and the [direction comparison](http://127.0.0.1:8766/viewer/?scene=../generated/solid-floor-decor-directions-fixture.json).

Three skeletal-remains/book-pile drafts finish the missing N14 floor-decor mappings. Separate bones preserve the bent source pose; the shoreline version clears the authored shallow water. Book comparison corrected upright cover orientation. See [SOURCES_SKELETON_BOOKS.md](../../Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_SKELETON_BOOKS.md). Physical anatomy, stacking and material wear remain drafts.

The cash-soda and CMB-equipment vendors now have source-layer drafts, and the old cola model has been rebuilt. Opposing rows and window-backed vendors face into the room through an opt-in native/offline layout rule. Floor-grate and inherited wire-rail contacts remain unresolved. See [SOURCES_VENDORS.md](../../Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_VENDORS.md).

Coffee, cigarette, snack, soda and equipment fronts retain their original art; all eight vendor variants now use the source horizontal pixel scale independently of inferred height. Wire rails have the source three posts and four bars. Six vendor intersections are cleared; floor-grate, cabinet and shallow rail/platform contacts remain. See [SOURCES_RAIL_VENDOR_FOOTPRINTS.md](../../Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_RAIL_VENDOR_FOOTPRINTS.md).

Prison carpet now composes original borders and corners from source neighbour states in the browser and native debug scene, covering 132 Redux and 20 classic placements. Thirty-two conservative contact pairs remain under review. See [SOURCES_PRISON_CARPET.md](../../Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_PRISON_CARPET.md).

Six ordinary overhead lattice variants now cover all 272 saved Redux placements. Source-colored chords, closed ends and diagonal braces retain saved rotations; 240 run joins align. Six streetlight contacts and four co-located lattice joints still need refinement. See [SOURCES_REDUX_LATTICE.md](../../Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_REDUX_LATTICE.md) and the [twenty-section comparison](http://127.0.0.1:8766/viewer/?scene=../generated/redux-lattice-fixture.json).

Grey SPP windows now cover all 138 saved Redux placements, with a separate broken-frame model and 16 connection forms each. Shutter/wall contacts and breakage animation remain open. See [SOURCES_SPP_GREY_WINDOWS.md](../../Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_SPP_GREY_WINDOWS.md) and the [32-state comparison](http://127.0.0.1:8766/viewer/?scene=../generated/spp-grey-fixture.json).

Hybrisa window shutters now use original open/closed bands and a thinner source-referenced depth. 884 Redux placements (1,299 including classic) mount beside their glazing; all those glass intersections clear. The later basin pass clears the three sink contacts; sixteen conservative foliage contacts remain recorded. See [SOURCES_SHUTTER_MOUNTING.md](../../Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_SHUTTER_MOUNTING.md) and the [mounting comparison](http://127.0.0.1:8766/viewer/?scene=../generated/shutter-mount-fixture.json).

Wash basins now follow the source rim, bowl and single tap, covering 80 Redux placements. All three sink/shutter contacts clear; irregular wall/counter fits and liquid appearance remain unfinished. Seven resource states are inventoried, with no animation clips implemented. See [SOURCES_SINK_FIT.md](../../Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_SINK_FIT.md) and the [four-direction comparison](http://127.0.0.1:8766/viewer/?scene=../generated/sink-fit-fixture.json).

Rear-wall clearance now exposes 47 Redux basins and 33 classic basins while preserving their saved facing and along-wall spacing. Another 64 contact pairs clear without new pairs; counter/toilet fits and the repeated inward-facing sink remain unresolved. See [SOURCES_BASIN_WALL_MOUNT.md](../../Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_BASIN_WALL_MOUNT.md) and the [mounting comparison](http://127.0.0.1:8766/viewer/?scene=../generated/basin-wall-fixture.json).

The existing grey SPP wall draft now has connected source artwork for all 708 Redux underground placements. It corrects 205 rendered orientations, clears 759 contact pairs without adding any, and gives 11 more basins rear-wall clearance. See [SOURCES_SPP_WALL.md](../../Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_SPP_WALL.md) and the [updated Redux underground scene](http://127.0.0.1:8766/viewer/?scene=../generated/spp-wall-redux-minus2-scene.json). Model coverage stays at 778 drafts.

Both mirror forms now use original silhouettes and face artwork with solid backing. All 28 Redux mirrors and 17 classic mirrors are fitted; eleven rendered facings are corrected and all 46 previous contact pairs clear. See [SOURCES_MIRROR_FIT.md](../../Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_MIRROR_FIT.md) and the [mounting comparison](http://127.0.0.1:8766/viewer/?scene=../generated/mirror-fit-fixture.json). Reflections and damaged appearances remain unfinished.

States and animations are incomplete: 33 library models contain 44 clips, selected door/fold/directional/random appearances have static variants, and six reagent carts follow supported source Fill layers. No model has every gameplay state verified. See [STATES_AND_ANIMATIONS.md](STATES_AND_ANIMATIONS.md) for the supported cases and remaining work.

The native preview now retains complete packed references for captured geometry. Its final 1,036-view/pose audit has zero omissions, including cells with 449 parts. This does not establish live frame rate or complete future-state coverage. See [PACKED_SCENE_REFERENCES.md](PACKED_SCENE_REFERENCES.md).

Six source-shaped reagent carts cover all 42 saved placements; the [Fill appearance study](http://127.0.0.1:8766/viewer/reagent-tank.html) exposes original layer composition and explicit color fixtures. Twelve door-mounted shutters have corrected depth offsets. The ten prison-window compound corrections are installed and checked offline.

The regular game renderer remains the default; first-person rendering is an opt-in debug viewport. The authored assets are drafts. Source references alone do not change live entity appearances.

## Review an assembled Garrison map

From the repository root, using the Python environment with `requirements.txt` installed:

```powershell
python Tools/three_d/serve.py --prepare --open
```

This exports the current models and saved Garrison Redux surface scene (`generated/redux-scene.json`), then serves the local viewer at `http://127.0.0.1:8766/viewer/`. Subsequent launches can omit `--prepare` until a map or asset changes. The server binds only to loopback and serves the viewer, generated review assets, and reference textures. Stop it with Ctrl+C.

The viewer uses a real WebGL depth buffer and instances the same solid geometry used by GLB export. Drag to orbit, right-drag to pan and scroll to zoom. With the scene focused, arrow keys pan, Shift+arrows pan faster, +/- zoom and H returns home. Click geometry to inspect the saved entity and compare its source sprite. Exact draft mappings, inherited candidates, and unmapped markers are distinguished. Inherited matches remain proposals, and markers have no claimed source silhouette. The scene contains saved map placement, not live simulation, lighting, visibility, or dynamic appearance. It is for art review; its full-map information must never become a normal player's visibility source.

The default Redux surface export contains 58,937 visible saved entities and 81,361 visible floor tiles. The classic comparison snapshot (`generated/scene.json`) contains 46,561 visible entities and 65,340 floor tiles. Hidden container contents are excluded and sprite-less elevator shafts remain open. These are static export counts, not the number rendered at once. The viewer crops to a selectable region and reports geometry omitted by its 160,000-part budget. It draws only when the view changes.

The browser uses original tile texture variants, including their allowed rotation/mirroring, to preserve floor markings and room materials. Inherited candidates are off by default. Authored tabletop footprints support small props without changing saved coordinates: 804 props currently rest on exact table, rack or pallet models, while 78 have no exact modeled support and remain at floor height, including floor-standing plants. The inspector reports the support condition. Native and region-GLB floors still use sampled colors.

Placement now distinguishes single-frame `noRot` sprites from directional RSI frames. Fixed props retain their source facing; directional chairs retain their facing, and single-frame `snapCardinals` sprites use the engine's residual angle. Connected fence/window panels follow anchored neighbours on the same grid, including unmapped neighbours and those outside an export crop. Isolated panels use an unambiguous adjacent-wall axis. The current scene contains 758 connected panels (15 aligned to wall openings) and 630 adjusted facings. Source comparisons select the saved direction and connected state. Another 62 reinforced/brown tables join their tops to same-key anchored neighbours; internal edge trim is omitted and small props use the resolved surface footprint. Isolated table edges remain inset and do not align to walls. Fixtures sharing a wall tile mount outside it; those on a room tile facing an adjacent wall mount inside that tile's boundary. Fractional fixture positions retain their spacing along the wall while mounting depth snaps to the tile face (61 corrections). Wall-mounted fixtures follow cutaways so they do not float above removed walls. Overwatch uses an explicit east/west source-frame correction. Overhead elbows use an explicit South/East/North/West cardinal permutation so their nonstandard source frames join the correct pipe runs. Directionless drink dispensers can face away from adjacent walls, and monitor banks use their explicitly listed workstation families to choose the visible wall side. Ambiguous contexts retain the saved facing. These rules change presentation only.

To inspect another configured level, generate a separate scene and use the viewer's `scene` query parameter:

```powershell
python Tools/three_d/scene.py --variant redux --level 1 --output Tools/three_d/generated/redux-level-1.json
```

Open `http://127.0.0.1:8766/viewer/?scene=../generated/redux-level-1.json`. Each export contains one selected map level. Z identifies that level; it is not a physical building-floor height for stacking multiple levels together. Use `--region MIN_X MIN_Y MAX_X MAX_Y` to crop before export. The scene diagnostics retain unresolved prototypes, unapplied state overrides, and any transform, tile or material errors.

Export an assembled region for a 3D editor:

```powershell
python Tools/three_d/export_scene.py
python Tools/three_d/export_scene.py --center 10 20 0 --radius 12 --include-inherited
```

The default input is `generated/redux-scene.json`; output is `generated/garrison-region.glb`, centered on the scene's suggested review location with a 20-tile radius. Use `--scene Tools/three_d/generated/scene.json` to export the retained classic comparison. Its adjacent JSON report lists included and omitted objects. GLB preserves editable parts, entity hierarchy, source IDs and draft status. Geometry uses the resolved presentation transform; node extras retain original saved coordinates and rotation. Unmapped entities are omitted; inherited matches are opt-in. Floor colors approximate their reference textures. Keep `SOURCES*.md` and referenced RSI attribution metadata with exported art.

The latest structural batch adds 20 assemblies covering 371 saved classic objects: HESCO and reinforced barriers, overhead pipes, fixed elevator panels, department walls/windows, prison-cell slit windows, blue boundary glazing and bone-resin walls. Source comparisons and map review refined pane depth around co-located hydroponics trays, pipe corner directions and organic crowns. See `SOURCES_STRUCTURES.md` and the `generated/structures-*-audit.json` records. All remain drafts.

The latest freight batch adds 40 source-specific sections covering 81 classic objects. Large left/middle/right sections and compact pairs retain continuous markings, physical corrugations and source-painted roof/strap details. Compact visual depth follows the source art and saved rows, including mixed CMU/RMC halves; simulation colliders are unchanged. All saved transforms are retained, with no part overlaps between separate containers. See `SOURCES_CONTAINERS.md`, `generated/containers-*-audit.json` and `generated/review/containers-*.png`. These remain drafts.

The latest machinery/debris batch adds 20 drafts covering 65 classic objects: six industrial assemblies and fourteen distinct discarded-prop arrangements. Capped cylinders preserve straight drums, ducts and bottle bodies across browser, native and GLB rendering. Context review corrected cabinet depth, source facing and debris pivots; all saved transforms are unchanged. Seven existing same-tile wall/debris overlaps are recorded separately. See `SOURCES_MACHINERY_DEBRIS.md`, `generated/machinery-debris-*-audit.json` and the comparison/map captures.

The supply batch adds 24 drafts / 25 classic types / 82 saved objects: medicine bottles, medical cartons, ammunition boxes, packets, donut boxes, gauze and two drums. Thirty exact source-crop uses share 25 new images. Static closed-cap/lid references preserve original layer composition; the two flashlight variants share identical closed artwork. Context review corrected 49 reinforced tables, keeping all 16 medicine bottles on connected tops without moving saved positions. Nine new props still lack an exact support at their pivot. See `SOURCES_SUPPLIES.md`, `generated/supplies-*-audit.json` and `generated/review/supplies-*.png`. All remain drafts.

The furniture pass adds 12 drafts covering 71 classic saved objects and revises four older models covering 55. Source-specific beds, bunk colors, hospital trolleys, gray/folded chairs, collapsed rack parts and brown tables replace generic inherited matches. Context review also corrected oversized existing beds, the plain trolley axis, and crate planks/lid. All 126 saved transforms remain unchanged. Fourteen original bedding crops preserve source patterns. See `SOURCES_FURNITURE.md`, `generated/furniture-*-audit.json` and `generated/review/furniture-*.png`. Two handcuff cartons still need a justified rack placement correction. Folded chair #1719 now rests on the separate top boards of pallet #7140; rack parts #14400 likewise rest on #7141.

Five additional drafts cover 37 objects: compact folded roller beds, both bedroll states, unlocked portable barriers and empty mop buckets. Source and room review refined folded depth to fit the separately placed shelf pairs. Two ten-object roller-bed groups retain their shared saved pivots. See `SOURCES_FOLDED_EQUIPMENT.md` and `generated/folded-equipment-*-audit.json`.

Five state-only partners subsequently complete seven folded/unfolded pairs for 134 currently mapped Foldable objects. Native and offline selection use explicit reciprocal pose links before orientation and support resolution; missing poses remain markers. Saved initial mappings and transforms are unchanged. All 14 pose cards and an isolated browser comparison are available; open `http://127.0.0.1:8766/viewer/?scene=../generated/fold-pose-review.json` for the labeled fixture. See `SOURCES_FOLD_STATES.md` and `generated/fold-states-*-audit.json`. Stable pose selection is tested locally; connected multiplayer folding remains unverified.

The remaining potted-plant pass adds 19 distinct drafts, covering 14 types / 30 classic objects and all 109 objects of those types across configured maps. Source-specific pots, foliage, flowers and stems replace missing markers; 24 classic plants use exact table supports and six stand on the floor. Hollow rims expose actual recessed soil, and two original crops retain planter decorations. Source offset/facing and all saved transforms are preserved. See `SOURCES_REMAINING_PLANTS.md`, `generated/remaining-plants-*-audit.json` and the 19 new comparison cards. Simplified leaf solids, hidden construction and animated/material appearance still need refinement.

Three HEMETT truck drafts cover six classic placements and eleven across configured maps. All four source directions informed the cab, eight wheels and distinct bare/open/covered beds. Continuous sloped windshields use `WedgeY`; fifteen exact original panel crops preserve glazing and markings. An authored half-tile horizontal pivot correction keeps saved facings and placement consistent. Wheel-track refinement clears the neighboring loading-bay crates without moving them. See `SOURCES_HEMETT.md`, `generated/hemtt-*-audit.json` and `review/hemtt-*.png`. Physical dimensions, hidden construction, ground contact and moving states remain unfinished.

Fifteen Mono-Supron compact-car drafts cover eighteen classic placements and thirty-three across configured maps. Source-specific colors, flipped facings, police/taxi details and wreck door openings use 59 original images. Forward and reverse slopes form continuous front/rear glazing. Context refinements narrowed the inferred width and corrected the depth pivot around road dividers, repair props and the purple wreck's narrow alley; every saved transform is unchanged, with zero conservative intersections against nearby modeled objects. See `SOURCES_COMPACT_CARS.md`, `generated/compact-cars-*-audit.json` and the comparison/map captures. All remain drafts with inferred physical depth and hidden construction.

Eight van/ambulance drafts cover eleven classic placements and eighteen across configured maps. Separate medical compartments and cab-over van bodies retain original panels, roof details, hazard stripes and medical markings. Comparison refinements fix duplicate fan imagery, mirror mounts and wheel colors. Explicit facing includes the east-facing blue-gray van, and horizontal offsets account for the occupied portion of the padded 128-pixel frame. All saved transforms are retained, with no conservative intersections against nearby modeled objects. See `SOURCES_VANS.md`, `generated/vans-*-audit.json` and `review/vans-*.png`. The maintenance fan remains a static first frame; physical depth and hidden construction are inferred.

Six small-truck drafts cover six classic placements: strapped cargo, a separate red/blue barrel load, two garbage-body facings and green/red empty beds. Context review also corrected all 64 red/blue plastic road barriers to their source tile-edge fixture, shorter height and original reflectors. The barrel truck now clears the barriers. One garbage truck retains an original saved-collider overlap with an inherited crate candidate; that exception is recorded explicitly. See `SOURCES_SMALL_TRUCKS.md`, `generated/small-trucks-*`, `generated/road-barriers-*` and the source/map comparison images. All remain drafts.

Three five-axle long-truck drafts cover three classic placements and six across configured maps. Brown, Donk and mining bodies retain original panels, raised corrugations and ten cylindrical wheels. The logo/identifiers read normally on both sides. Source framing and the collider's depth midpoint determine explicit render offsets; all saved transforms remain unchanged and nearby modeled objects have no conservative intersections. See `SOURCES_LONG_TRUCKS.md`, `generated/long-trucks-*` and `review/long-trucks-*`. Hidden construction and physical dimensions remain inferred.

Four heavy-loader drafts cover five classic placements: two strapped cases, a green wrapped load, an east-facing empty bed and the security roof-equipment variant. Comparison refined the front guard, windshield, wheel track and mirror reach. Context review also corrected all 303 road-center dividers to their south-edge fixture, paired thin frames and central mounting stem. All five trucks and all 303 dividers clear neighboring modeled objects without changing saved transforms. See `SOURCES_LOADER_TRUCKS.md`, `generated/loader-trucks-*`, `generated/center-road-*` and their review images. The security indicators remain a static first frame; physical dimensions and hidden details remain inferred.

Three crane drafts and the Kelland mining van cover the final seven placements in the inspected vehicle inventory. Open grated decks, crawler rollers, source-specific cargo and van panels retain their designs. Explicit repeated source facings correct the S/W and N/E aliases; all saved transforms remain unchanged. Only two cargo cranes at an original duplicate pivot overlap. See `SOURCES_CRANES.md`, `generated/cranes-*` and `review/cranes-*`. All 43 inspected vehicle types now have drafts; hidden geometry, fidelity and dynamic states remain unfinished.

Eleven stemmed-shrub drafts cover twelve prototypes and 37 classic placements. Source-specific brown forks, green rosettes, orange seed stalks and sparse gray branches use original palettes. Root recentering corrects a source-row/world-depth mistake without changing map transforms. The later platform correction clears one of the two confirmed planter-border pairs; #9782/#12904 still has eleven intersections with subdivided source-colored parts. See `SOURCES_STEMMED_SHRUBS.md`, `generated/stemmed-shrubs-*` and their comparison/context captures.

Four fern/broad-leaf sprig drafts add six classic placements. Refined crowns, source green patches and connected leaf bases preserve the distinct original arrangements. Saved transforms remain unchanged, with no wall or planter-border bound contacts. See `SOURCES_FOLIAGE_SPRIGS.md` and `generated/foliage-sprigs-*` for comparisons and remaining foliage/shoreline contacts.

Nine A/B/C olive-bush drafts add 25 classic placements. Each preserves its inspected upright, woody or drooping construction and separate root groups. Source-pixel refinement clears initial wall/border contacts without changing saved transforms; two grass contacts remain. See `SOURCES_LOW_BUSHES.md` and `generated/low-bushes-*`. The subsequent metal-platform correction addresses the older platform-source mismatch.

Nine existing Hybrisa metal-platform drafts are rebuilt as two-foot beams, open grilles, recessed ridged panels, mirrored stepped ends and compact corner returns. The correction covers 1,842 classic placements without changing saved transforms or adding offsets. Source palette/profile checks pass, and fixture-based cap refinement removes all newly introduced contacts. One earlier shrub-border pair remains unresolved. See `SOURCES_HYBRISA_PLATFORMS.md`, `generated/hybrisa-platform-*` and their source/context comparisons. That correction added no new models; physical height, hidden construction and raised terrain remain unfinished.

Nine I/K/L/M bush drafts add 13 classic placements: reed clumps, branching crowns, rounded foliage and conifers. Source comparison refined leaf width, depth, density and needle shape. All saved transforms and empty fixture overrides are retained; single-frame `noRot` controls facing. Three initial border/stair contacts are cleared, but one conifer/border intersection remains alongside an older shrub/border pair. Both occupy bounded soil patches requiring a terrain-elevation investigation. See `SOURCES_REMAINING_BUSHES.md`, `generated/remaining-bushes-*` and `generated/planter-elevation-followup.json`. All remain simplified drafts.

Three large jungle-bush drafts add twelve classic placements with distinct source-traced curling leaves. Successive comparisons refined joins, stalk thickness and inner foliage. The original horizontal offset and source facing are retained; five inferred leaf depths now clear two walls without moving roots or saved entities. Fourteen conservative foliage contacts remain. Source outlines and side views are still approximate. See `SOURCES_JUNGLE_BUSHES.md`, `generated/jungle-bushes-*` and the source/map comparisons. The two unmapped upstream tree prototypes use unknown saved RandomSprite variants and are not counted as default-state coverage.

## Inspect the current assets

While connected to a server, open the client console and run:

```text
cmu_3d
cmu_3d CMU3DFusionGenerator
cmu_3d off
```

Drag to orbit, scroll to zoom, and use the cardinal views to compare a model with its source prototype icon. Searchable selectors expose all assets and their reference IDs. The marine is a scale mannequin and the rifle is an unrigged prop.

- Editable world definitions: `Content.CMU/Resources/ThreeD/Prototypes/World/*.yml`.
- Equipment authoring definitions: `Content.CMU/Resources/ThreeD/Prototypes/Equipment/*.yml`.
- These presentation libraries are outside the normal prototype startup directory. Open 3D views share world geometry; only the model workbench loads equipment drafts. Closing the last view unloads model instances and parsed YAML and releases scene caches. Closing only the workbench unloads equipment while another 3D view can keep using world geometry. Reopening after release loads the required library again and can pause while loading. Memory becomes eligible for normal garbage collection; toggles do not force a collection. The server and clients that stay in 2D never load these libraries. Map elevation profiles and the shader declaration remain under `Prototypes/CMU14/ThreeD`.
- Portable models: `Content.CMU/Resources/Models/CMU14/Garrison/*.glb`.
- Attributions and modeling assumptions: `Content.CMU/Resources/Models/CMU14/Garrison/SOURCES*.md`.
- Contact sheet: `Tools/three_d/generated/review/overview.png`.
- Per-model references and four views: `Tools/three_d/generated/review/<model ID>.png`.
- Full work queue: `Tools/three_d/generated/coverage.json` and `coverage.md`.

## Inspect a live scene in the game

While connected with an active debug administrator and a controlled entity on a map, run `cmu_3d_live`. Use `cmu_3d_live off` to close it. Left-drag orbits, right-drag pans, the wheel zooms, and clicking an entity selects its model reference. The asset-workbench button opens the corresponding sprite/model comparison.

This scene follows the controlled entity and samples nearby live transforms and floor tiles ten times per second. Exact authored models are enabled; inherited candidates are opt-in. Teal markers identify missing models. Stable door and fold states select their explicit authored assemblies; amber markers replace unsupported transitions or missing poses. Lower walls reveals the interior for inspection. Floor colors are sampled from 461 source tile types (536 variants).

The native renderer finds the nearest solid surface using a GPU spatial grid with oriented box, ellipsoid, capped-cylinder and sloped-prism intersections. It admits at most 256 entities and 8,192 parts within six tiles of the actor, and reports omissions. Geometry is cached between changes. Original PNG surfaces support alpha cutouts; retained fractional source alpha and glass use the same 4-by-4 screen-door coverage as browser/review images. This is a bounded debug workbench, not a performance-qualified gameplay viewport.

It intentionally requires debug permission: replicated entities can include objects behind walls, and player fog, lighting and blindness are not applied yet. Revoking permission or losing the controlled actor closes and clears the scene. Changing actor or map replaces the geometry and resets the camera. The adapter does not read the saved-map survey.

## Regenerate and check

Use Python 3.11 or newer. Commands run from the repository root. The scripts accept explicit paths and do not require Blender.

```powershell
python -m pip install -r Tools/three_d/requirements.txt
python Tools/three_d/inventory.py
python Tools/three_d/build_models.py
python Tools/three_d/coverage.py
python Tools/three_d/build_tile_materials.py
python Tools/three_d/scene.py
python Tools/three_d/export_scene.py
python Tools/three_d/build_models.py --check
python Tools/three_d/build_tile_materials.py --check
python Tools/three_d/button_animation_review.py
node --test Tools/three_d/viewer/*.test.mjs
```

Run the Python regression suite from its tool directory so sibling modules are importable:

```powershell
cd Tools/three_d
python barricade_state_review.py
python -m unittest discover -s tests -v
```

Inventory scans the configured classic and Redux map paths, then resolves entity inheritance and sprite resource references. Refresh it when maps or prototypes change. It preserves resource state/license information and reports missing parents, missing files, and stale map entity-count headers. `build_models.py --check` verifies GLB, manifest, browser geometry and source-frame reproducibility; it does not certify visual similarity. Removed models are cleaned up only when their previous manifest owns the unchanged export; manually edited or unrelated files are preserved.

For an independent glTF check, install the Khronos `gltf-validator@2.0.0-dev.3.10` npm package in a tooling directory, then run:

```powershell
node Tools/three_d/validate_glb.cjs <path-to-gltf-validator-package>
node Tools/three_d/validate_glb.cjs <path-to-gltf-validator-package> Tools/three_d/generated Tools/three_d/generated/scene-glb-validation.json
```

This produces `generated/glb-validation.json`. Its zero errors/warnings refer to the GLB format, not art quality. The exporter follows the [glTF 2.0 specification](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html).

Normal repository validation:

```powershell
dotnet build Content.Shared/Content.Shared.csproj --no-restore
dotnet build Content.Client/Content.Client.csproj --no-restore
dotnet test Content.Tests/Content.Tests.csproj --no-restore --filter 'FullyQualifiedName~Content.Tests.Client.CMU14.ThreeD'
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~CMU14.ThreeD'
```

Run these sequentially because the projects share build outputs. Integration fixtures check the actual client/server resource loader, source prototype references, selecting every asset, and live-scene permission revocation, containers, map changes and actor loss. Headless tests do not verify native GPU output or interactive frame rate.

After building the client, a separate native shader check uses the built engine's parser and GLSL generator, then a hidden SDL OpenGL context:

```powershell
python Tools/three_d/validate_native_shader.py
```

It requires PowerShell 7 running a compatible .NET version, the client's SDL3 library and an OpenGL 3.3 driver. It compiles and links both uniform-buffer/native-sRGB configurations, then renders a rotated ellipsoid in front of a box from the actual C# encoder. Pixel checks compare center, background, empty-region and curved-corner-gap samples with the C# camera and picker, plus asymmetric printed colors, alpha gaps, mirrored wall mounting and cutaway UVs. Capped-cylinder fixtures cover all three axes, both end caps, the side surface and nonzero yaw. It does not establish full-window presentation, gameplay visibility or frame-time performance. See `STATUS.md` for the latest completed checks and outstanding blockers.

## Asset contract

`cmu3DModel` prototypes define an ID, label, description, review status, exact `sourcePrototypes`, and colored `parts` with optional labels, required min/max bounds and optional `shape: Ellipsoid`, `CylinderX`, `CylinderY`, `CylinderZ`, `WedgeY`, `WedgeYReverse`, `SlantedX`, `SlantedXReverse`, `SlantedY` or `SlantedYReverse` (default `Box`). X/Y are horizontal, Z is up, front is -Y, and one unit equals one map tile. The GLB exporter converts this to right-handed Y-up coordinates `(x, z, -y)` and converts sRGB paint values to linear glTF material factors.

Ellipsoids are inscribed in their min/max bounds. The browser and GLB use the same closed 224-triangle sphere mesh, with smooth inverse-scale normals; the native renderer uses analytic intersections. Comparison images render the curved geometry. Capped cylinders use the named local axis and elliptical bounds, with a closed 16-segment / 64-triangle mesh, flat end-cap normals and smooth inverse-scale side normals. Native intersections clip the radial quadratic to both cap planes; packing and picking retain all three axis choices.

Parts may specify local `yaw` (degrees, ±360) and `pitch` (degrees, ±90), applied about their own center. Pitch lifts +X toward +Z before yaw rotates +X toward +Y. Their centers follow the entity rotation once. Bounds, picking and exported node orientations use the same convention. Rotated connected/wall-mounted geometry and rotated support surfaces are unsupported; nonzero pitch also requires an untextured part. Omitted angles retain the previous behavior.

Part volumes must be finite and strictly positive; at most 128 parts are accepted by the exporter. The initial assets use flat materials and individually named nodes, so they remain editable after export. Printed surfaces use original PNG artwork with planar UVs; skinning and animation remain unfinished. The review rasterizer uses a depth buffer. Review-sheet source images show a first available frame, not a complete live layered sprite. Browser/native/review images multiply source and paint alpha for screen-door coverage. Source pixels below alpha 128 are cutouts; retained partial-alpha surfaces remain selectable, while fully clear paint passes picking through. GLBs use BLEND for fractional materials and MASK for binary PNG cutouts; PNG data remains unchanged.

`cmu3DSurface` resources define a PNG `texture` and unique `atlasIndex` in 1..4095. Images are at most 256 by 256 pixels and packed without resampling. Native and browser atlases choose the smallest power-of-two cell that fits the loaded images. The current 484 images use 64-pixel cells and 64 columns in a 4096 by 512 atlas (8 MiB RGBA). The information texture is 4096 by 1. Only occupied rows are allocated. A Box, WedgeY or WedgeYReverse part can reference `surface` and `surfaceAxis: XZ` (front), `XY` (floor) or `YZ` (side). Source alpha below 128 is a cutout in the browser, review sheets and native renderer/picker. GLBs use MASK for binary cutouts and BLEND for retained fractional alpha. PNGs are embedded in portable GLBs; original source attribution must accompany them. Room-side fixture variants reverse U to preserve reading direction; cutaways retain the lower image portion. Connected untextured support models extend the part named by `supportSurface` to each joined tile edge; `omitWhenConnected` uses north/south/east/west bits 1/2/4/8 for internal edge trim. Curved or automatically connected textured parts are rejected until their UV transforms are implemented. The current 168 textured drafts include signs, flags, carpets, freight containers, supplies, bedding, planter details, water/shorelines, trucks, compact cars, vans/ambulances, small and long trucks, heavy loaders, cranes, mining van, road reflectors and a machinery monitor; their evidence is in `SOURCES_SURFACES.md`, `SOURCES_CONTAINERS.md`, `SOURCES_MACHINERY_DEBRIS.md`, `SOURCES_SUPPLIES.md`, `SOURCES_FURNITURE.md`, `SOURCES_REMAINING_PLANTS.md`, `SOURCES_WATER.md`, `SOURCES_HEMETT.md`, `SOURCES_COMPACT_CARS.md`, `SOURCES_VANS.md`, `SOURCES_SMALL_TRUCKS.md`, `SOURCES_LONG_TRUCKS.md`, `SOURCES_LOADER_TRUCKS.md`, `SOURCES_CRANES.md` and their generated source/placement audits. Animation, emissive lighting and physically folded cloth remain unfinished.

`SlantedX`/`SlantedY` and their `Reverse` variants are closed continuous tapered leaves: the selected horizontal coordinate of the unit sphere becomes `sqrt(1-k*k)*coordinate + k*z`, with `k=0.85` (Reverse: `-0.85`). Their 224 triangles remain inside the authored bounds. Normals use the inverse transpose; native picking/rendering invert the same transform. glTF records actual sampled bounds. They do not support source textures. The twenty short-grass drafts use these leaves, original palettes and 161 inspected source groups; all 75 classic placements preserve `noRot` and zero offsets. See `SOURCES_GRASS.md` for source comparisons, limitations and context contacts.

`WedgeY` is a closed triangular prism rising from -Y to +Y, bounded by normalized `z <= y`. `WedgeYReverse` mirrors it across local Y, bounded by `z <= -y`. Their 18 vertices and eight triangles have flat outward normals; the mirror reverses triangle winding and Y normals. Native CPU/GPU intersection clips the oriented box against the slope; browser, GLB and review images use the same solid.

Small props can declare `placement: surface`. Furniture declares `supportSurface` as the unique label of its actual tabletop part, or `supportSurfaces` as a list of unique flat part labels for separate boards. The declarations are alternatives; multipart supports cannot yet use connected geometry. Every board retains its own footprint, so gaps remain unsupported. Placement uses that rotated part's XY footprint and top Z; it chooses the nearest exact support, with entity ID as the deterministic tie-breaker, then the highest containing part within that same support entity. Missing or inherited-only supports do not invent a floating height. Browser, region GLB and native live adapter apply the offset only to presentation geometry. Wall fixtures and directional barriers use authored offsets matching their mounting face or collision edge.

`sourceDirections` records the RSI direction count; it is essential because `noRot` still changes a directional sprite's selected frame. `yawOffset` specifies an explicit model-axis correction in degrees. `swapEastWest` corrects inspected four-direction sources whose side frames are reversed. `sourceCardinalFacings` optionally supplies four physical quarter turns 0–3 indexed by South/East/North/West source direction (not RSI sheet order). It requires four source directions and preserves residual rotation. Entries may repeat for source aliases: cranes and the Kelland van use `[0, 2, 2, 0]`. The native reference selector chooses the nearest physical view and prefers the requested source slot on ties; a permutation still selects its inverse. The overhead elbow uses `[0, 1, 3, 2]` to match its inspected source bends. `useEntityRotation` preserves the physical axis of directional fixtures such as curtains whose single-frame source always faces the 2D camera. `faceAwayFromWall` lets explicitly authored counter appliances infer their accessible front from adjacent walls. `wallFacingTargets` lists workstation prototypes that can select a display's visible wall side when adjacent targets agree on one direction. Paired `CMDoubleDoor` modules are one tile wide, with a 90-degree correction so their span follows the partner direction. `connectToNeighbours` uses the source `IconSmooth` key/additional keys to create grid-aligned straight, corner, T and cross panels. `wallMounted` applies wall cutaways to fixtures. The original 124-model source audit is in `generated/layout-audit.json`; new interior references are in `generated/interior-source-audit.json`, with separate plant attribution in `SOURCES_PLANTS.md`. Sprite screen offsets are not copied into world XY: tree offsets encode projected height, and RMC light offsets compensate for padded directional artwork. Physical model pivots and wall-face offsets are authored separately. `groundOffset` is an explicit two-coordinate correction in map axes, independent of model facing. It applies to rendered geometry and support footprints without mutating saved transforms; it is not an automatic copy of Sprite.Offset.

`doorState` and reciprocal `alternateDoorModel` links represent stable Open/Closed assemblies. Both reviewers choose the actual state before layout; transitional poses use small markers. `referencePrototype`, `referenceRsi`, `referenceState` and `referenceTint` provide an explicit pose comparison, including state-only partners without duplicate source mappings. `bakedSpriteTint` records source Sprite.Color already included in the part colors; native live tinting applies only the relative change. Zero baked channels cannot reconstruct colors that were removed during authoring.

`folded` declares the model's stable Foldable pose (default false). `alternateFoldModel` must be reciprocal, with the opposite pose and explicit RSI/state references on both partners. Native selection reads the live Foldable component; saved-map selection combines prototype defaults and saved overrides. The selected pose controls direction, placement and comparison reference, while preserving exact/inherited provenance. Missing partners remain unsupported-state markers. State-only partners use `referencePrototype` and an empty `sourcePrototypes` list, so alternate poses do not inflate exact prototype coverage.

`directionalModels` links direction-specific assemblies in RSI order (S, N, E, W, SE, SW, NE, NW; four-direction sources use four slots). Every member keeps the same reciprocal family and explicit RSI/state, with `referenceDirection` identifying its slot. Native and saved-map selection use entity yaw before layout; the free camera does not select geometry. Empty or invalid slots remain unsupported. A fixed `referenceDirection` also selects the correct source frame for native/browser/export review. Only base `sourcePrototypes` mappings count toward exact prototype coverage.

Set `status: reviewed` only after checking the intended source directions, inferred side/back surfaces, scale, silhouette, colors, and necessary states. Status is authored evidence, never an automatic similarity score. Inherited mappings remain candidates even when a parent model has been reviewed. Instance frequency does not measure completion: large repeated rock borders dominate this map's entity count.

## Remaining work

The saved maps contain 1,650 visual entity prototypes in 762 sprite resource groups. The 791 draft models reference 793 visual prototypes exactly across all configured maps; 119 more inherit possible matches. The other 738 have no candidate yet. These are pending art references, not completed gameplay coverage. Vendors, loadouts, construction, threat spawning, worn layers, carried items, and animation extend the scope beyond those counts.

Initial environment and utility kits cover rocks, walls, platforms/catwalks, barricades, windows/fences, lights, vegetation, solar equipment, conveyors, medical equipment, cabinets, phones and consoles. They still need visual review in adjacent runs, remaining variants, and state handling. Characters need reusable bodies, clothing attachments, rigging, and actions.

The debug live adapter now supplies moving transforms, floors, solid depth, inspection picking and wall cutaways. A gameplay renderer still needs actor visibility, lighting/blindness, animated appearance, transitional/damaged states, roof rules, gameplay input delegation, sprite fallbacks, transparent surfaces and performance validation. The static tactical survey includes unseen structures and must not become the player's visibility source. See `Content.CMU/Client/ThreeD/README.md` for the client architecture boundary.


## Source states and door-control animation (2026-09-25)

Run `python Tools/three_d/state_inventory.py` to regenerate the source-state inventory for all current models and 1,551 Redux visual prototype types. It records layers, visualizer rules, directions and frame timings. The 809 animated resource states include unused art; runtime coverage and every-model state completion remain unfinished.

The two door-control library models now have 44 source-frame/power compositions and four GLB animation clips. Their native administrative preview adapter selects geometry from the existing sprite animation/power layers. The regular gameplay viewport is unchanged, and interactive native playback still needs verification. Saved-map exports explicitly show only the default powered pose.

Run `python Tools/three_d/button_animation_review.py` to regenerate comparison exports using the authored library frame geometry. Open `/viewer/button-animation.html` to rotate the controls, compare source frames, scrub press/denied playback and inspect power overlays. Its four clips repeat the library sequences rather than adding new ones. Timing follows the source owner's 1.25-second completion and .5-second strip restart.

All 111 placed controls are refreshed, with 47 rear-wall clearances. Thirty-four old contact pairs clear, but five adjacent red-control contacts are introduced at the original case width; 34 pairs remain overall. Source silhouettes/colors match, while depth, rear construction, mechanical articulation and emission remain unfinished. See `generated/button-state-verification.json` and `SOURCES_DOOR_BUTTON_STATES.md` for evidence and limits.

Current verification: 169 Python, 28 JavaScript and 215 isolated native checks pass; the client builds with zero errors and 2,150 warnings. All 778 model GLBs, 540 assembled GLBs and two comparison GLBs validate with zero errors/warnings. Browser comparison/playback is checked; this does not establish live gameplay correctness or complete animation coverage.

## Cargo-crane tracks (2026-09-25)

Nine rail, junction and end-cap models now cover 105 Redux placements. All 104 joined edges align, with no modeled-neighbor contacts in the same-level check. One original uncapped end remains as placed. Source contours/colors match; physical section, height, runtime fade and native review remain draft. Open `/viewer/?scene=../generated/crane-track-fixture.json` for the nine forms at four rotations. `Focus this object` now centers on rendered geometry while retaining the saved floor. See `generated/crane-track-verification.json` and `SOURCES_CRANE_TRACKS.md`.

Historical crane-track checkpoint: 787 draft models, 744 exact Redux visual types, 114 inherited candidates and 693 unmapped types. Four prior door-control animation clips remain; this batch adds no clips. All 787 model and 546 region GLBs validate; 33 JavaScript checks and the native model-budget check pass. Full gameplay conversion and state coverage remain unfinished.

## Structural lattice frames (2026-09-25)

The short/tall charcoal frames add 121 Redux and two classic placements, bringing the library to 789 drafts. Original top/front artwork is retained on solid members, with inferred sides and rear. Two-pixel member depth reduces projection mismatch; 44 extra silhouette pixels remain in each source view. All 235 recorded contacts are co-located floor/edge/water/railing or duplicate-frame placements; terrain elevation and attachment fitting remain unfinished. See `generated/structural-frame-verification.json`, `SOURCES_STRUCTURAL_FRAMES.md` and `/viewer/?scene=../generated/structural-frame-fixture.json`. All 789 library and 559 assembled GLBs validate; the native budget test passes. No clips were added. Redux has 746 exact draft visual types, 114 inherited candidates and 691 unmapped types.

## Reinforced plasteel barricades (2026-09-25)

One model covers all 56 Redux +1 placements with four paired damage poses (four static GLB scenes, no new clips). Source front/rear artwork is retained on solid members; physical dimensions and hidden construction remain inferred. Cap refinement clears nine contacts; 19 wall-trim/catwalk contacts remain. Wire, acid, other overlays and native interaction review remain unfinished. See `PLASTEEL_STATES.md`, `generated/plasteel-verification.json` and `/viewer/?scene=../generated/plasteel-redux-plus1-scene.json`. All 790 library and 568 assembled GLBs validate; 231 native, 174 Python and 35 JavaScript tests pass, and the client builds with zero errors. The library has 790 drafts, 747 exact Redux visual types, 114 inherited candidates and 690 unmapped types. None is fidelity-approved.

## Strata grates and barricade seating (2026-09-25)

All 22 Strata grates on Redux +1 now use an exact source-specific draft with 43 physical members, original top pixels and 36 through-openings covering 100 source pixels. Its walking face is flush with floor-standing objects, clearing five barricade contacts. Fourteen separate barricade/wall contacts remain. All 791 library and 569 assembled GLBs validate, and the native budget test passes. See `generated/strata-grate-verification.json`, `generated/strata-grate-context.png`, `SOURCES_STRATA_GRATE.md` and `/viewer/?scene=../generated/strata-grate-redux-plus1-scene.json`. No animation clips were added; destruction, native visual review and inferred depth/underside fidelity remain open.

## Prison hull walls and connected artwork (2026-09-25)

The existing prison hull model now uses the original grey/red palette and all 32 source corner slots, with four recessed armored sides inside the source tile footprint. All 4,223 saved walls are verified, including 3,049 on Redux; 802 artwork orientations now follow the grid while saved transforms remain unchanged. Thirty-four fixtures adjust to the new wall face. All 14 remaining recorded plasteel/wall contacts clear, with no new contacts in the targeted audit. Full-map contacts, inferred side elevations and native visual review remain open. All 791 model and 716 scene GLBs validate; the native budget test passes. See `generated/prison-hull-verification.json`, `generated/prison-hull-joints-comparison.png`, `SOURCES_PRISON_HULL.md` and `/viewer/?scene=../generated/prison-hull-redux-plus1-scene.json`. No models or animation clips were added. Redux surface/classic scene defaults and upper-level review scenes are refreshed.

## Reinforced plasteel wire state (2026-09-25)

Four dry damage poses now have four wired counterparts, with 23 added strand/barb/tie members. The native administrative preview follows the original barbWired layer for installation/cutting alongside the original damage layers. Eight static scenes add no animation clips. The 32-pose fixture, sixteen source compositions and 56 actual potential wire placements are checked; no wire contacts occur against 1,573 same-level mapped-neighbor pairs. All 791 model / 717 scene GLBs validate. Native 243, Python 176 and browser 36 tests pass; client builds with zero errors. See `PLASTEEL_STATES.md`, `generated/plasteel-wire-verification.json` and `/viewer/?scene=../generated/plasteel-wire-fixture.json`. Native interactions, inferred wire depth/hidden bends, acid animation and full fidelity remain open.

## Reinforced plasteel bubbling acid (2026-09-25)

Five source frames now have geometry in all eight damage/wire contexts. Eight acid clips repeat one 0.5-second source loop; the game owns effect expiry and water removal. The native administrative preview follows the original acided layer. The library has 791 drafts and 12 clips across three models, with no full-state/fidelity approvals. The 192-pose fixture verifies 18,000 part transforms; 280 potential acid frames clear nearby modeled geometry at all 56 saved placements. Build and tests pass; 791 model / 718 scene GLBs validate. Physical depth, front/rear height interpretation, side fidelity and native interaction remain open. See `PLASTEEL_STATES.md`, `generated/plasteel-acid-verification.json` and `/viewer/barricade-acid.html`.

## Small wall bulb states (2026-09-25)

Four exact bulb assemblies now provide five states each; native preview follows the original powered-light Base layer and source-owned blinking. All 325 saved small lamps have exact drafts, with 310 explicit room-side clearances. All five poses clear nearby modeled geometry at these locations. The library has 794 drafts and 12 clips; no full-state or fidelity approvals. Review `/viewer/light-states.html`, `generated/small-light-comparison.png`, and `generated/small-light-verification.json`. Actual illumination, native interactions and physical fidelity remain open.

## Colony and ultra window states (2026-09-25)

Three new drafts add colony intact/broken frames and ultra directional glazing. All 55 saved windows and 22 dependent shutters are refreshed. The fixed-scale comparison covers 32 colony connection forms and four ultra facings; these static source states add no animation clips. Colony contact checks pass; 33 ultra wall/curtain/corner contact records remain open. The library has 797 drafts with no full-state or fidelity approvals. Review `generated/redux-window-comparison.png`, `generated/redux-window-verification.json`, and `/viewer/?scene=../generated/redux-window-fixture.json`.

## Ultra window and curtain fitting (2026-09-25)

Explicit end fitting and inside-glazing mounting now refine 33 ultra windows and both poses of all 46 shower curtains. The final audit clears 125 contact records without introducing any; all ultra windows are clear and 18 curtain records remain. Five scenes and 59 region exports are refreshed. Review `ULTRA_WINDOW_FITTING.md`, `generated/ultra-fit-context-comparison.png`, and `/viewer/?scene=../generated/ultra-fit-curtain-fixture.json`. All 797 models remain drafts; transition animation and native interaction review are unfinished.


Shower curtain opening follow-up: see `CURTAIN_OPENINGS.md` and
`generated/curtain-opening-verification.json`. Two blocked Redux openings now
follow their co-located shower facing; side glass and textured wire posts define
curtain ends. Four wall-light contact records remain. Both stable poses are
available in `/viewer/?scene=../generated/curtain-opening-curtain-fixture.json`;
cloth transitions and native interactions remain unfinished.


Fluorescent tube update: `/viewer/light-states.html` now includes single, double
and blue-double tubes, each with five source states. Review
`generated/tube-light-comparison.png`, `generated/tube-light-verification.json`
and `SOURCES_TUBE_LIGHTS.md` in the model directory. All 1,013 Redux and 482
classic tube placements have exact drafts. Native interactions, illumination
and 12 remaining contact pairs are unfinished; no clips were added.


Curtain fabric update: 12 source-patterned stable poses across six families,
including newly mapped green fabric curtains. Review
`generated/curtain-form-comparison.png`, `generated/curtain-form-verification.json`
and `/viewer/?scene=../generated/curtain-form-fixture.json`. All 134 checked
poses clear modeled neighbors; six saved Closing curtains, cloth motion and
native interaction remain unfinished. Library: 800 drafts, 12 existing clips.

## Overhead cabinet source animation

24 directional cabinet drafts now retain source frames, flipped offsets and all 43 saved placements. See [SPRITE_STATES.md](SPRITE_STATES.md), [source notes](../../Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_OVERHEAD_MACHINERY.md), and the [animation comparison](http://127.0.0.1:8766/viewer/sprite-animation.html). Native frame selection and offline exports are verified; live gameplay remains unverified. The later Platform Three follow-up admits both watched platforms but still fails strict neighbor preservation: seven other omission occurrences appear. Both reports remain available. Keep the game and server closed.

## Platform Three follow-up

The straight platform now uses six source-textured volumes instead of 37, with all 2,165 saved placements preserved. See [source notes](../../Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_PLATFORM_THREE.md), [source comparison](generated/review/platform-three/source-facing-overview.png), and `generated/platform-three-native-budget-audit.json`. Across 944 offline encoder cases, both watched platforms now fit. Strict neighbor preservation still fails: 42 appearances recover, but seven others become omitted in three views. This remains follow-up work alongside the draft-fidelity limitations. Keep the game and server closed.

## Wide machinery and corner follow-up

Four new wide machinery drafts cover 12 Redux placements and retain all 28 source ON/OFF frames. Corner platforms now use 22 parts instead of 73, preserving all 443 saved placements and their solid/color boundaries. All 864 library GLBs and 30 assembled fixtures validate. Offline native checks admit every sampled wide machine and corner without new omissions; six known surface omissions remain. See [wide source notes](../../Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_WIDE_MACHINERY.md), [corner notes](../../Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_PLATFORM_THREE_CORNER.md), and `generated/wide-native-budget-audit.json`. All models remain drafts. Keep the game and server closed.

## Desk-terminal and shutter checkpoint

The library now has 867 drafts. Three CRT-style terminals cover six Redux desk placements. Window shutters retain all 14 poses using 19 closed/six open parts. All 867 model exports and 42 assembled fixtures validate; original timings and 1,488 shutter placements remain intact. Renderer acceptance still fails in dense views: the corrected 1,372-case diagnostic records both recovered and new omissions. See [current status](STATUS.md), [terminal sources](../../Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_LARGE_COMPUTERS.md), and `generated/interior-native-budget-audit.json`. Keep the game and server closed.
