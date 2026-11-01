# Redux cargo-crane rails

Nine placed source states now have explicit rail, junction and end-cap assemblies,
covering 105 previously unmapped Redux objects (54 surface and 51 underground).
Each has a lower bearing flange, inset web, recessed rust channel and upper running
flanges. Cropped original artwork preserves the source contours and colors at 32
pixels per tile. Geometry has volume and an undercut; the unseen section and its
2.82–3.02-tile elevation remain inferred. These models are drafts.

All saved facings and pivots are retained. The sprite's `0,0.45` projection offset
is not copied into ground translation. The original horizontal and vertical rail
outlines are not rotationally identical; the nine distinct shapes retain their
source-specific joining edges instead of substituting a rotated straight segment.

The RSI has twelve static states; nine are placed in the inspected maps. Its
four-direction corner and two other T junctions are outside this batch. There are
no animated strips. Runtime `SpriteFade`, crane movement/interaction, lighting,
native context review and performance verification remain unfinished. This work
does not add animation clips or change the normal gameplay viewport.

## Source and license

`Resources/Prototypes/_RMC14/Entities/Structures/overhead_crane_tracks.yml`

`Resources/Textures/_RMC14/Structures/overhead_crane_track.rsi`

CC-BY-SA-3.0. Original metadata: Taken from cmss13 PVE at
https://github.com/cmss13-devs/cmss13-pve/blob/master/icons/obj/structures/props/cargocrane_tracks.dmi

The crops in `Content.CMU/Resources/Textures/CMU14/ThreeD/Garrison/CraneTracks/`
retain their original RGBA values. `garrison_crane_tracks.yml` provides the editable
geometry and surface definitions. Keep this attribution with exported assets.

## Verification checkpoint

All 36,864 source pixel/occupancy samples pass at four rotations. All 104 adjacent
joins agree at four height bands. No modeled-neighbor contact is found in the
same-level four-tile audit; unmapped and cross-level neighbors are excluded.
Underground #2730 has an uncapped north edge in the saved source map. There is no
neighboring track at [218.5, -103.5, -2]; storage rack #566 occupies that tile.

All 787 individual and 546 assembled GLBs validate without errors/warnings.
Export nodes verify 105 saved objects and the 36-object fixture; embedded images
retain the original crop pixels. The native model-budget test and 33 JavaScript
checks pass. The browser fixture and underground saved context were inspected.
Native interactive/frame-time review remains open. Evidence is in
`Tools/three_d/generated/crane-track-verification.json` and related files.
