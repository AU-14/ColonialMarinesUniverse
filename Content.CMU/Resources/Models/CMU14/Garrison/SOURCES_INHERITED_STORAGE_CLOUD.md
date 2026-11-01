# Inherited storage: source-specific draft replacements

Pinned source: [6e37a4d0a7d9433838c393a82c02422cb704dd5a](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a)

## Scope and counts

This is a separate audit of 45 inherited Redux candidates, not another pass over the 473 originally missing IDs. The audit has 16 source-compatible inheritance candidates, 29 evidenced mismatches, and zero unresolved candidates. There are 20 new physical draft assemblies, exactly binding the 29 mismatched child IDs (71 prototype-count Redux placements). Compatible inherited geometry is not duplicated; inheritance is not converted to approval. The 16 compatible candidates remain inherited drafts. No file or status is marked reviewed.

The model definition is `garrison_inherited_storage_cloud.yml`; the 20 exact source-crop surfaces are in `garrison_inherited_storage_cloud_art.yml` and the `inherited_storage_cloud` PNG folder. Atlas indices 1460–1479 are the first 20 entries from the explicitly reserved 60-slot list. The remaining reservations are unused; no other family’s slots were borrowed.

## Actual source and geometry evidence

All 45 direct owners, all inherited entity ancestors, and relevant Sprite, Lock, EntityStorage and StorageFill visualizer declarations were fetched through the connected GitHub source and byte-verified at the pin. There are 173 verified CMU source files plus the independently pinned Robust Sprite initialization source. The eight actual parent models were read from the 142 original canonical YAML files; none of those eight uses a surface texture. All 142 canonical files remain unchanged. The exporter implementation remains the pinned one, with only its pre-existing extra terminal newline. Parent validation geometry was never copied into the active editable library.

Each audit record in `inherited-storage-audit.json` includes the direct source file, parent chain, the owning prototype/file of every inherited Sprite field, final untinted Sprite and visible layer composition, storage and lock defaults, actual parent model ID/source file, geometry bounds, palette, part count, stated state limits, reason, and any exact replacement. `inherited-storage-audit.csv` is a compact per-ID index. Each assigned ID occurs once as an audit record.

“Source-compatible candidate” means the child uses the same exterior source family/default as its inherited parent geometry. It does not certify the parent’s physical dimensions, exact palette, tiny status lamp, mesh construction, hidden surfaces or other states. Parent approximations remain visible in the `candidate-*.png` comparisons.

## Default and state ownership

- `RMCCrateBase` provides base + closed door layers; welding and invoice layers are initially hidden. Its EntityStorageVisuals changes the door layer on open/close. Replacements are closed static studies, not a new storage-state adapter.
- `CMClosetBaseUnanchored` supplies closet base/closed layers. `CMLockerBase` replaces them with base/closed/locked, with the red lock state. LockComponent defaults Locked to true; LockVisualizerSystem controls locked/unlocked appearance and hides the lock layer when storage is open.
- Medical, electrical, welder, police and gun lockers replace the RSI while retaining locker-owned layer mappings. The new models bake only the examined initial base + closed + locked composition.
- Specialty bookcases keep book-0 and have commented-out StorageFill blocks. Their dynamic StorageFillVisualizer can select book-1 through book-5; names alone do not justify invented default books.
- Grocery, dry and organic fridges have hidden contents but the same refrigerator exterior. Armory and large wooden-crate fill variants likewise retain their source exterior and are not duplicated.
- The work-tools ID adds Sprite.state=crate while inheriting base + closed layers. Although basic.rsi has no crate state, pinned RobustToolbox edf061e7450a4074f173e3000bf1552b6f54082f SpriteComponent.AfterDeserialization only creates a state/texture shortcut layer when layerDatums.Count==0. Existing inherited layers take precedence. It remains a compatible inheritance candidate, with no duplicate art or live-test approval.

