# Garrison folded equipment and pallet placement

Five source-specific draft assemblies / 131 parts map 37 classic saved objects: 22 folded compact roller beds, five unrolled and three folded bedrolls, four portable barriers and three empty mop buckets. Canonical models are in `garrison_folded_equipment.yml`; the two existing pallet definitions in `garrison_storage.yml` also gain physical board supports. Scratch authoring scripts are not canonical regeneration sources.

New geometric work is CC0-1.0 to the extent separately licensable. Source visual design and derivative appearance retain the original licenses and attribution below.

## Source states and geometry

- The compact roller bed uses the initial Foldable folded state, with a nested upright gray frame, two handles and red retaining strap. Its .267-tile depth fits the .296875 spacing between #2777 and #2778. This upright reconstruction and unseen rear construction remain inferred; it is not rigged or animated.
- The unoccupied bedroll uses the plain unrolled layer, without the conditional pillow. Rounded end seams, piping, three padded sections and stitched channels form a physical mat. Folded bedrolls have nested padding, a curved rolled edge and front fold/strap details. Their .265-tile depth fits the .29688 spacing between #942/#943. Fine cloth deformation is unfinished.
- Portable barriers use `idle`: the prototype starts unlocked, so the red locked corner is absent. The hazard cage, inset panel, open lower grille, capped side columns, retaining bands, braces and foot latch are separate geometry. Deployment/locking behavior is not modeled.
- Mop buckets retain separate walls, a dark open interior, brown rim and gray bands. The default source fill overlay is hidden; no water surface is invented. #1250 rests on rack #2694; #1249 and #1251 remain on the floor.

## Pallet support correction

`supportSurfaces` names distinct flat Box parts as an alternative to a single `supportSurface`. Both declarations together, duplicate/ambiguous/missing labels, curved parts and connected multipart supports are rejected. Every named board contributes its actual rotated footprint. Gaps are not filled by an invisible rectangular deck. Overlapping parts of the same supporting entity select the highest top; nearest entity and stable entity-ID tie-breaking are retained.

Both existing timber pallet models declare only their five highest boards. Their top heights are .15 and .621 tiles. Grain relief was reduced to .0015 tiles so it remains below the .002 prop clearance. Checks exercise all ten board centers and all eight gaps. Folded chair #1719 now rests on pallet #7140, and rack parts #14400 on #7141. Browser top/orbit captures verify that the chair is visible above the boards.

## Map validation and remaining work

All 46,561 visible saved positions and rotations are unchanged. There are 779 supported props and 68 without an exact support. All 22 folded roller beds and all three folded bedrolls use exact rack tops. Two groups of ten roller beds share identical saved pivots (#2757–2766 and #2767–2776); these original overlaps remain and are recorded, without invented stacking. The two separately placed roller beds and folded bedrolls have positive clearance after refinement.

All five source/four-view comparisons and both pallet cards were inspected. Browser captures cover the separate roller/bedroll pairs, garage barriers, floor mats, open bucket and corrected pallet. See `generated/folded-equipment-source-audit.json`, `folded-equipment-placement-audit.json`, `review/folded-*.png` and `review/pallet-*.png`.

The models represent stable pose drafts. The subsequent [fold-state pass](SOURCES_FOLD_STATES.md) adds explicit native/offline folded and unfolded selection, with reciprocal partners and source-state comparisons. Connected multiplayer folding remains unverified. Contents, occupancy, water levels, lock/deploy/fold animation, physical materials, inferred dimensions and hidden faces remain unfinished. No model is approved and the gameplay viewport is unchanged.

## Original source licenses

### /Textures/_RMC14/Objects/Misc/Janitorial/mopbucket.rsi

- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/106c92cdf232ebc12c9d7a2feb23956c6755496f/icons/obj/janitor.dmi, https://github.com/cmss13-devs/cmss13/blob/ca94d2e8715b73103fa9f213be53d343359b4107/icons/obj/items/reagentfillings.dmi

### /Textures/_RMC14/Structures/Furniture/bedroll.rsi

- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/9dd2c0d0a1c21e3c9bddbad1321c63fd886f61cf/icons/obj/items/bedrolls.dmi

### /Textures/_RMC14/Structures/Furniture/rollerbeds.rsi

- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/rollerbed.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/medical_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/medical_lefthand.dmi

### /Textures/_RMC14/Structures/Walls/barrier.rsi

- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/b5a595ce32f9bb4fc8abde0efe21196b3862d8bf/icons/obj/objects.dmi

## Source states

| Prototype | State | Parts | Classic objects |
| --- | --- | ---: | ---: |
| CMRollerBedSpawnFolded | folded | 27 | 22 |
| Bedroll | bedroll | 16 | 5 |
| BedrollFolded | bedroll_folded | 12 | 3 |
| CMDeployableBarrier | idle | 54 | 4 |
| CMBucketMop | mopbucket | 22 | 3 |
