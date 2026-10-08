# Colony reinforced and ultra directional window drafts

This batch targets Stable Garrison Redux. All three assemblies remain drafts:
`CMU3DColonyReinforcedWindow`, `CMU3DColonyReinforcedWindowFrame`, and
`CMU3DUltraDirectionalWindow`. Definitions are in
`Content.CMU/Resources/Prototypes/CMU14/ThreeD/garrison_colony_ultra_windows.yml`.

## Source and attribution

Source RSI metadata declares **CC-BY-SA-3.0**, taken from cmss13. The adapted
geometry and source comparison images retain that attribution and license.

| Prototype | Source resource and state | Original artwork |
| --- | --- | --- |
| `CMWindowWhiteColonyReinforced` | `_RMC14/Structures/Windows/colony_rwindow.rsi`, `col_rwindow0` through `col_rwindow15` | [windows.dmi](https://github.com/cmss13-devs/cmss13/blob/6c8f3153bb8846baa74e413b3ed8d3355aebcea3/icons/turf/walls/windows.dmi) |
| `RMCWindowFrameColonyReinforced` | `_RMC14/Structures/Windows/Frames/colony_rframe.rsi`, `col_rframe0` through `col_rframe15` | [window_frames.dmi](https://github.com/cmss13-devs/cmss13/blob/6c8f3153bb8846baa74e413b3ed8d3355aebcea3/icons/turf/walls/window_frames.dmi) |
| `CMWindowUltraDirectional` | `_RMC14/Structures/Windows/directional.rsi`, `fwindow`, four directions | [windows.dmi](https://github.com/cmss13-devs/cmss13/blob/6c8f3153bb8846baa74e413b3ed8d3355aebcea3/icons/turf/walls/windows.dmi) |

The colony palette is sampled from the original sprite. The shared metal sill,
front panels and end posts persist in the broken-frame assembly, while the
eight glass bands are removed. There is no invented overhead lintel. The ultra
draft uses pale blue glazing, two reinforcing rails and short edge clips.
Physical height, depth, hidden faces and glass transmission are inferred.
The source pane pixels are mostly opaque; 3D transparency is an interpretation.
Source diagonal highlights are not fully reproduced. No fidelity approval is
implied by matching prototype IDs, sampled colors or valid GLBs.

## States and placement

The colony resources have 16 static cardinal connection states each. Their
models follow the same neighbor mask, retaining exposed ends and removing
joined end posts. The authored free ends have a 0.06-tile inset; only joined
edges extend to the tile boundary. Both native and offline layout paths apply
this rule without moving the saved entity or changing its facing.

The broken frame is a separate source replacement entity. The source construction
graph `window_colony_reinforced.yml` changes to `windowFrameColonyReinforced` on
the relevant damage/disassembly path and provides repair/rebuild edges. The
frame is not placed in the inspected maps, but is available for the live preview
to resolve when that source entity exists. Shatter, debris, repair interactions
and native runtime replacement remain unverified. These RSI states are static;
this batch adds no animation clips.

All 22 colony windows on Redux +1 and all 19 Redux surface / 14 classic ultra
windows have exact drafts. Twenty-two co-located colony shutters now mount
beside the glazing. Seven Redux and five classic ultra windows sit on their
modeled requisition desks. Ultra geometry stays within the source fixture's
horizontal footprint and clears desk rivets and co-located catwalk overlays;
its 0.036-tile base clearance and height remain inferred.

## Verification and open work

`Tools/three_d/generated/redux-window-comparison.png` compares all 32 colony
connection forms and all four ultra directions at a fixed physical render
scale. `redux-window-fixture.json` / `.glb` provide those 36 poses for orbiting.
The main saved-map scenes and 31 assembled region exports are refreshed.

The placement audit checks 77 poses (55 saved intact windows plus 22 hypothetical
broken frames) against 3,944 same-level modeled neighbors within four tiles.
All 44 colony intact/broken poses have zero recorded contacts. Ultra windows
still have 15 Redux and 18 classic contact records with wall trim, gathered
shower curtains and perpendicular windows. The two perpendicular-window pairs
are counted from both directions. These contacts remain open, with specific
entity/part pairs in `redux-window-verification.json`; unknown and cross-level
neighbors are outside this audit.

Actual GLB verification checks 111 saved export roots and 36 fixture roots,
including 6,305 part transforms and linear material colors. It covers all 55
windows and 22 dependent shutters. The 792 unrelated viewer entries and 163,185
unrelated saved scene records are unchanged. Export validity does not certify
in-game appearance: native visual review, remaining contacts, complete source
effects and physical fidelity are still required.

## Follow-up fitting

The original 33 ultra-window contact records above are now cleared by explicit
end fitting and inside-glazing curtain mounting. The window's authored default
geometry and source references are unchanged. Updated saved geometry fits the
whole assembly proportionally between named trim, retaining every frame member.
The broader audit includes all 46 curtains in both stable poses; 18 curtain
contact records remain. See `Tools/three_d/ULTRA_WINDOW_FITTING.md` and
`Tools/three_d/generated/ultra-fit-verification.json` for current scope, exports,
source attribution and remaining work. All models remain drafts.
