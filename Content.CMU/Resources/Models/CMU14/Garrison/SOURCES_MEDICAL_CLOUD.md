# Medical equipment cloud art batch

All 21 assemblies remain **draft**. This art-only batch authors exact mappings for six original prototype IDs representing 41 saved Redux placements in the existing inventory, plus four classic hypersleep placements. This is inventory coverage, not a map-render, gameplay, contact, or fidelity approval.

## Authored scope

- `CMUAutodocConsole`: four RSI-direction partners, `sleeperconsole` (two 0.2-second frames) and source icon state `sleeperconsole-p` (one static scene)
- `CMUAutodocPod`: four direction partners, `autodoc_open`, `autodoc_closed`, and all five 0.2-second `autodoc_closed_operate` frames
- `CMUBodyScannerConsole`: four direction partners, `body_scannerconsole` (four 0.2-second frames) and source icon state `body_scannerconsole-p` (one static scene)
- `CMUBodyScannerPod`: four direction partners, `body_scanner_open` and all seven 0.2-second `body_scanner_closed` frames
- `CMHyperSleepChamber`: four direction partners, `open` and all seven 0.2-second `closed` frames
- `CMULimbPrinter`: one non-directional assembly, `bioprinter` and all eight 0.1-second `bioprinter_working` frames

The total is 133 source-frame compositions, 25 static state scenes and 21 looping portable GLB clips. It does not imply 21 new gameplay animations. The existing source Sprite owns visibility, states and timing. The two `-p` console states are source resources; no power controller or in-game transition to them is invented. Exact prototype mappings are on the South/default family member only. North/East/West partners have empty `sourcePrototypes` and reciprocal `directionalModels` links, in RSI S/N/E/W order.

## Source evidence and attribution

