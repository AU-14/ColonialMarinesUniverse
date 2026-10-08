# Storage and colony processor art drafts

Authored 2026-10-06 from the `Chip/garrison-3d` branch of
`TheHellFireo/CMU-Garrison-3D`. These are editable physical studies, not a
published change, completed gameplay conversion or fidelity approval.

## Deliverables

- Canonical geometry: `Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_storage_cloud.yml`
- Original-art crops: adjacent `garrison_storage_cloud_art.yml`
- Textures: `Content.CMU/Resources/Textures/CMU14/ThreeD/storage_cloud/`
- Eight GLBs adjacent to this note; all `status: draft`
- Reproduction: `python Tools/three_d/author_storage_cloud.py`
- Source-facing, orbit, rear and closed/open comparisons:
  `Tools/three_d/generated/cloud-review/storage/`
- Detailed state, crop and export proof: `storage/storage-proof.json`
- Original source states: `cloud-review/storage-sources.png`
- Four literal processor compositions: `cloud-review/processor-source-compositions.png`
- GitHub blob SHAs and URLs: `cloud-review/storage-source-receipts.json`

The five mapped designs contain eleven exact source prototype references.
The supplied saved-map inventory records 26 Redux and 12 classic occurrences
of these IDs. This is a source-inventory count, not a checked assembled scene.
The three open-shell studies have empty `sourcePrototypes` arrays and add no
exact source coverage. No new animation clips are authored.

## Mini ammunition case

`CMU3DSecureCaseAmmoMiniCloud` references `RMCSecureCaseAmmoMini` and
`/Textures/_RMC14/Structures/Storage/Crates/miniammo.rsi`, state `base`.
The source has one direction and one static state. The inherited source
`RMCSmallChestYellow` supplies the small floor-standing fixture, `noRot`,
`CrateOpenable` and `SpawnOnTerminate` behavior. No container contents, reusable
hinged-open pose or visible randomized ammunition are inferred.

The model has a hollow metal pan and four shell walls, separate gasket/lid,
two raised retaining bands with physical front/rear returns, four folded
corners, and the exact original three yellow marks on the face. The source does
not show a carrying handle; an invented handle is deliberately avoided.
Its single `spriteStates.base` composition uses the existing single-layer
contract and source delay `[1]`, with no changes to the owner or renderer.

Source: [securecrates.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Prototypes/_RMC14/Entities/Structures/Storage/securecrates.yml)
and [miniammo metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Structures/Storage/Crates/miniammo.rsi/meta.json).
Artwork is CC-BY-SA-3.0, taken from cmss13 at
https://github.com/cmss13-devs/cmss13/blob/7cb618c69b75873f3ce893022fe08d1233b3152d/icons/obj/structures/crates.dmi.

Existing `garrison_secure_cases.yml` (yellow secure chest and mini secure case)
and `garrison_storage.yml` were read before authoring. Their conventions of
separate front/top art and physical raised members informed this new case;
their assets were not edited or silently substituted for this missing source.

## Pizza cartons

Three distinct designs retain their actual source artwork:

- `CMU3DPizzaGalaxyBoxCloud`: `RMCBoxPizzaGalaxyMargherita`,
  `RMCBoxPizzaGalaxyMeat`, `RMCBoxPizzaGalaxyVegetable`; resource
  `_RMC14/Objects/Storage/pizza_galaxy_box.rsi`; state `pizzabox1`
- `CMU3DPizzaRandomBoxCloud`: `RMCBoxPizzaRandom`; resource
  `_RMC14/Objects/Storage/pizza_box.rsi`; state `pizzabox1`
- `CMU3DFoodPizzaBoxCloud`: `FoodBoxPizza`, `FoodBoxPizzaFilled`; resource
  `Objects/Consumable/Food/Baked/pizza.rsi`; state `box`

All referenced world states are one-direction. Each carton is a bottom pan,
four independently thick walls, folded corner tabs, a separate printed lid,
and a front closure tongue. Only the original lid and front-band crops are
applied to those volumes; no complete sprite is used as a stand-up slab.

Each design also has a separate `OpenStudy` model with an upright lid attached
at its bottom edge, a real empty recessed tray, side rims and doubled lid edges.
The source references are `pizzabox_open` or `box-open`. The grey oval in the
RMC interior is retained as printed source shading, not modeled as a pizza.
Lid angle, wall thickness and hidden rear are draft physical interpretations.

### Important state boundary

The pizza sources use `EntityStorageVisuals` and two Sprite layers. The
inspected [EntityStorageVisualizerSystem](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Content.Client/Storage/Visualizers/EntityStorageVisualizerSystem.cs)
reads `StorageVisuals.Open` and changes the Door layer while retaining the
Base layer. This is not the existing single-visible-layer `spriteStates`
contract, nor the `DoorComponent` adapter. Accordingly, neither a fabricated
`alternateDoorModel` link nor a single-layer runtime claim is included.

