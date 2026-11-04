# Fixed containment walls — 2026-09-25

Seven draft assemblies cover all 46 saved containment wall pieces in Stable Garrison
Redux level -2. The three cells use six south pieces, eight north pieces, ten west and
ten east pieces, six corners, and three each of the west/east northern junctions.
These are fixed source pieces; they have no IconSmooth connectivity. Each mapping
keeps the saved entity position and rotation, including the three southwest corners
turned by -90 degrees. Pale cladding faces outside; brown service panels face inside.

Original artwork: `Resources/Textures/_RMC14/Structures/Walls/containment.rsi`,
**CC-BY-SA-3.0**, taken from cmss13:
https://github.com/cmss13-devs/cmss13/blob/09a5191fb11aab8ddffe3f9be94292b53e4d96f6/icons/turf/almayer.dmi
The original RSI metadata remains authoritative. The derivative models and three
`CMU3DContainment*` surface crops retain this license and attribution.

`Tools/three_d/author_containment_walls.py` authors the model/surface YAML, original
pixel crops and source/four-view review cards. Surface rectangles in source pixels:

| Surface | Source state suffix | Crop (left, top, right, bottom) |
| --- | --- | --- |
| Vent face | `south` | `(1, 2, 31, 8)` |
| Northwest inspection hatch | `connect_w2` | `(13, 2, 24, 11)` |
| Northeast inspection hatch | `connect_e2` | `(8, 2, 19, 11)` |

All referenced states have one static frame; the corner provides four source directions.
The invincible wall source declares no damage appearance, door behavior or animation.
No animation clips are invented for these pieces. The neighboring pod doors and shutters
remain separate entities, on their original saved planes and with their existing models.

The full one-tile collision footprint is preserved. Recessed white panels, brown cabinets,
equipment rails, vents, pale caps and green markings follow the source palette and cell
context. Height up to 2.8 tiles, unseen side elevations, cap construction and panel depths
are inferred. These are unfinished drafts and have no fidelity approval.

Each assembly has 49–62 solids. Shared frame rails, a continuous core and the cap replace
overlapping backing and lip solids. Detailed cladding is scaled to 99% in plan to leave a
narrow panel seam inside the native renderer's conservative spatial-grid padding; the base
and cap retain the full tile footprint. This is a renderer-budget refinement, not a change
to gameplay collision. The initial 112–120-solid drafts fit alone but exceeded cell budgets
when assembled beside floors, roofs, lights and neighboring walls.

Only the seven placed wall prototypes are mapped here. Off-map containment window variants
and other junctions have distinct source states and remain unmodeled. In particular,
runtime-spawned window descendants may still resolve to an opaque inherited base candidate;
that is not a verified window model. No such descendant is placed in the saved target maps.

Review evidence is under `Tools/three_d/generated/`: the
`containment-redux-minus2-scene.json` saved scene, `review/containment-walls/` source cards,
`containment-source-placement-audit.json`, and the containment audit/region exports.
All 46 saved transforms and interior-facing normals match the three cell layouts;
8,828 unrelated instances, 9,438 floor tiles and prior layout variants remain unchanged.
The three texture crops preserve all 378 source RGBA pixels.
Actual GLB exports verify 46 saved roots and 2,464 part transforms across the three cell
regions. All 827 individual models and these three region exports pass Khronos validation
with zero errors/warnings. The native library-budget test and 205 Python checks pass.

`containment-native-budget-audit.json` records 216 sampled cases through the actual native
encoder: three room origins, 36 sub-tile offsets and wall-only/mixed-context inputs. Final
geometry has no omitted containment walls and introduces no fixture omissions versus the
no-containment control. Inputs use exact mapped static geometry within radius 12 and
conservative ceilings on all saved nonempty tiles. Existing unrelated omissions are not
certified fixed. Browser review of the turned southwest corner and northern junction shows
the intended exterior/interior placement, with no warnings or errors.

Native interactive fidelity, broader collision review and reconstruction of upper/lower
stories remain open. The current running native client needs a later restart to load the
new asset prototypes.
