# Source-inspected field gear: cloud exterior-art batch

All 23 assemblies remain draft. This batch is editable ground-world exterior game art only. It changes no engine/runtime implementation, game behavior, saved maps, source gameplay definitions or publishing state. Source YAML and controller files in the cloud workspace are read-only reference copies. No weapon functional internals or real construction plans are authored.

## Deliverable

23 unchanged-exporter GLBs cover 23 unique exact source IDs. There are 24 distinct physical poses: the default for each prop plus the extended telebaton. The exporter writes 25 static glTF scenes because the default telebaton scene is also retained separately from its two named states. No animation clips are added. Maximum editable parts per pose: 48. All are supported Box, Cylinder, Ellipsoid and rotated source-part primitives.

Canonical authored files: `Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_field_gear_cloud.yml` and `garrison_field_gear_cloud_art.yml`. GLBs are under `Content.CMU/Resources/Models/CMU14/Garrison/CMU3DField*.glb`. The two exact unresampled detail crops are the full yellow FoamBox count strip and small red smart-scope bracket marking, under `Textures/CMU14/ThreeD/field_gear_cloud`. They use the first two indices from the centrally reserved field_gear list, 2583 and 2584; the script reads the actual gap-containing list and never invents a range. No whole-sprite slab or billboard is used.

## Source checkpoint and resolved states

Every source is pinned to repository `TheHellFireo/CMU-Garrison-3D`, commit `6e37a4d0a7d9433838c393a82c02422cb704dd5a`. The source-receipts JSON records exact paths, Git blob SHA-1 values, local reference paths and verified commit links. Both direct prototypes and inherited definitions were read; the closure contains 52 entity prototypes. Actual source PNG pixels and RSI metadata were inspected, including source alpha around openings.

- Entrenching tool: ItemToggle.Activated defaults false. The actual EntrenchingToolVisualsSystem hides Base and Dirt and shows Folded while inactive. The mapped default is etool_c alone. Its dark center is opaque in the source, so a nested blade pan supports the raised D-frame. The unfolded etool and dirt overlay are deliberately unsupported; this is not a Foldable owner and no false fold adapter is declared.
- Telebaton: AU14TeleBaton inherits the damaged-baton base but replaces its world layer. ItemToggle defaults inactive, and GenericVisualizer selects telebaton_off/on on the one actual visible layer. Compact/off is the mapped default; both source states use the existing single-layer static adapter. The long metal shaft appears only in the on state. No extension transition, held state or use effect is claimed.
- Magazines: inherited BallisticAmmoProvider with a non-null proto fills the visual count to capacity on MapInit. GunSystem.MagazineVisuals rounds full count to steps-1. M60/B92FS/PK7 therefore use base + mag-1, FoamBox uses base + mag-7, and the 357 speedloader uses base + base-5. The raw FoamBox YAML mag-1 is an initial placeholder, not the full-count default. PK7 also keeps its ammo_band layer with source #DF963F tint. These multi-layer owners have no single-layer adapter in this batch; empty/partial/count/feeding/reload states remain unsupported. The speedloader shows the five front-visible forms in its projected full-count art without inventing hidden mechanical contents.
- Grenades: GrenadeBase supplies a single TriggerVisualLayers.Base icon. The two default icons are slim plain cylinders with red/white caps. They contain no visible pull ring, pin or lever, so none was invented. Arming, timer, throw and detonation/effect appearances remain unsupported.
- Loose attachments: source iffbarrel, bipod and smgstock only. Bipod has an actual clear center between legs. M63 stock has an opaque ribbed panel, so it is not incorrectly hollowed. The smart-scope optical rim is physical geometry with a recessed colored lens. Installed alignments, aim/activation and deployed states are not covered.

## Source-specific geometry

- M11, M5, kukri and utility B remain separate designs: olive versus gray versus brown grips, distinct straight/recurved/clipped blades, different guards and source edge colors. The M11 pommel loop is open geometry.
- Ceremonial saber has a blue grip, gold swept open knuckle guard and long curved silver blade. Blade roots and guard connections were refined after convex-part contact checks.
- Garden scythe has the source yellow open lower handle, green bent shaft and short side grip, and swept metal blade. Red ice axe has a curved red shaft, pale midshaft collar and separate small silver spike.
- Wood and metal baseball bats retain separate source palettes, narrow wrapped handles, end knobs and broadened rounded barrels. The synthetic hammer has its long dark shaft, upper orange wrap, broad forged head and bands.
- Cases and magazines use actual separate shells, shoes, collars, front flutes, count artwork and visible source-loaded exterior details. HE mortar is an exterior red capsule, silver band, narrow tail and separate fins; it contains no operational internal construction.

