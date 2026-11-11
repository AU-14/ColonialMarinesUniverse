# Exact window shutter consolidation

This change consolidates the two existing draft window-shutter models without changing their occupied shapes, source color fields, mounting or animation timing. `CMU3DHybrisaWindowShutter` changes from 78 to 19 default parts; `CMU3DHybrisaWindowShutterOpen` changes from 30 to 6. Both records retain their existing metadata, links and draft status.

## Source and license

The original prototype family is defined in `Resources/Prototypes/_RMC14/Entities/Structures/Doors/Shutters/shutters.yml`. Artwork comes from `Resources/Textures/_RMC14/Structures/Doors/Shutters/Hybrisa/window_shutter.rsi`. The RSI declares **CC-BY-SA-3.0** and credits [CM-SS13 hybrisashutters.dmi at revision 789a362](https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/doors/hybrisashutters.dmi).

Derived appearance and exact source crops retain that attribution and license. No source art is repainted or resampled. The 43 distinct fully opaque crops are registered in `garrison_window_shutter_art.yml` as `CMU3DWindowShutterSurface1300` through `CMU3DWindowShutterSurface1342`. Atlas conflicts are checked before writing. Texture PNGs live under `Content.CMU/Resources/Textures/CMU14/ThreeD/Surfaces/`.

## All existing poses are retained

| Pose | Original parts | Consolidated parts |
| --- | ---: | ---: |
| Closed | 78 | 19 |
| Open | 30 | 6 |
| Opening 0 | 88 | 19 |
| Opening 1 | 73 | 15 |
| Opening 2 | 63 | 13 |
| Opening 3 | 61 | 10 |
| Opening 4 | 51 | 7 |
| Opening 5 | 30 | 6 |
| Closing 0 | 30 | 6 |
| Closing 1 | 51 | 7 |
| Closing 2 | 61 | 10 |
| Closing 3 | 63 | 13 |
| Closing 4 | 73 | 15 |
| Closing 5 | 88 | 19 |

The models share 14 pose compositions with different defaults, so all 28 stored model-frame compositions are covered. Their explicit `doorSpriteStates` remain, including every source delay. Each motion has six 0.1-second source frames and the existing one-second Door animation duration. The final source frame holds from 0.6 to 1.0 seconds. The DoorSystem Base sprite remains the frame owner; there is no second animation clock. Gameplay phase timing, saved collision transforms, interruption behavior and source controllers are unchanged.

Only the two target records' default/frame part lists are replaced. Alternate-model links, `doorState`, source mappings, references, four source directions, all 12 `windowMountTargets`, descriptions and every other metadata field are preserved. Unrelated records in the shared environment YAML are preserved byte for byte.

## Physical shape and source projection

The existing South-frame color rectangles were extruded uniformly through Y -0.0625 to +0.0625. Their exact opaque union is now partitioned into fewer solid boxes. Transparent spaces between the shutter bands remain empty volumes. The open pose retains the raised source frame; no full-height side guide or backing panel is added.

X still maps one tile to 32 source pixels. The closed height remains the inherited 2.74-tile inference mapped over 28 occupied source rows. Existing rounded Z edges are retained exactly, including the open pose's Z minimum of 2.250714286. Depth remains 0.125 tile and the mounting calculation retains the existing 0.02-tile glazing gap. Every pose's full bounds are identical to its original.

Opaque source crops use the existing XZ projection. That projection preserves the original extruded color field on fronts, reverse faces, ends, tops and bottoms. Hidden faces are not replaced with uniform gray. The stricter partition is necessary because the old Z boundaries were independently rounded to nine decimals: a simpler nine-part closed/four-part open mask partition would move some internal color transitions by up to 0.0000000006153846154 tile. Although that simpler construction matches shapes and source pixel centers, it does not preserve the complete continuous color field. The selected partition preserves those boundaries too.

The selected rectangles came from a minimum guillotine partition over source-change columns and original source-row edges. This is not claimed as the global minimum over unrestricted rectangle covers. The generator records the source rectangles explicitly and independently rechecks their preservation when authoring.

## Serialized resource verification

`Tools/three_d/author_window_shutter_consolidation.py` defaults to staging under `.codex/window-shutter-staged`. Its explicit `--apply` option writes the live resources. It does not run shared exports, a content build, a native client or a server.

The generator writes YAML and PNGs, discards the construction images, and reloads the actual serialized resources. It checks the two model records with `build_models.load_models`, validates original RSI delays, compares default parts with the corresponding stable frame, and repeats the opaque-union and source-color proofs for every pose against the actual original color rectangles. Repeat authoring after consolidation can reconstruct the original color extrusion from the checked-in source PNGs.

The exact proof uses rational arithmetic for the authored decimal geometry. Partition planes include all original/candidate solid boundaries and all proposed texture texel boundaries. Occupancy and projected RGBA are constant inside every resulting cell; all occupied cells and exposed face limits are compared. The full 32 by 32 source canvas is also checked in every pose, including every transparent sample.

The staged run passes 7,648 geometry/texel partition cells, 6,500 occupied RGBA cells, 16,752 exposed face patches, and 14,336 source samples: 6,500 opaque pixels and 7,836 empty alpha samples. Exact local equality remains equal under the existing shared rotation and mounting translation. GPU floating-point behavior and eventual GLB/atlas integration require separate checks.

Evidence: `Tools/three_d/generated/window-shutter-consolidation-proof.json`. Reviews: `Tools/three_d/generated/review/window-shutter-consolidated/`, including source/four-view cards and all serialized poses. While staged, these paths are beneath `.codex/window-shutter-staged` and the proof explicitly reports that the live resources are unchanged.

## Remaining limits

The original canonical South reconstruction and saved-facing behavior remain. North/East/West RSI images are not newly reconstructed by this consolidation. Height, depth, hidden roll construction and full side/reverse fidelity remain inherited draft inferences. Existing mounting contacts and runtime overlays are not certified by the geometry proof.

See `SOURCES_SHUTTER_MOUNTING.md` for mounting history and `SOURCES_SHUTTER_ANIMATION.md` for the authored source motion. The animation document supersedes the older mounting note that transitions were absent. Global exports, native packing and runtime verification are reported separately. A smaller part count does not by itself prove unchanged greedy admission, and this asset proof makes no packing claim.
