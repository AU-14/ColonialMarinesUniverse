# Additional Redux food: physical source-derived drafts

## Scope and verification

This asset-only family contains **22 draft assemblies / 25 authored physical poses**, covering the 19 assigned Redux source IDs as 18 exact mappings, one conditional RandomSprite family with two egg colors, and two unbound open-carton studies. Exact mapped source types account for **31 Redux and 18 classic saved placements**. The two Redux FoodEgg placements are conditional candidates; no unrecorded random choice is guessed and they do not inflate static exact coverage.

All exported GLBs use the unchanged `build_models` builder. The models contain real closed food volume, independent ingredients and seaweed/rice/fish cross-sections, curved noodles, shell/lid seams, molded carton wells, bottle bodies/shoulders/necks/caps and physical package crimps. No complete food sprite is substituted for its geometry. The 22 unchanged PNG crops retain only small packaging print, bottle labels and carton markings. Texture atlas indices are 2950–2971. Every untextured RGB is present in the actual source frame or stated layered reference; wrapper-film alpha is an explicitly inferred material choice.

Checks completed:
- All 22 GLBs reproduce byte-for-byte from canonical family YAML
- Independent Blender 4.3.2 import succeeds for all 22, with no nonfinite vertices or mesh repairs
- All 25 physical poses fit the existing 128-part limit; maximum 119
- Nine sampled checks verify three open ration mouths, their closed caps, twelve empty/full carton wells and an open sauce-cup mouth
- All 22 original detail crops match their source pixels; all 25 poses pass source-palette checks
- Combined local model IDs, exact source mappings and atlas indices have no collisions

These are draft art and offline geometric checks. Map fit, supporting-surface contacts, native admission/rendering, dynamic source behavior, actual gameplay, inferred scale/depth and complete visual fidelity are **unverified**. No engine/runtime file, gameplay rule, map, live server or published repository was changed. The complete source-facing fixed-scale sheet uses a declared 256 pixels/tile and does not claim equal shape or silhouette fidelity.

## Source and initial-state decisions

Sources were fetched through the GitHub connector from `TheHellFireo/CMU-Garrison-3D`, ref `Chip/garrison-3d`. PNGs were decoded directly from base64. `reference/food-extra-fetches.json` records repository file paths, source Git blob SHA values and URLs; its original PNG hashes were checked against Git's blob formula. Source images, individual count layers, compositions, per-pose front/orbit/rear cards and fixed-scale evidence are retained under `Tools/three_d/generated/review/food-extra-cloud/`.

Visual/default-state ancestors were fetched and inspected, including actual BaseItem Sprite defaults, BaseStorageItem, BaseBagOpenClose, BoxBase, BoxCardboard, food bases, ration/snack/pizza parents and condiment parents. The only unresolved ancestor in the scoped audit is CMCorrodible; the inventory evidence is preserved and complete root ancestry is not certified. Its missing nonvisual root is never substituted with guessed source data.

