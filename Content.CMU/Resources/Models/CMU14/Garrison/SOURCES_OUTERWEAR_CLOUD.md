# Civilian dropped outerwear cloud drafts

## Scope and status

Thirteen source-specific **draft** assemblies represent only the ordinary dropped/world `icon` of these civilian outer garments. They do not model actors or worn/held clothing. Live characters and their clothing remain sprites. All GLBs are direct, unmodified bytes from the existing `Tools/three_d/build_models.py` exporter, generated from the two canonical YAML files `garrison_outerwear_cloud.yml` and `garrison_outerwear_cloud_art.yml`.

The source icon is interpreted as clothing laid out face-up on a surface. Its original 32-pixel icon guides the horizontal and longitudinal proportions at one tile per 32 pixels; smoothed cloth edges may differ by less than a source pixel. Cloth thickness, underside folds and pocket depth are inferred. Clothing is made from individually named cloth faces, rounded fold volumes, sleeves, hollow cuff edges, collars, closures, hems and pockets. There is no whole-sprite card, solid alpha-mask extrusion, per-pixel voxel body, human mesh, rig, or actor attachment. Three puffer colors and four parka colors share source-confirmed identical cuts with independently sampled original palettes.

The inventory records 27 Redux and 23 classic instances of these exact prototype types. These are source inventory counts, not evidence that maps, loaders, live entities or actor clothing have been integrated or verified.

## Source retrieval and ownership

Sources were read on 2026-10-06 from `TheHellFireo/CMU-Garrison-3D`, ref `Chip/garrison-3d`, using GitHub `fetch_file` (base64 for PNGs). Original `icon.png` and complete `meta.json` files are retained under `Content.CMU/Resources/Textures/CMU14/Clothing/Civilian/Jacket/<resource>.rsi/`. The adjacent review proof records SHA-256 checksums, complete palettes, source bounds, RSI states, full original copyright text and source inventory counts.

Definitions inspected:

- `Content.CMU/Resources/Prototypes/CMU14/Entities/Clothing/Civilian/jackets.yml` supplies the exact Sprite and Clothing resource for each of the thirteen prototypes
- `Resources/Prototypes/_RMC14/Entities/Clothing/OuterClothing/coats.yml`: `RMCBaseJacket` and `RMCAllowSuitStorageClothingHazardVest`, as well as the separate buttonable base for comparison
- `Resources/Prototypes/Entities/Clothing/OuterClothing/base_clothingouter.yml`: `ClothingOuterStorageBase` inherits `ClothingOuterBase`, which owns `Sprite.state: icon`
- `Resources/Prototypes/Entities/Clothing/base_clothing.yml`: ordinary clothing base
- `Resources/Prototypes/_RMC14/Entities/Clothing/Accessory/base.yml`: `RMCBaseUniformAccessoryItemOuterClothing` and `RMCBaseUniformAccessoryItemBase`

Every assigned jacket uses the ordinary `RMCBaseJacket`, not the separate `RMCBaseJacketButtonable`. No child selects icon-open or Foldable behavior. The inspected inherited definitions contain no default Sprite color override for these garments, and the inventory resolves ordinary `icon`, Items draw depth and `noRot: false`. The authored colors already come from each original icon; no new runtime tint owner is added. The GLBs contain no animation clips.

`RMCBaseJacket` adds `CMUItemStain` with `wornStates.outerClothing: jacket_blood`. The accessory parents add `UniformAccessoryHolder` and accessory categories. Stains, blood, attached accessories, storage contents, dynamically changed tint, in-hand layers, worn layers, and any gameplay-selected composition remain explicitly unsupported by these art mappings. Most metadata also contains the four-direction `equipped-OUTERCLOTHING` state; its availability is not a claim that it was modeled. Only the one-direction, one-frame `icon` was authored.

## Source-specific geometry

- `AU14CivilianTanTrenchCoat` / `tantrenchcoat.rsi`: tan long coat, dark turned neck opening, broad diagonal lapels, belted waist, small original grey belt fastener, split tails, tapering lower corners and sloping cuffed sleeves
- `AU14CivilianBrownTrenchCoat` / `browntrenchcoat.rsi`: identical source cut with the original four brown colors and cloth-colored source belt closure; no invented silver fastener
- `AU14CivilianJacketBomberJacket` / `bomberjacket.rsi`: short plum-brown bomber, pale shearling-like collar and twin closure facings, pale ribbed bottom hem/cuffs and inset pockets
- `AU14CivilianJacketGrayPufferJacket` / `graypufferjacket.rsi`: blue-grey quilted sides, bright shoulder baffle, very dark broad center closure, compact hem and cuff openings
- `AU14CivilianJacketKhakiPufferJacket` / `kahkipufferjacket.rsi`: source spelling is `kahki`; cream/khaki baffles and dark brown center closure
- `AU14CivilianJacketOrangePufferJacket` / `orangepufferjacket.rsi`: source orange-brown channels and sleeves with brown center closure; the geometry follows the identical source cut
- `AU14CivilianJacketBlueParka` / `blueparka.rsi`
- `AU14CivilianJacketYellowParka` / `yellowparka.rsi`
- `AU14CivilianJacketGreenParka` / `greenparka.rsi`
- `AU14CivilianJacketRedParka` / `redparka.rsi`: the four parkas retain each exact source palette, longer rounded front panels, gathered waist, two raised lower patch pockets, narrow front closure and the shared ochre V-shaped fur neck trim, now carried by continuous cloth backing
- `AU14CivilianJacketGrayPufferVest` / `graypuffervest.rsi`
- `AU14CivilianJacketTanPufferVest` / `tanpuffervest.rsi`: narrow sleeveless open forms with angled shoulder fronts, bound edges and four flattened joined quilt bands per side. A narrow, low folded rear-waist connection is inferred to join both front leaves into one physical prop; this adds a small line within the otherwise transparent source center
- `AU14CivilianJacketSnowSuit` / `snowsuit.rsi`: the definition calls this a Snow Jacket. It is modeled as the white jacket icon with darker turned collar, grey waist bindings, flared insulated lower skirt and cuff edges. No trousers, person or complete worn suit is invented

