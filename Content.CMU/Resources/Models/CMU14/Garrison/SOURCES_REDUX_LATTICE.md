# Redux overhead lattice source and placement record

2026-09-24. Six draft assemblies cover `RMCOverheadLatticeVertical1`, `Vertical2`, `Vertical3`, `Horizontal4`, `Horizontal8`, and `Horizontal12`. These are the ordinary dark-steel lattice family, distinct from the existing Hybrisa cable-tray drafts. None is fidelity-approved.

## Source shape, direction and states

Source: `Resources/Textures/_RMC14/Structures/overhead_lattice.rsi`, CC-BY-SA-3.0. Original metadata attributes CMSS13 `icons/obj/structures/props/smoothlattice.dmi` at `https://github.com/cmss13-devs/cmss13/blob/master/icons/obj/structures/props/smoothlattice.dmi`. Keep this record and the original RSI metadata with exports.

Each assembly has twenty real solid parts: two four-band steel chords, the corresponding closed end when present, and continuous diagonal brace bands. The four shades are the original source colors. Plan dimensions use 32 pixels per tile. The top-view arrangement follows the six distinct `lattice1`, `lattice2`, `lattice3`, `lattice4`, `lattice8` and `lattice12` states. The height range 2.82-2.94 tiles and unseen steel cross section remain inferred, matching the current overhead datum above 2.8-tile wall drafts.

Source screen offsets are `0,0.5` for horizontal and `0.25,0.5` for vertical sprites. They are not copied into ground translation: the modeled plan axes remain centered on the saved tile, and co-located perpendicular end caps share their outer chord boundary. All 240 reciprocal run joins have eight identical paired chord-face bounds with no geometric gap. Moving the vertical plan by the source screen X offset would displace its corner boundary relative to the horizontal cap. Saved entity rotations remain authoritative; ten lower-level sections retain their 180-degree rotations.

All six actual solid plans were independently sampled at original pixel centers. Their silhouettes cover 99.43-99.70% of the original mask union, with zero extra occupied source samples and one/two missed edge-tip samples per model. These are smooth diagonal solids rather than pixel stair steps. This check does not establish physical height, materials or side-view fidelity. Diagonal edge tips and hidden construction still need refinement.

The source resource has fifteen named states with one frame each. This batch covers the six states placed in Redux, not all off-map lattice combinations. There are no authored animation clips. `SpriteFade` remains unsupported by the 3D reviewer; static source-state coverage must not be read as completed runtime visual behavior.

## Saved placement and remaining contacts

All 272 saved instances are now exact draft mappings: 100 Redux surface, 130 Redux -1, and 42 Redux -2. Every saved position/rotation, all floor tiles and all 92,038 unrelated scene records across these snapshots are unchanged. All 1,896 prior definitions are unchanged. The source defaults and saved records show no visual overrides beyond Transform.

The individual solid-box check records ten entity contact pairs / 661 part pairs. Four are perpendicular lattice sections intentionally co-located in the map (609 part pairs). Six involve streetlight drafts #885 and #893 on Redux surface (52 part pairs). These remain recorded mounting/junction work, not cleared intersections. Lattice unions and streetlight/truss attachment need further visual refinement. No arbitrary map movement or height lift is applied to hide the contacts.

Checks use fifteen separating axes with each part's local yaw/pitch and entity transform. Results are exact for opaque boxes and conservative for curved or textured neighbours; only modeled same-level neighbours within four tiles are inspected. There are no unpaired source-open lattice ends across the 240 reciprocal joins. That is a run-layout check, not an exhaustive structural approval.

## Review and exports

The six source/plan/oblique comparisons, source/four-view cards and six-model overview are in `Tools/three_d/generated/review/redux-lattice/` and `redux-lattice-plan-comparison.png`. The browser fixture contains twenty sections: six individual states, joined horizontal/vertical runs, and a perpendicular corner. Saved Redux surface and rotated Redux -2 contexts were inspected. Browser errors/warnings are zero. Native interactive and frame-time review remain open.

Twenty-four prior map regions are refreshed, eighteen saved regions and one fixture are added. Actual exported GLB node IDs verify all 272 placements. All 776 deterministic model exports, 776 individual GLBs and 297 assembled GLBs validate with zero glTF errors/warnings. The native library-budget test passes. This is an asset-only batch; the previous 140 Python / 140 native checks and client build are historical checks, not newly rerun results.

Library checkpoint: 776 drafts / 26,915 solids / 2,348,032 triangles. No atlas additions: 664 images, 198 textured models. Redux has 734 exact draft types, 114 inherited candidates and 703 unmapped types. Normal gameplay rendering and animation support are unchanged.

Evidence: `generated/redux-lattice-source-audit.json`, `redux-lattice-placement-source.json`, `redux-lattice-verification.json`, `redux-lattice-export-review.json`, and the two `redux-lattice-*-glb-validation.json` reports. The full modeling goal remains active.