Fetched through the connected repository from branch `Chip/garrison-3d` of [TheHellFireo/CMU-Garrison-3D](https://github.com/TheHellFireo/CMU-Garrison-3D). Prototype definitions and original RSI PNGs/meta are retained locally at their source paths.

- [Medical machine definitions](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Content.CMU/Resources/Prototypes/CMU14/Medical/Equipment/Machines/medical_machines.yml), Git blob `e4129e26b1b7017c550d2d81698a3cdf17ed00d7`
- [Hypersleep definitions](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Prototypes/_RMC14/Entities/Structures/Machines/hypersleep.yml), Git blob `65ace8b2c761ec09b671324773a1bec1fc961b76`
- Autodoc RSI: `Content.CMU/Resources/Textures/CMU14/Structures/Machines/Medical/autodoc.rsi`, metadata blob `4363bc375c91ecfabcdb1c3a2e18093cc1b00776`
- Body-scanner RSI: `Content.CMU/Resources/Textures/CMU14/Structures/Machines/Medical/bodyscanner.rsi`, metadata blob `a1dc77b7c5e4011b4a7ba71b8b63ff0d9cfc0774`
- Limb-printer RSI: `Content.CMU/Resources/Textures/CMU14/Structures/Machines/Medical/limbprinter.rsi`, metadata blob `f325d58642ab409acd35ab13159fa9bedd324e61`
- Hypersleep RSI: `Resources/Textures/_RMC14/Structures/Machines/hypersleep.rsi`, metadata blob `535e1b048008aa40f4daeb01f643316e15c906ed`

All four RSI resources declare **CC-BY-SA-3.0**. Their original copyright statements:

Autodoc and body-scanner: “Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/obj/structures/machinery/cryogenics.dmi”

Limb-printer: “Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/41b3798435ee57263bb67fa694ea944d6698c062/icons/obj/structures/machinery/surgery.dmi”

Hypersleep: “Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/09a5191fb11aab8ddffe3f9be94292b53e4d96f6/icons/obj/structures/machinery/cryogenics.dmi”

[CC-BY-SA-3.0 license](https://creativecommons.org/licenses/by-sa/3.0/). Derived texture crops and source-derived model artwork retain this license and attribution. Keep these notes and the original RSI metadata alongside redistribution. No generated imagery or unrelated textures are used.

## Construction and source fidelity

The assets have solid volume: stepped dual cabinets and a genuinely recessed chamber for the printer; pedestal, tray and sloping monitor case for each console; broad recessed chassis, feet, mattresses, endplates, rails and distinct lid constructions for the pods. They are not whole-sprite billboards. Small planar surfaces preserve original controls, display patterns, medical insignia context and cover highlights on corresponding solid parts.

Open pod covers use source-cut glass columns with positive depth, solid backing frames and explicit hinge supports connected to the rear rail. This replaced an earlier rejected draft whose cover sections floated above the rail. The scanner lid's diagonal contour is pixel-stepped to the inspected source. Source-purple masks retain source color values but exclude other components; the full model has separately authored metal frames and patient surfaces. Closed pods use opaque violet/blue curved cover volumes with separate bands and source-pixel front highlights; the geometry does not claim physically correct transparent glass.

The source artwork has aliased and mirrored directional frames. Each of the four original slots is retained. In particular, the autodoc open resource places the cross-bearing end on the opposite side for East compared with its closed resource. These source-owned differences remain separate poses rather than being normalized away. There is no camera-dependent direction selection.

64 deduplicated original-pixel detail surfaces use atlas indices **3600–3663**, reserved after comparison against the supplied existing registry. Images are under `Content.CMU/Resources/Textures/CMU14/ThreeD/medical_cloud/`. The crop manifest records source family, state, frame, direction, crop rectangle, masking/canonical mirroring where used, dimensions and SHA-256 for each output. No source images are resampled into the authored textures.

## Verification and review

- Canonical YAML: `Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_medical_cloud.yml`
- Surface YAML: `Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_medical_cloud_art.yml`
- Reproducible art authoring helper: `Tools/three_d/authoring/author_medical_cloud.py`
- Source comparisons, orbit renders, full state overview, crop manifest and verification: `Tools/three_d/generated/review/medical-cloud/`
- Individual portable GLBs: this model directory

All 21 models pass the existing Python model/source-state validator: positive volumes, supported shapes/surfaces, reciprocal directional links, reference frame-zero equality, exact RSI source timing and frame counts. Every frame is below the current 128-part limit; the largest frame has 87 parts. GLBs were exported through the unchanged `build_models.glb_bytes` path, retaining source metadata and static/animated scenes.

A conservative connectivity audit finds one connected solid-parts AABB component in all 133 frame compositions at 0.008-tile tolerance, excluding decorative texture patches. This rejects grossly floating covers; it is not an exact mesh-contact or physics proof. All 21 exported GLBs also pass local GLB-v2 structure, embedded-texture and deterministic-byte checks. Khronos validation remains part of the parent’s aggregate batch.

The source-facing comparisons use the actual labeled RSI direction. Pods use a slightly elevated south-facing camera to show their patient bay; consoles/printer use a straight source-facing elevation. Orbit views verify depth. They are visual studies, not numerical silhouette-fit claims. The all-state sheet inspects frame zero of every direction/state; full animated frame checks verify resource/frame/timing and positive geometry, not every native runtime interaction.

## Explicit limitations

- Back surfaces, mechanical internals, mattress depth, canopy curvature and hinge construction are inferred. Absolute physical height and projection from 2D source art are draft decisions
- Pod patient containment, operating interactions, occupants, transparent glass, emissive illumination, sounds, damage and live animation transitions are unverified
- Source `GenericVisualizer` and multi-layer visibility are untouched. The existing `spriteStates` adapter can only consume one supported visible layer; unsupported appearance/overlay combinations must retain the source fallback. Saved Appearance data is not guessed
- The console source is rotating and unsnapped. Its existing `sourceSpriteRotates: true` contract follows saved yaw; live behavior remains unverified
- No map records or engine/runtime code are changed. No source transform is rewritten. This bounded source subset has not run a full assembled-map contact pass, native renderer budget pass, game launch or shared full export
- Draft source type coverage is not runtime spawn closure, user fidelity approval, or proof that all 41 saved Redux instances render in a dense native scene


## Cumulative atlas assignment
This cumulative snapshot uses centrally allocated, nonconflicting atlas slots. Any authoring-time numeric range in this historical family note is superseded by the canonical surface YAML and `Tools/three_d/generated/cloud-review/atlas-allocation-current.json`. Source IDs, original PNG pixels and modeled geometry are unchanged by atlas-index remapping.
