# RobustToolbox v287.0.0 migration

Date: 2026-08-07

This document records the incremental engine and content migration from RobustToolbox v286.0.0 to v287.0.0 while merging the matching Space Station 14 content.

## Selected release and pins

| Item | Before | After |
| --- | --- | --- |
| RobustToolbox | `724345afdffcdedebc43577654385a9ecfe3a092` (`v286.0.0`) | `9e9eb234ba41e55b1d8e107ac40cefda7b819ff0` (`v287.0.0`) |
| Space Station 14 | common baseline `1e0e5e540ba3643ee93b86ada324d1163fd096b3` | `0087233f6116d9c544544a88baab5461af73fa08` |

The selected engine is the latest published RobustToolbox release and is the exact gitlink pinned by the merged Space Station 14 revision. Keeping these pins together preserves the engine/content API boundary. See the [v287.0.0 release](https://github.com/space-wizards/RobustToolbox/releases/tag/v287.0.0).

## Engine optimizations

The v287 engine update brings the upstream rendering, serialization, dependency-injection, PVS, game-state, and prototype-memory optimizations without copying engine implementation into content. Notable changes include cached post-shader targets and texture UVs, RSI draw fast paths, faster type and component-registry serialization, generated dependency injection, reduced game-state allocation, and interned prototype components.

Interned prototype components are immutable shared data. Content must copy data out of prototypes instead of mutating prototype-owned components.

## Content-side adaptations

- Converted the per-frame relayed eye-offset event to a value type and relayed it by reference.
- Ported polygon occluder rendering, batched ambient-occlusion drawing, reusable ray-result buffers, and polygon containment checks.
- Converted all 21 fork-owned rectangular occluders from `boundingBox` to equivalent four-vertex polygons and added the upstream diagonal-wall polygon.
- Migrated CMU Z-level visibility and map loading, solution access, and research-tree calls to the current APIs.
- Kept CMU-only dropship faction lookup code in a `Content.CMU/Shared` partial rather than adding a CMU dependency to the canonical RMC implementation.

## Warning policy

`Directory.Build.targets` retains the existing content compatibility suppressions for transition APIs still used by upstream content. Engine-only suppressions remain scoped to projects under the immutable `RobustToolbox/` gitlink; no engine source files are modified.

## Verification

Run from the repository root:

```powershell
dotnet restore SpaceStation14.slnx
dotnet build Content.Shared/Content.Shared.csproj --configuration DebugOpt --no-restore
dotnet build Content.Client/Content.Client.csproj --configuration DebugOpt --no-restore
dotnet build Content.Server/Content.Server.csproj --configuration DebugOpt --no-restore
dotnet build Content.IntegrationTests/Content.IntegrationTests.csproj --configuration DebugOpt --no-restore
dotnet test Content.Tests/Content.Tests.csproj --configuration DebugOpt --no-restore
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --configuration DebugOpt --no-build --no-restore --filter "FullyQualifiedName~CMUFactionTechPrototypeTest"
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --configuration DebugOpt --no-build --no-restore --filter "FullyQualifiedName~StableGarrisonZLevelSpawningTest"
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --configuration DebugOpt --no-build --no-restore --filter "FullyQualifiedName~ClientPrototypeSaveLoadSaveTest"
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --configuration DebugOpt --no-build --no-restore --filter "FullyQualifiedName~ServerPrototypeSaveLoadSaveTest"
```

The migration gate passed with 573 unit tests, three faction-tech integration tests, the Stable Garrison multi-Z regression, and both client and server prototype save-load-save round trips. The full server prototype-serialization validator still reports 662 prototypes from inherited catalog and localization debt; the v287 migration removed 408 failures, and none of the remaining roots use the migrated metabolism keys, resource paths, emotes, body prototype, or renamed RMC tags.
