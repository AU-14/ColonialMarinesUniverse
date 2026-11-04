# Wide Hybrisa machinery drafts

Four distinct source designs cover 12 saved Stable Garrison Redux placements. Each model remains a draft. The source faces and animation frames are retained exactly; depth, height, rear surfaces and mounting remain inferred from one-direction artwork.

| Model ID | Source prototype | Redux placements | Parts per frame |
| --- | --- | ---: | ---: |
| `CMU3DRMCMachinePropBig3` | `RMCMachinePropBig3` | 4 | 12 |
| `CMU3DRMCMachinePropBig5` | `RMCMachinePropBig5` | 1 | 21 |
| `CMU3DRMCMachinePropBig6` | `RMCMachinePropBig6` | 3 | 12 |
| `CMU3DRMCMachinePropBig7` | `RMCMachinePropBig7` | 4 | 28 |

## Source and license

The prototype definitions are in `Resources/Prototypes/_RMC14/Entities/Structures/hybrisa_machine_props.yml`. Original artwork is in `Resources/Textures/_RMC14/Structures/hybrisa_machine_props.rsi`. Its metadata declares **CC-BY-SA-3.0**, taken from CM-SS13:

[Original Hybrisa 64x64_props.dmi](https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/structures/props/hybrisa/64x64_props.dmi).

This URL follows `master`, as recorded by the RSI; it is not a pinned source revision. The checked-in PNGs and metadata are the authoring inputs. Derived appearance, geometry and pixel crops retain the source attribution and license. Texture files are unresampled source RGBA crops; semantic recess masks only partition the original pixels between surfaces. Reassembling those surfaces recovers the original full source canvas.

The eight source resources are `buildingventbig3`, `buildingventbig5`, `buildingventbig6`, `buildingventbig7` and their `_off` variants. Frames are 64 by 64 pixels with one direction. The 28 frame compositions deduplicate to **45 PNGs**, registered as `CMU3DWideMachinerySurface1250` through `CMU3DWideMachinerySurface1294`. These atlas slots were checked for conflicts before writing. Art is registered in `garrison_wide_machinery_art.yml`; PNGs live under `Content.CMU/Resources/Textures/CMU14/ThreeD/Surfaces/`.

## Placement and orientation

The inherited `Sprite` has `noRot: true` and offset `(0.5, 0.5)`. The source collision fixture is X 0 to 1 and Y -0.25 to +0.25. Saved entity yaw still rotates that fixture, even though the source sprite stays unrotated. Each model therefore has `sourceDirections: 1`, `referenceDirection: 0`, `useEntityRotation: false`, `yawOffset: 0`, and no directional aliases or cardinal-facing override. Fronts face local negative Y.

Local X is `(pixelX - 16) / 32`, which already includes the source horizontal offset once. `groundOffset` stays `(0, 0)`. The exact `sourceSpriteOffset: '0.5,0.5'` is a native source-validation guard, not a second displacement. Screen offset Y is not mapped to ground Y. Saved positions, collision yaw and gameplay fixtures are unchanged.

| Design | Local X bounds | Local Y bounds | Local Z bounds |
| --- | --- | --- | --- |
| Big 3 | -0.3125 to 1.3125 | -0.249 to 0.21 | 0.04 to 1.13375 |
| Big 5 | -0.1875 to 1.21875 | -0.25 to 0.21 | 0.04 to 1.165 |
| Big 6 | -0.3125 to 1.34375 | -0.249 to 0.21 | 0.04 to 1.13375 |
| Big 7 | -0.3125 to 1.40625 | -0.25 to 0.21 | 0.04 to 1.69625 |

The asymmetric rightward footprint is deliberate. Mounting at Z 0.04 leaves 0.005 tile above the existing lattice draft at the level -2 placement. Heights use the source face's 32 pixels per tile scale; that does not establish a recovered physical height. Main body depth is approximately 0.425 tile, with thin front details extending the overall depth to 0.459 or 0.46 tile. Plain backs, mounting and depth are inferred.

## Distinct construction and recesses

Big 3 has a tall left cabinet, dark vertical grille, three lower status modules, and a separate broad monitor case. Four bezel solids surround its display instead of filling the opening. The display sits at Y -0.21, behind the front face at -0.249.

Big 5 uses two stepped housings. The left face retains its top intake, stacked grille and controls; the right keeps its circular fan and lower service grilles. The fan's source crop is `(42,36,52,46)`. Twelve narrow solid segments form an annular lip, with the original fan art recessed at Y -0.205. Half-turn-equivalent box pitches stay within the supported -90 to +90 degree range without changing the geometry.

Big 6 has the slatted left service cabinet and a low rack with side jambs, top vent, bottom ports, white connectors and yellow corner pixels. The dark status row is recessed to Y -0.18 behind the face at -0.249. Big 7 preserves that low construction and adds the taller fan hood, crown and arched intermediate vent; it is not a vertically scaled Big 6. Its upper fan source crop is `(39,14,49,24)`, recessed at Y -0.19 behind its segmented lip.

Big 3's grille and lower details differ from Big 6/7 despite their shared cabinet outline. Surfaces are shared only when dimensions and complete RGBA pixels are identical. All untextured body colors come from the corresponding source palette. The generator also probes the empty space ahead of each recessed feature so a solid housing cannot silently fill the cavity.

