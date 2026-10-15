# Grey SPP reinforced windows and broken frames

Draft checkpoint: 2026-09-25. Stable Garrison Redux is the primary map.

`garrison_spp_grey_windows.yml` defines `CMU3DSPPGreyWindow` (60 parts) and
`CMU3DSPPGreyWindowFrame` (48 parts). These map exactly to
`RMCWindowSPPReinforcedGrey` and `RMCWindowFrameSPPReinforcedGrey`. The window
occurs 138 times on Redux level -2; the frame is an off-map replacement, not
an additional saved-map coverage claim.

## Original sources

- `Resources/Prototypes/_RMC14/Entities/Structures/Windows/spp_windows.yml`
- `Resources/Prototypes/_RMC14/Entities/Structures/Windows/frames.yml`
- `Resources/Prototypes/_RMC14/Recipes/Construction/Graphs/Structures/window_spp_reinforced_grey.yml`
- `Resources/Textures/_RMC14/Structures/Windows/spp_grey_window.rsi`
- `Resources/Textures/_RMC14/Structures/Windows/Frames/spp_grey_frame.rsi`

Both original RSIs declare CC-BY-SA-3.0, with attribution to cmss13-pve:
<https://github.com/cmss13-devs/cmss13-pve/blob/0afcb527e30cb9d24151005c28f095d704b24c83/icons/turf/walls/upp_grey_windows.dmi>.
Preserve those declarations when redistributing the source-derived material.
No external reconstruction service or generated texture was used.

Each RSI contains 16 single-frame, single-direction cardinal states.
`IconSmooth` uses the `walls` key and `CardinalFlags`; anchored neighbors on
the same grid determine the state. All 138 saved masks are independently
checked: 47 north/south, 87 east/west, and one of each single-ended state.
The construction graph has intact and frame nodes; window destruction can
replace the intact entity with the frame. This is a distinct prototype,
not a glass pane with a damage tint. The runtime breakage interaction has
not been visually verified.

## Geometry and state behavior

Original gray/brown steel colors form the ribbed lower sill and free-end
frames. The intact model adds ten source-color/alpha glass bands and two
teal glass-edge lips. Comparison removed those two lips from the broken
frame because the corresponding source pixels are transparent. The frame
retains 48 shared structural parts and an open center, without an invented
top lintel.

The connected-panel layout now rotates end-trim omission flags with each
panel's axis. Joined ends omit trim; corners and junctions use clipped
half-panels. All 16 masks have Python/native regression coverage. The
32-state comparison includes both assemblies. Straight-pane probes hit
intact glass and clear the broken frame above its sill.

The one-tile span follows saved tile connections. Height 2.6, sill height
0.65, depth and hidden construction are inferred, not recovered dimensions.
Existing saved positions/rotations and screen-to-world placement rules are
preserved. Shattering, flying shards, repair motion and animation clips are
not implemented. Source static states are not an animation-completion claim.

## Context and verification

All 1,902 prior definitions, 776 prior viewer model records, floor tiles,
8,736 unrelated level -2 entity records and prior connected geometry remain
unchanged. All 138 target connection states agree with saved neighbors.

The conservative contact audit records 127 entity pairs / 6,892 part pairs:
78 shutter pairs, 47 wall pairs, one overhead pipe and one tree. Shutters
share saved tiles with glazing; existing wall trim projects beyond tile
edges. These contacts remain unresolved. No arbitrary map relocation is
used to conceal them, and no fidelity approval is given.

143 Python tests, 156 isolated native checks and the client build pass
(zero errors, 2,150 existing warnings). The final library budget check also
passes after frame refinement. All 778 deterministic individual exports
and 314 assembled GLBs validate with zero glTF errors/warnings. Fifteen
prior regions are refreshed, sixteen saved regions and the 32-state fixture
are added; actual GLB node IDs cover every saved window.

Both source/four-view cards, all original/model connection comparisons,
the 32-form browser fixture and saved entities 9072/9107 were inspected.
The frame inspector uses its actual resolved prototype and original state.
Browser console errors/warnings are zero. Native interactive destruction,
frame-time review and the standard gameplay viewport conversion remain open.

Evidence: `Tools/three_d/generated/spp-grey-*` and
`Tools/three_d/generated/review/spp-grey/`.

Later checkpoint: `SOURCES_SHUTTER_MOUNTING.md` clears all 78 shutter/glass pairs recorded here. The 47 window/wall contacts, pipe and tree contacts are not cleared by that later pass.
