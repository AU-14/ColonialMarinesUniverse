# Shower curtain openings and side partitions

Stable Garrison Redux is the primary map. This follows the initial window and
curtain fitting checkpoint in `ULTRA_WINDOW_FITTING.md`. All 797 assemblies
remain drafts; this pass adds neither models nor animation clips.

## Source context and presentation

Two Redux curtains face backing walls in the saved map: entity 4111 on level +1
and entity 2098 on level -2. Their co-located `CMShower` fixtures, 972 and 614,
face north into a clear opening. The administrative preview and offline export
now use that fixture facing only when the curtain faces a wall and the named
fixture faces a clear side. Already clear, non-cardinal and conflicting choices
keep their previous facing. Saved transforms and gameplay entities are unchanged.

`CMShower` inherits `anchored: false`. The native lookup therefore includes
uncontained fixtures in a bounded local query, then requires the exact named
prototype, same grid tile and at most 0.125-tile separation. The curtain itself
must be anchored. The exporter uses the same saved spatial context. Regression
coverage includes an unanchored fixture and rotated grids.

Perpendicular blue glazing remains a side partition. It must meet the curtain
end instead of rotating the curtain into the glass. Both curtain poses now name
the relevant blue/tinted glazing, wire rail and Strata/grey-SPP wall models as
end supports. Textured Box parts still have physical thickness, so the rail's
textured posts contribute to end clearance. Existing limits prevent excessive
shrinking; front/rear walls crossing the pivot cannot act as end supports.

Seven saved presentation records change across Redux levels -1, -2 and +1.
The 33 ultra windows and the Redux/classic surface curtains retain their prior
presentation. Across the full checked scope, 23 panes and all 46 curtains have
end fitting, and 30 curtains also mount beside co-located glazing. Each open
and closed pose resolves independently.

## Verification and remaining work

The 125-pose check compares 9,512 same-level modeled neighbor pairs within four
tiles. Fourteen of the previous 18 contact records are cleared and none is
introduced. The four remaining records are both curtain poses against the
wall-tube light at Redux surface entity 15274 / light 17304 and classic entity
10364 / light 11743. The light canopy and mounting geometry still need work.
Unmodeled and cross-level neighbors are outside this audit; non-box contact
bounds are conservative. Clearance is not a fidelity approval.

The client build, 314 native tests and 199 Python tests pass. The 797 individual
model exports reproduce deterministically. Default part geometry is unchanged,
as are 795 unrelated viewer entries and 196,556 unrelated saved scene records.
Five saved scenes, 59 assembled regions and a 92-pose fixture are refreshed.
See `generated/curtain-opening-verification.json` for export and validator results.

The curtain source RSI has static `open` and `closed` states. The existing Door
owner declares 0.5/0.1-second transition phases; cloth motion, transitions,
interruptions, fire/destruction and native interaction remain unfinished.
Library animation coverage stays at 12 clips in three other models, with no
model verified across every state. Source attribution and inferred physical
dimensions remain documented in `ULTRA_WINDOW_FITTING.md`.

## Review

- `generated/curtain-opening-context-comparison.png`: four saved Redux contexts
  before/after at matching scale and camera. These offline renders were inspected.
- `/viewer/?scene=../generated/curtain-opening-curtain-fixture.json`: both stable
  poses for all 46 saved curtains. The adjacent `-source.json` maps fixture poses
  back to their map entities.
- `/viewer/?scene=../generated/curtain-opening-reduxPlus1-scene.json`: inspect
  entity 4111 in its room; level -2 entity 2098 has the other facing correction.
- `generated/curtain-opening-placement-source.json`: original overrides,
  defaults, before/after presentation and independently fitted alternate poses.

The browser loaded all 92 fixture models, inspected open and closed poses and
Redux room entity 4111, and reported no console warnings or errors.
Browser canvas screenshots and native interaction are not verified. The normal
gameplay viewport remains unchanged.
