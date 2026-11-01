# Source-inspected hand tools and small devices: cloud art batch

All assemblies are drafts. This is physical asset work only: no game launch, native rendering acceptance, saved-map fit, live interaction, full-state completion or fidelity approval. No engine/runtime code or source gameplay prototypes were changed. Local source prototype/RSI files are fetched reference copies.

## Deliverable

18 direct, unchanged-exporter GLBs cover 17 assigned exact draft prototype IDs. One extra open executive-lighter construction study has no sourcePrototypes mapping. Editable canonical files are `garrison_hand_tools_cloud.yml` and `garrison_hand_tools_cloud_art.yml`. Eight exact, unresampled source detail crops use the first eight centrally allocated hand_tools slots, 1452 through 1459, in the given list order. The authoring script reads the allocation list directly, including its gaps; no high-slot constant or inferred range is used.

The models use separate physical tool shafts, fork heads, grips, forged cheeks, rivets, bucket/container walls, rims, lids, handles, housings, antennae, controls, spool rings and cassette shell members. Graphic crops are restricted to the janitorial marking, radio screen frames and cassette label strips. There are no full-sprite billboard or sprite-slab substitutes.

## Source and state decisions

- Crowbar: the actual bent steel icon determines the shaft and working ends. The maintenance jack is the source dual-ended fork tool, not a name-inferred hydraulic jack. Shovel uses the source straight handle, ferrule and formed spade; no D-grip or fold is invented. Pruning clippers retain the green/yellow two-handle silhouette, bypass blades and pivot
- Screwdriver and wirecutters: both real visible layers are composed in their review panels. The source RandomSprite Rainbow palette changes only the handle layer. These neutral-gray handle studies do not claim any saved or live randomized color and intentionally have no single-layer adapter. The precomposed blue/orange map icons are inventoried but are not silently substituted for the runtime layered source
- Welder: unlit world icon only. The source on state changes the top cap and includes two 0.5/0.3-second flame frames; it is not supported here. Closed body, nozzle mouth, ignition lever, finger ridges and cap hinge are physical geometry
- Green toolbox: inherited ToolboxBase changes the one visible base layer between icon and icon-open. Both static states use the existing spriteStates adapter, with a hollow shell, separate latches and hinge barrels. The closed pose has a through-open carry handle. Contents and opening animation remain unsupported
- Executive lighter: the source closed/open/top/flame layers are distinct. Closed default has a single-state adapter; the separate unlit open study is unbound. Ignition, flame, lighting and helmet/held appearances remain unsupported
- Cable: the exact default coil3 layer has #FF0000 tint. The rendered coil and reference apply that tint; no wooden spool is invented. The geometry has wound rings, a real center opening and a separate lead. Stack-controlled coil1/coil2 and arbitrary recolor remain unsupported. A layer tint is not misrepresented as global Sprite tint
- Buckets: empty shells have real interiors and apertures. Three solution levels/colors and unused lid art remain explicitly unsupported. The janitorial warning is an exact source marking crop
- Approval and denial stamps: source green/red identification is retained on separate platens, collars and handles. No readable lettering is invented from the low-resolution source. Paper stamping is a separate effect
- Colony radio Off: this child disables RadioMicrophone and RadioSpeaker but still inherits the same three-frame walkietalkie icon. The existing source-clock adapter and one GLB clip retain the actual 0.2/0.4/0.2-second sequence using exact original screen crops. This is not a frequency, sound, powered/charged or UI implementation
- Rising Sun cassette: framed shell, small open reel bearings, teeth, tape bridge and four original red/white label strips. Cassette player: separate door frame, bay, visible reels, hinge spine and side controls. Playback, empty/playing overlays, six colored player case states, audio, UI and worn/headset effects remain unsupported

14 assemblies declare bounded single-visible-layer entries. The radio adds one source-timed clip. The toolbox has two stable geometry states. Those adapters only author the listed source cases; this is not complete gameplay support.

## Review and checks

