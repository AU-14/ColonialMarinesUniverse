# Batch 9 integration and Garrison elevation review - 2026-10-07

## Delivered

Imported the supplied `Stable-Garrison-Redux-Art-Batch-9-Assets.zip`: 637 additional draft assemblies and 1,021 surfaces. The combined library now has **1,676 draft models and 3,013 surfaces**. Import commit: `4e4cb2c`; native vector/PNG compatibility repair: `fb6623df2`.

The native prototype loader requires comma-separated vector scalars. Converted 57,742 Vector3 bounds and 96 Vector2 offsets without changing decoded geometry. Removed non-rendering PNG color-profile metadata from two exports; original pixels and attribution remain intact. Regenerated legacy exports were compared by decoded geometry, images, metadata and animation, because PNG encoding and insignificant float roundoff can change bytes.

Bound all 27 source-compatible inherited candidates to their existing draft assemblies. Twenty-six have identical resolved Sprite data. `AU14CrateWorkTools` inherits identical explicit layers; the additional top-level state is unused by `SpriteComponent.AfterDeserialization` when layers exist. Contents, access and objective metadata do not require duplicate geometry. This also supplies the three previously inherited engineering glass doors on level +1. Details: [compatible-bindings.json](compatible-bindings.json).

Refined the ice crystal into six angular, fully solid clasts with beveled faces, retaining source-derived colors. Its hidden surfaces remain inferred. No model was promoted to fidelity-approved status.

## Raised ground, stairs and map repairs

Added grid-local elevation profiles for Redux levels -2 through +4. Floors, foundations, ceilings, models, sprite fallbacks and the predicted first-person camera sample the same profile. Stair assemblies scale to meet adjoining landings; short flights rise 0.39 tile, inferred from existing retaining-platform caps. Larger flights split that rise between consecutive stair tiles. This is presentation height, not a replacement for logical multi-Z travel or server collision.

The bake uses stair facing, wall/door boundaries and retaining edges. U-shaped platform ends close all three sides. The warehouse loading deck retains its height across conveyor discharge gaps. Seven door thresholds bridge otherwise inconsistent floor heights. Profiles are explicit, editable resources; inference is an offline authoring step, not a runtime flood fill.

| Level | Elevated/recessed tiles | Moved rendered entities | Fitted stair tiles | Unresolved stair records |
| --- | ---: | ---: | ---: | ---: |
| -2 | 1,550 | 1,760 | 51 | 0 |
| -1 | 1,451 | 2,064 | 29 | 5 |
| 0 | 17,908 | 12,113 | 171 | 76 |
| +1 | 277 | 376 | 14 | 27 |
| +2 | 399 | 245 | 0 | 0 |
| +3 | 0 | 0 | 0 | 0 |
| +4 | 0 | 0 | 0 | 0 |

Saved map repairs:

- Removed surface stair 16755, an exact duplicate of 16751.
- Removed 15 visual-only Hybrisa stair overlays on level -2 where identical-position/facing FlightStairs already existed. Kept the FlightStairs physics fixtures. Removed IDs: 2396, 2397, 2402, 2403, 2408, 2409, 2410-2418.
- Turned surface FlightStairs 440 to match the eight adjacent retaining-edge constraints.
- Moved medical dividers 2386 and 2387 on level -2 south by 0.55 tile. Their previous 9 console / 34 scanner part contacts clear. The regenerated geometry has no conservative part-AABB intersections with modeled neighbors within three tiles. Facing is unchanged because the source Sprite ignores rotation.
- Corrected the changed map entity-count headers.
- Native scene admission now prioritizes occluding structures and doors before furniture, so dense furnishing does not exhaust the same budget first. Existing limits and sprite fallback remain.

The default offline Redux scene was rebuilt with the elevation profiles. Browser checks inspected building entry stairs, the warehouse deck, the recessed basement machinery channel, corrected facing and medical divider clearance. These are saved-scene checks; no interactive native walkthrough was performed.

## Review and verification

