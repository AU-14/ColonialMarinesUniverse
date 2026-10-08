# Final missing vending families: physical art drafts

## Deliverables and scope

Eleven distinct solid assemblies map all twelve remaining requested VendingMachines
source IDs, covering **22 Redux and 8 classic saved placements** in the supplied
inventory. These counts describe exact-ID draft art mappings, not native execution,
map-fit validation, gameplay conversion, or fidelity approval.

- `CMU3DVendorSovietCloud`: `CMVendorSodaSoviet`, 36 parts
- `CMU3DVendorSecurityCloud`: `CMVendorSec`, 39 parts
- `CMU3DVendorMedicalGearCloud`: `AU14CivilianEmergencyResponderOfficerVendor` and
  `AU14CivilianMedicalClothingVendor`, 50 parts
- `CMU3DVendorWYGuardCloud`: `CMUWYGuardequipmentvendor`, 38 parts
- `CMU3DVendorDinnerwareCloud`: `CMVendorDinnerware`, 37 parts
- `CMU3DVendorM34EmptyRackCloud`: `RMCGunRackM34IncineratorEmpty`, 24 parts
- `CMU3DVendorBoozeCloud`: `CMVendorBooze`, 49 parts
- `CMU3DVendorElectronicsCloud`: `CMVendorElectronics`, 36 parts
- `CMU3DVendorCondimentsCloud`: `VendingMachineCondiments`, 49 parts
- `CMU3DVendorBloodFieldCloud`: `AU14VendorBloodField`, 51 parts
- `CMU3DVendorComponentCloud`: `CMVendorComponent`, 29 parts

The 438 named solid nodes use 88 unique, unresampled source crops (98 uses), atlas
slots **1901–1988**, in `Textures/CMU14/ThreeD/vending_extra_cloud/`. Editable model
and surface definitions are `garrison_vending_extra_cloud.yml` and
`garrison_vending_extra_cloud_art.yml` under `Prototypes/CMU14/ThreeD/`.
The GLBs in this directory are byte-for-byte output from the unchanged exporter,
with embedded PNGs, 5,256 total triangles and zero animation clips.

Rebuild: `python Tools/three_d/author_vending_extra_cloud.py`.
Check: `python Tools/three_d/verify_vending_extra_cloud.py`.
Independent import: `blender -b -t 1 --python Tools/three_d/check_vending_extra_blender.py`.
All models remain `status: draft`.

## Source retrieval, attribution and reproducible evidence

