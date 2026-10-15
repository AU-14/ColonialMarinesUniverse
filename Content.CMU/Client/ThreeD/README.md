# Garrison 3D renderer and asset workbench

## Player controls

On **Stable Garrison Redux**, run `cmu3d` in the client console (tilde by default)
to enter first person; run it again to restore the normal view. This works for
characters and observers without administrator privileges. Each entry opens a
controls popup showing the current mouse-capture shortcut, **Alt+F8** by default.
The popup's **Change hotkey** button opens **Options → Controls → Camera**;
rebind **3D: Toggle mouse capture** there. **Start mouse look** captures the mouse
after the console closes. Escape releases it for the HUD.

`CMU3DMapComponent` is applied through Redux's `zLevelsComponentOverrides` to
all seven floors. The client closes first person when its actor leaves a supported
map. The server independently rejects/revokes player view subscriptions outside
those maps and only subscribes to marked floors. Administrator workbench access
remains separate. See [first-person documentation](../../../Tools/three_d/FIRST_PERSON_WALK.md)
for renderer behavior and current limitations.

Mobs retain their complete normal sprites, including clothing and held-item layers.
The live view does not attach 3D equipment to characters or draw first-person equipment
models. Dropped objects still use their world models where supported; item sprite
fallbacks lie flat at their physical elevation and retain pixel-aware targeting.

## Asset workbench

While connected to a server, open the client console and run `cmu_3d` to inspect the loaded `cmu3DModel`
prototypes. `cmu_3d <model ID>` selects one directly; `cmu_3d off` closes the
window. Models are available independently of the loaded map. Left-drag orbits
through 360 degrees and above/below the object; scroll zooms. The direction
selector returns to a south/front, east/right, north/back, or west/left view.
Reset frames the asset at the original south-facing angle.

The preview uses the same editable solid-part model definitions as the offline
exporter. Coordinates are X/Y horizontal, Z up, one unit per tile. Ground is
Z = 0; front is -Y. The camera stays outside the model's bounding sphere so no
preview triangle crosses the near plane. UI resizing refits the asset. The
camera also exposes an inverse projection ray for future picking.

The reference picker uses `SpriteSystem.GetPrototypeIcon` by default. Explicit
`referenceRsi` / `referenceState` / `referenceTint` fields select the intended pose
and directional texture for stateful models. It is a visual review aid: connected walls,
layered clothes, runtime recoloring and intermediate animation can differ
from prototype icons. Orbiting does not continuously change the reference;
choose a cardinal view to return to a reproducible comparison angle. Draft
status is shown from the prototype and is not automatically upgraded.

## Rendering limits

For untextured box-only models, `CMU3DModelRenderer` produces shaded triangles through `DrawingHandleScreen`.
A binary space partition splits the axis-aligned face rectangles at model load,
then traverses them back-to-front from the current camera. This keeps small
foreground details visible in front of large panels and handles crossing face
planes without using face-center depth as a proxy. Exactly coincident faces
use authored order, with later parts drawn last.

The preview has a bounded build budget: 128 input parts, 16,384 face fragments,
64 tree levels, and two million plane tests. A model exceeding the budget is
reported in the workbench and is not drawn. This is an asset renderer, not a
world renderer: there are no texture maps, skeletons, animations, or world
lighting, and the cached partition is intended for small static models.

Models with `shape: Ellipsoid`, `CylinderX`, `CylinderY`, `CylinderZ`, `WedgeY`, `WedgeYReverse`, `SlantedX`, `SlantedXReverse`, `SlantedY`, `SlantedYReverse` or a part `surface` use the same GPU intersection renderer as the local scene,
including mixed boxes and rounded parts. `CMU3DPreviewControl` supplies its existing orbit
camera to the child render control. A uniformly scaled review copy fits inside the bounded
grid; the authored bounds are not mutated. Edge outlines are disabled for this path.
The model part budget remains 128. Closed organic forms are still approximations; this
does not provide arbitrary meshes, leaf textures or animation.