## Review and bounded checks

`Tools/three_d/generated/cloud-review/field-gear/field-gear-overview.png` gathers 23 comparisons. Each `*-source-and-orbit.png` includes the resolved original default composition, a source-facing physical study and two orbit views. Panels are independently normalized for legibility, not an exact-scale silhouette score. `field-gear-telebaton-states.png` shows the actual short and extended source states with their separate geometry. The corrected default source contact sheet is `reference/field-gear-resolved-source-sheet.png`; pixel grids preserve raw individual layers for inspection.

`field-gear-proof.json` verifies 95 unchanged source blobs, all 23 physicalModelingFamilies targets and their Redux inventory presence, the 52-entity inheritance closure, finite actual accessor bounds, deterministic GLB bytes, exact-source palette membership, two byte-identical small crops, unique canonical source IDs and atlas slots, 24 connected convex-mesh pose graphs, and ten aperture/solid probes. Palette membership is not a perceptual-fidelity score.

The connected-part check uses convex exported meshes and linear halfspace intersection feasibility at 1e-6 model-unit tolerance, including intended contact/overlap. Initial small blade-root and floating-highlight gaps were corrected, without filling intended apertures. These are local model construction checks, not physics or saved-map neighbor collision tests. Aperture probes cover both baton loop states, M11 loop, saber guard, scythe handle, bipod legs and scope rim. Solid probes prevent false holes in the folded etool pan, M63 stock panel and recessed scope lens.

The available `build_models.py` was not modified. It matches the pinned executable text exactly after removing trailing whitespace; the local file has one pre-existing extra final blank line. `exporter` in the proof records both blob hashes and that bounded comparison. Separate isolated exporter output, manifest and viewer assets are under `field-gear/export/` and `field-gear/viewer/`, with the deterministic check log alongside. `field-gear-blender-import.json` records independent imports of the final bytes into Blender, including finite meshes and no repair requirement. This is not Khronos validation.

The inventory lists 35 Redux and 24 classic saved instances across these IDs. These are potential source inventory counts only, not admitted native objects, reviewed visible map placements or collision-clear placements.

## Remaining limits

All assets remain draft. Physical depth, unseen reverse surfaces, exact scale, finish, broader source-facing fit and support placement are inferred and require art review. No native rendering, live-state interaction, saved-map fit, full-source-state completion or fidelity approval is claimed. Held, firing, reloading, rigged, installed, arming and use effects are unsupported. No engine, runtime, map, live game, push or publishing step occurred.

## Exact model manifest

### CMU3DFieldAU14TeleBatonCloud
- Exact source: `AU14TeleBaton`; reference state `telebaton_off`; 13 default parts
- Inherited ItemToggle defaults inactive. GenericVisualizer selects the existing single world layer telebaton_off/on; both static source states are authored without a transition clip. Ground-world exterior art only. Physical depth, reverse surfaces and material finish are inferred. No held, firing, reload, rigging, arming or use effects; no native/map/live/fidelity approval.

### CMU3DFieldCMM11KnifeCloud
- Exact source: `CMM11Knife`; reference state `icon`; 25 default parts
- Static loose-world icon; distinct olive stacked grip and warm silver blade follow the source diagonal. Grip loop aperture is geometry. Ground-world exterior art only. Physical depth, reverse surfaces and material finish are inferred. No held, firing, reload, rigging, arming or use effects; no native/map/live/fidelity approval.

### CMU3DFieldKukriKnifeCloud
- Exact source: `KukriKnife`; reference state `icon`; 26 default parts
- Source-specific broad recurved silhouette, separate edge and backbone, short green grip. Hidden section thickness is inferred. Ground-world exterior art only. Physical depth, reverse surfaces and material finish are inferred. No held, firing, reload, rigging, arming or use effects; no native/map/live/fidelity approval.