No dynamic fill, opening/closing, welding, paper labels, alert-driven unlock, UI or damage states are represented as complete. No runtime, native, map-fit or full-fidelity claims are made. SourceDirections remains one; free camera orbit does not invent new source directions.

## Geometry and refinements

- Nine colored narrow closets/lockers use hollow five-panel shells, interior shelves, separate doors, headers, hinges, attached latches, raised moldings, ventilation details and small exact departmental graphics. The scientist model preserves purple upper and orange lower exterior fields.
- The gray gun cabinet is wider than the generic locker and has a real recessed empty cavity behind 26 crossing mesh strands; no opaque backing slab blocks the front mesh openings.
- Eight stationary crate families use separate pans, walls, lids, hinges and source-specific latch/strap/wood/plastic/hazard/medical/weapon details. Hidden StorageFill objects are not invented.
- The minecart has four grounded wheels, axles, a hollow tub and three physical top ribs. Its dark source gaps are represented as real openings rather than a sprite-covered solid lid. The open source overlay is blank and does not establish new runtime support.
- The green trash cart has a separately built lid, corrugated walls, four grounded wheels and an open side push handle.
- Source/orbit and attachment reviews corrected tiny floating overlays/latches, extended physical hinge links into the mesh door, removed a medical top restraint that obscured the cross, preserved the scientist lower orange frame, refined the minecart rib count, and restored all three ammunition ticks below the ended front straps. A later independent review corrected all 12 front logo/identity patches across nine narrow cabinet variants: every source pixel now uses exactly 1/32 tile in both front-plane axes; source-derived centers position the graphics independently of inferred cabinet height. Medical/police crop windows now preserve the complete original identity field, electrical/welder crops retain the upper accent pixel, and L3 lettering is trimmed to its actual 7×5 glyph block to exclude a neighboring latch pixel. A final localized source review restored the surgical crate’s two lower-left pale squares and lower-right pale dash from base.png row 27, on their correctly positioned green fields; those markings also retain a uniform 1/32-tile pixel scale.

All parts are supported editable Box/Cylinder primitives. No model is a full-sprite slab. Current maximum is 49 parts, below 128. Physical depth, height, hidden sides, rear and underside remain inferred.

## Verification

- All 20 unchanged-implementation exports reproduce byte-for-byte, have finite accessors and valid buffer bounds, and each contains one static scene with zero clips
- All 20 models have one connected local assembly, grounded support and no below-floor parts; contact checks test actual axis-aligned box/cylinder intersections, not merely common bounding corners
- 24 hollow-interior and aperture checks pass, including sampled rays through mesh, minecart top gaps and the trash-cart handle
- Every untextured color is present in its actual default source palette; every crop is an unchanged original RGBA rectangle; all 12 cabinet identity patches preserve source aspect and a uniform 1/32-tile pixel scale
- Exact child bindings, model IDs and atlas indices have no duplicates in the current active library
- All 142 baseline YAML files, all 173 retrieved CMU source files and the pinned Robust Sprite source bytes are unchanged
- Independent Blender final-byte import results are recorded separately in `inherited-storage-blender-import.json`
- Fitted source/orbit comparisons and four fixed-scale sheets make the inferred proportions inspectable; they are evidence rather than visual approval

Reproduce in this order: `python Tools/three_d/audit_inherited_storage_cloud.py`, `python Tools/three_d/author_inherited_storage_cloud.py`, `python Tools/three_d/verify_inherited_storage_cloud.py`, `blender -b --python Tools/three_d/check_inherited_storage_blender.py`, `python Tools/three_d/document_inherited_storage_cloud.py`. Auditing intentionally clears replacement annotations until verification repopulates them.

## Replacement assemblies

