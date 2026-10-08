# Natural and fishing props: cloud art-only draft batch

Source checkpoint: `TheHellFireo/CMU-Garrison-3D`, commit `6e37a4d0a7d9433838c393a82c02422cb704dd5a`.

This batch creates ten draft solid assemblies with thirteen static authored poses: seven new exact prototype reference mappings, two unbound extended-line rod studies and one unbound destroyed-egg study. The seven exact IDs occur in nine Redux placements and three classic placements according to the checkpoint inventory. These are inventory counts, not a live or saved-map fit certification. No engine, gameplay, renderer, spawn logic or original earlier model was edited. Nothing was pushed or published.

## Deliverables

- Editable model definitions: `Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_natural_final_cloud.yml`
- Small source-detail surfaces: `Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_natural_final_cloud_art.yml`
- Eight original-pixel PNG crops: `Content.CMU/Resources/Textures/CMU14/ThreeD/natural_final_cloud/`
- Portable GLBs beside this document, named by their model IDs
- Source-facing, orbit, reverse and fixed-scale evidence: `Tools/three_d/generated/review/natural-final-cloud/`
- Authored model/pose and crop hashes: `Tools/three_d/generated/natural-final-cloud-verification.json`
- Source, mapping, geometry, palette and deterministic-export audit: `Tools/three_d/generated/natural-final-cloud-source-audit.json`
- Independent Blender import results: `Tools/three_d/generated/natural-final-cloud-blender-import.json`
- Complete batch-owned file manifest: `Tools/three_d/generated/natural-final-cloud-manifest.json`

Eight atlas indices, 2990–2997, come from the exact reserved `natural_final` allocation. No other reserved slot was used. Original and new surface registries were checked together for collisions.

## New exact models

| Prototype | Model ID | Authored interpretation |
|---|---|---|
| RMCBushg3 | CMU3DRMCBushg3NaturalFinalCloud | Sparse forked brown branches, 27 source-positioned leaf sprays and 27 depth leaves |
| RMCRock06 | CMU3DRMCRock06NaturalFinalCloud | Six separate rocks: three chips and three differently shaped fractured shards |
| RMCStump1 | CMU3DRMCStump1NaturalFinalCloud | Small dark trunk, flared roots, exposed wood scars, cut top and two tiny bark details |
| FishBass | CMU3DFishBassNaturalFinalCloud | Lying olive fish prop, pale belly, seven fin groups, fin rays, gill, mouth, eye and scale details |
| FishingRod | CMU3DFishingRodNaturalFinalCloud | Rubber grip, brown rod, gold tip wrap, red reel, bracket, three open line guides and attached pale line |
| FishingRodMakeshift | CMU3DFishingRodMakeshiftNaturalFinalCloud | Irregular branch, broken twigs and string binding; the source does not show a manufactured reel |
| CMUXenoEggNotDS | CMU3DCMUXenoEggNotDSNaturalFinalCloud | Closed portable item, growing rooted egg, grown closed shell and physically open post-hatch shell |

### Vegetation, stones and stump

`Resources/Prototypes/_RMC14/Entities/Objects/Misc/bushes.yml` owns these three source identities. All inherit `noRot: true`. No whole-sprite plane, billboard canopy or generic canopy sphere is used. Bush and stump have connected structural wood and independently modeled leaves or roots.

The rock06 sprite contains six disconnected groups, not one boulder. Height and depth are inferred; its source-oblique review is 0.63 radians above horizontal. Depth positions compensate that projection to retain the source separation of the three rear chips from the tall foreground shard. Fifteen isolated group-pair checks found no sampled rock-to-rock penetration. These checks do not establish contact with surrounding map geometry.

The 64×64 stump source is mostly empty space and low-alpha ground shadow. Its `Sprite.offset: 0,0.7` is a projected-art offset, not a world-position instruction. Bush alpha-11/54/94 shadows, rock alpha-50 shadows, and the stump's broad low-alpha shadows were not modeled as physical soil slabs. Opaque geometry, footprint and hidden surfaces remain drafts.

