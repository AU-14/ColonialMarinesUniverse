# Native 3D renderer

The native view is a debug-admin tool. Enter `cmu_3d_firstperson` while attached
to an actor; `cmu_3d_firstperson off` restores the ordinary viewport.
`cmu_3d_live` opens the orbit inspector. Both require the client and server from
the same build for the 3D view subscription event.

## Replication and floors

Opening a view requests server-owned PVS origins on the actor's map and the
other maps in its Z network, within eight floors of the actor. Each origin
follows the actor's world position every server update, without the ordinary
2D Z visual offset. The radius defaults to 24 tiles and is bounded to 4–24, with eight
extra tiles for budgeted replication to arrive before entering the view. The radius is converted to the
engine's PVS diameter; the actor's visibility mask is preserved.

These origins do not recursively create ordinary Z probes. Closing the view,
losing debug permission, detaching the actor, or disconnecting releases them.
Changing maps or network membership replaces obsolete origins. The normal 2D
opening-based view subscriptions remain independently owned.

The scene initially reserves one share of its geometry budget for every floor
and gives an additional share to the player's floor. Each sample records the
parts admitted and rejected on each floor. The next sample reallocates unused
capacity from sparse floors in either direction, including back to the occupied
floor. First-person views use the part budget without a separate entity-count
cutoff. Entities within eight horizontal tiles are considered before
more distant terrain; walls and doors retain priority within each band. This
prevents far-away rock detailing from excluding all nearby furniture and
imported drafts, while preserving a reservation for the rest of the stack.
Already-admitted sources retain priority within a band. The near band has a
two-tile exit margin so crossing a sample boundary does not immediately evict
its models. Solid tile walls beyond six tiles of 3D distance use a simple shell
with their original solid footprint and full crown height, retaining full detail
until eight tiles when moving away. Frames, apertures, textured/transparent
cores and oversized assemblies are excluded; terrain cutouts run after detail
selection. Nearby art is unchanged. This reduces whole-object omissions at the
part cap rather than only moving that cap between floors.

## Movement and rendering

First-person snapshots include six extra tiles outside the visible area. The
scene origin remains fixed until the actor moves more than four tiles away,
leaving two tiles of coverage while the next snapshot is prepared. The visible
square follows the camera at the configured radius; extra buffered geometry is
not drawn or targeted. Reversing near a region boundary does not force recentering.
Discovery and packing yield after approximately six milliseconds of work, with
no mandatory frame delay for each floor. A completed refresh starts the 0.1-second
refresh interval, rather than consuming that interval during preparation. The
previous complete scene remains visible until publication.

Relative mouse look uses the latest local angle for presentation. Only the
sequenced predictive event changes the networked mover heading, so state rollback
cannot pull the visible camera back to an older heading between simulation ticks.
Ordinary 2D camera gestures retain their existing behavior.

The observer's first-person camera follows authored terrain and stair elevations
at its logical floor depth. Ghosts can retain a stair support offset because they
are excluded from gravity settling; that residual does not lift the inspection
camera above its landing. Stairs without authored profiles retain their physical
ascent while supporting the observer. Physical actors still use their predicted Z position,
including jumps and falls. The camera adjustment does not change ghost movement,
map transitions, or sprite placement.

