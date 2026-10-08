# Food, tableware and dry-ingredient sacks: source-grounded drafts

Date: 2026-10-06. Source repository: `TheHellFireo/CMU-Garrison-3D`, branch `Chip/garrison-3d`.

## Scope and status

Twelve exact prototype mappings add draft candidates for 33 Redux and nine classic saved placements. These are physical, editable assemblies; none is marked reviewed or fidelity-approved. Two bag models also contain the source-owned open state, giving fourteen static pose compositions in twelve portable GLBs. No animation clip is invented. No engine, gameplay, map transform or runtime code was modified, and nothing was published.

Canonical prototypes: `Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_food_cloud.yml`. Original print crops: adjacent `garrison_food_cloud_art.yml`; atlas indices 3800–3803. Derived print PNGs live in `Content.CMU/Resources/Textures/CMU14/ThreeD/food_cloud/`. The YAML is the editable geometry authority.

## Inspected source evidence

The actual YAML, RSI metadata and PNG pixels were fetched through the connected repository. Single-frame states here have one direction and no authored delays; both closed/open bag states therefore use the source's implicit one-second static frame. Source files remain copied without changes in the bounded working subset.

- `Resources/Prototypes/Entities/Objects/Consumable/Food/Containers/bowl.yml`: `FoodBowlBig`, default `bowl`, with five possible `fill-1` through `fill-5` states and live reagent tint. The empty source has a 24-pixel width. Fill pixels were inspected but their gameplay owner is not implemented by this batch.
- `Resources/Prototypes/Entities/Objects/Consumable/Food/soup.yml`: `FoodRiceEgg`, `FoodRicePudding`, `FoodSaladKimchi`, `FoodSoupBisque`, and `FoodSoupChiliHot`. These visibly compose `bowl` with `rice-egg`, `rice-pudding`, `kimchi`, `bisque` and `chili-hot`, respectively. The composite source references include both actual layers in source order.
- `Resources/Prototypes/Entities/Objects/Consumable/Food/Containers/plate.yml`: `FoodPlate`, `plates.rsi/plate`, 28-pixel width.
- `Resources/Prototypes/Entities/Objects/Consumable/Food/burger.yml`: `FoodBurgerBig` / `bigbite` and `FoodBurgerCheese` / `cheese` in `burger.rsi`, both 17-pixel widths. The tall burger has its own assembly rather than a scale-stretched cheeseburger.
- `Resources/Prototypes/Entities/Objects/Consumable/Food/ingredients.yml`: `FoodDoughPie` / `dough-pie`, plus `ReagentContainerFlour` and `ReagentContainerSugar`. The bags' `GenericVisualizer` maps `enum.OpenableVisuals.Opened` to their actual `*-big` and `*-big_open` states. Both states have geometry under the existing single-layer `spriteStates` contract; no new selection mechanism was added.

Original resources: `Resources/Textures/Objects/Consumable/Food/{bowl,plates,burger,ingredients}.rsi`. Source-specific mappings, layered reference paths, source alpha bounds, part counts and placement counts are in `Tools/three_d/generated/food-cloud-source-audit.json`.

## Physical construction

- The bowl has a foot and closed ceramic floor, twenty-four flared solid wall sections and twenty-four joined tubular rim sections. The basin is actually open. Neither a filled solid hemisphere nor a source-image slab substitutes for the vessel. Round plan, wall thickness, underside and depth are inferred from the single-direction source.
- Egg rice uses a connected ivory rice mound, 43 separate grains, carrot and green vegetable pieces and a small fried egg. Pudding is a continuous pale-gold heap with surface grains. Kimchi is separate folded angular leaf pieces over a low food bed. Bisque/chili have thin but positive-volume liquid discs and independently modeled morsels. The source's raised red pepper is a connected curved volume extending beyond the right rim.
- The plate has a shallow recessed eating surface, flared rim and supporting foot. Raw pie dough is a smooth, irregular flattened pastry disc; it is not a pie or finished crust.
- Burgers have separate lower bun, lettuce, meat patties, thin cheese slices/drapes and rounded top bun. Three meat layers distinguish the tall source. Bun highlight relief and concealed ingredient arrangement are inferred from the source pixels; they are not a claim of exact culinary construction.
- Bags use rounded bellies, separate paper panels, gussets, a bottom fold, rear seam and distinct closed-fold/open-neck assemblies. Tiny print and weave patches retain original pixels: label crop `(13,15)-(20,19)` and lower pattern crop `(12,19)-(21,22)`, exclusive upper bounds, from each closed original source. These four cropped PNGs are applied only to their corresponding physical front paper regions. Open-state source labels are identical in the inspected source. No full cutout sprite was extruded into a sack.