Closed mappings are explicitly static art references. The separate open
studies are not live-selected states. Unknown filled/random contents are not
shown. Resource-only stacked, messy and tag states, upstream bomb overlays,
carried/in-hand artwork, hinge motion, actual open-layer composition and saved
appearance overrides remain unsupported. No extra stacking owner is invented
merely because stack images exist.

Prototype sources: [RMC pizza.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Prototypes/_RMC14/Entities/Objects/Consumables/Food/pizza.yml)
and [food containers box.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Prototypes/Entities/Objects/Consumable/Food/Containers/box.yml).

RMC pizza box artwork is CC-BY-SA-3.0, taken from cmss13 at
https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/obj/items/food/pizza.dmi.
Metadata also credits in-hand sources at commit
`8d3eabd2575fcb859ab4320a298cdbc185910f6f`,
`icons/mob/humans/onmob/inhands/items/food_lefthand.dmi` and `food_righthand.dmi`.
Those in-hand states were not used by these models.

The upstream cream/red box artwork is CC-BY-SA-3.0, taken from tgstation and
modified by Swept at
https://github.com/tgstation/tgstation/commit/40d75cc340c63582fb66ce15bf75a36115f6bdaa.
The retained complete metadata further credits mkanke, mlexf, MisterImp,
Starlight PR 72 and Orsoniks for other states in the same RSI. Their unrelated
pizza filling and in-hand states are outside this batch.

## Colony submission processor

`CMU3DColonyProcessorCloud` has exact draft mappings for
`AUSubmissionPointFruit`, `AUSubmissionPointSteel`,
`AUSubmissionPointVegetables` and `AUSubmissionPointWood`.
All four inherit identical Sprite art from `AUSubmissionPointBase`; no
commodity-specific labels or colors are invented. The reference is
`/Textures/CMU14/Structures/colony_processor.rsi`, one-direction `icon`.

The physical study provides an actual upper recessed intake chute with thick
side/top/bottom walls, a recessed throat, five guide fingers, a lower feed tray
with three individual roller teeth, control fascia, separate ventilation ribs,
and outboard drive housings. Original control and bevel crops sit on these
members. The lower grille was refined to the source's three-pixel height;
source-facing horizontal scale and front elevation are explicit.

### Important directional/state boundary

The prototype has four layers, all initially visible: `icon`, `unlit`
(unshaded), `inserting`, and `panel`. Metadata declares `icon`, `unlit` and
`panel` as one-direction, but `inserting` and `building` as four-direction.
`building` has two source frames at 0.1 seconds each in each direction.
No existing single-layer adapter can legitimately represent this combination.

The literal South composition is pixel-identical to `icon`; the other three
compositions mix the fixed `icon` silhouette with the directional `inserting`
layer. The proof stores their exact pixels and difference counts. This model
is only the South/default physical study. It does not claim North/East/West
composition fidelity, live direction selection, building animation, material
insertion, power, maintenance state, unshaded emission or interior cargo.

Source: [job_machines.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Content.CMU/Resources/Prototypes/CMU14/Economy/Misc/job_machines.yml)
and [colony_processor metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Content.CMU/Resources/Textures/CMU14/Structures/colony_processor.rsi/meta.json).
Artwork is CC-BY-SA-3.0, credited by its source metadata to Nizzy on Discord.

## Verification and limits

- Twenty-two exact source crops use reserved atlas indices 2308–2329
- Every cropped pixel is independently compared with the source PNG
- All eight YAML assemblies validate through unchanged `build_models.py`
- Miniammo source state/direction/delay validates through unchanged `sprite_states.py`
- GLBs are deterministic outputs of the unchanged exporter
- All eight import in Blender 4.3.2 with finite geometry and zero mesh repairs
- All 53 non-textured processor solids form one connected component rooted in
  the chassis. Both rear support paths physically reach the hopper; the intake
  remains open. `processor-connected-solid-check.json` records exact Box
  overlaps and positive-area face contacts, excluding decorative patches
- Source-facing comparisons use a common 384 pixels/tile horizontal scale;
  orbit/rear cards are framing views, not a similarity score
- No map, native viewport, renderer admission, collision, support, placement,
  illumination, shader or gameplay-state acceptance is claimed

All hidden construction and physical depths remain inferred. Preserve these
notes, the source metadata, source-receipt URLs and the CC-BY-SA-3.0 notices
with exported or derived art. This batch contains no engine/runtime edits.


## Cumulative atlas assignment
This cumulative snapshot uses centrally allocated, nonconflicting atlas slots. Any authoring-time numeric range in this historical family note is superseded by the canonical surface YAML and `Tools/three_d/generated/cloud-review/atlas-allocation-current.json`. Source IDs, original PNG pixels and modeled geometry are unchanged by atlas-index remapping.
