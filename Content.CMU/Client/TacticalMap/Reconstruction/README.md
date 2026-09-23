# Tactical map replacement in development

Standard tactical-map actions, alerts, tables and tablets now open the 3D map by default through
their existing interfaces. The CMU classic-map preference selects the original window instead.
Unsupported survey layouts fall back to that window. Separate reconstruction tables remain development
fixtures. See [the replacement plan](REPLACEMENT.md) for remaining embedded-display and map compatibility work.

The content implementation renders the linked battlefield in a normal game window.
RobustToolbox is unchanged. A content shader traces a 3D structural volume using public 2D render
targets; no native graphics calls, additional context, or third-party renderer is needed.

## Try it on Stable Garrison Redux

1. Start Redux and open the normal tactical-map action or an existing map table. The optional
   `CMUTacticalReconstructionTableGovfor` and Opfor variant remain available for isolated tests.
2. Existing access and drawing permissions apply. Table drawing requires leadership level 2;
   personal maps retain their normal `CanDraw` permission.
3. The whole map streams automatically from its initial survey. Later construction, destruction,
   tile changes and door movement do not update that geometry. The server keeps the survey for the
   map's lifetime, including across long periods with no viewers. Reopening within five minutes also
   reuses the client's terrain, camera, floor and cutaway settings; only missing chunks download.
4. Drag to pan by default; middle-drag orbits. Scroll zooms toward the cursor.
   **Top down** looks straight down at the current location. **Reset** restores the initial
   3D orientation and frames the map. Right-drag also pans while the pencil is selected.
   **Center on me** selects your floor and focuses on your current position. The CMU options tab
   enables centering on each opening by default; turn it off to retain the last cached camera.
5. Select a floor. Low walls are enabled by default; isolation hides lower floors.
   Area names are optional. Floor variants, structure directions and static prop appearances come
   from the actual map and its existing resources.
6. Pick a pencil color and width (1, 3, 5 or 8 px), then drag to draw. Releasing keeps the stroke in your local draft.
   Strokes follow continuous map coordinates across walls, props and empty terrain. There are no
   waypoint nodes, route-clearance checks, squad fields or expiry settings.
   Turn **Pencil** off to return to normal dragging. **Undo drawing** removes the latest draft
   annotation, or stages removal of the latest shared annotation when the draft is empty.
   **Clear drawings** stages removal of the faction annotations. Press **Send** to publish all edits together. Failed submissions retain the draft.

Enter marker text, enable **Text marker**, and click to place a point with a text box. Text markers join the same draft and Send operation. **Tracked icons** displays the normal authorized faction and squad feeds on the appropriate floor, including commander/SL sprites, color, medical status, fireteam numbers and vehicle occupant count. Contacts arrive with opening metadata and update independently of terrain; changed feeds are forwarded every 250–300 ms. The existing faction/sensor and publication rules still determine when intelligence becomes available.

Drawings are shared across reconstruction tables of the same faction and Z network, retaining
32 recent annotations until cleared. Each viewer has an independent camera and chunk baseline.
Opening on a planet selects the planet. Opening aboard ship selects the ship unless the player
previously selected Planet while aboard ship. Explicit ship-side choices are saved in the client's
configuration and survive restarts; ordinary planet openings do not overwrite that preference.
The **Map** selector switches between the linked planet and the current or same-faction ship.
Position and floor come from the actor, not the console. Centering is unavailable when the player
is on a different map. Switching maps preserves each map's unpublished draft within the window.
The CMU options tab's **Use the classic tactical map** option opens the original interface on the
next opening, including normal personal actions and tactical computers.
Published planet drawings share the existing faction canvas with classic maps. Fractional stroke
paths, colour, width and floor survive classic resubmission; classic canvas coordinates are converted
using the map's actual origin and inverted Y axis. Text pins share tactical labels. Overwatch canvases
remain scoped to their assigned squad and appear on that squad's personal 3D maps. Publication uses the classic faction/hive announcement and published-contact path, with announcement cooldowns. Ordinary human personal maps and ghosts remain read-only; consoles/tablets retain their normal access, range and leadership requirements. Fresh observers can request geometry and see their authorized faction drawings. A classic canvas refresh preserves unsent local edits.
Drawing waits for a refreshed baseline when
reopening a cached map. Pencil sampling compacts long strokes to at most 512 points without stopping
input or truncating the beginning of the stroke. Unchanged strokes are not resent with terrain patches.

## Scope

- This is a **static structural survey**, captured incrementally when the map is first requested,
  including then-present unseen structures. It is not a round-start snapshot or a live geometry feed.
  Tracked contacts retain the normal feed's visibility rules and continue updating.
- The structural survey does not export actors or inventories. Tracked icons come separately from the normal faction-filtered tactical feed. Orders are markers for people reading
  faction tables; they do not add pathfinding, automatic movement or squad HUDs. Sending a plan does produce the normal update announcement.
  Pencil drawings are unrestricted annotations, not navigable paths.
- Single-level map-as-grid maps and Z networks support up to eight level slots and a 1024 by 1024 tile footprint.
  Redux's full linked footprint fits this budget. Moving grids and imported meshes are not supported.
