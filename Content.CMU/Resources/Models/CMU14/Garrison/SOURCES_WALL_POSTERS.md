# Wall posters

Fifteen exact prototype mappings cover 23 saved Redux posters (20 surface and three
on level −2) and 17 classic placements of the same types. Each has two thin solids:
one backing matching the occupied source silhouette and one original printed face.
The current source masks happen to be complete rectangles; the generator checks
every occupied and empty pixel and also supports separated or torn silhouettes.

`Tools/three_d/author_wall_posters.py` owns `garrison_wall_posters.yml`,
`garrison_wall_posters_art.yml`, the 15 `CMU3DWallPoster*Face.png` crops, and the
source/model/context review cards. Atlas slots 1600–1614 are reserved for these crops.
The untouched source frames are single-frame, one-direction 32×32 RSI artwork.
No animation is invented. Destruction and construction produce other source entity
prototypes; their gameplay remains unchanged and those forms are not this batch.

## Source and derivative license

Both source RSI collections are **CC-BY-SA-3.0**. The verbatim face crops, textured
exports and derivative physical constructions retain that license and attribution.

`Resources/Textures/_RMC14/Structures/Wallmounts/posters.rsi/meta.json` credits:
“Taken from cmss13 at
https://github.com/cmss13-devs/cmss13/blob/1ea078e04c49e1004b0bb715f252260cdf39b602/icons/obj/structures/props/posters.dmi,
electro modified by DrSmugleaf, horizon poster by github noctyrnal, weya poster 1 made
by sharksnake 98, minor edits by SharkSnake98”.

`Resources/Textures/Structures/Wallmounts/posters.rsi/meta.json` is authoritative
for upstream artwork and individual contributions. The selected upstream source
frames derive from tgstation commit
https://github.com/tgstation/tgstation/commit/f01de25493e2bd2706ef9b0303cb0d7b5e3e471b
and, for `poster53_contraband`, vgstation:
https://github.com/vgstation-coders/vgstation13/blob/435ed5f2a7926e91cc31abac3a0d47d7e9ad7ed4/icons/obj/posters.dmi.
The proof includes the full unchanged copyright strings and source file hashes.

## Physical presentation assumptions

All 40 saved appearances have default white tint, unit scale, zero sprite offset,
zero sprite rotation, `noRot=false`, and `snapCardinals=true`. A one-direction sprite
with cardinal snapping remains upright on screen; its saved transform angle is not
proof of a particular physical face. Treating every saved yaw as a physical front
buried several posters inside adjacent full wall tiles. The initial failed contact
report is retained under `review/wall-posters/initial-source-mount-proof.json`.

The authored choices use existing native/offline wall mounting only:

* `CMPostEat`, `CMPosterBeth`, `CMPosterBobda`, `CMPosterMissFebruary` and
  `CMPosterSafetyClean` retain the saved physical-facing convention where it fits.
* Bepis, Davenport Gin, Remember Io, High Effect Engineering, and the safety posters
  use the source's upright one-direction facing. This resolves the saved rotations
  which otherwise run along a solid wall row.
* The upstream EAT and Cohiba posters use an explicitly inferred east-facing
  physical front (`yawOffset=90`) on their north–south wall columns.
* Cleanliness uses source-upright facing, with the existing named light-fixture
  facing rule to select the east side of its column placements. Other source poses
  remain subject to the standard bounded wall-context rules.

These are context-derived draft choices, not recovered 3D source orientations.
Saved entity positions and rotations are never edited. Wall mounting normalizes
only depth, preserves along-wall spacing, uses the real mapped wall trim, and
reflects the printed face appropriately for room-side mounting.

Printed dimensions and lateral/vertical pixel pivots use exactly 32 pixels per tile.
Source-center height is inferred as 1.9 tiles for the smaller RMC posters and 1.75
tiles for the larger upstream prints. The latter clears existing wall lights whose
lowest fittings are above 2.26 tiles. Paper/backing thickness is 0.008 tiles plus a
0.002-tile printed skin; the blank reverse material is inferred. No fixture or wall
is removed to make a review image look clear.

## Evidence and scope

