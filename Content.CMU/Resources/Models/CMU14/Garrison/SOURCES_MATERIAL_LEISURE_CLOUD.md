# Material and leisure physical drafts

Art-only work against TheHellFireo/CMU-Garrison-3D, pinned commit `6e37a4d0a7d9433838c393a82c02422cb704dd5a`, inspected 2026-10-06. The 20 requested source IDs are all in the supplied `physicalModelingFamilies` and Redux inventory. Their historical counts are **32 Redux / 17 classic entries**. These counts are not verified visible placements, map-fit coverage, supported live states or approval.

Canonical definitions are `garrison_material_leisure_cloud.yml` and `garrison_material_leisure_cloud_art.yml`. This family contains 19 editable default draft assemblies covering 20 exact IDs; both empty-sandbag counts deliberately share one design. The four source orb frames are documented in a separate reference strip, not counted as additional models. Eight original cropped PNG surfaces use the exact allocated sequence 2382–2389 from the material_leisure reservation. No other atlas range is used. No earlier model, runtime/engine, map, server or publication files are changed.

Reproduce with `Tools/three_d/author_material_leisure_cloud.py`, using the unchanged `build_models` validator, renderer and GLB exporter. The bounded checks are `verify_material_leisure_cloud.py`, `check_material_leisure_contacts.py` and `check_material_leisure_blender.py`. Review images, source/default audit, source receipts, crop hashes and verification reports are under `Tools/three_d/generated/cloud-review/material-leisure/`. `overview.png` shows the family; every model has source-facing, orbit, rear-orbit and fixed-scale images. Piano and tom drums also have all four source-direction comparisons. Fixed-scale panels use **32 source pixels = one tile**, with separate centering; they do not claim pivot or map placement equality. Other comparison panels are independently auto-framed and explicitly labeled.

## Exact default scope and source interpretation

