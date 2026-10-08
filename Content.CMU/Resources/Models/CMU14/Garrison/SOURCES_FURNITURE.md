# Garrison beds, chairs and storage furniture

12 new draft assemblies / 252 parts, mapping 71 classic saved objects, plus four revised assemblies covering 55 saved objects. Ten former inherited candidates now have their own source-matched geometry; two formerly unmapped types have models. All remain drafts. Canonical sources are `garrison_furniture.yml`, `garrison_models.yml`, `garrison_interiors.yml` and `garrison_utilities.yml`; scratch scripts are not regeneration sources.

Geometric contributions are CC0-1.0 to the extent separately licensable. Original pixels and derivative appearance retain their source licenses and attribution below.

## Source-specific geometry

- Brown utility tables preserve the prototype's #8B7B5B tint, worn inset and double score at the right edge. Four legs and lower stretchers support a .86-tile top. Matching anchored neighbours extend the top and omit inner borders using the existing support-connection rules. The grid controls smoothing orientation. No generic table alias is promoted to exact coverage.
- The gray metal chair follows its four directional frames. The folded steel chair uses the actual one-direction `chair_folded` layer selected by the prototype's initial Foldable state, lying across X. It retains nested frame tubes, back/seat panels and hinges. Collapsed rack parts retain nested shelf panels, dark slots and raised carry handle. Their unseen construction remains inferred.
- Beds now distinguish bare tan mattresses, worn checker blankets, black/blue/green/red bunk assemblies, plain hospital trolleys, green sheets, medical-cross sheets and a blood-stained sheet. Bed length follows local X and pillows occupy +X. New bed footprints are approximately one tile wide, matching the source-frame width. Three older bed drafts were revised to use the same family scale; the old hospital trolley also had the wrong axis and raised rails despite its unbuckled default state.
- Fourteen exact original bedding crops preserve checkers, creases, medical-cross and stain patterns on physical sheets. Pillows have stepped cushion edges and highlights; cylinder casters, guard rails, ladder rungs, bed frames and nested carriages remain separate editable solids. Trolleys use the unbuckled/unfolded down state; no saved Foldable or Strap overrides occur in these instances. Original pixels are not repainted. Crops occupy atlas slots 264–277, verified byte-for-byte in `generated/furniture-crop-audit.json`.
- The existing large wooden crate now follows its vertical front planks, corner battens, lower rail and recessed closed lid. Its reference is the exact original base+closed composition. The previous oversized height was reduced; the closed lid at .63 tiles supports the saved flashlight box. Lid motion and interior/open states are unfinished.

## Context checks

All 126 affected saved instances retain XY positions and rotations. The brown tables and crate provide five additional usable supports; the MRE box #9715 and empty HE packet #2321 now rest on table #15265, and flashlight box #9697 rests on crate #10285. The later multipart-support correction places folded chair #1719 above co-located pallet #7140, and rack parts #14400 above #7141. Each actual top board has its own declared footprint; gaps remain open. See `SOURCES_FOLDED_EQUIPMENT.md` for regression and browser evidence. Rack-parts objects without a declared support also remain at floor height. Connected support count is 62 (49 reinforced and 13 brown tables); whole-scene support totals are 751 attached props and 68 without exact support.

Two green bunk objects #9535/#9536 share an identical saved pivot. This pre-existing overlap is preserved. Rack-top placement around handcuff cartons #9711/#9712 remains unresolved: the original shelf geometry is retained because increasing depth hid the shelf gaps in source comparisons. No hidden support plane or arbitrary offset was introduced. Gambling-table inspection found a connected 3-by-2 arrangement requiring rounded joined borders; it is still an inherited candidate.

All sixteen new/revised source/four-view cards were inspected. Browser context review covers bunk rooms, hospital trolleys, the brown-table supplies and the revised crate; the folded-chair check revealed the pallet occlusion subsequently corrected as described above. Captures are `generated/review/furniture-*.png`. The furniture region exports 183 entities and 225 floor tiles. Inferred bed height, unseen construction, fine cloth, practical ladder construction, wear/material response, occupant poses, folding, buckling and open-container animation remain unfinished. The normal gameplay viewport is unchanged.

