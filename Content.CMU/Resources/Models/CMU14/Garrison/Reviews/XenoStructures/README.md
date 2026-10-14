# Hive structure review

The refinement replaces the initial generic assemblies with distinct organ silhouettes, tapered folds, source resin skin detail, flared hollow chimneys and a cradle with small egg groups. `overview.png` shows representative assets. `assets-01.png` through `assets-12.png` compare all 92 assemblies with the original sprites from the front, rear and underside. `states.png` covers door retraction, egg hatching/debris and cardinal weed connections. These are offline geometry renders, not in-game screenshots.

`coverage.json` records 112 eligible non-mob prototypes: 104 use the refined models, four retain maintenance-cover models, and four transient construction effects retain sprites. Invisible helpers and living mobs are excluded.

## Verification

The focused command built the content projects and passed both tests:

```powershell
powershell.exe -NoProfile -File .codex/scripts/run.ps1 test -Project Content.IntegrationTests -Filter 'FullyQualifiedName~CMU3DXenoAppearanceTest|FullyQualifiedName~OptionalLibraryLoadsOnlyOnClientAndCanReloadAfterPrototypeReset'
```

The tests exercise real model loading and state selection: opening clears the door passage, hatching clears the egg mouth, hidden layers stay hidden, unknown appearances fall back, and optional geometry stays unloaded on 2D clients and servers.

Khronos glTF Validator 2.0.0-dev.3.10 checked the 92 rebuilt exports: zero errors and zero warnings. Manifest hashes match all 2,915 library exports. The audit also checks composed visible layers against the live 128-part limit; the largest supported composition contains 124 parts. No renderer, gameplay, networking or AI code changes are included in this refinement.

`verification.json` contains measured geometry counts. The refined default assemblies total 871,060 triangles and peak at 23,804; ground weeds have 36 shallow solid parts and 432 triangles. The added detail increases geometry relative to the first drafts. These counts are not FPS measurements; live crowded-hive performance has not been measured.

All-side renders and source/frame validation are complete. A live multiplayer hive playthrough and final art acceptance remain outstanding, so assets retain draft status. Depth and unseen surfaces are inferred from the sprites. See `../../SOURCES_XENO_STRUCTURES.md` and the texture folder's `sources.json` for attribution.