Each curved fold is a modest physical interpretation of a low-resolution painted feature. Source colors, broad silhouettes and semantic seams are preserved; high-resolution tailoring, fabric weaving, true sewn topology, inner lining construction and exact cloth curvature are unfinished. Some folds remain visibly primitive. No fidelity approval is claimed.

## Source artwork and attribution

All thirteen RSI metadata files declare **CC-BY-SA-3.0**. Keep those complete files and this note with redistributed models or derived textures. Attribution in those files names the original cmss13-pve sources, including:

- https://github.com/cmss13-devs/cmss13-pve/blob/167ac892a2df83180b3ea0905984cfe5702f9a95/icons/obj/items/clothing/suits.dmi
- https://github.com/cmss13-devs/cmss13-pve/blob/cdfdf986686fc69df2b7ce7fa258a32462c2f479/icons/mob/humans/onmob/suit_0.dmi
- Additional source revisions and files are preserved verbatim in each original metadata file and the JSON proof; use the per-resource metadata as the authority

Derived geometric interpretations and twelve tiny exact source-detail crops are provided under the same CC-BY-SA-3.0 license. The crops contain only two trench fasteners, the bomber center closure, eight small parka pocket faces, and the snow-jacket belt clasp. Every crop is at most nine source pixels. They live in `Content.CMU/Resources/Textures/CMU14/ThreeD/outerwear_cloud/` and use atlas indices 3070–3081; indices 3082–3099 remain unused. No crop contains an entire garment or supplies its overall silhouette.

## Reproduction and evidence

From the repository root:

1. `python Tools/three_d/author_outerwear_cloud.py` writes canonical YAML, exact detail crops, direct exporter GLBs, source-facing/top and two orbit previews, and the complete source proof
2. `python Tools/three_d/verify_outerwear_cloud.py` checks direct GLB byte reproducibility, exactly 13 unique references, draft status, source palette membership, one-frame icon ownership, crop bytes, unique atlas occupancy, image readability, GLB buffer ranges, finite accessor values, declared accessor bounds and index ranges
3. `blender -b --python Tools/three_d/verify_outerwear_blender.py` independently imports all thirteen final GLBs. Results are in `Tools/three_d/generated/cloud-review/outerwear/blender-import-validation.json`; import success and mesh sanity do not establish physical fidelity

The main review directory is `Tools/three_d/generated/cloud-review/outerwear/`. Each model has a comparison card showing the original icon and three model views at a common 640-display-pixels-per-tile scale. Original source pixels are nearest-neighbor enlarged. `outerwear-all-source-and-orbit.png` collects all full cards without silhouette cropping. `source-and-geometry-proof.json` retains source hashes and measurements; `offline-verification.json` and `raw-glb-accessor-verification.json` state precisely what the offline checks covered.

The Khronos glTF validator package is not installed in this environment and was **not run**. No browser/native renderer, game client, saved-map contacts, atlas integration, runtime admission, actor mapping, multiplayer appearance, cloth animation or gameplay test was performed. There are no engine/runtime edits, launches, published changes or GitHub writes in this batch.

## Same-asset collar and quilt refinement

Independent review identified a floating-bead appearance in the first parka collar and a detached-strip / pill-like reading in the vests. Conservative AABB checks alone did not prove disconnection. The parka trim is now a low continuous V-shaped fur roll with actual continuous cloth backing beneath it. The vest keeps its original rear-waist connection and shoulder underfolds; only the raised baffles were lowered and broadened into joined horizontal quilt bands. No extra rear bridge was duplicated, and the source-facing center opening was not filled. The narrow pre-existing inferred rear-waist seam remains the explicit source deviation described above.

`diagnose_outerwear_connections.py` reads positions and node transforms directly from the GLBs. It computes each convex exported mesh's face planes and maximizes the radius of a common inscribed sphere for candidate intersecting pairs. Only strictly positive radii greater than 1e-7 tile create contact edges. This goes beyond conservative AABB overlap. The representative blue parka's fourteen neck/collar/yoke/front-face parts form one positive-volume contact component. All thirty-nine parts of the representative tan vest form one such component. The other parka colors and grey vest have identical relevant geometry, verified in `refinement-geometry-equivalence.json`. These proofs establish physical part overlap for those scopes, not sewn topology, a whole-model boolean/manifold guarantee, full tailoring, or native behavior.

Each representative has an `-connection-diagnostic.png` showing ordinary final art, a support cutaway and its underside. Gold highlights collar backing or the existing waist join; cyan highlights neck/shoulder underfolds. Diagnostic colors and removed top detail are temporary image annotations and do not change the GLBs. `refinement-before/` retains the superseded representative comparison cards for review history; they are not current art. No already-created delivery archive was edited.


## Cumulative atlas assignment
This cumulative snapshot uses centrally allocated, nonconflicting atlas slots. Any authoring-time numeric range in this historical family note is superseded by the canonical surface YAML and `Tools/three_d/generated/cloud-review/atlas-allocation-current.json`. Source IDs, original PNG pixels and modeled geometry are unchanged by atlas-index remapping.
