# UNBOUND topology physical studies

Seven standalone, editable draft assemblies. Every definition has `sourcePrototypes: []`.
They add **zero exact prototype bindings and zero saved-map coverage**. These assets do
not make the seven topology-limited targets complete, playable, fitted, or renderable
in the native scene. No engine/runtime code or prior model was changed.

Source commit: `6e37a4d0a7d9433838c393a82c02422cb704dd5a` in
[CMU-Garrison-3D](https://github.com/TheHellFireo/CMU-Garrison-3D/tree/6e37a4d0a7d9433838c393a82c02422cb704dd5a).

## Deliverables

Canonical editable definitions are `garrison_topology_studies_cloud.yml` and
`garrison_topology_studies_cloud_art.yml` in the CMU14 ThreeD prototype directory.
The separate `topology_studies_cloud` texture directory owns 15 unchanged local
source crops using reserved atlas slots. Each model exports as its named GLB here.

| Model suffix after CMU3DTopology | Reference | Physical study | Parts |
|---|---|---|---:|
| StairsFlightStudyCloud | CMUMultiZStairsFlight / rampbottom / direction 0 | Four source-banded beveled tread plates on two real inclined stringers | 98 |
| StairsNoPreviewStudyCloud | CMUMultiZStairsNoPreview / p_stair_full / direction 0 | Four full-width pale tread plates on two real inclined stringers | 28 |
| CargoLoweredStudyCloud | CMCargoElevator / supply_elevator_lowered / direction 0 | 5×5 shaft, descending walls, guides/brackets, machine housings, ledges, open center and bottom | 99 |
| CargoRaisedStudyCloud | CMCargoElevator / supply_elevator_raised / direction 0 | 25 independent deck modules on crossing support beams and four jack supports | 98 |
| ChasmIsolatedStudyCloud | FloorChasmEntity / full / direction 0 | Twenty irregular source-traced cliff segments enclosing an unfilled pit | 80 |
| ChasmStraightQuarterStudyCloud | FloorChasmEntity / chasm_1 / direction 1 | One half-tile straight north quarter edge | 24 |
| ChasmCornerQuarterStudyCloud | FloorChasmEntity / chasm_0 / direction 1 | One curved quarter edge, seven source-traced segments | 28 |

The two elevator studies share the source artwork used by CMCargoElevator,
CMCargoElevatorGovfor, AU14CorporateASRSElevator and VehicleLift. They are not four
new mappings and are not state-bound partners. They implement no source animation.

## What is source-established, and what is inferred

### Stairs

The inspected South frame in each source contains **four repeated eight-pixel bands**.
That pixel evidence establishes the four authored tread groups. It is independent of
the four values in CMUZLevelHighGround.heightCurve. Both RSI resources have four
source directions; these studies explicitly reference only RSI direction 0. Other
facings have not been bound or claimed as reviewed models.

X/Y footprint follows 32 source pixels per tile. The four physical plate elevations
are 0.16, 0.32, 0.48 and 0.64 tile, with 0.095-tile plate thickness. Those rises,
stringers, bearing shoes, backside and load-bearing construction are art inferences.
The logical curve `[1.05, 1.05, 0.575, 0.1]` is **not a verified world-metre rise**
and has not been converted to physical world elevation.

Flight's Sprite.Color `#a6aeab` is multiplied into the source colors exactly once.
The model records both referenceTint and bakedSpriteTint, and the reference image
uses that tint once. Pale full stairs have no tint. Narrow top/nose/edge bands have
real backing plates and source-selected paint; there is no whole-stair sprite plane.

### Elevator

The original 160×160 lowered picture is a projected illustration of pit walls,
three ribbed bands, machinery, CARGO lettering and lattice detail. The 5×5 footprint
comes from the sprite pixels. The square wall interpretation, 2.4-tile shaft depth,
0.20-tile walls, heights, unseen walls, guide mounting and rearrangement of projected
bands onto upright interior walls remain inferences. Small label/grate crops and a
local diagonal CARGO stencil remain unchanged pixel crops; their physical mounting
and projection transfer are interpretive. There is **no center floor or black disk**.
The review section hides the front half for inspection only; the exported model
retains all four walls.

The raised source has a 5×5 panel grid: 21 dark checker/grate modules and four yellow
service hatches. The latter each have four independently editable plate quadrants.
Thick individual cassettes, crossing underside box beams, shoulders and jack stems
provide physical depth and support. There is no single 160px whole-sprite plane.
Original 32px local grate crops and 16px hatch crops retain the surface pattern.

**Fine checker/grate patterns are original opaque face artwork, not individually
modeled through-perforations.** Source pixels are fully opaque. Macro shaft/chasm
opening tests do not establish fine grate openings. Deck thickness, underneath
construction and jack positions are inferred. Source raising/lowering resources
have 20 frames each and are not authored or played by these static studies.

### Chasm

The original opaque cliff colors are `#302F33`, `#242329`, and `#17161B`.
The near-black `#070709` area is treated as unseen void, not solid geometry.
Much of the apparent pale blue-gray source outline is `#C7C7C7` at alpha 64 or 25
composited over the review background. Black partial-alpha fringes also occur.
Those pixels are **not sampled as opaque blue-gray rock paint**. Their atmospheric
fringe is not reproduced as physical stone.

The isolated `full` resource is an art reference, not the entity's Icon component:
FloorChasmEntity's actual Icon points to liquid-phoron/full. The runtime Sprite
uses chasm.rsi and IconSmooth. The two quarter studies retain the selected direction
frame's half-tile quarter placement; they do not expand the quarter to a full tile.

Twenty rim segments for the isolated pit and selected quarter-edge contours are
traced from original opaque boundaries. Real wall volumes and tapered rock tongues
provide depth. Hidden cliff thickness, downward extent, fracture facets and rear
faces are inferred. They are not terrain topology, collision, or gameplay geometry.
The real source IconSmooth family has **eight states × four quarters**. An isolated
pit and two quarter edges do not implement that neighbor-state space. Empty centers
and open bottoms are genuine geometry properties, not dark cap textures.

## Boundary that remains blocked

- No seven target IDs have a sourcePrototypes binding in this work
- Current native slab openings are limited to single rectangular source-tile bounds
  within ±0.5, require a single source state/direction, and reject IconSmooth and
  cornerSurfaces
- Multi-tile 5×5 elevator shaft apertures are unsupported
- Native scene presentation does not consume HighGround logical Z as world elevation
- Chasm all-neighbor compositions, edge/corner transitions and terrain aperture
  integration remain unsupported
- Elevator stable-pose runtime selection, raising/lowering animation, state changes,
  collisions, traversal and multi-Z gameplay integration remain unsupported
- No native/map/gameplay/Khronos/fidelity approval is claimed

## Verification and review

Run `Tools/three_d/author_topology_studies_cloud.py` to regenerate only this family's
owned definitions, local crops, seven GLBs and source/plan/orbit/section previews.
Run `Tools/three_d/verify_topology_studies_cloud.py` for source SHA, selected-frame
palette, once-only tint, unchanged crop, atlas reservation, no target binding,
connected assembly, macro empty-space and deterministic-export checks.
Run Blender with `Tools/three_d/check_topology_studies_blender.py` for independent
imports, finite vertices, editable-part counts and zero mesh-repair checks.

The source audit verifies original git blob SHA values and exporter implementation
against the pinned commit. The exporter remains unchanged, including its pre-existing
extra terminal newline. GLB bytes are reproduced twice and compared to disk.
Connection evidence uses exact overlap for unrotated boxes and inverse-primitive
membership sample witnesses for rotated boxes, wedges and cylinders. It is a
bounded isolated internal contact check, not a universal collision proof or map fit.

Review outputs: `Tools/three_d/generated/review/topology-studies-cloud/`, especially
`topology-studies-source-orbit-section.png`. Plan and orbit images are normalized
for inspection and are not projected sprite-similarity scores. The following audit
files sit beside that review directory:

- `topology-studies-cloud-verification.json`
- `topology-studies-cloud-source-audit.json`
- `topology-studies-cloud-blender-import.json`
- `topology-studies-cloud-delivery-manifest.json`

## Attribution

All three source RSI families declare **CC-BY-SA-3.0**. Retain their source metadata
and these credits with original texture crops and derivative geometry.

- stairs.rsi: taken from cmss13, commit
  `8e8d26bbb4f1617ea1b1ffc17ffffca552ce8c11`, icons/obj/structures/stairs;
  rampbottom from icons/obj/structures/structures.dmi
- elevator.rsi: taken from cmss13, commit
  `0d55a8ab1eb56d87e94ea65b60e79b1dfea3722a`, icons/effects/160x160.dmi
- chasm.rsi: created by TheShuEd (GitHub), from
  https://github.com/crystallpunk-14/crystall-punk-14/pull/290

Authoritative pinned source metadata links:
[stairs](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Structures/stairs.rsi/meta.json),
[elevator](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/_RMC14/Effects/elevator.rsi/meta.json),
[chasm](https://github.com/TheHellFireo/CMU-Garrison-3D/blob/6e37a4d0a7d9433838c393a82c02422cb704dd5a/Resources/Textures/Tiles/TileEntities/chasm.rsi/meta.json).