### CMU3DFieldRMCCeremonialSwordCloud
- Exact source: `RMCCeremonialSword`; reference state `icon`; 48 default parts
- Source blue grip, continuous curved silver blade and gold swept knuckle bow. The guard opening is not filled by backing geometry. Ground-world exterior art only. Physical depth, reverse surfaces and material finish are inferred. No held, firing, reload, rigging, arming or use effects; no native/map/live/fidelity approval.

### CMU3DFieldRMCCombatUtilityKnifeBCloud
- Exact source: `RMCCombatUtilityKnifeB`; reference state `icon`; 21 default parts
- Actual B variant has a clipped/hooked silver tip and gold curved guard. Its blade and grip proportions are distinct from M11 and M5. Ground-world exterior art only. Physical depth, reverse surfaces and material finish are inferred. No held, firing, reload, rigging, arming or use effects; no native/map/live/fidelity approval.

### CMU3DFieldRMCM5BayonetCloud
- Exact source: `RMCM5Bayonet`; reference state `icon`; 13 default parts
- Source-striped grip and narrow straight blade with a small clipped point. Attachment/held pose is not claimed. Ground-world exterior art only. Physical depth, reverse surfaces and material finish are inferred. No held, firing, reload, rigging, arming or use effects; no native/map/live/fidelity approval.

### CMU3DFieldCMEntrenchingToolCloud
- Exact source: `CMEntrenchingTool`; reference state `etool_c`; 17 default parts
- Default inactive ItemToggle selects only etool_c in EntrenchingToolVisualsSystem. A nested compact spade supports the raised handle; the source center is opaque, not a through-hole. Unfolded and dirt layers are unsupported because this is a multi-layer owner, not Foldable. Ground-world exterior art only. Physical depth, reverse surfaces and material finish are inferred. No held, firing, reload, rigging, arming or use effects; no native/map/live/fidelity approval.

### CMU3DFieldHydroponicsToolScytheCloud
- Exact source: `HydroponicsToolScythe`; reference state `icon`; 35 default parts
- Source yellow open lower hand loop and short green side grip, separate long shaft and silver curved blade. Cutting/held motion unsupported. Ground-world exterior art only. Physical depth, reverse surfaces and material finish are inferred. No held, firing, reload, rigging, arming or use effects; no native/map/live/fidelity approval.

### CMU3DFieldRMCBaseballBatCloud
- Exact source: `RMCBaseballBat`; reference state `icon`; 12 default parts
- Distinct warm wood source palette. Continuous rounded barrel widens from narrow brown handle with a separate knob. Swing/held effects unsupported. Ground-world exterior art only. Physical depth, reverse surfaces and material finish are inferred. No held, firing, reload, rigging, arming or use effects; no native/map/live/fidelity approval.

### CMU3DFieldRMCBaseballBatMetalCloud
- Exact source: `RMCBaseballBatMetal`; reference state `icon`; 10 default parts
- Distinct silver metal source palette. Continuous rounded barrel widens from narrow brown handle with a separate knob. Swing/held effects unsupported. Ground-world exterior art only. Physical depth, reverse surfaces and material finish are inferred. No held, firing, reload, rigging, arming or use effects; no native/map/live/fidelity approval.

### CMU3DFieldRMCIceAxeRedCloud
- Exact source: `RMCIceAxeRed`; reference state `icon_red`; 24 default parts
- Specific red world variant. Source curved handle, pale midshaft collar, separate small end spike and arched metal head remain distinct. Ground-world exterior art only. Physical depth, reverse surfaces and material finish are inferred. No held, firing, reload, rigging, arming or use effects; no native/map/live/fidelity approval.

### CMU3DFieldRMCSynthBreachingHammerCloud
- Exact source: `RMCSynthBreachingHammer`; reference state `icon`; 14 default parts
- Long dark shaft, bright orange upper wrap and wide gray head trace the actual source. This is a static prop, without impact mechanics. Ground-world exterior art only. Physical depth, reverse surfaces and material finish are inferred. No held, firing, reload, rigging, arming or use effects; no native/map/live/fidelity approval.

### CMU3DFieldRMCMagazinePistolB92FSCloud
- Exact source: `RMCMagazinePistolB92FS`; reference state `base`; 9 default parts
- Resolved default base + mag-1 composition from 2-step MagazineVisuals. The visible gold top and shell are exterior art. Empty/count/reload states use multiple source layers and remain unsupported. Ground-world exterior art only. Physical depth, reverse surfaces and material finish are inferred. No held, firing, reload, rigging, arming or use effects; no native/map/live/fidelity approval.