- **CMSheetGlass:** Stack count50 / max50, four states. Initialized default is `glass_4`. Four separate thin glass sheets follow the source visual convention; visible sheets do not equal inventory count. Original partial-alpha appearance is only approximated with flat translucent colors. Refraction, reflection and changing stacks are unsupported.
- **CMSheetPlasteel50:** count50 / max50 selects `plasteel_4`, despite the child's initial `Sprite.state: plasteel_2`. Four physical sheets retain small original reflective upper-face crops. No live StackVisuals adapter is added.
- **CMSheetPhoron:** count50, default `phoron`. Unlike wood/glass/plasteel, this source does not supply count-dependent LayerStates. One solid mineral bar with beveled planes and a small original upper-face crop preserves the icon convention. Material/reagent effects are unsupported.
- **CMRodMetal1:** count1 / max60 selects `rods`. The `rods` and `rods_5` PNGs have the same Git blob hash and both depict multiple short sticks. Four thin physical rods preserve that source convention; they are not four inventory units. Live count/hide state selection is unsupported.
- **RMCPlankWood:** count50 / max50 selects `wood_4`. Four solid overlapping planks retain source upper-grain crops.
- **RMCPlankWood25:** count25 / max50 selects index2, `wood_3`, via the actual source rounding function. It does not retain the initial inherited `wood_4` after appearance initialization. Three physical planks follow that source artwork. Count changes/consumption are unsupported for both wood variants.
- **CMSandbagEmpty10 / CMSandbagEmpty50:** counts10 and50 share the same `sandbag_stack` layers and no count LayerStates. The shared design has folded cloth volumes, seams and an upper fold. An empty source `acided` layer is not treated as a finished acid state. Filling, acid and other dynamic overlays are unsupported.
- **CMSandbagFull5:** count5 uses `sandbag_pile`, with three overlapping filled shapes and tied seams. The source defines no stack LayerStates here. Construction, emptying and acid overlays are unsupported.
- **BarbedWire15:** count15 / max20, three states, selects index2 `barbed_wire_3`. The raw child `Sprite.state: barbed_wire` is not the initialized mapped base layer. A connected four-turn helix, returning upper wire and attached sharp barbs leave real through-space. Hidden wire route, section thickness and coil pitch are inferred. Only this default amount is mapped; installation, count changes, hiding and tint states are unsupported.
- **RMCIceCrystal:** default `icon_1` only. Dark blue-green irregular solid masses with attached mineral facets follow the actual lumpy source silhouette. `icon_2` through `icon_8` are resource alternatives, not modeled states of this ID. Transparency/refraction is not invented.
- **Basketball:** resting `icon`, a curved orange body with three separate panel-seam loops. Source in-hand animation, bouncing, holding and deformation are outside this model.
- **ChessBoard:** 18×18 world `chessboard` icon. Solid board, 64 inlaid squares and 15 small raised white/black pieces follow the icon's scattered miniature arrangement. The tabletop UI owns separate gameplay pieces and positions; this static world model does not follow that board game.
- **ToyRubberDuck:** resting `icon`, with physical body, neck, head, folded wings, tail, beak and eyes. Secret stash, helmet/in-hand views and sound are unsupported.
- **PonderingOrb:** `icon` has four 0.2-second source frames. Pale highlights are fixed; frame1 changes the blue rim color, and frames2/3 add larger partial-alpha halos. One shared structural draft approximates frame0. Pale source-pixel runs form shallow attached colored details on the sphere. `orb-source-frame-limits.png` shows all four real source frames. Rim/halo changes, unshaded rendering, PointLight, tint, emitted illumination and animation selection remain unsupported. No duplicate structural assemblies are counted as additional frame coverage.
- **PianoInstrument:** `piano`, four source directions. Connected horizontal cabinet/lid sections preserve the asymmetric grand-piano tail; individual keys, support legs and pedals retain actual free space below. Hidden cabinet thickness, pedal mechanism and underside remain inferred. No invented open lid, pressed keys, broken state, musical action or audio binding. The stepped supported-primitive contour remains a draft approximation.
- **TomDrumsInstrument:** `toms`, four directions. Two actual hollow lower shells with separate skins, perimeter rims, tension rods and a tripod. Source direction-specific head/underside shading and drum tilt are approximated. No played animation or audio is provided.
- **RMCBarrelPileYard:** `pile_0`, one 96×96 source frame. Eighteen separate capped drums interpret visible and partly obscured cap groups, with source label crops on shallow attached tangent faces. The source's non-centered collider envelope is `-0.5,-0.5,2.4,2.5`. The inferred positive-quadrant model footprint was adjusted locally to avoid intersecting drum shells; it is not map-fit proof and is not a copy of the screen-space `Sprite.Offset: 1,1`. Barrel arrangement, hidden drums, body depth, labels on curved paint, burning/destruction and context contacts need further review.
- **RMCBasePropCasing4:** `cartridge_4`. Ten individually scattered hollow brass cases, each built from separate shell segments and a closed base, leave real mouths and bores. Their tiny section thickness, unseen base/primer and scatter depth remain inferred. Other cartridge variants and ejection are unsupported.
- **RMCRollingPin:** child supplies the RMC RSI; `RollingPin` in `Objects/Tools/tools.yml` supplies `state: icon`. A cylindrical roller and coaxial thinner handles follow the diagonal icon. Cooking, melee, equipped/in-hand and tool animation are unsupported.

## State-owner evidence

The actual prototype files, visual parents and source metadata are preserved under `reference/material-leisure-source/`. The audit reads `Content.Client/Stack/StackSystem.cs`, `Content.Client/Storage/Systems/ItemCounterSystem.cs`, `Content.Shared/Stacks/SharedStackSystem.cs`, `StackComponent.cs`, and `Content.Shared/Rounding/ContentHelpers.cs`.

The source `OnAppearanceChange` uses `StackVisuals.Actual`, `MaxCount` and `Hide`; non-composite layers go through `ProcessOpaqueSprite` and `RoundToEqualLevels`. `LayerStates` defaults to an empty list in `StackComponent`. The half-full wood and three-quarter barbed defaults therefore differ from their raw initial sprite declarations. Source max20 for wire comes from `_RMC14/Stacks/comtech_stacks.yml`; rod max60 comes from `_RMC14/Stacks/Materials/parts.yml`. The source-default report discloses unfetched ancillary nonvisual ancestors; this is not a complete engine composition audit. The supplied inventory independently supplies the existing effective sprite/resource references.

