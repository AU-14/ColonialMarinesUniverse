# Aluminium foam wall

One exact mapping: RMCFoamedAluminiumMetal. Twenty-one saved Redux instances are present: eighteen on the surface and three on level +1. Two source sprites have -90-degree yaw; their entity poses remain rotated. UIDs 16177 and 16181 share one saved location and both are preserved. No classic placement or iron-foam prototype is claimed.

The original static one-direction metal_foam base is followed by south, east, north and west edge layers. SmoothEdge startup offsets them by (0,-1), (1,0), (0,1), (-1,0). Enabled IconSmooth with key walls and mode NoSprite hides an edge when any enabled anchored walls-key neighbor occupies that grid-cardinal cell. That visibility query is independent of sprite yaw. The portable saved adapter reads all neighboring source prototypes, including ordinary walls, not only mapped foam geometry. Native rendering samples the actual five keyed source layers and their visibility; it does not implement another smoothing system or timer.

The wall is a physical cellular mass with sixteen structural columns, paired sloping crowns and closed irregular edge lobes. Its base covers the source tile; lobes extend only where the source PNG has nonzero pixels. Every top-facing source pixel is retained, including edge PNG alpha 240 and 255. Sprite color #FFFFFFCC appears once in each part, with bakedSpriteTint preventing a second multiplication. Source pixel alpha remains in the PNG crops and is multiplied by that 0.8 opacity once. True blend appearance is approximated by the renderer's dithered solid transparency.

Top artwork, silhouette, pivot, saved transforms and all sixteen edge compositions are source evidence. Physical height, sloped cellular crowns, repeating unseen side texture and depth are authored inferences. Independent top-ray/UV reconstruction of all sixteen assemblies matches the original five-layer alpha composite byte for byte. This proves source projection, not unseen 3D correctness or a runtime clearance guarantee. Source duplicate entities may visibly overlap just as they do in the saved map. Mobs remain sprites.

The source audit records all twenty-one saved poses, source owner fields and hashes. Context proof lists original positions, grid cells, source matching neighbor IDs and nearby entities; every actual saved mask is represented in the one review montage. Unknown source owners, changed layer states/transforms, animation, shader layers and unresolvable smoothing neighbors retain sprite fallback. Portable GLB/browser views expose edges-0 through edges-15 as static states, with no animation clock.

Reproduce this family only: python Tools/three_d/author_foam_wall.py. Deterministic check: --check --skip-reviews. Evidence: generated/foam-wall-source-audit.json and generated/foam-wall-proof.json. Review: generated/review/foam-wall/source-model-montage.png. Whole-library export and native execution are coordinated separately.

## Attribution

Original sprites and derived crops: CC-BY-SA-3.0. Preserve attribution on redistribution.

Taken from https://github.com/discordia-space/CEV-Eris/blob/81b3a082ccdfb425f36bbed6e5bc1f0faed346ec/icons/effects/effects.dmi, foam_directionals by brainfood1183 (github)