### CMU3DFieldRMCMagazinePistolPK7Cloud
- Exact source: `RMCMagazinePistolPK7`; reference state `base`; 8 default parts
- Default base + mag-1 + ammo_band; the band retains the source #DF963F tint. Only loaded world exterior is authored. Empty/count variations and reloading unsupported. Ground-world exterior art only. Physical depth, reverse surfaces and material finish are inferred. No held, firing, reload, rigging, arming or use effects; no native/map/live/fidelity approval.

### CMU3DFieldRMCMagazineLMGM60Cloud
- Exact source: `RMCMagazineLMGM60`; reference state `base`; 39 default parts
- Default base + mag-1 for inherited 2-step MagazineVisuals, full provider. Source belt remains exterior capsule detail. Empty/count/feeding/reload behavior and internal contents are not represented. Ground-world exterior art only. Physical depth, reverse surfaces and material finish are inferred. No held, firing, reload, rigging, arming or use effects; no native/map/live/fidelity approval.

### CMU3DFieldMagazineFoamBoxCloud
- Exact source: `MagazineFoamBox`; reference state `base`; 8 default parts
- Full provider and 8-step MagazineVisuals resolve base + mag-7, correcting the initial YAML mag-1 placeholder. Narrow original count strip is a detail crop on a modeled green case. Partial/empty count and reload states unsupported. Ground-world exterior art only. Physical depth, reverse surfaces and material finish are inferred. No held, firing, reload, rigging, arming or use effects; no native/map/live/fidelity approval.

### CMU3DFieldRMCSpeedLoader357Cloud
- Exact source: `RMCSpeedLoader357`; reference state `base`; 20 default parts
- Source base + base-5 is the full 6-step visual. Five front-visible capsule forms reproduce the projected art; no unseen internal or rear mechanical arrangement is invented. Count/empty/reload states remain unsupported. Ground-world exterior art only. Physical depth, reverse surfaces and material finish are inferred. No held, firing, reload, rigging, arming or use effects; no native/map/live/fidelity approval.

### CMU3DFieldCMGrenadeHighExplosiveCloud
- Exact source: `CMGrenadeHighExplosive`; reference state `icon`; 5 default parts
- Inherited GrenadeBase has one visible TriggerVisualLayers.Base icon. Default unarmed source is a plain slim cylinder with red cap. No pin, pull ring or lever is present in this art. Timer/armed/detonation effects unsupported. Ground-world exterior art only. Physical depth, reverse surfaces and material finish are inferred. No held, firing, reload, rigging, arming or use effects; no native/map/live/fidelity approval.

### CMU3DFieldRMCGrenadeTrainingCloud
- Exact source: `RMCGrenadeTraining`; reference state `icon`; 5 default parts
- Inherited GrenadeBase has one visible TriggerVisualLayers.Base icon. Default unarmed source is a plain slim cylinder with white cap. No pin, pull ring or lever is present in this art. Timer/armed/detonation effects unsupported. Ground-world exterior art only. Physical depth, reverse surfaces and material finish are inferred. No held, firing, reload, rigging, arming or use effects; no native/map/live/fidelity approval.

### CMU3DFieldRMCMortarShellHECloud
- Exact source: `RMCMortarShellHE`; reference state `mortar_ammo_he`; 13 default parts
- Red exterior capsule and separate source-visible gray tail/fin volumes. Only the loose-world visual is authored; no functional internals, dimensions or firing behavior. Ground-world exterior art only. Physical depth, reverse surfaces and material finish are inferred. No held, firing, reload, rigging, arming or use effects; no native/map/live/fidelity approval.

### CMU3DFieldRMCAttachmentBipodCloud
- Exact source: `RMCAttachmentBipod`; reference state `bipod`; 13 default parts
- Source folded U form with two distinct legs, dark feet and a clear central aperture. Deployed/installed/held forms remain unsupported; no mechanics are modeled. Ground-world exterior art only. Physical depth, reverse surfaces and material finish are inferred. No held, firing, reload, rigging, arming or use effects; no native/map/live/fidelity approval.

