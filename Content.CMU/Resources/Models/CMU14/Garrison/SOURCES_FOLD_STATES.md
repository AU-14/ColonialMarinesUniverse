# Garrison folded and unfolded furniture poses

Five new state-only drafts / 153 parts complete seven reciprocal pose pairs across 14 assemblies. They cover both stable poses of all 134 currently mapped classic Foldable objects: 83 chairs, eight bedrolls, 22 compact roller beds and 21 hospital trolleys. Initial geometry for the 108 unfolded and 26 folded saved objects is unchanged. These state partners do not claim additional exact source prototypes.

New canonical geometry is in `garrison_fold_states.yml`. Pose links also extend the existing chair, bedroll and trolley definitions in their original files. Generated GLBs and review cards are derived outputs; scratch authoring scripts must not be replayed over subsequent canonical refinements.

New geometric work is CC0-1.0 to the extent separately licensable. Source visual design and derivative appearance retain the licenses and attribution below.

## Source matching and inferred geometry

- The unfolded compact roller bed references `roller_down`, not the occupied `roller_up` state. Three red padded sections sit on a low gray carriage, with four casters, circular hubs, front piping and inset seams. Source width, color groups and visible structure guide the draft; physical height, hidden supports and rear surfaces remain inferred.
- The four hospital variants reference `surgical_folded`. Its alpha silhouette is byte-identical to the compact `folded` source; the pale retaining cloth and straps replace the compact model's red ones. The four folded models intentionally share this source appearance, with separate reciprocal links so each unfolds back to its own plain, bloodied or sheet-covered partner. Cloth deformation and unseen construction remain inferred.
- Existing chair and bedroll geometry is retained. Explicit references select `chair` / `chair_folded` and `bedroll` / `bedroll_folded`. The unfolded chair has four source directions; its folded pose has one. Pose selection occurs before direction and support resolution so the selected pose supplies the correct orientation and footprint.

## State contract

`folded` declares the model's stable pose, defaulting to false. `alternateFoldModel` must name an opposite-pose model that links back to the original; both partners require explicit RSI/state references. Export validation rejects missing, one-way or same-pose links. The native catalog preserves exact/inherited provenance and does not mutate its cached base match. A missing requested pose uses an unsupported-state marker rather than an incorrect unfolded model.

The saved-map pipeline combines prototype defaults with saved Foldable overrides, then applies the same pose choice. Source comparisons resolve the visible state from the source GenericVisualizer layer rules, including an unmodeled pose. The live adapter reads the replicated Foldable component during its existing refresh and selection updates; it does not change gameplay folding behavior. Connected multiplayer folding is not yet verified.

## Verification and review

`generated/fold-states-source-audit.json` records all 14 references, hashes, directions, license metadata and pose partners. `fold-states-placement-audit.json` records 268 simulated selections (both poses for 134 saved objects), checked against actual source layer rules. Every saved XY/yaw and initial model ID remains unchanged. Main-scene support/layout counts remain 779 supported props, 68 without exact support, 453 adjusted facings, 758 connected panels and 62 connected tables.

All 14 source/four-view cards were refreshed. `generated/fold-pose-review.json` is an explicitly labeled isolated fixture with 14 modeled poses, one unsupported folded scarf and 128 floor tiles; it is not a saved Garrison placement. Its paired browser views confirm the alternate references, folded colors and missing-state marker. `review/fold-states-hospital-pairs.png` and `fold-states-missing-pose.png` capture that fixture. Actual saved medical storage retains its rack placement and original duplicate pivots.

All 596 individual GLBs and all 13 assembled exports (12 actual Garrison regions plus the pose fixture) pass Khronos validation without errors or warnings. Deterministic regeneration, 103 Python tests, 83 isolated native tests and the client build pass. The connected test project is still blocked by unrelated tactical-map server compilation errors; live state reception and interaction remain unverified. No model is approved and the standard gameplay viewport is unchanged.

Stable poses do not provide folding animation, occupied bedding/raised rails, realistic cloth, material response or verified hidden construction.

## Original source licenses

### /Textures/_RMC14/Structures/Furniture/bedroll.rsi

- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/9dd2c0d0a1c21e3c9bddbad1321c63fd886f61cf/icons/obj/items/bedrolls.dmi

### /Textures/_RMC14/Structures/Furniture/folding_chair.rsi

- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/9dd2c0d0a1c21e3c9bddbad1321c63fd886f61cf/icons/obj/objects.dmi,  https://github.com/cmss13-devs/cmss13/blob/9dd2c0d0a1c21e3c9bddbad1321c63fd886f61cf/icons/mob/humans/items/furniture_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/9dd2c0d0a1c21e3c9bddbad1321c63fd886f61cf/icons/mob/humans/items/furniture_righthand.dmi

### /Textures/_RMC14/Structures/Furniture/rollerbeds.rsi

- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/rollerbed.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/medical_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/medical_lefthand.dmi

## Source states

| Model | State | Folded | Parts |
| --- | --- | --- | ---: |
| CMU3DBedroll | bedroll | False | 16 |
| CMU3DBedrollFolded | bedroll_folded | True | 12 |
| CMU3DCMChairFolded | chair_folded | True | 14 |
| CMU3DCMRollerBedSpawnFolded | folded | True | 27 |
| CMU3DFoldingChair | chair | False | 29 |
| CMU3DHospitalRollerBed | bigroller_down | False | 25 |
| CMU3DHospitalRollerBedFolded | surgical_folded | True | 29 |
| CMU3DRMCRollerBedHospitalBlood | bigrollerblood_down | False | 28 |
| CMU3DRMCRollerBedHospitalBloodFolded | surgical_folded | True | 29 |
| CMU3DRMCRollerBedHospitalSheet | bigrollerhospitalsheet_down | False | 25 |
| CMU3DRMCRollerBedHospitalSheet2 | bigrollerhospitalsheet2_down | False | 25 |
| CMU3DRMCRollerBedHospitalSheet2Folded | surgical_folded | True | 29 |
| CMU3DRMCRollerBedHospitalSheetFolded | surgical_folded | True | 29 |
| CMU3DRollerBedUnfolded | roller_down | False | 37 |
