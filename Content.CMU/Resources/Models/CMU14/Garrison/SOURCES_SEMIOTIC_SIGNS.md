# Semiotic wall signs â€” 2026-09-25

Twenty draft plate assemblies cover the placed `CMSemiotic*` signs: 19 prototype types / 43
saved Redux instances (31 surface, one -1, seven -2, four +1), plus 19 classic instances.
This is one missing environmental family, not completion of the map or its animations.

Original artwork: `Resources/Textures/_RMC14/Structures/Wallmounts/semiotics.rsi`,
**CC-BY-SA-3.0**, taken from cmss13:
https://github.com/cmss13-devs/cmss13/blob/53b8f7f634ef16422e463bd75eeaa16dccab25ab/icons/obj/structures/props/semiotic_standard.dmi
The source RSI metadata remains authoritative. The twenty `CMU3DSemiotic*Face.png` crops,
their textured GLBs, and the derivative plate assemblies retain this attribution/license.

`Tools/three_d/author_semiotic_signs.py` reproducibly authors
`garrison_semiotic_signs.yml`, `garrison_semiotic_art.yml`, the original-pixel crops and
four-view comparison cards. Every placed source state is a single static frame. There are
no fabricated animation clips. Characters and mobs remain live sprite billboards.

Each plate has five clipped backing strips and one thin printed face. The backing silhouette
matches every occupied source pixel; all twenty face crops preserve source RGBA bytes.
The visible source bounds are `(2, 9)-(15, 22)` within a 32-pixel canvas. That gives a
0.40625-tile square, with its original leftward offset from the saved entity pivot retained.
This is a physical thin plate, not a camera-facing sprite. Back material, 0.027-tile thickness
and mounting height around 1.9 tiles are inferred and remain draft art decisions.

Wall attachment follows the existing native/offline mounting rules. Saved entity angles,
positions and along-wall spacing remain unchanged. The plate is reflected for room-side
mounts where appropriate, with readable front artwork. Explicit backing-wall families clear
raised trim, including the Hybrisa medical wall that otherwise hid the sign. Unmodeled walls,
connected glazing and other fixtures remain limits of this attachment check. No full-map
contact clearance or fidelity approval is claimed.

Evidence is under `Tools/three_d/generated/`: `semiotic-source-audit.json`,
`semiotic-placement-audit.json`, `semiotic-glb-validation.json`, and
`review/semiotic-signs/`. The Redux surface browser snapshot and separate sign snapshots for
levels -1, -2 and +1 are refreshed. All 43 saved transforms match the preceding source snapshots.

Validation: all 820 individual model GLBs pass Khronos validation with zero errors/warnings;
204 Python pipeline checks and the native library-budget test pass. Eleven focused native
capture/menu regression cases also pass for the accompanying client fix. These checks do
not establish completed native input/gameplay acceptance or approval of all modeled assets.

Browser review confirmed the previously hidden medical plate is visible on its saved wall
face after the trim correction, alongside the life-support plate. Thirty-six placements
receive trim clearance; seven retain standard mounting. Browser warnings/errors: zero.
The saved-scene parser still reports existing missing gas-pipe and freezer base prototypes;
those warnings remain in each snapshot’s diagnostics and are unrelated to these signs.