- `CMU3DInheritedClosetsBioCloud`: 23 parts; exact bindings: `CMClosetBio`. Source: `_RMC14/Structures/Storage/Closets/bio.rsi`, composition base + closed.
- `CMU3DInheritedClosetsBioJanitorCloud`: 23 parts; exact bindings: `CMClosetBioJanitor`. Source: `_RMC14/Structures/Storage/Closets/bio_janitor.rsi`, composition base + closed.
- `CMU3DInheritedClosetsBioScientistCloud`: 29 parts; exact bindings: `CMClosetBioScientist`, `CMClosetBioScientistFilled`. Source: `_RMC14/Structures/Storage/Closets/bio_scientist.rsi`, composition base + closed.
- `CMU3DInheritedClosetsToolclosetCloud`: 29 parts; exact bindings: `CMClosetTool`, `CMClosetToolFilled`. Source: `_RMC14/Structures/Storage/Closets/toolcloset.rsi`, composition base + closed.
- `CMU3DInheritedLockersEngineerElectricCloud`: 30 parts; exact bindings: `CMLockerEngineerElectrical`. Source: `_RMC14/Structures/Storage/Lockers/engineer_electric.rsi`, composition base + closed + locked.
- `CMU3DInheritedLockersEngineerWelderCloud`: 30 parts; exact bindings: `CMLockerEngineerWelder`. Source: `_RMC14/Structures/Storage/Lockers/engineer_welder.rsi`, composition base + closed + locked.
- `CMU3DInheritedLockersMedicalCloud`: 31 parts; exact bindings: `CMLockerMedical`. Source: `_RMC14/Structures/Storage/Lockers/medical.rsi`, composition base + closed + locked.
- `CMU3DInheritedLockersMedicalWhiteCloud`: 31 parts; exact bindings: `CMLockerMedicalWhite`. Source: `_RMC14/Structures/Storage/Lockers/medical_white.rsi`, composition base + closed + locked.
- `CMU3DInheritedLockersPoliceCloud`: 31 parts; exact bindings: `CMLockerPolice`. Source: `_RMC14/Structures/Storage/Lockers/police.rsi`, composition base + closed + locked.
- `CMU3DInheritedLockersGunCabinetCloud`: 49 parts; exact bindings: `RMCLockerGunBase`, `RMCLockerGunStorageBrig`, `RMCLockerGunStorageCommand`. Source: `_RMC14/Structures/Storage/Lockers/gun_cabinet.rsi`, composition base + closed + locked.
- `CMU3DInheritedCratesPlasticCloud`: 21 parts; exact bindings: `AU14CrateBoxboxofingredients`, `AU14CrateFoodMRERMC`, `RMCCrateFoodIngredients`. Source: `_RMC14/Structures/Storage/Crates/plastic.rsi`, composition base + closed.
- `CMU3DInheritedCratesSecureWeYaCloud`: 24 parts; exact bindings: `AU14CrateSecureWeYuDrugs`, `CMUCrateSecureWYRandomExp`. Source: `_RMC14/Structures/Storage/Crates/secure_we_ya.rsi`, composition base + closed + locked.
- `CMU3DInheritedCratesSecureMedicalCloud`: 26 parts; exact bindings: `CMCrateSecureSurgery`. Source: `_RMC14/Structures/Storage/Crates/secure_medical.rsi`, composition base + closed + locked.
- `CMU3DInheritedCratesConstructionCloud`: 18 parts; exact bindings: `RMCCrateElectricalMaintenance`. Source: `_RMC14/Structures/Storage/Crates/construction.rsi`, composition base + closed.
- `CMU3DInheritedCratesFreezerCloud`: 29 parts; exact bindings: `RMCCrateFreezer`. Source: `_RMC14/Structures/Storage/Crates/freezer.rsi`, composition base + closed.
- `CMU3DInheritedCratesMinecartCloud`: 29 parts; exact bindings: `RMCCrateMinecart`. Source: `_RMC14/Structures/Storage/Crates/minecart.rsi`, composition base + closed.
- `CMU3DInheritedCratesSecureAmmoCloud`: 19 parts; exact bindings: `RMCCrateSecureAmmo`. Source: `_RMC14/Structures/Storage/Crates/secure_ammo.rsi`, composition base + closed + locked.
- `CMU3DInheritedCratesSupplyCloud`: 30 parts; exact bindings: `RMCCrateSuppliesBoxes`, `RMCCrateSupply`, `RMCCrateSupplyJanitor`. Source: `_RMC14/Structures/Storage/Crates/supply.rsi`, composition base + closed.
- `CMU3DInheritedCratesWeaponsCloud`: 19 parts; exact bindings: `RMCCrateWeapons`. Source: `_RMC14/Structures/Storage/Crates/weapons.rsi`, composition base + closed.
- `CMU3DInheritedCratesTrashCartCloud`: 41 parts; exact bindings: `RMCTrashCart`. Source: `_RMC14/Structures/Storage/Crates/trash_cart.rsi`, composition base + closed.

