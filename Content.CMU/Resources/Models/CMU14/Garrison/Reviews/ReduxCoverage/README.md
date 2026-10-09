# Stable Garrison Redux model coverage

This pass adds 138 draft model entries and 203 exact world-prototype bindings.
The audit follows all seven Redux levels, hidden subfloor placements, recursive
map spawners, faction vendor markers, platoon vehicle catalogs and faction
terminals. It finds 1,734 valid physical prototype types: 1,726 have exact model
bindings, four have conditional sprite-selected models, and four use existing
native stairs or atmospheric/void presentation. No valid audited type is left
without one of those paths. This measures bindings, not complete live-state fidelity.

The additions include 110 faction vendor bindings, both cable families and their
16 connection masks, straight/bent/routed/trunk/Y disposal castings and construction
poses, 26 supply vehicle variants, 35 mounted turret item variants, oxygen
canisters, machinery, shaft walls, ladders, lift platforms and loose map props.
The flipped router is also modeled so flipping a mapped router keeps its geometry.

All new default models are below the existing 128-part limit; the largest is
71 parts. Vehicle hulls, wheel layers and installed equipment are composed from
the actual visible source states. Independent turret entities use physical yaw,
not the camera-facing rotation applied to their 2D sprite layers. Removing or
changing a hardpoint changes its modeled assembly. Unknown equipment states
retain the original sprite rather than silently losing equipment.

![Source art and solid models from front, rear and below](contact-sheet.png)

## Verification

- Content client, shared and server projects built through the repository test wrapper.
- 14 focused C# cases passed: `CMU3DVehicleAppearanceTest` and `CMU3DAnchorStateTest`.
- All four `CMU3DModelLoadingTest` integration cases passed, covering optional
  client-only loading, workbench selection, library release/reopen and texture loading.
  `OptionalLibraryLoadsOnlyOnClientAndCanReloadAfterPrototypeReset` also passed
  again against the final generated assets.
- 338 existing Python asset-tool tests passed with `Tools/three_d` on `PYTHONPATH`.
- The new GLBs were regenerated with `build_models.py`; all delivered manifest
  file hashes were verified. Source references and positive-volume geometry were
  validated, and source/front/rear/underside review sheets were generated.
- `redux_coverage.py` reports no missing bindings among resolved physical types.

Focused commands:

```powershell
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.Tests -Filter 'FullyQualifiedName~CMU3DVehicleAppearanceTest|FullyQualifiedName~CMU3DAnchorStateTest'
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Filter 'FullyQualifiedName~CMU3DModelLoadingTest'
```

| Regression | Test |
| --- | --- |
| Removing a hardpoint or switching damage state changes only the visible assembly and preserves published snapshots | `RemovingHardpointAndChangingDamageStateRebuildsOnlyVisibleAssemblies` |
| Unrecognized equipment cannot silently disappear from a modeled vehicle | `UnknownStateOrResourceCannotSilentlyDiscardInstalledEquipment` |
| Mounted turrets use physical yaw without replacing unrelated/dropped items | `MountedTurretUsesItsOwnPhysicalYawWithoutBindingDroppedOrUnrelatedItems` |

## Limits and unresolved source data

These remain drafts. Inferred height, hidden construction and material response
need in-game acceptance. Lift travel, gear-wall animation, wheel texture animation,
aircraft effects and every small-item light/charge/stock overlay are not reproduced
by these static solids. The test results do not establish FPS or multiplayer
driving smoothness. No game collision, map elevation, AI or mob equipment behavior
is changed, and the existing optional-library lifecycle remains in place.

Six saved-map references have missing content definitions or missing parents:
`CMUGasPipeStraightAlt2`, `CMUGasThermoMachineFreezer`, `CMVentPump`,
`RMCGasPipeBend`, `RMCGasPipeStraight`, and `RMCGasPipeTJunction`.
They are reported separately in `coverage.json`; this pass does not invent gameplay
prototypes to bind them. `ChunkEntity` is an engine-owned, sprite-less map helper,
and is excluded from physical-model coverage.

Authoring and source/license notes: [SOURCES_REDUX_COVERAGE.md](../../SOURCES_REDUX_COVERAGE.md).
The procedural author is `Tools/three_d/author_redux_coverage.py`; the independent
coverage audit is `Tools/three_d/redux_coverage.py`.
