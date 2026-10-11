# Small medical props: source-derived solid drafts

Date: 2026-10-06. Source repository: TheHellFireo/CMU-Garrison-3D, branch `Chip/garrison-3d`. Canonical source checkpoint recorded by the parent: `6e37a4d0a7d9433838c393a82c02422cb704dd5a`. Fetched file blob IDs are retained in `reference/medical-small-fetches.json`; all 31 inspected source PNG byte streams match their Git blob hashes.

## Deliverable and boundary

Twenty editable solid-part drafts cover thirteen exact prototype IDs, with seven additional unbound static studies. The inventory lists 23 Redux and ten classic placements for the exact IDs. Those are source inventory counts, not new map-layout, native or live-behavior validation. Every model remains `status: draft`.

Canonical prototypes are `garrison_medical_small_cloud.yml` and `garrison_medical_small_cloud_art.yml`. The original exporter generates the GLBs directly without post-processing or engine modifications. Sixteen original detail crops, all pixel-unchanged, use the first sixteen entries from `families.medical_small.indices`: 1513–1519 and 1539–1547. The gap is intentional. The author script reads the central list and never assumes a continuous numeric range.

## Source states and supported scope

- CMBeaker, CMBeakerLarge and RMCBeakerHighCapacity explicitly inherit openable vessels and set `opened: true`; their inherited Solution has no initial reagents. The exact drafts therefore have open empty cavities and no lid. Separate unbound closed-lid studies use the actual base + `lid_*` artwork. The existing solution-glass adapter only accepts its named drinking-glass resources, so these chemistry beakers are not incorrectly routed through it. Dynamic reagent color, all five fill levels, lid selection, spills and breakage remain unsupported.
- CMAdvFirstAidKit, CMFirstAidKitSurgery and CMFirstAidO2Kit use the actual storage UI owner: SharedStorageSystem sets BagState from `IsUiOpen`, making their ordinary default closed. Their default art preserves the red, cyan-cross and blue center markings. Three unbound open-shell studies compare the original base + `kit_empty` overlay. Live UI changes, transitions and contents remain unsupported.
- RMCVialBoxFull creates six RMCVial contents. Its ItemCounter is composite and Storage explicitly sets `hideStackVisualsWhenClosed: false`. Accordingly, the full exact-prototype study composes all six original vialbox1…vialbox6 overlays. A separate empty-pocket study is unbound. Dynamic count changes and saved per-instance content overrides are not supported.
- CMSurgicalDrill uses the existing rotating single-visible-layer contract for `drill` and `drill_on`. The latter preserves the two source 0.1-second frames in one looping 0.2-second GLB clip. Surgery triggering and native interactive playback are unverified; no new operation controller was added.
- CMHemostat, RMCStethoscope, CMOintment10 and RMCStasisBagUsed use the existing one-state rotating loose-world contract. The used bag is BaseItem trash, not a folded/occupied body bag. Ointment's stack count ten does not imply ten bottle meshes. All held, worn and patient-use views remain unsupported.
- RMCDropper inherits an empty SolutionInjector. Its separate bulb, collar, hollow barrel and tip show the empty world default; live fill overlays and dispensing are unsupported.

All eleven inspected RSI resources use one-direction world states. Held/worn four-direction states in their metadata are not mislabeled as modeled coverage. No source gameplay definitions, maps, engine files or runtime adapters were edited. The root CMCorrodible and all relevant prototype ancestors are available in the final source audit; component and visualizer defaults were read rather than inferred from hidden template layers alone.

## Physical construction and deliberate inference

Beakers have sixteen separate thin wall segments, a bottom, rim, pouring lips and genuinely open mouths. Original graduation pixels are split into narrow columns that follow their curved walls. The high-capacity gauge sits in a attached recessed front bezel. Glass alpha, depth and hidden materials remain explicit inferences.

Hemostat finger rings and case carrying handles are through-open. The hemostat includes a seated pivot and opposing jaws; the drill has a motor barrel, chuck, fluted bit, trigger and connected grip ribs. The stethoscope has continuous joined cylindrical tubing with rounded bend joints, exposed metal ear stems, tips and a separate chestpiece. First-aid studies have shell walls, lower hinge knuckles, seams, clasps and a hinged-down open panel. Vial pockets are actual divided cavities with six seated glass/cap assemblies. Used-bag cloth is built from thin joined ribbons at varying heights, rolled seams and a worn-marking patch around a real tear, not a full sprite extruded slab.

These remain stylized physical interpretations. Fine cloth folding, realistic translucency, material response, source-view projected height, inferred back surfaces and overall fidelity need review. Mechanical operation, full state completion, native GPU rendering and map contacts are not certified.

## Evidence and reproducibility

Run from the repository root:

```
python Tools/three_d/author_medical_small_cloud.py
python Tools/three_d/verify_medical_small_cloud.py
python Tools/three_d/verify_medical_small_connections.py
blender -b -t 2 --python Tools/three_d/check_medical_small_blender.py
```