1. **MRE / SPP IRP / TSE ORP.** Actual `OpenableComponent.Opened` defaults to false. These three prototypes inherit CMMRE's GenericVisualizer, selecting closed/open on the source layer. Both physical states use the existing single-visible-layer spriteStates contract with source rotation enabled. UNMC has a peeled tear-strip and soft folded gussets; SPP and TSE have differently proportioned formed shells and raised lids. The real source state owns selection; there is no synthetic transition or new gameplay clock. Storage contents, hidden items, unsupported appearance overrides and native interaction remain unverified.
2. **FoodEgg.** Actual RandomSprite chooses `icon` or `white` on `enum.DamageStateVisualLayers.Base`. Both colors have conditional `randomSpritePrototypes` mappings, with no unconditional exact FoodEgg mapping. Unknown saved choices must remain unknown. The smooth oval shell is inferred; asymmetric egg geometry, breakage, cooking and replacement eggshell entities are unfinished.
3. **FoodContainerEgg.** BaseBagOpenClose is driven by `SharedStorageSystem.UpdateAppearance`, which uses actual UI-open state. `StorageComponent.HideStackVisualsWhenClosed` defaults to true; the owner sets `StackVisuals.Hide = !isOpen`. The initial closed carton therefore hides its twelve independently counted egg layers. This is checked through owner code, not inferred from template `visible:false`. The exact mapping is only a closed static source study; separate open-empty and open-full references have no prototype mappings. Live lid/count composition and partial counts remain unsupported.
4. **Hot-sauce bottles.** RMCCondiment inherits closed Openable defaults and no visible-cap visualizer. Each resource only supplies its complete `icon`; the Tabasco red cap/green neck and Cholula rounded pale wood cap/short red neck stay attached in the static model. No fictional open icon or cap-removal adapter is invented. Contents, pouring and cap logic remain source gameplay, unrepresented by these drafts.
5. **Wrapped hotdog/cheeseburger.** These use `SpawnItemsOnUse` to replace the package with food and trash entities. The packaged solid and its internal food mass are modeled, but there is no false open/closed partner or consumption animation.
6. **Food mass.** The actual bacon is raw pink/red with pale fat. The cooked-meat source is brown/seared. Pizzas have separately traced cheese, meat and basil clusters, rather than identical procedural topping layouts; source pixel projection into physical depth remains inferred. Sushi preserves the original count (three MRE rolls / two fish maki). Spaghetti has continuous segmented noodles beneath a low tomato mound; fries have separate sticks and the original white dish. Sashimi has separate pink slices, curved garnish rings and a physically open dipping cup. Food portions, slicing/refinement results, ingredient chemistry, cooking/burning, carried/worn sprites and consumed states remain explicit gaps.

### State-owner source links

- [Resources/Prototypes/Entities/Objects/base_item.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Prototypes/Entities/Objects/base_item.yml)
- [Resources/Prototypes/Entities/Objects/Misc/box.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Prototypes/Entities/Objects/Misc/box.yml)
- [Resources/Prototypes/Catalog/Fills/Boxes/general.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Prototypes/Catalog/Fills/Boxes/general.yml)
- [Resources/Prototypes/Entities/Objects/Consumable/Food/egg.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Prototypes/Entities/Objects/Consumable/Food/egg.yml)
- [Resources/Prototypes/Entities/Objects/Consumable/Food/Containers/box.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Prototypes/Entities/Objects/Consumable/Food/Containers/box.yml)
- [Resources/Prototypes/_RMC14/Entities/Objects/Consumables/Food/MRE/mre.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Prototypes/_RMC14/Entities/Objects/Consumables/Food/MRE/mre.yml)
- [Resources/Prototypes/_RMC14/Entities/Objects/Consumables/Food/condiments.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Prototypes/_RMC14/Entities/Objects/Consumables/Food/condiments.yml)
- [Content.Shared/Nutrition/Components/OpenableComponent.cs](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Content.Shared/Nutrition/Components/OpenableComponent.cs)
- [Content.Shared/Nutrition/EntitySystems/OpenableSystem.cs](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Content.Shared/Nutrition/EntitySystems/OpenableSystem.cs)
- [Content.Shared/Storage/StorageComponent.cs](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Content.Shared/Storage/StorageComponent.cs)
- [Content.Shared/Storage/EntitySystems/SharedStorageSystem.cs](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Content.Shared/Storage/EntitySystems/SharedStorageSystem.cs)
- [Content.Client/Storage/Systems/ItemCounterSystem.cs](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Content.Client/Storage/Systems/ItemCounterSystem.cs)

## Model/source register

