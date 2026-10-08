# Weapon-world exterior prop draft sources

## Scope and result

Fourteen unique exact draft mappings, covering the inventory’s 25 Redux and 15 classic records. There are fourteen default GLB scenes and zero clips, with 388 individually editable supported parts (17–44 per model). Every target is present in the frozen physicalModelingFamilies queue and Redux visual inventory.

These are nonfunctional stylized exterior art props. No internal mechanism, functional construction detail, engineering dimensions, engine code, game runtime, push or publication is part of this batch. All models remain draft.

## Geometry and source interpretation

- Stock, receiver, barrel exterior, grip, magazine and visible attachment housings are separate supported volumes. No whole-sprite slab is used. Fourteen tiny opaque source crops retain narrow receiver or tube artwork; all other colors are chosen from the relevant composed source palette.
- Every model uses the source-facing +X muzzle orientation. Sprite noRot=false and the original frame-center pivot are retained, including the unusually left-offset 64-pixel AR10 frame. Source-facing previews are presentation crops, not saved-map placement tests.
- Five models include deterministic starting attachments: compact MP5 stock, M5SPR flare attachment, M60 folded bipod, hunting stock and scope, and Mk1 collapsed stock plus underslung launcher. Source owners define suffix _a by default, with the hunting parts overriding it to empty. Total overlay offset is holder-slot offset plus attachment-visual offset.
- Both MP5s use the original ItemMapper stick-magazine layer selected by their starting magazine’s tag. The mapper’s minimum-count default is one. Other magazine layers use the source MagazineVisuals one-step rule; AR10’s initialized mag-0 has the exact same RGBA pixels as its declared mag-2.
- Default source-layer and deterministic attachment positions are composed for inspection. Fractional source offsets are retained numerically in the audit; nearest-pixel rounding is used only in the pixel-art reference images. Geometry silhouettes, bevels, depth and hidden faces remain stylized inferences.
- Twelve of nineteen checked openings correspond to source-transparent pixels. Seven are explicitly labeled inferred physical recess/opening studies within shaded low-resolution source artwork. All nineteen probes are empty in authored geometry. These are bounded probes, not a silhouette-fidelity score.
- An independent convex-hull contact check on the final GLB meshes finds one connected component per model, including touching skins. It is not manifold certification or a saved-map contact audit.

## Exact targets and selected source layers

- `AU14WeaponRifleM16A1` → `CMU3DWorldAU14WeaponRifleM16A1Cloud.glb`: 34 parts; layers base, mag-0
- `AU14WeaponRifleAR10` → `CMU3DWorldAU14WeaponRifleAR10Cloud.glb`: 34 parts; layers base, mag-0
- `WeaponSMGMP5` → `CMU3DWorldWeaponSMGMP5Cloud.glb`: 34 parts; layers base, mag-0
- `RMCWeaponSMGMP5Alt` → `CMU3DWorldRMCWeaponSMGMP5AltCloud.glb`: 32 parts; layers base, mag-0, mp5_stock_a
- `AU14WeaponRevolverM2019` → `CMU3DWorldAU14WeaponRevolverM2019Cloud.glb`: 21 parts; layers icon
- `RMCWeaponLauncherM81` → `CMU3DWorldRMCWeaponLauncherM81Cloud.glb`: 17 parts; layers base
- `WeaponRifleM5SPR` → `CMU3DWorldWeaponRifleM5SPRCloud.glb`: 32 parts; layers bolt-open, d_m4spr_custom_barrel, mag-0, flaregun_a
- `RMCWeaponLMGM60` → `CMU3DWorldRMCWeaponLMGM60Cloud.glb`: 44 parts; layers base, mag-0, barrel, m60_stock, bipod_a
- `RMCWeaponRifleABR40Tactical` → `CMU3DWorldRMCWeaponRifleABR40TacticalCloud.glb`: 20 parts; layers base, mag-0, abr40stock_tac
- `RMCWeaponRevolver38Magnum` → `CMU3DWorldRMCWeaponRevolver38MagnumCloud.glb`: 18 parts; layers icon
- `RMCWeaponBoltActionRifle` → `CMU3DWorldRMCWeaponBoltActionRifleCloud.glb`: 27 parts; layers base, mag-0, huntingscope, huntingstock
- `RMCWeaponRifleM54CMK1` → `CMU3DWorldRMCWeaponRifleM54CMK1Cloud.glb`: 34 parts; layers base, mag-0, m54c-col_a, grenade-mk1_a
- `RMCWeaponLauncherDisposable` → `CMU3DWorldRMCWeaponLauncherDisposableCloud.glb`: 21 parts; layers icon
- `RMCWeaponPistolB92FS` → `CMU3DWorldRMCWeaponPistolB92FSCloud.glb`: 20 parts; layers base, mag-0

