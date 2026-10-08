# Wire rails and vendor footprints — source refinement

This pass refines nine existing drafts. It adds no prototype coverage: the library remains at 769 draft assemblies, with 733 exact classic visual types, 64 inherited candidates and 417 unmapped types. It replaces the wire rail's invented structure and separates the vendor horizontal footprint from inferred physical height. Normal gameplay rendering is unchanged.

## Wire rail source

`Resources/Textures/_RMC14/Structures/Walls/Barricades/barricade.rsi/wire_rail.png` has four 32×32 directions. Its metadata attributes CM-SS13 commit `5cf465e72efb6beccd2b78bf263072816a2a60ad`, `icons/obj/structures/barricades.dmi`, under **CC-BY-SA-3.0**. Preserve this attribution and license with the model and four derived crops in `Content.CMU/Resources/Textures/CMU14/ThreeD/WireRail/`.

The south frame occupies rows 16–31. It contains boundary half-posts at columns 0–1 and 30–31, a center post at columns 14–17, and four bars at rows 19, 22, 25 and 28. Eleven solids cover these 224 opaque source pixels exactly once. Four deduplicated source crops retain the metal shading. The old two-post, five-strand and five-tie design did not match this structure. The model loses one part overall.

The east frame occupies columns 29–31; the west frame occupies columns 0–2. Those side views place the railing at the tile perimeter. Canonical post depth is now Y=-.5 to -.40625; the saved yaw supplies the side. The previous center Y=-.375 came from the broad inherited barricade collision fixture (-.45 to -.3), which is shared with other barricades and does not describe this sprite's narrower visual depth. The collider and saved maps are unchanged. Both `RMCBarricadeWireRail` and `RMCBarricadeWireRailAltDrawdepth` share the art; their draw ordering does not establish different physical heights.

One-tile physical rail height remains an interpretation of the projected sprite, not a recovered measurement. The model uses rectangular bars and posts; concealed construction and mounting still need review. Adjacent boundary half-posts complete each other along straight runs.

## Vendor horizontal scale

`RobustToolbox/Robust.Client/Graphics/ClientEye/EyeManager.cs` defines `PixelsPerMeter = 32`. None of the 1,171 inspected placements overrides the relevant source visual state, and their resolved sprites do not apply another scale. The eight vendor drafts now use 1/32 tile per horizontal source pixel. This is independent of their inferred .038-tile vertical scale, because projected sprite height is not the same measurement as horizontal map width.

Seven drafts previously used .038 for both axes; CMB equipment previously used .028 horizontally. Only their local X coordinates change. The source image, panel split, cavity geometry in depth, cabinet depth, vertical dimensions, pivot and saved facing remain unchanged. Bounding widths match the opaque source width:

| Cabinet | Source width | Draft width |
| --- | ---: | ---: |
| Coffee, cash coffee, cigarettes, Koor | 23 pixels | .71875 tiles |
| Cola, cash soda, snack | 21 pixels | .65625 tiles |
| CMB equipment | 32 pixels | 1 tile |

This retains the existing centered artwork convention; it does not infer a new ground translation from sprite padding. The eight original source-layer compositions are reverified against `vendors-source-audit.json` and `vendor-family-source-audit.json`. Those source records retain their original vendor licenses and attribution. Cabinet backs, roofs, heights, depths and material response remain inferred. No live power, damage, emission or dispensing animation is added.

## Placement evidence and remaining contacts

All eight configured map levels were inspected. The batch affects 966 exact rails, 199 exact vendors and six inherited vendor candidates: 1,171 placements total. Classic has 136, Redux surface 145, Redux -1 has 314, Redux -2 has 19, Redux +1 has 487, Redux +2 has 70, and the remaining two levels have none. All five previously generated scene snapshots compare identically, including every entity record and floor tile. Source transforms and renderer facing rules do not change. All 1,869 unrelated prototype/model/surface definitions are unchanged.

Six former entity intersections are cleared:

- Classic Koor #828 / rail #9462, and Redux #943 / #13552.
- Classic inherited SPP soda #15465 / rail #9463, and Redux #23013 / #13553. Their inherited artwork is still an unapproved candidate.
- Classic snack #4058 / platform #12893, and Redux #8774 / #19462.

Eight vendor contact pairs remain: six with elevator floor grates, Redux coffee #8724 with cabinet #3029, and Redux +1 coffee #1263 with cabinet #734. Their exact part-contact records are in `rail-vendor-verification.json`. The earlier cleared Redux coffee #8731 / filing cabinet #3007 remains separated.

The broader rail/vendor audit records 963 conservative entity contact pairs (9,721 solid-bound pairs). These are not all new: 92 pairs are new, consisting of 91 neighboring platform-cap contacts in Redux -1 and one classic rock-border contact (#9484 / #26423). The maximum of the smaller horizontal penetration axis is .02 tiles (.019 for the rock pair). Moving the rail to its source perimeter exposes the existing cap overhangs at these seams. Browser inspection of Redux -1 #9320 shows the rail run along that platform edge. Those contacts are recorded for a mounting review, not declared physically approved or silently cleared. The audit uses individual solid world AABBs within three tiles on the same level and excludes unmapped neighbors; it is not a complete collision or fidelity certification.

No global cabinet lift, per-entity relocation, clipping workaround, or gameplay collision change is introduced. Floor seating and platform mounting need a separate source/context solution.

## Verification and artifacts

The native library-budget test passes. All 769 deterministic model/manifest/viewer exports verify; Khronos validation reports zero errors and warnings for 769 individual GLBs and 268 assembled GLBs. The full Python suite, native suite and client build were not repeated for this asset-only pass. Earlier results remain historical, not new checks. Native interactive testing and frame-time profiling remain unperformed.

110 previous exports were refreshed, including the two vendor fixtures; 101 saved regions and an eleven-object comparison fixture were added. Reading actual GLB node IDs confirms that all 1,171 affected placements occur in the exports. Nine updated source/four-view cards and an overview are in `Tools/three_d/generated/review/rail-vendor/` and merged into the 769-card main index. Main evidence:

- `rail-vendor-source-audit.json`, `rail-vendor-baseline.json`, `rail-vendor-placement-source.json`, `rail-vendor-verification.json`.
- `rail-vendor-export-review.json`, both `rail-vendor-*-glb-validation.json` reports, and `rail-vendor-fixture.json` / `.glb` / `-export.json`.
- `scene.json` for current classic; `rail-vendor-<level>-scene.json` for the seven Redux levels.

Guarded `.codex/refine_rail_vendor_footprints.py` and `.codex/finalize_rail_vendor_footprints.py` have already been applied. Do not replay them over the installed models. Source definitions change in the existing files containing the nine models; new surface definitions are in `garrison_wire_rail_art.yml`.

Visual review covers the wire rail, Koor and CMB source/four-view cards, the nine-model overview, and the eleven-object browser fixture. Actual saved contexts inspected are classic Koor #828, classic snack #4058, and Redux -1 rail #9320. The cleared vendor contacts and remaining platform-cap seams are visible in those views. Browser console errors and warnings are zero.

Current library: 769 drafts / 26,791 solids / 2,346,544 triangles. Atlas: 651 textures / 4096×704 / 11 MiB, used by 197 models. No asset receives final fidelity approval; the full modeling goal remains active.