### CMU3DFieldRMCAttachmentM63StockCloud
- Exact source: `RMCAttachmentM63Stock`; reference state `smgstock`; 12 default parts
- Source alpha confirms a fully filled ribbed body, rather than a skeletal opening. Stepped exterior follows the sloped source underside. Installed/retracted or attachment transforms remain unsupported. Ground-world exterior art only. Physical depth, reverse surfaces and material finish are inferred. No held, firing, reload, rigging, arming or use effects; no native/map/live/fidelity approval.

### CMU3DFieldRMCAttachmentB8SmartScopeCloud
- Exact source: `RMCAttachmentB8SmartScope`; reference state `iffbarrel`; 42 default parts
- Source charcoal optical body, red identification bracket and separate lens rim. Hollow rim stops at recessed colored glass; inferred optical depth remains purely visual. Installed aiming/activation/held states unsupported. Ground-world exterior art only. Physical depth, reverse surfaces and material finish are inferred. No held, firing, reload, rigging, arming or use effects; no native/map/live/fidelity approval.

## Original RSI attribution

Keep these source licenses and copyright strings with the derived art and exports.

### Resources/Textures/_RMC14/Objects/Weapons/Melee/telebaton.rsi/meta.json
[Pinned source metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Weapons/Melee/telebaton.rsi/meta.json)

License: CC-BY-SA-3.0

Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/a7dd814ed7c7773a3b910f03ae597c885635a32a/icons/obj/items/weapons/melee/non_lethal.dmi, https://github.com/cmss13-devs/cmss13/blob/a7dd814ed7c7773a3b910f03ae597c885635a32a/icons/mob/humans/onmob/inhands/weapons/melee/non_lethal_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/a7dd814ed7c7773a3b910f03ae597c885635a32a/icons/mob/humans/onmob/inhands/weapons/melee/non_lethal_lefthand.dmi

### Resources/Textures/_RMC14/Objects/Tools/etool.rsi/meta.json
[Pinned source metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Tools/etool.rsi/meta.json)

License: CC-BY-SA-3.0

Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/32cb5892413243cc74bb2d11df8e3085f8ef1164/icons/obj/items/marine-items.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/equipment/tools_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/equipment/tools_righthand.dmi

### Resources/Textures/_RMC14/Objects/Weapons/Grenades/m40hedp.rsi/meta.json
[Pinned source metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Weapons/Grenades/m40hedp.rsi/meta.json)

License: CC-BY-SA-3.0

Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/a1079f4912a96473dae51cceaa1d74eafb3939e2/icons/obj/items/weapons/grenade.dmi, https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/mob/humans/onmob/inhands/weapons/grenades_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/mob/humans/onmob/inhands/weapons/grenades_lefthand.dmi

### Resources/Textures/_RMC14/Objects/Weapons/Melee/m11_knife.rsi/meta.json
[Pinned source metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Weapons/Melee/m11_knife.rsi/meta.json)

License: CC-BY-SA-3.0

Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/106c92cdf232ebc12c9d7a2feb23956c6755496f/icons/obj/items/weapons/weapons.dmi

### Resources/Textures/Objects/Tools/Hydroponics/scythe.rsi/meta.json
[Pinned source metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/Objects/Tools/Hydroponics/scythe.rsi/meta.json)

License: CC-BY-SA-3.0

Copyright: Taken from Aurorastation at commit https://github.com/Aurorastation/Aurora.3/commit/3160508c1a9f367be0ab054cceaf2e36c0b66250

### Resources/Textures/Objects/Weapons/Melee/kukri_knife.rsi/meta.json
[Pinned source metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/Objects/Weapons/Melee/kukri_knife.rsi/meta.json)

License: CC-BY-SA-3.0

Copyright: sprite made by Jackal298

### Resources/Textures/Objects/Weapons/Guns/Ammunition/Magazine/LightRifle/light_rifle_box.rsi/meta.json
[Pinned source metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/Objects/Weapons/Guns/Ammunition/Magazine/LightRifle/light_rifle_box.rsi/meta.json)

License: CC-BY-SA-3.0

Copyright: https://github.com/discordia-space/CEV-Eris/raw/aed9cbddbf9039dae1e4f02bab592248b0539431/icons/obj/ammo_mags.dmi, inhands by TiniestShark

