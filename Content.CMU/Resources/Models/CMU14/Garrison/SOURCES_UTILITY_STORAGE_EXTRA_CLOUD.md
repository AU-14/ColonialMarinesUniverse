# Utility and storage extras: source-inspected solid drafts

Date: 2026-10-06. Source: [TheHellFireo/CMU-Garrison-3D](https://github.com/TheHellFireo/CMU-Garrison-3D/tree/6e37a4d0a7d9433838c393a82c02422cb704dd5a), checkpoint `6e37a4d0a7d9433838c393a82c02422cb704dd5a`.

## Deliverable and scope

23 editable solid-part models comprise 19 exact-mapped designs covering all 20 assigned prototype IDs and four additional unbound studies. The two ordinary pistol cases use the same exterior source and share one design. Inventory counts are 23 Redux and 22 classic placements; these are reference inventory counts, not new saved-map fit evidence. All models remain `status: draft`.

Canonical files are `garrison_utility_storage_extra_cloud.yml` and `garrison_utility_storage_extra_cloud_art.yml`. Fourteen byte-exact original detail crops use the first fourteen reserved entries of `families.utility_storage_extra.indices`: 2292–2299 and 2330–2335. The gap is intentional. The author script reads the actual reserved list.

The existing local exporter is used directly with no post-processing or edits. Its only difference from the checkpoint exporter is a pre-existing extra final newline; the code content is unchanged. `utility-storage-extra-exporter-check.json` records both hashes. No engine, adapter, runtime, map, source entity prototype, game launch, push or publishing work is part of this family.

## Source states and default-state limits

- `AU14GunCasePistolHG45` and `RMCGunCasePistolM77`: identical single mapped `base` layer and inherited `StorageVisuals.Open` owner. Both static closed/open states have real bottom pans, four walls, clasps, hinges, lid and recessed patterned foam. The brown retaining band and four front latch positions follow the source. StorageFill weapons and ammunition are hidden, and are not invented.
- `AU14KitWeaponMP5`: the compact `procase_mini` has the same compatible one-layer state contract. The silver plate, compact shell, side grip roots and open empty lining are source-specific.
- `RMCGunCasePistolM44Custom`: actual `D18case` closed/open PNG bytes are identical. The prototype explicitly notes missing proper closed art, and its replacement layer lacks the inherited `base` mapping. This is only the depicted decorated wooden case with red catch and brass studs. No opening state or transition is invented.
- `RMCToolboxMechanical`, `RMCToolboxElectrical`, `RMCToolboxSyndicateFilled`: actual blue, ochre/yellow and black/red-striped source palettes, independent of the earlier green draft. Inherited ToolboxBase single-layer `icon`/`icon-open` states are compatible. The source syndicate case is predominantly black, not all-red. Filled tools remain hidden.
- `CMSurgicalCaseFilled`: default Closed bag composition is `surgical_case_base` plus `surgical_case_closed`. UI-driven visibility affects two separate layers, so the source is not incorrectly declared a single-layer adapter. An unbound open study uses the actual base/open composition, attached raised lid and real cavity. Surgical contents remain hidden.
- `RMCBoxVialCardboard`: a gray cardboard carton with pale folded lid and original printed vial pictogram, not a clear box showing vials. Its single static `vial` world state is supported.
- `RMCBeerCoolerFilled`: Closed BagState hides `beer_cooler-open`. Random beverage contents are hidden. The unbound open study composes the actual separate overlay and places the insulated lid at the source right side. The cyan checker is the original empty lining, not invented liquid.
- `CrateTrashCartFilled`: unwelded closed base/door composition, empty paper-label container and hidden stored contents. A separate open study has the raised structural lid and empty cavity. Welding, paper labels, sparking, opening transitions and live source-layer selection remain unsupported.
- `CMJanitorialCart`: ItemSlots creates `CMMop` and `RMCBucketJanitorial` at spawn. The default study therefore includes the actual `cart_mop` and `cart_bucket` layers, rather than incorrectly treating all filled contents as hidden. A physical blue basket/frame, lower white cleaning container, hollow yellow bucket, mop pole, push grip and caster wheels reproduce the visible assembly. ItemMapper changes to mop/bucket/signs/spray/bags are not bound.
- `CMWetSign`: yellow plaque with original warning pixels, separate front feet, splayed rear supports, restraints and a true handle opening. One static ground icon only; no folding state is invented.
- `CMMultitool`: static source display, contoured housing, narrow probe, ribs and keypad. One ground icon only.
- `RMCFlashlightPen`: unlit base only. The hidden on-layer, illumination, activation and held art remain unsupported.
- `RMCGeigerCounter`: off source body and analog gauge, open handle, tethered probe and connected cable. All five six-frame powered rate overlays, sound and runtime radiation reading are unsupported.
- `RMCBinocularsCiv`: civilian paired barrels, eyecups, center bridge, focus wheel and recessed lenses. The gap between barrels is physical. Targeting, eyes/held art and zoom remain unsupported.
- `RMCCameraBroadcasting`: the single world `icon` contains two source frames timed 2.5/0.5 seconds. Exactly one pixel changes: (20,18), green `#3EBA7D` to dark `#033B03`. The physical diode is the only corresponding geometry-property change; one original-timed GLB clip is exported. Broadcasting, held/wielded art and live playback are not verified.
- `RMCCorrespondentMicrophone`: actual diagonal loose-world `icon`, rather than its upright `storage` sprite. Rounded grille, retaining neck, handle, small switch and badge are separate connected solids. Broadcast/audio and held art remain unsupported.
- `BoxFolderClipboard`: the inherited source randomly creates zero to five papers. Content-independent wood board plus always-visible spring clip is exact-mapped, with no assertion that the saved instance has paper or pen. A paper-present composition is a separately counted unbound study. Dynamic ItemMapper contents and saved per-instance results are unsupported.

The four unbound studies never contribute `sourcePrototypes` coverage. Single-visible-layer bindings are declared only for the compatible cases, toolboxes, carton, sign, multitool, binoculars, microphone and camera. No held, worn or equipped state is claimed.

The inspected original SharedStorageSystem owns StorageVisuals.Open and BagState from UI.IsUiOpen (lines 1023–1028 at the checkpoint). EntityStorageVisualizerSystem independently selects the crate Door state from StorageVisuals.Open. These files are retained as read-only source references; neither is edited.

## Physical construction and inference

Cases and carts are assemblies of structural floor, walls, lid, rims, hinges and clasps; open variants have actual three-dimensional cavities and mouth clearance. Handles are separated legs and crossbars around physical openings. Equipment uses continuous cylinders or joined rods for shafts, cable, supports and lenses. Original detail pixels are restricted to small physically backed surface features such as foam, labels, gauge, display and warning print. No whole source sprite is used as the model geometry.

Depth, underside, unseen rear surfaces, support pose, lighting/material response, mechanical details and interpretation of pixel-scale source projections remain inferred. These are stylized editable drafts, not fidelity-approved replacements. Contact tests are local part-connectivity tests and do not establish saved-map contact clearance or surface manifoldness.

## Reproduction and evidence

From the repository root:

```
python Tools/three_d/author_utility_storage_extra_cloud.py
python Tools/three_d/verify_utility_storage_extra_cloud.py
python Tools/three_d/verify_utility_storage_extra_connections.py
blender -b -t 2 --python Tools/three_d/check_utility_storage_extra_blender.py
```

Evidence is in `Tools/three_d/generated/cloud-review/utility-storage-extra/`:

- `utility-storage-extra-proof.json`: 23 deterministic GLBs, 20 exact IDs, four unbound studies, 94 Git-blob-verified source inputs, 14 unchanged detail crops, actual palette membership, finite accessor/transform and buffer-range checks, source direction/timing checks and 70 explicit cavity/floor/handle probes
- `utility-storage-extra-source-defaults.json`: resolved relevant inherited source components for all 20 assigned prototypes, with no missing ancestors
- `utility-storage-extra-contact-checks.json`: every exported static GLB scene has one connected local part-contact component, with positive-volume convex primitive intersection witnesses; this includes all stable alternate case/toolbox poses and the four unbound studies
- `utility-storage-extra-blender-import.json`: all 23 final exports independently imported by Blender 4.3.2, finite vertices and zero mesh-validation repairs
- `utility-storage-extra-exporter-check.json`: existing local exporter fingerprint and checkpoint comparison
- Individual source-facing/orbit/reverse cards, every alternate single-layer state card, an overview, and a fixed shared-scale sheet

There are 38 exported GLB scenes, including repeated default/static state scenes, and one timed camera clip. These are not 38 different source designs. Maximum authored pose is 43 parts, below the unchanged 128-part limit. No Khronos validator run, native renderer admission, saved-map neighbor fitting, game behavior, live interaction or fidelity approval is claimed.

## Exact prototype IDs

AU14GunCasePistolHG45; RMCGunCasePistolM77; RMCGunCasePistolM44Custom; CMSurgicalCaseFilled; AU14KitWeaponMP5; RMCToolboxMechanical; RMCToolboxElectrical; RMCToolboxSyndicateFilled; RMCBoxVialCardboard; RMCBeerCoolerFilled; CMJanitorialCart; CrateTrashCartFilled; CMWetSign; CMMultitool; RMCFlashlightPen; RMCGeigerCounter; RMCBinocularsCiv; RMCCameraBroadcasting; RMCCorrespondentMicrophone; BoxFolderClipboard.

## Original source attribution

The following metadata is retained verbatim from each original RSI `meta.json`. Keep this file and those metadata files with exported art and derived pixel crops. Original art licenses apply to their source-derived portions; structural geometry does not remove attribution requirements.

### Resources/Textures/Objects/Fun/Instruments/microphone.rsi/meta.json

[Original metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/Objects/Fun/Instruments/microphone.rsi/meta.json)

License: CC-BY-SA-3.0

Created by EmoGarbage404, Storage by TiniestShark (Github)

### Resources/Textures/Objects/Misc/clipboard.rsi/meta.json

[Original metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/Objects/Misc/clipboard.rsi/meta.json)

License: CC-BY-SA-3.0

Clipboard sprites are by WJohn (Github) for tgstation, taken from https://github.com/tgstation/tgstation/commit/3cc10b1785bff95e861d722b5164189951761921. Inhand sprites by nmajask (Github) for SS14. clipboard_paper is a modified version of paper from bureaucracy.rsi.

### Resources/Textures/Structures/Storage/Crates/labels.rsi/meta.json

[Original metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/Structures/Storage/Crates/labels.rsi/meta.json)

License: CC-BY-SA-3.0

Sprites by Vermidia and modified by SpaceRox1244.

### Resources/Textures/Structures/Storage/Crates/trashcart.rsi/meta.json

[Original metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/Structures/Storage/Crates/trashcart.rsi/meta.json)

License: CC-BY-SA-3.0

Modified from https://github.com/tgstation/tgstation/commit/571e401e19514e8b0216e2efbbc95302007bfe9c by potato1234x (Github) for SS14

### Resources/Textures/_RMC14/Objects/Devices/binoculars.rsi/meta.json

[Original metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Devices/binoculars.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/c67c5213507a19e10e252ef2898d94354c0709bf/icons/obj/items/binoculars.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items_by_map/classic_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items_by_map/desert_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items_by_map/jungle_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items_by_map/snow_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items_by_map/urban_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items_by_map/classic_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items_by_map/desert_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items_by_map/jungle_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items_by_map/snow_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items_by_map/urban_righthand.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/6f01c67985480f7db0f88ace56788ec3ff721c19/icons/obj/items/binoculars.dmi

### Resources/Textures/_RMC14/Objects/Devices/broadcasting_camera.rsi/meta.json

[Original metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Devices/broadcasting_camera.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/6791699de749afc1c62bc65ec6f2326a00b3bb61/icons/obj/items/items.dmi, https://github.com/cmss13-devs/cmss13/blob/7bf8ce23bdf2dec7429ee4ae9c6e350d6cdb9f00/icons/mob/humans/onmob/items_lefthand_0.dmi, https://github.com/cmss13-devs/cmss13/blob/7bf8ce23bdf2dec7429ee4ae9c6e350d6cdb9f00/icons/mob/humans/onmob/items_righthand_0.dmi

### Resources/Textures/_RMC14/Objects/Misc/Janitorial/cart.rsi/meta.json

[Original metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Misc/Janitorial/cart.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/106c92cdf232ebc12c9d7a2feb23956c6755496f/icons/obj/janitor.dmi

### Resources/Textures/_RMC14/Objects/Misc/Janitorial/caution.rsi/meta.json

[Original metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Misc/Janitorial/caution.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/106c92cdf232ebc12c9d7a2feb23956c6755496f/icons/obj/janitor.dmi, https://github.com/cmss13-devs/cmss13/blob/bec6653d487a49aa2b5a8e0c97bed9612f620211/icons/mob/humans/onmob/items_righthand_0.dmi, https://github.com/cmss13-devs/cmss13/blob/bec6653d487a49aa2b5a8e0c97bed9612f620211/icons/mob/humans/onmob/items_lefthand_0.dmi

### Resources/Textures/_RMC14/Objects/Storage/D18case.rsi/meta.json

[Original metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Storage/D18case.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/items/storage.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items/storage_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items/storage_lefthand.dm

### Resources/Textures/_RMC14/Objects/Storage/boxes.rsi/meta.json

[Original metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Storage/boxes.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/71d46ee8057d19b12e1495419cb299d2fedef6cc/icons/obj/items/storage.dmi, https://github.com/cmss13-devs/cmss13/blob/c648dca374ddee1c80f61ab88678c287356fa66f/icons/obj/items/storage.dmi, l96 modified from m94 by github monomethylhydrazine, https://github.com/cmss13-devs/cmss13/blob/b6f4841b5768599c0fcf58a21fd88536a9959c2c/icons/obj/items/storage/boxes.dmi

### Resources/Textures/_RMC14/Objects/Storage/guncase.rsi/meta.json

[Original metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Storage/guncase.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/items/storage.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items/storage_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items/storage_lefthand.dmi

### Resources/Textures/_RMC14/Objects/Storage/procase_mini.rsi/meta.json

[Original metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Storage/procase_mini.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/commit/106c92cdf232ebc12c9d7a2feb23956c6755496f, Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/3a94024f59bfd350ecf02c511a7d99d597da3d6c/icons/obj/items/storage/kits.dmi

### Resources/Textures/_RMC14/Objects/Storage/surgical_case.rsi/meta.json

[Original metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Storage/surgical_case.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/ca94d2e8715b73103fa9f213be53d343359b4107/icons/obj/items/storage.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/medical_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/medical_lefthand.dmi Modified by KalimbaMachine and Vermidia

### Resources/Textures/_RMC14/Objects/Tools/Light/penlight.rsi/meta.json

[Original metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Tools/Light/penlight.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/884db783073c035b756c175b1bc75fb43279803e/icons/obj/items/lighting.dmi, https://github.com/cmss13-devs/cmss13/blob/bec6653d487a49aa2b5a8e0c97bed9612f620211/icons/mob/humans/onmob/items_righthand_0.dmi, https://github.com/cmss13-devs/cmss13/blob/bec6653d487a49aa2b5a8e0c97bed9612f620211/icons/mob/humans/onmob/items_lefthand_0.dmi

### Resources/Textures/_RMC14/Objects/Tools/Toolboxes/toolbox_blue.rsi/meta.json

[Original metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Tools/Toolboxes/toolbox_blue.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/items/storage.dmi , held sprites redone by Alekshhh, modified by Hyenh

### Resources/Textures/_RMC14/Objects/Tools/Toolboxes/toolbox_syndi.rsi/meta.json

[Original metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Tools/Toolboxes/toolbox_syndi.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/items/storage/toolbox.dmi , held sprites redone by Alekshhh and re-colored by Dutch-VanDerLinde, modified by Hyenh

### Resources/Textures/_RMC14/Objects/Tools/Toolboxes/toolbox_yellow.rsi/meta.json

[Original metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Tools/Toolboxes/toolbox_yellow.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/items/storage.dmi , held sprites redone by Alekshhh, modified by Hyenh

### Resources/Textures/_RMC14/Objects/Tools/geiger_counter.rsi/meta.json

[Original metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Tools/geiger_counter.rsi/meta.json)

License: CC-BY-SA-4.0

Made by SharkSnake98 on GitHub

### Resources/Textures/_RMC14/Objects/Tools/multitool.rsi/meta.json

[Original metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Tools/multitool.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/60ca3cddb69147cb17fcaef3b0514586292a4c55/icons/mob/humans/onmob/items_lefthand_0.dmi, https://github.com/cmss13-devs/cmss13/blob/60ca3cddb69147cb17fcaef3b0514586292a4c55/icons/mob/humans/onmob/items_righthand_0.dmi,https://github.com/cmss13-devs/cmss13/blob/bec6653d487a49aa2b5a8e0c97bed9612f620211/icons/obj/items/devices.dmi

### Resources/Textures/_RMC14/Structures/Storage/beer_cooler.rsi/meta.json

[Original metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Storage/beer_cooler.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/structures/souto_land.dmi