The camera updates after transform interpolation and is no longer capped at
30 FPS. Fallback sprite positions update on each draw, separately from their
30 Hz artwork. Their planes, atlas bounds and source pivots follow the entity's
orientation (or the source's no-rotation setting), independently of camera look.
CPU alpha picking uses that same fixed plane. These fallback assets are still
flat, and can appear edge-on; they are not substitutes for complete 3D models.
All fallback candidates are retained. Atlas updates select up to 256 sprites
from the camera frustum, with a margin for movement, sorted by distance. Objects
behind the camera and off-screen floors no longer consume a global 64-sprite
allowance before visibility is considered. Map lighting is sampled at 10 Hz, or when the sampled region
changes. Geometry/state discovery starts at most every 0.1 seconds while stationary;
a refresh spanning more frames finishes before the next starts. This budgets
discovery and packing, not the complete frame: individual map queries can
still exceed that slice. Local model
assemblies are cached by their resolved parts, rotation, tint, cutaway and stair
scale. Position and floor offsets remain live. Full floor budgets reject whole
sources before constructing their geometry. Packing reuses its source-group and
cell-index buffers. These changes do not alter movement prediction, collision,
or authoritative Z transitions.

Aim queries traverse the packed spatial cells instead of intersecting every part
on every floor. The first-person index has 64×64 horizontal cells and 80 vertical
slices, each two units tall, covering every admissible tilted model. CPU and GPU
rays advance across XYZ boundaries and only visit the intersected cells. Each GPU
spatial reference contains a source ID and conservative
height bounds, rejecting other floors with one texture read. Shader intersections
also reject geometry outside the current ray segment before expensive shape/material
work, including a precise height check for untilted parts. The reference texture is
2048 texels wide and stays within 4096 rows at the four-million-reference budget.
Scene capacity is 130,560 parts. Cell descriptors carry 24-bit counts and
references carry 17-bit IDs plus conservative two-unit height bounds in four
bytes. Admission still keeps each source
entity intact. Terrain fitting calculates shared local model bounds once per map
sample, while retaining per-entity clipping against the actual world cutouts.

The 3D render target defaults to a 2,073,600-pixel ceiling (1920×1080), retaining
aspect ratio. HUD and input retain the window's native resolution. The existing
`cvar cmu.3d.resolution` multiplier can lower resolution further;
`cvar cmu.3d.max_pixels 0` disables the ceiling. This prevents a maximized 4K
window from silently multiplying the ray shader's pixel workload by four.

Projected Z lighting derives viewport bounds from all four projected corners,
avoiding inverted bounds when a ghost/eye switch precedes the viewport eye update.

## Local development latency

The shared Debug entry point deliberately defaults to 75 ms of artificial
latency on each peer, 0.5% loss and 0.5% duplicate packets. A local native review
captured roughly 150 ms ping with an empty state buffer under those defaults.
For normal local play, launch **both** peers with:

```text
--cvar net.fakelagmin=0 --cvar net.fakelagrand=0 --cvar net.fakeloss=0 --cvar net.fakeduplicates=0
```

The ignored local launcher applies these overrides. The shared Debug defaults
remain available for intentional network testing. These settings do not remove
real network latency or replace movement prediction.

## Verification

```powershell
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.Tests -Filter 'FullyQualifiedName~ThreeD'
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Filter 'FullyQualifiedName~CMU3DLiveSceneTest|FullyQualifiedName~CMUZProbeOwnershipTest'
```

Use `-Configuration Release` for native performance review and its corresponding
checks; Debug captures are not representative of an optimized game build.

The connected regression explicitly enables `net.pvs` (the integration pool
normally disables it). It checks remote-floor geometry and height, movement
to another region, terminal probe ownership, close/reopen, and server-side
permission revocation. The headless movement regression calls the real
first-person scene sampler without requiring native window or mouse APIs.
The movement regression also covers cached model movement, rotation and tint,
and coherent publication while walking during a staged refresh. Inactive scene
encoding is also sliced across frames; drawing and picking retain the previous
complete encoder until the replacement is ready. A cancelled packing operation
cannot publish partial geometry. The focused regressions are
`WalkingKeepsStaticGeometryStableBetweenSceneOriginChanges`,
`PendingSceneKeepsPreviousGeometryUntilCompleteAndCanBeCancelled`,
`FirstPersonLookRemainsLocalUntilPredictionTick`,
`PrefetchedGeometryIsNotTargetableOutsideVisibleRange`,
`SparseFloorsDoNotHideFurnitureOnTheOccupiedFloor`,
`DistantRockRetainsItsSolidFootprintAndCrownHeight`, and
`DistantFrameKeepsItsOpening`. A deterministic
ray regression (`SpatialPickingMatchesExactGeometryAcrossCellsAndFloors`) compares spatial picking with exhaustive geometry intersections.
GPU sprite checks cover occlusion, alpha cutouts, fixed edge-on orientation and
oblique planes, and atlas slots beyond the previous 64-sprite cutoff. The buffered-view fixture also checks that geometry outside the
camera's visible square is clipped even while it remains packed in the scene.

A temporary headless measurement on 2026-10-07 used 2,380 static parts and 100
small movement samples within one scene region. Recentring on every step caused
99 packed geometry uploads, took 678.76 ms, and allocated 112,416,552 bytes.
With the stable origin, the same workload caused zero uploads, took 220.49 ms,
and allocated 60,396,320 bytes. These single-run CPU measurements isolate scene
sampling/packing, not GPU rendering or full-map performance. The temporary
instrumentation was removed; the geometry regression remains.

Native shader validation and optional isolated benchmarks:

```powershell
python Tools/three_d/validate_native_shader.py --powershell <PowerShell-7-path>
python Tools/three_d/validate_native_shader.py --powershell <PowerShell-7-path> --benchmark
```

On 2026-10-07, an RTX 4070 Ti SUPER (driver 610.62) rendered the same captured
32,740-part geometry at 1280×720 in a median 10.17 ms originally, 6.58 ms with
segment rejection, 2.90 ms with the packed height bounds, and 1.29 ms after
separating vertical cells (12 GPU timer samples after warmup). This fixture uses
neutral materials/lighting and omits live billboards; it measures the geometry
shader, not full game FPS. On a Ryzen 5 7600, 500 aim queries over the same
synthetic radius-24 fixture fell from 1,292.77 ms to 103.25 ms after spatial
traversal. The benchmark uses actual C# scene encoding and picking.

A native Redux review on the same machine found two separate costs. Disabling
Debug network simulation reduced localhost ping from about 150 ms to 0–4 ms.
After wall detail selection, the sampled stack held about 40,000 parts rather
than filling the 65,280-part cap. With packing sliced as well as discovery,
observed scene-system maximum calls fell from 52–58 ms to 13–17 ms. These are
successive live captures with user movement and focus changes, not a controlled
FPS comparison. The final focused windows still ran around 30–31 FPS; one long
frame included 118 ms in the engine's PVS-exit processing. Lighting-view rendering
and entity streaming remain significant costs. Temporary automation and capture
instrumentation were removed from the source.

A subsequent 20-second Release capture used the buffered scene and local look
changes, with 36,872 packed parts at capture start. Scene-system calls averaged
1.99–3.00 ms per frame and peaked at 9.16 ms. The final focused five-second
window received 7,467 new entities and averaged 66.49 FPS, with a 30.19 ms p95
and 53.80 ms worst frame. The capture also included a 704 ms stall during a
view switch, mostly in UI rendering. Focus, window size and view mode changed,
so this is not a controlled comparison with the earlier Debug capture and does
not establish hitch-free movement. No PVS entry budgets were increased.

A furniture pop-in investigation on 2026-10-08 found the fixed per-floor
shares discarding hundreds of modeled entities while leaving about 17,000
scene slots unused. The separate 1,536-entity floor cap also dropped entities
before the part allowance was filled. Demand-based sharing and removal of that
entity cap restored this capacity. The full 65,280-part scene then still omitted
hundreds of models in a dense street, motivating the 130,560-part capacity.
The sprite atlas previously kept only the first 64 candidates before checking
visibility; the view-based selection drew all 133 visible sprites out of 411
replicated candidates in one native sample.

After the capacity increase, the native capture packed 114,066 parts. Across
17 sampled origins, the occupied floor rejected no parts for its floor budget;
one early basement sample temporarily rejected 1,226 parts before the next
allocation. Encoding still rejected 860 parts at capture start, so this is not
an assertion that every source in the map is renderable. At a 2560×1351 window,
the four focused five-second windows averaged 42.35, 53.85, 34.43 and 43.67 FPS
while moving across the map and switching views. The worst wall frame was
949.80 ms, including 823.54 ms in UI rendering. Scene-system calls averaged
2.59–5.89 ms per frame and reached 28.93 ms. These changing views are not a
controlled comparison with earlier captures, and hitch-free movement remains
unverified. Temporary counters and screenshot/capture helpers were removed.

A follow-up native check found a floor-budget feedback loop: models removed
by surface placement or slab-opening fallback were counted as demand when
rejected, but disappeared from demand when admitted. This repeatedly transferred
capacity between floors and evicted/restored unchanged windows and furniture.
Demand now retains the pre-fallback reservation, or the final fragment count
when larger, plus rejected parts. At the same stationary Redux origin, the old
build alternated between 129,752 and 129,874 packed parts. After the fix, 30
consecutive snapshots held 129,812 parts with no source removals, additions or
part-count changes. The subsequent user walking check also reported that the
flashing stopped. This checks budget stability; it does not establish hitch-free
movement or eliminate capacity and streaming limits.

## Remaining limits and live review

Headless checks do not measure GPU frame rate or validate the live stair
walkthrough. For that review, walk and turn in the Redux map, cross a stair in
both directions, inspect upper and lower walls from outside, and change
`cvar cmu.3d.distance` between 12 and 24 while moving. Close/reopen the view and
confirm ordinary 2D rendering and input return. Record resolution and GPU with
any timing results; `cvar cmu.3d.resolution 0.5` can help distinguish pixel cost
from scene/network costs.

The native renderer caps first-person scenes at 130,560 parts and 256 visible
fallback sprites. Dense views can omit detail, and new PVS entities still arrive
through the engine's streaming budgets. Lighting is sampled from the actor's
map; it is not a separate lighting solution for every floor. Map-wide clipping/model-fidelity approval and sustained focused gameplay measurements remain open.