Repository: [TheHellFireo/CMU-Garrison-3D, Chip/garrison-3d](https://github.com/TheHellFireo/CMU-Garrison-3D/tree/Chip/garrison-3d).
Actual RSI meta and **every listed PNG state** were fetched through the GitHub
connector (base64 for PNGs), not reconstructed from thumbnails. Relevant child and
inherited prototype definitions were fetched separately. Source visualizer code was
read only and kept as `.cs.txt` evidence outside runtime source directories.

`Tools/three_d/generated/vending-extra-source-manifest.json` records every fetched
path, Git blob SHA and verified source URL. Original meta notices remain beside
all source PNGs. Prototype/code snapshots are under
`Tools/three_d/generated/vending-extra-sources/`.

Ten CM/RMC resources are **CC-BY-SA-3.0**. Unless noted below, their meta attribution
is: Taken from cmss13 at
https://github.com/cmss13-devs/cmss13/blob/09a5191fb11aab8ddffe3f9be94292b53e4d96f6/icons/obj/structures/machinery/vending.dmi

The empty M34 rack is attributed to cmss13
https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/structures/gun_racks.dmi;
its filled states are credited to GitHub noctyrnal. No filled-state art is applied
to this empty rack draft. `condiments.rsi` is **CC0-1.0**, made by EmoGarbage404 on
GitHub. Retain notices and source references with derivative models, textures and
previews, particularly the CC-BY-SA material.

## Per-family source inspection and geometry

All eleven RSIs have 32×32 frames and one direction. Horizontal scale is exactly
1/32 tile per source pixel. Height is the existing vendor convention, .038 tile
per source row, and is explicitly inferred. The condiment station is grounded at
its actual last visible row, y=22, rather than being elevated by ten transparent
bottom rows; dinnerware is grounded at y=31. Other families use y=32.

### Soviet water / BODA

- RSI `_RMC14/Structures/Machines/VendingMachines/sovietsoda.rsi`
- Prototype: RMC `Vending/vending_machines.yml`; inherits `CMVendor` →
  `VendingMachine` → `BaseVendingMachine`
- `CMVendor.ApcPowerReceiver.needsPower: false`; `VendingMachineVisuals` inherits
  `offState: off`, `normalState: normal-unshaded`, `brokenState: broken`
- Authored source-normal composition: `off` + first `normal-unshaded` frame,
  maintenance panel closed
- Silver cabinet, separate turquoise sign inset retaining Cyrillic artwork,
  turquoise control flanks, five raised silver selection buttons, right selector,
  and a genuinely recessed pickup with side walls and floor
- Opaque source footprint x=5..27, y=1..31; inferred depth .61 tiles
- `normal-unshaded`: seven frames, delays .1/.1/.1/12/.1/.1/.1 seconds
  The 12-second hold is source data; no animation binding or fabricated clock

### Security / SecTech

- RSI `_RMC14/Structures/Machines/VendingMachines/sec.rsi`
- Same powered base inheritance as BODA; exact child overrides denial/ejection
  to `deny-unshaded` / `eject-unshaded`
- Authored `off` + `normal-unshaded`, panel closed
- Olive display surround, recessed blue back, five separately volumetric original
  stock-face groups, orange label and thirteen physical ventilation slats
- Inferred depth .76 tiles. Printed restraints remain source-pixel groups; exact
  physical cuff curvature and the compartment's glazing remain unverified

### Civilian medical clothing / emergency responder

- RSI `_RMC14/Structures/Machines/VendingMachines/ColMarTech/medical_gear.rsi`
- Both children in CMU `Economy/Vendors/civilianvendors.yml` explicitly replace
  inherited layers with one `base` layer, mapped to both Base and BaseUnshaded
- Authoring uses that **open stocked base**, not its visibly different closed/off
  grille. No power assumption is needed to choose between the explicit layers
- Separate body, deep interior, three full-depth shelves, seventeen volumetric
  cartons/tubes/kits/bottles, original individual front prints, lower equipment
  unit and plinth; inferred depth .78 tiles
- The two source IDs genuinely share identical source geometry/art. Inventory
  differences do not invent additional visible items

### We-Yu guard equipment

- RSI `_RMC14/Structures/Machines/VendingMachines/ColMarTech/requisitions_ammo.rsi`
- CMU `Economy/Vendors/wyguardvendor.yml` inherits `ColMarTechBase`'s `off` + `base`
- Open stocked base: silver header, full-depth upper/base shelves, eight distinct
  stock groups, dark rear liner with an exact diamond-pattern crop and hidden
  support strips. The closed metal grille is not substituted
- Inferred depth .79 tiles; hidden supporting shelf under mid-height stock is an
  explicitly inferred physical support, not source-visible stock

### Dinnerware

- RSI `_RMC14/Structures/Machines/VendingMachines/dinnerware.rsi`
- RMC `Vending/vending_machines.yml`, `ColMarTechBaseAnchorable` ancestor
- Child explicitly has `needsPower: false`, `off` + `normal-unshaded` and a
  maintenance-panel layer. The panel is closed by the source component default
- Narrow ochre cabinet, stepped round shoulders, cyan display, raised dish marks,
  framed pickup cavity, lower service panel and two silver feet; depth .54 tiles
- The stepped shoulders and dish extrusion are draft interpretations of source
  rounded forms, not established physical dimensions

### Empty M240 incinerator rack

- RSI `_RMC14/Structures/Machines/VendingMachines/GunRacks/m34_rack.rsi`
- RMC `GunRacks/misc.yml`, ancestor `GunRacks/base.yml`
- Exact ID is `RMCGunRackM34IncineratorEmpty`; the source display name says M240
  while the RSI/whitelist retain M34. Neither identifier was silently renamed
- Inherited Sprite state is `empty`. Both ContainerSlot entities are null and no
  ContainerFill is present on this prototype. ItemMapper adds `fill_1`/`fill_2`
  at one/two qualifying guns; those content states are unsupported here
- Open-depth cabinet, two physical crossmembers, original dark diamond liner,
  two inferred retaining uprights/hooks/cradles. **No gun contents are invented**
- Inferred depth .57 tiles; retaining construction is deliberately labeled inferred

### Booze-O-Mat

- RSI `_RMC14/Structures/Machines/VendingMachines/boozeomat.rsi`
- RMC `Vending/food.yml` explicitly defines `off` + `normal-unshaded` layers with
  no shader/map metadata on those child layers
- Original double white header stripes, inset blue bottle bay, four separate
  bottle-stock groups, two green control columns, tall pickup opening and bottom
  ventilation baffles; inferred depth .63 tiles
- Source colors are retained; no claim of emitted/unshaded light or live power

### Electronics vendor

- RSI `_RMC14/Structures/Machines/VendingMachines/engivend.rsi`
- RMC `Vending/engineering.yml`; child inherits `CMVendorTool`'s explicit
  `off` + `normal-unshaded` layers
- Yellow-striped header, separate gold door/frame members, narrow green selector
  strip, three individually raised original circuit groups and wide pickup recess
- Inferred depth .68 tiles; internal circuitry remains a low-resolution printed
  group, with its physical circuit construction unverified

### Condiment station

- RSI `Structures/Machines/VendingMachines/condiments.rsi`
- Base `Entities/Structures/Machines/vending_machines.yml`, `BaseVendingMachine`
- Exact source layer is `off`; `icon` is not substituted. Source child says
  `Transform.noRot: false`, while Sprite retains snapCardinals from the ancestor
- Five original condiment bodies with separately raised necks/lid, four colored
  dispensing nozzles and two open plate bays with separately stacked plates
- Inferred depth .43 tiles. No full roof crosses the exposed bottles
- Review caught and corrected the right neck: original two-pixel neck is x=24,
  not transparent x=25. No empty or invented replacement texture remains

### Field blood dispenser

- RSI `_RMC14/Structures/Machines/VendingMachines/blood.rsi`
- CMU `Economy/Vendors/medicalvendor.yml`, `ColMarTechBaseAnchorable` ancestor
- Explicit `off` + `normal-unshaded`, panel `visible: false`, `needsPower: false`
- Cream original logo/header, nine individual red pack fronts on three shelf
  lips, blue recessed compartment, green control strip, pickup recess and green
  lower panel; inferred depth .61 tiles

### Component storage

- RSI `_RMC14/Structures/Machines/VendingMachines/engi.rsi`
- Inherits `CMVendorTool`'s `off` + `normal-unshaded` composition
- Brown cabinet, two upper switch recesses, separate framed amber-mark display,
  wide collection cavity and bottom plinth; inferred depth .62 tiles
- Authored first normal frame only. Normal/eject have six frames with delays
  .1/.2/.1/.2/.1/.2 seconds; broken has five at 2.5/.02/.03/.08/.02 seconds
- The display is retained as printed artwork on a recessed physical display,
  not converted into invented dispensing stock or falsely claimed animation

## Default composition, state coverage and limits

`Content.Client/VendingMachines/VendingMachineSystem.cs` was inspected. It sets
Base to OffState and, for Normal, BaseUnshaded to NormalState; it hides the unshaded
and screen layers for Off/Broken. The inherited YAML's initial two `off` entries
therefore do not describe a fully initialized normal-powered BODA/SecTech display.
Our comparison images explicitly show the chosen source-normal composition.

`WiresPanelComponent.Open` defaults false. The inspected Wires visualizer hides the
panel when closed or when appearance data is absent (`VisibleWhenClosed` defaults
false). Thus dinnerware/BODA/SecTech do not acquire a permanently open panel merely
because their YAML lists a panel layer without `visible: false`.

No new state adapter or runtime code was installed. `Transform.noRot` is not proof
of `Sprite.noRot`, and multi-layer visualizers cannot be treated as single-layer
`spriteStates`. All variants remain explicit static drafts. Alternate maintenance,
power, broken, denial/vending, equipment closed-grille, rack content and animated
lighting states are unsupported. All source state metadata is retained in the
verification JSON. SecTech, booze, electronics and blood each have two-frame
.1/.1-second denial/ejection art; all those clips remain unimplemented. Condiment
has only static `off`/`icon`; the rack's empty/fill states are static source overlays.

Source-facing references use exact `off`, `base` or `empty` state names. Custom
comparison cards show the full authored layer composition and label the first
frame explicitly. No single source sprite is pasted over an entire cabinet:
color-bodied walls, roof, back, floor, shelves, frames, cavities, stock and controls
have separate positive-volume geometry. Original artwork crops cover specific
printed panels, product fronts and woven liners. Hidden sides/back and depths are
inferred and plain; source-visible face detail does not establish their fidelity.

## Verification and review

- `generated/vending-extra-cloud-verification.json`: parts, mappings, inventory
  counts, 98 exact crop uses, all RSI states/delays and unsupported behavior
- `generated/vending-extra-source-byte-audit.json`: all 76 source files exactly
  match their fetched Git blob SHA, including all RSI PNG/meta states
- `generated/vending-extra-cloud-integrity.json`: all 11 GLBs exactly match direct
  `build_models.glb_bytes` output; finite accessors/buffer ranges, index bounds,
  embedded images, exact reference resources, drafts, surfaces and crop bytes pass
- Unmodified global `surfaces.load_surfaces()` passes; all 88 family images are
  nonempty and atlas indices are unique within the selected range
- `generated/vending-extra-cloud-blender-import.json`: Blender 4.3.2 independently
  imports all eleven final GLBs, zero invalid-mesh repairs, zero animation actions
- `generated/review/vending-extra-cloud/`: eleven source-composition images,
  eleven source/front/orbit comparison cards and 33 front/orbit/rear renders
- All 11 comparison cards were visually inspected. Source-height ratio is taller
  by design (.038 versus .03125); hidden construction and depth remain inference

Khronos glTF validator is not installed in this workspace, so no Khronos-clean claim
is made. Blender import and raw-buffer checks are independent bounded evidence,
not substitutes for native renderer, full-map contact, runtime state or fidelity
review. This batch did not start the game/server, edit engine/runtime code, mutate
saved maps, change the exporter, publish, or write to GitHub.


## Cumulative atlas assignment
This cumulative snapshot uses centrally allocated, nonconflicting atlas slots. Any authoring-time numeric range in this historical family note is superseded by the canonical surface YAML and `Tools/three_d/generated/cloud-review/atlas-allocation-current.json`. Source IDs, original PNG pixels and modeled geometry are unchanged by atlas-index remapping.
