# Garrison industrial machinery and discarded props

20 draft assemblies / 623 editable parts, explicitly mapping 65 classic saved objects. All remain drafts. Canonical geometry is in `garrison_machinery_debris.yml`; scratch authoring/refinement scripts are not regeneration sources.

Geometric contributions are CC0-1.0 to the extent separately licensable. Original pixels and derivative appearance retain their respective source licenses and attribution below. The N14 debris source has a noncommercial license; keep its attribution with those exports.

## Reconstruction and placement

Six industrial designs retain their distinct fan banks, louvres, grilles, controls, overhead duct and separate cabinet sections. Cabinet depth and duct height were reduced after source comparisons. Circular housings use capped cylinders; scaling preserves round faces. Big2 and Big8 extend from the left section's pivot over two tiles. Big11 has a 90-degree authored axis correction: its two saved control units face east into the room, with clearance from the west and south walls. The other five designs follow their one-direction noRot source facing even when a saved entity has nonzero rotation. None changes the simulation transform or collider.

The fourteen debris designs retain distinct book, carton, bottle/can, drum and refuse-bag groupings. Fallen barrels lie along local Y with their end caps facing the source front. Source-specific green/brown bottles retain their relative heights and labels. Bags use slumped rounded forms, knots and narrow creases. The 32-by-48 sprite's upper padding represents screen elevation, not ground displacement. The ground composition is centered on its tile; only oversized planar footprints were reduced to fit within 0.44 tiles of the pivot. Relative piece arrangements are retained. `generated/debris-pivot-refinements.json` records those changes.

All 65 saved translations/rotations are unchanged. Conservative world-space part bounds find zero intersections with neighboring walls after pivot correction. Seven saved debris objects already occupy the same tile as a rock border: #7107, #7135, #7184, #7185, #7203, #7205 and #7217. Their source uses FloorObjects draw depth (Default-13), while rock borders use Walls (Default-2), so wall art already covers them. Those saved overlaps are preserved; no support elevation or alternate map location is invented. Duplicate debris at the same saved pivot is also retained. See `generated/machinery-debris-placement-audit.json`.

Big9's green monitor uses the original 12-by-8 RGBA crop from frame zero of `buildingventbig9.png`, pixel rectangle [29,42,41,50]. Surface `CMU3DMachineryArtGreenMonitor` uses atlas slot 238, with no resampling or recoloring. Its source-pixel hash is recorded in the placement audit.

## Review and limits

All twenty source/four-view comparison cards were inspected. Browser context checks covered the machine row around #12024, paired unit #12023, east-facing control #12021, barrels/books #7109, bottles #7133 and the bag group around #7218. Captures are in `generated/review/machinery-*.png` and `debris-*.png`.

Heights, cabinet backs/interiors and physical material response are inferred. Machine animation, detailed corrosion, torn cloth, diagonal book bindings, jagged glass, hollow can shells and fine bag wrinkles are unfinished. The remaining directional debris changes its arrangement between source frames and needs explicit variants; it is not counted by rotating these models. Skeletal remains need separate organic geometry. These are draft static models, not art approval or a replacement gameplay viewport.

## Source licenses

### /Textures/CMU14/N14content/world.rsi

- License: CC-BY-NC-SA-3.0
- Attribution: Taken from mojave-sun-13 at https://github.com/Mojave-Sun/mojave-sun-13/blob/ffcecc82f28c796f8eff92ac46ff0f5e0d9b1ab6/mojave/icons/structure/miscellaneous.dmi

### /Textures/_RMC14/Structures/hybrisa_machine_props.rsi

- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/structures/props/hybrisa/64x64_props.dmi

## Source states

| Prototype | State | Directions | Parts |
| --- | --- | ---: | ---: |
| RMCMachinePropBig1 | buildingventbig1 | 1 | 54 |
| RMCMachinePropBig10 | buildingventbig10 | 1 | 36 |
| RMCMachinePropBig11 | buildingventbig11 | 4 | 43 |
| RMCMachinePropBig2 | buildingventbig2 | 1 | 95 |
| RMCMachinePropBig8 | buildingventbig8 | 1 | 56 |
| RMCMachinePropBig9 | buildingventbig9 | 1 | 24 |
| DecorFloorGlass1 | glass_1 | 1 | 20 |
| DecorFloorGlass2 | glass_2 | 1 | 17 |
| DecorFloorGlass3 | glass_3 | 1 | 18 |
| DecorFloorGlass4 | glass_4 | 1 | 23 |
| DecorFloorGlass6 | glass_6 | 1 | 20 |
| DecorFloorTrashbags1 | trashbags_1 | 1 | 17 |
| DecorFloorTrashbags2 | trashbags_2 | 1 | 27 |
| DecorFloorTrashbags3 | trashbags_3 | 1 | 27 |
| DecorFloorTrashbags4 | trashbags_4 | 1 | 30 |
| DecorFloorTrashbags5 | trashbags_5 | 1 | 40 |
| DecorFloorTrashbags6 | trashbags_6 | 1 | 30 |
| DecorFloorBookPile4 | bookpile_4 | 1 | 12 |
| DecorFloorFood1 | foodstuff_1 | 1 | 16 |
| DecorBarrels | barrels1 | 1 | 18 |