## Explicit unsupported states

Only one selected composed ground-world appearance is authored per target. No spriteStates adapter is declared. Firing, recoil, reload, bolt/breech or slide transitions, magazine removal or substitution, spent forms, toggled stocks, active bipods, attachment removal/replacement/random choices, held/worn appearances, rigging, animation and live container changes are unsupported. M5SPR’s declared bolt-open base art is a static source pose, not an implemented loading action. No new states or directions inflate the fourteen-target count. Full original RSI state names, directions and timing metadata remain in the source-state audit.

No Khronos validation, native renderer admission, full-map contact, live-state behavior or fidelity approval is claimed. The independent Blender imports do not establish those outcomes.

## Files and reproduction

- Canonical models: `Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_weapon_world_cloud.yml`
- Canonical source surfaces: `Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_weapon_world_cloud_art.yml`
- Fourteen source PNG patches: `Content.CMU/Resources/Textures/CMU14/ThreeD/weapon_world_cloud/`
- GLBs: this directory, named in the target list above
- Source-facing/orbit comparisons and overview: `Tools/three_d/generated/cloud-review/weapon-world/`
- Structured evidence: `weapon-world-proof.json`, `weapon-world-contacts.json`, `weapon-world-blender-import.json`, `weapon-world-source-state-audit.json`, `weapon-world-controller-source-audit.json` and `weapon-world-manifest.json`
- Exact reserved atlas prefix used: 2461, 2462, 2463, 2464, 2465, 2466, 2467, 2468, 2469, 2470, 2471, 2472, 2473, 2474; the remaining weapon_world slots stay unused.

Run in order from repository root:

```sh
python reference/inspect_weapon_world.py
python Tools/three_d/author_weapon_world_cloud.py
python Tools/three_d/verify_weapon_world_cloud.py
OPENBLAS_NUM_THREADS=1 python Tools/three_d/check_weapon_world_contacts.py
blender -b -t 1 --python Tools/three_d/check_weapon_world_blender.py
python Tools/three_d/document_weapon_world_cloud.py
```

The unchanged build_models exporter supplies deterministic bytes. Verification checks the final accessors, bounds, finite geometry, draft status, source palette, source-file Git SHAs, unique IDs, exact mappings, allocated atlas indices, cropped source bytes and above-floor geometry. Blender 4.3.2 imported all fourteen final-byte files with no mesh repairs and no actions.

## Source checkpoint and attribution

Repository: TheHellFireo/CMU-Garrison-3D, pinned commit `6e37a4d0a7d9433838c393a82c02422cb704dd5a`. Eighty-six unique source inputs are Git-blob-SHA verified. Source-derived textures retain their original CC-BY-SA-3.0 attribution and are derivative artwork under those terms. Complete unchanged original metadata and source links follow.

### Resources/Textures/_RMC14/Objects/Weapons/Guns/Attachments/m54_stocks/desert.rsi/meta.json

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7e3da429d0abfe55f0d948bb689bd7b49f3f304f/icons/obj/items/weapons/guns/attachments/stock.dmi

