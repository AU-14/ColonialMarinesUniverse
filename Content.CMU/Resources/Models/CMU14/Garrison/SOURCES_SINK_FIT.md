# Wash basin geometry and source states

Draft checkpoint: 2026-09-25. Stable Garrison Redux is the primary map.

`CMU3DSink` now uses 25 parts in place of the earlier 11-part approximation.
The invented floor pedestal and two separate handles are removed. The basin
has a recessed floor, rolled rim, rear inner slope, front apron and one short
tap. All part colors come from seven recorded pixels in the original art.
The 21-pixel front width supplies a .65625-tile frontage; the 13-pixel side
width supplies a .40625-tile depth reference. This is source-guided geometry,
not a reconstruction proving physical depth, height or hidden plumbing.

The local rear edge is Y=0, with the bowl extending toward its saved front
(-Y before rotation). This clears the three sink/shutter contacts introduced
by the preceding shutter pass. Saved transforms and facings are unchanged.
No general wall/counter mounting rule is added by this geometry refinement.

## Original resources and license

- Prototypes: `Resources/Prototypes/_RMC14/Entities/Structures/Furniture/sink.yml`.
- Art: `Resources/Textures/_RMC14/Structures/Furniture/sink.rsi/sink_emptied.png`.
- States, timing and attribution: `Resources/Textures/_RMC14/Structures/Furniture/sink.rsi/meta.json`.
- License: **CC-BY-SA-3.0**. The RSI attributes cmss13's `watercloset.dmi` at
  <https://github.com/cmss13-devs/cmss13/blob/cd8ab082e9c3de33652ce5cbf730026baefb6e96/icons/obj/structures/props/watercloset.dmi>.

Preserve this license and attribution with derived assets. The model is
procedurally authored from the source; no external reconstruction service
or generated texture was used. Inferred underside construction remains a
draft, and a complete physical mounting/drain assembly has not been modeled.

## State and animation audit

Every listed RSI state has four directions. Times below describe the resource
strips, not implemented 3D motion or confirmed gameplay transition timing.

| Source state | Frames per direction | Resource duration | Current use / 3D status |
| --- | --- | --- | --- |
| `sink_emptied` | 1 | Static | Base prototype layer; this is the refined 3D pose. |
| `sink-fill-1` | 1 | Static | Dynamic drain-buffer fill overlay; not implemented in 3D. |
| `sink_alt` | 1 | Static | No direct reference found in the searched current code/prototypes. |
| `sink_animation_empty` | 5 | .5 s | No direct reference found; no 3D clip. |
| `sink_animation_fill_loop` | 7 | .7 s | No direct reference found; no 3D clip. |
| `sink_animation_fill` | 10 | 1 s | No direct reference found; no 3D clip. |
| `sink_emptied_animation` | 9 | 2.8 s | First frame 2 s, then eight .1 s frames; no direct reference found and no 3D clip. |

`SharedRMCSinkWaterSystem` fills a refillable held container on interaction;
it does not drive a sink sprite animation. `DrainSystem` accepts liquid into
`drainBuffer`, destroys buffered liquid over time and changes drainage audio.
`SharedSolutionContainerSystem.UpdateAppearance` publishes the fraction and
reagent color. `SolutionContainerVisualsSystem` selects/hides `sink-fill-1`
and applies its tint. The source destruction thresholds remove the entity;
they do not specify a persistent broken-sink sprite.

Both `CMSink` and `CMSinkEmpty` therefore need their runtime liquid appearance
handled separately from their names. All 117 saved sink records in the five
reviewed maps override only `Transform`; this is not evidence that a running
round never changes their contents. No dynamic liquid rendering, interaction
animation, rigging or animation clips were added. The animation reference
search covers `Content.CMU`, `Content.Client`, `Content.Shared`,
`Content.Server` and `Resources/Prototypes`; absence of literal references
does not rule out every possible generated state name or future use.

## Saved-context fit and verification

The reviewed set contains 80 Redux sinks (43 surface, two -1, twelve -2 and
23 +1) plus 37 classic sinks. All 195,147 non-shutter records from the previous
checkpoint, the shutter offsets, and every other scene field remain intact:
the five scene files are byte-for-byte unchanged. All 1,903 unrelated
prototype definitions and 777 other viewer models are unchanged.

The same-level, four-tile-neighborhood solid contact check goes from 158
entity pairs to 94, clearing 64 with no newly intersecting entity pair. The
three cleared shutter pairs are classic #2820/#14777 and Redux #5718/#21886
and #5720/#21961. The remaining contacts involve walls, toilets, counters,
one operating table and one sink pair. More detailed geometry increases
part-pair counts from 2,141 to 2,267; neither metric alone proves better fit.
Curved/textured neighbors use conservative bounds, and unmodeled neighbors
are excluded. Irregular pivots can still bury a basin in a wall, and counter
cutouts/supports remain unfinished.

All 778 deterministic individual exports and 502 assembled GLBs validate
with zero glTF errors/warnings. Forty-four existing saved regions are
refreshed, 29 saved regions and a four-direction fixture are added. Actual
GLB node IDs, source/render transforms and 25-part geometry are checked for
all 117 saved sinks. The native model-library budget check passes. This is
an asset-only change; the preceding full Python/native suite and client
build are historical results, not repeated checks for this pass.

Evidence: `Tools/three_d/generated/sink-fit-*`,
`Tools/three_d/generated/review/sink-fit/` and
`Tools/three_d/generated/review/sink-fit-before/`. The source directions,
before/after four-view cards and saved contexts support continued review;
they do not grant fidelity approval. Native interaction/frame-time review
and the normal gameplay viewport conversion remain open.

Later checkpoint: `SOURCES_BASIN_WALL_MOUNT.md` adds 47 Redux / 33 classic rear-wall adjustments. It clears another 64 pairs without new pairs, leaving 30. Its native/offline rule updates the previously unchanged scene offsets; all saved transforms and geometry remain unchanged. Liquid and animation work remains open.
