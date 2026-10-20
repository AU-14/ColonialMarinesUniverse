# Garrison water and shoreline sources

Ten editable static drafts map nine classic source types / 152 saved objects and ten types / 1,524 objects across the configured maps. Canonical geometry and surface declarations are `garrison_water.yml` and `garrison_water_art.yml`. The ten new original frame crops occupy atlas slots 280–289; the full atlas stays 4096 by 320 pixels / 5 MiB.

New geometric work is CC0-1.0 to the extent separately licensable. The embedded original artwork and source visual designs retain the licenses and attribution below.

## Shape, orientation and placement

Each water piece is a thin physical volume spanning one tile, with its exact first South source frame projected horizontally. Its .008–.012 tile height is above the terrain and below the .028–.035 tile catwalk tops. `BelowFloor` is a source draw-order category, not a physical negative height. No invented walls or raised slabs close shoreline cutouts. Shallow/deep water and straight/convex/concave transitions retain separate source states and palettes.

All 46,561 saved entity positions/yaws are unchanged. All 152 selected source records contain only Transform overrides; no saved toxic appearance or sprite-state override was found. Water uses the saved rotation without the wall/furniture orientation heuristics. The beach source alpha footprint is byte-exact after rotation for all four directions (12 checks). The ten retained source frames, including black half-alpha shore shadows, are pixel-exact (RGBA). Attribution includes the distinct greener sea-water family.

Eight pairs of shallow desert-water entities already share identical saved pivots and facing; their coincident duplicate surfaces are documented in `water-placement-audit.json`, without invented offsets or changes to the source map. Model mappings outside this batch and all previous layout/support counts are unchanged.

## Fractional alpha correction

The browser, native scene shader and comparison rasterizer now multiply paint and retained source alpha for shared 4-by-4 ordered coverage. Half-alpha black shoreline pixels reveal the ground beneath them instead of turning into solid black patches. This is screen-door transparency, not blended refraction or physically based water. Source alpha below 128 remains a cutout; retained partial-alpha surfaces remain continuously selectable. Fully clear paint passes picking through. Native shader coverage also fixes previously opaque part-level glass.

Portable GLBs use BLEND when textured source pixels retain fractional alpha or paint is translucent, and MASK for binary source cutouts. They embed the unchanged PNG data. Ordinary glTF blended-material sorting is renderer-dependent; it does not reproduce the screen-door pattern.

## Validation and limits

The source and placement audits, ten source/four-view cards, saved-map top/orbit browser captures and two water-region GLBs are under `Tools/three_d/generated/`. The actual native shader compiles/links and passes 336 pixel checks in two desktop configurations (with/without native sRGB and uniform buffers); 160 of those specifically exercise fractional alpha, zero paint alpha, alpha cutouts and continuous CPU picking. The browser fixture passes 80 alpha color samples and five picks. The client build, 85 isolated native checks, 105 Python tests and 16 Node checks pass. Normal connected tests remain blocked by unrelated tactical-map server compilation errors.

These are static surface drafts, not finished water simulation. The source has three or nine animation frames per direction. Frame zero is frozen; on non-South orientations the caustic detail rotates with it, whereas the original directional frames preserve different internal wave detail. Beach shoreline footprints remain exact. Direction-specific caustics, flowing/waving animation, toxic/purified transitions, physical depth, reflection, refraction and connected native scene behavior remain unfinished. No model is marked reviewed and the gameplay viewport is unchanged.

## Original sources

### /Textures/_RMC14/Tiles/planet/desert_water.rsi

License: CC-BY-SA-3.0

Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/f7b9367cb5c083a8c052a40dece9417031e28ffe/icons/turf/floors/desert_water.dmi

### /Textures/_RMC14/Tiles/planet/water.rsi

License: CC-BY-SA-3.0

Attribution: Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/718b6531ec71e34f671ab37c269469e1638cc2ec/icons/turf/ground_map.dmi

### /Textures/_RMC14/Tiles/sheperd/shep_beach.rsi

License: CC-BY-SA-3.0

Attribution: Taken (and Edited) from cmss13 at https://github.com/cmss13-devs/cmss13/blob/f7b9367cb5c083a8c052a40dece9417031e28ffe/icons/turf/floors/desert_water.dmi

## Model references

| Model | Source state | Directions | Frames/direction | Classic objects |
| --- | --- | ---: | --- | ---: |
| CMU3DRMCEntityDesertWaterShallow | shallow | 1 | [3] | 78 |
| CMU3DRMCEntityDesertWaterDeep | deep | 1 | [3] | 11 |
| CMU3DRMCEntityDesertWaterShallowEdge | shallow_edge | 4 | [3, 3, 3, 3] | 13 |
| CMU3DRMCEntityDesertWaterShallowCorner | shallow_corner | 4 | [3, 3, 3, 3] | 4 |
| CMU3DRMCEntityDesertWaterShallowCornerEdge | shallow_corner_edge | 4 | [3, 3, 3, 3] | 10 |
| CMU3DAUEntityShepBeachEdge | shallow_edge | 4 | [9, 9, 9, 9] | 17 |
| CMU3DAUEntityShepBeachCorner | shallow_corner | 4 | [9, 9, 9, 9] | 6 |
| CMU3DAUEntityShepBeachCornerEdge | shallow_corner_edge | 4 | [9, 9, 9, 9] | 4 |
| CMU3DCMFloorShallowWaterEntity | seashallow | 1 | [3] | 9 |
| CMU3DCMFloorDeepWaterEntity | seadeep | 1 | [3] | 0 |
