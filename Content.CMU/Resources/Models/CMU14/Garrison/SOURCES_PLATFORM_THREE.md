# Platform Three source-texture consolidation

Only `CMU3DHybrisaPlatformThree`, the straight `RMCPlatformHybrisaThree` platform,
is changed. Six opaque textured volumes replace 37 colored rectangles. The beam,
two support legs, two narrowed bottom feet and cap retain the exact existing solid
union, open underside and exterior bounds. This reduces the model by 31 parts;
the asset library remains at 860 model records.

The native overhead audit found a whole-model admission swap at Redux level -2,
target 2672, actor offset `[0.5,0.5]`: platform UID 2835 was omitted in the new and
no-cabinet-control scenes, while the older scene instead omitted rock UID 4852.
Platform UID 4707 also had 12 pre-existing omissions per mode in the biomass
audit. This is a source-faithful geometry reduction intended to reduce cell
crowding. Actual native packing improvement requires the parent task's rerun;
aggregate omission counts alone cannot establish it.

## Source and license

The source prototype is defined in
`Resources/Prototypes/_RMC14/Entities/Structures/Platforms/platform.yml`.
Artwork comes from `Resources/Textures/_RMC14/Structures/platforms.rsi/hybrisaplatform3.png`.
The 64 by 64 sheet contains four static 32 by 32 frames in RSI order South, North,
East, West. Source metadata declares **CC-BY-SA-3.0** and attributes the artwork to:

