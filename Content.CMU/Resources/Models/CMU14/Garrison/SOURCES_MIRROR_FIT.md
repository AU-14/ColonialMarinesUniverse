# Mirror source profiles and mounting

This refines the existing rectangular and oval mirror drafts for 28 saved
Stable Garrison Redux placements and 17 classic comparison placements.
The library remains at 778 models; no placed-type coverage is added.

## Source evidence

- `Resources/Prototypes/_RMC14/Entities/Structures/Wallmounts/mirror.yml`
  defines the RMC rectangular mirror with a single-frame sprite, wall mount,
  rotation and `snapCardinals`. The latter keeps the 2D art upright; carrying
  it into physical 3D facing had erased nonzero cardinal directions.
- `Resources/Prototypes/Entities/Structures/Wallmounts/Misc/mirror.yml`
  defines the upstream oval mirror, its customization interaction and a
  separate modern rectangular variant. Resolved components and all saved
  overrides are recorded in `mirror-fit-placement-source.json`.
- `Resources/Textures/_RMC14/Structures/Wallmounts/mirror.rsi/meta.json`
  credits cmss13's watercloset art under CC-BY-SA-3.0.
- `Resources/Textures/Structures/Wallmounts/mirror.rsi/meta.json`
  credits /tg/station and K-Dynamic under CC-BY-SA-3.0.

Both cropped faces retain their original RGBA bytes and license attribution.
They are stored in `Content.CMU/Resources/Textures/CMU14/ThreeD/MirrorFaces/`.

## Geometry and appearance

The earlier mirror drafts were too tall relative to their source silhouettes.
The rectangular face occupies 15 by 18 pixels, mapped to .46875 by .5625
tiles. The oval occupies 14 by 19 pixels, mapped to .4375 by .59375 tiles.
Their original horizontal pivot offsets are preserved. A solid backing
follows every occupied source row; the oval's transparent corners remain
open. Two parts replace the rectangular draft's seven, and ten replace the
oval draft's fourteen.

The original frame, glass pattern and highlights are retained as front
surface artwork. These highlights are static and do not reflect the 3D
scene. The 1.5-tile mounting center and .054-tile thickness are inferred.
Matching source pixels does not establish those physical dimensions.

The raw face colors, alpha and solid backing silhouettes agree with all
8,192 source pixel samples across both models at four rotations. Both
source/four-view cards are in `generated/review/mirror-fit/`; earlier cards
are retained in `generated/review/mirror-fit-before/`.

## Facing and physical support

Saved entity rotation supplies the physical axis. A uniquely indicated
nearby basin can select the visible side, using the existing opt-in wall
target rule. Its offset now takes precedence over a misleading tile index:
a basin immediately below a mirror can cross the adjacent tile's X edge
without becoming an east-facing target. Close, diagonal or conflicting
targets do not supply a new unambiguous side. Existing cardinal-tile
fallback behavior is retained when the physical offset is ambiguous.

Co-located wall fixtures can opt into the existing rear-wall clearance
rule after their mounting pivot is normalized. The support plane comes
from exact anchored solid wall parts at the mirror's height and frontage.
This preserves along-wall spacing and uses a .01-tile gap. Adjacent-room
mirrored geometry keeps its existing mounting rule; depth fitting for
that separate context is not claimed by this extension.

All 45 saved mirrors receive a support adjustment. Eleven final rendered
facings change relative to the previous scene: seven on Redux, four on
classic. Saved positions and rotations remain unchanged. Independent
source-coordinate checks verify the nearby basin choices for every mirror.
All 196,590 unrelated scene records, prior non-mirror variants and floor
tiles remain unchanged, as do 1,918 unrelated definitions and 776 other
viewer models.

The first orientation-only candidate introduced five wall contacts. That
candidate was rejected; its audit is retained separately. The final fit
clears all 46 previous mirror/object contact pairs and introduces none.
The audit uses oriented solid bounds near modeled neighbors on the same
level; it excludes unmodeled neighbors and uses conservative bounds for
curved or textured parts. It does not verify gameplay collision behavior.

## States, exports and remaining work

The RMC resource has one static state. The upstream resource has four
static states: intact oval, intact modern rectangle and their broken
artwork. This pass implements only the two intact placed appearances.
It adds no damaged model, destruction transition or reflection renderer.
Neither resource contains an animation strip; the complete model library
still contains zero exported animation clips.

All 158 Python checks and 197 isolated native checks pass. The final client
build reports zero errors and 692 warnings. After a small allocation
cleanup, all 28 focused layout/mounting checks pass. All 778 deterministic
individual and 528 assembled GLBs validate without errors or warnings.
Actual exported IDs, parts, translations and rotations verify all 45
placements. Thirty-seven saved regions are refreshed, including two added
in this pass. A 32-entity fixture covers both forms in four directions,
co-located and adjacent-room mounting contexts.

The atlas has 682 surfaces used by 201 models; its dimensions remain
4096 by 704 (11 MiB RGBA). Native interactive/frame-time review, runtime
state/animation work, ambiguous door-control mounting, desk-lamp edge fits
and the normal gameplay viewport conversion remain open. No mirror is
fidelity-approved. Evidence is in `Tools/three_d/generated/mirror-fit-*`.

Visual review covered both source/four-view cards, the oval mounting
fixture and Redux underground mirror #2683 above its basin, including
orbiting to its visible side. Browser console errors/warnings were zero.