## Review and verification

`Tools/three_d/generated/review/food-cloud/food-source-orbit-sheet.png` compares each original composed sprite, a physical source-facing view and two orbit views. The source and rendered models use the same horizontal pixel-to-tile scale (32 source pixels per tile). The separate `food-overview.png` auto-fits each assembly for legibility and must not be used for relative-size measurement. `bag-states.png` shows both source bag states beside their distinct geometry. `first-bowl-preview.png` is the latest empty-bowl comparison. Every default model also has three individual review PNGs.

The generic historical `build_models.reference_frame` only returns one visible/explicit layer. It does not produce the filled bowls' two-layer reference; use the batch's original-alpha `references/Food*.png` compositions and source-audit layers for those comparisons. No renderer behavior was changed to conceal this limitation.

Each canonical model passes the existing validator and exporter under a surface registry scoped to this batch. The broader bounded working subset does not include every old vendor texture; that is why loading unrelated surfaces is inappropriate for the family check. Bounds, positive part volumes, source state/direction/timing, source mapping uniqueness and GLB structure are checked separately. Shared full-batch Khronos validation belongs to the parent export pass; see the generated verification report for exactly what was run.

## Limits

Only these inspected default food states and the two inspected bag opening states are authored. Native interaction, actual support placements and contacts, fill/tint selection, decreasing portions, eating, spills, destruction, broken bowls/plates, carried/inhand sprites and any other gameplay state are not approved. The empty reagent bowl does not claim its five live fill levels. Source-visible color and silhouette are the target, but side/back surfaces, body depth, material response and low-poly faceting remain interpretation. The models retain `status: draft` throughout.

## Attribution and license

All four original RSI `meta.json` files declare **CC-BY-SA-3.0**. These adaptations, derived crops, models and source-based previews retain that license and attribution. Original metadata is included with the source copies.

- `bowl.rsi`: metadata credits Mango Rice to SurfinNinja1; sprites taken from tgstation and modified by Swept at https://github.com/tgstation/tgstation/commit/40d75cc340c63582fb66ce15bf75a36115f6bdaa; escargot from tgstation at https://github.com/tgstation/tgstation/commit/7ffd61b6fa6a6183daa8900f9a490f46f7a81955; fills created by potato1234_x. The credit applies to the RSI collection and does not assert those unrelated states were modeled.
- `plates.rsi`: tgstation, modified by Swept at the `40d75cc` link above. Metadata additionally credits RumiTiger for muffin-tin modification and Orsoniks for plate inhand artwork; those are not used in this batch.
- `burger.rsi`: tgstation, modified by Swept and potato1234x at the `40d75cc` link above. Metadata additionally credits EmoGarbage, TurboTracker, TheShuEd and Tiniest Shark for other states and inhands; only `bigbite` and `cheese` are used here.
- `ingredients.rsi`: tgstation and baystation, modified by potato1234x; https://github.com/tgstation/tgstation/commit/c6e3401f2e7e1e55c57060cdf956a98ef1fefc24 and https://github.com/Baystation12/Baystation12/commit/a6067826de7fd8f698793f6d84e6c2f1f9b1f188. Opened flour/sugar and other bag variants are by korczoczek, based on unopened sprites. Full metadata retains credits for unrelated collection entries.

License: https://creativecommons.org/licenses/by-sa/3.0/


## Cumulative atlas assignment
This cumulative snapshot uses centrally allocated, nonconflicting atlas slots. Any authoring-time numeric range in this historical family note is superseded by the canonical surface YAML and `Tools/three_d/generated/cloud-review/atlas-allocation-current.json`. Source IDs, original PNG pixels and modeled geometry are unchanged by atlas-index remapping.