`Tools/three_d/generated/cloud-review/hand-tools/hand-tools-overview.png` gathers all 18 comparisons. Each individual source-and-orbit sheet includes actual source pixels, a source-facing physical study and two independent orbits. Panels are individually normalized for legibility, not an exact-scale silhouette similarity test. Neutral handle panels compose both layers; the cable reference includes the actual red source-layer tint.

`hand-tools-supported-state-study.png` shows the hollow toolbox closed/open construction and original radio sequence. `hand-tools-source-defaults.json` resolves all 17 assigned inheritance chains from actual fetched prototypes, including BaseItem noRot=false, ToolboxBase, the lighter ancestors, source Stack/fill/RandomSprite owners and radio Off components.

Bounded checks in `hand-tools-proof.json`:
- 18 exported GLBs are byte-identical to unchanged build_models.glb_bytes output; GLB framing, finite accessor values and declared min/max bounds are checked
- 78 source inputs match their connector Git blob SHAs; eight detail crops match the original frame rectangles byte-for-byte after RGBA decoding
- 17 exact mappings, unique model IDs, unique atlas slots, centrally allocated index order, source directions and source frame timing are checked
- 19 local geometric point probes check the crowbar hook, both maintenance jaws, handle gaps, nozzle mouth, bucket interiors, toolbox cavity/lid, coil center and cassette reel holes. These are bounded point checks, not collision or physical contact proofs
- Blender 4.3.2 independently imports all 18 final GLBs with finite vertices and zero mesh repairs; the source radio clip is present

No Khronos glTF-validator run is asserted by this family report unless the parent aggregate supplies its separate result. No runtime or map placements were loaded, corrected or claimed.

The existing inventory reports 17 assigned Redux types. Historical occurrence counts are reference metadata, not validated visible placements or coverage of runtime-spawned objects.

## Reproduce

Run, from the workspace root:

1. `python Tools/three_d/author_hand_tools_cloud.py`
2. `python Tools/three_d/verify_hand_tools_cloud.py`
3. `blender --background --python Tools/three_d/check_hand_tools_blender.py`

Keep canonical source attribution, original meta.json files and source crops with the GLBs. Details below quote the source-owner metadata exactly. The derived source graphics retain their original CC-BY-SA-3.0 terms. Original unseen backs, thicknesses, curved-section choices and support poses remain inferences.

## Actual source attribution

Repository: TheHellFireo/CMU-Garrison-3D, ref Chip/garrison-3d. Connector receipts are in hand-tools-source-receipts.json. Source owner paths and metadata follow.

### Resources/Textures/_RMC14/Objects/Tools/crowbar.rsi/meta.json

[Verified source metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Tools/crowbar.rsi/meta.json)

License: CC-BY-SA-3.0

Copyright: equipped-BELT Taken from tgstation at https://github.com/tgstation/tgstation/blob/eea0599511b088fdab9d43e562210cdbd51c6a98/icons/obj/tools.dmi. Everything else Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/c8c7cb927b013398cb4af9794783e964107513f5/icons/obj/items/items.dmi.

### Resources/Textures/_RMC14/Objects/Tools/welder.rsi/meta.json

[Verified source metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Tools/welder.rsi/meta.json)

License: CC-BY-SA-3.0

Copyright: equipped-BELT, taken from tgstation at https://github.com/tgstation/tgstation/blob/eea0599511b088fdab9d43e562210cdbd51c6a98/icons/obj/tools.dmi. Everything else Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/6791699de749afc1c62bc65ec6f2326a00b3bb61/icons/obj/items/items.dmi, https://github.com/cmss13-devs/cmss13/blob/bec6653d487a49aa2b5a8e0c97bed9612f620211/icons/mob/humans/onmob/items_lefthand_0.dmi, and https://github.com/cmss13-devs/cmss13/blob/bec6653d487a49aa2b5a8e0c97bed9612f620211/icons/mob/humans/onmob/items_righthand_0.dmi

### Resources/Textures/_RMC14/Objects/Tools/screwdriver.rsi/meta.json

