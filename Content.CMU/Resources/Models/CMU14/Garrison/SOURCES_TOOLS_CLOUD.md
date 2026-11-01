# Garrison loose-tool cloud art batch

Five draft assemblies, authored from actual pixels and definitions on `TheHellFireo/CMU-Garrison-3D`, branch `Chip/garrison-3d`. No game, source system, runtime renderer, source prototype, source RSI, map placement or repository publishing action is part of this art batch. Fetched prototype/RSI files are read-only reference inputs. Run `python Tools/three_d/author_tools_cloud.py` to reproduce the editable YAML, six exact source-crop textures, five GLBs and previews through the unchanged exporter.

## Exact model mapping and scope

- `CMU3DStunBatonCloud` → `CMStunbaton`: 22 default parts; 10 Redux / 2 classic records. Reference `_RMC14/Objects/Weapons/Melee/stun_baton.rsi`, `stunbaton_off`, one direction. Separate cylindrical grip, collars, central shaft, guard and open electrode cage. `stunbaton_on` has three complete 22/34/50-part frames with original 1.5/0.1/0.1-second intervals; spark colors and alpha come directly from original pixels. No-cell, in-hand and equipped appearances remain unsupported. Source ItemToggle/GenericVisualizer owns state; no new clock or gameplay controller is introduced. Native activation and glow appearance remain unverified.
- `CMU3DTaserCloud` → `RMCWeaponTaser`: 27 parts; 8 Redux / 1 classic records. Reference `_RMC14/Objects/Weapons/Guns/Energy/taser.rsi`, `taser`, one direction. Separate receiver, forward power block, paired electrode tips, thick grip and physically open trigger frame. Original receiver machining marks are one unresampled ten-by-three-pixel crop. This is deliberately a base-layer art study: the source also uses an unshaded MagazineVisuals layer with five charge states and a two-frame low-charge blink. The current single-layer contract does not admit it, so no complete layered/native appearance, charge state, held pose or firing coverage is claimed.
- `CMU3DEmptyLightReplacerCloud` → `RMCSprayBottleSpaceCleaner`: 26 parts; 8 Redux / 0 classic records. Reference `_RMC14/Objects/Misc/Janitorial/light_replacer.rsi`, `lightreplacer0`, one direction. Despite the legacy ID, the actual definition inherits `RMCLightReplacer` and is named light replacer. It has zero starting tubes/bulbs. This model preserves the actual source: orange cartridge, tall dark guide, service plate, actuator and open handle. No invented spray-bottle model replaces it. The source's unused `lightreplacer1` resource and in-hand artwork remain unverified.
- `CMU3DCleanerGrenadeCloud` → `CleanerGrenade`: 21 parts; 9 Redux / 1 classic records. Reference `Objects/Weapons/Grenades/janitor.rsi`, `icon`, one direction. Separate cylindrical cleaner reservoir, narrowed neck, metal cap, lever and timer bead. Five one-by-four-pixel exact crops follow the reservoir curvature as narrow tangent labels. Two `primed` frames preserve the original 0.1/0.1-second timing and exact #CCCCC9 / #EE6434 timer colors; inactive color is #D79835. The model reacts only to an actual source RSI state. Important source uncertainty: `GrenadeBase` maps `enum.TriggerVisualLayers.Base`, while `TimerGrenadeBase`'s GenericVisualizer targets `enum.ConstructionVisuals.Layer`. TimerTriggerVisuals is also inherited, but its live system was not run. Therefore the runtime transition to `primed` is not established by these exports. Deployed foam, explosion, held and worn visuals are out of scope.
- `CMU3DWhiteTowelCloud` → `TowelColorWhite`: 16 parts; 8 Redux / 2 classic records. Reference `Clothing/Multiple/towel.rsi`, `icon`, one direction. Thick folded sheets, separate rounded returns, open fold shadow and raised hems form actual geometry. The original #EAE8E8 layer tint is multiplied into the sampled grayscale palette and the comparison reference. It is not falsely declared as a white RSI layer, and has no `spriteStates` claim. Four-direction equipped/in-hand resources, wet appearance, absorption, body attachment and cloth simulation remain unsupported.

This is 5 exact draft IDs / 43 recorded Redux placements / 49 across the supplied configured-map inventory. These are source-frequency counts, not completed live rendering or verified placement coverage. The source record and retained entity position/yaw are not rewritten. All models use the existing surface-placement convention. Hand tools are modeled resting on their sides; the replacer and cleanade are upright inferred poses. Saved-map support fitting, wall/neighbor contacts, orientation and native acceptance require review in the full environment.

## Geometry and derived textures

The five default poses have 112 named editable solid parts. The largest supported animation frame has 50 parts, below the unchanged 128-part exporter limit. Every part has finite strictly positive bounds. There is no whole-object source-sprite slab. Only the receiver marks and five tiny label strips use source textures; electrical effects use separate original-alpha pixels over genuine shaft geometry.

