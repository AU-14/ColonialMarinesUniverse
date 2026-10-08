# Hybrisa overhead cabinets — 2026-09-25

Three source designs have six normal/flipped placement groups and four explicit
RSI direction records per group: 24 draft model records. Seven exact source
prototype mappings cover 35 newly authored Redux placements and eight existing
Big11 companions (six Redux and two classic), for 43 saved entities in total.

| Source prototype | Design | Saved Redux | Saved classic | Parts per frame |
| --- | ---: | ---: | ---: | ---: |
| `RMCMachinePropBig11` | 11 | 6 | 2 | 14 |
| `RMCMachinePropBig11Flipped` | 11 | 10 | 0 | 14 |
| `RMCMachinePropBaseOverheadFlipped` | 11, shared flipped model | 4 | 0 | 14 |
| `RMCMachinePropBig12` | 12 | 7 | 0 | 14 |
| `RMCMachinePropBig12Flipped` | 12 | 4 | 0 | 14 |
| `RMCMachinePropBig13` | 13 | 6 | 0 | 17 |
| `RMCMachinePropBig13Flipped` | 13 | 4 | 0 | 17 |

## Source and license

Prototype definitions are in
`Resources/Prototypes/_RMC14/Entities/Structures/hybrisa_machine_props.yml`.
Original artwork is in
`Resources/Textures/_RMC14/Structures/hybrisa_machine_props.rsi`.
Its metadata declares **CC-BY-SA-3.0**, taken from cmss13:

[Original Hybrisa 64x64_props.dmi](https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/structures/props/hybrisa/64x64_props.dmi).

That URL follows `master`, as recorded by the source RSI; it is not a pinned
source revision. The checked-in PNGs and RSI metadata are the actual authoring
inputs. Derived geometry and pixel crops retain the original attribution and
license. No source images are repainted or resampled in the texture assets.

The six referenced resource states are `buildingventbig11`, `buildingventbig12`,
`buildingventbig13` and their `_off` variants. Frames are 64 by 64 pixels with four
directions in RSI order **South, North, East, West**. The generator partitions the
source faces into semantic grille, control, spine and side-pod regions. Reassembling
those crops reconstructs all 72 original directional/state frames byte-for-byte,
including their transparent canvas: 294,912 RGBA canvas samples. Identical crops
share 68 surface textures at atlas slots 1100–1167.

## Geometry and placement

Design 11 has two upper grille banks, a lower circular control housing, a rear
service spine, a separate narrow side control pod and a connecting tube. Design
12 has a tall case, perforated upper vent, two raised copper cartridges, paired
indicator bank and two lower rectangular grilles. Design 13 preserves its distinct
cutaway side casing and missing side-pipe pixels instead of treating it as another
label for design 12. Solid case sections, cylindrical housings/cartridges and
separately recessed front surfaces provide physical volume. Rear faces are inferred.

The cabinet front is authored toward local -Y. Source South/East artwork is mapped
to a physical east-facing cabinet, and North/West to a west-facing cabinet, placing
the controls toward the aisle in the saved wall-side contexts. The model field
`sourceCardinalFacings: [1,1,3,3]` uses its own **South, East, North, West** indexing;
it must not be confused with RSI direction order. Each directional record has an
explicit `referenceDirection` and reciprocal `directionalModels` group. Artwork
mirroring inside a source frame does not trigger a second whole-model mirror.

The normal source Sprite offset is `[0.25,0.5]`; flipped variants use `[-0.25,0.5]`.
Flipped changes this offset only, not sprite scale or state. Each model records the
exact expected offset in `sourceSpriteOffset`. Its ground-X offset is
`(alphaBoundsCenterX - 32) / 32 + sourceSpriteOffset.X`. Ground Y remains zero.
The source screen-Y offset is not turned into a half-tile ground displacement.
No cabinet is moved or snapped to a wall based on neighboring geometry.

| Design | Source direction | Normal ground X | Flipped ground X | Physical facing |
| --- | --- | ---: | ---: | --- |
| 11 | South / East | -0.234375 | -0.734375 | East |
| 11 | North / West | 0.734375 | 0.234375 | West |
| 12 / 13 | South / East | -0.3125 | -0.8125 | East |
| 12 / 13 | North / West | 0.8125 | 0.3125 | West |

Source image width becomes physical face width along the aisle, while the shallow
cabinet depth lies across the aisle. The inferred vertical span is Z 0.10–2.10.
Designs 12/13 have maximum depth 0.291 tiles. Design 11's 33-pixel alpha width would produce a
1.03125-tile face at 32 pixels per tile. Two saved corner placements (-2 UID 2673
and +1 UID 4557) then intruded 0.015625 tiles into their south wall fixture.
Its physical face width is therefore uniformly scaled by 0.93 to 0.9590625 tiles,
a 7% compression that also clears the protruding Kutjevo rock-face geometry at
UID 4557. This applies to casing, pod, face
details, all four directions, normal/flipped variants and every state/frame.
Design 11 is a shallower control cabinet: its inferred local depth is scaled by
0.54 to 0.15714 tiles. This clears the lateral platform fascia and inward-projecting
top cap under its shifted North off-state at UIDs 2667 and 2668. The platform reaches Z 0.39; the cabinet is
not lifted above it. The full source-state shift remains 0.5 tiles because depth
is compressed around the case center before that translation is applied. The
inferred mounting base is Z 0.10 and height remains two tiles.
Textures and saved transforms are unchanged. Designs 12 and 13 use 0.875-tile
face widths. These widths, depths, heights and mounting construction remain
explicit reconstruction choices, not measured dimensions recovered from the sprite.