| Assembly | Source prototype | Authored state(s) | Binding |
|---|---|---|---|
| CMU3DCMMREExtraCloud | CMMRE | closed, open | existing single-layer source-state contract |
| CMU3DCMMREFoodSushiExtraCloud | CMMREFoodSushi | sushi | exact static source draft |
| CMU3DFoodContainerEggExtraCloud | FoodContainerEgg | box-closed | exact static source draft |
| CMU3DFoodContainerEggOpenEmptyStudyExtraCloud | FoodContainerEgg (reference only) | box-open | unbound static study |
| CMU3DFoodContainerEggOpenFullStudyExtraCloud | FoodContainerEgg (reference only) | box-open | unbound static study |
| CMU3DFoodEggCreamExtraCloud | FoodEgg | icon | conditional RandomSprite |
| CMU3DFoodEggWhiteExtraCloud | FoodEgg | white | conditional RandomSprite |
| CMU3DFoodMealFriesExtraCloud | FoodMealFries | fries | exact static source draft |
| CMU3DFoodMealSashimiExtraCloud | FoodMealSashimi | sashimi | exact static source draft |
| CMU3DFoodMeatBaconExtraCloud | FoodMeatBacon | bacon | exact static source draft |
| CMU3DFoodMeatCookedExtraCloud | FoodMeatCooked | plain-cooked | exact static source draft |
| CMU3DFoodNoodlesExtraCloud | FoodNoodles | tomato | exact static source draft |
| CMU3DRMCCondimentHotsauceCholulaExtraCloud | RMCCondimentHotsauceCholula | icon | exact static source draft |
| CMU3DRMCCondimentHotsauceTabascoExtraCloud | RMCCondimentHotsauceTabasco | icon | exact static source draft |
| CMU3DRMCFoodMeatFishSushiExtraCloud | RMCFoodMeatFishSushi | fishsushiroll | exact static source draft |
| CMU3DRMCFoodPizzaMargheritaFullExtraCloud | RMCFoodPizzaMargheritaFull | pizza | exact static source draft |
| CMU3DRMCFoodPizzaMeatFullExtraCloud | RMCFoodPizzaMeatFull | pizza | exact static source draft |
| CMU3DRMCFoodSnackCheeseburgerExtraCloud | RMCFoodSnackCheeseburger | hburger | exact static source draft |
| CMU3DRMCFoodSnackCheeseburgerPackagedExtraCloud | RMCFoodSnackCheeseburgerPackaged | burger | exact static source draft |
| CMU3DRMCFoodSnackHotdogPackagedExtraCloud | RMCFoodSnackHotdogPackaged | packaged-hotdog | exact static source draft |
| CMU3DRMCMRESPPExtraCloud | RMCMRESPP | closed, open | existing single-layer source-state contract |
| CMU3DRMCMRETSEExtraCloud | RMCMRETSE | closed, open | existing single-layer source-state contract |

## Original artwork attribution

Original artwork and derived printed crops retain the licenses recorded in their RSI metadata. The following declarations are copied verbatim from the inspected metadata; broad RSI credits may cover states that this family does not use. The new source-derived geometry is marked as an adaptation. Distribute these attributions and source metadata with the model exports. CC BY-SA 3.0 applies to the respective 3.0-origin adaptations; CC BY-SA 4.0 applies to `misc.rsi`, `mre_spp.rsi` and `mre_tse.rsi` adaptations.

### /Textures/Objects/Consumable/Food/egg.rsi

- Metadata: [Resources/Textures/Objects/Consumable/Food/egg.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/Objects/Consumable/Food/egg.rsi/meta.json)
- License: CC-BY-SA-3.0
- Original declaration: Taken from cev-eris at https://github.com/discordia-space/CEV-Eris/raw/9c980cb9bc84d07b1c210c5447798af525185f80/icons/obj/food.dmi

### /Textures/Objects/Consumable/Food/meals.rsi