- [cmss13 platforms.dmi, revision 789a362](https://github.com/cmss13-devs/cmss13/blob/789a362bc55d2d6331bc89699bb4f9f792695853/icons/obj/structures/props/platforms.dmi)
- [cmss13 platforms.dmi, revision 48e570b](https://github.com/cmss13-devs/cmss13/blob/48e570bd697f2476e28d89cd255d0539a5228228/icons/obj/structures/props/platforms.dmi)

The original design and derivative appearance retain that attribution and license.
New geometric work remains CC0-1.0 to the extent separately licensable, following
`SOURCES_HYBRISA_PLATFORMS.md`. The new texture PNGs are exact, unresampled RGBA
source crops. No source artwork is repainted or altered.

## Six-volume construction

The canonical edge occupies local south. Its complete bounds remain
`[-0.5,-0.5,0]` through `[0.5,-0.35,0.39]`. The front structure spans Y -0.49 to
-0.36, giving 0.13-tile depth; the cap spans Y -0.5 to -0.35, giving 0.15-tile
depth and the same overhang. The cap lies at Z 0.34–0.39. Height, physical depth,
hidden construction and terrain elevation remain inherited draft inferences.

| Volume | X range | Z range | Source sheet crop | Projection |
| --- | --- | --- | --- | --- |
| Beam | -0.5 to 0.5 | 0.2125 to 0.34 | `[32,7,64,10]`, mirrored | XZ |
| Left support | -0.375 to -0.09375 | 0.0425 to 0.2125 | `[51,10,60,14]`, mirrored | XZ |
| Right support | 0.125 to 0.40625 | 0.0425 to 0.2125 | `[35,10,44,14]`, mirrored | XZ |
| Left foot | -0.34375 to -0.125 | 0 to 0.0425 | `[52,14,59,15]`, mirrored | XZ |
| Right foot | 0.15625 to 0.375 | 0 to 0.0425 | `[36,14,43,15]`, mirrored | XZ |
| Cap | -0.5 to 0.5 | 0.34 to 0.39 | `[0,25,32,32]`, unmirrored | XY |

Front crops come from the North frame. Their horizontal mirror retains the
established local-X projection. The South cap's first PNG row maps to local
Y -0.35 and its last row to Y -0.5. Shared projection is applied on reverse/end
faces too, preserving the color field of the old extruded colored rectangles.

Left and right support images are identical, as are their foot images. Six
volumes therefore use four surfaces: `CMU3DPlatformThreeBeam`,
`CMU3DPlatformThreeSupport`, `CMU3DPlatformThreeFoot`, and
`CMU3DPlatformThreeCap`, with atlas IDs 1200–1203. These slots were checked for
conflicts before writing. Texture files live under
`Content.CMU/Resources/Textures/CMU14/ThreeD/Surfaces/` and are registered in
`garrison_hybrisa_platform_three_art.yml`.

All crop pixels have alpha 255. The 74 alpha-65 cast-shadow samples beneath the
source beam are excluded from solid geometry. There is no full lower fascia,
third tooth or transparent slab filling the open space between the two feet.

## Facing and preservation

The model ID, exact source mapping, source RSI/state, four-direction declaration,
status, label and description are unchanged. The generator rewrites only the
target record in `garrison_environment.yml`; all neighboring records are checked
for unchanged text. Source prototypes, collision fixtures and saved map transforms
are untouched. The full placement inventory has 2,165 source entities: 1,331 Redux
and 834 classic. The parent task independently checks all placements and exports.

Canonical yaw 0 places the edge south; 90 degrees places it east, 180 north and
270 west. The four source-facing cards show the actual matching RSI direction
beside the rotated model. This consolidation does not reinterpret the original
directional artwork: South/East use a seven-pixel cap pattern, while North/West
have different shading and West is six pixels wide. Those existing 2D differences
do not describe four mutually consistent physical views. The authored physical
projection is preserved, with this limitation stated explicitly.

## Verification

`Tools/three_d/author_hybrisa_platform_three.py` writes the model parts, four PNGs,
surface definitions, proof JSON and review cards. It compares the final authored
geometry against the frozen 37-part model where the baseline is available, and
can reconstruct that old partition directly from the source pixels otherwise.

`Tools/three_d/generated/platform-three-verification.json` records exact RGBA
hashes, crop coordinates, unchanged metadata, four rotated solid-union checks
and four sets of source-pixel checks. Each facing checks all 406 opaque front/cap
samples against the actual written PNGs through the shared UV projection, plus
all 74 excluded shadow samples. Each rotated union check covers 1,088 partition
cells. Color-field checks cover all 754 occupied partition cells and 660 exposed
face samples, with equal RGBA values on the front, back, ends, top and underside.
The original voxel coordinates rounded cap stripe
boundaries to seven decimal places; continuous boundary equality is limited by
that sub-0.00000005-tile rounding.

Source/four-view cards and the four-facing overview are under
`Tools/three_d/generated/review/platform-three/`. The earlier read-only proposal
remains at `generated/platform-three-followup-audit.json` as historical evidence.

The reduction leaves existing neighboring intersections and terrain elevation
limitations intact. Broken and construction-state models are outside its scope.
Global exports, all-map placement checks and native encoder reruns are separate
parent-owned validation. No game/server launch or user fidelity approval is
implied by these offline asset checks.

## Completed offline export and packing follow-up

`generated/platform-three-export-audit.json` independently verifies all 2,165
saved placements and four synthetic yaw poses. It checks 28 assembled GLBs,
3,525 entity roots and 50,474 part transforms, including actual mesh coordinates,
UVs and embedded source PNGs. Source map transforms and floor tiles, and the
other 859 model records, remain unchanged. All 860 library GLBs and 28 assembled
GLBs pass Khronos validation with zero errors or warnings; deterministic export
and the focused native library-budget test also pass.

`generated/platform-three-native-budget-audit.json` retains all 944 actual-DLL
encodings around the overhead and biomass placements. Both watched platforms
now fit, and all 2,573 sampled platform appearances are admitted in each new
ON/OFF/control mode. There are no capture or snapshot overflows.

**Strict neighbor preservation still fails.** The new geometry recovers 42
object occurrences but causes seven new omissions in three camera cases;
total omissions fall from 125 to 90. The no-overhead control has the same
outcome. Newly omitted objects include vegetation, one open shutter, reinforced
windows and a reinforced wall. The report preserves their exact saved IDs and
camera cases. This exposes the renderer's non-monotonic whole-object admission:
admitting a previously rejected object can crowd out another object later in
the order. Geometry reduction is validated; complete crowded-scene rendering
remains follow-up work. Historical failed reports are retained unchanged.
