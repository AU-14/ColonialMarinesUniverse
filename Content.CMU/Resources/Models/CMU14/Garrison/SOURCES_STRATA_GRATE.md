# Strata floor grate

`CMU3DStrataCatwalk` replaces the inherited `CMU3DMetalCatwalk` candidate
for all 22 `RMCCatwalkStrata` placements on Stable Garrison Redux +1.
The editable prototype is `garrison_strata_grate.yml`. Saved positions,
rotations, floor tiles and unrelated models are unchanged.

The model contains 43 separate solid perimeter, lip and open-web members.
Twelve deduplicated original-art crops retain the source top face. All 100
transparent source pixels form 36 through-openings, rather than a painted
black grid. All 4,096 top RGBA/occupancy samples match at four rotations.
This establishes the top projection, not a unique physical reconstruction.

The source Sprite uses FloorTiles draw depth and rotates with its entity.
The draft walking face is at local Z=0, so objects whose feet are at Z=0
seat on the frame. Inferred frame thickness is .0625 tiles downward; lip
and web thickness is .03125. Side and underside colors extend source edge
texels and remain inferred. The reference explicitly uses `strata_catwalk`;
the inherited Icon still names the generic `catwalk` state.

The five prior overlaps with reinforced plasteel barricades are cleared;
no new contacts were introduced. All 22 grates have no mapped same-level
neighbor contacts within four tiles under the bounding-box/SAT audit.
Fourteen barricade/prison-wall trim contacts remain unchanged. Cross-level,
unmapped neighbors, native visual review and physical fidelity remain open.

Ten saved-region exports were refreshed; four fixture rotations were added.
Actual GLB nodes verify all 22 saved placements and four fixture poses,
35 root occurrences, 1,505 part transforms and 12 embedded source images.
All 791 deterministic library GLBs and 569 assembled GLBs validate without
errors/warnings. The native model-budget check passes. No runtime code
changed; the prior full test suites and builds were not repeated.

Browser selection/focus and source reference were verified on saved grate
#4044. Screenshot capture was unavailable. Offline source/top/oblique/
underside and five isolated saved-pair comparisons were visually reviewed.
See `Tools/three_d/generated/strata-grate-verification.json` and adjacent
evidence files. The current scene is `strata-grate-redux-plus1-scene.json`.

## States

`strata_catwalk` is one static, one-direction RSI state. No animation clips
were added. Inherited Destructible thresholds include a 200-damage removal
with 0–1 metal-rod spawn and a 500-damage removal trigger; live destruction
and debris behavior have not been reviewed. Damage does not have a declared
alternate sprite here. Other catwalk palette states belong to other families.
The normal gameplay viewport is unchanged; no model is fidelity-approved.

## Attribution

Original image: `Resources/Textures/_RMC14/Structures/catwalk.rsi/strata_catwalk.png`.
The 12 cropped surface PNGs under
`Content.CMU/Resources/Textures/CMU14/ThreeD/Garrison/StrataGrate/`
are derivatives retaining original RGBA. Original and derived art: **CC-BY-SA-3.0**.

Taken from cmss13 at https://github.com/cmss13-devs/cmss13/blob/09a5191fb11aab8ddffe3f9be94292b53e4d96f6/icons/turf/almayer.dmi, https://github.com/cmss13-devs/cmss13/blob/8e8d26bbb4f1617ea1b1ffc17ffffca552ce8c11/icons/turf/floors/ice_colony/shiva_floor.dmi, https://github.com/cmss13-devs/cmss13/blob/8e8d26bbb4f1617ea1b1ffc17ffffca552ce8c11/icons/turf/floors/prison.dmi, https://github.com/cmss13-devs/cmss13/blob/8e8d26bbb4f1617ea1b1ffc17ffffca552ce8c11/icons/obj/structures/structures.dmi, https://github.com/cmss13-devs/cmss13/blob/8e8d26bbb4f1617ea1b1ffc17ffffca552ce8c11/icons/obj/structures/props/mining.dmi, https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/hybrisa/grates.dmi, https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/hybrisa/piping_wiring.dmi, https://github.com/cmss13-devs/cmss13/blob/29bfb8501be93dde3c2eececfcf60ae82b0e32df/icons/turf/floors/aicore.dmi, https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/turn/floors/hybrisafloors.dmi

Prison-hull follow-up: the remaining 14 recorded barricade/wall contacts are cleared by refining the wall inside its original tile footprint. Earlier contact counts above are historical. See `SOURCES_PRISON_HULL.md` and `Tools/three_d/generated/prison-hull-contact-verification.json`. Native visual, later-state and whole-map fidelity review remain open.
