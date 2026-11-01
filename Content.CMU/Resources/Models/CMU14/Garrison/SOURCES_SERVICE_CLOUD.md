# Service-machinery source art: cloud draft batch

## Scope and status

Eleven editable solid assemblies cover twelve exact prototype IDs. This is an art-only, source-referenced batch. Every assembly remains `status: draft`; no fidelity approval, native admission, gameplay-state coverage, saved-map fit, collision clearance or engine change is claimed. The inventory has 34 Redux and eight classic occurrences of these IDs; those are source inventory counts, not tested placement results.

The authoring script is `Tools/three_d/author_service_cloud.py`. It writes only this family's canonical YAML, source-detail crops, direct GLBs, review images and family report. GLBs use the unchanged `Tools/three_d/build_models.py` exporter. No full shared export, map change, game launch or publishing was performed.

- Definitions: `Content.CMU/Resources/ThreeD/Prototypes/World/garrison_service_cloud.yml`
- Surface definitions: `garrison_service_cloud_art.yml`
- Original-detail crops: `Content.CMU/Resources/Textures/CMU14/ThreeD/service_cloud/`
- Atlas reservation used: 2616–2660, 45 surfaces
- Previews: `Tools/three_d/generated/review/service-cloud/`
- Source/resource/crop record: `Tools/three_d/generated/service-cloud-verification.json`
- Deterministic export, exact crop, finite buffer and accessor checks: `service-cloud-integrity.json`
- Independent Blender 4.3.2 import: `service-cloud-blender-import.json`
- Recheck: `python Tools/three_d/verify_service_cloud.py`

## Exact family mappings and source composition

| Assembly family | Exact prototype IDs | Inspected source | Implemented study and limitations |
|---|---|---|---|
| YouTool / SCT | CMVendorTool, ColMarTechSCTTools | `_RMC14/Structures/Machines/VendingMachines/tool.rsi` | `off` + `normal-unshaded` frame zero, one direction. Actual shell, recessed amber display, individual bars, six open vent ribs and pickup cavity. Nine-frame normal/eject, four-frame denial, broken and maintenance remain unsupported |
| MegaSeed | CMVendorSeeds | `seeds.rsi`, same vendor directory | Powered `off` + `normal-unshaded` reference, one direction. Eleven seed packets on four physical shelves, green header stripe, seven distinct selectors and hollow pickup tray. Saved prototype initializes off layers; this is not proof of a loaded powered pose |
| NutriMax | CMVendorNutri | `nutri.rsi`, same vendor directory | Powered `off` + `normal-unshaded` reference, one direction. Separate bottles, packets, shelving, horizontal selector row and pickup cavity. Power, denial/ejection, damage and maintenance remain unsupported |
| We-Yu-Chem | CMVendorChemistry | `chem.rsi`, same vendor directory | `off` + `normal-unshaded` frame zero, one direction. Header service slots, green left indicator, central recessed dispensing bay and nozzle, original narrow medical crosses, independent controls. Two-frame display and layered effects remain unsupported |
| Autolathe | CMAutolathe | `_RMC14/Structures/Machines/autolathe.rsi` | `autolathe` + `autolathe_u` frame zero, one direction. Formed hopper rim/pan, hollow chamfered mouth, red internal tooling head, guide bars, two red side panels and feet. Idle, running, power, feed/output and maintenance layers are not adapted |
| Chemical storage | RMCChemStorageDrinks, RMCChemStorageMedbay, RMCChemStorageOCP, RMCChemStorageResearch | `_RMC14/Structures/Machines/chemical_storage.rsi` | Four reciprocal directional assemblies reference the sole `chemstorage` state. South alone carries exact source mappings. Two-bay cabinet, physical red flow-motif tubes, paired green supply pipes, small meter/console crops, cooling ribs and plinth. No chemical-content behavior is invented |
| Medilink | CMMedilinkSupplyPort | `_RMC14/Structures/Machines/Medical/medilink.rsi` | Fixed one-direction `medlink_green_clamped` floor-level study. Green socket frame, recessed hazard hatch, paired gray clamp jaws, red pins and small cyan floor marks. The source explicitly marks dynamic state changes TODO; other resource states do not prove a live trigger |
| Crematorium | CMCrematorium | `_RMC14/Structures/Storage/morgue.rsi` | One-direction `crema_closed`, empty/nonburning study. Stepped shell, upper hatch/louvers, closed lower tray cover, handles and original small control indicators. EntityStorageVisuals hides the offset tray when closed and no closed tray state is configured. Open/tray animation, contents and burning remain unsupported |

The vendor source directories and images were fetched through the GitHub repository connector from `TheHellFireo/CMU-Garrison-3D`, branch `Chip/garrison-3d`, on 2026-10-06. PNG bytes were fetched as base64 and decoded without resampling. References include the actual engineering, squad engineer, vending-machine, medical, lathe, chemistry/soda dispenser and morgue prototype definitions. The existing EntityStorageVisualizerSystem source was inspected for closed-tray behavior.

### Chemical-storage directions and pivot

