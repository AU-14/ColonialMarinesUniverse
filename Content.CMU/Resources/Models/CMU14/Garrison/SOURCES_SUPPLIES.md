# Garrison medical, ammunition and packaged supplies

24 draft assemblies / 311 editable parts, explicitly mapping 25 classic prototype types and 82 saved objects. 58 are visible and 24 are hidden container contents. The full and empty closed flashlight boxes share one model because their composed source pixels are identical. Canonical geometry is in `garrison_supplies.yml`; scratch authoring scripts are not regeneration sources.

Geometric contributions are CC0-1.0 to the extent separately licensable. Original pixels and derivative appearance retain the source licenses and attribution below.

## Reconstruction and source states

Five medicine-specific bottles retain body colors, right-hand labels, closed caps and cap ribs. Six medical cartons retain their individual contents icons, cardboard folds and pale lids. Ammunition boxes preserve shell/label palettes, lid and closure details. Shotgun shell profiles lie along local X in two rows of three, matching the visible source pattern; this icon depicts six profiles while StorageFill creates five shell stacks. Physical dimensions, hidden faces and lighting remain inferred.

Static reference compositions use the original RGBA layers and declared tints. Medicine bottles combine body and closed cap, hiding the open cap. Full shotgun boxes hide the empty overlay. Flashlight and M77 boxes retain undeployed closed lids; empty variants hide the contents layer. The MRE box keeps its closed lid/band. The empty HE packet uses the open empty state because it has no initial or saved contents; the HIRR packet starts with three items and uses the closed state. The donut box uses the closed lid without runtime random donut overlays. `generated/supplies-source-audit.json` records every layer and reason. Explicit one-direction references are shown by the browser inspector unless the saved map supplies a sprite or connected-state override.

The donut box has separate horizontal YUM lid and vertical striped front. Box fronts and tops use separate crops. Gauze remains folded under its printed top; the torn wrapper preserves the source's detached piece and transparent pixels. Two capped-cylinder drums retain steel rims, bungs and source labels. Drum labels are still short planar decals, not curved wrapped UVs. 30 crop uses share 25 unique source PNGs; they were checked byte-for-byte against independently recomposed original artwork. Source crops are in `garrison_supply_art.yml`, atlas slots 239–263.

## Placement and connected counters

All 58 visible objects retain saved XY positions and rotations; presentation changes do not move simulation transforms. 42 rest on exact modeled supports, including all 16 medicine bottles. Their minimum center spacing is 0.2418005 tiles against 0.24-tile cap diameters. Seven drums stand on the floor. Nine props lack a usable exact support at their pivot and remain at saved floor height: wrappers #381/#382, empty HE packet #2321, flashlight boxes #9697/#9704/#9705, handcuff cartons #9711/#9712 and MRE box #9715. These are not asserted to have correct support fidelity: the HE/MRE table is still an inherited candidate, one flashlight box coincides with a crate without a declared support, and the handcuff pivots fall outside the modeled rack top. Some loose source objects already overlap; they are not silently moved apart. See `generated/supplies-placement-audit.json`.

`CMU3DReinforcedTable` was revised after two bottles fell into artificial gaps between adjoining tops. Its original full icon and connected quarter states were inspected. Same-key anchored neighbours extend the tabletop only to joined tile edges; `omitWhenConnected` removes internal edge trim and the south apron. The authored support and exported/native geometry use the same resolved footprint. Isolated edges remain inset, nearby walls do not create table joins, and quarter-state direction follows the grid. This applies to 49 classic tables and fixes seven existing/new supported props overall. The support top is at .835 tiles; the .002 placement clearance clears the .836 skin. The isolated source icon remains the table's comparison reference; the inspector does not compose smoothed quarter sprites.

## Review and limits

All 24 source/four-view cards and the revised table card were inspected. Browser context captures cover the medicine counter, medical cartons, donut counter, barrels, gauze and ammunition storage. The map data reports unresolved support separately. All assets remain drafts. Opening, deployment, runtime storage contents, medication levels, damage, detailed materials, physical cloth and unseen faces remain unfinished. This work does not replace the gameplay viewport.

## Source licenses

### /Textures/CMU14/Items/quickclot.rsi

- License: CC-BY-SA-3.0
- Attribution: Nzzy

### /Textures/_RMC14/Objects/Chemistry/pill_canister.rsi

- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/311e8f06c0eae7c6a0ab71ba04943c93c55850eb/icons/obj/items/chemistry.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/medical_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/master/icons/mob/humans/onmob/inhands/equipment/medical_lefthand.dmi

### /Textures/_RMC14/Objects/Storage/boxes.rsi

- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/71d46ee8057d19b12e1495419cb299d2fedef6cc/icons/obj/items/storage.dmi, https://github.com/cmss13-devs/cmss13/blob/c648dca374ddee1c80f61ab88678c287356fa66f/icons/obj/items/storage.dmi, l96 modified from m94 by github monomethylhydrazine, https://github.com/cmss13-devs/cmss13/blob/b6f4841b5768599c0fcf58a21fd88536a9959c2c/icons/obj/items/storage/boxes.dmi

### /Textures/_RMC14/Objects/Storage/donutbox.rsi

- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/134b9b90f2d59c11f05e7e3faf06f0a971482809/icons/obj/items/food.dmi

### /Textures/_RMC14/Objects/Storage/packets.rsi

- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/items/storage/packets.dmi

### /Textures/_RMC14/Objects/Weapons/Guns/Ammunition/Boxes/modular_boxes.rsi

- License: CC-BY-SA-3.0
- Attribution: Original Sprite by Aleksh, Modified by Ramiris, SharkSnake98 on Github, RavenCaat on GitHub, github noctyrnal, and @TadJohnson00 (GitHub), _24 and _a3 by github monomethylhydrazine

### /Textures/_RMC14/Structures/barrels.rsi

- License: CC-BY-SA-3.0
- Attribution: https://github.com/cmss13-devs/cmss13/blob/7cb618c69b75873f3ce893022fe08d1233b3152d/icons/obj/structures/crates.dmi

### /Textures/Structures/Furniture/Tables/reinforced.rsi

- License: CC-BY-SA-3.0
- Attribution: Taken from https://github.com/discordia-space/CEV-Eris/blob/0b3ab17dbad632ddf738b63900ef8df1926bba47/icons/obj/tables.dmi

## Composed source states

| Prototype | Visible states | Parts |
| --- | --- | ---: |
| AU14HemostaticGauze | hemgauze | 6 |
| AU14HemostaticGauzePacketTrash | qc_ripped | 2 |
| CMPacketGrenadeHighExplosive | hedp_packet_e | 9 |
| CMPillCanisterBicaridine | pill_canister11 + closed | 23 |
| CMPillCanisterDexalin | pill_canister1 + closed | 23 |
| CMPillCanisterDylovene | pill_canister6 + closed | 23 |
| CMPillCanisterInaprovaline | pill_canister3 + closed | 23 |
| CMPillCanisterKelotane | pill_canister2 + closed | 23 |
| RMCBarrelWhite | barrel_white | 13 |
| RMCBarrelYellow | barrel_yellow | 13 |
| RMCBoxBodyBag | bodybags | 6 |
| RMCBoxDonut | box | 16 |
| RMCBoxFlashlights | supply_box + supply_ammo_full + supply_box_flashlight + supply_lid_closed | 7 |
| RMCBoxFlashlightsEmpty | supply_box + supply_box_flashlight + supply_lid_closed | 7 |
| RMCBoxHandcuffs | handcuff | 6 |
| RMCBoxLatexGloves | latex | 6 |
| RMCBoxMRE | supply_box_small + supply_ammo_small_full + supply_box_small_food + supply_lid_small_closed + supply_lid_small_closed_marking | 7 |
| RMCBoxMagazinePistolM77APEmpty | pistol_box + pistol_box_77 + pistol_box_marking + pistol_lid + pistol_lid_marking | 7 |
| RMCBoxPillCanister | pillbox | 6 |
| RMCBoxShotgunBeanbag | smallshell_box + shell_box_buckshot + smallshell_ammo_full_12g_base + smallshell_ammo_full_12g_color | 24 |
| RMCBoxShotgunBuckshot | smallshell_box + shell_box_buckshot + smallshell_ammo_full_12g_base + smallshell_ammo_full_12g_color | 24 |
| RMCBoxShotgunSlugs | smallshell_box + shell_box_buckshot + smallshell_ammo_full_12g_base + smallshell_ammo_full_12g_color | 24 |
| RMCBoxSterileMask | sterile | 6 |
| RMCBoxSyringe | syringe | 6 |
| RMCPacketGrenadeBatonSlugHIRRFilled | baton_packet | 8 |
