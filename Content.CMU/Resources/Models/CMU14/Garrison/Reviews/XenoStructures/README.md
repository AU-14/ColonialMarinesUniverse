# Hive structure review

`overview.png` shows representative models. `assets-01.png` through `assets-12.png` compare all 92 new assemblies with their source art, front/side, rear/side and underside. `states.png` shows retracting door leaves, egg hatching/debris and cardinal weed connections. These are offline geometry renders, not in-game screenshots.

`coverage.json` records all 112 eligible non-mob prototypes: 104 use new models, four reuse maintenance-cover models, and four transient construction effects retain sprites. Invisible helpers and living mobs are excluded. `verification.json` contains measured geometry counts and the complete library export count.

The focused integration command builds the client, server and shared projects:

```powershell
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Filter 'FullyQualifiedName~CMU3DXenoAppearanceTest|FullyQualifiedName~OptionalLibraryLoadsOnlyOnClientAndCanReloadAfterPrototypeReset'
```

Both tests passed. `SourceFramesOpenPassagesAndHiddenLayersDoNotBecomeGeometry` exercises the loaded assets through actual catalog/state-selection calls, checks that opening retracts solid geometry and hatching clears the egg mouth, and verifies hidden/unknown layer handling. The existing library lifecycle test verifies that 2D clients and the server do not load optional model geometry and that prototype reset/reload still works.

Khronos glTF Validator 2.0.0-dev.3.10 checked all 95 rebuilt exports (92 new assets and three updated bindings): zero errors and zero warnings. The complete library contains 2,915 exports with matching manifest hashes and no duplicate exact bindings. The new default assemblies have at most 73 solid parts and 4,008 triangles; ordinary weed tiles use 13 parts and 156 triangles. These are geometry counts, not measured frame-rate results.

All-side renders and source/frame validation are complete. A live multiplayer hive playthrough and final art acceptance remain outstanding; the assets retain draft status. Depth and unseen surfaces are inferred from the original sprites. See `../../SOURCES_XENO_STRUCTURES.md` for attribution and supported-state limits.
