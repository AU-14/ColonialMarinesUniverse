# Small wall bulbs and sockets

The four draft assemblies are `CMU3DSmallWallLight`, `CMU3DSmallRedWallLight`,
`CMU3DSmallBlueWallLight` and `CMU3DSmallEmptyWallLight`. They cover the five
placed small-light prototypes at 291 Redux and 34 classic locations. The warm
assembly covers both ordinary and always-powered lights. Red color is baked
once from the source Sprite tint `#C02526`; blue uses the separate `bbulb` art.

Source art: `Resources/Textures/_RMC14/Structures/Wallmounts/LightingOffset/light_bulb.rsi`.
Source and derived geometry/reference images retain **CC-BY-SA-3.0** attribution:
https://github.com/cmss13-devs/cmss13/blob/884db783073c035b756c175b1bc75fb43279803e/icons/obj/items/lighting.dmi.
Hashes and original metadata are in `Tools/three_d/generated/small-light-source-audit.json`.

Each assembly contains five static GLB scenes, keyed by the source On, Off,
Empty, Broken and Burned state names. Default geometry uses On except for the
empty fixture. These 20 compositions represent five behaviors in four contexts.
The RSI has ten static four-direction states and no animated frame strips.

`PoweredLightVisualizerSystem` owns runtime transitions and randomized blinking.
The native administrative preview reads the actual `PoweredLightLayers.Base`
state and chooses the corresponding geometry. No independent loop or runtime
illumination is added. Unknown effects and unsupported bulb replacements remain
markers; native interactions have not been verified. Saved map exports are
declared reference poses and do not assert actual live power state.

The bulb extends normal to the wall. The mounting footprint is derived from the
small source socket; the 64-pixel image padding and translucent halo are not
solid housing or ground offsets. The previous invented protective cap is removed.
Mount height (2.3 tiles), hidden depth, opaque glass shading, rounded shape and
broken glass edges remain inferred. Red tint affects the whole fixture, matching
the source Sprite color. There is no emitted light, transparency simulation,
fidelity approval or all-state completion claim.

All 325 saved locations use the room side of an adjacent wall. Explicit fitting
against six exact modeled wall prototypes corrects 310 clearances without
changing saved position, facing or along-wall spacing. All 1,625 state placements
clear 104,765 modeled-neighbor comparisons under conservative 15-axis SAT within
four tiles on the same level. Unknown/cross-level neighbors are outside this check.

The 80 reference images match 327,680 source RGBA pixels after declared tint.
That verifies reference extraction, not 3D pixel fidelity. Export checks cover
325 unique saved locations, 484 exported roots and 3,386 part transforms/colors.
Four single-model GLBs contain all five poses. The 100 refreshed region exports
and 75 added regions show the default map poses. Library animation count remains
12 across three other models.

Review: `/viewer/light-states.html`, `Tools/three_d/generated/small-light-comparison.png`
and `Tools/three_d/generated/small-light-verification.json`. Native 282 / Python
184 / browser module 39 checks pass. Client build: zero errors, 2,150 warnings.
All 794 library and 793 root scene GLBs validate without errors or warnings.
Browser state/facing/source controls and offline renders were inspected; native
interaction and browser canvas screenshots remain unverified.
