# Remaining Garrison potted plant sources

Nineteen distinct drafts / 1,206 parts cover the remaining potted-plant variants in the configured saved-map inventory. Fourteen source types / 30 objects are on classic Garrison; the full configured maps contain 109 objects of these 19 types. This is exact draft source mapping, not completed art or live appearance coverage.

Canonical geometry is in `garrison_remaining_plants.yml`. Original planter artwork is declared in `garrison_remaining_plant_art.yml`. Do not replay scratch authoring scripts over later canonical refinements.

New geometric work is CC0-1.0 to the extent separately licensable. Source visual designs and derivative appearances retain their original licenses and attribution below. The two PNG crops retain their source licenses without alteration.

## Source distinctions and corrections

- Every model has its own source state, pot proportions and foliage arrangement. Distinct forms include a thorned cactus, two-flowered orchid, cup flowers, sparse red bud stalk, looped and spreading succulents, drooping fans, three tiers of broad leaves, narrow canes, conifers and irregular tree crowns.
- Source `noRot` and one-direction art determine the fixed facing. The `0,0.3` sprite screen offset represents the projected artwork and is not applied as world XY translation. Side/rear foliage and hidden stems are inferred; the source contains only one view.
- Round pots have tapered cylindrical walls, a hollow rim built from narrow physical strips and visible recessed soil. A first pass incorrectly covered the soil with a solid rim; this was corrected. The cactus and charcoal tree planter were changed from square to tapered round profiles after comparison.
- Overlapping leaf sections reduce disconnected beads, rear offsets give the foliage depth, and smaller outer sprays break up broad crown masses. Large leaf lobes remain simplified and need finer organic meshes/materials. The broadleaf model keeps separate upper tiers and a dense lower curtain. The four flower cups retain recessed throats.
- Front highlights follow the gray/brown planter source bands. The woven planter front and upstream blue-white planter marking use two byte-exact original crops, atlas slots 278 and 279, with solid backing walls connecting both panels to their containers. A too-thin pot highlight initially failed native quantization; its physical depth is now .0015 tiles and the full library passes the native geometry check.
- Upstream `PottedPlant0` and `PottedPlant26` have visibly different source designs from the RMC numbered plants. They have independent models: a bright-green shrub with blue-white marked gray pot, and a single yellow flower with red center. They are not generic aliases. Neither applebush source shows fruit, so no apples were invented.

## Placement and validation

All 30 classic source objects are visible in the static scene. Twenty-four rest on exact modeled table tops; six remain at floor height (#2561, #2575, #2651, #2652, #8819, #8820). The placement audit verifies unchanged saved XY/yaw for all 46,561 scene entities and unchanged model choices outside this batch. No artificial stacking or global sprite-offset translation was added. Layout and door counts are unchanged.

`generated/remaining-plants-source-audit.json` records original state hashes, directions, licenses and model-specific assumptions. `remaining-plants-crop-audit.json` records crop bounds and independent RGBA hash checks. `remaining-plants-placement-audit.json` records all 30 classic instances, supports and scene totals. All nineteen source/four-view cards are in `generated/review/`; browser captures use the `plants-` prefix. The plant-room GLB includes 271 objects and 361 floors, with no missing or inherited candidates in that crop.

The final 615-model library passes 83 isolated native checks and 103 Python tests. Deterministic export verification and Khronos validation cover every individual model and the assembled regions. The normal connected test project remains blocked by unrelated tactical-map server compilation errors. No C# or shader change was needed for this asset pass, and no new full game build was run. Connected native placement, visibility, performance and live appearance remain unverified.

Physical height/depth, rear construction, leaf undersides, fine veins, wind, growth, damage and realistic materials remain unfinished. All models remain drafts; the standard gameplay viewport is unchanged.

## Original source licenses

### /Textures/Structures/Furniture/potted_plants.rsi

- License: CC-BY-SA-3.0
- Attribution: Taken from tgstation, plant-26 made by Fazansen(https://github.com/Fazansen), inhand-left and right made by Xeri(https://github.com/Xeri7)

### /Textures/_RMC14/Structures/Furniture/potted_plants.rsi

- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/Zenith00000/cmss13/blob/5eaab0124aa46008a9f0f5e2f74a41bdfc0c315f/icons/obj/structures/props/natural/vegetation/plants.dmi

## Source states

| Model | Prototype | State | Parts | Classic objects |
| --- | --- | --- | ---: | ---: |
| CMU3DPottedPlant0 | CMPottedPlant0 | applebush | 94 | 4 |
| CMU3DPottedPlant1 | CMPottedPlant1 | pottedplant_1 | 64 | 5 |
| CMU3DPottedPlant10 | CMPottedPlant10 | pottedplant_10 | 73 | 8 |
| CMU3DPottedPlant11 | CMPottedPlant11 | pottedplant_11 | 49 | 1 |
| CMU3DPottedPlant13 | CMPottedPlant13 | pottedplant_13 | 37 | 0 |
| CMU3DPottedPlant14 | CMPottedPlant14 | pottedplant_14 | 34 | 1 |
| CMU3DPottedPlant16 | CMPottedPlant16 | pottedplant_16 | 63 | 0 |
| CMU3DPottedPlant18 | CMPottedPlant18 | pottedplant_18 | 59 | 1 |
| CMU3DPottedPlant19 | CMPottedPlant19 | pottedplant_19 | 100 | 1 |
| CMU3DPottedPlant23 | CMPottedPlant23 | pottedplant_23 | 100 | 1 |
| CMU3DPottedPlant24 | CMPottedPlant24 | pottedplant_24 | 98 | 1 |
| CMU3DPottedPlant30 | CMPottedPlant30 | pottedplant_30 | 37 | 1 |
| CMU3DPottedPlant4 | CMPottedPlant4 | pottedplant_4 | 98 | 2 |
| CMU3DPottedPlant5 | CMPottedPlant5 | pottedplant_5 | 81 | 0 |
| CMU3DPottedPlant7 | CMPottedPlant7 | pottedplant_7 | 60 | 1 |
| CMU3DPottedPlant8 | CMPottedPlant8 | pottedplant_8 | 51 | 0 |
| CMU3DPottedPlant9 | CMPottedPlant9 | pottedplant_9 | 30 | 0 |
| CMU3DUpstreamPottedPlant0 | PottedPlant0 | applebush | 51 | 2 |
| CMU3DUpstreamPottedPlant26 | PottedPlant26 | plant-26 | 27 | 1 |