[Verified source metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Tools/screwdriver.rsi/meta.json)

License: CC-BY-SA-3.0

Copyright: equipped-BELT Taken from tgstation at https://github.com/tgstation/tgstation/blob/eea0599511b088fdab9d43e562210cdbd51c6a98/icons/obj/tools.dmi. Everything else Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/c8c7cb927b013398cb4af9794783e964107513f5/icons/obj/items/items.dmi.

### Resources/Textures/_RMC14/Objects/Tools/wirecutters.rsi/meta.json

[Verified source metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Tools/wirecutters.rsi/meta.json)

License: CC-BY-SA-3.0

Copyright: equipped-BELT, handle-inhand-right, handle-inhand-left, cutters-inhand-right,cutters-inhand-left Taken from tgstation at https://github.com/tgstation/tgstation/blob/eea0599511b088fdab9d43e562210cdbd51c6a98/icons/obj/tools.dmi. Everything else Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/c8c7cb927b013398cb4af9794783e964107513f5/icons/obj/items/items.dmi.

### Resources/Textures/_RMC14/Objects/Tools/shovel.rsi/meta.json

[Verified source metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Tools/shovel.rsi/meta.json)

License: CC-BY-SA-3.0

Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/31b765e01f85e936b0124a0e82678737028e6649/icons/mob/humans/onmob/inhands/equipment/tools_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/31b765e01f85e936b0124a0e82678737028e6649/icons/mob/humans/onmob/inhands/equipment/tools_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/31b765e01f85e936b0124a0e82678737028e6649/icons/obj/items/tools.dmi

### Resources/Textures/_RMC14/Objects/Tools/maintenance_jack.rsi/meta.json

[Verified source metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Tools/maintenance_jack.rsi/meta.json)

License: CC-BY-SA-3.0

Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/obj/items/tools.dmi, https://github.com/cmss13-devs/cmss13/blob/678d63ad96b75b1ac639436b2a9fdd7bd8009b70/icons/mob/humans/onmob/inhands/equipment/tools_righthand.dmi, https://github.com/cmss13-devs/cmss13/blob/678d63ad96b75b1ac639436b2a9fdd7bd8009b70/icons/mob/humans/onmob/inhands/equipment/tools_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/clothing/back/misc.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/clothing/suit_storage/tools.dmi

### Resources/Textures/_RMC14/Objects/Tools/Toolboxes/toolbox_green.rsi/meta.json

[Verified source metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Tools/Toolboxes/toolbox_green.rsi/meta.json)

License: CC-BY-SA-3.0

Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/items/storage/toolbox.dmi , held sprites redone by Alekshhh and re-colored by Dutch-VanDerLinde, modified by Hyenh

### Resources/Textures/_RMC14/Objects/Tools/Lighters/execzippo.rsi/meta.json

[Verified source metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Tools/Lighters/execzippo.rsi/meta.json)

License: CC-BY-SA-3.0

Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/items/items.dmi

### Resources/Textures/Objects/Tools/Hydroponics/clippers.rsi/meta.json

[Verified source metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/Objects/Tools/Hydroponics/clippers.rsi/meta.json)

License: CC-BY-SA-3.0

Copyright: Taken from Aurorastation at commit https://github.com/Aurorastation/Aurora.3/commit/3160508c1a9f367be0ab054cceaf2e36c0b66250

### Resources/Textures/_RMC14/Objects/Power/coil.rsi/meta.json

[Verified source metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Power/coil.rsi/meta.json)

License: CC-BY-SA-3.0

Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/71d46ee8057d19b12e1495419cb299d2fedef6cc/icons/obj/structures/machinery/power.dmi, https://github.com/cmss13-devs/cmss13/blob/7bf8ce23bdf2dec7429ee4ae9c6e350d6cdb9f00/icons/mob/humans/onmob/items_lefthand_0.dmi, https://github.com/cmss13-devs/cmss13/blob/7bf8ce23bdf2dec7429ee4ae9c6e350d6cdb9f00/icons/mob/humans/onmob/items_righthand_0.dmi

