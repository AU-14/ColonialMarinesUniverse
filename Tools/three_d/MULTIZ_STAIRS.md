# Multi-floor scene and stair fitting

The Redux review now includes all seven saved maps, from -2 to +4. Logical map
indices are separate from physical height: floors are three game tiles apart.
The viewer defaults to **All floors**; **Focus floor and below** cuts away the
upper maps, and **Focus floor only** isolates a map. Search accepts a prototype,
saved entity ID, or a qualified ID such as `0:30`.

The browser's wall cutaway applies only to the focus floor. Other visible floors
retain full-height walls. **Focus floor and below** removes upper slabs when
inspecting an interior.

The live administrator renderer samples replicated maps in the same Z network.
It keeps placement/support calculations separate for each map, then combines
their solids at the correct heights. Floor changes preserve camera direction.
Actual upper-floor slabs supply ceilings and have visible undersides; empty
tiles and authored stair/ladder apertures stay open. Billboard heights and
entity targeting retain the map that owns each entity.

## Browser visibility repair

The previous 160,000-part cap admitted objects one floor at a time and omitted
whole later objects once full. At the Redux home position, a 128 × 128 region
dropped 26,559 objects. The browser now submits every enabled object in the
selected region; growable numeric buffers avoid full-sized JavaScript-array
copies during GPU upload. Wider views still take longer to rebuild.

Some layout and appearance variants also copied comma-separated YAML vectors
directly into scene JSON. The renderer rejected those strings, leaving 816
additional objects without geometry in that same region. The scene exporter
now converts these bounds to numeric vectors, and both affected Redux geometry
chunks have been updated without changing placement or artwork.

With cutaway disabled, the repaired home-region audit submits all 37,245 eligible
objects (37,214 authored models and 31 missing-art markers) across seven floors:
1,040,067 parts, with no absent entity IDs. The browser also passed inspection
of previously invisible wall `-2:8328`, floor switching and the widest region.

Regression coverage:

- `dense multi-floor regions upload later walls and props after the former part limit`
  exercises geometry construction through GPU-buffer submission after the old cap.
- `wall cutaway preserves full walls on the other floors and follows the focus floor`
  checks world-space wall height when focus changes.
- `test_connected_wall_export_has_numeric_geometry_after_yaml_vector_input`
  exports connected walls from raw YAML-style bounds and verifies numeric JSON
  geometry with the selected neighbour artwork intact.

All 56 browser tests and 336 Python tests passed after this repair. The native
renderer is separate and retains the replication/geometry limits described below.

## Stair geometry and movement

The 27 distinct inter-floor assemblies use 81 fitted stair tiles, including
their ordinary decorative approaches and exits. Heights run continuously from
the lower landing to the upper landing. Camera-only copies of those slopes on
both maps give the same physical position when the existing Z physics changes
the actor's map. Subtracting the source support curve before adding the remaining
physics height preserves jumping and falling above the fitted steps.

Eleven lower-map stair projections are suppressed where the same steps exist on
the upper map. Five upper floor tiles become stair apertures. Saved surface
entity 4143 was an exact duplicate of stair 1399 and has been removed; the
unrelated decal index 4143 is preserved. Ordinary same-floor raised and recessed
landings still use their authored elevation profiles.

The serialized profiles are presentation data. Movement, collision, map
transitions and server authority remain in the existing Z-level systems.
Unprofiled sticky stairs fall back to closed treads sampled from their actual
high-ground curve. Flat ladder supports remain handled by the ladder models.

## Regeneration

From the repository root, with the art-tool Python dependencies installed:

```text
python Tools/three_d/author_elevation.py
python Tools/three_d/author_multiz_elevation.py
python Tools/three_d/scene.py --variant redux --all-levels --output Tools/three_d/generated/redux-scene.json
python Tools/three_d/serve.py
```

The scene manifest references deterministic compressed JSON chunks in
`generated/redux-scene/`. This keeps the complete scene portable in Git; the
browser and GLB export reader both load the chunks. `serve.py --prepare` also
exports all configured floors. Single-level exports remain available without
`--all-levels`.

## Verification and limits

```powershell
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.Tests -Filter 'FullyQualifiedName~ThreeD'
$env:PYTHONPATH = 'Tools/three_d'
python -m unittest discover -s Tools/three_d/tests
node --test Tools/three_d/viewer/*.test.mjs
```

- Native build and 629 ThreeD tests passed, including normalized up/down crossings,
  fitted stair support, camera height, and retained jump height.
- 335 Python tests passed. The server route checks also pass with compressed
  chunks, while private files and traversal stay excluded.
- 54 browser geometry/control tests passed, covering floor selection, slab undersides and
  apertures; the seven-floor scene loads through the local review server.
- The map audit compares 189 positions across all 27 stair crossings: both map
  copies agree to floating-point precision. Every scene key, geometry variant
  and floor-material reference resolves. The scene has 313,143 entities and
  342,578 floor tiles; the 36 compressed chunks total approximately 4.7 MB.

A live multiplayer stair walkthrough has not been performed. The native view
now requests bounded replication of its floor stack; see [native renderer](NATIVE_RENDERER.md)
for the subscription lifecycle and movement changes. Dense views can still
omit geometry within the scene budget. Lighting is sampled from the actor's map.
The offline exporter also reports existing missing gas-pipe/freezer prototype
parents. These changes do not constitute full model-fidelity or map-wide
clipping approval.