Printed surfaces use `CMU3DSurfacePrototype` with a unique atlas slot in 1..4095 and an original PNG up to 256 by 256 pixels. `CMU3DSceneSurfaces` supplies matching CPU and GPU nearest-pixel sampling. Atlas cells grow to the smallest power of two fitting the loaded PNGs (up to 256); pixels are not resampled. The shader derives cell width from the actual atlas width. The current 864 images occupy a 4096 by 896 RGBA atlas with 64-pixel cells and 64 columns (14 MiB). Only occupied rows are allocated. The information texture has 4096 entries. XZ, XY and YZ projections preserve image orientation; inner-wall fixtures toggle `SurfaceFlipU`, while cutaways retain the lower image portion. The sixth packed texel stores shape in R, projection in G bits 0–1, horizontal flip in G bit 2, surface-index high bits in G bits 3–6, low index bits in B, and UV scale in A. CPU indices use ushort; the 4095 maximum is checked before encoding. The shader and picker test the exit face if an entry face is transparent. Atlas/info textures are released with the control and invalidated on live prototype reload. Textured boxes and sloped prisms route through the GPU asset-preview path. Curved textured geometry, connected textured panels, animated appearance and emissive lighting remain unfinished.

`SlantedX`/`SlantedY` and their `Reverse` variants are sheared ellipsoids within their bounds (horizontal/Z correlation +/-.85). Shape codes 7–10 extend the existing packed metadata. CPU picking and GPU rendering invert the shear before the quadratic intersection; shading uses its surface gradient. Browser, comparison mesh and GLB normals use the inverse transpose. These shapes reject source textures.

`WedgeY` is a closed triangular prism bounded by normalized `z <= y`; `WedgeYReverse` is bounded by `z <= -y`. CPU and shader clip the oriented box interval against its slope and select the appropriate face normal. Planar artwork, mirrored U and cutouts follow the same sampling contract as boxes. `GroundOffset` applies an explicit map-axis horizontal pivot correction before live geometry and support placement; it does not rotate with entity facing or mutate simulation transforms.

## Live scene workbench

`cmu_3d_live` opens a second window for an active debug administrator with a controlled
entity on a map. `cmu_3d_live off` closes it. `Scene/CMU3DLiveSceneSystem` samples nearby
replicated sprite entities and current grid tiles at 10 Hz. Exact models are preferred;
inherited matches are optional and visibly counted. Missing models use teal markers;
stable Open/Closed states select reciprocal `alternateDoorModel` assemblies, while
Foldable poses select reciprocal `alternateFoldModel` assemblies using `folded` metadata.
Unsupported transitions or missing poses use low amber markers. This does not render the
saved tactical survey. Containers, detached entities and invisible sprites are excluded.

Two door-control models provide `doorButtonStates`: explicit powered and unpowered
parts for each source frame. `CMU3DDoorButtonAppearance` reads the existing animation
and power layers at the preview's 10 Hz refresh. It does not add a second animation
timer or button-event subscription. Known RSI/state/frame combinations select the
authored geometry; additional, tinted or transformed visible layers remain unsupported.
Inside-wall variants are cached by model, source state, frame and power composition.
The model's `frameAnimations` export four portable press/denied clips; native playback
follows the live sprite owner instead. The static asset reviewer and saved-map exports
show the default pose. Interactive native playback and frame-time verification remain
unfinished; this adapter does not convert the normal gameplay viewport.

Fold-state selection preserves exact/inherited provenance without changing cached catalog
matches. The selected model supplies its own orientation, support footprint and inspector
reference; the four-direction chair therefore becomes a one-direction folded item.
Opposite-pose links must be reciprocal, and the exporter requires explicit RSI/state
references for both partners. Seven pairs cover both stable poses of 134 currently mapped
classic objects. Focused catalog tests and offline source replay pass; connected state
reception and interactive folding still need verification. No folding animation is implied.

