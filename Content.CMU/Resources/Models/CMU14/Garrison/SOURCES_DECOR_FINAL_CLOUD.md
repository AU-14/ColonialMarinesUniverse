# Final small decor: editable physical draft assets

Source checkpoint: TheHellFireo/CMU-Garrison-3D, branch Chip/garrison-3d, commit `6e37a4d0a7d9433838c393a82c02422cb704dd5a`. Source and actual pixels inspected 2026-10-06. Every model remains draft.

## Deliverable and scope

10 assemblies: 8 exact prototype designs and 2 unbound planted-flag frame-zero construction studies. These source IDs account for 8 Redux and 6 classic entries in the supplied historical inventory. Every target is present in `physicalModelingFamilies`; counts are inventory entries, not verified live placements. Canonical definitions are `garrison_decor_final_cloud.yml` and `garrison_decor_final_cloud_art.yml`. The 24 original crop PNGs use all and only decor_final slots 2894–2899 and 2972–2989; the intentional gap is retained. No earlier asset, engine, runtime, map, game process or exporter implementation was changed. Nothing was pushed or published.

## Physical construction and exact source limits

- **UA / UPP loose flags:** exact inherited icon, one visible base layer. Diagonal round pole, finial and ferrule, with four shallow original-art cloth bands following the source hoist and cloth alpha contour. Cloth itself really is a thin object; it is backed only where the source cloth exists. Source printed colors, including the UA white/black fields, are all retained. The world icon is presented as a loose resting object, not a character holding pose. The existing default-only source adapter rejects deploy and nonzero sprite offsets.
- **Planted flag studies:** separate unbound designs use deploy frame0, a vertical round pole, source foot and thin contour-backed cloth. The source deploy state has four 0.5-second frames; these are inventoried, not animated or bound. PlantableFlagSystem sets sprite offset (0,0.5) while planted and restores zero on removal. Dynamic planting/offset selection, cloth wind, hand sprites, character rigs and four-direction in-hand states are unsupported. No invented flapping clip is exported.
- **UPP wall flag:** distinct three-section draped sheet with shallow fold depth, exact printed emblem and contour, and small rear suspension tabs/pins at the actual upper corners. The lowered middle of the top edge and rounded lower edge are open geometry, not a filled rectangular board. Source 96px canvas preserves the half-tile left-of-pivot visible center. Wall height, rear pins and cloth reverse are inferred. Fixed uppflag only; worn/destruction states unsupported.
- **Johnny Wayne Jr. cardboard:** original entire printed face and exact die-cut perimeter, backed by 17 solid contour rectangles plus a narrow folding rear easel. The two source-transparent openings near the arms remain real holes. The brown cardboard web between the printed legs is opaque in the actual source and is correctly retained. The easel has a sampled real triangular opening. This is a printed display prop, not a human/character rig. Metadata declares a 32×32 icon; the PNG is 32×64 with a fully transparent extra row. Only the declared first frame is used.
- **Maintainers memorial:** full solid slab and continuous shallow base/plinth, two individual engraved face plates, reflective upper header, metal divider, edge mouldings and rear ribs. All original source facade pixels are retained across three exact crops. The 96px canvas places the visible two-tile slab half a tile left of pivot. Inherited Transform.noRot=true is respected. No inserted dogtags, readable names, UI or reflection simulation is invented.
- **Powerloader pamphlet:** very thin original printed sheet with exact contour backing, lower fold lip and separate real open retaining-wire loop. The grayscale clip is structural rather than a transparent-texture pretend hole. No inside text, pages, use/consumption animation or skill behavior is implemented.
- **No Smoking 2:** original complete printed sign face with thin contour backing and small rear attachments. Source-clipped corners remain clear. Reverse and mounts are source-palette inferred materials; no broken sign state is modeled.
- **Skeleton with cigarette painting:** source painting9 is divided exactly into recessed painted canvas and four raised source-textured frame rails. A solid rear board and connected stretchers supply physical construction. Smoke in the printed picture is part of the unchanged painting, not an effect. No new smoke animation or character geometry.

## Verification

- 36 pinned original files match their Git blob hashes. Full inherited component chains resolve with no missing ancestor; actual Sprite, Transform, GenericVisualizer, WallMount and relevant behavior owners are recorded.
- All 24 crops retain exact original RGBA pixels; all untextured RGB colors belong to the corresponding original source palette. All 24 crop backing masks pass opaque and transparent pixel-center tests against exported solid hulls.
- Structural contact checks inspect actual exported default-visible GLB mesh vertices and transforms. Textured rectangular faces are excluded from the connectivity graph, so they cannot hide a missing physical joint. All ten structural assemblies have one positive-volume connected component. Selected cardboard arm openings, rear easel triangle and pamphlet retaining loop pass true-solid void probes.
- Part budget: 22 maximum editable parts per pose (limit128). The two flag icon models have an additional identical default source-state scene; this is not an animation. All exports have zero animation clips.
- Unchanged-exporter output is deterministic; binary lengths, accessor bounds, finite coordinates and indices pass. Materialized build_models.py differs from the pinned source solely by its previously present extra terminal newline; it was not edited.
- All 10 final GLBs pass independent Blender 4.3.2 import, finite mesh checks and zero mesh repair. Blender imports all scene nodes for the flags; the raw GLB audit separately proves default-visible parts. This is not Khronos glTF validation.
- Source-facing, orbit, actual reverse/underside and fixed-scale comparisons are retained. Physical depth, unseen backs, wall height, material behavior, edge smoothness and support fitting remain inferred drafts. No native, map-contact, live state, whole-model manifold, gameplay collision or fidelity approval is claimed.

