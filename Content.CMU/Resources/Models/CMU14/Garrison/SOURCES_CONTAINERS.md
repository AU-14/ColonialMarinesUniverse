# Garrison freight containers

40 draft sections, 740 editable parts and 196 unique source crops. All remain draft.

Geometric contributions are CC0-1.0 to the extent separately licensable. Original pixels and derivative appearance retain the source attribution and licenses below. Crops preserve original pixel values; colors for unseen structural faces are sampled from the source palette.

## Reconstruction decisions

Large containers occupy the original one-by-two-tile section footprint; three adjacent sections form a long container. Compact crates occupy one-by-one-tile sections and join in pairs. Raised front corrugations use cropped source strips so graffiti and logos continue across the physical ridges. Separate roof projection prevents perspective artwork from becoming an upright billboard. Outer ends have structural frames; interior section seams do not acquire duplicate end posts.

Compact CMU Tartarus variants inherit the large base collider even though they use the same short art as RMC crates. Classic rows place them one tile apart and even mix CMU/RMC halves (#222 and #10198). The one-tile visual depth follows that evidence. Simulation collision and saved transforms are unchanged.

Heights, unseen end/rear surfaces, metal response and locking hardware depth remain inferred. The compact draft is .96 tiles tall; the larger draft is 2.02 tiles tall before raised trim. Lids do not open, containers do not acquire inventory functionality, and damage variants remain unfinished.

## Source licenses

### /Textures/CMU14/Structures/containers.rsi

- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/Steelpoint/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/containers/containersextended.dmi, https://github.com/Steelpoint/cmss13/blob/bf751971a956160f294b8f0e71addb109f0242be/icons/obj/structures/props/containers/contain.dmi

### /Textures/_RMC14/Structures/containers.rsi

- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/a69663ee8d9e3a980d486a1ee05efa44f79abc17/icons/obj/structures/props/contain.dmi and https://github.com/cmss13-devs/cmss13/blob/a69663ee8d9e3a980d486a1ee05efa44f79abc17/icons/obj/structures/props/containers/containersextended.dmi, modified by troytroy40 and noctyrnal on GitHub, AMS containers by SharkSnake98 on GitHub

## Context refinements and validation

The raised roof strips follow their actual source pixel columns, with exact crops on the raised surfaces. Compact locking straps retain their embossed source artwork. Frames, corrugations and hardware stay within the saved one- or two-tile depth. All 81 classic transforms remain unchanged, and conservative part-bound comparisons find zero intersections between different freight-container instances. Adjacent section seams have zero gap.

All 40 individual comparisons and 18 assembled groups were inspected, including the mixed CMU/RMC Kelland pair. Browser checks cover graffiti, Seegson storage, USCM and tightly packed compact rows. The container region exports 117 entities and 169 floors. Source crop and placement evidence is in `generated/containers-source-audit.json` and `containers-placement-audit.json`; joined comparisons and map captures are in `generated/review/containers-*.png`. All remain drafts; these checks do not establish animation or hidden-face fidelity.

## Crops

| Prototype | Source state | Role | Pixel crop |
| --- | --- | --- | --- |
| AU14ContainerBlueGraffitiLeft | grafcontain_l | Face0 | [1, 26, 4, 62] |
| AU14ContainerBlueGraffitiLeft | grafcontain_l | Face1 | [4, 26, 8, 62] |
| AU14ContainerBlueGraffitiLeft | grafcontain_l | Face2 | [8, 26, 12, 62] |
| AU14ContainerBlueGraffitiLeft | grafcontain_l | Face3 | [12, 26, 16, 62] |
| AU14ContainerBlueGraffitiLeft | grafcontain_l | Face4 | [16, 26, 20, 62] |
| AU14ContainerBlueGraffitiLeft | grafcontain_l | Face5 | [20, 26, 24, 62] |
| AU14ContainerBlueGraffitiLeft | grafcontain_l | Face6 | [24, 26, 28, 62] |
| AU14ContainerBlueGraffitiLeft | grafcontain_l | Face7 | [28, 26, 32, 62] |
| AU14ContainerBlueGraffitiLeft | grafcontain_l | Roof | [1, 2, 32, 26] |
| AU14ContainerBlueGraffitiMiddle | grafcontain_rm | Face0 | [0, 26, 4, 62] |
| AU14ContainerBlueGraffitiMiddle | grafcontain_rm | Face1 | [4, 26, 8, 62] |
| AU14ContainerBlueGraffitiMiddle | grafcontain_rm | Face2 | [8, 26, 12, 62] |
| AU14ContainerBlueGraffitiMiddle | grafcontain_rm | Face3 | [12, 26, 16, 62] |
| AU14ContainerBlueGraffitiMiddle | grafcontain_rm | Face4 | [16, 26, 20, 62] |
| AU14ContainerBlueGraffitiMiddle | grafcontain_rm | Face5 | [20, 26, 24, 62] |
| AU14ContainerBlueGraffitiMiddle | grafcontain_rm | Face6 | [24, 26, 28, 62] |
| AU14ContainerBlueGraffitiMiddle | grafcontain_rm | Face7 | [28, 26, 32, 62] |
| AU14ContainerBlueGraffitiMiddle | grafcontain_rm | Roof | [0, 2, 32, 26] |
| AU14ContainerBlueGraffitiRight | grafcontain_r | Face0 | [0, 26, 4, 62] |
| AU14ContainerBlueGraffitiRight | grafcontain_r | Face1 | [4, 26, 8, 62] |
| AU14ContainerBlueGraffitiRight | grafcontain_r | Face2 | [8, 26, 12, 62] |
| AU14ContainerBlueGraffitiRight | grafcontain_r | Face3 | [12, 26, 16, 62] |
| AU14ContainerBlueGraffitiRight | grafcontain_r | Face4 | [16, 26, 20, 62] |
| AU14ContainerBlueGraffitiRight | grafcontain_r | Face5 | [20, 26, 24, 62] |
| AU14ContainerBlueGraffitiRight | grafcontain_r | Face6 | [24, 26, 28, 62] |
| AU14ContainerBlueGraffitiRight | grafcontain_r | Face7 | [28, 26, 30, 62] |
| AU14ContainerBlueGraffitiRight | grafcontain_r | Roof | [0, 2, 30, 26] |
| AU14ContainerSeegsonLeft | seegson_l | Face0 | [1, 26, 4, 62] |
| AU14ContainerSeegsonLeft | seegson_l | Face1 | [4, 26, 8, 62] |
| AU14ContainerSeegsonLeft | seegson_l | Face2 | [8, 26, 12, 62] |
| AU14ContainerSeegsonLeft | seegson_l | Face3 | [12, 26, 16, 62] |
| AU14ContainerSeegsonLeft | seegson_l | Face4 | [16, 26, 20, 62] |
| AU14ContainerSeegsonLeft | seegson_l | Face5 | [20, 26, 24, 62] |
| AU14ContainerSeegsonLeft | seegson_l | Face6 | [24, 26, 28, 62] |
| AU14ContainerSeegsonLeft | seegson_l | Face7 | [28, 26, 32, 62] |
| AU14ContainerSeegsonLeft | seegson_l | Roof | [1, 2, 32, 26] |
| AU14ContainerSeegsonMiddle | seegson_m | Face0 | [0, 26, 4, 62] |
| AU14ContainerSeegsonMiddle | seegson_m | Face1 | [4, 26, 8, 62] |
| AU14ContainerSeegsonMiddle | seegson_m | Face2 | [8, 26, 12, 62] |
| AU14ContainerSeegsonMiddle | seegson_m | Face3 | [12, 26, 16, 62] |
| AU14ContainerSeegsonMiddle | seegson_m | Face4 | [16, 26, 20, 62] |
| AU14ContainerSeegsonMiddle | seegson_m | Face5 | [20, 26, 24, 62] |
| AU14ContainerSeegsonMiddle | seegson_m | Face6 | [24, 26, 28, 62] |
| AU14ContainerSeegsonMiddle | seegson_m | Face7 | [28, 26, 32, 62] |
| AU14ContainerSeegsonMiddle | seegson_m | Roof | [0, 2, 32, 26] |
| AU14ContainerSeegsonRight | seegson_r | Face0 | [0, 26, 4, 62] |
| AU14ContainerSeegsonRight | seegson_r | Face1 | [4, 26, 8, 62] |
| AU14ContainerSeegsonRight | seegson_r | Face2 | [8, 26, 12, 62] |
| AU14ContainerSeegsonRight | seegson_r | Face3 | [12, 26, 16, 62] |
| AU14ContainerSeegsonRight | seegson_r | Face4 | [16, 26, 20, 62] |
| AU14ContainerSeegsonRight | seegson_r | Face5 | [20, 26, 24, 62] |
| AU14ContainerSeegsonRight | seegson_r | Face6 | [24, 26, 28, 62] |
| AU14ContainerSeegsonRight | seegson_r | Face7 | [28, 26, 30, 62] |
| AU14ContainerSeegsonRight | seegson_r | Roof | [0, 2, 30, 26] |
| AU14ContainerTartarusLeftBlackWY | blackwyleft | Face0 | [0, 42, 32, 61] |
| AU14ContainerTartarusLeftBlackWY | blackwyleft | Roof | [0, 24, 32, 42] |
| AU14ContainerTartarusLeftBlueWYWings | bluewywingsleft | Face0 | [0, 42, 32, 61] |
| AU14ContainerTartarusLeftBlueWYWings | bluewywingsleft | Roof | [0, 24, 32, 42] |
| AU14ContainerTartarusLeftGrayWY | greywyleft | Face0 | [0, 42, 32, 61] |
| AU14ContainerTartarusLeftGrayWY | greywyleft | Roof | [0, 24, 32, 42] |
| AU14ContainerTartarusLeftKelland | kelland_alt_l | Face0 | [0, 42, 32, 61] |
| AU14ContainerTartarusLeftKelland | kelland_alt_l | Roof | [0, 24, 32, 42] |
| AU14ContainerTartarusLeftKellandAlt | kelland_l | Face0 | [0, 42, 32, 61] |
| AU14ContainerTartarusLeftKellandAlt | kelland_l | Roof | [0, 24, 32, 42] |
| AU14ContainerTartarusLeftLightGrayWY | lightgreywyleft | Face0 | [0, 42, 32, 61] |
| AU14ContainerTartarusLeftLightGrayWY | lightgreywyleft | Roof | [0, 24, 32, 42] |
| AU14ContainerTartarusLeftOffBlueWY | whitewyleft | Face0 | [0, 42, 32, 61] |
| AU14ContainerTartarusLeftOffBlueWY | whitewyleft | Roof | [0, 24, 32, 42] |
| AU14ContainerTartarusLeftTanWYWings | tanwywingsleft | Face0 | [0, 42, 32, 61] |
| AU14ContainerTartarusLeftTanWYWings | tanwywingsleft | Roof | [0, 24, 32, 42] |
| AU14ContainerTartarusRightBlackWY | blackwyright | Face0 | [0, 42, 32, 61] |
| AU14ContainerTartarusRightBlackWY | blackwyright | Roof | [0, 24, 32, 42] |
| AU14ContainerTartarusRightBlueWYWings | bluewywingsright | Face0 | [0, 42, 32, 61] |
| AU14ContainerTartarusRightBlueWYWings | bluewywingsright | Roof | [0, 24, 32, 42] |
| AU14ContainerTartarusRightGrayWY | greywyright | Face0 | [0, 42, 32, 61] |
| AU14ContainerTartarusRightGrayWY | greywyright | Roof | [0, 24, 32, 42] |
| AU14ContainerTartarusRightKelland | kelland_alt_r | Face0 | [0, 42, 32, 61] |
| AU14ContainerTartarusRightKelland | kelland_alt_r | Roof | [0, 24, 32, 42] |
| AU14ContainerTartarusRightLightGrayWY | lightgreywyright | Face0 | [0, 42, 32, 61] |
| AU14ContainerTartarusRightLightGrayWY | lightgreywyright | Roof | [0, 24, 32, 42] |
| AU14ContainerTartarusRightOffBlueWY | whitewyright | Face0 | [0, 42, 32, 61] |
| AU14ContainerTartarusRightOffBlueWY | whitewyright | Roof | [0, 24, 32, 42] |
| AU14ContainerTartarusRightTanWYWings | tanwywingsright | Face0 | [0, 42, 32, 61] |
| AU14ContainerTartarusRightTanWYWings | tanwywingsright | Roof | [0, 24, 32, 42] |
| AU14ContainerUSCMLeft | uscm4_l | Face0 | [1, 26, 4, 62] |
| AU14ContainerUSCMLeft | uscm4_l | Face1 | [4, 26, 8, 62] |
| AU14ContainerUSCMLeft | uscm4_l | Face2 | [8, 26, 12, 62] |
| AU14ContainerUSCMLeft | uscm4_l | Face3 | [12, 26, 16, 62] |
| AU14ContainerUSCMLeft | uscm4_l | Face4 | [16, 26, 20, 62] |
| AU14ContainerUSCMLeft | uscm4_l | Face5 | [20, 26, 24, 62] |
| AU14ContainerUSCMLeft | uscm4_l | Face6 | [24, 26, 28, 62] |
| AU14ContainerUSCMLeft | uscm4_l | Face7 | [28, 26, 32, 62] |
| AU14ContainerUSCMLeft | uscm4_l | Roof | [1, 2, 32, 26] |
| AU14ContainerUSCMMiddle | uscm4_m | Face0 | [0, 26, 4, 62] |
| AU14ContainerUSCMMiddle | uscm4_m | Face1 | [4, 26, 8, 62] |
| AU14ContainerUSCMMiddle | uscm4_m | Face2 | [8, 26, 12, 62] |
| AU14ContainerUSCMMiddle | uscm4_m | Face3 | [12, 26, 16, 62] |
| AU14ContainerUSCMMiddle | uscm4_m | Face4 | [16, 26, 20, 62] |
| AU14ContainerUSCMMiddle | uscm4_m | Face5 | [20, 26, 24, 62] |
| AU14ContainerUSCMMiddle | uscm4_m | Face6 | [24, 26, 28, 62] |
| AU14ContainerUSCMMiddle | uscm4_m | Face7 | [28, 26, 32, 62] |
| AU14ContainerUSCMMiddle | uscm4_m | Roof | [0, 2, 32, 26] |
| AU14ContainerUSCMRight | uscm_r | Face0 | [0, 26, 4, 62] |
| AU14ContainerUSCMRight | uscm_r | Face1 | [4, 26, 8, 62] |
| AU14ContainerUSCMRight | uscm_r | Face2 | [8, 26, 12, 62] |
| AU14ContainerUSCMRight | uscm_r | Face3 | [12, 26, 16, 62] |
| AU14ContainerUSCMRight | uscm_r | Face4 | [16, 26, 20, 62] |
| AU14ContainerUSCMRight | uscm_r | Face5 | [20, 26, 24, 62] |
| AU14ContainerUSCMRight | uscm_r | Face6 | [24, 26, 28, 62] |
| AU14ContainerUSCMRight | uscm_r | Face7 | [28, 26, 30, 62] |
| AU14ContainerUSCMRight | uscm_r | Roof | [0, 2, 30, 26] |
| AU14ContainerWYLeft | wy_l | Face0 | [1, 26, 4, 62] |
| AU14ContainerWYLeft | wy_l | Face1 | [4, 26, 8, 62] |
| AU14ContainerWYLeft | wy_l | Face2 | [8, 26, 12, 62] |
| AU14ContainerWYLeft | wy_l | Face3 | [12, 26, 16, 62] |
| AU14ContainerWYLeft | wy_l | Face4 | [16, 26, 20, 62] |
| AU14ContainerWYLeft | wy_l | Face5 | [20, 26, 24, 62] |
| AU14ContainerWYLeft | wy_l | Face6 | [24, 26, 28, 62] |
| AU14ContainerWYLeft | wy_l | Face7 | [28, 26, 32, 62] |
| AU14ContainerWYLeft | wy_l | Roof | [1, 2, 32, 26] |
| AU14ContainerWYMiddle | wy_m | Face0 | [0, 26, 4, 62] |
| AU14ContainerWYMiddle | wy_m | Face1 | [4, 26, 8, 62] |
| AU14ContainerWYMiddle | wy_m | Face2 | [8, 26, 12, 62] |
| AU14ContainerWYMiddle | wy_m | Face3 | [12, 26, 16, 62] |
| AU14ContainerWYMiddle | wy_m | Face4 | [16, 26, 20, 62] |
| AU14ContainerWYMiddle | wy_m | Face5 | [20, 26, 24, 62] |
| AU14ContainerWYMiddle | wy_m | Face6 | [24, 26, 28, 62] |
| AU14ContainerWYMiddle | wy_m | Face7 | [28, 26, 32, 62] |
| AU14ContainerWYMiddle | wy_m | Roof | [0, 2, 32, 26] |
| AU14ContainerWYRight | wy_r | Face0 | [0, 26, 4, 62] |
| AU14ContainerWYRight | wy_r | Face1 | [4, 26, 8, 62] |
| AU14ContainerWYRight | wy_r | Face2 | [8, 26, 12, 62] |
| AU14ContainerWYRight | wy_r | Face3 | [12, 26, 16, 62] |
| AU14ContainerWYRight | wy_r | Face4 | [16, 26, 20, 62] |
| AU14ContainerWYRight | wy_r | Face5 | [20, 26, 24, 62] |
| AU14ContainerWYRight | wy_r | Face6 | [24, 26, 28, 62] |
| AU14ContainerWYRight | wy_r | Face7 | [28, 26, 30, 62] |
| AU14ContainerWYRight | wy_r | Roof | [0, 2, 30, 26] |
| RMCContainerBlueLeft | bluecontainerleft | Face0 | [1, 26, 4, 62] |
| RMCContainerBlueLeft | bluecontainerleft | Face1 | [4, 26, 8, 62] |
| RMCContainerBlueLeft | bluecontainerleft | Face2 | [8, 26, 12, 62] |
| RMCContainerBlueLeft | bluecontainerleft | Face3 | [12, 26, 16, 62] |
| RMCContainerBlueLeft | bluecontainerleft | Face4 | [16, 26, 20, 62] |
| RMCContainerBlueLeft | bluecontainerleft | Face5 | [20, 26, 24, 62] |
| RMCContainerBlueLeft | bluecontainerleft | Face6 | [24, 26, 28, 62] |
| RMCContainerBlueLeft | bluecontainerleft | Face7 | [28, 26, 32, 62] |
| RMCContainerBlueLeft | bluecontainerleft | Roof | [1, 2, 32, 26] |
| RMCContainerBlueMiddle | bluecontainermiddle | Face0 | [0, 26, 4, 62] |
| RMCContainerBlueMiddle | bluecontainermiddle | Face1 | [4, 26, 8, 62] |
| RMCContainerBlueMiddle | bluecontainermiddle | Face2 | [8, 26, 12, 62] |
| RMCContainerBlueMiddle | bluecontainermiddle | Face3 | [12, 26, 16, 62] |
| RMCContainerBlueMiddle | bluecontainermiddle | Face4 | [16, 26, 20, 62] |
| RMCContainerBlueMiddle | bluecontainermiddle | Face5 | [20, 26, 24, 62] |
| RMCContainerBlueMiddle | bluecontainermiddle | Face6 | [24, 26, 28, 62] |
| RMCContainerBlueMiddle | bluecontainermiddle | Face7 | [28, 26, 32, 62] |
| RMCContainerBlueMiddle | bluecontainermiddle | Roof | [0, 2, 32, 26] |
| RMCContainerBlueRight | bluecontainerright | Face0 | [0, 26, 4, 62] |
| RMCContainerBlueRight | bluecontainerright | Face1 | [4, 26, 8, 62] |
| RMCContainerBlueRight | bluecontainerright | Face2 | [8, 26, 12, 62] |
| RMCContainerBlueRight | bluecontainerright | Face3 | [12, 26, 16, 62] |
| RMCContainerBlueRight | bluecontainerright | Face4 | [16, 26, 20, 62] |
| RMCContainerBlueRight | bluecontainerright | Face5 | [20, 26, 24, 62] |
| RMCContainerBlueRight | bluecontainerright | Face6 | [24, 26, 28, 62] |
| RMCContainerBlueRight | bluecontainerright | Face7 | [28, 26, 30, 62] |
| RMCContainerBlueRight | bluecontainerright | Roof | [0, 2, 30, 26] |
| RMCContainerShortBlueLeft | blueleft | Face0 | [0, 42, 32, 61] |
| RMCContainerShortBlueLeft | blueleft | Roof | [0, 24, 32, 42] |
| RMCContainerShortBlueRight | blueright | Face0 | [0, 42, 32, 61] |
| RMCContainerShortBlueRight | blueright | Roof | [0, 24, 32, 42] |
| RMCContainerShortGreenLeft | greenleft | Face0 | [0, 42, 32, 61] |
| RMCContainerShortGreenLeft | greenleft | Roof | [0, 24, 32, 42] |
| RMCContainerShortGreenRight | greenright | Face0 | [0, 42, 32, 61] |
| RMCContainerShortGreenRight | greenright | Roof | [0, 24, 32, 42] |
| RMCContainerShortRedLeft | redleft | Face0 | [0, 42, 32, 61] |
| RMCContainerShortRedLeft | redleft | Roof | [0, 24, 32, 42] |
| RMCContainerShortRedRight | redright | Face0 | [0, 42, 32, 61] |
| RMCContainerShortRedRight | redright | Roof | [0, 24, 32, 42] |
| RMCContainerShortTanLeft | tanleft | Face0 | [0, 42, 32, 61] |
| RMCContainerShortTanLeft | tanleft | Roof | [0, 24, 32, 42] |
| RMCContainerShortTanRight | tanright | Face0 | [0, 42, 32, 61] |
| RMCContainerShortTanRight | tanright | Roof | [0, 24, 32, 42] |
| RMCContainerTartarusLeft | tartarus_l | Face0 | [0, 42, 32, 61] |
| RMCContainerTartarusLeft | tartarus_l | Roof | [0, 24, 32, 42] |
| RMCContainerTartarusRight | tartarus_r | Face0 | [0, 42, 32, 61] |
| RMCContainerTartarusRight | tartarus_r | Roof | [0, 24, 32, 42] |
| AU14ContainerBlueGraffitiLeft | grafcontain_l | Ridge0 | [2, 4, 5, 24] |
| AU14ContainerBlueGraffitiLeft | grafcontain_l | Ridge1 | [10, 4, 13, 24] |
| AU14ContainerBlueGraffitiLeft | grafcontain_l | Ridge2 | [18, 4, 21, 24] |
| AU14ContainerBlueGraffitiLeft | grafcontain_l | Ridge3 | [26, 4, 29, 24] |
| AU14ContainerBlueGraffitiMiddle | grafcontain_rm | Ridge0 | [2, 4, 5, 24] |
| AU14ContainerBlueGraffitiMiddle | grafcontain_rm | Ridge1 | [10, 4, 13, 24] |
| AU14ContainerBlueGraffitiMiddle | grafcontain_rm | Ridge2 | [18, 4, 21, 24] |
| AU14ContainerBlueGraffitiMiddle | grafcontain_rm | Ridge3 | [26, 4, 29, 24] |
| AU14ContainerBlueGraffitiRight | grafcontain_r | Ridge0 | [2, 4, 5, 24] |
| AU14ContainerBlueGraffitiRight | grafcontain_r | Ridge1 | [10, 4, 13, 24] |
| AU14ContainerBlueGraffitiRight | grafcontain_r | Ridge2 | [18, 4, 21, 24] |
| AU14ContainerBlueGraffitiRight | grafcontain_r | Ridge3 | [26, 4, 29, 24] |
| AU14ContainerSeegsonLeft | seegson_l | Ridge0 | [2, 4, 5, 24] |
| AU14ContainerSeegsonLeft | seegson_l | Ridge1 | [10, 4, 13, 24] |
| AU14ContainerSeegsonLeft | seegson_l | Ridge2 | [18, 4, 21, 24] |
| AU14ContainerSeegsonLeft | seegson_l | Ridge3 | [26, 4, 29, 24] |
| AU14ContainerSeegsonMiddle | seegson_m | Ridge0 | [2, 4, 5, 24] |
| AU14ContainerSeegsonMiddle | seegson_m | Ridge1 | [10, 4, 13, 24] |
| AU14ContainerSeegsonMiddle | seegson_m | Ridge2 | [18, 4, 21, 24] |
| AU14ContainerSeegsonMiddle | seegson_m | Ridge3 | [26, 4, 29, 24] |
| AU14ContainerSeegsonRight | seegson_r | Ridge0 | [2, 4, 5, 24] |
| AU14ContainerSeegsonRight | seegson_r | Ridge1 | [10, 4, 13, 24] |
| AU14ContainerSeegsonRight | seegson_r | Ridge2 | [18, 4, 21, 24] |
| AU14ContainerSeegsonRight | seegson_r | Ridge3 | [26, 4, 29, 24] |
| AU14ContainerTartarusLeftBlackWY | blackwyleft | Strap | [0, 24, 7, 42] |
| AU14ContainerTartarusLeftBlueWYWings | bluewywingsleft | Strap | [0, 24, 7, 42] |
| AU14ContainerTartarusLeftGrayWY | greywyleft | Strap | [0, 24, 7, 42] |
| AU14ContainerTartarusLeftKelland | kelland_alt_l | Strap | [0, 24, 7, 42] |
| AU14ContainerTartarusLeftKellandAlt | kelland_l | Strap | [0, 24, 7, 42] |
| AU14ContainerTartarusLeftLightGrayWY | lightgreywyleft | Strap | [0, 24, 7, 42] |
| AU14ContainerTartarusLeftOffBlueWY | whitewyleft | Strap | [0, 24, 7, 42] |
| AU14ContainerTartarusLeftTanWYWings | tanwywingsleft | Strap | [0, 24, 7, 42] |
| AU14ContainerTartarusRightBlackWY | blackwyright | Strap | [25, 24, 32, 42] |
| AU14ContainerTartarusRightBlueWYWings | bluewywingsright | Strap | [25, 24, 32, 42] |
| AU14ContainerTartarusRightGrayWY | greywyright | Strap | [25, 24, 32, 42] |
| AU14ContainerTartarusRightKelland | kelland_alt_r | Strap | [25, 24, 32, 42] |
| AU14ContainerTartarusRightLightGrayWY | lightgreywyright | Strap | [25, 24, 32, 42] |
| AU14ContainerTartarusRightOffBlueWY | whitewyright | Strap | [25, 24, 32, 42] |
| AU14ContainerTartarusRightTanWYWings | tanwywingsright | Strap | [25, 24, 32, 42] |
| AU14ContainerUSCMLeft | uscm4_l | Ridge0 | [2, 4, 5, 24] |
| AU14ContainerUSCMLeft | uscm4_l | Ridge1 | [10, 4, 13, 24] |
| AU14ContainerUSCMLeft | uscm4_l | Ridge2 | [18, 4, 21, 24] |
| AU14ContainerUSCMLeft | uscm4_l | Ridge3 | [26, 4, 29, 24] |
| AU14ContainerUSCMMiddle | uscm4_m | Ridge0 | [2, 4, 5, 24] |
| AU14ContainerUSCMMiddle | uscm4_m | Ridge1 | [10, 4, 13, 24] |
| AU14ContainerUSCMMiddle | uscm4_m | Ridge2 | [18, 4, 21, 24] |
| AU14ContainerUSCMMiddle | uscm4_m | Ridge3 | [26, 4, 29, 24] |
| AU14ContainerUSCMRight | uscm_r | Ridge0 | [2, 4, 5, 24] |
| AU14ContainerUSCMRight | uscm_r | Ridge1 | [10, 4, 13, 24] |
| AU14ContainerUSCMRight | uscm_r | Ridge2 | [18, 4, 21, 24] |
| AU14ContainerUSCMRight | uscm_r | Ridge3 | [26, 4, 29, 24] |
| AU14ContainerWYLeft | wy_l | Ridge0 | [2, 4, 5, 24] |
| AU14ContainerWYLeft | wy_l | Ridge1 | [10, 4, 13, 24] |
| AU14ContainerWYLeft | wy_l | Ridge2 | [18, 4, 21, 24] |
| AU14ContainerWYLeft | wy_l | Ridge3 | [26, 4, 29, 24] |
| AU14ContainerWYMiddle | wy_m | Ridge0 | [2, 4, 5, 24] |
| AU14ContainerWYMiddle | wy_m | Ridge1 | [10, 4, 13, 24] |
| AU14ContainerWYMiddle | wy_m | Ridge2 | [18, 4, 21, 24] |
| AU14ContainerWYMiddle | wy_m | Ridge3 | [26, 4, 29, 24] |
| AU14ContainerWYRight | wy_r | Ridge0 | [2, 4, 5, 24] |
| AU14ContainerWYRight | wy_r | Ridge1 | [10, 4, 13, 24] |
| AU14ContainerWYRight | wy_r | Ridge2 | [18, 4, 21, 24] |
| AU14ContainerWYRight | wy_r | Ridge3 | [26, 4, 29, 24] |
| RMCContainerBlueLeft | bluecontainerleft | Ridge0 | [2, 4, 5, 24] |
| RMCContainerBlueLeft | bluecontainerleft | Ridge1 | [10, 4, 13, 24] |
| RMCContainerBlueLeft | bluecontainerleft | Ridge2 | [18, 4, 21, 24] |
| RMCContainerBlueLeft | bluecontainerleft | Ridge3 | [26, 4, 29, 24] |
| RMCContainerBlueMiddle | bluecontainermiddle | Ridge0 | [2, 4, 5, 24] |
| RMCContainerBlueMiddle | bluecontainermiddle | Ridge1 | [10, 4, 13, 24] |
| RMCContainerBlueMiddle | bluecontainermiddle | Ridge2 | [18, 4, 21, 24] |
| RMCContainerBlueMiddle | bluecontainermiddle | Ridge3 | [26, 4, 29, 24] |
| RMCContainerBlueRight | bluecontainerright | Ridge0 | [2, 4, 5, 24] |
| RMCContainerBlueRight | bluecontainerright | Ridge1 | [10, 4, 13, 24] |
| RMCContainerBlueRight | bluecontainerright | Ridge2 | [18, 4, 21, 24] |
| RMCContainerBlueRight | bluecontainerright | Ridge3 | [26, 4, 29, 24] |
| RMCContainerShortBlueLeft | blueleft | Strap | [0, 24, 7, 42] |
| RMCContainerShortBlueRight | blueright | Strap | [25, 24, 32, 42] |
| RMCContainerShortGreenLeft | greenleft | Strap | [0, 24, 7, 42] |
| RMCContainerShortGreenRight | greenright | Strap | [25, 24, 32, 42] |
| RMCContainerShortRedLeft | redleft | Strap | [0, 24, 7, 42] |
| RMCContainerShortRedRight | redright | Strap | [25, 24, 32, 42] |
| RMCContainerShortTanLeft | tanleft | Strap | [0, 24, 7, 42] |
| RMCContainerShortTanRight | tanright | Strap | [25, 24, 32, 42] |
| RMCContainerTartarusLeft | tartarus_l | Strap | [0, 24, 7, 42] |
| RMCContainerTartarusRight | tartarus_r | Strap | [25, 24, 32, 42] |
