# Garrison outdoor sources

19 authored or revised draft assemblies: twelve broadleaf trees, a stump, three jungle-grass footprints and three rail sections. Sixteen new assemblies; three replace earlier drafts. Canonical geometry is the model YAML.

New geometric work is contributed under CC0-1.0 to the extent separately licensable. Existing source visual-design and derivative rights retain their original licenses and attribution.

## Placement and fidelity

The twelve tree crowns now use overlapping ellipsoids in place of stepped box shelves. Source widths, distinct crown bands, mirrored variants and trunk pivots remain explicit. The browser/GLB use a shared 224-triangle sphere topology, while native rendering/picking uses the corresponding analytic curved surface. Branch/root details, leaf textures, wind and unseen forms remain unfinished; no draft is upgraded to reviewed.

Tree crown widths follow source visible bounds at 32 pixels per tile. The trunk stays at the saved ground pivot; source screen-height offsets are not ground displacement. Fixed noRot art remains fixed. Large variants 4/5/6 and normal variant 6 mirror 1/2/3 and 3 respectively. Normal 4 follows mirrored 1 (the source has a one-pixel image-space shift); normal 5 has an independently authored spreading crown because it is not an exact mirror of 2. Random-sprite spawners are not claimed as exact static matches. Round foliage, wind, hidden branches, height and depth remain inferred.

Grass uses the source gray palette multiplied by #64AA6E, declared as both referenceTint and bakedSpriteTint. Side and corner footprints use their directional source shapes. Occlusion overlays and sway are unfinished. The stump has no snow in its actual source state.

Rail sections preserve open sleeper gaps, .44-tile gauge and all four source facings. A south-facing bend connects south to east; red buffers face the approaching track from south. Curves use stepped solids within the native 128-part budget. No models are marked reviewed.

## Models

| Model | Exact source reference | State |
| --- | --- | --- |
| CMU3DBroadleafTree01 | RMCFloraTree01 | tree01 |
| CMU3DBroadleafTree02 | RMCFloraTree02 | tree02 |
| CMU3DBroadleafTree03 | RMCFloraTree03 | tree03 |
| CMU3DBroadleafTree04 | RMCFloraTree04 | tree04 |
| CMU3DBroadleafTree05 | RMCFloraTree05 | tree05 |
| CMU3DBroadleafTree06 | RMCFloraTree06 | tree06 |
| CMU3DBroadleafTreeLarge01 | RMCFloraTreeLarge01 | treelarge01 |
| CMU3DBroadleafTreeLarge02 | RMCFloraTreeLarge02 | treelarge02 |
| CMU3DBroadleafTreeLarge03 | RMCFloraTreeLarge03 | treelarge03 |
| CMU3DBroadleafTreeLarge04 | RMCFloraTreeLarge04 | treelarge04 |
| CMU3DBroadleafTreeLarge05 | RMCFloraTreeLarge05 | treelarge05 |
| CMU3DBroadleafTreeLarge06 | RMCFloraTreeLarge06 | treelarge06 |
| CMU3DBrokenTreeStump | RMCFloraTreeStump | treestump |
| CMU3DTallJungleGrass | RMCGrassTallJungle | tallgrass |
| CMU3DTallJungleGrassSide | RMCGrassTallSidesJungle | tallgrass_sides |
| CMU3DTallJungleGrassCorner | RMCGrassTallCornerJungle | tallgrass_corner |
| CMU3DRailroadStraight | RMCRailroadStraight | railroadStraight |
| CMU3DRailroadBend | RMCRailroadBend | railroadBend |
| CMU3DRailroadBumper | RMCRailroadBumper | railroadBumper |

## Source attribution

### Resources/Textures/Objects/Decoration/Flora/flora_trees.rsi

- Metadata: `Resources/Textures/Objects/Decoration/Flora/flora_trees.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from tgstation at commit https://github.com/tgstation/tgstation/commit/e00cae8d065f9cf520688cc0dd0e15ba5bef12a9

### Resources/Textures/Objects/Decoration/Flora/flora_treeslarge.rsi

- Metadata: `Resources/Textures/Objects/Decoration/Flora/flora_treeslarge.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from tgstation at commit https://github.com/tgstation/tgstation/commit/d388dee8b7b6d854f6f0d844988552acf5962b1f

### Resources/Textures/Objects/Decoration/Flora/flora_treessnow.rsi

- Metadata: `Resources/Textures/Objects/Decoration/Flora/flora_treessnow.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from tgstation at commit https://github.com/tgstation/tgstation/commit/8aeb2678e0150dcb308f02304ec200cef235e62c

### Resources/Textures/_RMC14/Structures/Flora/tallgrass.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/Flora/tallgrass.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/0525b5ada7da1afcd9b260e76d5fea01500d9c8d/icons/obj/structures/props/natural/vegetation/tallgrass.dmi

### Resources/Textures/_RMC14/Structures/catwalk.rsi

- Metadata: `Resources/Textures/_RMC14/Structures/catwalk.rsi/meta.json`
- License: CC-BY-SA-3.0
- Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/09a5191fb11aab8ddffe3f9be94292b53e4d96f6/icons/turf/almayer.dmi, https://github.com/cmss13-devs/cmss13/blob/8e8d26bbb4f1617ea1b1ffc17ffffca552ce8c11/icons/turf/floors/ice_colony/shiva_floor.dmi, https://github.com/cmss13-devs/cmss13/blob/8e8d26bbb4f1617ea1b1ffc17ffffca552ce8c11/icons/turf/floors/prison.dmi, https://github.com/cmss13-devs/cmss13/blob/8e8d26bbb4f1617ea1b1ffc17ffffca552ce8c11/icons/obj/structures/structures.dmi, https://github.com/cmss13-devs/cmss13/blob/8e8d26bbb4f1617ea1b1ffc17ffffca552ce8c11/icons/obj/structures/props/mining.dmi, https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/hybrisa/grates.dmi, https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/hybrisa/piping_wiring.dmi, https://github.com/cmss13-devs/cmss13/blob/29bfb8501be93dde3c2eececfcf60ae82b0e32df/icons/turf/floors/aicore.dmi, https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/turn/floors/hybrisafloors.dmi