## Source attribution

All examined storage RSI metadata declares CC-BY-SA-3.0. Original copyright strings are retained below; derived source-crop artwork remains under that attribution/license. Geometry is a draft interpretation of the cited source art.

### [Resources/Textures/_RMC14/Structures/Furniture/bookshelf.rsi](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Furniture/bookshelf.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7cb618c69b75873f3ce893022fe08d1233b3152d/icons/obj/structures/structures.dmi

### [Resources/Textures/_RMC14/Structures/Storage/Closets/bio.rsi](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Storage/Closets/bio.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/c67c5213507a19e10e252ef2898d94354c0709bf/icons/obj/structures/closet.dmi

### [Resources/Textures/_RMC14/Structures/Storage/Closets/bio_janitor.rsi](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Storage/Closets/bio_janitor.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/c67c5213507a19e10e252ef2898d94354c0709bf/icons/obj/structures/closet.dmi

### [Resources/Textures/_RMC14/Structures/Storage/Closets/bio_scientist.rsi](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Storage/Closets/bio_scientist.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/c67c5213507a19e10e252ef2898d94354c0709bf/icons/obj/structures/closet.dmi

### [Resources/Textures/_RMC14/Structures/Storage/Closets/standard.rsi](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Storage/Closets/standard.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/c67c5213507a19e10e252ef2898d94354c0709bf/icons/obj/structures/closet.dmi

### [Resources/Textures/_RMC14/Structures/Storage/Closets/toolcloset.rsi](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Storage/Closets/toolcloset.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/c67c5213507a19e10e252ef2898d94354c0709bf/icons/obj/structures/closet.dmi

### [Resources/Textures/_RMC14/Structures/Storage/Crates/basic.rsi](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Storage/Crates/basic.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7cb618c69b75873f3ce893022fe08d1233b3152d/icons/obj/structures/crates.dmi

### [Resources/Textures/_RMC14/Structures/Storage/Crates/construction.rsi](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Storage/Crates/construction.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7cb618c69b75873f3ce893022fe08d1233b3152d/icons/obj/structures/crates.dmi

### [Resources/Textures/_RMC14/Structures/Storage/Crates/densecrate.rsi](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Storage/Crates/densecrate.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7cb618c69b75873f3ce893022fe08d1233b3152d/icons/obj/structures/crates.dmi , modified by Hyenh#6078(313846233099927552)

### [Resources/Textures/_RMC14/Structures/Storage/Crates/freezer.rsi](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Storage/Crates/freezer.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7cb618c69b75873f3ce893022fe08d1233b3152d/icons/obj/structures/crates.dmi

### [Resources/Textures/_RMC14/Structures/Storage/Crates/minecart.rsi](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Storage/Crates/minecart.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7cb618c69b75873f3ce893022fe08d1233b3152d/icons/obj/structures/crates.dmi

### [Resources/Textures/_RMC14/Structures/Storage/Crates/plastic.rsi](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Storage/Crates/plastic.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7cb618c69b75873f3ce893022fe08d1233b3152d/icons/obj/structures/crates.dmi

