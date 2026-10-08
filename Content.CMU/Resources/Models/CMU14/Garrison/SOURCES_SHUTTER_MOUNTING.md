# Window shutter geometry and mounting

Draft checkpoint: 2026-09-25. Stable Garrison Redux remains the primary map.

The closed/open Hybrisa window shutter drafts now follow their original
steel bands, transparent gaps, edging and palette. They use 78/30 solids,
replacing the earlier 29/7-part approximations. All 2,048 sample positions
in the two 32-pixel reference frames agree with front geometry, including
860 occupied pixels and their colors. This is a normalized front-profile
check, not proof of physical proportions or full directional fidelity.

## Source evidence

The original prototypes are in
`Resources/Prototypes/_RMC14/Entities/Structures/Doors/Shutters/shutters.yml`.
`RMCShutterHybrisaWindow` and its `Open` child inherit grid-center placement,
wall-mount behavior and the source's `walls` smoothing key (`NoSprite`).
Their four-direction sprite selects the saved facing. The saved maps place
these shutters on the same grid tiles as glazing; a shared tile center is
not sufficient depth separation for physical 3D solids.

Original art:
`Resources/Textures/_RMC14/Structures/Doors/Shutters/Hybrisa/window_shutter.rsi`.
The RSI declares CC-BY-SA-3.0 and attributes cmss13:
<https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/doors/hybrisashutters.dmi>.
Preserve this attribution/license with derived assets. No external
reconstruction or generated texture is used.

The South closed state has a broad upper panel and six separated lower
bands. The open state retains only the raised strip; the previous model's
full-height side guides are not present in that source pose. Open East/West
occupy four columns of a 32-pixel tile, supplying the revised .125-tile
depth reference instead of the previous .29. Hidden roll construction and
2.74-tile closed height remain inferred. The horizontal scale is one tile
per 32 source pixels; the vertical mapping uses the inferred height over
the closed frame's 28 occupied rows. Original gaps remain open volumes.

The RSI also has opening/closing states: six .1-second frames per direction
for each transition. The inherited door component has its own animation
timing. Neither those transitions nor their runtime synchronization is
implemented in these models. GLBs still contain zero animation clips.

## Placement behavior

Only the two shutter models opt into `windowMountTargets`. Native and
offline layout find an exact, anchored, same-grid glazing model on the
same tile. They resolve its connected shape and project its modeled face
onto the shutter's saved facing, placing the shutter rear beyond that face
with a .02-tile assembly gap. Both poses use the same depth. Saved map
positions, yaw and source-state selection remain unchanged; presentation
offsets are explicit and exported.

No inherited window candidate supplies a mounting plane. Disabled smoothing,
unanchored objects, perpendicular panes and ambiguous square/corner forms
do not acquire guessed offsets. Standalone shutters retain their pivot.
Ten co-located prison-cell observation windows have ambiguous geometry and
remain among the 189 unshifted shutters; the other 179 have no configured
co-located glazing target in these snapshots.

The audited set contains 1,488 shutters: 460 classic surface, 539 Redux
surface, 59 Redux -1, 109 Redux -2 and 321 Redux +1. There are 1,299 mounts:
415 classic, 461 Redux surface, 59 Redux -1, 78 Redux -2 and 286 Redux +1.
All 1,902 unrelated definitions, 776 other viewer models, 195,147 unrelated
entity records, floor tiles and connected variants remain unchanged.

## Fit results and limits

The conservative contact audit falls from 2,343 entity pairs to 999,
clearing 1,363 pairs and introducing 19. All 1,299 shutter/glazing pairs
are cleared. Redux -1, -2 and +1 introduce no new pair. The 19 new surface
pairs involve three sinks and sixteen foliage contacts; the latter use
conservative bounds. Existing and new contacts remain unresolved, not
fidelity-approved. The early thick-mount candidate introduced 235 pairs;
it is retained only as comparison evidence and is not the installed model.

147 Python tests, 168 isolated native checks and the client build pass
(zero errors, 2,150 existing warnings). The final native library budget
check passes after geometry refinement. All 778 deterministic individual
GLBs and 472 assembled exports validate with zero glTF errors/warnings.
159 existing regions are refreshed; 157 saved regions and an 18-entity
mounting fixture are added. Actual node IDs and translated positions verify
all 1,488 shutters, including all 1,299 mounted placements.

Both source/four-view cards, eight original directional reference frames,
before/after geometry, all four mounting sides in both poses, standalone
controls and saved Redux lower-level open/closed contexts were inspected.
Browser console errors/warnings are zero. Native interactive interaction,
frame-time profiling and ordinary gameplay viewport conversion remain open.

Evidence: `Tools/three_d/generated/shutter-mount-*` and
`Tools/three_d/generated/review/shutter-mount/`. Next inspect the new sink
contacts and foliage bounds, plus the previously recorded window/wall trim
interfaces. No asset is marked reviewed and the full goal remains active.

Later checkpoint: `SOURCES_SINK_FIT.md` clears all three sink/shutter pairs recorded here by refining the basin geometry. The sixteen conservative foliage contacts and other outstanding contacts are not cleared by that pass.