## Reproduction and review

Run `python Tools/three_d/author_decor_final_cloud.py`, then `python Tools/three_d/verify_decor_final_cloud.py`, `blender -b -t 1 --python Tools/three_d/check_decor_final_blender.py`, and `python Tools/three_d/document_decor_final_cloud.py`. The exporter and its dependencies are original shared tools, not replacement engine code. Family proof, crop ledger, full source audit, contact witnesses, independent import report and all review images are under `Tools/three_d/generated/cloud-review/decor-final/`.

## Exact references and attribution

### AU14FlagCarriableUA
- Original owner: `Content.CMU/Resources/Prototypes/CMU14/Entities/Objects/Tools/USCM/flag.yml`
- Ancestors: RMCFlagCarriableBase, BaseItem, CMCorrodible
- RSI: `/Textures/CMU14/Objects/Flags/ua_flag.rsi`
- License: CC-BY-SA-3.0
- Copyright/attribution: taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/structures/plantable_flag.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/items/items_righthand_64.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/items/items_lefthand_64.dmi

### AU14FlagCarriableUPP
- Original owner: `Content.CMU/Resources/Prototypes/CMU14/Entities/Objects/Tools/UPP/flag.yml`
- Ancestors: RMCFlagCarriableBase, BaseItem, CMCorrodible
- RSI: `/Textures/CMU14/Objects/Flags/upp_flag.rsi`
- License: CC-BY-SA-3.0
- Copyright/attribution: taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/structures/plantable_flag.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/items/items_righthand_64.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/items/items_lefthand_64.dmi

### AU14L4D2cardboardcutoutjohnnywayne
- Original owner: `Content.CMU/Resources/Prototypes/CMU14/Entities/Structures/Misc/cardboard.yml`
- Ancestors: BaseStructureDynamic, BaseStructure, CMCorrodible
- RSI: `/Textures/CMU14/Structures/cardboard.rsi`
- License: CC-BY-SA-3.0
- Copyright/attribution: Made by gixer94 for the Aliens-For-Dead event.

### AU14WallFlagUPP
- Original owner: `Content.CMU/Resources/Prototypes/CMU14/Entities/Structures/Misc/wallflags.yml`
- Ancestors: AU14WallFlagBase, BaseSign, BaseWallmountMetallic, BaseWallmount, BaseSignIndestructible, StructureHealthFurniture, BaseStructureHealth
- RSI: `/Textures/CMU14/Structures/wallflags.rsi`
- License: CC-BY-SA-3.0
- Copyright/attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/wall_decorations/banners.dmi

### CMMemorialMaintainers
- Original owner: `Resources/Prototypes/_RMC14/Entities/Structures/Furniture/memorial.yml`
- Ancestors: CMMemorialBase, Memorial, BaseStructure, CMCorrodible
- RSI: `/Textures/_RMC14/Structures/Furniture/memorial.rsi`
- License: CC-BY-SA-3.0
- Copyright/attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/ec4ab3ab5734c5388a95f723d1c98b178519d0ae/icons/obj/structures/props/almayer_props64.dmi and cmss13-pve at https://github.com/cmss13-devs/cmss13-pve/blob/master/icons/obj/structures/props/almayer_props64.dmi

### CMPamphletPowerloader
- Original owner: `Resources/Prototypes/_RMC14/Entities/Objects/Misc/pamphlets.yml`
- Ancestors: CMPamphlet, BaseItem, CMCorrodible
- RSI: `/Textures/_RMC14/Objects/Misc/pamphlets.rsi`
- License: CC-BY-SA-3.0
- Copyright/attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/3a94024f59bfd350ecf02c511a7d99d597da3d6c/icons/obj/items/pamphlets.dmi, pamphlet_honorguard.png made by CatAndHats (github) based on pamphlet.png

### CMPosterNoSmoking2
- Original owner: `Resources/Prototypes/_RMC14/Entities/Structures/Wallmounts/Signs/posters.yml`
- Ancestors: CMPosterNoSmoking, CMPosterBase, BaseSign, BaseWallmountMetallic, BaseWallmount, BaseSignIndestructible, StructureHealthFurniture, BaseStructureHealth
- RSI: `/Textures/_RMC14/Structures/decals.rsi`
- License: CC-BY-SA-3.0
- Copyright/attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/wall_decorations/decals.dmi, roplaque by github noctyrnal

### PaintingSkeletonCigarette
- Original owner: `Resources/Prototypes/Entities/Structures/Wallmounts/Signs/paintings.yml`
- Ancestors: PaintingBase, BaseSignWeak, BaseSignIndestructible, BaseWallmount, StructureHealthFurnitureWeak, BaseStructureHealth
- RSI: `/Textures/Structures/Wallmounts/paintings.rsi`
- License: CC-BY-SA-3.0
- Copyright/attribution: Created by EmoGarbage, Painting 17 and the Among Us IP is used in respect with the policy at https://www.innersloth.com/fan-creation-policy/, painting 19 was taken from vg station at commit https://github.com/vgstation-coders/vgstation13/commit/af32149e4e9e4ccb1edcbc36d9a1f72b05a6c66c