### Resources/Textures/_RMC14/Objects/Weapons/Guns/Attachments/rail.rsi/meta.json

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/44751f0cc11332a5ba4f05ef91b2d72a0b08082b/icons/obj/items/weapons/guns/attachments/rail.dmi, edited iffbarrel & iffbarrel_a & type88_scope by SG6732, magnetic magnetic_a made by Sigma Draconis, XM43E1 scope by github noctyrnal, reddot and reflex by VictorJob

### Resources/Textures/_RMC14/Objects/Weapons/Guns/Attachments/stock.rsi/meta.json

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7e38a6a747b200b3854888a3eacd18b47992d066/icons/obj/items/weapons/guns/attachments/stock.dmi, https://github.com/cmss13-devs/cmss13-pve/blob/f34d783a28859a36813c88778d57c38655076eb0/icons/obj/items/weapons/guns/attachments/stock.dmi, all m42a2 wooden stock camo types edited by SG6732, m16a5-stock modified from m16a5 taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7e3da429d0abfe55f0d948bb689bd7b49f3f304f/icons/obj/items/weapons/guns/guns_by_faction/colony/assault_rifles.dmi

### Resources/Textures/_RMC14/Objects/Weapons/Guns/Attachments/under.rsi/meta.json

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/44751f0cc11332a5ba4f05ef91b2d72a0b08082b/icons/obj/items/weapons/guns/attachments/under.dmi, grenade-mk1_a.png grenade-mk1-open_a.png edited by Cephalopod222, flashgrip flashgrip-on flashgrip_a flashgrip_a-on by Sigma Draconis. laserlight resprite by VictorJob, flamethrower by github noctyrnal

### Content.CMU/Resources/Textures/CMU14/Weapons/Guns/Civilian/auar1064x32.rsi/meta.json

License: CC-BY-SA-3.0

Created or reused for CMU14/CMU14

### Content.CMU/Resources/Textures/CMU14/Weapons/Guns/Civilian/aum1648x32.rsi/meta.json

License: CC-BY-SA-3.0

nzzy

### Content.CMU/Resources/Textures/CMU14/Weapons/Guns/Civilian/blaster.rsi/meta.json

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/3161d3df2825c9e3eaeba5a71b2b17749dba67db/icons/obj/items/weapons/guns/guns_by_faction/colony/revolvers.dmi, https://github.com/cmss13-devs/cmss13/blob/678d63ad96b75b1ac639436b2a9fdd7bd8009b70/icons/mob/humans/onmob/inhands/weapons/guns/revolvers_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/678d63ad96b75b1ac639436b2a9fdd7bd8009b70/icons/mob/humans/onmob/inhands/weapons/guns/revolvers_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/mob/humans/onmob/clothing/suit_storage/guns_by_type/pistols.dmi

### Resources/Textures/_RMC14/Objects/Weapons/Guns/Attachments/barrel.rsi/meta.json

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/44751f0cc11332a5ba4f05ef91b2d72a0b08082b/icons/obj/items/weapons/guns/attachments/barrel.dmi, npz92_suppressor by github noctyrnal. Ebarrel, hbarrel and suppressor resprite by VictorJob, tanto, dagger and combat utility knife bayonets by august-sun (github). suppressed_sniper_barrel modified from sniperbarrel by N2H4

### Resources/Textures/_RMC14/Objects/Weapons/Guns/GrenadeLaunchers/m81.rsi/meta.json

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/4047c473d4d2f3e58e68efdec5aa95d0676bf4d1/icons/obj/items/weapons/guns/guns_by_faction/uscm.dmi, https://github.com/cmss13-devs/cmss13/blob/106c92cdf232ebc12c9d7a2feb23956c6755496f/icons/mob/humans/onmob/items_lefthand_1.dmi, https://github.com/cmss13-devs/cmss13/blob/4047c473d4d2f3e58e68efdec5aa95d0676bf4d1/icons/mob/humans/onmob/items_righthand_1.dmi, https://github.com/cmss13-devs/cmss13/blob/4047c473d4d2f3e58e68efdec5aa95d0676bf4d1/icons/mob/humans/onmob/suit_slot.dmi, https://github.com/cmss13-devs/cmss13/blob/78bf17edb3a0ce984c1cd71e6ebb6e788eb0fb1b/icons/mob/humans/onmob/back.dmi

