# Chemistry and Bobda container art drafts

This cloud art batch supplies 23 editable physical assemblies: eleven exact-prototype **draft proposals**, plus twelve unbound static fill/lid studies. The target inventory records total 32 Redux placements and eight classic placements. Those numbers describe source inventory, not verified native/saved-map/state coverage. All statuses remain `draft`. No engine, runtime, physics, gameplay, map, or publication change is part of this batch.

## Deliverables

- `Content.CMU/Resources/ThreeD/Prototypes/World/garrison_containers_cloud.yml`: editable physical parts
- `garrison_containers_cloud_art.yml`: 51 original PNG label crops, reserved atlas indices 4000–4050
- `Content.CMU/Resources/Textures/CMU14/ThreeD/containers_cloud/`: unchanged source pixels cropped into curved-wall print strips
- `Tools/three_d/generated/cloud-containers/models/`: 23 GLBs and manifest produced directly by unchanged `build_models.py`
- `Tools/three_d/generated/cloud-containers/review/`: standard per-model source/orbit sheets
- `containers-source-and-orbit.png`: layered-source/physical-front/orbit/rear comparison, same horizontal tile scale
- `container-static-layer-studies.png`: empty/lid and all five neutral fill shapes for each chemical vessel
- `bobda-source-states.png`: all six authored can-state views
- `containers-verification.json`, `containers-proof.json`, `source-crops.json`: bounded art checks and source provenance

The standard builder reference for bottle/jug study models shows the `referenceState` base image only. It does not automatically composite unsupported chemical Fill/lid layers. Use the custom layered-source sheets above for those comparisons. This distinction must remain visible when reviewing the work.

## Exact source ownership

Fetched from `TheHellFireo/CMU-Garrison-3D`, branch `Chip/garrison-3d`. Original PNGs and metadata were physically inspected before modeling; the source path, state, frame and license are recorded in `containers-verification.json`.

### Small chemical bottles

Actual owner: `Resources/Prototypes/_RMC14/Entities/Objects/Medical/bottles.yml`; ancestor inspected at `Resources/Prototypes/Entities/Objects/Specific/Chemistry/chemistry-bottles.yml`.

Exact prototypes: `CMBottleEmpty`, `CMBottleBicaridine`, `CMBottleDexalin`, `CMBottleDylovene`, `CMBottleInaprovaline`, `CMBottleKelotane`, `CMBottleTricordrazine`. Redux inventory: two empty bottles, one of each filled bottle, eight total.

All seven actually use `_RMC14/Objects/Chemistry/bottles.rsi`, state `bottle-1`, one direction and one static frame. The other three bottle designs in that RSI are not randomly selected by these prototypes and are not substituted. The owner supplies `bottle-1-1` through `bottle-1-5` in the Fill layer and `lid_bottle-1` in `enum.OpenableVisuals.Layer`. Source default `Openable.opened` is true, so default lid is hidden. Filled child solutions explicitly contain 60/60 units of one named reagent.

Static default study colors, read from `_RMC14/Reagents/medicine.yml`:

- Bicaridine `#ED4847`
- Dexalin `#1F28A7`
- Dylovene `#3FC92A`
- Inaprovaline `#FE33CB`
- Kelotane `#F5E123`
- Tricordrazine `#D87F2B`

These are static prototype-default studies, not simulation of source MapInit, saved overrides, mixtures or reactions. The six neutral bottle studies (five fills and one empty/capped) carry no `sourcePrototypes`. The model has a segmented hollow glass body, a thick heel, inward shoulders, a narrow hollow neck and a rolled open lip. The cap study physically closes the throat.

### Reagent jug

Actual owner and complete bucket ancestry: `Resources/Prototypes/_RMC14/Entities/Objects/Tools/bucket.yml`.

Exact prototype `RMCReagentJug`: ten Redux records, no classic records. Source `_RMC14/Objects/Chemistry/reagent_jug.rsi`, base `reagentjug`, Fill `reagentjug-1` through `reagentjug-5`, separate `lid_reagentjug`. Each world state has one direction and one frame. Source default is empty and open; inherited capacity is 500 units. Carried states have four directions and are not claimed.

