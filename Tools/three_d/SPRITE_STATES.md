# Source-owned sprite animation models

`spriteStates` supports complete 3D poses for an existing single visible RSI
layer. The source Sprite remains responsible for state changes, animation timing
and alpha. The native adapter reads `AnimationFrame`; it does not run another
animation clock or add a gameplay controller.

An authored model declares `referenceRsi`, `referenceState`, `sourceDirections`,
`sourceSpriteOffset`, and named `spriteStates`. Each state contains `frames`,
where each frame has a complete `parts` list, and matching positive `delays` in
seconds. The model's ordinary `parts` must equal frame zero of `referenceState`.
Four-direction resources require four reciprocal `directionalModels` and a
`referenceDirection` in RSI order: South, North, East, West. All four source delay
rows must be identical so the source frame index has one meaning.

`sourceSpriteOffset` validates the original screen offset; it does not translate
the model. Physical placement still uses `groundOffset` and the established
direction/yaw fields. `sourceCardinalFacings` uses South, East, North, West order,
which differs from RSI direction order. Preserve saved entity transforms.

The export validator accepts 1–32 states, 1–64 frames per state, and up to 128
valid parts per frame. These models require floor placement, white baked tint,
and no competing door, fold, random, powered-light, barricade, mounting,
connectivity, support or terrain-cutout adapter. These limits keep the selected
composition unambiguous; they are not a promise that every scene fits the renderer.

Audited rotating sources can opt in with `sourceSpriteRotates: true`. This allows
floor or surface placement, requires `NoRotation: false` and `SnapCardinals: false`,
and rejects granular rendering, post shaders and shader parameter copies. Every
directional sibling must declare the same rotation contract. The console variants
use this path: their physical body follows saved yaw once, and their selected
directional artwork follows the existing source frame. Tables supply the usual
surface support; this does not add a second animation clock.

A rotating surface model with exactly one state and one frame may also use
`backWallMountTargets`. This bounded static case checks its rear face against
explicit neighboring walls at the actual supported height, then looks up support
again at the corrected pivot. A height/footprint cycle that does not converge in
four attempts retains the original source sprite. Every directional sibling uses
the same targets. Multi-state and animated compositions retain the
incompatible-adapter fallback.

Without that explicit rotating-source opt-in, the native adapter requires
`Sprite.NoRotation`. Both paths require unit scale, zero sprite-local rotation, the
expected offset, and exactly one visible untransformed white RSI layer with no
texture, shader or direction override. Unknown states, overlays or mismatched
resources/frame counts retain the existing fallback. Overall Sprite tint and
alpha multiply the selected composition, preserving the source fade value.
Accepted sprite-state entities are queued through the existing SpriteSystem
update path so replacing the ordinary world draw does not freeze their RSI clock.

Saved-map export proves the default/saved layer and selects frame zero. Saved
appearance data that requires a live visualizer is not guessed. Exported regions
carry the selected state, reference direction, geometry variant and original
position/yaw. They are static studies, not simulations.
An explicitly empty `layers: []` with a top-level state initializes one source
layer, matching `SpriteComponent.AfterDeserialization`; an empty layer list with
no state or texture remains unsupported.

Dropped clothing can reserve an empty `enum.WebbingVisualLayers.Base` layer.
The saved-map adapter ignores that placeholder only after proving that its
`WebbingClothing` owner has no attached or starting webbing and its configured
container is empty. Unknown placeholder owners, added artwork and visualizer
overrides retain fallback. The map reader retains saved `WebbingClothing` and
`CMUItemStain` fields; any non-null stain color requires the original layered
sprite. Native playback already skips resource-less layers and rejects additional
visible artwork. These mappings cover the loose world icon, not worn clothing.
For `ToggleableVisuals` items, activated `HandheldLight` or `ItemToggle` owners
also require fallback: an initially hidden lamp layer is not proof that the
loaded source owner will leave the light off. Both light/visual components are
retained by the map reader; native playback continues to read actual layers.

The audited opaque Dollar stack is an explicit saved-map exception to reading
the inherited layer's initial state. `stack_states.py` applies the existing
`StackSystem` threshold sequence and equal-level selection to the saved/default
count. It validates the mapped base layer, Dollar maximum and every authored
state before choosing geometry. This fixes denominations whose inherited layer
starts as `spacecash` before the owner initializes it. Unsupported owners,
composite layers, nonpositive counts and saved appearance overrides retain
fallback. Native rendering still reads the owner's actual RSI state/frame;
the exporter does not simulate cash spending, merging or gameplay events.

The four-state opaque CMSteel and RMCPlastic stacks use the same audited source
owner's equal-level path with `layerFunction: None`. Their maximum counts come
from the actual stack prototypes, with saved overrides honored. Other stack
types, extra visualizer owners and unsupported layers still retain fallback.

Each multi-frame library state exports a looping glTF STEP clip with the original
RSI intervals and a final return to frame zero. A one-frame state gets a separate
static glTF scene rather than a synthetic clip. The source comparison player is
[`viewer/sprite-animation.html`](viewer/sprite-animation.html); select the model,
facing and state, then play, pause, step or scrub beside the exact source image.

The Hybrisa overhead cabinets are the first users of this contract. Their off
resources are represented without inventing a power switch. Their inferred depth,
height and hidden construction remain draft decisions; see
[`SOURCES_OVERHEAD_MACHINERY.md`](../../Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_OVERHEAD_MACHINERY.md).
Offline frame, transform and packing checks do not establish live gameplay,
complete 3D fade/occlusion behavior or user acceptance.

Disposal junctions use exact reciprocal `anchored` / `alternateAnchorModel` poses.
The installed pose follows the original `AnchorVisuals.Anchored` pipe state and
opens a finite service channel; its `preserveSlabCladding` flag retains original
grates above that channel. The loose pose uses the construction state above the
floor with no aperture. Offline visibility derives from `SubFloorHide`, the
effective transform and resolved tile `isSubfloor`; it never rewrites source
components or gameplay tiles. Native selection also requires the actual source
layer to agree, otherwise the original sprite remains. Scanner-revealed gameplay
and unseen channel construction are not fidelity approvals.

The recharger uses a separate bounded `chargerAppearance` composition for its
base, six indicator states and the original taser/baton ItemMapper layers.
Native playback reads their actual state/frame; saved exports prove the known
MapInit owners and contained battery/tag data. The comparison page reuses the
source-frame player for 32 compositions and four versions of the same original
full-charge blink. Unknown owners, layers or transforms retain fallback.
Indicator color and timing are preserved, but the original unshaded illumination
has no per-part equivalent in the current 3D renderer.
