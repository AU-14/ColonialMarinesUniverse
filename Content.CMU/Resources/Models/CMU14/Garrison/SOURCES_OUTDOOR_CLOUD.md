# Outdoor equipment: source-owned draft art

This batch adds 23 draft GLBs / eight exact prototype bindings associated with 29 Redux inventory records. These are inventory associations, not verified rendered placements. All assets remain `status: draft`. No engine, runtime, map, or gameplay files were changed, and nothing was published or launched.

## Deliverables and reproduction

- Canonical editable assets: `Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_outdoor_cloud.yml` and `garrison_outdoor_cloud_art.yml`
- Canonical GLBs: the 23 `CMU3D*Cloud.glb` files listed by `Tools/three_d/generated/outdoor-cloud/models/manifest.json`
- Original unmodified RGBA source crops: `Content.CMU/Resources/Textures/CMU14/ThreeD/outdoor_cloud/`, 14 surfaces at atlas indices 2518–2531
- Source versus four-orbit comparisons: `Tools/three_d/generated/review/outdoor-cloud/`
- Complete tower-state, director-direction, and gear-phase sheets: `tower-all-states.png`, `directors-all-directions.png`, and `gear-moving-studies.png` in that review directory
- Representative family sheet: `outdoor-family-comparison.png`
- Authoring/review scripts: `reference/outdoor/build_outdoor_assets.py`, `review_outdoor_assets.py`, and `check_outdoor_blender.py`

The authoring script writes only the new family YAML and derived images. All GLBs are direct products of the existing, unchanged exporter:

```sh
python reference/outdoor/build_outdoor_assets.py
python Tools/three_d/build_models.py \
  --source Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_outdoor_cloud.yml \
  --output Tools/three_d/generated/outdoor-cloud/models \
  --review-output Tools/three_d/generated/review/outdoor-cloud \
  --viewer-output Tools/three_d/generated/outdoor-cloud/viewer
python reference/outdoor/review_outdoor_assets.py
blender -b --python reference/outdoor/check_outdoor_blender.py
```

The review script compares GLB bytes to `build_models.glb_bytes()` before copying those identical bytes into the canonical model directory. It does not modify the shared exporter or global manifest.

## Family decisions and state limits

### RMCPlatformRound: nine Redux records

The actual 32×32 source is a U-shaped platform edge, not a round disc. The source prototype explicitly supplies three collision bars: south, west and east. The model has three solid raised crossmembers, separate feet and trim, and no central or rear fill. Exact original hazard stripes sit on the corresponding three surfaces. Physical through-openings remain open from both sides. Source directions are South/North/East/West; they rotate the same three-sided form, with no directional alias or invented connection adapter.

The source `platform_round` state has four directions and no animation. The prototype defines destruction/breakage behavior but no alternate round broken artwork. No broken round model, animated transition, automatic neighbour connection, or terrain-level offset is claimed. Vertical section/feet, underside and final map contacts remain inferred.

### RMCCampfire: seven Redux records

A ring of thirteen separate gray stones surrounds six charcoal pieces and four crossed, visibly cylindrical burnt logs with char seams. The ring and wood are physical parts, with no rectangular sprite proxy or enclosing disc. A single static `campfire` composition uses the existing single-visible-layer state format.

`burning` contains five 0.3-second source frames (1.5 seconds total). The inspected source frames include the campfire base as well as flame pixels; the prototype toggles this second layer via `CampfireVisuals.Lit`. The added visible layer is deliberately unsupported by the single-layer 3D adapter. Original flame/base animation, light and sound remain owned by the source sprite/game. No fire animation clip or new renderer effect is provided.

### CMGear: four Redux records

The actual `base` sprite is a narrow, edge-on brown ribbed mechanism, not a broad face-on cog icon. The model is a thin capped axial wheel with fourteen separate axial teeth. Its plain circular side plates, wheel depth and phase interpretation are inferred from an edge-on source and remain draft.

`base` has one direction. `moving` declares four directions with three 0.1-second frames per direction, but the actual PNG contains pixels only in the first six slots: South and North. All six East/West slots are fully transparent. The existing sprite-state adapter requires one direction count for every state, so `base` and `moving` cannot truthfully share it. Static base mapping is supported; the moving state remains an explicit fallback. Six unbound art-only GLBs show the two nonempty directions' phase studies, each compared with its actual source frame. They add no runtime selector and no animation clip. Empty directions are not filled with invented gear views.

### AU14CommunicationsTower / AU14CommunicationsTowerOn: one plus three Redux records

Two mapped default assemblies share the three source-owned base states:

- `static1_off`: intact mast, dark mast tips and red receiver indicator, one static pose
- `static1_broken`: long mast removed, short torn wires at the socket and separate dangling front cables, one static pose
- `static1`: intact tower, green receiver panel and the original red/green mast-tip sequence, frames lasting 2 and 2.5 seconds

Each GLB contains one `static1` source-frame clip, one static Off scene and one static Broken scene, plus its default scene. Across both files there are two clips, representing the same source cycle. There is no invented mechanical transition between Broken/Off/On. Source rods, antenna crossbar, twin forked upstands, collar, receiver housing and front conduit are solid parts; the panel and casing faces use unmodified source crops. Actual height, receiver depth, unseen rear/side construction and pipe routing remain inferred.

The parent sprite YAML starts on `static1_broken`, while `CommunicationsTowerComponent.State` defaults to Off; `AU14CommunicationsTowerOn` explicitly sets On. The existing sprite-state adapter reads the supported visible source state rather than this batch inventing a new component-to-pose selector. The exported default references are Off and On respectively. Saved initial state timing and native component updates have not been verified.