- Metadata: [Resources/Textures/Objects/Consumable/Food/meals.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/Objects/Consumable/Food/meals.rsi/meta.json)
- License: CC-BY-SA-3.0
- Original declaration: Taken from tgstation at commit https://github.com/tgstation/tgstation/commit/c6e3401f2e7e1e55c57060cdf956a98ef1fefc24, taco from https://github.com/ss220-space/Paradise/commit/6c9bd827610433093a79d814b96bd50f9cf12eec, corn in butter from im_kreks

### /Textures/Objects/Consumable/Food/meat.rsi

- Metadata: [Resources/Textures/Objects/Consumable/Food/meat.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/Objects/Consumable/Food/meat.rsi/meta.json)
- License: CC-BY-SA-3.0
- Original declaration: Taken from tgstation and modified by Swept, potato1234x and deltanedas at https://github.com/tgstation/tgstation/commit/40d75cc340c63582fb66ce15bf75a36115f6bdaa, snail by IproduceWidgets (github) and Kezu (discord), anomalymeat/cooked by august-sun, dragoncutlet, dragoncutlet_veins, dragoncutlet-cooked and dragon-cooked by JuneSzalkowska (discord), raw and cooked patty taken from tgstation at https://github.com/tgstation/tgstation/commit/b83c7deee4c91df4de130db242facce20308aa8a. A lot of inhands by Orsoniks.

### /Textures/Objects/Consumable/Food/noodles.rsi

- Metadata: [Resources/Textures/Objects/Consumable/Food/noodles.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/Objects/Consumable/Food/noodles.rsi/meta.json)
- License: CC-BY-SA-3.0
- Original declaration: Taken from tgstation and modified by Swept at https://github.com/tgstation/tgstation/commit/40d75cc340c63582fb66ce15bf75a36115f6bdaa

### /Textures/_RMC14/Objects/Consumable/Food/Pizza/margherita.rsi

- Metadata: [Resources/Textures/_RMC14/Objects/Consumable/Food/Pizza/margherita.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Consumable/Food/Pizza/margherita.rsi/meta.json)
- License: CC-BY-SA-3.0
- Original declaration: Slice sprite by SurfinNinja, rest taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/obj/items/food/pizza.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items/food_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items/food_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items/food_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items/food_righthand.dmi

### /Textures/_RMC14/Objects/Consumable/Food/Pizza/meat.rsi

- Metadata: [Resources/Textures/_RMC14/Objects/Consumable/Food/Pizza/meat.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Consumable/Food/Pizza/meat.rsi/meta.json)
- License: CC-BY-SA-3.0
- Original declaration: Slice sprite by SurfinNinja, rest taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/obj/items/food/pizza.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items/food_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items/food_righthand.dmi

### /Textures/_RMC14/Objects/Consumable/Food/Sauces/cholula.rsi

- Metadata: [Resources/Textures/_RMC14/Objects/Consumable/Food/Sauces/cholula.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Consumable/Food/Sauces/cholula.rsi/meta.json)
- License: CC-BY-SA-3.0
- Original declaration: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/obj/items/food/condiments.dmi, https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/mob/humans/onmob/inhands/items/food_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/mob/humans/onmob/inhands/items/food_righthand.dmi

### /Textures/_RMC14/Objects/Consumable/Food/Sauces/tabasco.rsi

- Metadata: [Resources/Textures/_RMC14/Objects/Consumable/Food/Sauces/tabasco.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Consumable/Food/Sauces/tabasco.rsi/meta.json)
- License: CC-BY-SA-3.0
- Original declaration: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/obj/items/food/condiments.dmi, https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/mob/humans/onmob/inhands/items/food_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/mob/humans/onmob/inhands/items/food_righthand.dmi

### /Textures/_RMC14/Objects/Consumable/Food/misc.rsi

- Metadata: [Resources/Textures/_RMC14/Objects/Consumable/Food/misc.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Consumable/Food/misc.rsi/meta.json)
- License: CC-BY-SA-4.0
- Original declaration: peanut butter cookie sprite by Crow/noctrn., -silog sprites by Kezu, other sprites sprites by SurfinNinja, Raw Fish, Grilled Fish, Fish & Chips by SharkSnake98