### Resources/Textures/_RMC14/Objects/Weapons/Guns/LMGs/m60.rsi/meta.json

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/168c8a79a3d858a8c531ff7ed0bc3033e47d1d16/icons/obj/items/weapons/guns/guns_by_faction/colony.dmi, https://github.com/cmss13-devs/cmss13/blob/168c8a79a3d858a8c531ff7ed0bc3033e47d1d16/icons/mob/humans/onmob/items_righthand_1.dmi, https://github.com/cmss13-devs/cmss13/blob/168c8a79a3d858a8c531ff7ed0bc3033e47d1d16/icons/mob/humans/onmob/items_lefthand_1.dmi, https://github.com/cmss13-devs/cmss13/blob/168c8a79a3d858a8c531ff7ed0bc3033e47d1d16/icons/mob/humans/onmob/suit_slot.dmi

### Resources/Textures/_RMC14/Objects/Weapons/Guns/Pistols/b92fs.rsi/meta.json

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/168c8a79a3d858a8c531ff7ed0bc3033e47d1d16/icons/obj/items/clothing/belts.dmi, https://github.com/cmss13-devs/cmss13/blob/168c8a79a3d858a8c531ff7ed0bc3033e47d1d16/icons/mob/humans/onmob/items_lefthand_1.dmi, https://github.com/cmss13-devs/cmss13/blob/168c8a79a3d858a8c531ff7ed0bc3033e47d1d16/icons/mob/humans/onmob/items_righthand_1.dmi, https://github.com/cmss13-devs/cmss13/blob/168c8a79a3d858a8c531ff7ed0bc3033e47d1d16/icons/obj/items/weapons/guns/guns_by_faction/colony.dmi, https://github.com/cmss13-devs/cmss13/blob/fe0134eee0c220b5b4a896689630fa9e14d92f55/icons/obj/items/weapons/guns/ammo_by_faction/uscm.dmi

### Resources/Textures/_RMC14/Objects/Weapons/Guns/Pistols/magnum38.rsi/meta.json

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/3161d3df2825c9e3eaeba5a71b2b17749dba67db/icons/obj/items/weapons/guns/guns_by_faction/colony/revolvers.dmi, https://github.com/cmss13-devs/cmss13/blob/678d63ad96b75b1ac639436b2a9fdd7bd8009b70/icons/mob/humans/onmob/inhands/weapons/guns/revolvers_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/678d63ad96b75b1ac639436b2a9fdd7bd8009b70/icons/mob/humans/onmob/inhands/weapons/guns/revolvers_righthand.dmi

### Resources/Textures/_RMC14/Objects/Weapons/Guns/Rifles/abr40tac.rsi/meta.json

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7e3da429d0abfe55f0d948bb689bd7b49f3f304f/icons/obj/items/weapons/guns/guns_by_faction/colony/marksman_rifles.dmi, https://github.com/cmss13-devs/cmss13/blob/c4bd5fb6834149adf299a152a0c838c723c102b9/icons/mob/humans/onmob/inhands/weapons/guns/marksman_rifles_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/c4bd5fb6834149adf299a152a0c838c723c102b9/icons/mob/humans/onmob/inhands/weapons/guns/marksman_rifles_righthand.dmi

### Resources/Textures/_RMC14/Objects/Weapons/Guns/Rifles/hunting.rsi/meta.json

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/168c8a79a3d858a8c531ff7ed0bc3033e47d1d16/icons/obj/items/weapons/guns/guns_by_faction/colony.dmi, https://github.com/cmss13-devs/cmss13/blob/168c8a79a3d858a8c531ff7ed0bc3033e47d1d16/icons/mob/humans/onmob/items_lefthand_1.dmi, https://github.com/cmss13-devs/cmss13/blob/168c8a79a3d858a8c531ff7ed0bc3033e47d1d16/icons/mob/humans/onmob/items_righthand_1.dmi - Resprited by Discord Merfarukier. Second resprite by kleinerstation13 (Github)