Source resin assets were fetched and inspected: `resin_growing` has twenty 0.2-second frames; `resin_idle` has 0.3, 0.3, 0.3, 0.2-second frames; `resin_final` is static. The extra resin layer stays unsupported and source-owned. Landmark states are map/editor markers and are not tower geometry.

### AU14PropTrafficDirector2 / 4 / 5: two plus one plus two Redux records

Three printed messages each have four explicit RSI-direction models. Every source slot actually shows the message front-on; the physical facings therefore alias to zero, while separate direction slots preserve the source chassis/taillight variation. Each model has a thick sign cabinet, separate orange mast and cable, stepped trailer body, real tires/hubs/fenders and inferred rear drawbeam. Only original panel and chassis crops are textured; the remainder has physical volume.

The exact inspected messages are CHECK POINT AHEAD, FOLLOW ARMY ORDERS and SLOW DOWN. Director 4's YAML suffix says “Tune to Freq 91.7am”, but its actual `traffic_4.png` says FOLLOW ARMY ORDERS. The original pixels are preserved rather than correcting or inventing lettering. The sources have four directions, no animated strips and no moving or powered visual states. Readability from the unshown rear, trailer depth and rear access panel are inferred.

## Validation evidence

`Tools/three_d/generated/outdoor-cloud/asset-validation.json` records:

- 23 draft models and eight exact bindings, associated with 29 Redux inventory records
- 14 source crops whose RGBA pixels exactly match their recorded source rectangles
- 48 empty probes through the platform center/open end, and proof the broken tower omits the long mast/aerial rods
- 20 offline adapter cases: supported campfire base, gear base, three tower states and twelve director slots; unsupported burning campfire, moving gear and visible tower resin remain rejected
- Direct-exporter byte equality, GLB 2.0 headers and lengths, valid buffer-view ranges, finite node transforms, hashes, scene names and actual clip names for every file

`blender-import-validation.json` records independent Blender 4.3.2 import of all 23 final GLBs, nonempty finite meshes and zero invalid-mesh repairs. Blender may create one action per animated node; this is not the GLB clip count. Khronos validator was not installed and was not run.

`outdoor-cloud-source-audit.json` records actual frame bounds/opaque pixel counts and source metadata; `outdoor-cloud-source-fetches.json` records repository/ref, Git blob SHAs and source URLs; `outdoor-cloud-crops.json` records derived-image rectangles, source direction/frame, and pixel hashes. A direct `build_models.py --check` passed for all 23 deterministic GLBs, the family manifest and the refreshed viewer outputs. Shared viewer atlases can become stale when another family adds textures; re-export the atlas after aggregate changes. These are source/file checks, not saved-map, native, live state-transition, collision, gameplay, performance or fidelity certification.

## Attribution and license

All inspected source RSI metadata declares **CC-BY-SA-3.0**. Preserve these source metadata files and this attribution with the GLBs and derived PNGs. The new model arrangements and cropped textures are source-derived drafts distributed under the same CC-BY-SA-3.0 terms. Source files were fetched from `TheHellFireo/CMU-Garrison-3D`, ref `Chip/garrison-3d`; precise Git blob SHAs are in the fetch record.

- Platform: `Resources/Textures/_RMC14/Structures/platforms.rsi/meta.json`, attributed by the source to [cmss13 platforms.dmi](https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/platforms.dmi) and [its later revision](https://github.com/cmss13-devs/cmss13/blob/48e570bd697f2476e28d89cd255d0539a5228228/icons/obj/structures/props/platforms.dmi)
- Campfire: `Resources/Textures/_RMC14/Structures/campfire.rsi/meta.json`, attributed to [cmss13 structures.dmi](https://github.com/cmss13-devs/cmss13/blob/7cb618c69b75873f3ce893022fe08d1233b3152d/icons/obj/structures/structures.dmi)
- Gear: `Resources/Textures/_RMC14/Structures/gear.rsi/meta.json`, attributed to [cmss13 stationobjs.dmi](https://github.com/cmss13-devs/cmss13/blob/9ab207cd7ffba86a0411d7058645fb8f2a7895f3/icons/obj/structures/props/stationobjs.dmi)
- Communications tower: `Resources/Textures/_RMC14/Structures/communications_tower.rsi/meta.json`, attributed to [cmss13 comm_tower3.dmi](https://github.com/cmss13-devs/cmss13/blob/e77c994c8b3fcf97b13886de7c56c6b407108598/icons/obj/structures/machinery/comm_tower3.dmi)
- Traffic directors: `Content.CMU/Resources/Textures/CMU14/Structures/trafficdirectors.rsi/meta.json`, attributed to [Steelpoint/cmss13 traffic_signal.dmi](https://github.com/Steelpoint/cmss13/blob/5e66378de08af7e9cc629c1020286002ba187dfe/icons/obj/structures/props/industrial/traffic_signal.dmi)

Inherited prototype YAML and read-only component/visualizer references are identified in the source-fetch record. Exact source PNGs and RSI metadata remain alongside the editable assets in their resource paths. No source artwork was repainted.


## Cumulative atlas assignment
This cumulative snapshot uses centrally allocated, nonconflicting atlas slots. Any authoring-time numeric range in this historical family note is superseded by the canonical surface YAML and `Tools/three_d/generated/cloud-review/atlas-allocation-current.json`. Source IDs, original PNG pixels and modeled geometry are unchanged by atlas-index remapping.