`Tools/three_d/generated/wall-posters-proof.json` records all selected IDs, actual
source/saved appearances, all ten configured map counts, 40 mounting contexts,
source/crop RGBA equality, silhouette comparisons and conservative neighbor contact
candidates. `review/wall-posters/source-model-montage.png` compares source artwork,
fronts, oblique geometry and inferred reverse construction. Individual context
cards are explicitly labeled cutaways; contact checks include all nearby modeled
geometry, including walls omitted from those visual cutaways.

The executed authoring proof compared **6,572 source/crop RGBA pixels** and **15,360
occupied/empty silhouette pixels**, with zero mismatches. Its 427 baseline neighbor
comparisons did not include newly modeled posters against each other. The final
shared-scene check supersedes its broad clearance count: **19/23 Redux and 12/17
classic placements have zero modeled contacts**, across 439 neighbor comparisons.
Thirty nearby instance occurrences remain unknown or inherited models and are
outside that conclusion. All 40 mounts use a real modeled backing wall.

`wall-posters-export-audit.json` verifies all 15 exported GLBs (720 mesh vertices,
360 UV vertices), every embedded source pixel and the same 6,572 pixels in the
native atlas. Saved source transforms and authored presentation are retained.

Two overlapping paper pairs repeat in both maps: Bepis/Remember Io is Redux
5247/5255 and classic 2539/2550; Davenport Gin/Miss February is Redux 5251/5252 and
classic 2543/2544. Their original source sprites also overlap at the saved positions.
At the historical 916-model checkpoint these prints were coplanar and required
source-aware paper stacking; this was not a remaining Redux wall orientation fault.
`wall-posters-overlap-classification.json` records exact source overlap bounds and
default-view draw-order evidence. Remember Io and Miss February sort in front under
the default source ordering, but no live RenderOrder was sampled. Existing wall
fitting absorbs arbitrary depth translations, so stacking must follow attachment.
The historical export audit and overlap classification remain unchanged.

## Source-ordered paper attachment

The `wallPaper: true` metadata now opts these fifteen models into a bounded
post-attachment pass. The native adapter reads the actual one-frame, one-direction
32×32 source layer, DrawDepth and RenderOrder. For equal identity canvases, the
canonical unrotated source-view ordering is descending world Y, then source entity
ID. This ordering is fixed relative to the source view; moving the 3D camera never
reorders the physical paper. Offline saved-map rendering uses the same comparison
with saved/default RenderOrder and saved entity IDs. An exact tie can differ when
runtime entity IDs differ from saved IDs; none of these four pairs has that tie.

Connected paper on the same grid face is considered after wall fitting. Each
later overlapping paper moves toward its resolved front until its rear clears the
earlier paper's complete solid front by **0.004 tiles**. The present two-layer
pairs therefore need **0.014 tiles**, retaining the original 0.010-tile solids.
Only Redux 5252/5255 and classic 2544/2550 move. No paper is thickened, no source
transform/artwork is altered, and no gameplay fixture or collision is changed.
Neighbor discovery includes paper outside the captured/exported crop and follows
overlap chains; the maximum is sixteen papers and 0.25 tiles of separation.
Unsupported source transforms/layers/states, noncardinal grid-relative attachment,
or exceeded budgets preserve the original sprite fallback.

`wall-paper-fit-proof.json` records the actual raw-map/prototype context check
against the frozen 916-model neighbor library: all forty placements retain their
source geometry/transforms; all forty isolated crops produce the same placement;
**23/23 Redux and 16/17 classic** have zero conservative modeled-neighbor contacts
across 439 comparisons. This is a focused context proof, not a final global-export
or native-runtime claim. The earlier broad clearance figures describe the previous
checkpoint. Final shared-export evidence is recorded separately after export.

**Known classic exception:** `PosterLegitCleanliness` UID 8816 at `(15.5,-27.5)`
has opaque walls north, south and west, and a half-tile sink east. Its current
physical front intersects the southern prison wall UID 46208. An unconditional
sink-facing rule would misorient other washroom posters; this placement remains
deferred for a bounded context-specific attachment treatment. It is not included
among the current 39 fully clear placements and is not claimed fixed by a cutaway image. The
Redux counterpart has a different arrangement and passes the current context check.

This authoring pass does not claim native runtime verification or full map visual
completion. Unknown or inherited neighboring models remain listed. Global exports,
shared-scene verification and native builds are coordinated separately. Neither
the game nor a server is launched. The classic-only `CMPosterSafetyGoggles1` and the
separate `decals.rsi` no-smoking signs are excluded from this Redux poster batch.