No model in this family adds `spriteStates`, animation clips, state links or a new adapter. The four orb source frames remain reference-only for the unsupported glow changes. Source count fields and dynamic owners remain unchanged. Mapping a default draft must not be interpreted as support for every gameplay state.

## Bounded geometry and format verification

The proof records deterministic direct export bytes, unique names and mappings, part counts, finite raw GLB accessors, index/buffer bounds, source metadata directions, exact original crop bytes and pinned Git blob hashes. The original exporter is used without edits.

The contact audit tests exact transformed convex primitive mesh hull intersections with a 0.000015-tile tolerance. Every candidate pair in these isolated models is checked. Assembled props are connected; the 18 barrels and ten spent cases deliberately remain separate pieces, as does one loose rod beside the other rod group. Sheet/plank stacks and cloth folds make actual geometric contact. All model and intentional disconnected-piece-group mesh minima are at z=0. Sampled mesh halfspace tests check the wire center, upper loop, drum lower cavities, drum skin, piano underside, board support and a casing bore. These checks do not establish load-bearing, full ray-space clearance, collision behavior or map-neighbor fit.

Independent Blender import examines final exported bytes, finite vertices and mesh validation repair counts. It is separate from the repository exporter. Neither that import nor raw accessor tests are Khronos conformance validation. No native or full-map check is claimed. All assemblies remain **draft**, and all hidden construction, physical materials, source projection interpretation, support placement and first-person fidelity need review.

## License and source attribution

All incorporated source RSI metadata declares **CC-BY-SA-3.0**. Keep these notices and the original `meta.json` files with derived art; the proof includes full notices, original paths, source blob hashes and source URLs. Source-inspired geometry and cropped artwork retain the applicable attribution/share-alike obligations.

- Material rods, wood, plasteel and phoron: CM-SS13 `icons/obj/items/stacks.dmi`, commit `1719ee43f983ae46841fcdd636bf9bc4c80706d0`
- Glass: CM-SS13 `icons/obj/items/items.dmi`, commit `106c92cdf232ebc12c9d7a2feb23956c6755496f`, modified by Hyenh#6078
- Sandbags: CM-SS13 `icons/obj/items/marine-items.dmi`, commit `32cb5892413243cc74bb2d11df8e3085f8ef1164`
- Barbed wire: VictorJob
- Ice: CM-SS13 `icons/obj/structures/props/ice_colony/props.dmi`
- Barrel yard: CM-SS13 `icons/obj/structures/props/ice_colony/barrel_yard.dmi`, commit `7cb618c69b75873f3ce893022fe08d1233b3152d`
- Spent cases: cmss13-pve `icons/obj/items/casings.dmi`
- Rolling pin: CM-SS13 `icons/obj/items/kitchen_tools.dmi`, commit `0525b5ada7da1afcd9b260e76d5fea01500d9c8d`
- Basketball: tgstation commit `e1142f20f5e4661cb6845cfcf2dd69f864d67432`
- Chessboard: Visne
- Rubber duck: tgstation commit `4f6190e2895e09116663ef282d3ce1d8b35c032e`, modified by Swept
- Pondering orb: Pancake
- Piano/tom drums: vgstation13 commit `8d9c91e19cb52713c7f7f1804c2b6f7203f8d331`; full mixed-RSI attribution is retained without attributing unrelated instrument states to these models

## Integrated ice-crystal refinement

The combined-library review replaced the six rounded ellipsoids in `CMU3DMaterialLeisureIceCrystalCloud` with six angular clasts made from closed box cores and paired beveled wedges (18 parts). The source remains `RMCIceCrystal`, `icon_1`; colors were sampled from that sprite. The reverse and underside are solid and remain inferred. This supersedes the rounded geometry in the original batch evidence, not its source attribution or draft status.