Surface IDs in `garrison_tools_cloud_art.yml` occupy 3900–3905, reserved for this batch. Source crop coordinates are in `Tools/three_d/generated/cloud-review/tools-surface-crops.json`; every generated pixel equals the corresponding source RGBA pixel. Towel and curved bodies are source-colored geometry. The backside, thickness, round profiles, physical construction, exact support height and soft-cloth detail are inferred and remain draft decisions.

## Verification and evidence

- `garrison_tools_cloud.yml`: five exact model mappings accepted by unchanged `build_models.load_models`; no YAML aliases required
- `Tools/three_d/generated/cloud-review/*-source-and-orbit.png`: five visually inspected original/front/oblique/reverse comparisons, independently enlarged for inspection
- `tools-world-state-comparison.png`: all authored baton and cleanade world-state frames beside the actual RSI frames
- `tools-proof.json`: deterministic GLB hashes, default counts, source references, state frame counts and explicit unverified live/map flags
- `tools-asset-validation.json`: glTF headers, finite accessor ranges, scenes, clips and physical bounds
- `tools-blender-import-validation.json`: independent Blender 4.3.2 import of all five GLBs; no nonfinite vertices or repaired invalid meshes
- Source-frame dimensions, direction count and delays validated against original RSI metadata; viewer-reference extraction verified all authored frame sheets
- Original emitter alpha is preserved as fractional geometry paint, but the current 3D rasterizer uses screen-door coverage and no source-equivalent unshaded lighting
- Khronos glTF Validator is not installed in this cloud subset and was not run; native engine, live game, full-map fit and complete-state fidelity were not tested

All five assets remain `draft`. Passing asset/import checks is not fidelity acceptance or engine acceptance.

## Attribution

All five source RSI packages declare CC-BY-SA-3.0. These source-guided derivative models and exact artwork crops retain that attribution and are distributed under CC-BY-SA-3.0. Preserve this document, the original RSI metadata and referenced source artwork with exported GLBs. Original metadata follows verbatim by resource.

### _RMC14/Objects/Weapons/Melee/stun_baton.rsi

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/106c92cdf232ebc12c9d7a2feb23956c6755496f/icons/obj/items/weapons/weapons.dmi, https://github.com/cmss13-devs/cmss13/blob/bec6653d487a49aa2b5a8e0c97bed9612f620211/icons/mob/humans/onmob/items_lefthand_0.dmi, https://github.com/cmss13-devs/cmss13/blob/bec6653d487a49aa2b5a8e0c97bed9612f620211/icons/mob/humans/onmob/items_righthand_0.dmi, https://github.com/cmss13-devs/cmss13/blob/81c7806eb705f3a6b43085056cef1be0055d8ed2/icons/mob/humans/onmob/belt.dmi, https://github.com/cmss13-devs/cmss13/blob/81c7806eb705f3a6b43085056cef1be0055d8ed2/icons/mob/humans/onmob/suit_storage.dmi

Source metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Weapons/Melee/stun_baton.rsi/meta.json

### _RMC14/Objects/Weapons/Guns/Energy/taser.rsi

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/efc4e0aa3bda7ba8c48b08a9e1980831620a77b3/icons/obj/items/weapons/guns/guns_by_faction/uscm.dmi, https://github.com/cmss13-devs/cmss13/blob/55861402d03d1186ee2c0016be98c31ffa6015d8/icons/mob/humans/onmob/items_lefthand_1.dmi, https://github.com/cmss13-devs/cmss13/blob/55861402d03d1186ee2c0016be98c31ffa6015d8/icons/mob/humans/onmob/items_righthand_1.dmi

Source metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Weapons/Guns/Energy/taser.rsi/meta.json

### _RMC14/Objects/Misc/Janitorial/light_replacer.rsi

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/106c92cdf232ebc12c9d7a2feb23956c6755496f/icons/obj/janitor.dmi, https://github.com/cmss13-devs/cmss13/blob/bec6653d487a49aa2b5a8e0c97bed9612f620211/icons/mob/humans/onmob/items_righthand_0.dmi, https://github.com/cmss13-devs/cmss13/blob/bec6653d487a49aa2b5a8e0c97bed9612f620211/icons/mob/humans/onmob/items_lefthand_0.dmi

Source metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Misc/Janitorial/light_replacer.rsi/meta.json

### Objects/Weapons/Grenades/janitor.rsi

License: CC-BY-SA-3.0

Taken from tgstation at https://github.com/tgstation/tgstation/commit/b13d244d761a07e200a9a41730bd446e776020d5. Inhands by TiniestShark (github)

Source metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/Objects/Weapons/Grenades/janitor.rsi/meta.json

### Clothing/Multiple/towel.rsi

License: CC-BY-SA-3.0

Taken from Baystation12 at commit https://github.com/Baystation12/Baystation12/commit/c5dc6953e6e1fde87c2ded60038144f1d21fbd48

Source metadata: https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/Clothing/Multiple/towel.rsi/meta.json


## Cumulative atlas assignment
This cumulative snapshot uses centrally allocated, nonconflicting atlas slots. Any authoring-time numeric range in this historical family note is superseded by the canonical surface YAML and `Tools/three_d/generated/cloud-review/atlas-allocation-current.json`. Source IDs, original PNG pixels and modeled geometry are unchanged by atlas-index remapping.