The physical assembly uses separate canister walls/floor/shoulders, a hollow pouring throat, a bridge handle with an actual through-aperture and raised source-colored calibration ticks. The five neutral fill and empty/capped studies are unbound. The liquid surface is flat physical geometry; its five authored heights follow the inspected source ordering, not a runtime volume-to-state algorithm.

### Bobda / suoto cans

Actual owner: `Resources/Prototypes/_RMC14/Entities/Objects/Consumables/Drinks/cans.yml`. Ancestors inspected at `Resources/Prototypes/Entities/Objects/Consumable/Drinks/drinks_cans.yml`, `drinks_base.yml`, `drinks_base_materials.yml`.

- `CMDrinkCanBobdaBlue`: `_RMC14/Objects/Consumable/Drinks/Bobda/blueraspberry.rsi`, nine Redux / three classic
- `CMDrinkCanBobdaCherry`: `.../cherry.rsi`, three Redux / three classic
- `CMDrinkCanBobdaClassic`: `.../classic.rsi`, two Redux / two classic

Their owner explicitly maps both open and closed to `icon`; this batch does not invent a visible opening state. Each resource contains static one-direction `icon` and `crushed`; both have real geometric poses authored under the existing rotating `spriteStates` contract. Three models author six distinct source-state scenes and zero animation clips; the exporter also retains each model's default scene, so those three GLBs contain nine scene entries. No claim is made that these targets' gameplay currently selects `crushed`. A source-owned state can be shown only when the existing adapter accepts the actual layer/state contract.

The round metal body, raised rolled seams, recessed top and cosmetic embossed tab have physical depth. Central source print columns are retained as unresampled PNG tangent strips on the curved front. The top and rear are inferred physical interpretation, with original grayscale values. The crushed form has a short body, buckled rounded bottom and side dents, rather than an extrusion of the entire sprite.

## Limits and compatibility

The current `solution_glass_states.py` contract accepts only the existing clear/drink-glass resources and their exact Base/Fill/Overlay owners. It rejects these chemical bottle/jug resources and the additional GenericVisualizer lid owner. This batch does not broaden it or introduce another adapter. Therefore:

- Bottle/jug live Fill, lid, color, reaction, mixture, emptiness, in-hand and destroyed-state selection are unsupported by these static proposals
- No claim of saved-map appearance compatibility or native admission follows from their `sourcePrototypes` fields
- The 12 static bottle/jug studies deliberately have empty `sourcePrototypes` and cannot inflate exact-prototype coverage
- The can single-layer RSI contract is authored and export-validated; actual native interaction, source state selection and game support remain unverified
- Full map contact/support fitting, packing/admission, renderer performance, gameplay and player visibility have not been run in this bounded art workspace

Original glass/plastic sprites contain opaque RGB artwork. This 3D interpretation uses the original four aqua RGB values with inferred partial-alpha wall material to reveal the hollow interior and liquid volume. That is an intentional physical draft decision, **not preservation of the source alpha**. Source compositing in the comparison sheets preserves actual source alpha. Physical circular/rectangular section, shoulder construction, hidden sides, rear printing, depth, metal top, liquid meniscus and highlight placement are inferred. They require further fidelity review.

## Default open-state provenance clarification

The bottle/jug comparison captions explicitly identify the actual prototype default: `opened: true`, with the separate lid layer hidden. This is a presentation/provenance clarification, not a geometry correction. The existing base source composites already omitted the separate lid; the open-neck base geometry is retained. The frozen batch3 archive is unchanged.

This conclusion does **not** depend on the Sprite template's initial `visible: false` alone:

