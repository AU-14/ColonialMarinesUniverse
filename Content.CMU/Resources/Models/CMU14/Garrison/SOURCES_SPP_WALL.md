# Grey SPP reinforced wall: connected artwork and placement

This refines the existing `CMU3DSPPGreyWall` draft for all 708
`RMCWallSPPGreyReinforced` placements on Stable Garrison Redux level -2.
The earlier basin notes incorrectly described this family as unmodeled.
It already had an exact 17-part assembly. This work replaces that assembly
with 35 parts; model and placed-type coverage do not increase.

## Original evidence and license

- `Resources/Prototypes/_RMC14/Entities/Structures/Walls/spp_walls.yml`
  resolves through `RMCWallSPPReinforced` and `RMCBaseWallReinforced`.
- `Resources/Prototypes/_RMC14/Entities/Structures/Walls/walls.yml`
  supplies inherited wall behavior, including smoothing and destruction.
- `Resources/Textures/_RMC14/Structures/Walls/spp_grey_wall.rsi/meta.json`
  credits cmss13-pve's `upp_grey.dmi`, under CC-BY-SA-3.0.
  The original attribution is retained beside the crops and review reference.

The source contains one standalone icon and eight four-direction corner
states, `uppwall_interior0` through `uppwall_interior7`. None has multiple
animation frames. Walls connect to `walls`, `windows` and `doors` keys.
All 708 saved masks were independently checked against anchored source
neighbors, including those outside a review crop. There are 126 distinct
saved masks on this map.

## Geometry and artwork

The four corner frames do not contain equal quarters. The upper patches
are 16 by 6 pixels; the lower patches are 16 by 26. Cropping at the original
split preserves all opaque pixels without stretching. Thirty-two slots
share sixteen unique cropped textures. The cap splits at local Y .3125,
with SE, NE, NW, SW patches using RSI directions 0, 2, 1, 3.

The cap uses the original artwork above a solid body with recessed plates,
jambs and divided vertical ribs on all four sides. Its sampled body colors
are #444645, #2D2F2E, #1C1C1C and #555858. Every new part lies inside the
previous one-tile-wide core. Old projecting trim has been removed.

The 2.8-tile height, wall depth profile, hidden elevations and translation
of projected sprite art onto a top surface remain inferred. Exact artwork
sampling is not proof that these physical dimensions are correct.

Native and offline corner assembly now allow unequal first-four patches
and retain the rest of the model. Source smoothing aligns those patches
to the grid, correcting 205 previously entity-rotated wall renderings.
Saved entity transforms and facings are preserved. Basins can use the
invariant solid body beneath corner artwork as rear-wall support. Eleven
underground basins gain a .01-tile clearance in their existing front
direction; the twelfth does not require that adjustment.

## States and animations

All 256 static neighbor combinations are represented by the corner rule.
The original standalone icon and isolated composed corner art are distinct
references; the saved connected wall uses the latter rule. No destruction,
bullet-hole, damage, acid or repair appearance is implemented by this pass.
Inherited gameplay components still need a full runtime visual audit.
No 3D animation clip is added; the entire library still exports zero clips.

## Contact and export checks

The audit includes all 708 walls and all 117 configured-map basins.
Contacts fall from 1,005 to 246 pairs: 759 clear, none are introduced.
On Redux -2, 982 pairs fall to 223. Of those remaining pairs, 171 involve
rock walls; others include overlapping foliage, fixtures, overhead parts
and existing basin/counter or basin/toilet contacts. Textured/curved parts
use conservative solid bounds. Unmodeled neighbors are excluded. These
results are not a fidelity approval or proof of gameplay collision behavior.

All saved transforms, floors, previous connection variants and non-wall
render facings are preserved. 195,810 unrelated scene records, 1,902
unrelated definitions and 776 other viewer models are unchanged.

Source verification samples all 256 masks at four actual rotations through
the model's XY UV projection: 1,048,576 RGBA pixels agree with rotated
compositions of the original frames. All 32 crops and body palette colors
also agree. The 155 Python checks, 187 isolated native checks and client
build pass (zero errors; 2,150 existing warnings). All 778 deterministic
individual and 525 assembled GLBs validate with zero errors/warnings.

Thirty-four saved regions are refreshed and 21 are added. Actual GLB node
IDs, rotations, translations and part counts verify all 708 walls and 12
underground basins. A 260-object fixture contains every mask plus four
rotations of an asymmetric pattern. The atlas now contains 680 surfaces;
its dimensions remain 4096 by 704 (11 MiB RGBA). There are 199 models with
original surface artwork.

Evidence: `Tools/three_d/generated/spp-wall-*` and
`Tools/three_d/generated/review/spp-wall/`. Native interactive behavior,
frame-time measurements and normal gameplay viewport conversion remain
open. The full asset/state/animation goal remains active.

Visual review covered the source/four-view cards, saved Redux wall #8265,
basin #619, connected cap from above and the rotated-pattern fixture.
Browser console errors and warnings were zero.

Later checkpoint: `SOURCES_MIRROR_FIT.md` clears the three mirror/SPP-wall
contacts recorded in this historical pass. Other unresolved fixture,
foliage and rock-wall contacts are not claimed as cleared.
