# Prison observation window and shutter assemblies

The ten co-centered `RMCWindowPrisonCell` and Hybrisa window-shutter pairs now
share a context-derived presentation assembly. Five occur on Stable Garrison
Redux surface and five on classic Garrison. Source entity positions, yaw,
physics, identity, original artwork and animation clocks are unchanged.
The four observation windows without shutters retain their existing geometry.

## Source interpretation and inferred depth

The source description specifies a glass window with a rod matrix inside a
wall frame. Its inherited one-tile collision fixture also appears on thinner
window types; it does not recover the frame's physical depth. The original
`CMU3DPrisonCellObservationWindow` draft used a full-depth wall module, with
facade detail reaching .525 tile from its pivot and inner glazing at .045.
That buried a co-centered shutter. Moving the shutter wholly outside the
unmodified frame introduced fourteen neighboring-object collision pairs.
The failed exterior-offset candidate remains in the historical reports.

The compound correction reserves a .45-tile outer face: half a tile minus
the adjacent .04 wall-trim projection and .01 clearance. Subtracting the
shutter's .125 depth and .02 backing gap gives a .305 window facade and a
.3875 shutter-center displacement. This is a bounded architectural inference
from the saved context, not a measurement recovered from the flat source.

Only the facing facade depth beyond the original .045 glazing plane is
monotonically compressed. The opposite half, profile coordinates, source
colors and all translucent glazing parts remain unchanged. Two opposite
shutters reserve both sides independently. No source model or texture file
is rewritten; the geometry variant exists only for the particular assembly.

## Bounded selection and lifecycle

Both paths require exact source/model pairs, an anchored same-grid source
pivot and a supported straight connectivity axis. Corners, crossings,
perpendicular shutters, off-pivot objects, hidden shutters and unknown
geometry do not acquire a guessed recess. The native query additionally
uses the existing door appearance adapter to reject unsupported live layers
or source animation frames.

`layout.py` reads the complete saved source cell, including objects omitted
from a cropped export. The native prison-window partial reads the anchored
cell on each scene refresh. Both use the same backing geometry for the
window and the shutter's mounting plane, independent of iteration order.
Only immutable geometry is cached, keyed by model, connection mask and
facing-side bits; source entity membership is never cached. Removing,
moving or unanchoring a shutter restores its side on the next resolution.
Prototype reload clears the native geometry cache.

## Verification and joining-face correction

`prison-shutter-compound-verification.json` uses the frozen 872 library and
actual saved maps. It checks all 1,488 shutters and fourteen prison windows:
exactly ten shutter records and ten window records change, while 1,478
shutters and the four unmounted windows remain identical. All fourteen
shutter poses clear their backing by .02 tile within floating-point
tolerance. No modeled same-level neighbor contact is introduced within
the four-tile context query.

All ten computed assemblies equal the previously reviewed candidate to
1e-12 in geometry coordinates and exactly in material metadata. Its twenty
old-draft versus candidate facade comparisons matched all 2,240,000 pixels.
This compares the two 3D draft projections; it does not certify that a
single original sprite completely specifies the reconstructed object.
The two model YAML files and shutter art YAML match the 872 snapshot byte
for byte. All 43 shutter texture PNGs and five source-resource files retain
their earlier exact-source proof hashes.

Fifteen focused Python mounting tests pass, covering axes, sides, rotated
grids, source traversal order, cropped exports, opposing shutters,
removal/unanchoring and unsupported arrangements. Native helper fixtures
are included; shared compilation, native capture/packing and final GLB
verification are coordinated by the parent task and recorded separately.

The first compound audit retained eight actual opaque trim intersections:
two .018-tile side-seam pairs in nine shutter poses and six upper-course/
edge-rib pairs reaching .035 tile in all fourteen poses. These were not
transparent gaps or AABB-only false positives. The subsequent joining-face
correction is measured separately in `prison-shutter-joined-verification.json`.

`RMCWallPrisonReinforced` explicitly includes windows and doors in its source
IconSmooth connection keys. The previous 15-part draft lacked connection
handling and kept exposed decorative relief on an internal joining face.
Actual saved connection masks change the original RSI's end treatment when
the opening cell is removed. Grid-normalized corner-state comparison cards
show these source differences; they are not captured live SpriteSystem frames.

Only a reinforced prison wall beside a verified co-mounted prison window
gets the joining-face variant. The full core, other wall faces, all shutter
parts, opening width and texels remain intact. Relief wholly beyond the
joining core face moves intact behind .49 tile; spanning courses terminate
at .49 while their opposite ends stay fixed. This intentionally hides
inferred exposed-face decoration at an internal source-connected joint.
It does not claim to preserve the erroneous old 3D protrusion.

Across all 1,919 reinforced prison-wall placements in the eight complete
scenes, exactly eight wall records change and 1,911 remain identical.
The correction clears all eight pairs through fourteen shutter poses, with
at least .01 tile of separation from the changed relief. The unchanged wall
core meets the full-width shutter at the shared tile boundary: zero gap,
zero positive-volume intersection. Every changed wall part is a subset of
its original box or lies entirely inside the original opaque core, so the
correction cannot add occupied volume or introduce a neighbor collision.

Both native and offline paths derive the joining faces from the complete
source cell, requiring exact prototypes/models, enabled window smoothing,
anchoring, matching opening axes and exact adjacent source positions.
Relative wall/grid rotations are handled explicitly; source membership is
recomputed after removal/unanchoring. Geometry cache keys contain the local
joining-face bits and are cleared on prototype reload. Unknown textured,
curved or changed core geometry is rejected. Shared build and native tests
are parent-owned; the two published 872-model surface/classic scene hashes
and unchanged six companion scenes are recorded in the joined proof.

## Attribution

Window source:
`Resources/Textures/_RMC14/Structures/Windows/prison_cellwindow.rsi` and
`Resources/Prototypes/_RMC14/Entities/Structures/Windows/prison_windows.yml`.
Shutter source:
`Resources/Textures/_RMC14/Structures/Doors/Shutters/Hybrisa/window_shutter.rsi`.
The original CM-SS13-derived resources retain their RSI attribution and
CC-BY-SA-3.0 licensing. Existing shutter consolidation/animation notes retain
the complete upstream source link and exact-pixel/state timing evidence.
All checks and review renders were offline; no game or server was launched.