## Source licenses

### /Textures/Structures/Furniture/chairs.rsi

- License: CC-BY-SA-3.0
- Attribution: Taken from tgstation at commit https://github.com/tgstation/tgstation/commit/11402f6ae62facc2e8bcfa1f8ef5353b26663278, meat.png is CC0-1.0 by EmoGarbage404 (github) for Space Station 14. chair.png and its derrivatives taken from shiptest at commit https://github.com/shiptest-ss13/Shiptest/commit/f761c784812e827960a66cd10aac17ebc6edfac3, palette for chair.png, steel-bench.png and chair-greyscale.png taken from paradise equivalent chairs at commit https://github.com/ParadiseSS13/Paradise/commit/5ce5a66c814c4a60118d24885389357fd0240002, steel by SonicHDC, brass chair.png taken from tgstation at https://github.com/tgstation/tgstation/blob/b7e7779c19b76449c290aaf2150fb93545b1a79a/icons/obj/chairs.dmi, wooden bench by Ko4erga (discord), xeno-chair by juneszalkowska (discord)

### /Textures/_RMC14/Objects/Materials/rack_parts.rsi

- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/items/table_parts.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/construction_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/construction_lefthand.dmi

### /Textures/_RMC14/Structures/Furniture/Tables/standard.rsi

- License: CC-BY-SA-3.0
- Attribution: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/5c70b0d01cc16865b7a000c3a74e0d4f729661f6/icons/obj/structures/tables.dmi

### /Textures/_RMC14/Structures/Furniture/bed.rsi

- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/9dd2c0d0a1c21e3c9bddbad1321c63fd886f61cf/icons/obj/objects.dmi, https://github.com/cmss13-devs/cmss13/blob/6a955a3c180f3efcf3b997c230fff4d634eb0629/icons/obj/structures/machinery/yautja_machines.dmi, https://github.com/cmss13-devs/cmss13/blob/39a39f5df6c4b32708e50ed711dc5b1bebe313b6/icons/obj/structures/props/furniture/chairs.dmi

### /Textures/_RMC14/Structures/Furniture/folding_chair.rsi

- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/9dd2c0d0a1c21e3c9bddbad1321c63fd886f61cf/icons/obj/objects.dmi,  https://github.com/cmss13-devs/cmss13/blob/9dd2c0d0a1c21e3c9bddbad1321c63fd886f61cf/icons/mob/humans/items/furniture_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/9dd2c0d0a1c21e3c9bddbad1321c63fd886f61cf/icons/mob/humans/items/furniture_righthand.dmi

### /Textures/_RMC14/Structures/Furniture/rollerbeds.rsi

- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/rollerbed.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/medical_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/medical_lefthand.dmi

### /Textures/_RMC14/Structures/Storage/Crates/densecrate.rsi

- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7cb618c69b75873f3ce893022fe08d1233b3152d/icons/obj/structures/crates.dmi , modified by Hyenh#6078(313846233099927552)

## Source states

| Prototype | State | Parts |
| --- | --- | ---: |
| RMCTableBrown | full | 19 |
| Chair | chair | 13 |
| CMChairFolded | chair_folded | 14 |
| RMCRackParts | icon | 13 |
| RMCBedAlt | abed | 12 |
| RMCBedDingy | dingy_bed | 13 |
| RMCBedBunkBlue | bunk_blue | 30 |
| RMCBedBunkGreen | bunk_green | 30 |
| RMCBedBunkRed | bunk_red | 30 |
| RMCRollerBedHospitalBlood | bigrollerblood_down | 28 |
| RMCRollerBedHospitalSheet | bigrollerhospitalsheet_down | 25 |
| RMCRollerBedHospitalSheet2 | bigrollerhospitalsheet2_down | 25 |
| RMCRollerBedHospital | bigroller_down | 25 |
| CMBed | bed | 13 |
| RMCBedBunk | bunk_black | 30 |
