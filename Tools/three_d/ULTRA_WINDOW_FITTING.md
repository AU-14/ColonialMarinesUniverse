# Ultra windows and shower curtains — initial fitting checkpoint

Follow-up: `CURTAIN_OPENINGS.md` clears 14 more curtain contact records, leaving
four wall-light records. It adds two context-based opening facings and side
partition clearance. The numbers below describe the earlier checkpoint.

Stable Garrison Redux remains the primary map. This pass refines the existing
ultra-window and open/closed shower-curtain drafts. The library remains at 797
assemblies, with no fidelity or full-state approvals.

The checked scope is all 33 ultra windows (19 Redux, 14 classic) and all 46 shower
curtains (27 across four Redux levels, 19 classic). Saved positions and facings
are preserved. The native administrative preview and offline map exporter now
use the same explicit fitting rules:

- Named solid end trim defines the horizontal opening. The complete pane,
  rails and clips fit proportionally between its ends, with a 0.01-tile gap.
  Twenty-three window assemblies and 42 curtain assemblies use this fitting.
  The wall footprint is conservative over its full height; it does not claim
  a physically measured frame profile.
- At perpendicular window corners, the pane along the grid's horizontal axis
  continues through, while the other pane ends beside it. This decision is
  independent of camera direction, entity IDs and enumeration order.
- Thirty curtains move to the inside of explicitly named co-located glazing,
  with a 0.02-tile gap. Fitting resolves each stable pose separately. Other
  curtain placements retain their original depth.
- Front/rear walls crossing the panel pivot do not count as two end supports.
  Excessive fitting, unsupported part rotations and ambiguous glazing faces
  remain unchanged. Missing geometry is not silently removed.

The comparison examines 125 poses against 9,512 same-level modeled neighbors
within four tiles. It clears 125 of the previous 143 contact records and
introduces none. All recorded ultra-window contacts are cleared. Eighteen
curtain records remain: 2 Redux surface, 2 classic, 2 Redux -1, 3 Redux -2 and
9 Redux +1. These include wall-tube lights, Strata/grey-SPP trim, a wire rail,
perpendicular glazing and a curtain facing into a shower/back wall. A pair
may have separate open/closed records; these are not 143 distinct objects.
Unmodeled and cross-level neighbors are outside the audit.

## States and source limits

`_RMC14/Structures/Furniture/Curtains/shower.rsi` contains static `open` and
`closed` states. Both are authored and exported at every saved placement as
92 fitted comparison poses. The source base curtain declares an
`AnimationPlayer` and 0.5/0.1-second opening and closing phases. Static source
art does not make those gameplay transitions complete: motion, timing,
interruption, fire/destruction and native interactive verification remain open.
This pass adds no animation clips; the library remains at 12 clips in three
other models. Curtain height, fabric thickness and hidden construction are
inferred. The source folds remain visibly simplified in the geometry.

The curtain RSI declares CC-BY-SA-3.0, taken from cmss13:
[curtain.dmi](https://github.com/cmss13-devs/cmss13/blob/0a8d59abad27ec6112ef59d7661ab2139e227d0a/icons/obj/structures/props/curtain.dmi).
The existing adapted curtain geometry and comparisons retain that attribution.
Window attribution and inferred dimensions are recorded in
`Content.CMU/Resources/Models/CMU14/Garrison/SOURCES_COLONY_ULTRA_WINDOWS.md`.

## Evidence and review

- `generated/ultra-fit-context-comparison.png`: four saved Redux contexts,
  before/after at the same scale and camera.
- `generated/ultra-fit-source-poses.png`: original source art and three default
  model assemblies. This is a visual comparison, not a pixel-fidelity claim.
- `/viewer/?scene=../generated/ultra-fit-curtain-fixture.json`: all 92 fitted
  stable curtain poses; `ultra-fit-curtain-fixture-source.json` links each
  fixture pose to its original map entity.
- `generated/ultra-fit-placement-source.json` and
  `generated/ultra-fit-verification.json`: source overrides, per-pose geometry,
  fitting, remaining contacts and validation results.

Five saved scenes and 59 existing assembled region exports are refreshed.
Actual GLB verification covers all 79 placements across 129 saved export roots,
92 fixture roots, and 13,539 part transforms/material colors. All 794 unrelated
viewer entries and 196,556 unrelated saved scene records remain unchanged.
The 797 library and 795 assembled GLBs have no validation errors or warnings.
The client build and 311 native / 195 Python checks pass. Browser loading and
open/closed inspection were checked without console warnings/errors; offline
renders were inspected. Browser canvas screenshots and native interaction
are not verified. Ordinary gameplay rendering remains unchanged.