The overhead base is anchored, clickable and anchorable, with `SpriteFade` and
Overdoors draw depth. It declares no Physics or Fixtures. These models add no
collision or gameplay machinery. Some saved cabinets are freestanding; the physical
facing map must not be replaced by a universal wall-mount rule.

## States and source animation

Each directional model provides `spriteStates`, where every named source state has
complete `frames[].parts` and matching `delays`. The default `parts` exactly equals
frame zero of the referenced on-state. The actual source timings are:

| Design | On-state frame delays in seconds | Cycle |
| --- | --- | ---: |
| 11 | 0.25, 0.25, 0.25, 0.25, 0.25 | 1.25 s |
| 12 / 13 | 0.4, 0.25, 0.4, 0.25, 2.5 | 3.8 s |

All five intervals are retained even where pixels repeat: designs 12/13 have
identical on frames 0, 2 and 4. Frame changes affect small indicator regions,
not the case silhouette. The source on-state South/East frames are identical;
North/West are identical and are exact horizontal mirrors of South. Explicit
directional records still retain the source slot and actual frame data.

Each off state is a single static resource frame. Off 12/13 have the same direction
aliases. Off 11 has real exceptions: East moves the South image +16 pixels;
North's alpha bounds are `[15,0,48,64]`, rather than the on-state's `[31,0,64,64]`,
and 134 opaque RGB pixels differ from the mirrored South crop. The authoring uses
the actual four off images. Per-frame canonical depth translation produces the
measured +0.5-tile East / -0.5-tile North world-X shifts without altering the saved
entity transform or silently replacing those images with a generic off pose.

The default prototype states animate automatically through the source Sprite.
No power, Appearance or toggle controller is declared for this family. Off-frame
resource support does not introduce or claim such a controller. Live model playback
must follow the original visible layer's animation frame and retain source alpha,
including SpriteFade's 0.4 target alpha. Native validation and fallback behavior
are the parent task's responsibility; the asset generator alone cannot
establish that those live behaviors work.

## Ownership and verification

`Tools/three_d/author_overhead_machinery.py` owns
`Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_overhead_machinery.yml`,
`garrison_overhead_machinery_art.yml`, the `CMU3DOverheadSurface*` textures and this
source family. The existing `CMU3DRMCMachinePropBig11` ID is preserved; only its
record was removed from `garrison_machinery_debris.yml` and replaced in the new
family file. Other models in that shared YAML are unchanged.

The generator validates every frame composition, checks each crop against its
original RGBA bytes, checks palette membership for untextured solids, and writes
source/four-view cards under `Tools/three_d/generated/review/overhead-machinery/`.
The `off-states/` subdirectory contains 12 additional source/four-view cards,
one for each design and RSI direction using the actual static off-frame parts.
These are review-only copies; library IDs, defaults and schema are unchanged.
The standard cards crop source padding and fit the model to their panels, so the
half-tile off-state translations are established by the saved-context geometry
audit rather than by the card's centered framing.
`Tools/three_d/generated/overhead-machinery-source-audit.json` records the exact
frame/crop evidence. Saved source evidence comes from
`Tools/three_d/generated/next-overhead-machinery-audit.json` and
`.codex/overhead-source-state-audit.json`.

The family review directory also contains `context-audit.json` for all 43 saved
placements and seven saved-area image sheets. Contacts are checked against frozen
resolved neighboring scene geometry, including platforms, lattice, catwalks,
lights and pipes. These are conservative part-bound contacts: transparent source
patches and curved solids can report overlap through empty space. All 43 saved
placements have zero such contacts in both their on and off state against the
modeled neighbors in the audit. On-frame geometry bounds remain constant across
all five animation frames. The smallest relevant positive clearances, in tiles,
are:

| Cabinet UID | Modeled neighbor | Minimum part-bound separation |
| --- | --- | ---: |
| 2667 / 2668, off North | PlatformThree inward top cap | 0.002835 |
| 4557 | Kutjevo rock border, UID 31234 | 0.0014687 |
| 2673 | SPP grey wall, UID 8884 | 0.0204687 |
| 18117 | Co-centered light, UID 16984 | 0.16875 |
| 10300 | Co-centered overhead fuel line, UID 10504 | 0.397492 |

The cap reaches local X -0.35; the shifted cabinet front reaches X -0.347165.
These small positive gaps avoid current geometry intersections without moving
saved placements. They do not establish a robust clearance against future changes
to neighboring draft geometry. The complete per-neighbor evidence is retained in
`context-audit.json`. Unknown neighboring models remain listed as unknown.
`family-state-pair-audit.json` additionally checks every same-map/level cabinet
pair in every on/off combination: 1,020 checks, no contacts, minimum separation
0.595 tiles. Context images use a labeled flat reference floor and existing
draft neighbor geometry. Neither check establishes clearance against objects
whose geometry is not yet modeled.

Visual inspection of the source cards and seven saved-area sheets found the
grilles, side pods, mirrored controls and design-13 side notch recognizable.
Wall-side controls face the aisle; the freestanding pair at UIDs 4559/4560 faces
opposing aisles and leaves the catwalk visible. The tube light above the cabinet
and the depth-separated overhead fuel pipe remain visible in their respective
contexts. Static off cards preserve the dark indicator artwork and off-North
design-11 side shading. Shallow profiles, plain rear faces and inferred mounting
still require fidelity review; these images are not a user approval.

All cabinet models remain drafts. Native packing, full assembled exports, live
source-clock selection and source-alpha preservation require the parent task's
final verification. No game/server launch or user fidelity approval is implied by
the source-crop, orthographic rendering or geometric checks in this asset batch.