### Resources/Textures/_RMC14/Objects/Weapons/Guns/Attachments/rail.rsi/meta.json
[Pinned source metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Weapons/Guns/Attachments/rail.rsi/meta.json)

License: CC-BY-SA-3.0

Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/44751f0cc11332a5ba4f05ef91b2d72a0b08082b/icons/obj/items/weapons/guns/attachments/rail.dmi, edited iffbarrel & iffbarrel_a & type88_scope by SG6732, magnetic magnetic_a made by Sigma Draconis, XM43E1 scope by github noctyrnal, reddot and reflex by VictorJob

### Resources/Textures/_RMC14/Objects/Weapons/Guns/Attachments/under.rsi/meta.json
[Pinned source metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Weapons/Guns/Attachments/under.rsi/meta.json)

License: CC-BY-SA-3.0

Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/44751f0cc11332a5ba4f05ef91b2d72a0b08082b/icons/obj/items/weapons/guns/attachments/under.dmi, grenade-mk1_a.png grenade-mk1-open_a.png edited by Cephalopod222, flashgrip flashgrip-on flashgrip_a flashgrip_a-on by Sigma Draconis. laserlight resprite by VictorJob, flamethrower by github noctyrnal

### Resources/Textures/_RMC14/Objects/Weapons/Guns/Attachments/stock.rsi/meta.json
[Pinned source metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Weapons/Guns/Attachments/stock.rsi/meta.json)

License: CC-BY-SA-3.0

Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7e38a6a747b200b3854888a3eacd18b47992d066/icons/obj/items/weapons/guns/attachments/stock.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/f34d783a28859a36813c88778d57c38655076eb0/icons/obj/items/weapons/guns/attachments/stock.dmi, all m42a2 wooden stock camo types edited by SG6732, m16a5-stock modified from m16a5 taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7e3da429d0abfe55f0d948bb689bd7b49f3f304f/icons/obj/items/weapons/guns/guns_by_faction/colony/assault_rifles.dmi

### Resources/Textures/_RMC14/Objects/Weapons/Melee/baseball_bat.rsi/meta.json
[Pinned source metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Weapons/Melee/baseball_bat.rsi/meta.json)

License: CC-BY-SA-3.0

Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/a7dd814ed7c7773a3b910f03ae597c885635a32a/icons/obj/items/weapons/melee/non_lethal.dmi, https://github.com/cmss13-devs/cmss13/blob/7b4244612942e910b40148d4b54d38748f114a84/icons/mob/humans/onmob/inhands/weapons/melee/non_lethal_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/7b4244612942e910b40148d4b54d38748f114a84/icons/mob/humans/onmob/inhands/weapons/melee/non_lethal_righthand.dmi

### Resources/Textures/_RMC14/Objects/Weapons/Melee/metal_bat.rsi/meta.json
[Pinned source metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Weapons/Melee/metal_bat.rsi/meta.json)

License: CC-BY-SA-3.0

Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/a7dd814ed7c7773a3b910f03ae597c885635a32a/icons/obj/items/weapons/melee/non_lethal.dmi, https://github.com/cmss13-devs/cmss13/blob/7b4244612942e910b40148d4b54d38748f114a84/icons/mob/humans/onmob/inhands/weapons/melee/non_lethal_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/7b4244612942e910b40148d4b54d38748f114a84/icons/mob/humans/onmob/inhands/weapons/melee/non_lethal_righthand.dmi

### Resources/Textures/_RMC14/Objects/Weapons/Melee/co_sabre.rsi/meta.json
[Pinned source metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Weapons/Melee/co_sabre.rsi/meta.json)

License: CC-BY-SA-3.0

Copyright: Made by Aleksh

### Resources/Textures/_RMC14/Objects/Weapons/Melee/combat_utility_b.rsi/meta.json
[Pinned source metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Weapons/Melee/combat_utility_b.rsi/meta.json)

License: CC-BY-SA-4.0

