# Garrison small-prop source references

26 draft assemblies. Source sprites, default and saved components, and placement were inspected. Canonical geometry is the YAML; none of these drafts is art-approved.

New geometric work is contributed under CC0-1.0 to the extent separately licensable. Source visual-design and derivative rights retain their existing licenses and attribution.

## Placement and fidelity

Tools, cuffs, clipboard, medical packs and syringes lie near their support plane. Toolboxes, clock, bell and bottle stand on their bases. Produce follows the distinct source cultivar, color and arrangement. Item models use actual authored table surfaces where available and remain at their saved floor position otherwise; transforms are not moved. Widths follow visible sprite extents at 32 pixels per tile with inferred physical depth. Hidden sides, thickness, resting poses and organic surfaces still need refinement.

Mappings are explicit per inspected prototype. Closed filled toolboxes do not expose their contents. Clipboard paper/pen and dry mop fill layers are hidden by default. The syringe is empty; the water bottle is capped. The clock uses the first animated source frame, with two cyan bands. Dynamic opening, liquid levels, reagent tints, clock animation and inventory appearances remain unfinished.

The full blood pack needs a runtime composite: BloodPackComponent has seven levels; IVDripSystem.UpdatePackAppearance selects bloodpack7 at full, colors it with Blood (#800000), draws bloodpack above it and hides the optional Label layer. The model represents that full pose. Its ordinary reference sheet shows the case, not a falsely empty blank-label composite. A separately saved smallprops-bloodpack-composite.png records the full source state for review. Hidden IV-slot contents remain excluded from the map scene.

## Models

| Model | Exact prototype | State |
| --- | --- | --- |
| CMU3DEmergencyToolbox | RMCToolboxEmergencyFilled | icon |
| CMU3DElectricalToolbox | RMCToolboxElectricalFilled | icon |
| CMU3DMechanicalToolbox | RMCToolboxMechanicalFilled | icon |
| CMU3DGreenMechanicalToolbox | RMCToolboxMechanicalGreenFilled | icon |
| CMU3DMiningPickaxe | AU14ToolPickaxe | icon |
| CMU3DJanitorialMop | CMMop | mop |
| CMU3DSoapBar | CMSoap | soap |
| CMU3DBareClipboard | CMClipboard | clipboard |
| CMU3DDigitalDeskClock | RMCDigitalClock | digital_clock |
| CMU3DBrassDeskBell | RMCDeskBell | desk_bell |
| CMU3DSteelHandcuffs | RMCHandcuffs | handcuff |
| CMU3DFullBloodPack | CMBloodPackFull | bloodpack |
| CMU3DEmptyMedicalSyringe | CMSyringe | syringe |
| CMU3DWeyaWaterBottle | CMDrinkWEYAWaterBottle30 | icon |
| CMU3DGreenApple | FoodApple | produce |
| CMU3DMixedBerries | FoodBerries | produce |
| CMU3DRedGrapeBunch | FoodGrape | produce |
| CMU3DCarrotRoot | FoodCarrot | produce |
| CMU3DCurvedBanana | FoodBanana | produce |
| CMU3DRedChiliPepper | FoodChiliPepper | produce |
| CMU3DCornCob | FoodCorn | produce |
| CMU3DYellowLemon | FoodLemon | produce |
| CMU3DRedTomato | FoodTomato | produce |
| CMU3DTwinCherries | FoodCherry | produce |
| CMU3DLeafyCabbage | FoodCabbage | produce |
| CMU3DRibbedPumpkin | FoodPumpkin | produce |

## Source attribution

### Resources/Textures/Objects/Specific/Hydroponics/apple.rsi

- Metadata: `Resources/Textures/Objects/Specific/Hydroponics/apple.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from https://github.com/vgstation-coders/vgstation13/commit/1dbcf389b0ec6b2c51b002df5fef8dd1519f8068, inhands by mubururu_ (github), Growth stages, harvest, dead, and produce sprites created by Chaoticaa (GitHub), inhands modified by Prole0 (GitHub)

### Resources/Textures/Objects/Specific/Hydroponics/banana.rsi

- Metadata: `Resources/Textures/Objects/Specific/Hydroponics/banana.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from tgstation at https://github.com/tgstation/tgstation/commit/6be7633abca9f1a51cab1020500cf0776ce78e5c, inhands by mubururu_ (github), Growth stages, harvest, dead, and produce created by Chaoticaa (GitHub), inhands modified by Prole0 (GitHub), On-head sprites by FlipBrooke,.

### Resources/Textures/Objects/Specific/Hydroponics/berries.rsi

- Metadata: `Resources/Textures/Objects/Specific/Hydroponics/berries.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from tgstation: https://github.com/tgstation/tgstation/commit/696dfcc59c9e65e7bbe3923d1f7e880ea384783f, inhands by mubururu_ (github), Growth, harvest, and dead sprites created by Chaoticaa (GitHub)

### Resources/Textures/Objects/Specific/Hydroponics/cabbage.rsi

- Metadata: `Resources/Textures/Objects/Specific/Hydroponics/cabbage.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from https://github.com/vgstation-coders/vgstation13/commit/b459ea3fdee965bdc3e93e7983ad7fa610d05c12#diff-3fdb6bdffec70eb17e6315b5cc5447b4cc73d39f583f077f064ad70f286138fa, inhands by mubururu_ (github), Growth stages, harvest, dead, and produce created by Chaoticaa (GitHub), inhands modified by Prole0 (GitHub)

### Resources/Textures/Objects/Specific/Hydroponics/carrot.rsi

- Metadata: `Resources/Textures/Objects/Specific/Hydroponics/carrot.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from https://github.com/vgstation-coders/vgstation13/commit/1dbcf389b0ec6b2c51b002df5fef8dd1519f8068, inhands by mubururu_ (github)

### Resources/Textures/Objects/Specific/Hydroponics/cherry.rsi

- Metadata: `Resources/Textures/Objects/Specific/Hydroponics/cherry.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from https://github.com/vgstation-coders/vgstation13/commit/1dbcf389b0ec6b2c51b002df5fef8dd1519f8068 and remade by RumiTiger, Growth stages, harvest, dead, and produce sprites created by Chaoticaa (GitHub)

### Resources/Textures/Objects/Specific/Hydroponics/chili.rsi

- Metadata: `Resources/Textures/Objects/Specific/Hydroponics/chili.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from https://github.com/vgstation-coders/vgstation13, inhands by mubururu_ (github), Growth stages, harvest, dead, and produce sprites created by Chaoticaa (GitHub), inhands modified by Prole0 (GitHub)

### Resources/Textures/Objects/Specific/Hydroponics/corn.rsi

- Metadata: `Resources/Textures/Objects/Specific/Hydroponics/corn.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from https://github.com/vgstation-coders/vgstation13/commit/1dbcf389b0ec6b2c51b002df5fef8dd1519f8068, Growth, dead, harvest, and produce created by Chaoticaa (GitHub)

### Resources/Textures/Objects/Specific/Hydroponics/grape.rsi

- Metadata: `Resources/Textures/Objects/Specific/Hydroponics/grape.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from https://github.com/vgstation-coders/vgstation13/commit/b459ea3fdee965bdc3e93e7983ad7fa610d05c12, Growth stages, harvest, dead, and produce created by Chaoticaa (GitHub), inhands by mubururu_ (github), inhands modified by Prole0 (GitHub)

### Resources/Textures/Objects/Specific/Hydroponics/lemon.rsi

- Metadata: `Resources/Textures/Objects/Specific/Hydroponics/lemon.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from https://github.com/vgstation-coders/vgstation13/commit/1dbcf389b0ec6b2c51b002df5fef8dd1519f8068, inhands by mubururu_ (github), Growth stages, harvest, dead, and produce created by Chaoticaa (GitHub), inhands modified by Prole0 (GitHub)

### Resources/Textures/Objects/Specific/Hydroponics/pumpkin.rsi

- Metadata: `Resources/Textures/Objects/Specific/Hydroponics/pumpkin.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from https://github.com/tgstation/tgstation/blob/5d507cfbad6f73d1beaba66d93f31f893adb3a84/icons/obj/hydroponics/harvest.dmi, carved sprites by ps3moira, inhands by mubururu_ (github), Growth stages, dead, and harvest sprites created by Chaoticaa (GitHub)

### Resources/Textures/Objects/Specific/Hydroponics/tomato.rsi

- Metadata: `Resources/Textures/Objects/Specific/Hydroponics/tomato.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from https://github.com/vgstation-coders/vgstation13 at 1dbcf389b0ec6b2c51b002df5fef8dd1519f8068, inhands by mubururu_ (github), Growth stages, harvest, dead, and produce sprites created by Chaoticaa (GitHub)

### Resources/Textures/_RMC14/Objects/Consumable/Drinks/WEYADrinks/weya_water_bottle.rsi

- Metadata: `Resources/Textures/_RMC14/Objects/Consumable/Drinks/WEYADrinks/weya_water_bottle.rsi/meta.json`
- License: CC-BY-SA-4.0
- Attribution: Made by SharkSnake98 on GitHub

### Resources/Textures/_RMC14/Objects/Devices/clocks.rsi

- Metadata: `Resources/Textures/_RMC14/Objects/Devices/clocks.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/407cccb4e442d24c75cbc4b308ec74eea684b09f/icons/obj/items/devices.dmi, https://github.com/cmss13-devs/cmss13/blob/407cccb4e442d24c75cbc4b308ec74eea684b09f/icons/obj/structures/props/furniture/catclock.dmi

### Resources/Textures/_RMC14/Objects/Medical/blood_pack.rsi

- Metadata: `Resources/Textures/_RMC14/Objects/Medical/blood_pack.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/1d28964d37f9b95773580cca3471a2a4f5c03eb0/icons/obj/items/bloodpack.dmi

### Resources/Textures/_RMC14/Objects/Medical/syringe.rsi

- Metadata: `Resources/Textures/_RMC14/Objects/Medical/syringe.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/b0bca8ef7dedf94a30f456544bec6ef3aaa7ec8a/icons/obj/items/syringe.dmi, Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/1d28964d37f9b95773580cca3471a2a4f5c03eb0/icons/obj/items/reagentfillings.dmi , en from cev-eris https://github.com/discordia-space/CEV-Eris/commit/989b7b343045f30120c198ee100c9fee7ff8a989, bluespace syringe sprites modified by EmoGarbage404 (github), cryo syringe sprites by Ubaser

### Resources/Textures/_RMC14/Objects/Misc/Janitorial/mop.rsi

- Metadata: `Resources/Textures/_RMC14/Objects/Misc/Janitorial/mop.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/106c92cdf232ebc12c9d7a2feb23956c6755496f/icons/obj/janitor.dmi, https://github.com/cmss13-devs/cmss13/blob/bec6653d487a49aa2b5a8e0c97bed9612f620211/icons/mob/humans/onmob/items_righthand_0.dmi, https://github.com/cmss13-devs/cmss13/blob/bec6653d487a49aa2b5a8e0c97bed9612f620211/icons/mob/humans/onmob/items_lefthand_0.dmi

### Resources/Textures/_RMC14/Objects/Misc/Janitorial/soap.rsi

- Metadata: `Resources/Textures/_RMC14/Objects/Misc/Janitorial/soap.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/5a2359bab582e18b3b432539733be04204148a5e/icons/obj/items/items.dmi

### Resources/Textures/_RMC14/Objects/Misc/clipboard.rsi

- Metadata: `Resources/Textures/_RMC14/Objects/Misc/clipboard.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/9ab207cd7ffba86a0411d7058645fb8f2a7895f3/icons/obj/items/paper.dmi, clipboard_paper modified by KalimbaMachine (Github) from cmss13 paper.dmi, inhand sprites modified by KalimbaMachine (Github) from nmajask (Github) for SS14's inhand sprites, equipped-BELT Made by KalimbaMachine (Github), clipboard_pen made by KalimbaMachine (Github)

### Resources/Textures/_RMC14/Objects/Misc/handcuffs.rsi

- Metadata: `Resources/Textures/_RMC14/Objects/Misc/handcuffs.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/659327b8ccb48e73ccb568fc157b780871763867/icons/obj/items/items.dmi, https://github.com/cmss13-devs/cmss13/blob/bec6653d487a49aa2b5a8e0c97bed9612f620211/icons/mob/humans/onmob/items_lefthand_0.dmi, https://github.com/cmss13-devs/cmss13/blob/bec6653d487a49aa2b5a8e0c97bed9612f620211/icons/mob/humans/onmob/items_righthand_0.dmi, https://github.com/cmss13-devs/cmss13/blob/bec6653d487a49aa2b5a8e0c97bed9612f620211/icons/mob/humans/onmob/items_righthand_0.dmi, https://github.com/cmss13-devs/cmss13/blob/81c7806eb705f3a6b43085056cef1be0055d8ed2/icons/mob/humans/onmob/belt.dmi, https://github.com/cmss13-devs/cmss13/blob/96af5f3aaa14175d176450b34e92b7e3f68e8567/icons/mob/mob.dmi, https://github.com/cmss13-devs/cmss13/blob/81c7806eb705f3a6b43085056cef1be0055d8ed2/icons/mob/humans/onmob/suit_storage.dmi

### Resources/Textures/_RMC14/Objects/Tools/Toolboxes/toolbox_blue.rsi

- Metadata: `Resources/Textures/_RMC14/Objects/Tools/Toolboxes/toolbox_blue.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/items/storage.dmi , held sprites redone by Alekshhh, modified by Hyenh

### Resources/Textures/_RMC14/Objects/Tools/Toolboxes/toolbox_green.rsi

- Metadata: `Resources/Textures/_RMC14/Objects/Tools/Toolboxes/toolbox_green.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/items/storage/toolbox.dmi , held sprites redone by Alekshhh and re-colored by Dutch-VanDerLinde, modified by Hyenh

### Resources/Textures/_RMC14/Objects/Tools/Toolboxes/toolbox_red.rsi

- Metadata: `Resources/Textures/_RMC14/Objects/Tools/Toolboxes/toolbox_red.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/items/storage.dmi , held sprites redone by Alekshhh, modified by Hyenh

### Resources/Textures/_RMC14/Objects/Tools/Toolboxes/toolbox_yellow.rsi

- Metadata: `Resources/Textures/_RMC14/Objects/Tools/Toolboxes/toolbox_yellow.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/items/storage.dmi , held sprites redone by Alekshhh, modified by Hyenh

### Resources/Textures/_RMC14/Objects/Weapons/Melee/pickaxe.rsi

- Metadata: `Resources/Textures/_RMC14/Objects/Weapons/Melee/pickaxe.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cm-ss13 at commit https://github.com/cmss13-devs/cmss13/commit/794d6668d4d1d1f767241c70b2171cbe92215421

### Resources/Textures/_RMC14/Structures/objects.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/objects.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/b5a595ce32f9bb4fc8abde0efe21196b3862d8bf/icons/obj/objects.dmi