## Source states and timing

| Design | ON frames and delays | Cycle | OFF resource |
| --- | --- | ---: | --- |
| Big 3 | 4 frames, each 0.25 seconds | 1 second | One static frame |
| Big 5 | 5 frames, each 0.25 seconds | 1.25 seconds | One static frame |
| Big 6 | 7 frames at 0.25 seconds, then one at 4 seconds | 5.75 seconds | One static frame |
| Big 7 | 6 frames at 0.25 seconds, then one at 4 seconds | 5.5 seconds | One static frame |

Every source frame has its own explicit `spriteStates` composition and original delay. Default parts equal ON frame zero. Alpha masks and ordered solid geometry stay constant across ON/OFF states; source colors change. Big 6's final two images match, but the final four-second interval remains. No mechanical fan rotation is invented.

The OFF artwork is an available resource state, not evidence of a gameplay power controller. These inherited prototypes declare no dedicated power or machine-operation visualizer, and the source audit found no direct family/state controller. The model should show the actual source `Sprite` state and `AnimationFrame`. Runtime overlays, source overrides and unsupported states must retain normal fallback behavior instead of disappearing to force a 3D match.

## Context checks and limitations

The generator compares the authored records with current `platform-three-redux-*-scene.json` snapshots at levels -2, -1, +1 and +2. Its report hashes the exact inputs, including the model library and saved source audit. The snapshots include the finished overhead refinements and reduced Platform Three. A separately owned corner refinement retains its existing bounds; that geometry remains tied to the recorded input snapshot here.

The final context report records all 12 saved placements, positive modeled-part clearances, unmapped neighbors and existing fixture overlap candidates. It uses conservative part AABBs, including alpha planes and annular segments. Because geometry is invariant through the source frames, the same bounds apply to ON and OFF states. Review floors are explicitly flat reference underlays, not reconstructed terrain elevation.

The authored-asset run found zero modeled contacts across 317 placement-to-neighbor comparisons; 62 unmapped neighbor occurrences remain unresolved. Positive minimum separations include 0.005 tile above the level -2 lattice, 0.101 tile from its Platform Three fascia, 0.09375 tile between Big 7 UID 4580 and its side wall, 0.20075 tile between Big 3 UID 4574 and Big 9, and 0.25053 tile between Big 6 UID 1789's visual and the prison wall. These are part-bound separations, not gameplay clearance guarantees.

Known saved-source discrepancies remain explicit:

- Level -1 Big 7 UID 10529 has a 180-degree fixture extending west into rack UID 7584 by 0.3 by 0.5 tile in AABB terms. Its noRot visual continues rightward.
- Level -2 Big 5 UID 2678 overlaps disposal pipe fixtures 1461 and 1462 in ground XY. Those pipes have subfloor masks; shared XY is not proof of an unintended gameplay collision.
- Level +2 Big 6 UID 1789 has a 90-degree fixture extending north into prison wall UID 13133 by 0.5 by 0.5 tile. The unrotated visual remains south of the wall.

Collision masks and runtime interactions are not simulated by these checks. Unmapped neighbors remain unresolved. At Big 5's location, the foreground platform fascia can obscure the low controls from some views; terrain elevation is uncertain, and the model was not raised arbitrarily to hide that limitation. Plain backs and inferred depth require continued first-person fidelity review.

## Reproduction and evidence

Run `Tools/three_d/author_wide_machinery.py` with the repository's Python dependencies. It writes only the two dedicated prototype files, this family's PNG resources, source/context evidence and family reviews. It does not run shared exports, build the game, alter saved map entities, or launch a game/server.

- `Tools/three_d/generated/wide-machinery-source-audit.json`: frame counts, original timings, atlas slots, source input hashes, exact written-texture reconstruction and empty-recess probes.
- `Tools/three_d/generated/wide-machinery-context-audit.json`: saved poses, modeled clearances, unknown neighbors, fixture caveats and input hashes.
- `Tools/three_d/generated/review/wide-machinery/`: source/four-view cards, static OFF cards, complete source frame sheet and 12 assembled context cards.

Focused resource validation reads the written model file through `build_models.load_models`, including source timing and state checks. RGBA reconstruction reads actual PNG files and derives crop placement from the written model coordinates. Global GLB/viewer exports, actual native frame selection, fallback regressions, cell packing and any later runtime evidence are reported separately by the parent task. Asset checks do not imply that all those integration checks have passed.

## Final offline integration checkpoint

The final wide export audit verifies all 12 saved placements, 32 synthetic yaw/state poses, 28 source compositions and 3,820 STEP samples. All 864 library GLBs and 30 assembled GLBs validate without errors or warnings. The 664-case native audit admits all 56 sampled ON and 56 OFF wide occurrences, plus 40 incidental occurrences in existing scenes, without introducing neighboring omissions. Twenty-seven focused appearance tests and the final library-budget test pass. See `wide-export-audit.json`, `wide-timeline-audit.json`, `wide-material-audit.json` and `wide-native-budget-audit.json` under `Tools/three_d/generated`. Six pre-existing surface omissions remain; no live game or browser GPU test was performed.