- Geometry is reconstructed from tiles, anchored structures and static scenery, not individually authored 3D models. One spatial query per surveyed chunk includes unanchored trees, bushes, rocks and static props while excluding dynamic entities and inventory. Large trees have broader rounded crowns.
  It distinguishes walls, directional doors/glass/barricades/rails, machinery, furniture, crates,
  vegetation, rocks and stairs. Props have inset footprints and independent floors underneath.
  Double doors use both facing leaves and directional closed/open sprite frames; open doors retain their jambs and header. Water entities are shallow textured surfaces. Irregular props such as chairs and beds use alpha-cutout sprite cards with an overhead representation, preserving their silhouette instead of inventing a solid box.
  Tables have separate tops and supports; rails have bars and posts; stairs have individual
  treads. Machines and crates have recessed bodies and caps, and trees have trunks and rounded crowns.
  One highest-priority structure is represented per tile. Prototype icons approximate prop appearance;
  connected corner textures supply wall and rock materials without editor icon markings.
  Live sprite layers, per-corner wall autotiling and terrain edge overlays are not reproduced.

## Data and rendering

`CMUTacticalReconstructionSystem` maintains a shared atlas per open Z network. Actual map bounds
replace the old 48-tile sector. Metadata arrives first; 16 by 16 chunks follow in batches of at most
128 every 0.1 seconds per viewer within an approximate 44 KiB payload budget. A lossless chunk palette compresses repeated cells, with raw fallback for highly varied chunks. Chunks absent at initial layout discovery are represented by a compact bitmask in the metadata, without individual chunk messages; other empty chunks carry coordinates without cell arrays. Encoded chunks are cached per revision and shared between viewers. Both extraction and transmission prioritize the operator's floor and nearby tiles. Each cell carries material, floor/structure appearance IDs and
orientation. A bounded palette references existing tile variants and entity prototypes.
The opening metadata request retries every two seconds until a baseline arrives, so closing and
reopening during subscription updates cannot leave the window waiting indefinitely. Retries stop
on receipt, an unavailable-map response, or window closure.
One partial or completed CPU survey is retained clientside for at most five minutes, scoped to the actor, table,
faction and map binding, and cleared on connection state changes. No closed-window GPU resources
are retained. A stable atlas ID and per-chunk revisions allow a reopening to adopt current metadata immediately and reuse geometry and surface textures already queued in the window. If the atlas was replaced, a refreshing window assembles a separate baseline before swapping its geometry and
palette together; contacts still update during that refresh and cached appearance IDs are never mixed with a new atlas. Camera settings survive
that swap. Building caps, vertical faces and exposed edges receive distinct shading, and labels
use measured text bounds with a smaller density budget at wide zoom.

Initial extraction uses one global budget of at most
128 chunks or two milliseconds per frame across active networks, checking the budget between rows
and committing whole chunks. Initial extraction prioritizes the operator's floor and nearby tiles.
There are no terrain change subscriptions or rolling rescans. Extraction finishes even if all viewers
close their windows, and the server retains that baseline until its map is removed. Reopening never
rebuilds it from later world changes. Completed atlases skip extraction and per-viewer revision scans.
Absent chunks on sparse floors bypass cell extraction. Contacts and drawings continue updating independently.

The client packs up to four floors across data textures (only the required columns for one to three floors) and skips empty chunks during ray traversal.
A 2048-square surface atlas contains up to 4095 actual tile/prop images. Data uploads reverse UV Y
to match Clyde's top-left `SetSubImage` convention. CPU picking uses the same bounds, footprints,
heights, cutaway and isolation rules as `reconstruction.swsl`, using coarse cell envelopes rather
than the shader's individual decorative parts. Pencil strokes intersect the selected floor plane.

Surface colors preserve prototype sprite tint and recover straight color from the alpha-composited
atlas. Warm directional light, neutral ambient light, exposed-edge bevels, seams and contact shadows
give structures depth. Distance filtering reduces texture shimmer when the whole map is visible.

The cached view target is capped at 1440 pixels on its longest side (880 while dragging). GPU uploads
are queued, including cached reopening, with at most 24 chunks or two milliseconds per frame. Empty chunks skip GPU uploads unless clearing existing geometry. Surface
icon creation is limited to four entries or two milliseconds per frame, and incremental loading redraws
the volume at most ten times per second. Labels and order markers use
native UI resolution. Camera, geometry, surface or viewport changes invalidate the cache.
Contact shadows are bounded and disabled at distant zoom. The initialized occupancy mask also guards neighbour/shadow reads, so terrain textures need no map-sized clear allocation or upload on opening. GPU resources are released with the view.

Batched edits carry a generation, request ID, bounded additions and removals. Each addition contains continuous map points, color, width, signed floor and optional plain text. The server validates the whole batch before applying any mutation and acknowledges it before the client discards its draft. The server checks open UI,
access, range, leadership, generation, current faction/network membership, finite coordinates, map bounds,
color and payload size. Terrain clearance is irrelevant to pencil annotations.

## Focused checks

```powershell
dotnet test Content.Tests/Content.Tests.csproj --no-restore --filter 'FullyQualifiedName~CMUReconGeometryTest|FullyQualifiedName~CMUReconRoutesTest'
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~CMUReconstructionTest
```

Coverage includes stacked floors, occlusion, map-edge rays, directional footprints, floors underneath
props, distant orders, frozen geometry after world changes and idle reopening, actual floor appearances, connected chunk delivery and order
authorization. Reopening checks drop the first request or metadata reply and verify recovery,
complete chunk delivery, and retry cancellation on success and closure.
Further checks cover camera retention, cached refresh staging, malformed drawing payloads, strokes
across walls and missing ground, fractional coordinate/color transport, undo, and sample compaction.
Draft/Send checks cover acknowledgement, atomic validation, widths, text markers and rejected submissions.
Extraction and contact checks cover water, facing double-door leaves, sparse chunk clearing and faction filtering.
GPU checks must emulate Clyde's upload row reversal and packed-floor layout.
Graphical verification is still required for resource appearance and navigation; these checks are
not a representative performance benchmark for target hardware.
