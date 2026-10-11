# Garrison environment draft assets

This batch adds 40 manually authored environment models, 695 colored cuboid parts and 48 exact saved-map prototype references. Every model is **draft**, and matching a prototype is a coverage reference rather than a completion claim.

## Coordinate and state contract

- X/Y horizontal, Z up, one tile = one unit. Ground is Z=0; default front faces -Y.
- Wall and rock cores occupy a full tile. Their elevations are inferred from top-down connected sprite art and must be reviewed in adjacent runs.
- Windows and fences are centered across the X axis. Correct tile-edge placement and map rotation require scene validation.
- Platform models contain only their exposed edge fascia; they do not fill an entire tile with an invented floor. Corner variants are separate exact mappings.
- Wall lights mount on the local +Y edge at approximately 2.3 units. Colored lenses do not emit light by themselves.
- The six-step staircase is decorative geometry; it does not add multi-Z navigation.
- Glass uses RGBA alpha in its material. Blending/sorting behavior and physical glass appearance are not validated in the game.
- Vines represent a default low mat; random sprite variants and placement over objects or walls are unresolved.
- Open and closed shutters are separate exact mappings. The open prototype has an explicit Door state even though its base Sprite references closed.

## Authorship and licenses

All cuboid coordinates and assemblies in this batch were newly authored with Codex assistance after inspecting the actual source sprite images. No source sprite pixels or existing 3D meshes are embedded. Newly authored geometry contributions are dedicated under **CC0-1.0**, to the extent those contributions can be independently licensed. Upstream visual designs and derivative appearance rights retain their existing terms; this does not relicense the referenced sprites. Preserve applicable attribution and share-alike requirements when distributing derived assets. The unmodified RSI metadata files below remain authoritative.