### [Resources/Textures/_RMC14/Structures/Storage/Crates/secure_ammo.rsi](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Storage/Crates/secure_ammo.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7cb618c69b75873f3ce893022fe08d1233b3152d/icons/obj/structures/crates.dmi

### [Resources/Textures/_RMC14/Structures/Storage/Crates/secure_basic.rsi](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Storage/Crates/secure_basic.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7cb618c69b75873f3ce893022fe08d1233b3152d/icons/obj/structures/crates.dmi

### [Resources/Textures/_RMC14/Structures/Storage/Crates/secure_medical.rsi](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Storage/Crates/secure_medical.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7cb618c69b75873f3ce893022fe08d1233b3152d/icons/obj/structures/crates.dmi

### [Resources/Textures/_RMC14/Structures/Storage/Crates/secure_we_ya.rsi](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Storage/Crates/secure_we_ya.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7cb618c69b75873f3ce893022fe08d1233b3152d/icons/obj/structures/crates.dmi

### [Resources/Textures/_RMC14/Structures/Storage/Crates/supply.rsi](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Storage/Crates/supply.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7cb618c69b75873f3ce893022fe08d1233b3152d/icons/obj/structures/crates.dmi

### [Resources/Textures/_RMC14/Structures/Storage/Crates/trash_cart.rsi](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Storage/Crates/trash_cart.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7cb618c69b75873f3ce893022fe08d1233b3152d/icons/obj/structures/crates.dmi

### [Resources/Textures/_RMC14/Structures/Storage/Crates/weapons.rsi](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Storage/Crates/weapons.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7cb618c69b75873f3ce893022fe08d1233b3152d/icons/obj/structures/crates.dmi

### [Resources/Textures/_RMC14/Structures/Storage/Lockers/armory_locker.rsi](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Storage/Lockers/armory_locker.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13

### [Resources/Textures/_RMC14/Structures/Storage/Lockers/engineer_electric.rsi](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Storage/Lockers/engineer_electric.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/c67c5213507a19e10e252ef2898d94354c0709bf/icons/obj/structures/closet.dmi

### [Resources/Textures/_RMC14/Structures/Storage/Lockers/engineer_welder.rsi](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Storage/Lockers/engineer_welder.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/c67c5213507a19e10e252ef2898d94354c0709bf/icons/obj/structures/closet.dmi

### [Resources/Textures/_RMC14/Structures/Storage/Lockers/fridge.rsi](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Storage/Lockers/fridge.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/c67c5213507a19e10e252ef2898d94354c0709bf/icons/obj/structures/closet.dmi

### [Resources/Textures/_RMC14/Structures/Storage/Lockers/gun_cabinet.rsi](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Storage/Lockers/gun_cabinet.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13-pve at https://github.com/cmss13-devs/cmss13-pve/master/icons/obj/structures/props/misc.dmi

### [Resources/Textures/_RMC14/Structures/Storage/Lockers/medical.rsi](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Storage/Lockers/medical.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/c67c5213507a19e10e252ef2898d94354c0709bf/icons/obj/structures/closet.dmi

### [Resources/Textures/_RMC14/Structures/Storage/Lockers/medical_white.rsi](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Storage/Lockers/medical_white.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/c67c5213507a19e10e252ef2898d94354c0709bf/icons/obj/structures/closet.dmi

### [Resources/Textures/_RMC14/Structures/Storage/Lockers/police.rsi](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Storage/Lockers/police.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/c67c5213507a19e10e252ef2898d94354c0709bf/icons/obj/structures/closet.dmi

### [Resources/Textures/_RMC14/Structures/Storage/Lockers/standard.rsi](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Storage/Lockers/standard.rsi/meta.json)

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/c67c5213507a19e10e252ef2898d94354c0709bf/icons/obj/structures/closet.dmi
