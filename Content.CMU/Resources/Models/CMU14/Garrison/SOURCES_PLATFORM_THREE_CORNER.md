# Platform Three corner consolidation

Only `CMU3DHybrisaPlatformThreeCorner`, mapped exactly to
`RMCPlatformHybrisaThreeCorner`, changes from 73 parts to 22. The existing solid
union, open underside, exterior colors, source mapping and saved-facing behavior
are preserved. This reduces the model by 51 parts without replacing its eleven
colored cap strips or extending the clipped east support.

## Source and license

The source entity is defined in
`Resources/Prototypes/_RMC14/Entities/Structures/Platforms/platform.yml`.
Its actual reference artwork is
`Resources/Textures/_RMC14/Structures/platforms.rsi/hybrisaplatform3_corner.png`.
The 64 by 64 sheet contains four static 32 by 32 RSI frames, ordered South,
North, East, West. The dedicated review uses these actual corner frames at
saved yaws 0, 90, 180 and 270 degrees; straight-platform frames are not used
as corner-facing references.

Source metadata declares **CC-BY-SA-3.0**, with attribution to:

- [cmss13 platforms.dmi, revision 789a362](https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/platforms.dmi)
- [cmss13 platforms.dmi, revision 48e570b](https://github.com/cmss13-devs/cmss13/blob/48e570bd697f2476e28d89cd255d0539a5228228/icons/obj/structures/props/platforms.dmi)

The source design and derived appearance retain that attribution and license.
Separately licensable geometric work follows the CC0-1.0 treatment in
`SOURCES_HYBRISA_PLATFORMS.md`.

## Preserved geometry and color

The canonical corner occupies the south and east edges. Its bounds remain
`[-0.5,-0.5,0]` through `[0.5,0.5,0.39]`. Five south-facing volumes reuse the
straight platform's exact source textures for one beam, two supports and two
feet. The east-facing assembly uses six volumes. All eleven original cap-strip
records remain intact, including their rounded coordinates, colors and labels.
The straight platform and every other record in `garrison_environment.yml`
are checked for unchanged text.

The east left support starts at Y -0.36 instead of the complete support's
-0.375. Stretching the full nine-pixel image over this shorter interval would
shift its colors. The remaining fraction of the first source pixel therefore
stays a separate opaque `#1C201E` strip between Y -0.36 and -0.34375. The eight
complete remaining source columns cover Y -0.34375 to -0.09375 at their original
scale. Both volumes retain X 0.36 to 0.49 and Z 0.0425 to 0.2125.

The new eight-by-four PNG is an exact unresampled crop `[1,0,9,4]` of
`CMU3DPlatformThreeSupport.png`. That existing support comes from the horizontally
mirrored `[51,10,60,14]` crop of `hybrisaplatform3.png`. Sharing those source
pixels preserves the existing corner's color field; it does not replace the
corner's directional reference artwork. The new texture has alpha 255 throughout.

`CMU3DPlatformThreeCornerTrimmedSupport` uses atlas slot **1204**, checked unused
before registration in `garrison_hybrisa_platform_three_corner_art.yml`.
The other three surfaces are reused without modification:
`CMU3DPlatformThreeBeam`, `CMU3DPlatformThreeSupport` and
`CMU3DPlatformThreeFoot`. South surfaces use XZ projection; east surfaces use YZ
with `surfaceFlipU: true`. The clipped fractional strip remains an ordinary
colored solid. No transparent slab fills the open underside.

## Verification and scope

The original target record, complete model file, library and target GLB were
frozen in `.codex/platform-three-corner-baseline` before editing.
`Tools/three_d/author_hybrisa_platform_three_corner.py` checks the serialized
replacement against that baseline and samples the actual written PNGs through
the production UV projection. At each of four rotations it compares the solid
union, occupied partition-cell colors and every exposed positive/negative
X/Y/Z face color. The exact counts are recorded in
`Tools/three_d/generated/platform-three-corner-verification.json`.

Reviews and the four source-frame references are written to
`Tools/three_d/generated/review/platform-three-corner/`. The immutable placement
baseline records all 443 configured corners: 213 on the classic surface, 215 on
the Redux surface and 15 on Redux level -2. Every saved corner has only Transform
overrides. Source entity definitions, map files and gameplay collision are
unchanged.

Height, depth and hidden construction remain the existing inferred draft.
Original corner direction frames differ in shading; the four reviews retain
those distinctions without claiming that one rotated 3D assembly reproduces
every 2D projection exactly. Broken/construction states and other platform
variants are outside this consolidation. Global exports, scene verification and
native admission checks are coordinated separately; this generator launches
neither the game nor the server.

## Final offline integration checkpoint

The final corner export audit verifies all 443 saved placements, 17 assembled/fixture GLBs, 488 corner roots and 10,736 part transforms. Actual mesh/UV/PNG sampling checks 18,432 partition cells, 7,912 occupied colors and 5,944 exposed-face colors across four rotations. In the 664-case native follow-up, sampled corner acceptance rises from 122/128 to 128/128; basement wall 8754 also recovers. Six earlier surface omissions remain. See `platform-three-corner-export-audit.json` and `wide-native-budget-audit.json` under `Tools/three_d/generated`. These offline checks do not approve physical depth, hidden construction, all states or live fidelity.