### /Textures/_RMC14/Objects/Consumable/Food/mre.rsi

- Metadata: [Resources/Textures/_RMC14/Objects/Consumable/Food/mre.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Consumable/Food/mre.rsi/meta.json)
- License: CC-BY-SA-3.0
- Original declaration: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7cb618c69b75873f3ce893022fe08d1233b3152d/icons/obj/items/storage.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items/storage_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items/storage_righthand.dmi, with small modifications by Vermidia

### /Textures/_RMC14/Objects/Consumable/Food/mre_contents.rsi

- Metadata: [Resources/Textures/_RMC14/Objects/Consumable/Food/mre_contents.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Consumable/Food/mre_contents.rsi/meta.json)
- License: CC-BY-SA-3.0
- Original declaration: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/1cc75f29838f1dd7c6772bd5512716767a86a35f/icons/obj/items/food.dmi, SPP entries taken and renamed from cmss13-pve at https://github.com/cmss13-devs/cmss13-pve/blob/c4f882d5993d839696096c8d824d774b07d211ac/icons/obj/items/food.dmi

### /Textures/_RMC14/Objects/Consumable/Food/mre_spp.rsi

- Metadata: [Resources/Textures/_RMC14/Objects/Consumable/Food/mre_spp.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Consumable/Food/mre_spp.rsi/meta.json)
- License: CC-BY-SA-4.0
- Original declaration: Sprites by github noctyrnal

### /Textures/_RMC14/Objects/Consumable/Food/mre_tse.rsi

- Metadata: [Resources/Textures/_RMC14/Objects/Consumable/Food/mre_tse.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Consumable/Food/mre_tse.rsi/meta.json)
- License: CC-BY-SA-4.0
- Original declaration: Sprites by github noctyrnal

### /Textures/_RMC14/Objects/Consumable/Food/packaged.rsi

- Metadata: [Resources/Textures/_RMC14/Objects/Consumable/Food/packaged.rsi/meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Consumable/Food/packaged.rsi/meta.json)
- License: CC-BY-SA-3.0
- Original declaration: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/134b9b90f2d59c11f05e7e3faf06f0a971482809/icons/obj/items/food.dmi and https://github.com/cmss13-devs/cmss13/blob/95997cd3e1b745f93a49c1ce345a6b5f98a567e1/icons/obj/items/trash.dmi

## Reproduce and review

Run from the repository root:

    python Tools/three_d/author_food_extra_cloud.py
    python Tools/three_d/verify_food_extra_cloud.py
    blender -b -t 1 --python Tools/three_d/check_food_extra_blender.py

The authoring helper emits only this family's YAML, cropped textures, portable GLBs and review images. It invokes the existing builder directly and does not regenerate shared scenes/manifests or change the builder. The verification script checks current canonical YAML and exact exports, not an alternate art format.

Evidence:
- `Tools/three_d/generated/food-extra-cloud-verification.json`
- `Tools/three_d/generated/food-extra-cloud-source-audit.json`
- `Tools/three_d/generated/food-extra-cloud-blender-import.json`
- `Tools/three_d/generated/review/food-extra-cloud/food-extra-highlights.png`
- `Tools/three_d/generated/review/food-extra-cloud/food-extra-source-orbit-sheet.png`
- `Tools/three_d/generated/review/food-extra-cloud/food-extra-fixed-scale.png`
- Per-model `*-comparison.png` cards include both MRE states where applicable


## Cumulative atlas assignment
This cumulative snapshot uses centrally allocated, nonconflicting atlas slots. Any authoring-time numeric range in this historical family note is superseded by the canonical surface YAML and `Tools/three_d/generated/cloud-review/atlas-allocation-current.json`. Source IDs, original PNG pixels and modeled geometry are unchanged by atlas-index remapping.