The scene uses a GPU shader for oriented boxes, ellipsoids and capped cylinders, with a 16-by-16 cell
index. CPU picking and the shader consume matching fixed-point geometry. Entities
are admitted atomically, avoiding partially rendered models when a cell fills.
Limits are a six-tile query radius, 256 entities, 8,192 parts and 128 references per
cell. Each part occupies six RGBA8 texels, including an explicit shape byte; ellipsoid
hits and inverse-scale normals use the authored half-extents. Cylinders use shape bytes 2/3/4 for local X/Y/Z, intersect a radial quadratic with the two flat caps, and handle parallel and interior rays. Cap normals remain flat while side normals use inverse radial scale. The cached render target is at most 600 pixels along its largest axis (480
while dragging), redrawn when needed with a 30 Hz cap. This is a bounded experiment;
it does not establish large-scene throughput. Original PNG artwork supports alpha cutouts, including click-through gaps. The shader multiplies retained source alpha and part alpha for 4-by-4 ordered screen-door coverage, matching the browser and review rasterizer. Partial surfaces remain selectable across dither holes; completely clear paint passes picking through. This does not implement smooth blending or refraction.

The camera follows the actor while preserving its orbit. Actor/map changes clear the
previous scene and reset it. Losing the actor or debug permission closes the window,
clears CPU data and releases render resources. Reopening creates a fresh control.
Catalog reload and setting changes invalidate the current selection.

`build_tile_materials.py` creates native `cmu3DTileMaterial` prototypes using averaged
source colors for 461 tile types and 536 variants. Tile geometry uses the live grid's
position, tile size and rotation. Sprite-less tiles do not acquire artificial floors.

`CMU3DScenePlacement` raises `placement: surface` props onto the rotated footprint of
an admitted exact model's named `supportSurface` part, or each distinct flat Box listed in
`supportSurfaces`. Multiple board footprints preserve gaps; invalid declarations are rejected
as a whole. Multipart supports currently exclude connected geometry. It uses the tabletop's actual
height and the prop's lowest point, with deterministic tie-breaking and highest-part selection
within the same support entity. Missing supports
leave props on the floor; server coordinates and interaction rules remain unchanged.
The native view still uses averaged floor colors; original tile textures are currently
available in the browser reviewer only.

`CMU3DSceneLayout` applies source RSI direction counts with live `NoRotation` and
`SnapCardinals` settings, then the authored axis correction. `useEntityRotation`
keeps the physical axis of authored fixtures with billboard source art.
`bakedSpriteTint` removes the source tint already included in part colors before
applying relative live tint changes, avoiding double-darkening. It builds connected
panels from actual anchored `IconSmooth` neighbours in grid coordinates, with an
unambiguous wall-opening fallback for isolated panels. Fixtures facing an adjacent
wall mount inside their room tile; fixtures sharing a wall tile keep the exterior
mount. Geometry variants are cached until prototype reload. Wall fixtures follow
the cutaway. Directional sprite screen
offsets are not added to physical ground positions.

## Route to the game view

The workbench is local and does not request or read the tactical survey. It
does not replace the normal viewport, input bindings, or simulation. The
existing tactical reconstruction includes unseen static structures and cannot
be used as the source of a player-visible 3D world.

The debug adapter still needs explicit per-actor visibility filtering. Being present in client
PVS is not equivalent to being visible: entities behind nearby walls may still
be replicated. Test visibility from the controlled actor before admitting an
entity to the scene; camera rotation must never expand that visibility. Keep
fog/occlusion through doors, walls, containers, and Z transitions consistent
with the standard view. There should be no omniscient fallback.

The engine's public `IClydeViewport.FovRenderTarget` is currently backed by a shared
texture, overwritten by successive viewports, including MultiZ views. A later FOV
bridge must capture the controlled actor's completed viewport into an owned mask
at the appropriate rendering point and invalidate it when the frame, eye or map
changes. Reading the shared texture later is not evidence of actor visibility.
The viewport light target also does not include every blindness/critical-health overlay.
The player command is scoped to Redux, but complete fog/blindness parity remains
unfinished. That limitation also applies to its first-person presentation.

The first-person adapter adds animated entity appearance, roof rules,
hit testing that delegates to existing gameplay verbs, lighting and sprite fallbacks
for uncovered prototypes. Qualify the native renderer's output and performance on
representative hardware before expanding its bounds. A dedicated regression map
should exercise a hidden actor behind a door, destruction, moving entities,
containers, Z changes, and camera rotation without changing gameplay rules.