Copyright: Created for RMC14 by augustsun(discord/august-sun(github)

### Resources/Textures/_RMC14/Objects/Weapons/Grenades/m07training.rsi/meta.json
[Pinned source metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Weapons/Grenades/m07training.rsi/meta.json)

License: CC-BY-SA-3.0

Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/a1079f4912a96473dae51cceaa1d74eafb3939e2/icons/obj/items/weapons/grenade.dmi, https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/mob/humans/onmob/inhands/weapons/grenades_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/mob/humans/onmob/inhands/weapons/grenades_lefthand.dmi

### Resources/Textures/_RMC14/Objects/Weapons/Melee/ice_axe.rsi/meta.json
[Pinned source metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Weapons/Melee/ice_axe.rsi/meta.json)

License: CC-BY-SA-3.0

Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/a7dd814ed7c7773a3b910f03ae597c885635a32a/icons/obj/items/weapons/melee/axes.dmi, https://github.com/cmss13-devs/cmss13/blob/7b4244612942e910b40148d4b54d38748f114a84/icons/mob/humans/onmob/inhands/weapons/melee/axes_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/7b4244612942e910b40148d4b54d38748f114a84/icons/mob/humans/onmob/inhands/weapons/melee/axes_righthand.dmi

### Resources/Textures/_RMC14/Objects/Weapons/Melee/m5_bayonet.rsi/meta.json
[Pinned source metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Weapons/Melee/m5_bayonet.rsi/meta.json)

License: CC-BY-SA-3.0

Copyright: Taken/modified from cmss13 at https://github.com/cmss13-devs/cmss13/pull/4029/commits/0d5274d407a165bc605b6a80686be22f6782cf06, equipped-MASK created by KalimbaMachine

### Resources/Textures/_RMC14/Objects/Weapons/Guns/Ammunition/Magazines/m60.rsi/meta.json
[Pinned source metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Weapons/Guns/Ammunition/Magazines/m60.rsi/meta.json)

License: CC-BY-SA-3.0

Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/168c8a79a3d858a8c531ff7ed0bc3033e47d1d16/icons/obj/items/weapons/guns/ammo_by_faction/colony.dmi

### Resources/Textures/_RMC14/Objects/Weapons/Guns/Ammunition/Magazines/b92fs.rsi/meta.json
[Pinned source metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Weapons/Guns/Ammunition/Magazines/b92fs.rsi/meta.json)

License: CC-BY-SA-3.0

Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/fe0134eee0c220b5b4a896689630fa9e14d92f55/icons/obj/items/weapons/guns/ammo_by_faction/uscm.dmi

### Resources/Textures/_RMC14/Objects/Weapons/Guns/Ammunition/Magazines/pk7.rsi/meta.json
[Pinned source metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Weapons/Guns/Ammunition/Magazines/pk7.rsi/meta.json)

License: CC-BY-SA-4.0

Copyright: Made by SharkSnake98 on Github

### Resources/Textures/_RMC14/Objects/Weapons/mortar.rsi/meta.json
[Pinned source metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Weapons/mortar.rsi/meta.json)

License: CC-BY-SA-3.0

Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/ca94d2e8715b73103fa9f213be53d343359b4107/icons/obj/structures/mortar.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/weapons/ammo_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/weapons/ammo_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items_by_map/classic_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items_by_map/desert_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items_by_map/jungle_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items_by_map/snow_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items_by_map/urban_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items_by_map/classic_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items_by_map/desert_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items_by_map/jungle_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items_by_map/snow_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/items_by_map/urban_righthand.dmi, heat shell recolor done by patogrone on discord.

### Resources/Textures/_RMC14/Objects/Weapons/Guns/Ammunition/SpeedLoaders/spearhead.rsi/meta.json
[Pinned source metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Weapons/Guns/Ammunition/SpeedLoaders/spearhead.rsi/meta.json)

License: CC-BY-SA-3.0

Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/0b241c0584b792912d42c34007d808494e386023/icons/obj/items/weapons/guns/ammo_by_faction/uscm.dmi

### Resources/Textures/_RMC14/Objects/Weapons/Melee/breaching.rsi/meta.json
[Pinned source metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Objects/Weapons/Melee/breaching.rsi/meta.json)

License: CC-BY-SA-3.0

Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/obj/items/weapons/melee/hammers.dmi, https://github.com/cmss13-devs/cmss13/blob/678d63ad96b75b1ac639436b2a9fdd7bd8009b70/icons/mob/humans/onmob/inhands/equipment/tools_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/678d63ad96b75b1ac639436b2a9fdd7bd8009b70/icons/mob/humans/onmob/inhands/equipment/tools_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/c4bd5fb6834149adf299a152a0c838c723c102b9/icons/mob/humans/onmob/clothing/back/melee_weapons.dmi