[CC0-1.0](https://creativecommons.org/publicdomain/zero/1.0/) and [CC-BY-SA-3.0](https://creativecommons.org/licenses/by-sa/3.0/) license references.

## Models and references

| Model | Exact prototype IDs | Source RSI metadata |
| --- | --- | --- |
| CMU3DKutjevoRock | RMCWallKutjevoRock | `Resources/Textures/_RMC14/Structures/Walls/kutjevo_rock.rsi/meta.json` |
| CMU3DKutjevoRockBorder | RMCWallKutjevoRockBorder | `Resources/Textures/_RMC14/Structures/Walls/kutjevo_rock.rsi/meta.json` |
| CMU3DHybrisaRock | RMCWallHybrisaRock | `Resources/Textures/_RMC14/Structures/Walls/hybrisa_rock.rsi/meta.json` |
| CMU3DSteelOreRock | CMUWallMineableLV376Steel | `Content.CMU/Resources/Textures/CMU14/Structures/Walls/steelore.rsi/meta.json` |
| CMU3DShuttleOrangeWall | CMWallShuttleOrange | `Resources/Textures/_RMC14/Structures/Walls/shuttle.rsi/meta.json` |
| CMU3DPrisonHullWall | RMCWallPrisonHull | `Resources/Textures/_RMC14/Structures/Walls/prison_rwall.rsi/meta.json` |
| CMU3DSPPGreyWall | RMCWallSPPGreyReinforced | `Resources/Textures/_RMC14/Structures/Walls/spp_grey_wall.rsi/meta.json` |
| CMU3DHybrisaWindow | RMCWindowHybrisaReinforced, RMCWindowHybrisaHull | `Resources/Textures/_RMC14/Structures/Windows/hybrisa_window.rsi/meta.json` |
| CMU3DPrisonWindow | RMCWindowPrisonReinforced, RMCWindowPrisonHull | `Resources/Textures/_RMC14/Structures/Windows/prison_rwindow.rsi/meta.json` |
| CMU3DDirectionalWindow | CMWindowDirectional, RMCWindowDirectionalAltDrawDepth | `Resources/Textures/_RMC14/Structures/Windows/directional.rsi/meta.json` |
| CMU3DDirectionalReinforcedWindow | CMWindowReinforcedDirectional | `Resources/Textures/_RMC14/Structures/Windows/directional.rsi/meta.json` |
| CMU3DDirectionalTintedWindow | CMWindowTintedDirectional | `Resources/Textures/_RMC14/Structures/Windows/directional.rsi/meta.json` |
| CMU3DHybrisaFence | RMCFenceHybrisa | `Resources/Textures/_RMC14/Structures/hybrisa_fences.rsi/meta.json` |
| CMU3DHybrisaElectricFence | RMCFenceHybrisaElectric | `Resources/Textures/_RMC14/Structures/hybrisa_fences_electric.rsi/meta.json` |
| CMU3DMetalCatwalk | CMCatwalk | `Resources/Textures/_RMC14/Structures/catwalk.rsi/meta.json` |
| CMU3DPrisonCatwalk | CMCatwalkPrison | `Resources/Textures/_RMC14/Structures/catwalk.rsi/meta.json` |
| CMU3DHybrisaLatticeCatwalk | RMCCatwalkHybrisaLattice | `Resources/Textures/_RMC14/Structures/catwalk.rsi/meta.json` |
| CMU3DHybrisaElevatorGrate | RMCCatwalkHybrisaElevator | `Resources/Textures/_RMC14/Structures/catwalk.rsi/meta.json` |
| CMU3DPlatformEdge | CMPlatform | `Resources/Textures/_RMC14/Structures/platforms.rsi/meta.json` |
| CMU3DHybrisaPlatformThree | RMCPlatformHybrisaThree | `Resources/Textures/_RMC14/Structures/platforms.rsi/meta.json` |
| CMU3DHybrisaPlatformThreeCorner | RMCPlatformHybrisaThreeCorner | `Resources/Textures/_RMC14/Structures/platforms.rsi/meta.json` |
| CMU3DHybrisaPlatformThreeCornerSmall | RMCPlatformHybrisaThreeCornerSmall | `Resources/Textures/_RMC14/Structures/platforms.rsi/meta.json` |
| CMU3DWireRail | RMCBarricadeWireRail, RMCBarricadeWireRailAltDrawdepth | `Resources/Textures/_RMC14/Structures/Walls/Barricades/barricade.rsi/meta.json` |
| CMU3DHybrisaHandrail | RMCBarricadeHybrisa | `Resources/Textures/_RMC14/Structures/Walls/Barricades/barricade.rsi/meta.json` |
| CMU3DHybrisaRoadCenterBarrier | RMCBarricadeHybrisaCenterRoadDouble | `Resources/Textures/_RMC14/Structures/Walls/Barricades/barricade.rsi/meta.json` |
| CMU3DPlasticRoadBarrierRed | RMCBarricadeHybrisaPlasticRoadBarrier | `Resources/Textures/_RMC14/Structures/Walls/Barricades/barricade.rsi/meta.json` |
| CMU3DPlasticRoadBarrierBlue | RMCBarricadeHybrisaPlasticRoadBarrierBlue | `Resources/Textures/_RMC14/Structures/Walls/Barricades/barricade.rsi/meta.json` |
| CMU3DSandbagBarricade | CMBarricadeSandbag | `Resources/Textures/_RMC14/Structures/Walls/Barricades/barricade.rsi/meta.json` |
| CMU3DWallTubeLight | RMCLightFixture, RMCLightFixtureAlwaysPowered | `Resources/Textures/_RMC14/Structures/Wallmounts/LightingOffset/light_tube.rsi/meta.json` |
| CMU3DWallBlueDoubleLight | RMCLightFixtureBlueDouble, RMCLightFixtureBlueDoubleAlwaysPowered, RMCLightFixtureBlueDoubleOffset | `Resources/Textures/_RMC14/Structures/Wallmounts/LightingOffset/light_tube.rsi/meta.json` |
| CMU3DSmallWallLight | RMCLightFixtureSmall, RMCLightFixtureSmallAlwaysPowered | `Resources/Textures/_RMC14/Structures/Wallmounts/LightingOffset/light_bulb.rsi/meta.json` |
| CMU3DStreetlight | AU14Streetlight | `Resources/Textures/_RMC14/Objects/Misc/Lights/streetlight.rsi/meta.json` |
| CMU3DLightVines | CMVinesLight | `Resources/Textures/_RMC14/Objects/vines.rsi/meta.json` |
| CMU3DHeavyVines | CMVinesHeavy | `Resources/Textures/_RMC14/Objects/vines.rsi/meta.json` |
| CMU3DBroadleafTree01 | RMCFloraTree01 | `Resources/Textures/Objects/Decoration/Flora/flora_trees.rsi/meta.json` |
| CMU3DBroadleafTreeLarge01 | RMCFloraTreeLarge01 | `Resources/Textures/Objects/Decoration/Flora/flora_treeslarge.rsi/meta.json` |
| CMU3DTallJungleGrass | RMCGrassTallJungle | `Resources/Textures/_RMC14/Structures/Flora/tallgrass.rsi/meta.json` |
| CMU3DHybrisaStairFlight | RMCHybrisaStairs | `Resources/Textures/_RMC14/Structures/stairs.rsi/meta.json` |
| CMU3DHybrisaWindowShutter | RMCShutterHybrisaWindow | `Resources/Textures/_RMC14/Structures/Doors/Shutters/Hybrisa/window_shutter.rsi/meta.json` |
| CMU3DHybrisaWindowShutterOpen | RMCShutterHybrisaWindowOpen | `Resources/Textures/_RMC14/Structures/Doors/Shutters/Hybrisa/window_shutter.rsi/meta.json` |

## Existing source attribution

### CMU14/Structures/Walls/steelore.rsi

- Metadata: `Content.CMU/Resources/Textures/CMU14/Structures/Walls/steelore.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/09a5191fb11aab8ddffe3f9be94292b53e4d96f6/icons/turf/walls/strata_ice.dmi

### Objects/Decoration/Flora/flora_trees.rsi

- Metadata: `Resources/Textures/Objects/Decoration/Flora/flora_trees.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from tgstation at commit https://github.com/tgstation/tgstation/commit/e00cae8d065f9cf520688cc0dd0e15ba5bef12a9

### Objects/Decoration/Flora/flora_treeslarge.rsi

- Metadata: `Resources/Textures/Objects/Decoration/Flora/flora_treeslarge.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from tgstation at commit https://github.com/tgstation/tgstation/commit/d388dee8b7b6d854f6f0d844988552acf5962b1f

### _RMC14/Objects/Misc/Lights/streetlight.rsi

- Metadata: `Resources/Textures/_RMC14/Objects/Misc/Lights/streetlight.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cms13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/streetlights.dmi

### _RMC14/Objects/vines.rsi

- Metadata: `Resources/Textures/_RMC14/Objects/vines.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/6a955a3c180f3efcf3b997c230fff4d634eb0629/icons/effects/spacevines.dmi

### _RMC14/Structures/Doors/Shutters/Hybrisa/window_shutter.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Doors/Shutters/Hybrisa/window_shutter.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/doors/hybrisashutters.dmi

### _RMC14/Structures/Flora/tallgrass.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Flora/tallgrass.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/obj/structures/props/natural/vegetation/tallgrass.dmi

### _RMC14/Structures/Wallmounts/LightingOffset/light_bulb.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Wallmounts/LightingOffset/light_bulb.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/884db783073c035b756c175b1bc75fb43279803e/icons/obj/items/lighting.dmi

### _RMC14/Structures/Wallmounts/LightingOffset/light_tube.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Wallmounts/LightingOffset/light_tube.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/884db783073c035b756c175b1bc75fb43279803e/icons/obj/items/lighting.dmi

### _RMC14/Structures/Walls/Barricades/barricade.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Walls/Barricades/barricade.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/5cf465e72efb6beccd2b78bf263072816a2a60ad/icons/obj/structures/barricades.dmi

### _RMC14/Structures/Walls/hybrisa_rock.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Walls/hybrisa_rock.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/turf/walls/kutjevorockdark.dmi

### _RMC14/Structures/Walls/kutjevo_rock.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Walls/kutjevo_rock.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/71d46ee8057d19b12e1495419cb299d2fedef6cc/icons/turf/walls/kutjevo/kutjevo.dmi

### _RMC14/Structures/Walls/prison_rwall.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Walls/prison_rwall.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/09a5191fb11aab8ddffe3f9be94292b53e4d96f6/icons/turf/walls/prison.dmi

### _RMC14/Structures/Walls/shuttle.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Walls/shuttle.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/24f6af93a52fe4099f05b27ec77c59877ddc662d/icons/turf/shuttle.dmi

### _RMC14/Structures/Walls/spp_grey_wall.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Walls/spp_grey_wall.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13-pve at https://github.com/cmss13-devs/cmss13-pve/blob/0afcb527e30cb9d24151005c28f095d704b24c83/icons/turf/walls/upp_grey.dmi

### _RMC14/Structures/Windows/directional.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Windows/directional.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/6c8f3153bb8846baa74e413b3ed8d3355aebcea3/icons/turf/walls/windows.dmi

### _RMC14/Structures/Windows/hybrisa_window.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Windows/hybrisa_window.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/turf/walls/hybrisa_colony_window.dmi

### _RMC14/Structures/Windows/prison_rwindow.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Windows/prison_rwindow.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/09a5191fb11aab8ddffe3f9be94292b53e4d96f6/icons/turf/walls/windows.dmi

### _RMC14/Structures/catwalk.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/catwalk.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/09a5191fb11aab8ddffe3f9be94292b53e4d96f6/icons/turf/almayer.dmi, https://github.com/cmss13-devs/cmss13/blob/8e8d26bbb4f1617ea1b1ffc17ffffca552ce8c11/icons/turf/floors/ice_colony/shiva_floor.dmi, https://github.com/cmss13-devs/cmss13/blob/8e8d26bbb4f1617ea1b1ffc17ffffca552ce8c11/icons/turf/floors/prison.dmi, https://github.com/cmss13-devs/cmss13/blob/8e8d26bbb4f1617ea1b1ffc17ffffca552ce8c11/icons/obj/structures/structures.dmi, https://github.com/cmss13-devs/cmss13/blob/8e8d26bbb4f1617ea1b1ffc17ffffca552ce8c11/icons/obj/structures/props/mining.dmi, https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/hybrisa/grates.dmi, https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/hybrisa/piping_wiring.dmi, https://github.com/cmss13-devs/cmss13/blob/29bfb8501be93dde3c2eececfcf60ae82b0e32df/icons/turf/floors/aicore.dmi, https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/turn/floors/hybrisafloors.dmi

### _RMC14/Structures/hybrisa_fences.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/hybrisa_fences.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/fences/dark_fence.dmi

### _RMC14/Structures/hybrisa_fences_electric.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/hybrisa_fences_electric.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/fences/electric_fence.dmi

### _RMC14/Structures/platforms.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/platforms.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/platforms.dmi, https://github.com/cmss13-devs/cmss13/blob/48e570bd697f2476e28d89cd255d0539a5228228/icons/obj/structures/props/platforms.dmi

### _RMC14/Structures/stairs.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/stairs.rsi/meta.json`
- License: CC-BY-SA-3.0
- Existing attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/8e8d26bbb4f1617ea1b1ffc17ffffca552ce8c11/icons/obj/structures/stairs, rampbottom taken from https://github.com/cmss13-devs/cmss13/blob/8e8d26bbb4f1617ea1b1ffc17ffffca552ce8c11/icons/obj/structures/structures.dmi

## Context correction pass — 2026-09-24

Directional panes now occupy the source fixture's south-edge strip (Y -0.49 to -0.38), and wire rail/Hybrisa handrail occupy the barricade edge at Y -0.375. Four catwalk/grate drafts were lowered to a 0.035-tile deck. Broadleaf canopy widths now follow the visible 61- and 97-pixel reference extents at 32 pixels per tile; height/depth remain inferred. Hybrisa fences use the heavy recessed posts and open cross-bracing of fence0, replacing a generic rectangular wire grid and unsupported electrical towers. All statuses remain draft.

## Orientation correction pass — 2026-09-24

Hybrisa fences and full-tile Hybrisa/prison windows now resolve cardinal connections from anchored IconSmooth neighbours on their own grid. Straight runs use the matching axis; corner, T and cross variants join clipped half panels. Isolated panels can align with an unambiguous adjacent-wall opening. The source comparison selects the connected sprite state. Fence braces use eight steps per diagonal to fit every connected variant within the native part budget. These variants establish topology and orientation; detailed state-specific silhouettes, materials and joined surfaces still require art review.

## Unresolved art work

These are stylized solid-part drafts, not photogrammetric reconstructions or final production meshes. Rock and orange-wall colors were sampled directly from source pixels. Hidden surfaces, heights and canopy depth are inferred. Cuboid-only construction approximates diagonal fence braces, rounded sandbags, trunks and foliage. Fractures, damage states, animated lights, swaying vegetation, tiling transitions, border occlusion, transparent-material sorting and runtime door movement require further work. Border/debug letters found in source wall icons are intentionally omitted from visible geometry.