Connected support models use the same resolved parts for rendering and surface attachment. `ConnectToNeighbours` with `SupportSurface` extends only that tabletop to matching anchored tile neighbours; `OmitWhenConnected` removes internal edge trim using N/S/E/W bits 1/2/4/8. These models follow grid orientation, retain isolated insets, and never infer joins from adjacent walls. Reinforced-table support reaches .835 tiles with .002 attachment clearance.

Reinforced plasteel barricades provide four paired damage poses with and without wire. The live administrative preview reads the existing body/reinforcement and barbWired layers, checks wire layer order/resource/state/frame/tint/transform and the acided layer. The five acid frames follow the original sprite frame/visibility, with layer ordering and blank reserved-slot checks. Acid compositions use a 160-part instanced budget (maximum authored pose: 129); static/default models retain 128. Unknown visible overlays remain unsupported. No new timer or gameplay owner is added. Native installation/cutting/damage/repair/acid-removal interaction and destruction review remain open; the normal gameplay viewport is unchanged. See `Tools/three_d/PLASTEEL_STATES.md`.

## Small wall light states

Four bulb/socket assemblies expose `PoweredLightStates` for the five known Base
RSI states. `CMU3DLightAppearance` follows the existing light visualizer's actual
base state, including owner-controlled blinking. It rejects unexpected layers,
RSIs, frames, transforms and tints. No light lifecycle subscription or timer is
added. `FitInsideWall` explicitly opts these fixtures into clearance against
named modeled wall bodies from their adjacent room tiles; other fixtures keep
their existing behavior. Saved map scenes show declared reference poses, not
live power snapshots. Native interaction, emission and physical fidelity remain
unverified. See `Tools/three_d/generated/small-light-verification.json`.

## Connected window endpoint clearance

`ConnectionEndInset` opts connected panel models into a shortened free end.
Authored members touching that endpoint extend to the tile edge only where
the source neighbor mask joins another panel. Both `CMU3DSceneLayout` and the
offline layout apply this before selecting half panels; window-mount clearance
uses the same resolved parts. The two colony reinforced window/frame models
use 0.06; the default is zero. The exporter validates the supported range and
rejects use without neighbor connection or with support-surface models.
This changes presentation only. The source construction system still owns
window destruction, replacement and repair; native interactive review remains
open. See `Tools/three_d/generated/redux-window-verification.json`.

## Directional window and curtain fitting

`WindowMountTargets` can also name fixed directional glazing. `WindowMountInside`
selects the back of the saved facing, used by both shower curtain poses to keep
a 0.02-tile gap from the glass. The existing sprite owner and Door state still
select the pose. `PanelEndTargets` names exact independent wall/pane models that
define the horizontal opening; fitting happens after mounting, keeps all part
members, and uses conservative full-height wall footprints. Front/rear walls
crossing the panel pivot are excluded. Changes over 0.18 tiles per end or 30%
of the width are rejected. Perpendicular pane priority is based on grid axes,
not IDs, camera facing or enumeration order. The preview still has no curtain
cloth transition animation or native interaction approval. See
`Tools/three_d/ULTRA_WINDOW_FITTING.md` for scope and remaining contacts.

`OpeningFacingTargets` lets an anchored curtain use a co-located named fixture's
source facing only when its own opening faces an adjacent wall and the fixture
faces a clear side. Decorative showers are not anchored, so the bounded native
lookup includes uncontained fixtures and checks exact prototype, grid, tile and
distance. This changes render yaw only; the source transform and Door state stay
owned by gameplay. Both curtain poses name side glazing, wire rail and wall trim
for end fitting. Textured Box parts remain solid end supports. See
`Tools/three_d/CURTAIN_OPENINGS.md` for the two corrected openings and remaining
wall-light contacts. Native interactive verification is still open.

## Fluorescent tube states

The single, double and blue-double tube assemblies use the same
`PoweredLightStates`/`PoweredLightLayers.Base` adapter as the small bulbs.
Their five static poses use `tube`, `ptube` and `bptube` source state names.
No lifecycle owner or blink timer is added. Named backing-wall clearance also
fits these slim fixtures to projecting engineering and rock walls. Runtime
illumination, replacement art/tints and native interactions remain unverified.
See `Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_TUBE_LIGHTS.md` for
source attribution and the remaining curtain, doorway and foliage contacts.