### Resources/Textures/_RMC14/Objects/Weapons/Guns/Rifles/m4spr_custom/desert.rsi/meta.json

License: CC-BY-SA-3.0

Taken and modified from cmss13 at https://github.com/cmss13-devs/cmss13/blob/07455a5f3986b610e7ccdc3096cad1d914fca2f9/icons/obj/items/weapons/guns/guns_by_map/desert/back.dmi, https://github.com/cmss13-devs/cmss13/blob/07455a5f3986b610e7ccdc3096cad1d914fca2f9/icons/obj/items/weapons/guns/guns_by_map/desert/guns_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/07455a5f3986b610e7ccdc3096cad1d914fca2f9/icons/obj/items/weapons/guns/guns_by_map/desert/guns_obj.dmi, https://github.com/cmss13-devs/cmss13/blob/07455a5f3986b610e7ccdc3096cad1d914fca2f9/icons/obj/items/weapons/guns/guns_by_map/desert/guns_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/07455a5f3986b610e7ccdc3096cad1d914fca2f9/icons/obj/items/weapons/guns/guns_by_map/desert/suit_slot.dmi

### Resources/Textures/_RMC14/Objects/Weapons/Guns/Rifles/m54cmk1.rsi/meta.json

License: CC-BY-SA-3.0

Created by Cephalopod222 and github noctyrnal

### Resources/Textures/_RMC14/Objects/Weapons/Guns/RocketLaunchers/m5a1_disposable.rsi/meta.json

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/15d853a340792692e623bcd30f7d8cc058ad6343/icons/mob/humans/onmob/inhands/weapons/guns/rocket_launchers_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/df1d568bcc9ef92e8e5b8a49ee844977076a9ef0/icons/mob/humans/onmob/clothing/suit_storage/guns_by_type/rocket_launchers.dmi, https://github.com/cmss13-devs/cmss13/blob/15d853a340792692e623bcd30f7d8cc058ad6343/icons/mob/humans/onmob/inhands/weapons/guns/rocket_launchers_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/df1d568bcc9ef92e8e5b8a49ee844977076a9ef0/icons/obj/items/weapons/guns/guns_by_faction/USCM/rocket_launchers.dmi

### Resources/Textures/_RMC14/Objects/Weapons/Guns/SMGs/mp5.rsi/meta.json

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/168c8a79a3d858a8c531ff7ed0bc3033e47d1d16/icons/obj/items/weapons/guns/guns_by_faction/colony.dmi, https://github.com/cmss13-devs/cmss13/blob/168c8a79a3d858a8c531ff7ed0bc3033e47d1d16/icons/mob/humans/onmob/items_lefthand_1.dmi, https://github.com/cmss13-devs/cmss13/blob/168c8a79a3d858a8c531ff7ed0bc3033e47d1d16/icons/mob/humans/onmob/items_righthand_1.dmi; magazine states modified by rando for CMU

### Resources/Textures/_RMC14/Objects/Weapons/Guns/SMGs/mp5alt.rsi/meta.json

License: CC-BY-SA-3.0

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/7e3da429d0abfe55f0d948bb689bd7b49f3f304f/icons/obj/items/weapons/guns/guns_by_faction/colony/smgs.dmi, https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/mob/humans/onmob/clothing/suit_storage/guns_by_type/smgs.dmi, https://github.com/cmss13-devs/cmss13/blob/48e570bd697f2476e28d89cd255d0539a5228228/icons/obj/items/weapons/guns/attachments/stock.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/weapons/guns/smgs_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/41b3798435ee57263bb67fa694ea944d6698c062/icons/mob/humans/onmob/inhands/weapons/guns/smgs_righthand.dmi