### Bass and fishing rods

Fish source: `Resources/Prototypes/_SS14/_Goobstation/Entities/Objects/Specific/Fishing/fish.yml`, BaseFish → FishBass. The single-frame `bass` world icon is a static prop only. No swimming or carried rig exists in this batch.

Rod source: `Resources/Prototypes/_SS14/_Goobstation/Entities/Objects/Tools/fishing_rods.yml`. Both inherited loose-world defaults are `icon`, one direction, one frame. The source silhouette is authored as a lying solid prop. The normal rod keeps a physically attached line running beside the blank; the makeshift default has the short tip binding visible in its source.

The actual relocated CMU fishing code was read at the pinned commit:

- `Content.CMU/Shared/Fishing/Components/FishingRodComponent.cs`
- `Content.CMU/Shared/Fishing/Systems/SharedFishingSystem.cs`
- `Content.CMU/Client/Fishing/FishingSystem.cs`
- `Content.CMU/Client/Fishing/Overlays/FishingOverlay.cs`
- `Content.CMU/Server/Fishing/FishingSystem.cs`

Those owners create separate lure and rope visuals. They do not select the rod's `icon-active` resource. Therefore `CMU3DFishingRodNaturalFinalCloudActiveStudy` and `CMU3DFishingRodMakeshiftNaturalFinalCloudActiveStudy` have empty `sourcePrototypes` and remain unbound resource studies. Their extended line, float or hook follows the inspected original image. No casting, retrieval or reel animation is claimed, and no runtime adapter was added. Held left/right and active-held images remain outside this world-prop batch.

### Xeno egg source states

`CMUXenoEggNotDS` only adds DestroyOnPreset=DistressSignal to `XenoEgg`. The inherited XenoEggComponent enum field initializes to Item; the source Sprite Base layer starts at `egg_item`. `XenoEggVisualizerSystem` maps Item/Growing/Grown/Opening/Opened to `egg_item`/`egg_growing`/`egg`/`egg_opening`/`egg_opened` respectively. It can also change the RSI for fragile/sustained forms.

This batch uses the existing single-visible-RSI-layer art format for four static states, all one direction and one frame:

1. `egg_item`: the smaller closed portable egg, no ground tendrils
2. `egg_growing`: squat rooted closed egg
3. `egg`: taller grown closed egg
4. `egg_opened`: source post-hatch/opened shell, open central lumen, recessed floor, pink rim lobes and roots

“Opened” and “post-hatch” describe the same source state; no invented separate “hatched” enum is introduced. Closed geometry has interior backing; the opened pose removes it. A nine-point transverse grid sampled through the cavity above its recessed floor remains clear. Closed central-shell samples are occupied. Carapace depth, backside coloration and the root arrangement remain inferred.

The ten-frame `egg_opening` and seven-frame `egg_exploding` assets were inspected but are not authored animations. Fragile/sustained RSIs, parasite spawning, growth timing, interactions and state transitions are not implemented or verified. The destroyed source `egg_exploded` belongs to the replacement `XenoEggDestroyed` entity; `CMU3DCMUXenoEggNotDSNaturalFinalCloudDestroyedStudy` is deliberately unbound and does not claim CMUXenoEggNotDS default or destruction coverage.

## Existing conditional mappings reviewed, not duplicated

All 142 original model-definition files and every current authored definition were scanned, along with inventory, `physicalModelingFamilies` and the checkpoint `randomSpriteMappings` records. The following source choices already exist:

| Prototype | Required source choices | Existing models | New geometry |
|---|---|---|---|
| FloraTree | `random`: tree01 through tree06 | CMU3DBroadleafTree01 through 06 | None |
| FloraTreeLarge | `random`: treelarge01 through treelarge06 | CMU3DBroadleafTreeLarge01 through 06 | None |
| FoodEgg | `enum.DamageStateVisualLayers.Base`: icon, white | CMU3DFoodEggCreamExtraCloud, CMU3DFoodEggWhiteExtraCloud | None |