### Resources/Textures/_RMC14/Objects/Misc/Janitorial/janibucket.rsi/meta.json

[Verified source metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Misc/Janitorial/janibucket.rsi/meta.json)

License: CC-BY-SA-3.0

Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/106c92cdf232ebc12c9d7a2feb23956c6755496f/icons/obj/janitor.dmi, https://github.com/cmss13-devs/cmss13/blob/ca94d2e8715b73103fa9f213be53d343359b4107/icons/obj/items/reagentfillings.dmi

### Resources/Textures/_RMC14/Objects/Misc/Janitorial/bucket.rsi/meta.json

[Verified source metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Misc/Janitorial/bucket.rsi/meta.json)

License: CC-BY-SA-3.0

Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/106c92cdf232ebc12c9d7a2feb23956c6755496f/icons/obj/janitor.dmi, https://github.com/cmss13-devs/cmss13/blob/ca94d2e8715b73103fa9f213be53d343359b4107/icons/obj/items/reagentfillings.dmi, https://github.com/cmss13-devs/cmss13/blob/bec6653d487a49aa2b5a8e0c97bed9612f620211/icons/mob/humans/onmob/items_righthand_0.dmi, https://github.com/cmss13-devs/cmss13/blob/bec6653d487a49aa2b5a8e0c97bed9612f620211/icons/mob/humans/onmob/items_lefthand_0.dmi

### Resources/Textures/_RMC14/Objects/Misc/paper.rsi/meta.json

[Verified source metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Misc/paper.rsi/meta.json)

License: CC-BY-SA-3.0

Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/9ab207cd7ffba86a0411d7058645fb8f2a7895f3/icons/obj/items/paper.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/equipment/paperwork_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/equipment/paperwork_righthand.dmi, paper_stamp-provost, paper_stamp-provost-inspector, stamp-provost, paper_stamp-sea, and stamp-sea made by pursuitinashes (discord) based off of paper_stamp-marine and stamp-deny. paper_stamp-clf, paper_stamp-spp, paper_stamp-tse, and paper_stamp-free-press created by crazy1112345 (discord). stamp-clf, stamp-spp, stamp-tse, and stamp-free-press created by crazy1112345, based on stamp-marine. weya_pen made by SharkSnake98. Standard paper stamp overlays taken from tgstation at https://github.com/tgstation/tgstation/commit/e1142f20f5e4661cb6845cfcf2dd69f864d67432, with paper_stamp-syndicate by Veritius, paper_stamp-greytide by ubaser, paper_stamp-psychologist by clinux, and paper_stamp-wizard by brassicaprime69 (Discord), paper_stamp-cca by Oslo, https://github.com/cmss13-devs/cmss13/blob/52681260f1021befe2cdce04d8e21deee78e8b07/icons/obj/items/paper.dmi

### Resources/Textures/_RMC14/Objects/Devices/radio.rsi/meta.json

[Verified source metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Devices/radio.rsi/meta.json)

License: CC-BY-SA-3.0

Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/a767d448e1a73e7f96dea6bebc07deee8d54bdc2/icons/obj/items/radio.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/equipment/devices_lefthand.dmi, https://github.com/cmss13-devs/cmss13/blob/8d3eabd2575fcb859ab4320a298cdbc185910f6f/icons/mob/humans/onmob/inhands/equipment/devices_righthand.dmi

### Resources/Textures/_RMC14/Objects/Devices/cassette_player.rsi/meta.json

[Verified source metadata](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/Chip/garrison-3d/Resources/Textures/_RMC14/Objects/Devices/cassette_player.rsi/meta.json)

License: CC-BY-SA-3.0

Copyright: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/2314793744d0b4ae00e330a9c9fdb7662e67b2de/icons/obj/items/walkman.dmi, https://github.com/cmss13-devs/cmss13/blob/925b704ac6622abaa972fd2a1c8d6348340bbb51/icons/mob/humans/onmob/clothing/ears.dmi
