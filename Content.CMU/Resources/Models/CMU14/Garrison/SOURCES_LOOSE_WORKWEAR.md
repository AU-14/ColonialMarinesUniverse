# Loose workwear: dropped items only

Four exact mappings: RMCShoesPrisoner, RMCShoesBlack, RMCShoesBlue and AU14CivilianHazardVestKellandMiningCorporation. All source world states are static, one-direction `icon`, noRot=false, zero offset. Saved transforms remain unchanged. Equipped-FEET and equipped-OUTERCLOTHING are character clothing sprites and are deliberately not model states.

The 79 raw records contain 43 visible world items (41 Redux, 2 classic) and 36 contained shoes. Prisoner shoes account for 13 visible Redux and 32 contained records; black shoes have one visible and two contained per map variant; blue shoes have one visible per variant. All 26 Kelland vests are visible on the Redux surface. Raw overrides are Transform only, including after explicitly retaining CMUItemStain/CMItemSlots/ItemSlots in the source audit.

Sneakers preserve the original pair silhouette and source pixels: individual dark soles, descending toe sections, taller staggered ankle collars and two recessed beds at the source's dark ankle apertures. A one-pixel-wide lowered seam separates the uppers above the intact sole footprint. Collar tops are 0.170/0.188 tiles, while toe tops descend to 0.038/0.049 and sole tips to 0.014; this makes the pair legible from low views. The dropped vest is a shallow folded shell, with separate raised front panels, low central closure and shoulder straps around the original neck/arm alpha cutout. Written texture crops or exact uniform palette solids reconstruct each original RGBA frame without interpolation, recentering or filled transparent pixels. Depth, unseen sides, ankle recess depth and the folded-cloth interpretation are explicit construction inferences, not recovered 3D evidence.

The existing sourceSpriteRotates/spriteStates contract selects only the actual clean icon layer and frame. Unknown states, multiple visible layers, layer tint, altered source offset or unknown Appearance data use the original sprite fallback. Overall sprite tint remains owned by the runtime presentation adapter. CMInventorySystem changes shoe fill only if an enum.CMItemSlotsLayers.Fill layer exists; these shoe icons have no such layer, so starting hidden cash does not alter the visible shoe. CMUItemStain.color is null when clean; a non-null stain makes CMUItemStainVisualizerSystem add shader/mask layers, which remain outside the authored clean-icon contract. No stain, worn-clothing or attachment geometry is invented.

Surface placement uses existing authored support footprints and the 0.002 gap, otherwise the item remains on the floor. All 43 visible placements and their selected supports are inspected against the frozen 994 library. Source-coincident prison-shoe groups and large vest piles retain their saved pivots and therefore overlap; a reusable source-order item-pile adapter is separate work. Nearby mapped/unknown neighbors, exact duplicate identities and saved support offsets remain in loose-workwear-proof.json. No exhaustive part-contact audit or universal fit/gameplay collision claim is made for these known overlapping piles.

Reproduce only this batch with `python Tools/three_d/author_loose_workwear.py`. Use `--check --skip-reviews` to check deterministic assets. The generator does not export the global library, build native projects or launch the game/server. Source evidence: generated/loose-workwear-source-audit.json. Review: generated/review/loose-workwear/source-model-montage.png.

## Attribution

Original icons and derived crops are CC-BY-SA-3.0. Retain original attribution on redistribution.

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/36ab9674a311463bb056f7fab5fe3f3079305569/icons/obj/items/clothing/shoes.dmi, https://github.com/cmss13-devs/cmss13/blob/a15efa985114bf92a98cda275aa8319636ae5abe/icons/mob/humans/onmob/feet.dmi

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/mob/humans/onmob/clothing/suits/vests_aprons.dmi, https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/items/clothing/suits/vests_aprons.dmi