The tree mappings are in the pinned `garrison_environment.yml` and `garrison_outdoors.yml`; the two food-egg mappings are in `garrison_food_extra_cloud.yml`. Every required tuple is present exactly once. None of these three prototypes is assigned an unconditional exact mapping in this batch. Their aggregate inventory is eight Redux tree placements plus two Redux food eggs (seven classic trees); selected random choices are not recorded in this inventory. This review does not convert those placements into exact static coverage. Unknown choices, runtime selection behavior and fidelity remain separate questions.

The random choices are source-owned by `Resources/Prototypes/Entities/Objects/Decoration/flora.yml` and `Resources/Prototypes/Entities/Objects/Consumable/Food/egg.yml`. SharedRandomSpriteSystem and client RandomSpriteSystem were inspected to verify that the selected source layer state is replicated/applied. Existing tree or food models were not edited.

## Evidence and limits

- All 53 fetched prototype, metadata, image and owner-code files match the pinned Git blob SHA; per-file details are in the source audit
- Eight crops exactly preserve source RGBA pixels without resampling or repainting; whole object silhouettes are solid geometry
- Every untextured part color in every authored pose belongs to that pose's original opaque source palette
- Source-facing and orbit/reverse views are supplied for all thirteen static poses; fixed-scale sheets use 32 source pixels per tile and 192 rendered pixels per tile
- Isolated shell opening, closed-shell, open-guide and six-rock separation checks pass; touching parts within one assembly are intentional
- Maximum authored pose size is 112 supported parts, within the 128-part contract
- Every GLB byte stream matches deterministic re-export, and bounded buffer ranges and node transforms pass local checks
- Blender 4.3.2 independently imports all ten GLBs with no nonfinite vertices or mesh repairs
- The exporter implementation matches the pinned source; its pre-existing terminal-newline difference is recorded and it was not modified

These are draft-art and file-integrity checks. They do not constitute Khronos validation, native renderer verification, complete map-contact review, fidelity approval, gameplay coverage, complete source-animation support or approval of all inferred sides/materials.

## Original artwork attribution and license boundaries

The fetched original `meta.json` files retain exact attribution. Keep those metadata files, this document and source references with the derived exports.

- Bushes and rocks: CC-BY-SA-3.0, tgstation art. Bush metadata cites commits `729d858807905263adab8b5a331c1d8a04982dd3` and `79296e902cbdf2352c9303e4769ea39bf3b34e58`. Rock metadata cites `79296e902cbdf2352c9303e4769ea39bf3b34e58` and `74bda160b97739cb9159dd19fe0800a5526735c0`
- Stump: CC-BY-SA-3.0, tgstation commit `8aeb2678e0150dcb308f02304ec200cef235e62c`
- Bass/fish RSI: CC-BY-SA-3.0; exact contributor attribution for the multi-fish resource is preserved in `fish.rsi/meta.json`. That metadata does not separately identify bass's artist
- Standard rod: CC-BY-NC-SA-3.0, Goonstation commit `39ddf6bbd54c9f27fe49f5cea1a3119738b09596`
- Makeshift rod: CC-BY-NC-SA-3.0, arraydeess and rouden_ (Discord)
- Xeno egg: CC-BY-SA-3.0, cmss13 effects.dmi at commit `20db6e3fc344d59ed9502ee7626d9fea89674713`
- Reviewed existing normal trees: CC-BY-SA-3.0, tgstation commit `e00cae8d065f9cf520688cc0dd0e15ba5bef12a9`
- Reviewed existing large trees: CC-BY-SA-3.0, tgstation commit `d388dee8b7b6d854f6f0d844988552acf5962b1f`

The noncommercial/share-alike restrictions on the rod sources must not be lost when this family is combined with other art. Reading source code for default/state ownership does not change the artwork licenses. No blanket relicensing or permission for commercial use is asserted.
