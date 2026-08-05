# RobustToolbox v283.0.0 migration

Date: 2026-07-19

This document records the RobustToolbox release selection, the engine-side change inventory, and the content/build migrations required to move the fresh RMC14 baseline to RobustToolbox v283.0.0.

## Selected release and pins

| Item | Before | After |
| --- | --- | --- |
| RobustToolbox | `ea09bdafbf0f55919b237960c4d7ed534daed9f8` (`v264.0.2-fix-physics`) | `7bfa10ec04bfc8f00956419609bd6ec370f9bbac` (`v283.0.0`) |
| .NET SDK | 9.0.100 | 10.0.100 with `latestFeature` roll-forward |
| Lidgren.Network | previous pin | `68a5b883` |
| NetSerializer | previous pin | `c32b756` |
| Robust.LoaderApi | previous pin | `2b6a7d` |
| XamlX | previous pin | `5da4e1` |
| CefGlue | previous pin | `f8f5135` |

`v283.0.0` was selected because it is the newest version with a published RobustToolbox release and authored release notes. A later `v283.1.0` Git tag exists, but it is not a published GitHub Release and was deliberately not used. See the [v283.0.0 release](https://github.com/space-wizards/RobustToolbox/releases/tag/v283.0.0).

## Comparison limits

The old `v264.0.2-fix-physics` lineage and the published `v283.0.0` lineage have no merge base. RobustToolbox history was rewritten/split, so a normal commit-range changelog would be misleading.

- `git rev-list --left-right --count old...new` reports 9,536 commits only on the old lineage and 214 commits only on the new lineage.
- A direct tree comparison covers 1,431 files, with 45,045 insertions and 45,329 deletions.
- The appendix therefore lists all 214 commits in the target release lineage, while the release sections summarize the published behavior changes.

## Published release changes

### v277.0.0, v278.0.0, and v279.0.0

The published pages contain version markers but no authored changelog. Their exact target-lineage commits are preserved in the appendix: [v277.0.0](https://github.com/space-wizards/RobustToolbox/releases/tag/v277.0.0), [v278.0.0](https://github.com/space-wizards/RobustToolbox/releases/tag/v278.0.0), and [v279.0.0](https://github.com/space-wizards/RobustToolbox/releases/tag/v279.0.0).

### v280.0.1

- Fixed `DynamicTree.Clear()` retaining node references.
- Reverted `UiBox2i` constructor validation that regressed debug UIs.
- Restored command-completion ordering and adjusted Lidgren rate limits.
- Serialized `EyeComponent.DrawLight`.

Source: [v280.0.1 release](https://github.com/space-wizards/RobustToolbox/releases/tag/v280.0.1).

### v281.0.0

- Updated Lidgren with MTU, NAT, malformed-packet, and rate-limit work.
- Exposed the corresponding networking properties and CVARs.
- Fixed server-side subscription targeting in `EntitySystemSubscriptionsGenerator`.

Source: [v281.0.0 release](https://github.com/space-wizards/RobustToolbox/releases/tag/v281.0.0).

### v282.0.0

- Merged the Serv5 rewrite of DataDefinition serialization internals.
- Added readonly-field writes.
- Required `[DataDefinition]` wherever `[DataField]` is used.
- Copied eligible value types directly instead of round-tripping through custom-copy paths.
- Reduced expression-tree use, enabling more concurrent tests.

Source: [v282.0.0 release](https://github.com/space-wizards/RobustToolbox/releases/tag/v282.0.0).

### v283.0.0

- Changed generated component networking to clear/add compatible collections on clients.
- Deferred BUI state application to the update loop and exposed selected file names in dialogs.
- Removed `GridEventHandler`.
- Added opt-in `NetMessage.EstimateBufferSize()` pooled-buffer sizing.
- Added audio variation helpers, public audio auxiliaries, uncached owned-texture loading, public/virtual table containers, palette helpers, FOV render-target access, and non-generic `TryComp` proxies.
- Improved connection-error selection.
- Fixed grid traversal over static entities, one-field state deltas, generator interface casts, PVS session disposal, and window resize jitter.
- Improved serialization-generator clean/incremental build times and other debug/release hot paths.

Source: [v283.0.0 release](https://github.com/space-wizards/RobustToolbox/releases/tag/v283.0.0).

## Applied repository migrations

### Build and dependency layout

- Reset content to `RMC-14/RMC-14` on branch `Rebase`; the previous CMU state remains on `Chip/backup-cmu-before-rmc14-reset-2026-07-19`.
- Updated `global.json`, net9 projects, and CI workflows to .NET 10.
- Updated setup-dotnet actions and OpenTK 4.9.4 APIs.
- Removed the missing OpenToolkit solution project.
- Replaced direct engine project references with the supported `RobustToolbox/Imports/*.props` imports.
- Added explicit content-owned package references where the old transitive dependency graph had hidden them.
- Synchronized every nested RobustToolbox submodule.

### ECS, map, and prototype APIs

- Replaced obsolete `IMapManager` use with `SharedMapSystem`/`MapSystem`, including integration tests and map rendering.
- Resolved the client mapping state against the concrete `MapSystem` required by v283 and raised store integration-test events locally, matching the current ECS dispatch contract.
- Migrated grid-node, grid-query, coordinate, entity-system proxy, `TryComp`, and prototype-component access to v283 signatures.
- Added the content-side scale-visual component/systems that v283 intentionally removed from the engine.
- Updated auto-network-state attributes, after-state events, serialized property setters, DataDefinitions, DataRecords, and generated-property conflicts for Serv5.
- Updated toolshed prototypes, station/prototype lookups, timers, and entity-lifetime guards.
- Removed obsolete saved-map `Timer` components and converted direct RSI-state PNG icon paths to structured sprite/state specifiers.
- Normalized the invalid `Medical Doctor` guide prototype ID to `MedicalDoctor` and updated every matching guide reference.
- Updated the RMC map plating validation to dispose every v283 `LoadResult`, keeping standalone validation memory bounded.

### Analyzer and compiler improvements

- Applied 15,478 shared/client/server analyzer migrations across dependency injection, partial types, and serialized setters.
- Replaced literal prototype/tag/tool/faction IDs with typed `ProtoId<T>` fields.
- Stopped treating legacy literal construction names/descriptions and rendered short-key labels as Fluent message IDs; valid localization IDs still resolve normally while literal fallback text no longer emits warning spam.
- Added a mixed localization/literal display-name contract for RMC tile prototypes, used it in mapping actions and server pointing, and supplied all 11 missing en-US `tiles-*` messages found in the RMC tile resources.
- Removed invalid pause generation from components without paused fields.
- Replaced repeatedly parsed static regex calls; dynamic localized patterns are escaped and compiled once per replacement.
- Fixed XAML-generated `Label` members that hid `Button.Label`.
- Replaced obsolete `Thread.VolatileRead`, OpenTK window/cursor APIs, and benchmark RNG usage.
- Removed intentional dead code after unconditional returns and expressed the disabled integration test with `[Ignore]`.
- Fixed a real leap-protection bug that assigned `InherentBlockSound` to itself instead of restoring `BlockSound`.
- Fixed signed bit-packing warnings by preserving the intended unsigned 32-bit halves.
- Fixed hidden/overridden RMC ban, tactical-map, and BUI APIs, nullable dependencies, unused fields, and an unawaited Discord shutdown.

## Warning policy

The initial non-incremental build produced 1,426 warnings (1,424 unique diagnostics): 1,303 from content/root projects and 121 from the immutable engine tree. All non-deprecation content diagnostics were fixed or given a line-scoped explanation.

`Directory.Build.targets` suppresses `CS0612`, `CS0618`, and `CS0672` after project-local properties. v283 deliberately marks still-supported transition surfaces obsolete while upstream content remains on them: legacy status effects, control disposal, sprite mutation, component ownership, and component-owned VV setters. Migrating those systems is semantic rewrite work, so this reset records them as compatibility debt instead of changing gameplay behavior mechanically.

Engine-only diagnostics are suppressed from the repository root because `RobustToolbox/` is pinned external code and is not edited. The suppression list is limited to diagnostics actually emitted by v283 under .NET 10.

NuGet auditing remains enabled. Exact advisory exceptions are used for:

- Pow3r's unused `Veldrid -> SharpDX -> Microsoft.NETCore.App 1.0.x` runtime assets; Pow3r targets net10.0 and does not consume those assets.
- The externally pinned RSI.NET ImageSharp package. RSI.NET was not edited; its submodule should be advanced separately when authorized.

## Verification

Run from the repository root:

```powershell
dotnet restore SpaceStation14.slnx
dotnet build SpaceStation14.slnx --no-restore --nologo --verbosity:minimal
```

Final verification results:

- Complete solution build: 0 warnings, 0 errors.
- Unit tests: 368 passed, 1 skipped, 0 failed.
- Store and mapping integration regressions: 2 passed, 0 failed.
- Serial prototype and map validation group: 4 passed, 0 failed.
- Standalone RMC plating validation: 1 passed, 0 failed; memory remained bounded at approximately 2.8 GB.
- Guidebook prototype-content validation: 1 passed, 0 failed.
- Client literal/tile-localization regression: 1 passed, 0 failed.
- YAML/prototype linter: no errors.

The all-in-one integration-test process was stopped when its shared fixture pool reached approximately 12 GB. The affected validation groups were then run serially and all passed; this avoids conflating aggregate test-host retention with the standalone plating validator, whose `LoadResult` cleanup now remains bounded.

A real client/server replay of the reported warning sequence reached `InGame` over both IPv4 and dual-stack IPv6 with zero localization warnings and zero dropped `MsgStateAck` messages. The remaining `MainLoop: Cannot keep up!` line is emitted by RobustToolbox when Debug startup exceeds its five-tick backlog window; it is an engine startup-timing diagnostic rather than a content failure and was not hidden or patched in the external engine tree.

## Target release lineage: all 214 commits

Because the histories have no merge base, this is the complete reachable history of the published target commit rather than a synthetic `old..new` range.

- `08a3d120b` 2026-05-08 — Version: 277.0.0
- `6e7876317` 2026-05-08 — Improved collision filter test (#6431)
- `aac0e4d1c` 2026-05-11 — Remove comment (#6560)
- `2e5f20cfb` 2026-05-13 — Fix Overrides In WrapContainer (#6561)
- `00c1a9f9d` 2026-05-14 — Fix BoxContainer's SeparationOverride (#6570)
- `0dc7da78c` 2026-05-19 — Invalidate the measure of BoxContainer's SeparationOverride (#6572)
- `b7de935f3` 2026-05-20 — Make windows keep relative position when game re-sizes
- `321bd3cf3` 2026-05-19 — Fix swapped args in grid lookup (#6578)
- `611023acb` 2026-05-22 — Add `SharedMapSystem.GetFilledTileCount` (#6562)
- `f1af4bb3b` 2026-05-23 — Merge remote-tracking branch 'upstream/master' into 2026-05-20-window-fix
- `7bdec921c` 2026-05-25 — Update .gitignore to ignore C# Dev Kit cache (#6593)
- `0a648ab61` 2026-05-25 — More Cursor Usage in Controls (#6583)
- `a79c6bbc1` 2026-05-26 — Add Track StyleProperty to ScrollBar (#6559)
- `6d5212916` 2026-05-26 — Update submodules for lscache gitignore
- `5ff88372b` 2026-06-04 — Use AsSpan for audio resource signatures (#6613)
- `bd6b7068f` 2026-06-03 — Small fix to doc comment (#6616)
- `94c20cae2` 2026-06-04 — Add scroll lock key (#6617)
- `72ae628a5` 2026-06-07 — Optimise sprite sorts
- `338825c8e` 2026-06-10 — Add Pure attr to EntityLookup bounds methods (#6622)
- `9d677d227` 2026-06-11 — Reduce per-frame allocs for openGL logs
- `8e98d5479` 2026-06-11 — Also this one
- `e0496a3ca` 2026-06-11 — Reduce TryParseEnum string allocs
- `ee6e7994e` 2026-06-13 — ResPath allocs fix
- `ff67da37b` 2026-06-16 — Cleanup Robust integration test boilerplate (#8)
- `ae38b3a56` 2026-06-15 — Update Lidgren submodule to fix DOS attack, fix by Darkrell from Starlight
- `bae997f8b` 2026-06-16 — Build and test against our own content repo (#2)
- `b9e96ce8e` 2026-06-16 — Fix wrong submodules being used in "Test content master against engine" workflow (#18)
- `3e72678d1` 2026-06-16 — Release notes
- `fe368d63f` 2026-06-16 — Version: 277.1.0
- `e42f9f692` 2026-06-16 — Remove broken GHAs
- `d09a9b8a2` 2026-06-16 — Fix .gitmodules urls to use the new repositories
- `d3faec7fd` 2026-06-17 — Merge pull request #19 from kontakt/20260616-cleanup
- `2d8d5c303` 2026-01-24 — fix not giving prototype class that wasnt registered
- `c9d5f31fb` 2026-01-24 — add Proto field to EntitySystem for proxy and all systems to use
- `1be04a588` 2026-01-24 — add HasComp methods to EntityPrototype
- `c12e4e7ac` 2026-01-24 — add HasComp proxy methods for EntProtoId and EntityPrototype
- `0c4a40c6a` 2026-01-24 — add tests for EntityPrototype.HasComp methods
- `a86e33ccb` 2026-05-17 — a
- `6522ff42e` 2026-05-17 — Apply suggestions from code review
- `f9285784f` 2026-05-17 — Update Robust.Shared.IntegrationTests/Prototypes/PrototypeHasCompTest.cs
- `dc2cbd952` 2026-05-17 — CompName gaming
- `9160f4860` 2026-05-17 — fix remark on no ignored components
- `cb768d2aa` 2026-06-18 — Raise a BUI message on any input
- `51cdfb6b7` 2026-06-18 — Add "hidden" console commands
- `a008dda4f` 2026-06-18 — add pure
- `e1ebbd0ac` 2026-06-18 — !
- `325a1a43c` 2026-06-19 — Merge pull request #26 from deltanedas/proto-hascomp-ops
- `dc7bb35ca` 2026-06-19 — Make Box2.Contains faster
- `bfaccfced` 2026-06-18 — Test building content master against engine in release configuration (#35)
- `97b4e33c6` 2026-06-19 — Merge remote-tracking branch 'upstream/master' into 2026-06-11-ogl-string-allocs
- `85c075d48` 2026-06-19 — Merge pull request #6 from Space-Wizards-Federation/2026-06-11-ogl-string-allocs
- `1173a3048` 2026-06-19 — Merge pull request #7 from Space-Wizards-Federation/2026-06-11-enum
- `ab3217fc7` 2026-06-19 — Merge pull request #9 from Space-Wizards-Federation/2026-06-13-res-path
- `af82ba744` 2026-06-19 — Merge remote-tracking branch 'upstream/master' into 2026-05-20-window-fix
- `5600ae5e8` 2026-06-19 — Fix relative window positions on display re-size
- `faad9098f` 2026-06-19 — Update release notes
- `3000a7632` 2026-06-19 — Merge remote-tracking branch 'upstream/master' into 2026-06-07-sprite-sort
- `75e3e75e5` 2026-06-19 — Update release notes
- `fa874bf20` 2026-06-19 — Merge remote-tracking branch 'upstream/master' into 2026-06-07-sprite-sort
- `703cf3293` 2026-06-19 — RN
- `c89ba529f` 2026-06-19 — Optimise sprite sorts
- `61855deaf` 2026-06-19 — Merge remote-tracking branch 'upstream/master' into 2026-06-19-box2-caret
- `f48ac8c01` 2026-06-19 — RN
- `ed8cc71cd` 2026-06-19 — Make Box2.Contains faster
- `751952a85` 2026-06-19 — Merge remote-tracking branch 'upstream/master' into 2026-06-18-bui-message
- `32237cf71` 2026-06-19 — RN
- `75d1c9a7f` 2026-06-19 — Raise a BUI message on any input
- `8f6ebad5b` 2026-06-19 — Reduce game memory usage a crumb (#30)
- `fcf255e11` 2026-06-20 — Make Box2Rotated Transform faster (#34)
- `ead6018a6` 2026-06-19 — Add markup escaping Fluent functions
- `a1a03d9fa` 2026-06-19 — Allow specifying tooltip in cmdlink tag
- `30cac6ec2` 2026-06-20 — Add CommandParsing.EscapeCommand
- `a4f427557` 2026-06-20 — Add IUserInterfaceManager.GetRootForMouse and Popup.OpenAtCursor
- `fbab67805` 2026-06-20 — Add `FormattedStringBuilder` for safely constructing markup with code.
- `1a09f17da` 2026-05-07 — Add HBox and VBox convenience types.
- `c1919263f` 2026-06-20 — Track isLocal in user data (#6641)
- `f41b2d5fa` 2026-06-20 — bump natives to 0.2.5
- `24da77408` 2026-06-20 — Release notes for c1919263f4604ad306a95a11f190d0e8ce3c7b4f
- `8af60684d` 2026-06-20 — Update release notes
- `7cfce4363` 2026-06-20 — Version: 277.1.0
- `4f1563162` 2026-06-20 — fix publish-client.yml
- `d0decda59` 2026-06-20 — fix it actually this time
- `a80e78692` 2026-06-20 — Replace default Auth as well as README and other links (#49)
- `5718e9e38` 2026-06-20 — Release notes
- `3c77bfde6` 2026-06-20 — Version: 277.2.0
- `e568c7ba7` 2026-06-21 — LocalRotation normalize + obsolete setter (#125)
- `bec88c87a` 2026-06-21 — Add NotNullWhenTrue to EntityPrototype.TryComp factory overload (#132)
- `931b2a098` 2026-06-21 — Add trust scores for localhost@ and guest@ connections (#6642)
- `e164c9463` 2026-06-23 — Direction optimisations (#135)
- `31053007f` 2026-06-23 — fix websocket no dispose (#6646)
- `b8c946a27` 2026-06-24 — Release notes
- `f600a84ad` 2026-06-24 — Version: 277.2.0
- `d07c1af29` 2026-06-24 — Early-out PVS updates on rotation (#133)
- `27c2f3487` 2026-06-24 — Implement IList for ValueList (#32)
- `b32ef31b0` 2026-06-24 — Fix ValueList TryPop holding references (#31)
- `ea2529e1c` 2026-06-25 — Fix ValueList Peek (#145)
- `6712c4fd4` 2026-06-25 — fix publish
- `2b63bfdcc` 2026-06-25 — Update Lidgren.Network
- `d71a1cc1b` 2026-06-25 — Release notes
- `f47f6b02b` 2026-06-25 — Version: 277.2.1
- `dbaf8c4e8` 2026-06-26 — Merge branch 'new_master' into master-merge
- `e519bcdc9` 2026-06-26 — undo some changes specific to the new repo
- `f7b6a24e2` 2026-06-26 — update release notes for merge from new to old repo
- `1b9de71b8` 2026-06-26 — Remove obsolete TryIndex overloads (#6478)
- `f4e404a61` 2026-06-26 — (Try to) Fix a null reference exception in ComponentTreeSystem.UpdateTreePositions (#6604)
- `8862e8d63` 2026-06-26 — Debug Console Autocompletes for Contains (#6418)
- `03ef50328` 2026-06-26 — Add batch drawing methods (#6655)
- `afd792c0b` 2026-06-26 — Merge branch 'master' into master-merge
- `9faee42c3` 2026-06-27 — Merge Bouba RT into Kiki RT (#6649)
- `c51009d4f` 2026-06-27 — Allow foreach with EntityQueryEnumerator and AllEntityQueryEnumerator (#6660)
- `7b25f1e25` 2026-06-27 — Fix DataRecord serialization (#6619)
- `c0e29a3ad` 2026-06-27 — Add PhysicsBodyStatusChangedEvent (#6614)
- `9c45f8c86` 2026-06-27 — Simplify generated code check in `AccessAnalyzer` (#6581)
- `5dbf86fb9` 2026-06-27 — IMapManager to hospice care (#6579)
- `7fcf49ea6` 2026-06-27 — Add audio device switching API (#6661)
- `13730c920` 2026-06-27 — Version: 278.0.0
- `c6cf80334` 2026-06-27 — Server & shared containerSystem clean up (#6564)
- `7e74ae708` 2026-06-27 — Reduce `ReallyBeIdle` default ticks (#6675)
- `08dd77afc` 2026-06-27 — Fix OnClientRequestFull throwing an error when trying to log data about a deleted entity (#6420)
- `6c017eba7` 2026-06-28 — Fix ApplyLinearImpulse treating the impulse as being both in world and local space (#6444)
- `dd2d4b118` 2026-06-28 — Fix `Robust.Benchmarks` compile (#6679)
- `c3f80b99b` 2026-06-28 — Change SpawnAtPoisition EntityCoordinates overload to use the rotation of the attached entity, and to allow a rotation override.  (#6527)
- `d9cd400e0` 2026-06-28 — Optimise Box2i (#6662)
- `7dbb2a261` 2026-06-28 — Clean up Player manager (#6566)
- `2cbcb92cb` 2026-06-28 — Add DictionaryEquals helper (#6682)
- `8da4630d7` 2026-06-29 — SharedPhysicsSystem.Fixture typo fix one line (#6687)
- `e128a7f69` 2026-06-29 — Partially Revert "More Cursor Usage in Controls"  (#6654)
- `39306dfd2` 2026-06-29 — feat: raise event on audio despawns (#6056)
- `d9330ecda` 2026-06-30 — Add test timeouts (#6695)
- `dfe1d4ef8` 2026-06-30 — Cleanup: ProtoMan Part 3 - remove redundant EntitySystem IPrototypeManager, IComponentFactory instances (#6693)
- `58fd434a5` 2026-06-30 — Remove QuadTree (#6664)
- `9c010a121` 2026-06-30 — Add .ftl file upload support (#6473)
- `e458c13e1` 2026-06-30 — Basic Tracy support (#6686)
- `1322177e6` 2026-06-30 — IEntitySystem Event Subscription Code Generation (#6227)
- `aeadf07a0` 2026-07-01 — Version: 279.0.0
- `792f795fb` 2026-07-01 — Sourcegen eventsub fixes (#6702)
- `fb70123a9` 2026-07-01 — Version: 279.0.1
- `80560814f` 2026-07-02 — UiBox2i validation (#6665)
- `e3851fa31` 2026-07-01 — Fix template (#6703)
- `f2625bf5c` 2026-07-01 — Removes IMapManager (#6584)
- `3d1cbc6e2` 2026-07-01 — Update Lidgren.Network (#6707)
- `9b00e2d93` 2026-07-02 — Add lidgren rate-limited logging CVars (#6708)
- `098508f3a` 2026-07-01 — upd release notes
- `0a6b4a6fb` 2026-07-01 — Version: 280.0.0
- `fbef47820` 2026-07-02 — Fix `EntitySystemSubscriptionsGeneratorAttributes` xmldocs (#6709)
- `6a6cff2b8` 2026-07-04 — Serialize EyeComponent.DrawLight (#6433)
- `38019943b` 2026-07-04 — Add cvars for new lidgren stuff (#6658)
- `38a8e3202` 2026-07-04 — Less restrictive box2iui validation (#6717)
- `ece4f7d9b` 2026-07-04 — add methods to support generating color palettes
- `8027cbd39` 2026-07-04 — Fix DynamicTree Clear (#6651)
- `aae482c97` 2026-07-05 — guess we doin arrays now
- `d042eb002` 2026-07-05 — robust random
- `4a8ab7e59` 2026-07-05 — schmoovin
- `249660be3` 2026-07-06 — angels
- `622ecc624` 2026-07-06 — Fix command ordering (#6729)
- `893750195` 2026-07-06 — tests
- `e9b28d80c` 2026-07-06 — Update lidgren ee2e945 (#6736)
- `b35ef5487` 2026-07-06 — upd release notes
- `b366e2975` 2026-07-06 — Version: 280.0.1
- `4e69bf267` 2026-07-07 — yeas
- `a1d3d16e4` 2026-07-07 — Update lidgren (#6737)
- `332222b13` 2026-07-06 — Add missing EntitySystemSubscriptionsGenerator.targets into Server.csproj (#6740)
- `cd2026606` 2026-07-07 — Add new Lidgen CVars (#6742)
- `1acad0a44` 2026-07-06 — upd release notes
- `51913660b` 2026-07-06 — Version: 281.0.0
- `83c5c8eaf` 2026-07-07 — Fix UserDataDir accepting relative game names (#6745)
- `471e92d92` 2026-07-07 — Update lidgren (#6750)
- `47572c2e5` 2026-07-07 — make TableContainer virtual and public
- `ba96b63f9` 2026-07-08 — Fix ShapeCast on sensors (#6588)
- `34ce2ba53` 2026-07-08 — serv5 (#6496)
- `960edb32c` 2026-07-08 — Version: 282.0.0
- `c2d0490a9` 2026-07-08 — Add `EntitySystem.TryComp(EntityUid, Type, out IComponent?)` proxy method (#6746)
- `dcadd3438` 2026-07-09 — Serv5 fixes (#6761)
- `eb25a0380` 2026-07-09 — fuckit one more
- `4d1e39373` 2026-07-09 — OOPS
- `7ffe5fc58` 2026-07-10 — yeah ok
- `167a93a94` 2026-07-10 — test edits
- `b0b1fa354` 2026-07-11 — FovRenderTarget (#6781)
- `292dedc86` 2026-07-11 — Add methods to support generating color palettes (#6720)
- `5a83ab127` 2026-07-11 — Try to make connection failure msg useful (#6743)
- `2e07cfcbd` 2026-07-11 — Add TestPair ID to GetPair logging (#6780)
- `72d56f37d` 2026-07-11 — Remove unused GridEventHandler delegate (#6777)
- `89a054696` 2026-07-11 — Move EventBus benchmarks to the Engine. (#6778)
- `657e9bfb2` 2026-07-12 — Reduce ImageSharp arraypool ballooning (#6768)
- `1c84809e0` 2026-07-12 — Make TableContainer virtual and public (#6753)
- `b3daa0b79` 2026-07-14 — Fix BaseWindow jittering on resize (#6788)
- `d314fe48a` 2026-07-14 — Avoid NetMessage copies for MsgState (#6744)
- `6052c5026` 2026-07-14 — Dispose PVS session statestream on disconnect (#6663)
- `07ddeaa8d` 2026-07-14 — File Dialog: Expose file name of the selected file (#6755)
- `3211b437c` 2026-07-14 — Cache GetAllChildren (#6766)
- `205a8a647` 2026-07-14 — Pool PVS entity state collections (#6752)
- `4e11a3892` 2026-07-15 — Add API for loading OwnedTexture with texture parameters (#6794)
- `62e3bdbd2` 2026-07-15 — Replace overly restrictive (and sometimes wrong?) cast in serialization generator (#6796)
- `e6a7849fe` 2026-07-16 — Skip log formatting if level not enabled (#6764)
- `7454c6b31` 2026-07-16 — Fix serialization normalize performance (#6797)
- `91f64a7f6` 2026-07-16 — Add sundries + static sundries to grid traversal (#6653)
- `423ca3ba1` 2026-07-16 — Filter datadef types by attributes (#6798)
- `681fa0b5d` 2026-07-16 — Test fix (#6799)
- `15a7ed295` 2026-07-16 — Fix DistanceProxy.Set allocation spam (#6770)
- `f44d7e00c` 2026-07-16 — Defer UI operations until UI system runs frame update (#6789)
- `17b66c574` 2026-07-16 — Fix SetUniformDirect allocation spam (#6769)
- `9ceb7c5b4` 2026-07-16 — Workflow standardisation + pruning (#6726)
- `a60d2df5b` 2026-07-16 — Cache datadef types (#6801)
- `9e1c568cd` 2026-07-16 — Allow content to read actual auxiliary off of `AudioAuxiliaryComponent` (#6800)
- `651e9034d` 2026-07-16 — Cache grafana histograms (#6767)
- `a978e3356` 2026-07-16 — Cache entity system profile names (#6765)
- `36969713a` 2026-07-16 — Fix physics hull allocs (#6802)
- `ab4db16d0` 2026-07-16 — Fix calling Dirty+DiryField on same tick (#6685)
- `7c7d1ae45` 2026-07-16 — Cleanup: Warning resolution/suppression (#6721)
- `0e72d557d` 2026-07-16 — Add an AudioParams.AddVariation() function akin to AddVolume() (#5804)
- `dfb397647` 2026-07-16 — Omit .Collect() for sourcegen (#6803)
- `faabd8434` 2026-07-16 — Fix componentnetworkgenerator not copying getstate on client (#6748)
- `86464ca98` 2026-07-16 — Test building content master against engine in release configuration (#6557)
- `7bfa10ec0` 2026-07-16 — Version: 283.0.0
