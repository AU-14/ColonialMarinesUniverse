# Single and double fluorescent wall tubes

`CMU3DWallTubeLight`, `CMU3DWallDoubleTubeLight` and
`CMU3DWallBlueDoubleLight` cover all eight placed tube-light prototypes:
1,013 Redux and 482 classic locations. The ordinary double-tube assembly replaces
inherited single-tube candidates at 30 Redux and 18 classic locations.
All three models remain drafts.

Source art is
`Resources/Textures/_RMC14/Structures/Wallmounts/LightingOffset/light_tube.rsi`.
The source and adapted geometry/reference images retain **CC-BY-SA-3.0** attribution:
https://github.com/cmss13-devs/cmss13/blob/884db783073c035b756c175b1bc75fb43279803e/icons/obj/items/lighting.dmi.
Original metadata and file hashes are recorded in
`Tools/three_d/generated/tube-light-source-audit.json`.

The former broad canopy is replaced by slim tubes, a narrow support rail and
small end fittings. The opaque source spans 24 pixels horizontally; its padding
and translucent glow are excluded from solid geometry. Each assembly includes
five static GLB scenes: On, Off, Empty, Broken and Burned, using the corresponding
`tube`, `ptube` or `bptube` state names. Empty forms retain sockets and support;
broken forms have unequal retained glass ends and an open center. Burned forms
retain the tube with dark patches. The source's 15 selected states have four
directions each and no animated strips. The other tube-resource families remain
outside these three assemblies.

The existing native administrative light adapter selects the original
`PoweredLightLayers.Base` state, including owner-driven On/Off blinking. No new
timer or emitted illumination is added. Saved scenes show the declared default
pose, not live power or a saved bulb-health snapshot. Replacement bulb artwork,
unexpected tint/effects, interactive break/remove/replace/power behavior and
native visual approval remain unfinished. The library still contains 12 clips
in three other models; these 15 static compositions add no clips.

Mount height (2.3 tiles), hidden depth, paired-tube separation, cylindrical glass
and broken edges are inferred from the sprites. Opaque shading does not reproduce
glass transmission or bloom. Original saved positions/facings are preserved;
977 fixtures receive clearance against explicitly named modeled backing walls.
Native preview and offline export use the existing shared mounting rules.

The audit checks all 7,475 state placements against 466,015 same-level modeled
neighbors within four tiles. Conservative box bounds surround curved solids;
unmodeled and cross-level neighbors are excluded. Refining the first drafts and
backing-wall clearance removes 140 of the initial 200 contact records without
introducing any. Sixty records remain across 12 pairs: black/shower curtains,
elevator/prison walls, one security airlock and foliage. Each pair has a record
for each of the five light states. These are explicit remaining context issues,
not claims of finished placement. The separate 125-pose curtain/window audit
still finds its four shower-curtain/light records.

All 60 extracted directional references match 245,760 source RGBA pixels. This
checks reference extraction, not the fidelity of the 3D geometry. Actual GLBs
verify 1,495 saved placements across 2,600 roots and 29,380 part transforms/colors.
497 assembled regions are refreshed and 106 are added. The 795 unrelated viewer
entries and 358,243 unrelated saved records remain unchanged. All 798 library
and 902 assembled GLBs validate with zero errors/warnings; library generation
is deterministic. Checks pass: 317 native, 200 Python and 39 browser module tests.
No C# production code changed, so the game client build was not repeated.

Review `/viewer/light-states.html`,
`Tools/three_d/generated/tube-light-comparison.png`,
`Tools/three_d/generated/tube-light-context-comparison.png` and
`Tools/three_d/generated/tube-light-verification.json`.
All 15 tube-state browser selections and the direction/mount controls were
exercised without console warnings or errors. Offline renders were inspected;
browser canvas screenshots and native interaction are not verified.