- `medical-small-cloud-verification.json`: exporter bytes, exact mappings, parts, scenes, clips, source crops and atlas assignments
- `medical-small-cloud-source-audit.json`: 27 geometry/aperture tests; original PNG Git hashes; inherited source components; unchanged crops; deterministic GLB, source state/timing, finite transform and buffer-bound checks
- `medical-small-cloud-contact-checks.json`: direct default-scene GLB convex primitive planes and linear-programming positive-volume contact witnesses; all thirteen default assemblies form one connected contact component after corrections. This is not a whole-model manifoldness or opacity proof, and it does not include all unbound study poses
- `medical-small-cloud-blender-import.json`: independent Blender 4.3.2 imports of all twenty exports, finite vertices and zero mesh-validation repairs
- `review/medical-small-cloud/`: every model's original source composition plus front/top, orbit and rear views; both drill frames; fixed-scale sheet; compact highlights

Maximum authored pose: 60 parts, beneath the unchanged 128-part limit. A source-conserving detail patch is never treated as the object's whole geometry. No Khronos validator result is claimed for this family; only the explicitly listed checks have run.

## Exact prototype IDs

- CMBeaker
- CMBeakerLarge
- RMCBeakerHighCapacity
- CMHemostat
- CMSurgicalDrill
- RMCStethoscope
- RMCDropper
- CMAdvFirstAidKit
- CMFirstAidKitSurgery
- CMFirstAidO2Kit
- CMOintment10
- RMCStasisBagUsed
- RMCVialBoxFull

## Original sprite attribution

These adapted model colors and unchanged detail crops derive from the original sprites. The full original RSI metadata remains alongside the fetched images. All eleven resources identify CC-BY-SA-3.0; retain this attribution and their original source links with redistribution. New inferred geometry and crop adaptations are provided under the same CC-BY-SA-3.0 terms. Original metadata follows verbatim, including its spelling.

### _RMC14/Objects/Chemistry/dropper.rsi

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/4c03b0ed78e7565ec0fb4112f4f2e59421e239cc/icons/obj/items/chemistry.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/medical_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/medical_lefthand.dmi

### _RMC14/Objects/Medical/Surgery/drill.rsi

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7917d83bac9cddd14c0ca0b457256b2683cc047f/icons/obj/items/surgery_tools.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/medical_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/medical_lefthand.dmi

### _RMC14/Objects/Medical/Surgery/hemostat.rsi

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7917d83bac9cddd14c0ca0b457256b2683cc047f/icons/obj/items/surgery_tools.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/medical_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/medical_lefthand.dmi

### _RMC14/Objects/Medical/beaker.rsi

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/d0e9166e3c6cabda8c4173ce9adb9b7f8122fe0a/icons/obj/items/chemistry.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items/bottles_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items/bottles_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/ca94d2e8715b73103fa9f213be53d343359b4107/icons/obj/items/reagentfillings.dmi

### _RMC14/Objects/Medical/first_aid_kits.rsi

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/ca94d2e8715b73103fa9f213be53d343359b4107/icons/obj/items/storage.dmi, https://github.com/cmss13-devs/cmss13/blob/09583166e0082a417aef46dbb280675fa93a81e8/icons/mob/humans/onmob/inhands/equipment/medical_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/09583166e0082a417aef46dbb280675fa93a81e8/icons/mob/humans/onmob/inhands/equipment/medical_lefthand.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/d393d64e20ab878b476dd43900e32b0fbb4cb484/icons/obj/items/storage/medical.dmi

### _RMC14/Objects/Medical/high_capacity_beaker.rsi

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/4c03b0ed78e7565ec0fb4112f4f2e59421e239cc/icons/obj/items/chemistry.dmi,https://github.com/cmss13-devs/cmss13/blob/4c03b0ed78e7565ec0fb4112f4f2e59421e239cc/icons/obj/items/reagentfillings.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items/bottles_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items/bottles_lefthand.dmi

### _RMC14/Objects/Medical/large_beaker.rsi

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/d0e9166e3c6cabda8c4173ce9adb9b7f8122fe0a/icons/mob/humans/onmob/items_righthand_0.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items/bottles_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items/bottles_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/ca94d2e8715b73103fa9f213be53d343359b4107/icons/obj/items/reagentfillings.dmi

### _RMC14/Objects/Medical/medical.rsi

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/b0bca8ef7dedf94a30f456544bec6ef3aaa7ec8a/icons/obj/items/items.dmi, hhttps://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/medical_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/medical_lefthand.dmi, and https://github.com/cmss13-devs/cmss13/blob/a4f7021eba78932d6cc05bdf2eb669b4dabe4ded/icons/obj/items/surgery_tools.dmi, https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/obj/items/medical_stacks.dmi

### _RMC14/Objects/Medical/stasisbag.rsi

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/a5990526c258405745260c0b2f8fac01c84261a3/icons/obj/cryobag.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/medical_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/medical_lefthand.dmi, holocard sprites by Vermidia

### _RMC14/Objects/Medical/stethoscope.rsi

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/1574b8777d8b8237a588f99fb23ce5edb2b443fc/icons/obj/items/clothing/accessory/misc.dmi, https://github.com/cmss13-devs/cmss13/blob/09583166e0082a417aef46dbb280675fa93a81e8/icons/mob/humans/onmob/inhands/equipment/medical_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/09583166e0082a417aef46dbb280675fa93a81e8/icons/mob/humans/onmob/inhands/equipment/medical_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/7b4244612942e910b40148d4b54d38748f114a84/icons/mob/humans/onmob/clothing/accessory/misc.dmi

### _RMC14/Objects/Storage/vial_box.rsi

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/f7b9367cb5c083a8c052a40dece9417031e28ffe/icons/obj/items/vialbox.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/medical_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/medical_lefthand.dmi