1. [Bottle owner YAML, lines 25–37](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Prototypes/_RMC14/Entities/Objects/Medical/bottles.yml#L25-L37) explicitly sets `Openable.opened: true` and maps true to lid visibility false. `CMBottleFilled` inherits `CMBottleEmpty`; none of the six target medicine children overrides Openable.
2. [Jug owner YAML, lines 170–182](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Prototypes/_RMC14/Entities/Objects/Tools/bucket.yml#L170-L182) explicitly sets `opened: true` with the same true → hidden lid / false → visible lid mapping.
3. [OpenableComponent](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Content.Shared/Nutrition/Components/OpenableComponent.cs) has a generic false Boolean default when no serialized value is supplied. The explicit target YAML values override it.
4. [OpenableSystem startup, lines 29 and 55–58](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Content.Shared/Nutrition/EntitySystems/OpenableSystem.cs#L55-L58) subscribes ComponentInit to OnInit, which calls UpdateAppearance. [UpdateAppearance, lines 174–180](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Content.Shared/Nutrition/EntitySystems/OpenableSystem.cs#L174-L180) forwards `comp.Opened` directly to `OpenableVisuals.Opened`; it does not reset the target to closed.

The actual separate bottle cap is `lid_bottle-1`, and the jug cap is `lid_reagentjug`. These remain represented by explicitly unbound static cap studies. Saved-instance Openable overrides could close an individual entity; those records and live selection remain unverified. The neutral fill studies likewise remain unbound and are not runtime chemistry selection. See `Tools/three_d/generated/cloud-containers/container-default-open-proof.json` for the compact read-only evidence and geometry-preservation check.

## Checks

Unchanged `build_models.py` successfully exports the family, and immediate `--check` verifies deterministic GLBs, manifest and viewer assets. The 23 GLBs contain 29 scene entries representing 26 distinct modeled poses, with zero animation clips. A separate local GLB header/accessor/embedded-image smoke check passes; this is not independent Khronos validation. Source PNG crop checks compare actual RGBA bytes for all 51 images. All 20 inspected world source states are 32×32, one direction and one static frame. Part counts are below 128 for every default and source-state pose. Offline aperture tests sample 131 points through the bottle mouth/interior, 193 through the jug throat/interior, and 363 through the handle aperture; all are clear. Cap studies separately prove the throat sample is occupied. Those sample tests do not establish complete solid topology, native ray behavior, physics or gameplay.

Run from repository root:

    python Tools/three_d/author_containers_cloud.py
    python Tools/three_d/verify_containers_cloud.py
    python Tools/three_d/build_models.py --source Content.CMU/Resources/ThreeD/Prototypes/World/garrison_containers_cloud.yml --output Tools/three_d/generated/cloud-containers/models --review-output Tools/three_d/generated/cloud-containers/review --viewer-output Tools/three_d/generated/cloud-containers/viewer
    python Tools/three_d/build_models.py --source Content.CMU/Resources/ThreeD/Prototypes/World/garrison_containers_cloud.yml --output Tools/three_d/generated/cloud-containers/models --review-output Tools/three_d/generated/cloud-containers/review --viewer-output Tools/three_d/generated/cloud-containers/viewer --check

## Attribution

Keep this file and the relevant original RSI `meta.json` files with exported or derivative art.

Bottle art: CC-BY-SA-3.0. Source metadata credits CM-SS13 art at:

- https://github.com/cmss13-devs/cmss13/blob/311e8f06c0eae7c6a0ab71ba04943c93c55850eb/icons/obj/items/chemistry.dmi
- https://github.com/cmss13-devs/cmss13/blob/ca94d2e8715b73103fa9f213be53d343359b4107/icons/obj/items/reagentfillings.dmi
- Other carried-art sources and the unused `label-1` attribution remain in the unchanged source metadata

Jug art: CC-BY-SA-4.0. Original metadata: “Made by Sigma Draconis, modified by github noctyrnal”.

Bobda art and derived PNG print crops: CC-BY-SA-3.0, taken from CM-SS13 at:

- https://github.com/cmss13-devs/cmss13/blob/0e5b77c99f162aa1462823e996ba8e0d52656448/icons/obj/items/drinkcans.dmi
- Original carried-art references remain in each unchanged RSI metadata file

These geometry and crop adaptations retain the applicable source attribution and share-alike terms. No ownership or fidelity approval is asserted.


## Cumulative atlas assignment
This cumulative snapshot uses centrally allocated, nonconflicting atlas slots. Any authoring-time numeric range in this historical family note is superseded by the canonical surface YAML and `Tools/three_d/generated/cloud-review/atlas-allocation-current.json`. Source IDs, original PNG pixels and modeled geometry are unchanged by atlas-index remapping.