- **1,676 / 1,676 GLBs pass Khronos glTF Validator 2.0.0-dev.3.10: zero errors, zero warnings.**
- `python Tools/three_d/build_models.py --check --no-review`: all 1,676 exports, manifest and viewer assets deterministic.
- Solid-part audit: 55,478 default parts across 12 primitive shapes. Welded mesh edges are paired, winding is consistent, triangles are nondegenerate and signed volumes are positive. Every assembly has nonzero dimensions and positive-volume parts. This checks individual solids, not a Boolean-union watertight assembly. Details: [geometry-review.json](geometry-review.json).
- Visually inspected source/front/back/underside sheets for 127 representative assemblies across authoring files, plus the focused ice correction. This is a sample, not manual approval of all 1,676 models. Sheets are locally generated under `generated/batch9-review/`.
- `python -m unittest discover -s Tools/three_d/tests` with `PYTHONPATH=Tools/three_d`: **332 passed**. Existing review fixtures were regenerated with `barricade_state_review.py` and `button_animation_review.py`. The wide-machinery helper now loads its historical audit only when running that authoring workflow.
- `node --test Tools/three_d/viewer/*.test.mjs`: **52 passed**.
- `powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.Tests -Filter 'FullyQualifiedName~Content.Tests.Client.CMU14.ThreeD'`: **625 passed**, including shared landing/camera continuity and negative-coordinate descent. Shared, Server, Client and Tests build successfully through this command.
- Full client prototype serialization integration check loaded the 3D library without 3D prototype errors, but **did not pass**: existing `VehicleAev` references absent `/Maps/Vehicles/arv.yml`, and `AU14CrateSecureWeYuCounteragent` references absent `AU14AbominationCounteragentSyringe`. Those unrelated content references were not changed.
- All 272 authored ramps (265 stair tiles and seven thresholds) meet both adjoining height samples with zero discontinuous edges in the final profiles.
- `git diff --check`: clean.

## Remaining work

All models remain drafts. Complete artistic fidelity, inferred backs/interiors, every worn/held pose and every live visual state are not certified. The original batch documents are historical evidence; this review supersedes their statements that the combined library, native serialization, Khronos validation, ordinary elevation and the two medical contacts had not been examined.

The current Redux inventory has **1,532 exact draft visual prototype mappings**, three inherited hatch/ladder candidates (eight saved records), and 16 unmapped visual types (355 saved records). Exact mapping is not completed art. Conditional tree/egg/sample choices, retained fog/UI presentation and physical topology studies are distinguished in `generated/coverage.json`.

Remaining topology work covers ten prototype types: neighboring chasm openings; compound multi-Z stairs; five-by-five cargo/ASRS/vehicle elevator apertures; and through-hatch/ladder ownership. The supplied physical studies exist, but binding them directly would duplicate compound geometry or cover required openings. The separate arbitrary-mesh helmet study was not present in this ZIP and is not a supported native primitive assembly.

The elevation audit retains **108 unresolved ordinary stair records**: 55 touch multi-Z transitions, 51 have blocked/absent landings, and two reconnect to the same floor region around the stair. These keep their existing geometry and adjoining ground height. Entity IDs, coordinates, directions and inferred-region conflicts are in [map-review.json](map-review.json). Five soft door constraints on level 0 are bridged by the seven threshold ramps; no ordinary stair-height contradiction remains among fitted flights.

One saved surface airlock (13074, `RMCAirlockHybrisaPersonal`) is in an unsupported `Opening` snapshot state, so the offline reviewer retains a missing-state marker and the native path retains its sprite fallback. The two fog-wall names are atmosphere, not missing opaque wall models. Full native traversal, native occlusion/picking under the enlarged library, remaining map contacts from earlier family audits, and cross-level travel still need direct acceptance checks.

## Regenerate

Run from the repository using Python with the existing three_d dependencies:

```text
python Tools/three_d/inventory.py
python Tools/three_d/build_models.py --no-review
python Tools/three_d/coverage.py
python Tools/three_d/author_elevation.py
```

The final command writes explicit elevation profiles and seven review scenes/audits under `Tools/three_d/generated/elevation-review/`. Copy its `redux-0.json` to `generated/redux-scene.json` to refresh the default browser scene. Review all authoring differences before committing map/profile changes.