The 64×32 RSI has South/North/East/West slots. East differs from South by three RGB pixels in the small right status lamp; North and West are horizontal mirrors of East. All differences are enumerated in the integrity report. Four authored siblings retain the exact `referenceDirection` in RSI order and reciprocal `directionalModels`; `sourceCardinalFacings: [0, 0, 2, 2]` is in the separate South/East/North/West contract order. The two-sided cabinet's opposite face is physically present rather than being selected by camera orbit. Small original lamps distinguish the appropriate direction assemblies.

The original Sprite offset is `0.5,0`. It is documented as a screen offset, not automatically copied into a ground translation. The physical pivot is inferred at the left cabinet while the 64-pixel manifold projects right. That pivot, inferred depth and aliases require saved-map/native review before acceptance. Four prototype mappings occur only on the South/base assembly; state/direction siblings do not inflate exact prototype coverage.

### State boundary

None of these static drafts claims `spriteStates`, a power, contents, door, fold or storage-state adapter. Layered vendors, autolathe and crematorium do not satisfy the single-visible-layer contract. Chemical storage and medilink retain explicit source directions/reference states without asserting an audited live one-layer animation binding. No source resource-only state is treated as proof of a power or clamping controller. There are zero animation clips in all eleven GLBs.

### Physical construction and remaining art work

Cabinet horizontal scale uses 1/32 tile per original pixel. Vertical height is the existing vendor draft convention of .038 tile/pixel; physical depth, hidden surfaces and support dimensions are inferred. Medilink instead interprets its FloorObjects source as top-facing floor hardware with a shallow three-dimensional frame. Source tiny markings are preserved only on appropriate small physical panels. There are no full-sprite slabs or generic whole-machine texture boxes. Openings, trays, vents, manifolds, shelf stock, clamp jaws and handles are separate volumes.

Cabinet backs, material wear, rounded corners, monitor emission, actual transparent glass, moving mechanisms, inventory changes, exact hose bends, surface contacts and native visual acceptance remain unfinished. Material review lighting can darken original colors; no unshaded/emissive equivalence is claimed. Cyan medilink symbols are represented as thin source-owned floor marks and remain an inferred interpretation.

## Source attribution and license

All eight RSI metadata files specify **CC-BY-SA-3.0**. Original detail pixels retain their source colors and alpha; only exact rectangular crops are made. Geometry is a new source-guided adaptation. Source-derived art in this batch, including crops and embedded GLB image derivatives, is distributed under the same CC-BY-SA-3.0 license: <https://creativecommons.org/licenses/by-sa/3.0/>. Copyright and provenance remain with the original contributors; no new authorship of source pixels is claimed.

- Tool, seeds, nutri and chemistry vendor artwork: “Taken from cmss13” at <https://github.com/cmss13-devs/cmss13/blob/09a5191fb11aab8ddffe3f9be94292b53e4d96f6/icons/obj/structures/machinery/vending.dmi>
- Autolathe and crematorium artwork: “Taken from cmss13” at <https://github.com/cmss13-devs/cmss13/blob/c7b4d6bd868de669ad96f1d3e4dc3702a3404355/icons/obj/structures/props/stationobjs.dmi>
- Chemical storage artwork: “Taken from cmss13” at <https://github.com/cmss13-devs/cmss13/blob/6791699de749afc1c62bc65ec6f2326a00b3bb61/icons/obj/structures/machinery/science_machines_64x32.dmi>
- Medilink artwork: “Taken from cmss13” at <https://github.com/cmss13-devs/cmss13/blob/f3e535d5e24f44bd2c4412913144ebe4427a17de/icons/effects/warning_stripes.dmi>

The full unchanged metadata, original resource paths and crop hashes are included in `service-cloud-verification.json`. Source hashes and fetched prototype paths are recorded in `service-cloud-source-audit.json`.

## Validation actually run

- Eleven assemblies pass the unchanged local model validator, including positive bounds and the 128-part limit; current counts are 40–108 parts
- Twelve exact source IDs match the current inventory and original RSI resource references
- All 72 crop uses compare byte-for-byte with the inspected original frame/composition rectangle
- Forty-five unique surfaces remain inside the parent-reserved atlas interval
- Every GLB is byte-identical to direct re-export through the unchanged exporter
- Independent checks cover GLB chunk bounds, buffer-view/accessor bounds, finite numeric values, declared minima/maxima, index ranges and embedded image decoding
- All eleven files independently import in Blender 4.3.2
- Source-facing, orbit and reverse images are present for every model; comparative visuals were inspected, leading to corrected seed/nutrient packet positions, visible header recesses and strictly grayscale crematorium metal

**Khronos glTF validator was not run because its package is not installed.** These narrower structural/import checks do not establish full glTF conformance. No shared export, map fit, native appearance, gameplay interactions or source animation tests were run.


## Cumulative atlas assignment
This cumulative snapshot uses centrally allocated, nonconflicting atlas slots. Any authoring-time numeric range in this historical family note is superseded by the canonical surface YAML and `Tools/three_d/generated/cloud-review/atlas-allocation-current.json`. Source IDs, original PNG pixels and modeled geometry are unchanged by atlas-index remapping.
