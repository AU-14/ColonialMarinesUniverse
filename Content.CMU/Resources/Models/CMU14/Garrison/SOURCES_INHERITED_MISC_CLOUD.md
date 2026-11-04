# Inherited miscellaneous source audit and draft replacements

Pinned source: [TheHellFireo/CMU-Garrison-3D at 6e37a4d0a7d9433838c393a82c02422cb704dd5a](https://github.com/TheHellFireo/CMU-Garrison-3D/tree/6e37a4d0a7d9433838c393a82c02422cb704dd5a).

## Scope and counts

This audit covers exactly **26 inherited queue IDs / 130 saved instances**, from **13 owner YAML files**, checking **13 inherited canonical model candidates across 8 model YAML files**. It is separate from the original **473-ID missing queue** and does not add to or subtract from that queue.

Outcomes: **10 compatible existing draft defaults (20 instances)**; **16 source mismatches with dedicated replacement drafts (110 instances)**; **0 unresolved source identities**. The authored family has **17 draft models: 16 bound replacement IDs plus one unbound bare WeYa clipboard study**. No extra prototype is counted for that study.

Zero unresolved is limited to source identity and named static compositions. It does not mean complete state coverage, native validation, map/contact validation, fidelity approval, or Khronos validation.

## Files and evidence

- Editable family: `Content.CMU/Resources/ThreeD/Prototypes/World/garrison_inherited_misc_cloud.yml`
- Surface registry: `Content.CMU/Resources/ThreeD/Prototypes/World/garrison_inherited_misc_cloud_art.yml`
- Full per-ID/source/blob/license audit: `Tools/three_d/generated/inherited-misc-cloud-audit.json`
- Authored export/crop/palette record: `Tools/three_d/generated/inherited-misc-cloud-verification.json`
- Initial compatible-candidate audit: `reference/inherited-misc-compat-audit.json`
- Actual inherited candidate extraction: `reference/inherited-misc-source/canonical-candidates.json`
- Comparison images: `Tools/three_d/generated/review/inherited-misc-cloud/`
- Unchanged compatible-candidate previews: `reference/inherited-misc-source/compat-previews/`

The audit independently matched 99 fetched source files to their Git blob SHA, checked all 17 current GLB hashes against the authored verification snapshot, reconstructed the 17 source-preview first-frame compositions, checked 24 exact RGBA crops, and checked that non-textured part colors are entries from the selected source-composition palettes. Exact palette membership/crop equality proves provenance; it does not prove correct geometry, region placement or fidelity.

## State and physical limits

- Zero unresolved means source identity and selected compositions were resolved; runtime and physical fidelity remain unverified.
- All five computers show powered overlay frame zero, not animation or a power-aware model.
- Both vendors show unpowered off with maintenance panel closed.
- WeYa bound clipboard is a static initial-four-paper contents interpretation; bare model is unbound. No live ItemMapper implementation.
- Capture default UA flags are compatible; located capture client computes/logs state without applying Sprite/Appearance changes. No working dynamic capture flag claim.
- Only source direction/frame scopes described per case are represented. Hidden surfaces, depths, mounting heights, material response, topology details and native/map contacts remain draft.
- No native engine run, saved-map validation, visual fidelity approval or Khronos validation is asserted by this audit.

## Final bounded artifact checks

The final authored batch contains **357 parts**, **150672 GLB bytes**, **23 registered surfaces**, **24 exact crop uses**, and **333 source-color sample records**; the largest model has **58 parts**.

- `Tools/three_d/generated/inherited-misc-cloud-integrity.json`: direct-export byte agreement, source/crop/palette integrity, zero canonical atlas/source-mapping collisions, and eight local void/solid probes
- `Tools/three_d/generated/inherited-misc-cloud-contacts.json`: all 17 direct exported meshes have one closed-solid contact component under the recorded convex-hull/LP method; this concerns within-model connections, not placement against the game map
- `Tools/three_d/generated/inherited-misc-cloud-blender-import.json`: Blender 4.3.2 imported all 17 current hash-matched GLBs with zero invalid-mesh repairs; this is an import check, not native-engine or fidelity validation
- `Tools/three_d/generated/inherited-misc-baseline-integrity.json`: all 142 pinned checkpoint source-file Git hashes remain unchanged
- `Tools/three_d/generated/inherited-misc-reexport-proof.json`: re-running the author preserved all 17 GLB byte hashes


The later twin-computer source-region check corrected keypad positions to x13/x15 at y10/y12/y14 and sampled the actual #BCBCBC button pixels, added the source lower fasteners, removed the absent black center seam, and sampled the lower chassis from its own source region. Both twin drafts now have 43 parts. The exact targeted checks are recorded in `Tools/three_d/generated/inherited-misc-twin-source-qa.json`; these corrections do not assign reviewed or approved fidelity status.


During review, the narrow MPS screen was found to use a source rectangle nine pixels too far right. The authored source rectangles were corrected to the actual occupied x6..23 range, with CRT crop [12,9,20,18]; the refreshed crop contains the source cyan pixels. This was an authoring/source-coordinate correction, not a native runtime test.

The WeYa source Sprite hides paper/pen in its declaration, while its owner fill specifies four CMPaper and ItemMapper can enable the paper layer on initialization/container changes. The bound draft deliberately shows that static initial-contents composition; the bare source-declared state remains a separate unbound study. A source-declared hidden layer must not be called the complete initialized appearance.

## Per-ID outcomes

### AU14CorporateASRSConsole

- Outcome: compatible_existing_draft_default; 1 saved instances
- Owner: [corporate_asrs.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Prototypes/CMU14/Entities/Structures/Machines/corporate_asrs.yml)
- Inherited candidate: `CMU3DStandingASRSTerminal`
- Source/default proof: Owner only changes requisitions/access/business configuration. CMASRSConsole defines off body plus on screen layers in the same asrs_console.rsi, with needsPower:false. Neither child overrides Sprite, Computer or power defaults. RSI meta has one-direction on/off/broken states.
- Geometry evidence: Read and rendered all 38 canonical parts: brown freestanding case and feet, turquoise screen with yellow/cyan chart, projecting keyboard, lower recess and eight vertical vents. Canonical referenceState is on, matching the visible prototype composite.
- Limits: Existing model is a static draft; no live power/broken-state support established. Source-default compatibility does not approve scale, hidden geometry or visual fidelity.

### AUCMDefibrillatorEmpty

- Outcome: compatible_existing_draft_default; 2 saved instances
- Owner: [audefib.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Prototypes/CMU14/Entities/Objects/Items/Medical/audefib.yml)
- Inherited candidate: `CMU3DPortableDefibrillator`
- Source/default proof: Empty variant overrides ItemSlots.cell_slot only. AU14CMDefibrillator has PowerCellDraw.enabled:false. AU14BaseDefibrillator defines defib base plus invisible defib_on toggle layer; GenericVisualizer toggles only defib_on visibility. Same resource and initial visible base as the filled parent.
- Geometry evidence: Read/rendered all 11 existing parts: blue-gray body, dark face, simplified screen/control, carry handle, paired paddles and leads. Same exterior prop as parent; no cell exterior geometry is represented.
- Limits: The existing screen is a simplified colored rectangle while source default screen is mostly dark; handle/body outlines are simplified. These are existing draft-fidelity limitations shared by parent and child, not a new empty-variant sprite mismatch. No support for animated eleven-frame defib_on, held/worn forms or live battery/toggle visuals established. RSI battery-state names alone do not prove they are active layers.

### CMASRSConsoleColony

- Outcome: compatible_existing_draft_default; 1 saved instances
- Owner: [requisitions.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Prototypes/_RMC14/Entities/Structures/Machines/requisitions.yml)
- Inherited candidate: `CMU3DStandingASRSTerminal`
- Source/default proof: Owner only changes requisitions/access/business configuration. CMASRSConsole defines off body plus on screen layers in the same asrs_console.rsi, with needsPower:false. Neither child overrides Sprite, Computer or power defaults. RSI meta has one-direction on/off/broken states.
- Geometry evidence: Read and rendered all 38 canonical parts: brown freestanding case and feet, turquoise screen with yellow/cyan chart, projecting keyboard, lower recess and eight vertical vents. Canonical referenceState is on, matching the visible prototype composite.
- Limits: Existing model is a static draft; no live power/broken-state support established. Source-default compatibility does not approve scale, hidden geometry or visual fidelity.

### CMPenClicky

- Outcome: replacement_draft_for_source_mismatch; 25 saved instances
- Owner: [pen.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Prototypes/_RMC14/Entities/Objects/Misc/pen.yml)
- Inherited candidate: `CMU3DPen`
- Replacement: `CMU3DCMPenClickyInheritedMiscCloud` (9 parts, draft)
- Parent/child sprite comparison: `CMPen` uses `_RMC14/Objects/Misc/paper.rsi` / `pen`; child uses `_RMC14/Objects/Misc/paper.rsi` / `weya_pen`
- Geometry evidence: Canonical 14-part stair-stepped black diagonal pen has silver clip and dark cap; actual weya_pen includes gold/yellow details and a distinct source silhouette. Replacement has rounded barrel, bands, clip and tip.
- State limit: Single world state only; click mechanism is TODO in owner source. No click, held/worn or ink-selection behavior implemented.

### CMPenFountain

- Outcome: replacement_draft_for_source_mismatch; 32 saved instances
- Owner: [pen.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Prototypes/_RMC14/Entities/Objects/Misc/pen.yml)
- Inherited candidate: `CMU3DPen`
- Replacement: `CMU3DCMPenFountainInheritedMiscCloud` (9 parts, draft)
- Parent/child sprite comparison: `CMPen` uses `_RMC14/Objects/Misc/paper.rsi` / `pen`; child uses `_RMC14/Objects/Misc/paper.rsi` / `fountain_pen`
- Geometry evidence: Canonical plain black/silver pen lacks the fountain source gold/copper accents, pale details and writing nib profile. Replacement uses cylindrical barrel, bands, cap and nib.
- State limit: Single world state only. No writing, ink UI or held/worn appearance support; small physical thickness remains inferred.

### CMSoapDeluxe

- Outcome: replacement_draft_for_source_mismatch; 4 saved instances
- Owner: [soap.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Prototypes/_RMC14/Entities/Objects/Tools/soap.yml)
- Inherited candidate: `CMU3DSoapBar`
- Replacement: `CMU3DCMSoapDeluxeInheritedMiscCloud` (8 parts, draft)
- Parent/child sprite comparison: `CMSoap` uses `_RMC14/Objects/Misc/Janitorial/soap.rsi` / `soap`; child uses `_RMC14/Objects/Misc/Janitorial/soap.rsi` / `soap_deluxe`
- Geometry evidence: Canonical eight-part bar is beige and unmarked; child source is peach/pink. Existing bevel/rim topology can inform the replacement but cannot preserve the beige palette.
- State limit: Single static world state. Solution depletion, fillBaseName deluxe-, residue, consumption and held visuals are unsupported.

### CMSoapNT

- Outcome: replacement_draft_for_source_mismatch; 4 saved instances
- Owner: [soap.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Prototypes/_RMC14/Entities/Objects/Tools/soap.yml)
- Inherited candidate: `CMU3DSoapBar`
- Replacement: `CMU3DCMSoapNTInheritedMiscCloud` (8 parts, draft)
- Parent/child sprite comparison: `CMSoap` uses `_RMC14/Objects/Misc/Janitorial/soap.rsi` / `soap`; child uses `_RMC14/Objects/Misc/Janitorial/soap.rsi` / `soap_nt`
- Geometry evidence: Canonical beige soap cannot represent the lavender/gray WeYa state. Replacement retains raised rim/inset physical structure in the child palette.
- State limit: Single static world state. Solution depletion, fillBaseName weya-, residue, consumption and held visuals are unsupported.

### CMSoapSyndie

- Outcome: replacement_draft_for_source_mismatch; 4 saved instances
- Owner: [soap.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Prototypes/_RMC14/Entities/Objects/Tools/soap.yml)
- Inherited candidate: `CMU3DSoapBar`
- Replacement: `CMU3DCMSoapSyndieInheritedMiscCloud` (8 parts, draft)
- Parent/child sprite comparison: `CMSoap` uses `_RMC14/Objects/Misc/Janitorial/soap.rsi` / `soap`; child uses `_RMC14/Objects/Misc/Janitorial/soap.rsi` / `soap_syndie`
- Geometry evidence: Canonical beige soap cannot represent the bright red syndie state. Replacement uses child colors on a separately modeled raised rim and inset top.
- State limit: Single static world state. Solution depletion, fillBaseName syndie-, residue, consumption and held visuals are unsupported.

### RMCBlackSensorComputer3

- Outcome: replacement_draft_for_source_mismatch; 15 saved instances
- Owner: [telecommunications.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Prototypes/_RMC14/Entities/Structures/Machines/telecommunications.yml)
- Inherited candidate: `CMU3DMappingComputer`
- Replacement: `CMU3DRMCBlackSensorComputer3InheritedMiscCloud` (43 parts, draft)
- Parent/child sprite comparison: `RMCBlackMappingComputer` uses `_RMC14/Structures/hybrisa_computer_props.rsi` / `blackmapping_comp_off + blackmapping_comp`; child uses `_RMC14/Structures/hybrisa_computer_props.rsi` / `blacksensor_comp3_off + blacksensor_comp3`
- Geometry evidence: The 17-part canonical mapping cabinet has one tall cyan map plus a neighboring status screen; child requires two short CRTs and a central keypad, dark full-width hood and different lower controls.
- State limit: Static powered composite: off body plus first blacksensor_comp3 frame. Four 0.3-second powered frames are declared but not animated or power-adapted.

### RMCBoxZiptie

- Outcome: compatible_existing_draft_default; 4 saved instances
- Owner: [handcuff_box.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Prototypes/_RMC14/Catalog/Fills/Boxes/handcuff_box.yml)
- Inherited candidate: `CMU3DRMCBoxHandcuffs`
- Source/default proof: RMCBoxZiptie changes StorageFill to ten zipties, Item size/shape and Tag; no Sprite override. RMCBoxHandcuffs selects handcuff state. RMCBoxCardboard supplies boxes.rsi. Both world visuals therefore use the same one-direction handcuff PNG.
- Geometry evidence: Read/rendered six canonical parts: dark folded carton, rear closure flap, front pictogram, shared top artwork, side seams. Verified registered handcuff front surface is an exact source crop [9,15,22,24]; surface registry indices 241(front),240(top).
- Limits: Contents and Item inventory size do not establish a changed world mesh. No live storage contents behavior modeled; dimensions and hidden faces stay draft.

### RMCCarpetGreyBlue4

- Outcome: replacement_draft_for_source_mismatch; 2 saved instances
- Owner: [carpets.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Prototypes/_RMC14/Entities/Structures/Furniture/carpets.yml)
- Inherited candidate: `CMU3DPrintedRMCCarpetGreyBlue1`
- Replacement: `CMU3DRMCCarpetGreyBlue4InheritedMiscCloud` (2 parts, draft)
- Parent/child sprite comparison: `RMCCarpetGreyBlue1` uses `_RMC14/Structures/Furniture/Carpets/grey_blue_carpet.rsi` / `bcarpet01`; child uses `_RMC14/Structures/Furniture/Carpets/grey_blue_carpet.rsi` / `bcarpet04`
- Geometry evidence: Canonical single carpet slab samples Carpet1 upper/left corner pattern; Carpet4 has left border without that top/corner motif. Replacement uses exact bcarpet04 face and thin backing.
- State limit: Single flat-world tile state only. Saved-neighbor joins, autotiling and map contacts not verified.

### RMCKitchenKnifeButcher

- Outcome: replacement_draft_for_source_mismatch; 2 saved instances
- Owner: [kitchen.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Prototypes/_RMC14/Entities/Objects/Weapons/Melee/kitchen.yml)
- Inherited candidate: `CMU3DLooseKitchenKnife`
- Replacement: `CMU3DRMCKitchenKnifeButcherInheritedMiscCloud` (14 parts, draft)
- Parent/child sprite comparison: `RMCKitchenKnife` uses `_RMC14/Objects/Weapons/Melee/Kitchen/knife.rsi` / `icon`; child uses `_RMC14/Objects/Weapons/Melee/Kitchen/cleaver.rsi` / `icon`
- Geometry evidence: Canonical 17-part loose knife uses a narrow pointed blade and curved cutting edge with knife-specific surfaces. Cleaver source has a broad rectangular blade and hanging hole; a new solid blade, tang, edge and riveted grip is necessary.
- State limit: Loose-world icon only. Held art, combat and cutting behavior unsupported; physical thickness/back construction inferred.

### RMCVendorColaSPP

- Outcome: replacement_draft_for_source_mismatch; 4 saved instances
- Owner: [vending_machines.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Prototypes/_RMC14/Entities/Structures/Machines/Vending/vending_machines.yml)
- Inherited candidate: `CMU3DColaVendor`
- Replacement: `CMU3DRMCVendorColaSPPInheritedMiscCloud` (32 parts, draft)
- Parent/child sprite comparison: `CMVendorCola` uses `_RMC14/Structures/Machines/VendingMachines/cola.rsi` / `off`; child uses `_RMC14/Structures/Machines/VendingMachines/spp_cola.rsi` / `off`
- Geometry evidence: Canonical 33-part orange cola cabinet has three upper controls, orange branded door and one pickup recess. SPP is red with Cyrillic artwork and two distinct pickup openings, so both face layout and geometry differ.
- State limit: Unpowered off reference, maintenance panel closed. Source normal-unshaded/panel/broken states are inventoried; no vend/deny/power/maintenance runtime adapter.

### RMCVendorNutriCoDrink

- Outcome: replacement_draft_for_source_mismatch; 2 saved instances
- Owner: [vending_machines.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Prototypes/_RMC14/Entities/Structures/Machines/Vending/vending_machines.yml)
- Inherited candidate: `CMU3DColaVendor`
- Replacement: `CMU3DRMCVendorNutriCoDrinkInheritedMiscCloud` (58 parts, draft)
- Parent/child sprite comparison: `CMVendorCola` uses `_RMC14/Structures/Machines/VendingMachines/cola.rsi` / `off`; child uses `_RMC14/Structures/Machines/VendingMachines/NutriCo/drink.rsi` / `off`
- Geometry evidence: Canonical short orange cola cabinet differs from gray NutriCo 32x64 canvas, 20-pixel transparent top margin, offset (0,0.5), controls, nozzle grille, cup bay and ventilation. New cabinet uses those source regions.
- State limit: Unpowered off reference with maintenance panel closed. Native offset/contact behavior is not established. Vend has 12 frames; deny has two; all live power/vend/deny/broken/panel behaviors remain unsupported.

### RMCWeYaClipboard

- Outcome: replacement_draft_for_source_mismatch; 1 saved instances
- Owner: [clipboard.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Prototypes/_RMC14/Entities/Objects/Misc/clipboard.yml)
- Inherited candidate: `CMU3DBareClipboard`
- Replacement: `CMU3DRMCWeYaClipboardInheritedMiscCloud` (12 parts, draft)
- Parent/child sprite comparison: `CMClipboard` uses `_RMC14/Objects/Misc/clipboard.rsi` / `clipboard + clipboard_over`; child uses `_RMC14/Objects/Misc/weya_clipboard.rsi` / `clipboard + clipboard_over`
- Geometry evidence: Canonical six-part brown bare clipboard lacks the dark branded WeYa backing. Child owner also specifies four papers. Bound draft models four separate sheets with paper-overlay composite; separate unbound bare study preserves the exposed yellow/green marking.
- State limit: Sprite declaration hides paper and pen; child EntityTableContainerFill specifies four CMPaper. ItemMapper code updates visibility on initialization and insertion/removal. Authored geometry is a static initial-contents interpretation, not an adapter or verified spawned-map state. No pen is invented; removing/adding pages or pen and held/belt states unsupported.

### RMCWhiteMPSComputer

- Outcome: replacement_draft_for_source_mismatch; 2 saved instances
- Owner: [telecommunications.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Prototypes/_RMC14/Entities/Structures/Machines/telecommunications.yml)
- Inherited candidate: `CMU3DMappingComputer`
- Replacement: `CMU3DRMCWhiteMPSComputerInheritedMiscCloud` (24 parts, draft)
- Parent/child sprite comparison: `RMCBlackMappingComputer` uses `_RMC14/Structures/hybrisa_computer_props.rsi` / `blackmapping_comp_off + blackmapping_comp`; child uses `_RMC14/Structures/hybrisa_computer_props.rsi` / `mps_off + mps`
- Geometry evidence: Canonical broad map/status layout is incompatible with the narrow ivory single-CRT cabinet. Source occupied x6..23; the corrected authored branch retains that source pivot and actual cyan CRT crop.
- State limit: Static powered composite: mps_off plus first mps frame. Seven 0.3-second frames; no live power/animation adapter. Initial audit caught a nine-pixel crop/pivot error before regeneration.

### RMCWhiteMappingComputer

- Outcome: replacement_draft_for_source_mismatch; 3 saved instances
- Owner: [telecommunications.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Prototypes/_RMC14/Entities/Structures/Machines/telecommunications.yml)
- Inherited candidate: `CMU3DMappingComputer`
- Replacement: `CMU3DRMCWhiteMappingComputerInheritedMiscCloud` (37 parts, draft)
- Parent/child sprite comparison: `RMCBlackMappingComputer` uses `_RMC14/Structures/hybrisa_computer_props.rsi` / `blackmapping_comp_off + blackmapping_comp`; child uses `_RMC14/Structures/hybrisa_computer_props.rsi` / `mapping_comp_off + mapping_comp`
- Geometry evidence: Canonical cabinet is dark with symbolic mapping bars; source changes the shell to ivory and source-specific map/status screen artwork. Replacement uses raised bezels and source crops.
- State limit: Static powered composite: off body plus first mapping_comp frame. Four 1.5-second powered frames; no live power or animation adapter.

### RMCWhiteSensorComputer2

- Outcome: replacement_draft_for_source_mismatch; 3 saved instances
- Owner: [telecommunications.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Prototypes/_RMC14/Entities/Structures/Machines/telecommunications.yml)
- Inherited candidate: `CMU3DMappingComputer`
- Replacement: `CMU3DRMCWhiteSensorComputer2InheritedMiscCloud` (21 parts, draft)
- Parent/child sprite comparison: `RMCBlackMappingComputer` uses `_RMC14/Structures/hybrisa_computer_props.rsi` / `blackmapping_comp_off + blackmapping_comp`; child uses `_RMC14/Structures/hybrisa_computer_props.rsi` / `sensor_comp2_off + sensor_comp2`
- Geometry evidence: Canonical tall map/status arrangement conflicts with ivory full-width horizontal monitor and long lower switch panel. Distinct screen topology, lower controls and vents are modeled.
- State limit: Static powered composite: off body plus first sensor_comp2 frame. Five powered frames with delays 1,0.4,0.3,0.3,0.3 seconds; no live power/animation adapter.

### RMCWhiteSensorComputer3

- Outcome: replacement_draft_for_source_mismatch; 2 saved instances
- Owner: [telecommunications.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Prototypes/_RMC14/Entities/Structures/Machines/telecommunications.yml)
- Inherited candidate: `CMU3DMappingComputer`
- Replacement: `CMU3DRMCWhiteSensorComputer3InheritedMiscCloud` (43 parts, draft)
- Parent/child sprite comparison: `RMCBlackMappingComputer` uses `_RMC14/Structures/hybrisa_computer_props.rsi` / `blackmapping_comp_off + blackmapping_comp`; child uses `_RMC14/Structures/hybrisa_computer_props.rsi` / `sensor_comp3_off + sensor_comp3`
- Geometry evidence: Canonical dark tall map/status arrangement conflicts with ivory twin short CRTs, central keypad and full-width hood. Source-specific face arrangement and palette are needed.
- State limit: Static powered composite: off body plus first sensor_comp3 frame. Four 0.3-second frames; no live power/animation adapter.

### VehicleInteriorWallPhone

- Outcome: replacement_draft_for_source_mismatch; 5 saved instances
- Owner: [general_interiors.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Prototypes/_RMC14/Vehicles/Common/general_interiors.yml)
- Inherited candidate: `CMU3DWallPhone`
- Replacement: `CMU3DVehicleInteriorWallPhoneInheritedMiscCloud` (21 parts, draft)
- Parent/child sprite comparison: `RMCRotaryPhoneWallmount` uses `_RMC14/Structures/wallmount_phone.rsi` / `wall_phone`; child uses `_RMC14/Structures/Vehicles/Interiors/general.rsi` / `wall_phone`
- Geometry evidence: Canonical 20-part red wall phone differs from gray/green vehicle source although all four alpha silhouettes match. Secondary 15-part red tabletop rotary candidate also has wrong orientation/topology. Replacement uses child south-frame palette/art and receiver-right arrangement.
- State limit: Four directions inspected, south frame drives local draft. Ring has two 0.3-second frames per direction; ring/off-hook, UI, wall contacts and live state selection remain unsupported.

### captureadminobjective

- Outcome: compatible_existing_draft_default; 2 saved instances
- Owner: [captureobjectives.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Prototypes/CMU14/RoundSetup/Intel/Objectives/captureobjectives.yml)
- Inherited candidate: `CMU3DPrintedAU14WallFlagUA`
- Source/default proof: Owner directly inherits AU14WallFlagUA with objective/capture/nav-map components and no Sprite override. Parent defines uaflag from wallflags.rsi, one 96x32 frame. The exact same inherited default is used for all six capture IDs.
- Geometry evidence: Read/rendered four canonical parts: a shallow printed cloth, top rod and two clasps. Registered surface CMU3DSurfaceAU14WallFlagUA (index 8) is byte-for-byte original uaflag crop [12,3,52,29]. Left-of-pivot cloth anchor and wall-mounted geometry retained.
- Limits: Source runtime capture visuals are not proven to apply. Client UpdateFlagSpriteState checks Appearance then computes/logs a selected string, without setting Sprite or Appearance data. The resolved prototype component list lacks Appearance. Do not claim working capture-driven flag changes. Server computes GovforFlagState/OpforFlagState from selected platoons; fallback opfor uses uaflagworn, but actual RSI state is uaflag_worn. The component defaults use uaflag_worn/uaflag and client CLF fallback selects clfflag. Those strings are evidence of intent, not implemented render changes. Existing model only represents uaflag and retains draft depth/mounting/material assumptions. Do not add invented capture animations or claim fidelity approval.

### capturedesertobjective

- Outcome: compatible_existing_draft_default; 2 saved instances
- Owner: [captureobjectives.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Prototypes/CMU14/RoundSetup/Intel/Objectives/captureobjectives.yml)
- Inherited candidate: `CMU3DPrintedAU14WallFlagUA`
- Source/default proof: Owner directly inherits AU14WallFlagUA with objective/capture/nav-map components and no Sprite override. Parent defines uaflag from wallflags.rsi, one 96x32 frame. The exact same inherited default is used for all six capture IDs.
- Geometry evidence: Read/rendered four canonical parts: a shallow printed cloth, top rod and two clasps. Registered surface CMU3DSurfaceAU14WallFlagUA (index 8) is byte-for-byte original uaflag crop [12,3,52,29]. Left-of-pivot cloth anchor and wall-mounted geometry retained.
- Limits: Source runtime capture visuals are not proven to apply. Client UpdateFlagSpriteState checks Appearance then computes/logs a selected string, without setting Sprite or Appearance data. The resolved prototype component list lacks Appearance. Do not claim working capture-driven flag changes. Server computes GovforFlagState/OpforFlagState from selected platoons; fallback opfor uses uaflagworn, but actual RSI state is uaflag_worn. The component defaults use uaflag_worn/uaflag and client CLF fallback selects clfflag. Those strings are evidence of intent, not implemented render changes. Existing model only represents uaflag and retains draft depth/mounting/material assumptions. Do not add invented capture animations or claim fidelity approval.

### capturemineobjective

- Outcome: compatible_existing_draft_default; 2 saved instances
- Owner: [captureobjectives.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Prototypes/CMU14/RoundSetup/Intel/Objectives/captureobjectives.yml)
- Inherited candidate: `CMU3DPrintedAU14WallFlagUA`
- Source/default proof: Owner directly inherits AU14WallFlagUA with objective/capture/nav-map components and no Sprite override. Parent defines uaflag from wallflags.rsi, one 96x32 frame. The exact same inherited default is used for all six capture IDs.
- Geometry evidence: Read/rendered four canonical parts: a shallow printed cloth, top rod and two clasps. Registered surface CMU3DSurfaceAU14WallFlagUA (index 8) is byte-for-byte original uaflag crop [12,3,52,29]. Left-of-pivot cloth anchor and wall-mounted geometry retained.
- Limits: Source runtime capture visuals are not proven to apply. Client UpdateFlagSpriteState checks Appearance then computes/logs a selected string, without setting Sprite or Appearance data. The resolved prototype component list lacks Appearance. Do not claim working capture-driven flag changes. Server computes GovforFlagState/OpforFlagState from selected platoons; fallback opfor uses uaflagworn, but actual RSI state is uaflag_worn. The component defaults use uaflag_worn/uaflag and client CLF fallback selects clfflag. Those strings are evidence of intent, not implemented render changes. Existing model only represents uaflag and retains draft depth/mounting/material assumptions. Do not add invented capture animations or claim fidelity approval.

### captureobjectiveneroidgeothermalgenerators

- Outcome: compatible_existing_draft_default; 2 saved instances
- Owner: [captureobjectives.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Prototypes/CMU14/RoundSetup/Intel/Objectives/captureobjectives.yml)
- Inherited candidate: `CMU3DPrintedAU14WallFlagUA`
- Source/default proof: Owner directly inherits AU14WallFlagUA with objective/capture/nav-map components and no Sprite override. Parent defines uaflag from wallflags.rsi, one 96x32 frame. The exact same inherited default is used for all six capture IDs.
- Geometry evidence: Read/rendered four canonical parts: a shallow printed cloth, top rod and two clasps. Registered surface CMU3DSurfaceAU14WallFlagUA (index 8) is byte-for-byte original uaflag crop [12,3,52,29]. Left-of-pivot cloth anchor and wall-mounted geometry retained.
- Limits: Source runtime capture visuals are not proven to apply. Client UpdateFlagSpriteState checks Appearance then computes/logs a selected string, without setting Sprite or Appearance data. The resolved prototype component list lacks Appearance. Do not claim working capture-driven flag changes. Server computes GovforFlagState/OpforFlagState from selected platoons; fallback opfor uses uaflagworn, but actual RSI state is uaflag_worn. The component defaults use uaflag_worn/uaflag and client CLF fallback selects clfflag. Those strings are evidence of intent, not implemented render changes. Existing model only represents uaflag and retains draft depth/mounting/material assumptions. Do not add invented capture animations or claim fidelity approval.

### captureobjectiveneroidspaceport

- Outcome: compatible_existing_draft_default; 2 saved instances
- Owner: [captureobjectives.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Prototypes/CMU14/RoundSetup/Intel/Objectives/captureobjectives.yml)
- Inherited candidate: `CMU3DPrintedAU14WallFlagUA`
- Source/default proof: Owner directly inherits AU14WallFlagUA with objective/capture/nav-map components and no Sprite override. Parent defines uaflag from wallflags.rsi, one 96x32 frame. The exact same inherited default is used for all six capture IDs.
- Geometry evidence: Read/rendered four canonical parts: a shallow printed cloth, top rod and two clasps. Registered surface CMU3DSurfaceAU14WallFlagUA (index 8) is byte-for-byte original uaflag crop [12,3,52,29]. Left-of-pivot cloth anchor and wall-mounted geometry retained.
- Limits: Source runtime capture visuals are not proven to apply. Client UpdateFlagSpriteState checks Appearance then computes/logs a selected string, without setting Sprite or Appearance data. The resolved prototype component list lacks Appearance. Do not claim working capture-driven flag changes. Server computes GovforFlagState/OpforFlagState from selected platoons; fallback opfor uses uaflagworn, but actual RSI state is uaflag_worn. The component defaults use uaflag_worn/uaflag and client CLF fallback selects clfflag. Those strings are evidence of intent, not implemented render changes. Existing model only represents uaflag and retains draft depth/mounting/material assumptions. Do not add invented capture animations or claim fidelity approval.

### captureuaflagobjective

- Outcome: compatible_existing_draft_default; 2 saved instances
- Owner: [captureobjectives.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Prototypes/CMU14/RoundSetup/Intel/Objectives/captureobjectives.yml)
- Inherited candidate: `CMU3DPrintedAU14WallFlagUA`
- Source/default proof: Owner directly inherits AU14WallFlagUA with objective/capture/nav-map components and no Sprite override. Parent defines uaflag from wallflags.rsi, one 96x32 frame. The exact same inherited default is used for all six capture IDs.
- Geometry evidence: Read/rendered four canonical parts: a shallow printed cloth, top rod and two clasps. Registered surface CMU3DSurfaceAU14WallFlagUA (index 8) is byte-for-byte original uaflag crop [12,3,52,29]. Left-of-pivot cloth anchor and wall-mounted geometry retained.
- Limits: Source runtime capture visuals are not proven to apply. Client UpdateFlagSpriteState checks Appearance then computes/logs a selected string, without setting Sprite or Appearance data. The resolved prototype component list lacks Appearance. Do not claim working capture-driven flag changes. Server computes GovforFlagState/OpforFlagState from selected platoons; fallback opfor uses uaflagworn, but actual RSI state is uaflag_worn. The component defaults use uaflag_worn/uaflag and client CLF fallback selects clfflag. Those strings are evidence of intent, not implemented render changes. Existing model only represents uaflag and retains draft depth/mounting/material assumptions. Do not add invented capture animations or claim fidelity approval.

## Canonical model files actually inspected

- `CMU3DPen`: [garrison_interiors.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_interiors.yml), 14 parts, `db986515b935d11612d86fe051a546f5c8ba0f15`
- `CMU3DPortableDefibrillator`: [garrison_utilities.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_utilities.yml), 11 parts, `a8c910e108f92cd80b8ab9ee06bc876267c33990`
- `CMU3DRotaryPhone`: [garrison_utilities.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_utilities.yml), 15 parts, `a8c910e108f92cd80b8ab9ee06bc876267c33990`
- `CMU3DWallPhone`: [garrison_utilities.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_utilities.yml), 20 parts, `a8c910e108f92cd80b8ab9ee06bc876267c33990`
- `CMU3DMappingComputer`: [garrison_utilities.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_utilities.yml), 17 parts, `a8c910e108f92cd80b8ab9ee06bc876267c33990`
- `CMU3DStandingASRSTerminal`: [garrison_consoles.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_consoles.yml), 38 parts, `bea4d61aed802e3bb5bcf8a5253d302bf50192cd`
- `CMU3DSoapBar`: [garrison_smallprops.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_smallprops.yml), 8 parts, `3a663f78246e1cd8c148ba84864e7070be306b30`
- `CMU3DBareClipboard`: [garrison_smallprops.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_smallprops.yml), 6 parts, `3a663f78246e1cd8c148ba84864e7070be306b30`
- `CMU3DLooseKitchenKnife`: [garrison_loose_tools.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_loose_tools.yml), 17 parts, `c6b13561b0ed1ac909cb798a22c8301a644441e6`
- `CMU3DPrintedAU14WallFlagUA`: [garrison_printed_props.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_printed_props.yml), 4 parts, `3dc4cd9c0b70f92cfcbebdd494ff30c52e2af62f`
- `CMU3DPrintedRMCCarpetGreyBlue1`: [garrison_printed_props.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_printed_props.yml), 1 parts, `3dc4cd9c0b70f92cfcbebdd494ff30c52e2af62f`
- `CMU3DColaVendor`: [garrison_models.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_models.yml), 33 parts, `75992e790d35875f6e6e99e300ed4821db553882`
- `CMU3DRMCBoxHandcuffs`: [garrison_supplies.yml](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_supplies.yml), 6 parts, `d98eef568bac65743588a813f98586227c14323d`

## Source artwork licenses and attribution

Original source pixel crops and source-derived palette values are retained. Preserve these upstream notices with redistributed derivatives. The WeYa clipboard artwork has **CC-BY-SA-4.0** metadata; the other source RSI metadata in this audit is **CC-BY-SA-3.0**. The exact metadata and source-file hashes are retained in the JSON audit.

### CMU14/Items/audefib.rsi

- License: CC-BY-SA-3.0
- Pinned metadata: [meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Textures/CMU14/Items/audefib.rsi/meta.json)
- Git blob SHA: `4129d78491ccb36f028bcc01a856a19ebe9e622b`
- Upstream attribution: Normal defib sprites by aleksh on discord. Advanced Defib Sprites Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/05eaa3484b1a35aa759300bcfc8f0119f009daae/icons/obj/items/devices.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/medical_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/medical_lefthand.dmi

### CMU14/Structures/wallflags.rsi

- License: CC-BY-SA-3.0
- Pinned metadata: [meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Content.CMU/Resources/Textures/CMU14/Structures/wallflags.rsi/meta.json)
- Git blob SHA: `200a95dd3240bfdedc8e729a602e38bddaa8d880`
- Upstream attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/wall_decorations/banners.dmi

### _RMC14/Objects/Misc/Janitorial/soap.rsi

- License: CC-BY-SA-3.0
- Pinned metadata: [meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Misc/Janitorial/soap.rsi/meta.json)
- Git blob SHA: `c39eb3f84e0633dc0a3525ce3179cde5fe8bc122`
- Upstream attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/5a2359bab582e18b3b432539733be04204148a5e/icons/obj/items/items.dmi

### _RMC14/Objects/Misc/clipboard.rsi

- License: CC-BY-SA-3.0
- Pinned metadata: [meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Misc/clipboard.rsi/meta.json)
- Git blob SHA: `9540dcf47be7e392400ce9e06e83efd471454784`
- Upstream attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/9ab207cd7ffba86a0411d7058645fb8f2a7895f3/icons/obj/items/paper.dmi, clipboard_paper modified by KalimbaMachine (Github) from cmss13 paper.dmi, inhand sprites modified by KalimbaMachine (Github) from nmajask (Github) for SS14's inhand sprites, equipped-BELT Made by KalimbaMachine (Github), clipboard_pen made by KalimbaMachine (Github)

### _RMC14/Objects/Misc/paper.rsi

- License: CC-BY-SA-3.0
- Pinned metadata: [meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Misc/paper.rsi/meta.json)
- Git blob SHA: `784b754dea47de9aa05c0a11260047c833463ec8`
- Upstream attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/9ab207cd7ffba86a0411d7058645fb8f2a7895f3/icons/obj/items/paper.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/equipment/paperwork_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/equipment/paperwork_righthand.dmi, paper_stamp-provost, paper_stamp-provost-inspector, stamp-provost, paper_stamp-sea, and stamp-sea made by pursuitinashes (discord) based off of paper_stamp-marine and stamp-deny. paper_stamp-clf, paper_stamp-spp, paper_stamp-tse, and paper_stamp-free-press created by crazy1112345 (discord). stamp-clf, stamp-spp, stamp-tse, and stamp-free-press created by crazy1112345, based on stamp-marine. weya_pen made by SharkSnake98. Standard paper stamp overlays taken from tgstation at https://github.com/tgstation/tgstation/commit/e1142f20f5e4661cb6845cfcf2dd69f864d67432, with paper_stamp-syndicate by Veritius, paper_stamp-greytide by ubaser, paper_stamp-psychologist by clinux, and paper_stamp-wizard by brassicaprime69 (Discord), paper_stamp-cca by Oslo, https://github.com/cmss13-devs/cmss13/blob/52681260f1021befe2cdce04d8e21deee78e8b07/icons/obj/items/paper.dmi

### _RMC14/Objects/Misc/weya_clipboard.rsi

- License: CC-BY-SA-4.0
- Pinned metadata: [meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Misc/weya_clipboard.rsi/meta.json)
- Git blob SHA: `4a6f868734463dd7f1ad4cfb99e841d02a26b590`
- Upstream attribution: Made by SharkSnake98 on Github

### _RMC14/Objects/Storage/boxes.rsi

- License: CC-BY-SA-3.0
- Pinned metadata: [meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Storage/boxes.rsi/meta.json)
- Git blob SHA: `7b368fe0c16ef94b47600400a9fb898f59b2445a`
- Upstream attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/71d46ee8057d19b12e1495419cb299d2fedef6cc/icons/obj/items/storage.dmi, https://github.com/cmss13-devs/cmss13/blob/c648dca374ddee1c80f61ab88678c287356fa66f/icons/obj/items/storage.dmi, l96 modified from m94 by github monomethylhydrazine, https://github.com/cmss13-devs/cmss13/blob/b6f4841b5768599c0fcf58a21fd88536a9959c2c/icons/obj/items/storage/boxes.dmi

### _RMC14/Objects/Weapons/Melee/Kitchen/cleaver.rsi

- License: CC-BY-SA-3.0
- Pinned metadata: [meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Weapons/Melee/Kitchen/cleaver.rsi/meta.json)
- Git blob SHA: `7dc5f8df2b307bf6d4b2be4010c1c27288bb2f85`
- Upstream attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/obj/items/kitchen_tools.dmi, https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/mob/humans/onmob/inhands/equipment/kitchen_tools_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/mob/humans/onmob/inhands/equipment/kitchen_tools_righthand.dmi

### _RMC14/Objects/Weapons/Melee/Kitchen/knife.rsi

- License: CC-BY-SA-3.0
- Pinned metadata: [meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Weapons/Melee/Kitchen/knife.rsi/meta.json)
- Git blob SHA: `aa7b9405f89a995e1ae74e2c909188bf59b6bed4`
- Upstream attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/obj/items/kitchen_tools.dmi, https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/mob/humans/onmob/inhands/weapons/melee/knives_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/mob/humans/onmob/inhands/weapons/melee/knives_righthand.dmi

### _RMC14/Structures/Furniture/Carpets/grey_blue_carpet.rsi

- License: CC-BY-SA-3.0
- Pinned metadata: [meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Furniture/Carpets/grey_blue_carpet.rsi/meta.json)
- Git blob SHA: `f1959f8edfdb8a269418513747b81d30c8b7350f`
- Upstream attribution: Taken from cmss13 at: https://github.com/cmss13-devs/cmss13/blob/9dd2c0d0a1c21e3c9bddbad1321c63fd886f61cf/icons/turf/floors/hybrisafloors.dmi

### _RMC14/Structures/Machines/VendingMachines/NutriCo/drink.rsi

- License: CC-BY-SA-3.0
- Pinned metadata: [meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Machines/VendingMachines/NutriCo/drink.rsi/meta.json)
- Git blob SHA: `9ba651d5b3d970d85bbab7b6dc8cef778d975a29`
- Upstream attribution: Taken from cmss13-pve at https://github.com/cmss13-devs/cmss13-pve/blob/master/icons/obj/structures/machinery/vending_32x64.dmi, deny and panel sprites by github noctyrnal

### _RMC14/Structures/Machines/VendingMachines/cola.rsi

- License: CC-BY-SA-3.0
- Pinned metadata: [meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Machines/VendingMachines/cola.rsi/meta.json)
- Git blob SHA: `d2598ed6995bf7e59cc24cee750d0b554b08f2d2`
- Upstream attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/09a5191fb11aab8ddffe3f9be94292b53e4d96f6/icons/obj/structures/machinery/vending.dmi

### _RMC14/Structures/Machines/VendingMachines/spp_cola.rsi

- License: CC-BY-SA-3.0
- Pinned metadata: [meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Machines/VendingMachines/spp_cola.rsi/meta.json)
- Git blob SHA: `dc3099d17fd62ac0372d2cb7a68ef178aa3066b3`
- Upstream attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13-pve/blob/master/icons/obj/structures/machinery/vending.dmi, broken sprite by github noctyrnal

### _RMC14/Structures/Machines/asrs_console.rsi

- License: CC-BY-SA-3.0
- Pinned metadata: [meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Machines/asrs_console.rsi/meta.json)
- Git blob SHA: `45f6fb9de1222e5baab9db3d69a8f9d7d0822cbc`
- Upstream attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/d5b119380250ea512db2a5319e36592c7f604250/icons/obj/structures/machinery/computer.dmi

### _RMC14/Structures/Vehicles/Interiors/general.rsi

- License: CC-BY-SA-3.0
- Pinned metadata: [meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/Vehicles/Interiors/general.rsi/meta.json)
- Git blob SHA: `e0f6f5be5648af6968e4b2a3d20c94260ba2dc32`
- Upstream attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/tree/master/icons/obj/vehicles/interiors/general

### _RMC14/Structures/hybrisa_computer_props.rsi

- License: CC-BY-SA-3.0
- Pinned metadata: [meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/hybrisa_computer_props.rsi/meta.json)
- Git blob SHA: `79c719c76ca4d5d9e0bea71c48cea6630e309fa9`
- Upstream attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/structures/props/hybrisa/computers.dmi

### _RMC14/Structures/wallmount_phone.rsi

- License: CC-BY-SA-3.0
- Pinned metadata: [meta.json](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/wallmount_phone.rsi/meta.json)
- Git blob SHA: `9efffa45246cc671ad8b86f55d84719b97e4d9d6`
- Upstream attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/obj/structures/phone.dmi
