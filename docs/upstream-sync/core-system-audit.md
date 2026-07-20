# SS14 upstream core-system audit

This ledger records CMU's integration of `space-wizards/space-station-14` into the
RMC-based `Rebase` branch. It is intentionally technical and durable: player-facing
changelogs are not a substitute for migration decisions, rejected changes, or debt.

Update this file in the same commit as each core-system integration. Use the next
`CS-####` identifier in the commit subject so Git history links the decision to its
implementation without recording a self-referential CMU commit hash here.

## Pinned baseline

Date established: 2026-07-19

| Item | Commit | Notes |
| --- | --- | --- |
| Shared SS14 baseline | `59633f6dc50e77dda8cefa344d87c7b01e06a810` | SS14 master on 2025-07-09; the merge base of current RMC and current SS14 history. |
| RMC merge carrying the baseline | `163efb562c0e48c9dfd7a73710511aa393779d19` | Merged SS14 master into RMC on 2025-07-09. |
| RMC reset baseline | `b6d677947dd8ebcb06194a66798938645fed5a54` | RMC master used to create `Rebase`. |
| Initial Rebase checkpoint | `29918ff342f3f46c6c953fcf6d0f5523597568af` | RMC plus the content-side RobustToolbox v283 migration and warning fixes. |
| Initial SS14 audit target | `40ca2c7f90d11d27be5457d177c133f0947d1c08` | SS14 master on 2026-07-19. Pin a new target when starting a later sync session. |
| RobustToolbox pin | `7bfa10ec04bfc8f00956419609bd6ec370f9bbac` | External engine boundary; do not resolve a content merge by changing this pointer. |

The complete baseline-to-target range contains 4,144 reachable commits, including
3,834 commits on SS14's first-parent history. The clone was originally shallow at
the SS14 tip and was deepened through 2025-06-01 before these counts were recorded.
Counts made before that deepening showed only four commits and must not be reused.

The `Rebase` branch was intentionally reset from CMU to RMC before this audit. At
the initial checkpoint it contains no `_CMU14` or `_AU14` paths. The pre-reset CMU
state remains at `Chip/backup-cmu-before-rmc14-reset-2026-07-19`; replaying that
state is a later integration phase and must not be inferred to have happened here.

## Core-system scope

Use every applicable area; do not force a change into only one category.

| Area | Includes |
| --- | --- |
| Movement | Input, mob movement, sprinting, pulling/dragging, buckling, climbing, movement modifiers, prediction, and related status effects. |
| Shooting | Guns, projectiles, hitscan, ammunition, recoil/spread, fire timing, targeting, melee-to-ranged interaction boundaries, and combat prediction. |
| Medical | Damage, body/organs, wounds, surgery, health states, revival, treatment, status effects, and medical UI. |
| Chemistry | Solutions, reagents, reactions, metabolism, injectors, hyposprays, dispensers, and chemistry UI. |
| Interactions | Use, activate, verbs, do-after, hands, inventory, storage, construction, click handling, and examination. |
| Physics | Fixtures, collision, grids/maps, anchoring, joints, explosions, spatial queries, and transform-driven simulation. |
| GameTicking | Tick/update scheduling, pause timing, prediction/reconciliation, entity lifecycle, round timing, and simulation-loop work. |
| Gamerules | Round start/end, game presets, objectives, antagonists, events, votes, win conditions, and rule-driven spawning. |

## Status and risk

- `Inventoried`: identified but not yet behaviorally reconciled.
- `Ported`: upstream behavior adopted without a downstream semantic change.
- `Adapted`: upstream behavior adopted with an explicit RMC/CMU adaptation.
- `AlreadyPresent`: equivalent behavior was already implemented downstream.
- `Deferred`: intentionally left on downstream behavior for a later focused audit.
- `Rejected`: upstream behavior intentionally does not fit CMU/RMC; give a durable reason.
- `Superseded`: a later entry replaces the decision; link both entries.

Risk is `Low`, `Medium`, or `High`. Treat prediction, timing, damage, collision,
round state, and broad prototype-contract changes as high risk unless validation
demonstrates otherwise.

## Integration rules

1. Preserve `_RMC14` and later `_CMU14` behavior when an upstream merge cannot be
   reconciled mechanically. Record preserved core behavior as `Deferred`, not as
   silently integrated.
2. Never edit or advance `RobustToolbox/` while resolving content changes. Keep the
   pinned content-compatible engine and adapt in content code.
3. Prefer current SS14 placement and APIs for upstream-owned code, while keeping
   fork behavior in fork paths, events, partials, or narrowly marked regions.
4. Give each independently reviewable core decision one entry and one commit.
5. Record exact upstream PR/commit links, affected files, validation, and remaining
   debt. A successful compile alone does not prove behavioral parity.

## Audit and validation cadence

Cadence revised: 2026-07-20, after CS-0048.

- Inventory the pinned first-parent range in ordered waves of 200 upstream commits. Classify every commit as already present, irrelevant to this fork/scope, deferred or rejected with a reason, or a port candidate.
- Keep every accepted core-system decision in its own atomic CMU commit even when candidates were discovered in the same inventory wave.
- Execute the accumulated focused tests and full solution build after each 1,000 upstream commits have been classified. Validate earlier only when a change's risk or static evidence makes deferral unsafe.
- Earlier CS-0041–CS-0048 references to a CS-0060 checkpoint are superseded by this 1,000-commit cadence; their queued regressions remain part of the next checkpoint.

## Entry template

```markdown
## CS-#### — Change title

- Upstream: <PR URL>, `<merge SHA>`, <date>
- Areas: Movement | Shooting | Medical | Chemistry | Interactions | Physics | GameTicking | Gamerules
- Status: Inventoried | Ported | Adapted | AlreadyPresent | Deferred | Rejected | Superseded
- Risk: Low | Medium | High
- Behavior/API delta:
- RMC/CMU divergence:
- Decision and rationale:
- Files changed:
- Validation:
- Follow-up/debt:
```

## Audit entries

## CS-0000 — Establish the SS14 comparison baseline

- Upstream: [space-wizards/space-station-14](https://github.com/space-wizards/space-station-14), `40ca2c7f90d11d27be5457d177c133f0947d1c08`, 2026-07-19
- Areas: Movement, Shooting, Medical, Chemistry, Interactions, Physics, GameTicking, Gamerules
- Status: Inventoried
- Risk: High
- Behavior/API delta: SS14 has 4,144 reachable commits after the last shared baseline. A no-worktree trial merge reports 973 content conflicts, 153 modify/delete conflicts, 14 add/add conflicts, two rename/delete conflicts, and one `RobustToolbox` pointer conflict before applying a downstream-preserving content strategy.
- RMC/CMU divergence: RMC continued developing all eight areas after the shared baseline. The current branch also contains a mechanical v283 content migration, while CMU-specific content has not yet been replayed from the backup branch.
- Decision and rationale: Integrate against exact pinned SS14 checkpoints, preserve explicit RMC gameplay where mechanical resolution is unsafe, and turn every preserved core conflict into a later auditable decision instead of claiming automatic parity.
- Files changed: `docs/upstream-sync/core-system-audit.md`
- Validation: `dotnet build SpaceStation14.slnx --no-restore --nologo --verbosity:minimal` completed at the initial Rebase checkpoint with 0 warnings and 0 errors (86.30 seconds, .NET SDK 10.0.301).
- Follow-up/debt: Inventory the upstream range by core area, record each merge resolution, run focused tests for touched systems, then replay and reconcile the pre-reset CMU layer.

## CS-0001 — Keep wall mounts visible from every viewing direction

- Upstream: [space-wizards/space-station-14#44770](https://github.com/space-wizards/space-station-14/pull/44770), `f3cff7bb8cc2268a7637b5bef658f740e2bfccbb`, 2026-07-19
- Areas: Interactions
- Status: AlreadyPresent
- Risk: Low
- Behavior/API delta: SS14 reverted directional visibility for wall-mounted entities, removing its visibility overlay, component tree, replicated CVar, and `WallMountComponent` visibility state while retaining the interaction-obstruction arc.
- RMC/CMU divergence: RMC never contained the reverted client overlay/tree files or the directional-visibility CVar. Its `WallMountComponent` already matches the post-revert interaction-only shape.
- Decision and rationale: Keep the existing RMC implementation; applying the revert would be empty and would not change runtime behavior.
- Files changed: `docs/upstream-sync/core-system-audit.md`
- Validation: Confirmed all five reverted client wall-visibility files are absent, no wall-visibility CVar or type reference exists, and the current `WallMountComponent` blob matches the upstream post-revert blob.
- Follow-up/debt: None for this upstream change.

## CS-0002 — Show locked and unlocked wall-cabinet states

- Upstream: [space-wizards/space-station-14#44388](https://github.com/space-wizards/space-station-14/pull/44388), `f63a5d2a5929e0616f229cd60d7e070b76e25421`, 2026-07-19
- Areas: Shooting, Interactions
- Status: Adapted
- Risk: Low
- Behavior/API delta: Fire-axe and shotgun cabinets now show distinct locked and unlocked indicator sprites, and hide the indicator while the glass door is open.
- RMC/CMU divergence: Current SS14 moved these entities under wall-mount cabinet base prototypes that RMC does not yet have. RMC retains the older flattened `FireAxeCabinet` hierarchy, including `RMCFireAxeCabinet` descendants.
- Decision and rationale: Port the final visual behavior into the existing `FireAxeCabinet` parent so the shotgun and RMC descendants inherit it without prematurely importing the broader cabinet prototype reorganization.
- Files changed: `Resources/Prototypes/Entities/Structures/Wallmounts/fireaxe_cabinet.yml`, `Resources/Prototypes/Entities/Structures/Wallmounts/shotgun_cabinet.yml`, and the four corresponding `locked.png`/`unlocked.png` sprite states.
- Validation: `dotnet build Content.YAMLLinter/Content.YAMLLinter.csproj --no-restore --nologo --verbosity:minimal` completed with 0 warnings and 0 errors; `dotnet run --project Content.YAMLLinter/Content.YAMLLinter.csproj --no-build` reported no YAML/prototype errors.
- Follow-up/debt: Reconcile the broader SS14 wall-mount cabinet prototype hierarchy in the later Interactions/prototype batch.

## CS-0003 — Correct reagent-quantity equality

- Upstream: [space-wizards/space-station-14#39574](https://github.com/space-wizards/space-station-14/pull/39574), `de7486b8dba0481c1abc676f05e32beeaa67ea6a`, 2025-08-12
- Areas: Chemistry
- Status: Ported
- Risk: Medium
- Behavior/API delta: Two reagent quantities now compare equal only when both their reagent identities and quantities are equal. Previously, identical values compared unequal while matching reagents with different quantities compared equal.
- RMC/CMU divergence: RMC retained the inverted quantity comparison from the shared SS14 baseline; no fork-specific behavior depends on that implementation.
- Decision and rationale: Port the one-line upstream correction exactly and pin the value, object, and operator equality contracts with focused NUnit tests.
- Files changed: `Content.Shared/Chemistry/Reagent/ReagentQuantity.cs`, `Content.Tests/Shared/Chemistry/ReagentQuantityTests.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: `dotnet build Content.Tests/Content.Tests.csproj --no-restore --nologo --verbosity:minimal` completed with 0 warnings and 0 errors; the filtered `dotnet test` run passed all 3 cases; solution-level `--list-tests` discovery found all 3 cases; `dotnet build SpaceStation14.slnx --no-restore --no-incremental --nologo --verbosity:minimal` completed with 0 warnings and 0 errors.
- Follow-up/debt: Audit `ReagentId.GetHashCode()` separately; its list-reference hashing is a distinct upstream fix and is intentionally outside this commit.

## CS-0004 — Use the extracted cartridge when loading revolver chambers

- Upstream: [space-wizards/space-station-14#43259](https://github.com/space-wizards/space-station-14/pull/43259), `a80863f9b344cde2bf8de83a2f699d53439654b4`, 2026-07-01
- Areas: Shooting, Interactions
- Status: Ported
- Risk: Medium
- Behavior/API delta: Revolver speedloader insertion now derives each chamber's loaded/spent state from the extracted cartridge entity instead of from the speedloader entity. Spent cartridges loaded through a speedloader no longer appear as live chambers.
- RMC/CMU divergence: RMC retains the older `SetChamber(int, RevolverAmmoProviderComponent, EntityUid)` signature, but its cartridge-state logic is equivalent to upstream and requires the same corrected entity argument.
- Decision and rationale: Hand-port the one-argument correction into the older RMC method shape without changing reload ordering, containers, audio, prediction, or RMC gun behavior.
- Files changed: `Content.Shared/Weapons/Ranged/Systems/SharedGunSystem.Revolver.cs`, `Content.IntegrationTests/Tests/Weapons/Ranged/RevolverSpeedLoaderTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: The focused shared-content and `DebugOpt` integration-test builds completed with 0 warnings and 0 errors. The filtered spent-cartridge integration test passed, was discovered by name, failed against a temporary mutation restoring the old speedloader argument, and passed again after restoring the fix. `dotnet build SpaceStation14.slnx --configuration DebugOpt --no-restore --no-incremental --nologo --verbosity:minimal` completed with 0 warnings and 0 errors.
- Follow-up/debt: Verify RMC live-speedloader reload/fire/eject behavior in a later Shooting gameplay pass; the automated regression specifically covers spent-cartridge state propagation.

## CS-0005 — Hash reagent identities by their value contract

- Upstream: [space-wizards/space-station-14#39494](https://github.com/space-wizards/space-station-14/pull/39494), `915d8152542f45bd197965147fff65807393e7f7a`, 2025-08-11
- Areas: Chemistry
- Status: Adapted
- Risk: Medium
- Behavior/API delta: Equal `ReagentId` values now produce equal hash codes, so dictionary and hash-set lookups work across independently created reagent-data lists. Hashing remains insensitive to data order and duplicate distribution exactly where the existing equality implementation is insensitive to them.
- RMC/CMU divergence: RMC retained collection-identity hashing from the shared baseline. Upstream changed to a value hash, but its ordered polynomial still conflicts with the order-insensitive `ReagentId.Equals` contract retained by both codebases.
- Decision and rationale: Hash the prototype, null/list-count distinction, and the commutative XOR of distinct reagent-data value hashes. This adopts upstream's value-hashing intent while ensuring every pair accepted by current equality receives the same hash.
- Files changed: `Content.Shared/Chemistry/Reagent/ReagentId.cs`, `Content.Tests/Shared/Chemistry/ReagentIdTests.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: The focused `DebugOpt` test-project build completed with 0 warnings and 0 errors; all 4 filtered tests passed and were discovered by name. A temporary restoration of CMU's collection-identity hash failed all 4 cases; a temporary installation of upstream's ordered hash failed the reordered and duplicate-equivalent cases while passing the other 2; the adapted implementation passed all 4 after restoration. `dotnet build SpaceStation14.slnx --configuration DebugOpt --no-restore --no-incremental --nologo --verbosity:minimal` completed with 0 warnings and 0 errors.
- Follow-up/debt: Audit the unusual duplicate-distribution semantics in `ReagentId.Equals`, its list-reference-based `Equals(string, List<ReagentData>?)` overload, and mutation of reagent data after use as a hashed key as separate Chemistry contracts.

## CS-0006 — Reuse the weightless collider-query set

- Upstream: [space-wizards/space-station-14#38290](https://github.com/space-wizards/space-station-14/pull/38290), `444180c20dd4f758e2a9311a7e0ba1a65402a9fe`, 2025-07-26
- Areas: Movement, Physics, GameTicking
- Status: Ported
- Risk: Low
- Behavior/API delta: Weightless near-wall movement checks now clear and refill a per-controller `HashSet<EntityUid>` instead of allocating a new set for every collider query on every movement tick. The queried entities and default lookup flags are unchanged.
- RMC/CMU divergence: RMC's active-input-mover marker reduces work for detached player movers but does not remove allocations from active weightless collider checks. The current controller otherwise retains the upstream baseline query shape.
- Decision and rationale: Port only the allocation-cache hunk. Keep the cache private, non-static, and per controller; exclude #38290's server Prometheus gauge and unrelated optimizations so this remains an independently reviewable behavior-preserving change.
- Files changed: `Content.Shared/Movement/Systems/SharedMoverController.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: `dotnet build Content.Shared/Content.Shared.csproj --configuration DebugOpt --no-restore --nologo --verbosity:minimal` completed with 0 warnings and 0 errors; the existing Movement integration filter passed all 3 discovered tests; `dotnet build SpaceStation14.slnx --configuration DebugOpt --no-restore --no-incremental --nologo --verbosity:minimal` completed with 0 warnings and 0 errors.
- Follow-up/debt: Profile or benchmark zero-gravity movement to quantify allocation reduction, consider the separate active-mover gauge, and replace this shared cache with per-worker/local storage if mover processing ever becomes parallel or reentrant.

## CS-0007 — Reset a successful fallback preset after its round

- Upstream: [space-wizards/space-station-14#41367](https://github.com/space-wizards/space-station-14/pull/41367), `589b9eddc7c8630aa8905d3cd65e419a037d76e0`, 2025-11-09
- Areas: GameTicking, Gamerules
- Status: Adapted
- Risk: Medium
- Behavior/API delta: When a selected preset cannot start and a configured fallback succeeds, the fallback is temporary: the configured default preset is restored during the fallback round's restart. The string `SetGamePreset` overload now forwards an optional reset countdown to the prototype overload.
- RMC/CMU divergence: RMC's shipped preset disables fallback and configures `CMDistressSignal` as both its default and fallback, so the defect is dormant in that preset but remains reachable through other configurations and runtime CVar changes.
- Decision and rationale: Port upstream's string-overload propagation but use `resetDelay: 0` instead of upstream's `1`. The target SS14 code still post-decrements a countdown of `1` without resetting, unintentionally retaining the fallback for a second round; zero performs the PR's stated one-round reset behavior.
- Files changed: `Content.Server/GameTicking/GameTicker.GamePreset.cs`, `Content.IntegrationTests/Tests/GameRules/GamePresetFallbackResetTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: `dotnet build Content.IntegrationTests/Content.IntegrationTests.csproj --configuration DebugOpt --no-restore --nologo --verbosity:minimal` completed with 0 warnings and 0 errors; the focused integration test passed 1/1 and appeared in test discovery. Mutation testing proved both branches: upstream's `resetDelay: 1` failed with an actual countdown of `1` instead of `0`, and dropping string-overload forwarding failed with `null` instead of `0`; after restoring the adapted implementation, the test passed again. `dotnet build SpaceStation14.slnx --configuration DebugOpt --no-restore --no-incremental --nologo --verbosity:minimal` completed with 0 warnings and 0 errors.
- Follow-up/debt: Consider porting SS14 #41522's fallback observability logs separately; do not mix those logging-only changes into this behavioral fix.

## CS-0008 — Count station tiles without enumerating them

- Upstream: [space-wizards/space-station-14#43929](https://github.com/space-wizards/space-station-14/pull/43929), `efda3a71d24ecb674022e195c50bc16ff96c2680`, 2026-06-30
- Areas: GameTicking, Gamerules
- Status: Ported
- Risk: Low
- Behavior/API delta: Grid selection for random station tiles keeps the same filled-tile weighting while obtaining each grid's count from maintained chunk metadata instead of enumerating every filled tile.
- RMC/CMU divergence: None in this call site. CMU's RobustToolbox revision already provides `SharedMapSystem.GetFilledTileCount(Entity<MapGridComponent>)` and derives it by summing each chunk's maintained `FilledTiles` count.
- Decision and rationale: Apply the exact upstream substitution. It removes work proportional to every cell in every grid chunk from a gamerule utility path without changing the intended weights or random-selection behavior.
- Files changed: `Content.Server/GameTicking/Rules/GameRuleSystem.Utility.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: `dotnet build Content.Server/Content.Server.csproj --configuration DebugOpt --no-restore --nologo --verbosity:minimal` and the integration-test project build both completed with 0 warnings and 0 errors. The existing `Content.IntegrationTests.Tests.GameRules` filter completed with 4 passes, 4 skips, and 0 failures. After a transient post-test file handle exited, `dotnet build SpaceStation14.slnx --configuration DebugOpt --no-restore --no-incremental --nologo --verbosity:minimal` was rerun and completed with 0 warnings and 0 errors.
- Follow-up/debt: The target SS14 revision later rewrites this method in #44382 (`c0f35ade3e`) and fixes that rewrite in #44668 (`667f7fa8bb`). Port those commits only as a paired, separately tested behavior change; never port the rewrite without its coordinate fix.

## CS-0009 — Refresh gun modifiers after inserting an upgrade

- Upstream: [space-wizards/space-station-14#43856](https://github.com/space-wizards/space-station-14/pull/43856), `f53c7d6a9d9ade810be07612c2d648cc3f5a795e`, 2026-05-05
- Areas: Shooting, Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: A successfully inserted gun upgrade is now inside the upgrade container before modifier refresh events are relayed, so its effects take effect during the same interaction instead of waiting for a later refresh.
- RMC/CMU divergence: RMC extends the gun modifier event and subscribes additional weapon systems, but retains SS14's upgrade-container relay. Moving insertion earlier preserves those extensions and lets them observe the same complete container state.
- Decision and rationale: Apply upstream's one-line ordering fix exactly. `GunUpgradeSystem.GetCurrentUpgrades` only enumerates contained entities, so refreshing before insertion cannot include the new upgrade.
- Files changed: `Content.Shared/Weapons/Ranged/Upgrades/GunUpgradeSystem.cs`, `Content.IntegrationTests/Tests/Weapons/Ranged/GunUpgradeTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: The integration-test project built with 0 warnings and 0 errors; the focused test passed 1/1 and appeared in discovery. Restoring CMU's old ordering made the test fail with the upgrade inserted but `FireRateModified` still `0.5` instead of `0.75`; restoring the port made it pass again. `dotnet build SpaceStation14.slnx --configuration DebugOpt --no-restore --no-incremental --nologo --verbosity:minimal` completed with 0 warnings and 0 errors.
- Follow-up/debt: SS14 #44685 later changes popup presentation in the same method but is unrelated to modifier ordering; review and port it independently.

## CS-0010 — Log preset fallback decisions and player-count failures

- Upstream: [space-wizards/space-station-14#41522](https://github.com/space-wizards/space-station-14/pull/41522), `8d4888b726aa51e4fb64b9d1405d3b13c6e1ac0c`, 2025-11-21
- Areas: GameTicking, Gamerules
- Status: Adapted
- Risk: Low
- Behavior/API delta: Round startup now emits informational diagnostics for the selected preset, fallback entry, cleanup, each fallback attempt and failure, disabled fallback, and gamerules rejected for insufficient ready players.
- RMC/CMU divergence: CMU retains the older generic `GameRuleSystem<T>` minimum-player validation path, while later SS14 moves it during a broad antagonist-selection rewrite. The log belongs in CMU's current handler. CS-0007's corrected fallback `resetDelay: 0` is preserved instead of reintroducing upstream's off-by-one value.
- Decision and rationale: Port the upstream messages at their current CMU decision points. These logs make otherwise opaque lobby restarts and fallback selection observable without changing the control flow.
- Files changed: `Content.Server/GameTicking/GameTicker.GamePreset.cs`, `Content.Server/GameTicking/Rules/GameRuleSystem.cs`, `Content.IntegrationTests/Tests/GameRules/GamePresetFallbackResetTest.cs`, `Content.IntegrationTests/Tests/GameRules/GameRuleLoggingTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: The integration-test project built with 0 warnings and 0 errors; the fallback and minimum-player logging tests passed 2/2 and both appeared in discovery. Removing the fallback-attempt message made the fallback test fail, while removing the rule diagnostic left the minimum-player test's capture empty and made it fail; restoring both messages returned the tests to green. `dotnet build SpaceStation14.slnx --configuration DebugOpt --no-restore --no-incremental --nologo --verbosity:minimal` completed with 0 warnings and 0 errors.
- Follow-up/debt: SS14 #43191 moves minimum-player validation as part of a much larger antagonist rewrite and should not be pulled in for logging alone. Monitor repeated fallback log volume and remember that configured preset IDs are written to server logs.

## CS-0011 — Complex-shape storage insertion rotation audit

- Upstream: [space-wizards/space-station-14#38896](https://github.com/space-wizards/space-station-14/pull/38896), `3638b2f44e52dbe4e8c20812a9ea98a98b9a9c04`, 2025-08-07
- Areas: Interactions
- Status: Superseded by RMC
- Risk: Low for current behavior; Medium if storage rotation is restored
- Behavior/API delta: Upstream starts unconstrained storage placement at zero rotation rather than deriving it from an item's stored sprite rotation, preventing complex item shapes from missing otherwise valid slots.
- RMC/CMU divergence: RMC disabled the placement rotation loop and constructs every candidate `ItemStorageLocation` with `Angle.Zero`. The calculated `startAngle` is therefore dead, and `ItemComponent.StoredRotation` has no production assignments in CMU beyond its zero initializer.
- Decision and rationale: Do not port a no-op one-line change. CMU already enforces the bug-free zero-angle behavior more strongly, although it also ignores `DefaultStorageOrientation` during actual candidate construction.
- Files changed: `docs/upstream-sync/core-system-audit.md` only; no production code changed.
- Validation: Static call-path review confirmed that `TryGetAvailableGridSpace` always passes `Angle.Zero` to `ItemStorageLocation`; repository-wide assignment search found no production writes to `StoredRotation`. The exact upstream substitution would only change an unused local in the current RMC fork.
- Follow-up/debt: If item rotation or `DefaultStorageOrientation` support is restored, port the upstream zero-angle starting rule as part of that work and add complex-shape insertion tests covering both horizontal and vertical storage grids.

## CS-0012 — Type chemistry reaction reagent identifiers

- Upstream: [space-wizards/space-station-14#44653](https://github.com/space-wizards/space-station-14/pull/44653), `928ecf541bcf73f34b65e9148a43012eb913ba20`, 2026-07-13
- Areas: Chemistry
- Status: Adapted
- Risk: Medium
- Behavior/API delta: `ReactionPrototype.Reactants` and `Products`, reaction caches, and reaction product results now use `ProtoId<ReagentPrototype>` keys instead of untyped strings. The reactant value contract is the typed `ReactantInfo` data record. Existing reaction YAML remains compatible; every current reactant entry explicitly supplies its amount.
- RMC/CMU divergence: RMC requires guidebook reagent lookups to pass through `IndexReagent` so fork-specific reagent handling is preserved. The server's public JSON DTO must continue converting typed IDs to string keys because its plain `System.Text.Json` serializer has no `ProtoId` property-name converter.
- Decision and rationale: Adopt typed IDs throughout reaction parsing, caching, execution, and guidebook presentation, while adapting the two fork boundaries instead of copying upstream mechanically. This gains compile-time reagent-ID safety without breaking RMC reagent resolution or reaction JSON generation.
- Files changed: `Content.Shared/Chemistry/Reaction/ReactionPrototype.cs`, `Content.Shared/Chemistry/Reaction/ChemicalReactionSystem.cs`, `Content.Client/Guidebook/Controls/GuideReagentReaction.xaml.cs`, `Content.Server/GuideGenerator/ReagentEntry.cs`, `Content.IntegrationTests/Tests/Chemistry/ReactionEntryJsonTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Shared, client, server, and integration-test project builds completed with 0 warnings and 0 errors. The existing all-reactions integration test and the new typed-API/JSON export test each passed and the new test appeared in discovery. Replacing RMC's `IndexReagent` call with a direct prototype lookup failed the client build with `RMCA0000`; changing the JSON DTO to typed dictionary keys compiled but made the regression fail with `System.Text.Json`'s unsupported dictionary-key exception. Restoring both adaptations returned the tests to green. A non-incremental full solution build completed with 0 warnings and 0 errors after the production change.
- Follow-up/debt: Downstream code added from CMU's backup branch must migrate callers from string-key assumptions to `ProtoId<ReagentPrototype>`. Audit broader upstream chemistry effect and metabolism changes separately; they are not implied by this API migration.

## CS-0013 — Tolerate unsupported topical damage types

- Upstream: [space-wizards/space-station-14#43087](https://github.com/space-wizards/space-station-14/pull/43087), `5add0838b16250dd5ae8ec1d02e2b99428536531`, 2026-03-01
- Areas: Medical, Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: Checking whether a topical can heal a target now treats damage types absent from the target's damage container as zero damage. Using a topical whose healing specification contains an unsupported type no longer throws `KeyNotFoundException`; supported types retain the same positive-damage check.
- RMC/CMU divergence: None at this call site. RMC retains the shared-baseline healing flow and damage-container representation, so the target-final guard applies directly without changing do-after, blood, stack, sound, popup, or admin-log behavior.
- Decision and rationale: Port the exact `TryGetValue` guard. A damage container intentionally defines which damage keys exist, and a missing key cannot represent healable damage.
- Files changed: `Content.Shared/Medical/Healing/HealingSystem.cs`, `Content.IntegrationTests/Tests/Medical/TopicalHealingTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: The shared and integration-test projects built with 0 warnings and 0 errors. The focused integration test passed with a target container containing only a supported control type and a topical containing only an absent type. Temporarily restoring the direct dictionary index made the same test fail at `HealingSystem.HasDamage` with `KeyNotFoundException` for `TopicalHealingTestUnsupported`; restoring the guard returned it to green. A non-incremental full solution build completed with 0 warnings and 0 errors.
- Follow-up/debt: Audit other Medical call sites that assume every `DamageSpecifier` key exists in a target container, especially while reconciling later SS14 body and damage-model migrations.

## CS-0014 — Separate energy-gun examine and popup presentation

- Upstream: [space-wizards/space-station-14#42103](https://github.com/space-wizards/space-station-14/pull/42103), `2aa29de1eeb8d38b649f809d902b728fd49221e5`, 2025-12-26; target follow-up [#44685](https://github.com/space-wizards/space-station-14/pull/44685), `a88d2c51648acca6e143973fa90a8481437ed501`, 2026-07-16
- Areas: Shooting, Interactions
- Status: Adapted
- Risk: Low
- Behavior/API delta: Energy guns now use separate localized messages for detailed examination and fire-mode confirmation. Examination renders `Set to <mode>.` with the mode highlighted in yellow; the local popup renders the plain `Changed to <mode>` confirmation without markup.
- RMC/CMU divergence: RMC retains the older predicted energy-weapon path and its client-only `PopupClient` confirmation. The pinned target later replaces this call with `PopupEntity` as one hunk in a 178-file popup-semantics cleanup, after other energy-gun and power-cell changes that are not present here.
- Decision and rationale: Port #42103's final message split exactly while preserving the current fork's popup delivery API. Applying only #44685's one-line broadcast change makes a server-side mode update create a client world popup under the present architecture; that semantic change is deferred until the broader popup and prediction migration can be reconciled as a unit.
- Files changed: `Content.Shared/Weapons/Ranged/Systems/BatteryWeaponFireModesSystem.cs`, `Resources/Locale/en-US/weapons/ranged/gun.ftl`, `Content.IntegrationTests/Tests/Weapons/Ranged/BatteryWeaponFireModesLocalizationTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: The shared and integration-test projects built with 0 warnings and 0 errors, and the existing localization-backed integration test confirmed the Fluent resources load. The connected regression passed while asserting the exact examine text, yellow markup, plain popup text, client-only delivery, and absence of cursor popups. Mutating either call site to the other message key failed the corresponding presentation assertions; replacing `PopupClient` with target #44685's `PopupEntity` failed because the server-side update broadcast a world popup. Restoring the adaptation returned the test to green. A non-incremental full solution build completed with 0 warnings and 0 errors.
- Follow-up/debt: Revisit #44685 together with the broader popup API cleanup and the upstream predicted power-cell/energy-weapon migration; test real use-in-hand and verb-driven mode changes for duplicate or missing confirmations before changing delivery semantics.

## CS-0015 — Validate construction events before queueing

- Upstream: [space-wizards/space-station-14#39869](https://github.com/space-wizards/space-station-14/pull/39869), `24f4b40881fc4094c76dcbba7088af930a3d37ca`, 2025-09-03; paired fix [#41396](https://github.com/space-wizards/space-station-14/pull/41396), `044aa4c8dc4d28af3493f984964801c1c456a63b`, 2026-01-26
- Areas: Interactions, GameTicking
- Status: Ported
- Risk: Medium
- Behavior/API delta: Every construction event must now validate against the active graph edge before entering the next-tick interaction queue. Invalid unrelated events can no longer replace a valid queued interaction and clobber its edge. Temperature and part-assembly steps return `Validated` during the pure validation pass, then perform their transition only when the queued event is processed.
- RMC/CMU divergence: RMC's construction flow retains its `ToolSystem.UseTool` `duplicateCondition` callback, `predicted: false` behavior, lingering stack exception, current temperature namespaces, and `_RMC14` construction systems. None of those fork paths needed to change for the upstream validation contract.
- Decision and rationale: Port both upstream commits atomically. The queue guard alone cannot admit valid temperature or part-assembly events because their old handlers never returned `Validated`; changing only those handlers is inert while non-`HandledEntityEventArgs` events bypass validation. Together they make validation complete and side-effect-free without changing next-tick execution ordering.
- Files changed: `Content.Server/Construction/ConstructionSystem.Interactions.cs`, `Content.IntegrationTests/Tests/Construction/Interaction/EdgeClobbering.cs`, `Content.IntegrationTests/Tests/Construction/Interaction/ConstructionEventValidation.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: The integration-test project built with 0 warnings and 0 errors, and all 3 focused tests passed. Removing the global queue guard made the unrelated temperature event overwrite the valid screwing edge and produced node `B` instead of `C`; restoring either specialized handler to unconditional `True` made its focused test trigger the construction system's validation-side-effect assertion. Restoring the complete pair returned all 3 tests to green. `dotnet build SpaceStation14.slnx --configuration DebugOpt --no-restore --nologo --verbosity:minimal --no-incremental --disable-build-servers` completed with 0 warnings and 0 errors.
- Follow-up/debt: Audit other custom construction graph steps and RMC xeno construction handlers for the same pure-validation contract, especially any event type that mutates state or reports `True` while `validation` is set.

## CS-0016 — Keep singularities and Tesla energy balls frictionless

- Upstream: [space-wizards/space-station-14#44078](https://github.com/space-wizards/space-station-14/pull/44078), `85acec95352490add20d9383bbfd1529ebd17860`, 2026-05-27
- Areas: Movement, Physics
- Status: Ported
- Risk: Medium
- Behavior/API delta: Singularity and Tesla energy-ball prototypes now have a zero-valued `TileFrictionModifier`, preventing tile/air damping from slowing or stopping these self-propelled hazards. Both full-size and mini Tesla balls inherit the behavior from `BaseEnergyBall`.
- RMC/CMU divergence: The affected prototype regions match RMC, and the content-side friction controller already supports the component. RMC's first-prediction guard remains unchanged; with the current zero minimum-friction CVar and the component's default modifier of zero, its calculation produces the intended zero damping without an engine change.
- Decision and rationale: Apply the two exact upstream component additions. `CanMoveInAir` permits propulsion but does not neutralize damping, so it is not a substitute for the friction modifier. Keeping the Tesla component on the abstract parent preserves inheritance for both concrete energy-ball sizes.
- Files changed: `Resources/Prototypes/Entities/Structures/Power/Generation/Singularity/singularity.yml`, `Resources/Prototypes/Entities/Structures/Power/Generation/Tesla/energyball.yml`, `Content.IntegrationTests/Tests/Physics/FrictionlessHazardPrototypeTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: The YAML-linter project built with 0 warnings and 0 errors, and its full resource/prototype validation reported no errors. The focused integration test passed while resolving `Singularity`, `TeslaEnergyBall`, and `TeslaMiniEnergyBall` and asserting a zero modifier. Removing the singularity component failed its case; removing the component from `BaseEnergyBall` failed both inherited Tesla cases; setting the singularity modifier to `1` failed the value assertion. Restoring the port returned the test to green. A non-incremental full solution build completed with 0 warnings and 0 errors.
- Follow-up/debt: Add a deterministic movement-simulation regression if the hazard controllers gain test hooks, and re-audit these prototypes if tile-friction defaults or RMC's prediction guard changes.

## CS-0017 — Preserve caller-owned solutions during partial adds

- Upstream: [space-wizards/space-station-14#40959](https://github.com/space-wizards/space-station-14/pull/40959), `ccd47a00a3a26e11b7f93e09b6e3c6638c9a0bac`, 2025-10-18
- Areas: Chemistry
- Status: Adapted
- Risk: Medium
- Behavior/API delta: `SharedSolutionContainerSystem.AddSolution` now has an additive, non-consuming input contract. When only part of a supplied solution fits, the accepted quantity is split from a clone; the destination receives that quantity while the caller's solution remains unchanged. The destination is updated once after either the partial or complete path.
- RMC/CMU divergence: RMC's refillable-solution system passes a freshly split solution already sized to destination availability and does not depend on the accidental consumption. `TryTransferSolution` intentionally consumes its source for transfer semantics and remains unchanged. The current `PrototypeManager` name and surrounding entity deconstruction are retained instead of importing unrelated upstream cleanup.
- Decision and rationale: Port only #40959's `AddSolution` ownership fix and contract documentation. Cloning inside `TryTransferSolution` would break its distinct consuming API, while cloning only the partial additive path fixes the defect without changing transfer callers or the full-add behavior.
- Files changed: `Content.Shared/Chemistry/EntitySystems/SharedSolutionContainerSystem.cs`, `Content.IntegrationTests/Tests/Chemistry/SolutionSystemTests.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: The shared and integration-test projects built with 0 warnings and 0 errors. The focused integration test passed with a 75-unit caller solution and a 50-unit destination, asserting 50 units accepted and the caller still at 75. Removing `Clone()` left the caller at 25 and failed both source assertions; bypassing the available-volume minimum returned 75 instead of 50 and failed the accepted-quantity assertion. Restoring both contracts returned the test to green. A non-incremental full solution build completed with 0 warnings and 0 errors.
- Follow-up/debt: Audit downstream callers restored from CMU's backup branch for assumptions that `AddSolution` consumes its input, and replace the clone/split implementation with a direct non-consuming reagent transfer if a suitable API is introduced.

## CS-0018 — Replace health HUD caches on active refresh

- Upstream: [space-wizards/space-station-14#39288](https://github.com/space-wizards/space-station-14/pull/39288), `b707110dea2fb4cbb049a5a2ec4654573e55cb93`, 2026-01-11
- Areas: Medical
- Status: Adapted
- Risk: Low
- Behavior/API delta: Consecutive active equipment-HUD refreshes now replace health-bar damage-container filters, the optional status icon, and health-icon damage-container filters instead of accumulating stale values. An empty active refresh clears all cached presentation state without requiring deactivation first.
- RMC/CMU divergence: RMC's `CMHealthIconsSystem.GetIcons` resolver and `DamageableComponent` path remain authoritative and are not replaced with later SS14 medical refactors. That resolver currently bypasses `ShowHealthIconsSystem.DamageContainers`, so clearing the icon-system cache is target parity and a future invariant; clearing the health-bar overlay fixes currently visible stale filtering.
- Decision and rationale: Port the three target-final cache resets at the start of each `UpdateInternal` call and replace the LINQ flattening with the upstream nested loop. Preserve the surrounding RMC resolver, prototype dependency, and inactive cleanup behavior.
- Files changed: `Content.Client/Overlays/ShowHealthBarsSystem.cs`, `Content.Client/Overlays/ShowHealthIconsSystem.cs`, `Content.IntegrationTests/Tests/Medical/HealthHudRefreshTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: The client and integration-test projects built with 0 warnings and 0 errors. The connected client regression passed while applying biological, inorganic, and empty active refreshes and cleaning both singleton HUD systems afterward. Removing the bar-filter clear retained both containers; removing the status reset retained `HealthIconFine` after the empty refresh; removing the icon-filter clear retained both icon containers. Restoring all three resets returned the test to green. A non-incremental full solution build completed with 0 warnings and 0 errors.
- Follow-up/debt: Decide whether RMC's health-icon resolver should intentionally honor `DamageContainers`; if that filter is restored, keep this replacement-on-refresh invariant and add resolver-level coverage.

## CS-0019 — Forward combined access to equipment verbs

- Upstream: [space-wizards/space-station-14#41631](https://github.com/space-wizards/space-station-14/pull/41631), `83ed95952ab8046639436b69e26d68ee3601174c`, 2025-11-30
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: `EquipmentVerb` handlers now receive access when either ordinary interaction access or special equipment access succeeds. Nearby visible worn items can provide strip-menu equipment verbs, ordinary loose items retain their verbs, and distant equipment remains inaccessible.
- RMC/CMU divergence: RMC's adjacent `RMCAdminVerb`, lag-compensated `CanAccessEquipment` implementation, and inventory-relay behavior remain unchanged. RMC dogtags, uniform accessories, and webbing benefit from the corrected combined access; attachable holders that intentionally do not check `CanAccess` retain their existing behavior.
- Decision and rationale: Port upstream's one-line access combination exactly. Passing only ordinary access hides verbs on worn equipment, while passing only equipment access regresses loose items; the logical OR preserves both valid paths without widening either predicate.
- Files changed: `Content.Shared/Verbs/SharedVerbSystem.cs`, `Content.IntegrationTests/Tests/Verbs/EquipmentVerbAccessTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: The shared/integration dependency graph built with 0 warnings and 0 errors, and the focused connected integration test passed 1/1. Restoring the old raw-access argument hid the verb on nearby worn equipment; using only equipment access hid it on a loose nearby item; granting access unconditionally exposed it on distant worn equipment. Restoring the combined predicate returned the test to green. `dotnet build SpaceStation14.slnx --configuration DebugOpt --no-restore --nologo --verbosity:minimal --no-incremental --disable-build-servers` completed with 0 warnings and 0 errors.
- Follow-up/debt: Audit other `EquipmentVerb` consumers for assumptions about the old access value, and review strip-menu slot visibility independently from verb eligibility.

## CS-0020 — Block shots fired from inside morgues

- Upstream: [space-wizards/space-station-14#44188](https://github.com/space-wizards/space-station-14/pull/44188), `470acb50b0ddf8e6f24edb23de8728baf429b7b9`, 2026-06-01
- Areas: Shooting, Physics, Interactions
- Status: Adapted
- Risk: Medium
- Behavior/API delta: Morgues and crematoriums now participate in projectile collision while using `RequireProjectileTarget`. Untargeted shots from outside pass over them, but a projectile fired by an occupant collides with the containing structure instead of escaping through it.
- RMC/CMU divergence: RMC removed `BulletImpassable` from the global `MachineLayer`, so each affected fixture explicitly combines both layers to retain upstream collision behavior without changing every machine. `CMMorgue` owns an independent fixture and targeting component, while `CMCrematorium` inherits both from the standard `Crematorium` parent.
- Decision and rationale: Port the upstream fixture and targeting contract to all standard and CM variants, adapting collision layers at the prototype boundary. Adding only `MachineLayer` would compile but would not collide with projectiles in this fork; changing the global collision group would be a broad RMC gameplay regression.
- Files changed: `Resources/Prototypes/Entities/Structures/Storage/morgue.yml`, `Resources/Prototypes/_RMC14/Entities/Structures/Storage/morgue.yml`, `Content.IntegrationTests/Tests/Weapons/Ranged/MorgueProjectileCollisionTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: The full server/client prototype validator reported no errors. The integration-test project built with 0 warnings and 0 errors, and all 4 focused prototype cases passed. Removing the explicit bullet layer failed `Morgue`; removing the base targeting component failed `Crematorium` and inherited `CMCrematorium`; removing the CM-owned component failed `CMMorgue`; forcing collision cancellation for contained shooters failed all 4 inside-shot assertions. Restoring the adaptation returned the suite to green. `dotnet build SpaceStation14.slnx --configuration DebugOpt --no-restore --nologo --verbosity:minimal --no-incremental --disable-build-servers` completed with 0 warnings and 0 errors.
- Follow-up/debt: Audit other RMC storage structures that use `MachineLayer` without explicit projectile blocking, especially containers that can hold an armed occupant, and keep this local adaptation if RMC's global collision-group divergence remains intentional.

## CS-0021 — Respect presented identity in hypospray popups

- Upstream: [space-wizards/space-station-14#39735](https://github.com/space-wizards/space-station-14/pull/39735), `47cf99fb7e34888a3d4798122620e08f721e8f21`, 2025-08-18
- Areas: Medical, Chemistry, Interactions
- Status: Adapted
- Risk: Low
- Behavior/API delta: A successful hypospray injection now names the target through the identity system and applies Fluent's `THE` grammar function. Disguised or otherwise presented identities are shown to the injector instead of leaking the target entity's true name.
- RMC/CMU divergence: RMC's vial-backed hypospray path duplicates the standard success popup after its custom skill, do-after, transfer, reaction, sound, and logging flow. Both implementations now use the same identity-aware argument while those RMC mechanics and client-only popup delivery remain unchanged.
- Decision and rationale: Port the original identity fix to the standard path and adapt it to RMC's duplicate path. The pinned target later folds hyposprays into a broader injector system, but importing that migration here would combine unrelated medical and prediction changes with this isolated privacy fix.
- Files changed: `Content.Shared/Chemistry/EntitySystems/HypospraySystem.cs`, `Content.Shared/_RMC14/Chemistry/RMCSharedHypospraySystem.cs`, `Resources/Locale/en-US/chemistry/components/hypospray-component.ftl`, `Content.IntegrationTests/Tests/Chemistry/HyposprayIdentityPopupTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Before the 20-port batch cadence began, the shared and integration-test projects built successfully and the connected regression passed 1/1 for both standard and RMC vial-backed hyposprays. Replacing either identity-aware argument with the raw target leaked the true name; removing `THE` dropped the required article. The full-solution checkpoint is deferred to the CS-0021–CS-0040 batch boundary.
- Follow-up/debt: Reconcile both paths with the target's unified injector-system migration in dependency order, and audit other duplicated RMC medical popups for raw entity-name arguments that can bypass presented identity.

## CS-0022 — Keep joint-visual targets local between network boundaries

- Upstream: [space-wizards/space-station-14#39987](https://github.com/space-wizards/space-station-14/pull/39987), `af05313f37e45103fcaa51f21e654f9a076a4819`, 2025-10-10
- Areas: Physics, Shooting
- Status: Ported
- Risk: Low
- Behavior/API delta: `JointVisualsComponent.Target` is now an `EntityUid?` in component, system, and overlay code. Auto-generated component state continues to translate that local identifier to `NetEntity?` on the wire and back to the receiving side's local entity, removing manual conversion from grappling-gun and rendering call sites.
- RMC/CMU divergence: No RMC-specific systems currently consume `JointVisualsComponent`. The existing grappling implementation, projectile lifecycle, rope sprite, joint physics, and prediction behavior remain unchanged; only ownership of the network-boundary conversion moves to the component-state generator.
- Decision and rationale: Port the three upstream call-site and component changes together. Storing a `NetEntity` directly in a data field leaks transport representation into local ECS code and lets the overlay accidentally query a server identifier on the client.
- Files changed: `Content.Shared/Physics/JointVisualsComponent.cs`, `Content.Client/Physics/JointVisualsOverlay.cs`, `Content.Shared/Weapons/Misc/SharedGrapplingGunSystem.cs`, `Content.IntegrationTests/Tests/Physics/JointVisualsTargetTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static review confirms the production hunks match #39987 and no RMC consumer exists. A connected regression was added to assert the public local field type, the grappling-shot assignment on the server, and automatic remapping to the corresponding client-local target. Per the requested 20-port cadence, execution is deferred to the CS-0021–CS-0040 batch checkpoint.
- Follow-up/debt: Re-audit the target's later grappling rework and rope-visual changes in dependency order; retain the local-entity component contract if those larger mechanics need CMU-specific adaptation.

## CS-0023 — Preserve job priorities when randomizing character profiles

- Upstream: [space-wizards/space-station-14#44100](https://github.com/space-wizards/space-station-14/pull/44100), `a19e63fd25f616cac36a6711bf9f2a69c8cb723f`, 2026-05-31
- Areas: GameTicking, Gamerules
- Status: Ported
- Risk: Low
- Behavior/API delta: When `ic.random_characters` replaces a selected character profile, the randomized profile now retains the player's job-priority dictionary. Late-join selection and `PlayerBeforeSpawnEvent` consumers therefore see the same eligible jobs instead of an empty preference set that can force an observer spawn.
- RMC/CMU divergence: RMC's `IsJobAllowedEvent`, inline character-spawn path, station job assignment, and marine-presence announcement remain unchanged. Normal CM rounds are unaffected because random characters default to disabled; custom late-join, respawn, admin, and gamerule paths that enable the CVar receive the corrected profile.
- Decision and rationale: Port #44100's profile replacement exactly and avoid later unrelated `GameTicker` modularization. Capturing priorities after assigning the random profile would copy an already-empty dictionary, while changing station overflow logic would address a separate allocation policy.
- Files changed: `Content.Server/GameTicking/GameTicker.Spawning.cs`, `Content.IntegrationTests/Tests/GameRules/RandomizedCharacterJobPrioritiesTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static review confirms the isolated hunk and its profile APIs are present without prerequisites. A regression was added that enables randomized characters, intercepts `PlayerBeforeSpawnEvent`, and compares both a CM rifleman and standard passenger priority against the selected profile. Per the requested 20-port cadence, execution is deferred to the CS-0021–CS-0040 batch checkpoint.
- Follow-up/debt: Exercise the full randomized late-join allocation path at the batch checkpoint and separately audit the fork's job-ban null guard and overflow-job behavior; neither is changed by this upstream fix.

## CS-0024 — Mark spent cartridge entities as trash

- Upstream: [space-wizards/space-station-14#40829](https://github.com/space-wizards/space-station-14/pull/40829), `2696fd7cd50cb1ed097875c4edff00a8f2f61f48`, 2025-10-12
- Areas: Shooting, Interactions
- Status: Adapted
- Risk: Medium
- Behavior/API delta: `CartridgeAmmoComponent` now exposes the networked `MarkSpentAsTrash` opt-out, defaulting to true. Every shared spent-state transition adds or removes the `Trash` tag accordingly, and the pre-spent pistol casing declares the tag because it never passes through that transition on spawn.
- RMC/CMU divergence: Seventy RMC prototype files declare cartridge ammunition, including conventional casings, shotgun handfuls, flares, and vehicle rounds. They all flow through `SetCartridgeSpent`, retain existing ammunition/caliber tags, and gain generic trash-collection compatibility when an entity remains after firing; caseless entities still follow their existing deletion path. No RMC system directly rewrites `Spent`.
- Decision and rationale: Port the upstream default and per-prototype escape hatch without bulk-editing RMC ammunition. `TagSystem.AddTag` and `RemoveTag` are additive state changes, so they do not replace fork tags; a reusable special cartridge can explicitly set `markSpentAsTrash: false` if discovered.
- Files changed: `Content.Shared/Weapons/Ranged/Components/AmmoComponent.cs`, `Content.Shared/Weapons/Ranged/Systems/SharedGunSystem.cs`, `Resources/Prototypes/Entities/Objects/Weapons/Guns/Ammunition/Cartridges/pistol.yml`, `Content.IntegrationTests/Tests/Weapons/Ranged/SpentCartridgeTrashTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static review confirms the target behavior survives later gun-system refactors and that `CartridgePistolSpent` is the only current prototype initialized with `spent: true`. A regression was added for the default transition, explicit opt-out, pre-spent prototype, and removal when a revolver materializes an unspent cartridge. Per the requested 20-port cadence, execution is deferred to the CS-0021–CS-0040 batch checkpoint.
- Follow-up/debt: During the batch playtest, inspect persistent RMC launcher and vehicle-ammunition entities for intentional reuse and apply the opt-out only where trash collection would conflict with their lifecycle.

## CS-0025 — Default action cooldown checks to current game time

- Upstream: [space-wizards/space-station-14#39329](https://github.com/space-wizards/space-station-14/pull/39329), `21eb662377ed0d267744287c870b0c9916444211`, 2025-08-03
- Areas: Interactions, GameTicking
- Status: Ported
- Risk: Low
- Behavior/API delta: `SharedActionsSystem.IsCooldownActive` now substitutes `GameTiming.CurTime` when its optional timestamp is omitted. The public default path therefore reports active cooldowns correctly instead of evaluating the lifted nullable comparison as false; explicitly supplied timestamps retain their existing boundary behavior.
- RMC/CMU divergence: Current RMC xeno psychic communication, queen-word, and mindshield callers pass their own current time and are behaviorally unchanged. RMC's `StartUseDelayEvent` override remains intact and continues to control cooldown start/end before this query is made.
- Decision and rationale: Port the single upstream null-coalescing assignment exactly. Changing the comparison or overwriting non-null timestamps would regress prediction and callers that intentionally query historical or future action state.
- Files changed: `Content.Shared/Actions/SharedActionsSystem.cs`, `Content.IntegrationTests/Tests/Actions/ActionCooldownDefaultTimeTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static review confirms `GameTiming` is already injected, every present CMU/RMC caller supplies a time, and no prerequisite is required. A regression was added for the omitted-time path, an explicit pre-end time, the exact end boundary, and a removed cooldown. Per the requested 20-port cadence, execution is deferred to the CS-0021–CS-0040 batch checkpoint.
- Follow-up/debt: Prefer the default overload for ordinary current-state queries as callers are updated, while retaining explicit timestamps where prediction or replay code needs a deliberate time reference.

## CS-0026 — Restrict conveyor friction to grounded active conveyance

- Upstream: [space-wizards/space-station-14#37468](https://github.com/space-wizards/space-station-14/pull/37468), `e64b6b03fa1e92e2e2312d0c40107bc0754bf83e`, 2025-11-08
- Areas: Movement, Physics, GameTicking
- Status: Adapted
- Risk: Low
- Behavior/API delta: A lingering `ConveyedComponent` only suppresses tile friction while its `Conveying` flag is active. The controller clears that flag and skips processing for bodies that are not grounded, and applies matching angular friction alongside linear friction so conveyed items stop residual spinning.
- RMC/CMU divergence: CMU retains its current `_gravity.IsWeightless(entity, physics, xform)` overload and controller structure instead of importing later dependency-injection cleanups. No RMC-specific conveyor controller or override exists, and the shared client/server path keeps prediction behavior symmetric with RMC's tile-friction controller.
- Decision and rationale: Port #37468's four behavioral changes onto the current controller while preserving fork wiring. The in-air/weightlessness early return inside the parallel job still reports a processable result, so it does not prevent the later result loop from applying friction; the explicit `BodyStatus.OnGround` guard is still required.
- Files changed: `Content.Shared/Physics/Controllers/SharedConveyorController.cs`, `Content.IntegrationTests/Tests/Physics/ConveyorFrictionTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static review confirms the current controller has the required corner-handling base and no RMC override. A regression was added for inactive conveyed friction, active friction suppression, grounded angular damping, airborne angular preservation, and clearing the active flag. Per the requested 20-port cadence, execution is deferred to the CS-0021–CS-0040 batch checkpoint.
- Follow-up/debt: Reconcile the later stopped-item stacking change in #42829 separately because it edits the same result loop, then recheck both angular damping branches after that merge.

## CS-0027 — Preserve theobromine when coffee is iced

- Upstream: [space-wizards/space-station-14#40063](https://github.com/space-wizards/space-station-14/pull/40063), `a8ba84ecf70eba6c740e641c41ca96392d056d41`, 2025-09-05
- Areas: Chemistry, Medical
- Status: Adapted
- Risk: Low
- Behavior/API delta: Iced coffee now produces `0.05` units of theobromine during each full metabolism-effect cycle, matching hot coffee and iced tea instead of losing the stimulant metabolite when ice is added.
- RMC/CMU divergence: No RMC reagent overrides or consumers replace `IcedCoffee`, and the current fork still uses metabolism groups with a default `0.5`-unit rate. The pinned target later represents the same output as a `Digestion` metabolite coefficient of `0.1`, whose rate product is also `0.05`.
- Decision and rationale: Port #40063's original `AdjustReagent` effect exactly for the current chemistry architecture and defer #42172's broad metabolism-stage migration. Importing only the target-final `metabolites` mapping would not deserialize or execute under the present model.
- Files changed: `Resources/Prototypes/Reagents/Consumable/Drink/drinks.yml`, `Content.IntegrationTests/Tests/Chemistry/IcedCoffeeTheobromineTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static review confirms the theobromine reagent, effect type, recipe, and metabolism rate are already present and that no RMC override exists. A regression was added to resolve the prototype effect, assert its amount, execute it against a solution, and verify the resulting theobromine quantity. Per the requested 20-port cadence, execution is deferred to the CS-0021–CS-0040 batch checkpoint.
- Follow-up/debt: When porting #42172, replace this effect with the target's typed digestion metabolite mapping and rerun the same output-contract regression across partial metabolism scales.

## CS-0028 — Raise dropped events after final placement

- Upstream: [space-wizards/space-station-14#44372](https://github.com/space-wizards/space-station-14/pull/44372), `de842aace42ba28092f779f5f2f44ee7ecc2be64`, 2026-06-28
- Areas: Interactions, Physics
- Status: Adapted
- Risk: Medium
- Behavior/API delta: A targeted hand drop now moves the item to its collision-constrained destination before running the dropped interaction. `DroppedEvent` is raised only after the dropper's target-relative rotation is applied, so subscribers observe the authoritative final coordinates and rotation instead of the old hand-container exit position and rotation.
- RMC/CMU divergence: RMC's special path for users inside containers still calls `DropNextTo` and raises `RMCDroppedEvent`; it does not enter the standard targeted-drop interaction. Existing magnetic, attachable, targeting, detector, CAS-flare, labeler, and gun-cleanup subscribers retain their behavior, while standard drop effects such as drop sounds now use the final location.
- Decision and rationale: Adapt the upstream ordering change into the fork's expanded `TryDrop` flow by forwarding the optional target through virtual `DoDrop`. This preserves the client override and ensures placement happens after successful hand-container removal but before `DroppedInteraction`; moving only the event below rotation would still expose the pre-target position.
- Files changed: `Content.Shared/Hands/EntitySystems/SharedHandsSystem.Drop.cs`, `Content.Client/Hands/Systems/HandsSystem.cs`, `Content.Shared/Interaction/SharedInteractionSystem.cs`, `Content.IntegrationTests/Tests/Hands/DropEventOrderingTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static comparison confirms the upstream order is preserved around RMC's container-specific branch. A regression was added that records coordinates and rotation from inside `DroppedEvent` and compares them with the final transform. Per the requested 20-port cadence, execution is deferred to the CS-0021–CS-0040 batch checkpoint.
- Follow-up/debt: Audit manual `DroppedEvent` raisers, including stripping paths, for whether they promise the same final-transform contract, and decide separately whether RMC's `RMCDroppedEvent` branch should converge on the standard event.

## CS-0029 — Use lithium in the Licoxide reaction

- Upstream: [space-wizards/space-station-14#40991](https://github.com/space-wizards/space-station-14/pull/40991), `49860b820cb5fe9953bcff21206c6a2388a4126c`, 2025-10-30
- Areas: Chemistry
- Status: Adapted (dormant source sync)
- Risk: Low
- Behavior/API delta: The dormant upstream Licoxide source now consumes one unit each of lithium and zinc instead of lead and zinc. CMU gameplay is unchanged while the standard `fun.yml` reaction file remains ignored.
- RMC/CMU divergence: `Resources/IgnoredPrototypes/cm_ignoredPrototypes.yml` explicitly abstracts every standard SS14 reaction file. RMC supplies a separate active reaction set under `_RMC14`, with no active Licoxide equivalent, so the standard typed reaction never enters the runtime prototype manager.
- Decision and rationale: Preserve the target-final source correction for later SS14 chemistry convergence, but do not unignore the full standard reaction suite as part of an isolated recipe port. Activating it would import a broad set of recipes that RMC deliberately removed and requires its own compatibility project.
- Files changed: `Resources/Prototypes/Recipes/Reactions/fun.yml`, `Content.IntegrationTests/Tests/Chemistry/LicoxideReactionTest.cs`, `Content.IntegrationTests/Tests/Chemistry/IgnoredReactionSourceTestHelper.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: The first CS-0040 focused run failed because `Licoxide` was absent from the runtime prototype manager, exposing the ignored-source policy. The corrected regression verifies that the file remains in the ignore manifest, the runtime recipe remains absent, and the actual dormant YAML has exactly lithium and zinc at one unit each, no lead, and one unit of Licoxide product.
- Follow-up/debt: Decide which standard recipes should be migrated into the active RMC chemistry set. If Licoxide is activated, convert this source-level regression into a runtime prototype test and re-audit reagent availability and balance.

## CS-0030 — Reuse traitor role briefing components

- Upstream: [space-wizards/space-station-14#39261](https://github.com/space-wizards/space-station-14/pull/39261), `3c76b5a8aa7d15413eaa50f13fef0bca7a51d1e9`, 2025-07-28
- Areas: Gamerules, GameTicking
- Status: Ported
- Risk: Low
- Behavior/API delta: `TraitorRuleSystem.MakeTraitor` now ensures and reuses the role entity's `RoleBriefingComponent` before updating its text. Reapplying traitor setup no longer attempts to add a duplicate component and aborts the assignment path.
- RMC/CMU divergence: RMC does not use the standard traitor rule in its normal CM round flow, but the inherited rule, reinforcement prototype, admin paths, and custom presets remain available. No RMC system overrides `MakeTraitor` or the standard mind-role entity.
- Decision and rationale: Port upstream's `EnsureComp` change exactly. Removing the existing component first would create avoidable component lifecycle events, while skipping the update when present would retain stale briefing text.
- Files changed: `Content.Server/GameTicking/Rules/TraitorRuleSystem.cs`, `Content.IntegrationTests/Tests/GameRules/TraitorRoleBriefingTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static review confirms the pinned target retains the idempotent component lookup and no prerequisite is required. A regression was added that assigns a reinforcement traitor twice and asserts both calls succeed while retaining the same briefing component instance. Per the requested 20-port cadence, execution is deferred to the CS-0021–CS-0040 batch checkpoint.
- Follow-up/debt: Audit whether repeated `MakeTraitor` calls should also deduplicate `TraitorMinds`, briefing notifications, objectives, and other side effects; this port intentionally fixes only the upstream component-add failure.

## CS-0031 — Reactivate health analyzers when patients return to range

- Upstream: [space-wizards/space-station-14#42608](https://github.com/space-wizards/space-station-14/pull/42608), `ceb175c92d68a324613b1ebe6e2167bd35e8c9a0`, 2026-01-28
- Areas: Medical, Interactions, GameTicking
- Status: Ported
- Risk: Low
- Behavior/API delta: A standard health analyzer now pauses continuous updates when its patient leaves range without clearing `ScannedEntity`. It sends one inactive state, retains the patient link, and resumes active updates automatically when the same patient returns to range.
- RMC/CMU divergence: CMU retains RMC's surrounding power-cell and UI behavior plus the source-generated dependency form. RMC's primary `CMHealthAnalyzer` uses its separate health-scanner system and is unaffected; standard handheld analyzers and dynamically attached MedTek analyzer components receive the corrected behavior.
- Decision and rationale: Port upstream's server-only active-state field and pause path exactly. Repeated inactive messages are suppressed, while deletion, explicit toggle-off, insertion, and drop still use the full stop path and clear the link.
- Files changed: `Content.Server/Medical/Components/HealthAnalyzerComponent.cs`, `Content.Server/Medical/HealthAnalyzerSystem.cs`, `Content.IntegrationTests/Tests/Medical/HealthAnalyzerRangeReactivationTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: The exact two-file upstream production patch applies to the fork without conflict. A regression was added that establishes an active scan, moves the patient outside range, verifies the link is retained while inactive, then returns the patient and verifies automatic reactivation. Per the requested 20-port cadence, execution is deferred to the CS-0021–CS-0040 batch checkpoint.
- Follow-up/debt: During the checkpoint, also exercise powered handheld and MedTek UI flows; separately audit whether full-stop paths should reset the server-only active flag before a later scan.

## CS-0032 — Disable the old jetpack when switching packs

- Upstream: [space-wizards/space-station-14#42689](https://github.com/space-wizards/space-station-14/pull/42689), `f5bab1961f70f5bbefdbe3f16a141dd240cb6eb5`, 2026-02-16
- Areas: Movement, Physics, Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: Enabling a jetpack for a user who is already linked to a different pack now disables the old pack through the normal teardown path before enabling the new one. The old pack loses its active marker and owner, stops consuming fuel and emitting effects, and the user retains exactly one current jetpack link.
- RMC/CMU divergence: No RMC-specific code or prototype uses `JetpackComponent`, `JetpackUserComponent`, or `ActiveJetpackComponent`; standard jetpacks share this system unchanged. CMU's nearby divergence is limited to source-generated dependency syntax and does not alter activation behavior.
- Decision and rationale: Port upstream's exclusivity block exactly inside the enabled branch. Calling the established `SetEnabled(..., false, ...)` path preserves appearance, physics, component-removal, and movement-modifier cleanup; directly overwriting fields would leave those side effects stale.
- Files changed: `Content.Shared/Movement/Systems/SharedJetpackSystem.cs`, `Content.IntegrationTests/Tests/Movement/JetpackSwitchingTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static review confirms all APIs and filled jetpack prototypes are already present and the pinned target retains this block through later dependency-injection cleanup. A regression was added that enables two packs in sequence and asserts the first is fully inactive and unowned while the second becomes the sole linked pack. Per the requested 20-port cadence, execution is deferred to the CS-0021–CS-0040 batch checkpoint.
- Follow-up/debt: At the checkpoint, verify server fuel consumption and client particles stop for the replaced pack; audit non-jetpack movement devices for the same single-owner invariant separately.

## CS-0033 — Throttle unassigned suit-sensor retries

- Upstream: [space-wizards/space-station-14#41872](https://github.com/space-wizards/space-station-14/pull/41872), `b0b88b216d146e9a401345f058bc7b5d11742d83`, 2025-12-16
- Areas: Medical, GameTicking
- Status: Adapted
- Risk: Low
- Behavior/API delta: A networked suit sensor that cannot find an owning station now schedules its next attempt before returning. Failed station-discovery retries are therefore limited to the configured update cadence instead of running every server tick once the sensor becomes due.
- RMC/CMU divergence: Upstream advances the previous deadline, while this fork already schedules from current time to avoid stale timers causing catch-up bursts; that existing `curTime + UpdateRate` policy is preserved. RMC marine uniforms declare `SuitSensor` without `DeviceNetwork`, so they do not enter this two-component update loop and remain unaffected.
- Decision and rationale: Adapt the upstream scheduling position while retaining the fork's timer formula. Moving the existing assignment before `CheckSensorAssignedStation` throttles both failed and successful passes without changing packet cadence or importing the target's later suit-sensor system split.
- Files changed: `Content.Server/Medical/SuitSensors/SuitSensorSystem.cs`, `Content.IntegrationTests/Tests/Medical/SuitSensorRetryThrottleTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static review confirms the standard uniform supplies both queried components and an initialized transmit frequency, while nullspace guarantees no station assignment. A regression was added that verifies the first failed attempt advances `NextUpdate` and an immediate second pass leaves the deadline unchanged. Per the requested 20-port cadence, execution is deferred to the CS-0021–CS-0040 batch checkpoint.
- Follow-up/debt: Profile station discovery after the checkpoint and revisit the timer formula only alongside a deliberate catch-up policy; separately decide whether RMC sensors should ever join the device network.

## CS-0034 — Remove uranium from the mute-toxin recipe

- Upstream: [space-wizards/space-station-14#42787](https://github.com/space-wizards/space-station-14/pull/42787), `381fda04403358d01c491c0aa63ad59b8aa7e978`, 2026-02-04
- Areas: Chemistry, Medical
- Status: Adapted (dormant source sync)
- Risk: Low
- Behavior/API delta: The dormant upstream MuteToxin source now requires two units each of Vestine and SpaceGlue and produces two units without uranium. CMU gameplay is unchanged while the standard `chemicals.yml` reaction file remains ignored.
- RMC/CMU divergence: `Resources/IgnoredPrototypes/cm_ignoredPrototypes.yml` explicitly abstracts every standard SS14 reaction file. RMC supplies a separate active reaction set under `_RMC14`, with no active MuteToxin equivalent, so this standard recipe is not deserialized at runtime.
- Decision and rationale: Preserve upstream's target-final source correction without enabling the entire standard chemistry suite. Product yield, temperature threshold, reaction impact, and intended reactant amounts remain untouched for a future selective migration.
- Files changed: `Resources/Prototypes/Recipes/Reactions/chemicals.yml`, `Content.IntegrationTests/Tests/Chemistry/MuteToxinReactionTest.cs`, `Content.IntegrationTests/Tests/Chemistry/IgnoredReactionSourceTestHelper.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: The first CS-0040 focused run failed because `MuteToxin` was absent from the runtime prototype manager, exposing the ignored-source policy. The corrected regression verifies that the file remains in the ignore manifest, the runtime recipe remains absent, and the actual dormant YAML has exactly Vestine and SpaceGlue at two units each, no uranium, and two units of MuteToxin product.
- Follow-up/debt: Decide whether MuteToxin belongs in the active RMC chemistry set. If it is activated, convert this source-level regression into a runtime prototype test and audit its medical balance against RMC reagents.

## CS-0035 — Let lethal energy-shotgun pellets hit holograms

- Upstream: [space-wizards/space-station-14#37920](https://github.com/space-wizards/space-station-14/pull/37920), `1b62863e52f129dcc88386b508afbb41c741966b`, 2025-10-08
- Areas: Shooting, Physics
- Status: Adapted
- Risk: Low
- Behavior/API delta: `BulletLaser` now includes `Opaque` in its projectile collision mask. The lethal wide and narrow energy-shotgun spreads emit this projectile, so their pellets can hit holoparasites, holocarps, and other holographic targets whose collision layer is opaque.
- RMC/CMU divergence: RMC removed the upstream projectile's separate fly-by fixture; that divergence remains untouched while the single mask bit is ported. Existing `Impassable` and `BulletImpassable` masks are retained, and no RMC-specific prototype directly consumes the standard laser-spread IDs.
- Decision and rationale: Adapt only upstream's additive `Opaque` mask entry. Replacing either existing mask would let the projectile pass through walls or bullet blockers, while restoring the removed fly-by fixture would combine an unrelated RMC projectile-physics divergence.
- Files changed: `Resources/Prototypes/Entities/Objects/Weapons/Guns/Projectiles/projectiles.yml`, `Content.IntegrationTests/Tests/Weapons/Ranged/EnergyShotgunHoloCollisionTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static review confirms both real hologram prototypes expose an opaque fixture and both energy-shotgun spread prototypes resolve to `BulletLaser`. A regression was added to require all three projectile mask bits and verify collision-mask intersection with holoparasite and holocarp fixtures. Per the requested 20-port cadence, execution is deferred to the CS-0021–CS-0040 batch checkpoint.
- Follow-up/debt: Audit other energy projectiles against opaque-only holograms and decide independently whether RMC's removed fly-by fixture should remain a fork divergence.

## CS-0036 — Keep recallable-evac station events eligible

- Upstream: [space-wizards/space-station-14#42199](https://github.com/space-wizards/space-station-14/pull/42199), `4b960f4bfb6d68659ddbfa7464d06a459592b163`, 2026-07-15
- Areas: Gamerules, GameTicking
- Status: Ported
- Risk: Low
- Behavior/API delta: A station event with `OccursDuringRoundEnd: false` is now excluded only after evacuation has been requested and the shuttle can no longer be recalled. During a recallable countdown, the event remains eligible instead of being blocked for the entire evacuation window.
- RMC/CMU divergence: RMC's delayed final-round handling remains unchanged, and its round-end system exposes the same `CanCallOrRecall` state used by this eligibility check. `SleeperAgents` is currently the only event that opts out, so the immediate content impact is narrow and standard CM event policy is otherwise preserved.
- Decision and rationale: Port the target's three-part condition and documentation together. Checking only `IsRoundEndRequested` conflates a reversible shuttle call with certain round end; ignoring the opt-out flag would allow restricted events even after recall is locked.
- Files changed: `Content.Server/StationEvents/Components/StationEventComponent.cs`, `Content.Server/StationEvents/EventManagerSystem.cs`, `Content.IntegrationTests/Tests/GameRules/StationEventRoundEndEligibilityTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static review confirms `RoundEndSystem.CanCallOrRecall` already models the required cooldown state and no RMC override bypasses event filtering. A regression was added that enters a recallable evacuation countdown and verifies the opt-out SleeperAgents event remains in the available set. Per the requested 20-port cadence, execution is deferred to the CS-0021–CS-0040 batch checkpoint.
- Follow-up/debt: Port and audit #41863's ZombieOutbreak evacuation restriction separately, then verify non-recallable eligibility once the countdown passes its final recall boundary.

## CS-0037 — Let standard energy projectiles hit holograms

- Upstream: [space-wizards/space-station-14#40782](https://github.com/space-wizards/space-station-14/pull/40782), `df6307fe66f71944c5b3d5ed1e683a2723953181`, 2025-10-08
- Areas: Shooting, Physics
- Status: Adapted
- Risk: Low
- Behavior/API delta: Taser, disabler, practice-disabler, and disabler-SMG projectiles now include `Opaque` in their collision masks, allowing standard energy weapons to hit opaque-layer holographic creatures. `WatcherBolt` already carried the required opaque mask in this fork and remains unchanged.
- RMC/CMU divergence: RMC's removed per-projectile fly-by fixtures remain absent, and no RMC-specific prototype directly consumes the affected standard projectile IDs. RMC species and structures that expose opaque collision can now be hit if a standard energy weapon is spawned, while normal RMC weapon loadouts are unchanged.
- Decision and rationale: Port the four applicable additive mask hunks, treat the already-compliant Watcher hunk as a no-op, and preserve every existing `Impassable` and `BulletImpassable` bit. The predecessor #37581 changed Watcher temperature-gun window and reflectivity behavior and is deferred as a separate audit item.
- Files changed: `Resources/Prototypes/Entities/Objects/Weapons/Guns/Projectiles/projectiles.yml`, `Content.IntegrationTests/Tests/Weapons/Ranged/EnergyProjectileHoloCollisionTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Prototype-ID-scoped static review confirms `Opaque` is present on the four intended projectiles, CS-0035's `BulletLaser`, and the pre-existing Watcher while wall and bullet masks remain on every applicable projectile. The regression now checks all six projectiles against both real hologram fixtures. Per the requested 20-port cadence, execution is deferred to the CS-0021–CS-0040 batch checkpoint.
- Follow-up/debt: Audit #37581 independently before changing Watcher wall/window behavior, and keep the generalized hologram-collision contract when additional energy projectiles are ported.

## CS-0038 — Cap diphenhydramine drowsiness duration

- Upstream: [space-wizards/space-station-14#41169](https://github.com/space-wizards/space-station-14/pull/41169), `c8b26adb38473aa83c11c5a337b25b3e573583eb`, 2025-10-28
- Areas: Chemistry, Medical
- Status: Adapted
- Risk: Low
- Behavior/API delta: Repeated diphenhydramine metabolism now updates drowsiness to the later of its current expiry and 1.5 seconds from the current tick instead of adding another 1.5 seconds every cycle. Continuous exposure therefore keeps the patient drowsy without building an unbounded duration after the reagent is gone.
- RMC/CMU divergence: This fork retains the older `ModifyStatusEffect` API where additive effects choose between maximum-duration refresh and accumulation with a separate `refresh` field. No RMC or CMU reagent override replaces standard diphenhydramine.
- Decision and rationale: Remove `refresh: false` so the field returns to its `true` default, while retaining `type: Add`. The target branch removed the additive type after a later API changed its default semantics; copying that deletion literally into this older API would leave the behavior unchanged rather than fix accumulation.
- Files changed: `Resources/Prototypes/Reagents/medicine.yml`, `Content.IntegrationTests/Tests/Chemistry/DiphenhydramineDrowsinessTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static API inspection confirms `Add` plus the default refresh path calls `TryUpdateStatusEffectDuration`, which preserves the greater expiry rather than summing duration. A regression was added that applies the parsed reagent effect twice in one tick and requires an unchanged expiry. Per the requested 20-port cadence, execution is deferred to the CS-0021–CS-0040 batch checkpoint.
- Follow-up/debt: Carry the semantic regression through the newer status-effect metabolism API when that refactor is ported, and audit other sedatives that explicitly opt into duration accumulation separately.

## CS-0039 — Clear in-hand visuals before removing hands

- Upstream: [space-wizards/space-station-14#44405](https://github.com/space-wizards/space-station-14/pull/44405), `8b228db4ccc0bed71bbc304821259fe993a751ca`, 2026-06-29
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: Removing a hand now shuts down its item container while the hand definition is still present. Container-removal handlers can therefore resolve the hand location, remove its sprite layers, and emit the normal unequip notifications instead of leaving stale in-hand visuals behind.
- RMC/CMU divergence: The shared hand and client visual handlers match the upstream event contract, while CMU retains RMC's separate drop behavior around the same path. Zombie transformations, borg modules, and dynamically equipped extra hands all call the corrected shared removal method; no RMC override bypasses it.
- Decision and rationale: Port the target-final ordering change exactly. Moving only the dictionary removal preserves all existing drop, container shutdown, active-hand selection, dirtying, and hand-count behavior while ensuring synchronous shutdown events still see their metadata.
- Files changed: `Content.Shared/Hands/EntitySystems/SharedHandsSystem.cs`, `Content.IntegrationTests/Tests/Hands/HandVisualRemovalTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static review confirms the client removal handler exits when the hand definition is absent and the pinned target retains the reordered shutdown. A client-side regression was added with a nullspace-held two-layer crowbar, forcing container shutdown to perform removal and requiring both recorded and sprite-mapped layers to disappear. Per the requested 20-port cadence, execution is deferred to the CS-0021–CS-0040 batch checkpoint.
- Follow-up/debt: Exercise network-reconciled hand removal for borg modules and extra-hand equipment when those systems are revised, and audit other dynamic container metadata for the same synchronous-event lifetime rule.

## CS-0040 — Block zombie outbreaks during locked evacuation

- Upstream: [space-wizards/space-station-14#41863](https://github.com/space-wizards/space-station-14/pull/41863), `83e1a6a8eb4b992f2ed71eb83f814786f7d9deaa`, 2025-12-15
- Areas: Gamerules, GameTicking
- Status: Ported
- Risk: Low
- Behavior/API delta: The standard Zombie Outbreak station event now opts out once evacuation has been requested and the shuttle can no longer be recalled. Initial infected can no longer be selected during a locked evacuation, while the event remains eligible during a recallable call under CS-0036's corrected policy.
- RMC/CMU divergence: RMC does not reference or override the standard `ZombieOutbreak` prototype, but the inherited basic antagonist event table and administrative rule paths remain available. RMC's delayed final-round handling is unchanged and continues to expose the same round-end request and recall-lock state.
- Decision and rationale: Port the isolated prototype flag exactly and validate it against both sides of the shared eligibility condition. Blocking throughout the entire shuttle countdown would regress CS-0036; leaving the default `true` would permit a conversion antagonist too late for a viable event.
- Files changed: `Resources/Prototypes/GameRules/events.yml`, `Content.IntegrationTests/Tests/GameRules/StationEventRoundEndEligibilityTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static review confirms the pinned target retains the opt-out flag and the current event manager gates it only when round end is requested and recall is locked. The regression verifies normal availability, creates an immediate locked-evac state, and requires Zombie Outbreak to leave the eligible set. Execution follows immediately in the requested CS-0021–CS-0040 checkpoint.
- Follow-up/debt: Audit later upstream evacuation opt-outs as separate policy decisions, especially antagonist ghost roles and visitor events, rather than bulk-copying their flags into CMU's round flow.

## Batch checkpoint — CS-0021–CS-0040

Date completed: 2026-07-20

- Scope: 20 audited upstream items. Eighteen affect active CMU/RMC content; CS-0029 and CS-0034 synchronize intentionally dormant standard-reaction sources without enabling RMC's ignored SS14 chemistry suite.
- Project build: `dotnet build Content.IntegrationTests/Content.IntegrationTests.csproj --no-restore --verbosity minimal` completed with 0 warnings and 0 errors.
- Focused validation: 19 integration-test cases covering all 20 entries passed with 0 failures. The initial run passed 17 and failed the two chemistry cases because the standard reaction prototypes are deliberately abstracted; the tests and audit were corrected to validate the ignored source contract before the passing rerun.
- Solution build: `dotnet build SpaceStation14.slnx --no-restore --verbosity minimal` completed with 0 warnings and 0 errors.
- Disposition: The batch is closed. Continue with CS-0041 and defer the next build/test execution until CS-0060 unless a change is high-risk or static review exposes a reason to validate earlier.

## CS-0041 — Default the Viper and pulse carbine to full-auto

- Upstream: [space-wizards/space-station-14#42830](https://github.com/space-wizards/space-station-14/pull/42830), `f2bea7b435214e62cc317778877ab5a0ab95d6dc`, 2026-07-13
- Areas: Shooting
- Status: Ported
- Risk: Low
- Behavior/API delta: Newly spawned Viper pistols and pulse carbines now select full-auto by default. Both weapons retain semi-auto as an available mode, and their fire rate, ammunition, projectile, and damage behavior are unchanged.
- RMC/CMU divergence: Neither weapon has an RMC prototype override or RMC gun-system initialization path. The Viper inherits both modes and a semi-auto default from `BaseWeaponPistol`, so its local `Gun` component overrides only the selected mode; the pulse carbine already defines both modes locally.
- Decision and rationale: Port only the two target-final default-mode fields. Changing `BaseWeaponPistol` would affect unrelated sidearms, while removing semi-auto availability would turn a spawn preference into a broader weapon-balance change.
- Files changed: `Resources/Prototypes/Entities/Objects/Weapons/Guns/Battery/battery_guns.yml`, `Resources/Prototypes/Entities/Objects/Weapons/Guns/Pistols/pistols.yml`, `Content.IntegrationTests/Tests/Weapons/Ranged/DefaultAutomaticFireModeTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static comparison confirms the pinned target retains both full-auto defaults and both selectable modes. A regression was added that spawns both weapons, verifies full-auto selection with semi-auto still available, and uses the Mk58 as a control for the unchanged base-pistol default. Per the requested 20-port cadence, execution is deferred to the CS-0041–CS-0060 checkpoint.
- Follow-up/debt: Audit later upstream selective-fire default changes individually; do not normalize RMC marine weapons to SS14 defaults without a separate balance decision.

## CS-0042 — Preserve complete strip-time durations

- Upstream: [space-wizards/space-station-14#43022](https://github.com/space-wizards/space-station-14/pull/43022), `7bc062ee14a6549c961c6d5f4555035ad3a17951`, 2026-02-26
- Areas: Interactions, GameTicking
- Status: Ported
- Risk: Low
- Behavior/API delta: Strip-time modifiers now operate on complete `TimeSpan` values and retain sub-second precision. Durations longer than a minute no longer use only their seconds component, additive fractions are no longer discarded, and a negative final duration still clamps to zero.
- RMC/CMU divergence: RMC's inventory skill system writes a strip multiplier into the same event, while standard thieving and clothing systems apply additive reductions and delays. The corrected calculation preserves those fork-specific multipliers and makes their fractional results accurate without changing skill tables or do-after cancellation rules.
- Decision and rationale: Port the target-final tick calculation exactly. Using `TotalSeconds` would also fix the component bug but would introduce an unnecessary floating-point round trip; multiplying and adding `TimeSpan` values before clamping their ticks preserves the upstream precision contract.
- Files changed: `Content.Shared/Strip/Components/StrippableComponent.cs`, `Content.Tests/Shared/Strip/StripTimeCalculationTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static review confirms there are no later target changes to this calculation and all downstream subscribers only mutate `Multiplier`, `Additive`, or `Stealth`. Regressions cover a 90.5-second input with fractional multiplier/additive values and the zero clamp. Per the requested 20-port cadence, execution is deferred to the CS-0041–CS-0060 checkpoint.
- Follow-up/debt: Audit other interaction timers for accidental use of `TimeSpan.Seconds` or `Milliseconds`, especially RMC skill-adjusted do-afters, before normalizing their arithmetic.

## CS-0043 — Remove sentience-event eligibility during zombification

- Upstream: [space-wizards/space-station-14#39950](https://github.com/space-wizards/space-station-14/pull/39950), `0bbe335a3aec216e55e901b9d043de8b0d0c4db1`, 2025-08-29
- Areas: Gamerules, Medical
- Status: Ported
- Risk: Low
- Behavior/API delta: Zombifying an eligible animal now removes its `SentienceTarget` marker. The Random Sentience station event can no longer select an already-zombified creature and grant it a second sentience or ghost-role path.
- RMC/CMU divergence: RMC retains the shared zombification flow with unrelated equipment exceptions and adds no sentience-target prototypes of its own. Standard eligible creatures and custom/admin zombification remain reachable, while zombie combat, mind ownership, infection, and RMC equipment behavior are unchanged.
- Decision and rationale: Remove the stale eligibility component at the state transition, matching the target-final fix. Adding a zombie exclusion only to Random Sentience would leave the invalid marker visible to other consumers and couple the event query to a transformation detail.
- Files changed: `Content.Server/Zombies/ZombieSystem.Transform.cs`, `Content.IntegrationTests/Tests/GameRules/ZombieSentienceTargetTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static review confirms Random Sentience still enumerates `SentienceTargetComponent`, the pinned target retains removal during zombification, and no RMC override re-adds it. A regression spawns an eligible monkey, zombifies it through the public system API, and requires the zombie marker to replace sentience eligibility. Per the requested 20-port cadence, execution is deferred to the CS-0041–CS-0060 checkpoint.
- Follow-up/debt: Audit other irreversible creature transformations for stale station-event eligibility markers, particularly ghost-role and polymorph transitions.

## CS-0044 — Preserve base diagonal-window identity

- Upstream: [space-wizards/space-station-14#39032](https://github.com/space-wizards/space-station-14/pull/39032), `f7c64ab86c35fbd23dc05ac26002678e45b00a21`, 2025-07-17
- Areas: Physics, Medical
- Status: Ported
- Risk: Low
- Behavior/API delta: `WindowDiagonal` now retains the `Window` tag alongside `Diagonal`. Electrified structures configured to stop working when a window occupies their tile can therefore detect the diagonal base window and avoid shocking through it.
- RMC/CMU divergence: The standard electrocution system performs its tile obstruction check through the `Window` tag, and RMC has no override for `WindowDiagonal` or that query. Existing inherited maps use the prototype, while its fixture, airtight directions, construction graph, and damage behavior remain unchanged.
- Decision and rationale: Restore the semantic tag on the prototype rather than special-casing diagonal fixtures in electrocution. The local `Tag` component replaces the parent's tag list, so explicitly carrying both identities is required even though `WindowDiagonal` inherits from `Window`.
- Files changed: `Resources/Prototypes/Entities/Structures/Windows/window.yml`, `Content.IntegrationTests/Tests/Physics/DiagonalWindowTagTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static comparison confirms the pinned target retains both tags and `ElectrocutionSystem` still checks the `Window` tag. A regression spawns the real diagonal prototype and verifies its resolved runtime tag identity. Per the requested 20-port cadence, execution is deferred to the CS-0041–CS-0060 checkpoint.
- Follow-up/debt: Port #39580's matching tags for specialized diagonal windows separately, together with its distinct diagonal-grille collision-layer correction.

## CS-0045 — Preserve specialized diagonal-window identity

- Upstream: [space-wizards/space-station-14#39580](https://github.com/space-wizards/space-station-14/pull/39580), `d58ef22d62795c1c4393c1eb09d33c1ff78087c6`, 2025-08-17
- Areas: Physics, Medical
- Status: Adapted
- Risk: Low
- Behavior/API delta: Clockwork, mining, plasma, plastitanium, reinforced, reinforced-plasma, reinforced-uranium, shuttle, and uranium diagonal windows now retain the `Window` tag alongside `Diagonal`. Tag-driven electrocution and other window-presence checks recognize every specialized diagonal family in the pinned target.
- RMC/CMU divergence: RMC does not override these standard structure prototypes or the tag query. Several inherited maps use the affected concrete prototypes, and the plastitanium tag is applied at its abstract diagonal base so both destructible and indestructible variants inherit the identity.
- Decision and rationale: Port only #39580's nine window-tag hunks here and audit its grille collision-layer change separately. The two changes share a PR but have different consumers and regression contracts; splitting them keeps each commit independently reversible without changing target-final behavior.
- Files changed: the nine specialized files under `Resources/Prototypes/Entities/Structures/Windows/`, `Content.IntegrationTests/Tests/Physics/DiagonalWindowTagTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static comparison confirms the pinned target retains every added tag. The CS-0044 runtime regression now covers one concrete prototype from each affected family, including inheritance through the plastitanium diagonal base. Per the requested 20-port cadence, execution is deferred to the CS-0041–CS-0060 checkpoint.
- Follow-up/debt: Port and validate #39580's diagonal-grille `GlassLayer` change as its own physics decision; audit future tag-list overrides for other inherited semantic identities.

## CS-0046 — Use glass collision for diagonal grilles

- Upstream: [space-wizards/space-station-14#39580](https://github.com/space-wizards/space-station-14/pull/39580), `d58ef22d62795c1c4393c1eb09d33c1ff78087c6`, 2025-08-17
- Areas: Physics, Shooting
- Status: Adapted
- Risk: Medium
- Behavior/API delta: Standard and clockwork diagonal grilles now expose `GlassLayer` rather than `WallLayer` on their polygon fixture. Raycasts and projectile masks can treat them like their transparent grille family instead of opaque walls while their shape, hard collision mask, construction, and electrification remain unchanged.
- RMC/CMU divergence: RMC does not override either prototype or their fixture IDs, and inherited maps place the standard diagonal grille. The fork's collision-group definitions match the upstream glass/wall distinction, so no engine or RMC projectile changes are required.
- Decision and rationale: Port only #39580's two grille-layer replacements after recording its window-tag half in CS-0045. Do not copy later target changes around these prototypes, such as transform rotation metadata, without their own ancestry and behavior audit.
- Files changed: `Resources/Prototypes/Entities/Structures/Walls/grille.yml`, `Content.IntegrationTests/Tests/Physics/DiagonalGrilleCollisionTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static comparison confirms the pinned target retains `GlassLayer` on both `fix1` fixtures. A regression spawns both concrete grilles and requires the exact glass-layer mask while excluding its opaque bit. Per the requested 20-port cadence, execution is deferred to the CS-0041–CS-0060 checkpoint.
- Follow-up/debt: Audit later diagonal-structure rotation metadata separately and expand collision coverage if RMC introduces custom diagonal grille variants.

## CS-0047 — Block late-spawn events during locked evacuation

- Upstream: [space-wizards/space-station-14#42196](https://github.com/space-wizards/space-station-14/pull/42196), `42a9292e9cb33203080a645752df515298380906`, 2026-07-15
- Areas: Gamerules, GameTicking
- Status: Adapted
- Risk: Medium
- Behavior/API delta: Nine antagonist, ghost-role, or pest event families plus the visitor-shuttle base now opt out during locked evacuation. Under CS-0036 they remain eligible while an evacuation call can still be recalled, then leave the event pool once recall is locked.
- RMC/CMU divergence: CMU retains one concrete `DerelictCyborgSpawn` instead of the target's later abstract base and derived borg variants, so the policy is applied directly to that concrete event. RMC's primary distress-signal preset does not install the standard scheduler, while inherited standard presets, admin/custom rules, and visitor-shuttle scheduling receive the corrected policy without changing their spawn definitions.
- Decision and rationale: Port all ten target-final policy flags together because they define one evacuation boundary. Preserve CMU's current event weights, antag-selection schema, announcements, and concrete derelict-borg structure; importing later prototype refactors would exceed this policy change.
- Files changed: `Resources/Prototypes/GameRules/events.yml`, `Resources/Prototypes/GameRules/pests.yml`, `Resources/Prototypes/GameRules/unknown_shuttles.yml`, `Content.IntegrationTests/Tests/GameRules/StationEventRoundEndEligibilityTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static comparison confirms no later pinned-target commit changes these flags. The shared evacuation regression now resolves twelve restricted concrete events, verifies every flag, keeps all events eligible during recallable evacuation, and excludes all during locked evacuation; `UnknownShuttleCargoLost` proves base inheritance. Per the requested 20-port cadence, execution is deferred to the CS-0041–CS-0060 checkpoint.
- Follow-up/debt: Audit remaining station events for explicit evacuation policy instead of bulk-disabling them, especially non-antagonist supply, weather, and emergency-response events.

## CS-0048 — Protect apprentice jobs from Bureaucratic Error

- Upstream: [space-wizards/space-station-14#40001](https://github.com/space-wizards/space-station-14/pull/40001), `3e63e4590d8d9df78eaf0dafc3cc601c12b73bd0`, 2025-09-03
- Areas: Gamerules
- Status: Ported
- Risk: Low
- Behavior/API delta: Bureaucratic Error now preserves Research Assistant, Medical Intern, Security Cadet, and Technical Assistant slots alongside Station AI. Its random branches can no longer remove or mutate every department-entry job and leave Passenger as the only practical late-join choice.
- RMC/CMU divergence: All four standard job prototypes exist, and no RMC prototype or system overrides this event or those IDs. The event remains available to inherited standard presets and admin/custom rules; the primary RMC distress-signal preset does not schedule standard station events, so its normal job flow is unchanged.
- Decision and rationale: Port the exact four target-final exclusions and retain the existing Station AI exclusion. RMC-specific roles are not added without a separate event-policy audit, and event probability, timing, random branches, and job-slot APIs remain untouched.
- Files changed: `Resources/Prototypes/GameRules/events.yml`, `Content.IntegrationTests/Tests/GameRules/BureaucraticErrorIgnoredJobsTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static review confirms every protected job prototype resolves and the pinned target retains exactly these five ignored IDs. A regression spawns the real rule and requires set equivalence, detecting both missing safeguards and unintended policy expansion. Per the requested 20-port cadence, execution is deferred to the CS-0041–CS-0060 checkpoint.
- Follow-up/debt: If standard station events are enabled in CMU's primary mode, define an explicit protected-role policy for RMC jobs before allowing Bureaucratic Error to mutate their slots.

## CS-0049 — Stop dead mobs vomiting by default

- Upstream: [space-wizards/space-station-14#40020](https://github.com/space-wizards/space-station-14/pull/40020), `ca29e0a16690a5f827095718afb60cfb44e702a8`, 2025-09-01
- Areas: Medical, Chemistry
- Status: Adapted
- Risk: Low
- Behavior/API delta: Standard vomiting now exits before changing hunger, thirst, movement, stomach contents, bloodstream chemicals, puddles, sound, or popups when the target is dead. Callers that intentionally need the old behavior can pass the new `force` argument.
- RMC/CMU divergence: RMC's separate predicted `RMCVomitSystem` already rejects dead entities in both delayed and immediate entry points. Standard reagent effects and administrative smites still use the server `VomitSystem`; their normal calls now inherit the upstream safeguard without changing the RMC sequence.
- Decision and rationale: Adapt the target-final guard to the current server-owned implementation and keep the optional `force` escape hatch. Importing the pinned target's later shared/predicted vomit rewrite here would mix a much broader API and prediction migration into this isolated medical fix.
- Files changed: `Content.Server/Medical/VomitSystem.cs`, `Content.IntegrationTests/Tests/Medical/VomitDeadMobTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static comparison confirms the pinned target retains the dead-state guard after moving standard vomiting into shared code. A queued integration regression fills a real body stomach, verifies a normal dead-mob call preserves its contents, then verifies `force: true` empties it. Execution is deferred to the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Audit the later shared/predicted vomit rewrite separately, including how its relayed stomach event should coexist with RMC's own predicted vomit sequence.

## CS-0050 — Correct zero-gravity push-off surfaces

- Upstream: [space-wizards/space-station-14#44053](https://github.com/space-wizards/space-station-14/pull/44053), `e3c10b36b1a41bff03c05ba196e6f56d224cdac6`, 2026-06-26
- Areas: Movement, Physics, GameTicking
- Status: Ported
- Risk: Medium
- Behavior/API delta: The per-tick weightless near-surface query now considers only approximate dynamic/static physics candidates, ignores fixtures belonging to the mover's transform descendants, and requires the mover's collision mask to accept the contacted fixture's layer. A surface that only collides in the reverse direction no longer grants push-off movement.
- RMC/CMU divergence: RMC adds carried entities, relay movers, vehicles, and collision groups around the shared controller but does not override this near-surface check. Ignoring descendants prevents carried or attached static fixtures from becoming a private movement surface, while the one-way test respects RMC's asymmetric projectile, mob, and vehicle masks.
- Decision and rationale: Port the pinned target's complete query and filter contract while retaining CS-0006's reusable per-controller result set. Use the existing cached `PullableQuery` instead of a fresh generic component lookup; no RMC speed, relay, or movement-input policy changes are included.
- Files changed: `Content.Shared/Movement/Systems/SharedMoverController.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static comparison confirms the pinned target retains the same lookup flags, descendant exclusion, one-way mask test, and cached pullable query. Compilation and focused movement/physics execution are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add a behavioral integration fixture for asymmetric layers and mover-owned static children if the controller gains a test seam; separately audit later zero-gravity and relay-movement changes rather than expanding this collision fix.

## CS-0051 — Retain maximum gun-spread modifier updates

- Upstream: [space-wizards/space-station-14#38960](https://github.com/space-wizards/space-station-14/pull/38960), `f3ce4281656b120fa2f4f7ee48ffaaf09e329e55`, 2025-07-12
- Areas: Shooting
- Status: AlreadyPresent
- Risk: Low
- Behavior/API delta: Refreshing gun modifiers compares the cached maximum spread against the event's new maximum spread before updating and dirtying it. A maximum-only modifier change is no longer skipped merely because the old maximum happens to equal the new minimum.
- RMC/CMU divergence: RMC independently carried the identical two-byte comparator fix in `ab8af5fb7d3` on 2025-01-28, before the shared SS14 baseline. RMC's attachment and weapon-controller subscribers continue to use the corrected shared refresh path.
- Decision and rationale: Keep the existing RMC implementation and classify the later SS14 commit as already present. Reapplying it would be empty; changing modifier ordering or RMC attachment behavior would exceed the upstream bug fix.
- Files changed: `docs/upstream-sync/core-system-audit.md`.
- Validation: The SS14 commit is not an ancestor of `Rebase`, but blame attributes the exact `ev.MaxAngle` comparison in the current shared gun system to RMC commit `ab8af5fb7d3`; the pinned target contains the same comparison. No runtime code changed, so this entry adds no checkpoint test obligation.
- Follow-up/debt: Add direct gun-modifier refresh coverage when that API receives a stable test seam, especially for independent minimum and maximum attachment modifiers.

## CS-0052 — Retain empty projectile-grenade protection

- Upstream: [space-wizards/space-station-14#38946](https://github.com/space-wizards/space-station-14/pull/38946), `cfe825b0e3d4fea6d63251a22003820873cff343`, 2025-07-12
- Areas: Shooting, Physics
- Status: AlreadyPresent
- Risk: Low
- Behavior/API delta: Fragmentation returns before calculating a segment angle when a projectile grenade has no contained or pending projectiles, preventing integer division by zero.
- RMC/CMU divergence: RMC independently added the same guard in `d39173e479b` on 2025-05-10 as part of its airburst-grenade implementation. Its condition accepts all non-positive totals and protects the additional RMC fragment event, cluster event, shot event, and deletion flow around the shared calculation.
- Decision and rationale: Preserve RMC's earlier, slightly defensive `<= 0` guard and classify the SS14 change as already present. Replacing it with `== 0` would narrow protection without providing any target-final benefit.
- Files changed: `docs/upstream-sync/core-system-audit.md`.
- Validation: Blame traces the current pre-division guard to RMC commit `d39173e479b`, which predates the shared baseline; the pinned SS14 target retains its equivalent zero-count guard. No runtime code changed, so this entry adds no checkpoint test obligation.
- Follow-up/debt: Audit RMC's mutable `FragmentIntoProjectilesEvent.TotalCount` separately because subscribers can alter it after the initial segment angle has already been calculated.

## CS-0053 — Consume failed lock activations once

- Upstream: [space-wizards/space-station-14#39039](https://github.com/space-wizards/space-station-14/pull/39039), `a093a2dd289c8edeb973f6aca8a4bcc4321efa48`, 2025-07-17
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: Clicking a lock configured to toggle on activation now marks the activation handled before attempting the lock change. Failed access or interaction checks still produce their own feedback, but later activation handlers no longer also attempt to open the storage and display a second failure popup.
- RMC/CMU divergence: RMC uses the same shared lock and activation ordering for many access-controlled lockers, vendors, and storage structures, including RMC access groups. No fork-specific handler depends on a configured lock-toggle activation falling through after the attempt fails.
- Decision and rationale: Port the target-final handled-state ordering for both lock and unlock paths, plus the same punctuation/capitalization cleanup for their user-facing messages. Preserve all existing access rules, do-after durations, RMC access IDs, and storage behavior.
- Files changed: `Content.Shared/Lock/LockSystem.cs`, `Resources/Locale/en-US/lock/lock-component.ftl`, `Resources/Locale/en-US/storage/components/entity-storage-component.ftl`, `Content.IntegrationTests/Tests/Interaction/LockActivationHandledTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static comparison confirms the pinned target retains pre-attempt handling on both branches. A queued integration regression uses an access-controlled lock, proves an unauthorized unlock remains rejected, and requires the activation to be consumed. Execution is deferred to the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Audit the target's later in-hand lock activation and renamed lock-policy components separately; they are broader interaction/API changes and are not implied by this popup fix.

## CS-0054 — Refresh airtight data when emergency firelocks close

- Upstream: [space-wizards/space-station-14#38918](https://github.com/space-wizards/space-station-14/pull/38918), `76a7b31c1e59a11b5079d20a3e2feb9c0a7836dd`, 2025-07-15
- Areas: Physics, GameTicking
- Status: Ported
- Risk: Medium
- Behavior/API delta: When Monstermos closes a firelock during depressurization flood-fill, it now refreshes airtight data for both affected tiles before recomputing their adjacency bits. The active equalization pass sees the new barrier immediately instead of flowing through stale tile flags.
- RMC/CMU divergence: RMC does not override the atmosphere equalization or firelock emergency-stop systems. Its vehicle collision code recognizes firelocks independently and is unaffected; inherited maps and any RMC firelocks using the standard component receive the corrected gas-flow timing.
- Decision and rationale: Port the two target-final airtight refreshes at the exact state-transition boundary. Do not alter flood-fill limits, firelock thresholds, atmosphere scheduling, or RMC vehicle interaction as part of this stale-cache fix.
- Files changed: `Content.Server/Atmos/EntitySystems/AtmosphereSystem.Monstermos.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static comparison confirms the pinned target retains both pre-adjacency refresh calls and their ordering. The first 1,000-upstream-commit checkpoint will compile the server and run the accumulated focused suite; this private equalization path currently has no narrow integration seam.
- Follow-up/debt: Add a controlled depressurization/firelock integration scenario when atmosphere test utilities expose deterministic single-cycle equalization, and audit later Monstermos changes independently.

## CS-0055 — Let interaction-bypass users reach covered subfloors

- Upstream: [space-wizards/space-station-14#38813](https://github.com/space-wizards/space-station-14/pull/38813), `decaa58dfe09f3e17c3f6f798013d5b8a6fc703a`, 2025-07-10
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: Generic interaction attempts against entities hidden below floor tiles now skip the subfloor blocker when the actor carries `BypassInteractionChecksComponent`. Normal users remain blocked, and the change does not bypass attack, anchoring, or unanchoring restrictions.
- RMC/CMU divergence: RMC already uses the same marker for admin access to vendors, ID-locked storage, guns, stripping, pulling, and other interaction gates, and its `AdminObserver` prototype carries it. The shared subfloor system was the inconsistent holdout.
- Decision and rationale: Port the target-final early return only in the generic interaction-attempt handler. Reuse the established marker instead of detecting admin sessions directly, and retain the separate safety policies for attacks and structure anchoring.
- Files changed: `Content.Shared/SubFloor/SharedSubFloorHideSystem.cs`, `Content.IntegrationTests/Tests/Interaction/SubfloorAdminInteractionTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static comparison confirms the pinned target retains this exact marker gate. A queued integration regression places a real high-voltage cable beneath steel flooring, requires a normal attempt to be cancelled, and permits the same attempt from a marked actor. Execution is deferred to the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Audit other visibility-layer blockers for use of the shared bypass marker, but do not generalize it to damage or anchoring without a separate admin-safety decision.

## CS-0056 — Publish communications cooldown after map initialization

- Upstream: [space-wizards/space-station-14#38305](https://github.com/space-wizards/space-station-14/pull/38305), `d0b798b63fc58138043007f2635b8ac99b80391e`, 2025-07-18
- Areas: GameTicking, Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: A communications console now publishes its initial UI state after `MapInit` assigns the announcement cooldown. Newly spawned consoles no longer advertise that announcements are available during their configured initial delay.
- RMC/CMU divergence: RMC's communication towers and distress-signal timing use separate components and systems. Inherited standard, syndicate, wizard, and CentComm consoles use this shared server system and retain their individual delay, access, global-announcement, and shuttle policies.
- Decision and rationale: Move the initial state publication from `ComponentInit` to the existing cooldown-setting `MapInit` handler, matching the pinned target's final ordering. Do not change periodic UI refresh timing or any RMC communication-tower lifecycle.
- Files changed: `Content.Server/Communications/CommunicationsConsoleSystem.cs`, `Content.IntegrationTests/Tests/Communications/CommunicationsConsoleInitialCooldownTest.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static comparison confirms the pinned target retains this exact lifecycle ordering. A queued integration regression spawns the real standard console, requires its stored cooldown to equal `InitialDelay`, and verifies the first bound-UI state reports `CanAnnounce == false`. Execution is deferred to the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Audit later communications-console logging, access, and identity changes independently; none are required to correct the initial state publication.

## CS-0057 — Prevent duplicate reaction sounds

- Upstream: [space-wizards/space-station-14#38999](https://github.com/space-wizards/space-station-14/pull/38999), `bdf3c891e78193e7217416d3d4e0799cb5667c9a`, 2025-07-15
- Areas: Chemistry, GameTicking
- Status: Adapted
- Risk: Low
- Behavior/API delta: Chemical reactions still run through the shared predicted path, but their PVS sound is now emitted only by the authoritative server. Clients no longer hear both a locally predicted reaction sound and the replicated server sound. The reaction loop also stops allocating and populating a set that was never read.
- RMC/CMU divergence: RMC retains additional reagent data and reaction behavior around the shared system, including typed reagent prototype IDs introduced during this sync. None of those changes assign separate sound ownership, so server authority removes the duplicate without changing reaction selection, quantities, effects, or administrative logging.
- Decision and rationale: Adapt the pinned target's network-side guard to the current typed-reagent implementation and leave all reaction math intact. A fully predicted audio path would require threading an initiating actor through every reaction entry point and is outside this isolated fix.
- Files changed: `Content.Shared/Chemistry/Reaction/ChemicalReactionSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static comparison confirms the pinned target retains server-only PVS emission and removal of the unused set. Compilation and the accumulated focused suite are queued for the first 1,000-upstream-commit checkpoint; the current reaction test harness has no stable client/server audio replication seam.
- Follow-up/debt: Add a two-instance audio regression if the integration harness exposes replicated sound events, and separately audit any future conversion to actor-aware predicted reaction audio.

## CS-0058 — Store transformable-container reagent identity

- Upstream: [space-wizards/space-station-14#38988](https://github.com/space-wizards/space-station-14/pull/38988), `2e6549a308f8838fd5fc41981970a806f1d3d9ad`, 2025-07-14
- Areas: Chemistry, Interactions
- Status: Adapted
- Risk: Low
- Behavior/API delta: `TransformableContainerComponent` now stores the current reagent's prototype ID instead of retaining a `ReagentPrototype` instance. Name refreshes resolve the live prototype, so prototype reloads cannot leave glasses comparing against or displaying data from a stale object.
- RMC/CMU divergence: RMC resolves reagents through `TryIndexReagent` and its reagent-system compatibility layer rather than relying solely on the base prototype manager. The port keeps that lookup path and stores CMU's existing typed `ProtoId<ReagentPrototype>`, while transformation descriptions and primary-reagent selection remain unchanged.
- Decision and rationale: Adapt the target-final identity-based component state to the current RMC reagent API. Store the exact primary-reagent ID after successful resolution, and resolve it again only when localized naming needs the prototype object.
- Files changed: `Content.Server/Chemistry/Components/TransformableContainerComponent.cs`, `Content.Server/Chemistry/EntitySystems/TransformableContainerSystem.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static review confirms no prototype object remains in component state and both comparison and refresh use the typed ID. Compilation and the accumulated focused suite are queued for the first 1,000-upstream-commit checkpoint; reproducing a live prototype reload requires broader prototype-manager integration coverage.
- Follow-up/debt: Add a reload regression for transformable drink containers when the integration harness exposes deterministic prototype reloads, and remove the RMC compatibility lookup only as part of a dedicated reagent-system migration.

## CS-0059 — Let utility belts hold remote signallers

- Upstream: [space-wizards/space-station-14#35212](https://github.com/space-wizards/space-station-14/pull/35212), `4e59b617490e0709bb6ca496c4eef57bd40b3fb8`, 2025-07-16
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: Remote signallers now carry a dedicated storage tag, and the standard utility belt accepts that tag. Engineers can keep the handheld signal-control tool in the same constrained belt used for other basic engineering equipment.
- RMC/CMU divergence: RMC maps and engineering vendors already use the inherited `RemoteSignaller` entity, while RMC-specific belt whitelists remain independent. Adding the tag to the base signaller also covers its advanced child, without widening the standard utility belt to unrelated device-link items.
- Decision and rationale: Port the target-final dedicated tag and the narrowly scoped utility-belt whitelist entry. Preserve every existing size, capacity, RMC storage rule, and signaller networking behavior.
- Files changed: `Resources/Prototypes/Entities/Clothing/Belt/belts.yml`, `Resources/Prototypes/Entities/Objects/Devices/Electronics/signaller.yml`, `Resources/Prototypes/tags.yml`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static comparison confirms the pinned target retains the tag on the signaller and its engineering-belt whitelist. Prototype loading and the accumulated focused suite are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add resolved-prototype storage whitelist coverage if belt acceptance rules gain a lightweight integration helper; audit RMC engineering belts separately rather than inheriting the standard whitelist implicitly.

## CS-0060 — Avoid false client-side power loss

- Upstream: [space-wizards/space-station-14#38647](https://github.com/space-wizards/space-station-14/pull/38647), `c60910dfa68bbed56a4cad4b0739b532f8930006`, 2025-07-14
- Areas: Physics, GameTicking, Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: Client-side power checks now optimistically treat an entity without a local `ApcPowerReceiverComponent` as powered. Terminals and other client interactions no longer mispredict a power failure when the authoritative receiver state is simply absent from client prediction.
- RMC/CMU divergence: RMC terminals and powered interactables share this client helper but retain their server-authoritative APC and machine logic. Existing receivers still return their replicated `Powered` value; only the missing-component prediction fallback changes.
- Decision and rationale: Port the pinned target's single fallback change. The server remains authoritative and will reject genuinely unpowered actions, while the client avoids a false negative it cannot establish from local state.
- Files changed: `Content.Client/Power/EntitySystems/StaticPowerSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static comparison confirms the pinned target retains the optimistic missing-receiver fallback. Client compilation and the accumulated focused suite are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add a client prediction regression covering both absent and explicitly unpowered receivers when the integration harness can instantiate the client-only component manager directly.

## CS-0061 — Suppress damage examination for indestructible windows

- Upstream: [space-wizards/space-station-14#38950](https://github.com/space-wizards/space-station-14/pull/38950), `dd87e7ef644fa2e0ed3d2003151e1fbcaf0afcbb`, 2025-07-12
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: Square and diagonal indestructible plastitanium windows now override their inherited `ExaminableDamage` message set with `null`. Examining them no longer asks the damage-examine system to describe structural damage on entities that intentionally have no damageable/destructible state.
- RMC/CMU divergence: RMC adds knock interaction messaging to the common plastitanium-window base but does not replace these inherited standard prototypes or the examination system. Knock feedback remains available; only the invalid damage description is suppressed on the two indestructible children.
- Decision and rationale: Port the target-final per-prototype override rather than weakening `ExaminableDamageSystem` for every entity. Destructible plastitanium windows keep their normal damage messages and thresholds.
- Files changed: `Resources/Prototypes/Entities/Structures/Windows/plastitanium.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static comparison confirms the pinned target retains `messages: null` on exactly the square and diagonal indestructible variants. Prototype loading and the accumulated focused suite are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Audit other indestructible children that inherit damage examination without damage state, preferably with a resolved-prototype linter rule instead of broad runtime suppression.

## CS-0062 — Preserve produce with no extractable yield

- Upstream: [space-wizards/space-station-14#38427](https://github.com/space-wizards/space-station-14/pull/38427), `d9545dd3803333e2865a43b476466fb9eddc4a1c`, 2025-07-14
- Areas: Chemistry, Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: A produce material extractor now rejects plants whose matching reagent quantity truncates to zero, shows the user a specific popup, and leaves the produce intact. It no longer plays the extraction sound and deletes an item while adding no biomass.
- RMC/CMU divergence: RMC retains the standard produce extractor and typed reagent IDs used by its inherited botany content. The guard runs after the existing RMC-compatible reagent match, so extraction recipes, valid yields, power checks, and material storage behavior are unchanged.
- Decision and rationale: Port the pinned target's zero-yield boundary and user feedback at the point where fractional reagent quantity becomes an integer material amount. Preserve the existing truncation rule for valid positive yields rather than changing material balance.
- Files changed: `Content.Server/Materials/ProduceMaterialExtractorSystem.cs`, `Resources/Locale/en-US/materials/material-extractor.ftl`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static comparison confirms the pinned target retains the zero-yield early return before storage mutation, sound, deletion, and handled state. Server compilation, localization validation, and the accumulated focused suite are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add an interaction regression proving sub-unit produce survives while valid produce is consumed; separately decide whether fractional extraction should accumulate instead of truncate.

## CS-0063 — Correct powered-machine admin logs

- Upstream: [space-wizards/space-station-14#38961](https://github.com/space-wizards/space-station-14/pull/38961), `8b3232f305024876427c1c73ccbc4d14c8bdda07`, 2025-07-12
- Areas: Interactions, GameTicking
- Status: Ported
- Risk: Low
- Behavior/API delta: Administrative action logs for toggling a power-charge machine no longer insert a literal dollar sign before the formatted target entity. The structured entity field remains intact for investigation and filtering.
- RMC/CMU divergence: RMC uses the shared power-charge system for inherited machines and adds no alternate formatter at this log site. Machine state, power load, UI refresh, and log impact levels are unchanged.
- Decision and rationale: Port the target-final interpolation correction and adjacent pattern-spacing cleanup exactly; no broader logging or machine-power refactor is needed.
- Files changed: `Content.Server/Power/EntitySystems/PowerChargeSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static comparison confirms the pinned target retains the corrected structured log template. Server compilation and the accumulated focused suite are queued for the first 1,000-upstream-commit checkpoint; this presentation-only fix adds no runtime test obligation.
- Follow-up/debt: Consider a structured admin-log formatting test if the logging harness gains capture support, and scan other templates for accidental interpolation-prefix characters separately.

## CS-0064 — Ship live scurrets in ventilated crates

- Upstream: [space-wizards/space-station-14#38951](https://github.com/space-wizards/space-station-14/pull/38951), `31c84eaf20b3cea8af6b49336489a0c9e2b4ee27`, 2025-07-13
- Areas: Medical, Physics
- Status: Ported
- Risk: Low
- Behavior/API delta: The hydrated-scurret cargo product now inherits the livestock crate instead of the sealed plastic crate. Its living occupant receives the same ventilation behavior as other shipped animals and no longer suffocates before delivery.
- RMC/CMU divergence: RMC inherits both the cargo product and crate families without overriding `CrateFunScurret`. Its atmosphere and mob-respiration systems therefore receive the data correction without changes to scurret physiology, cargo price, crate contents, or storage interaction.
- Decision and rationale: Port the pinned target's parent replacement at the cargo prototype. Changing scurret respiration or globally ventilating plastic crates would alter unrelated gameplay.
- Files changed: `Resources/Prototypes/Catalog/Fills/Crates/fun.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static comparison confirms the pinned target retains `CrateLivestock` for this product and the parent prototype resolves locally. Prototype loading and the accumulated focused suite are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add a resolved-prototype check for living `StorageFill` occupants in non-livestock crates, then review intentional exceptions individually.

## CS-0065 — Size the ocarina as a small item

- Upstream: [space-wizards/space-station-14#38971](https://github.com/space-wizards/space-station-14/pull/38971), `bf1b55e22f11cde064bba01dbad2ca159e62a824`, 2025-07-13
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: The ocarina now overrides its inherited item size to `Small`, allowing it to fit the same storage spaces as other compact handheld wind instruments.
- RMC/CMU divergence: RMC does not override the standard ocarina or base woodwind storage behavior. Instrument playback, loadout placement, arcade rewards, and RMC-specific storage whitelists remain unchanged.
- Decision and rationale: Port the pinned target's explicit item-size override on the concrete prototype. Changing the woodwind base would resize larger instruments unintentionally.
- Files changed: `Resources/Prototypes/Entities/Objects/Fun/Instruments/instruments_wind.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static comparison confirms the pinned target retains `size: Small` on `OcarinaInstrument`. Prototype loading and the accumulated focused suite are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Audit anomalous inherited item sizes by concrete sprite footprint as a separate data-quality pass.

## CS-0066 — Classify the war declarator as contraband

- Upstream: [space-wizards/space-station-14#38972](https://github.com/space-wizards/space-station-14/pull/38972), `7fd74b08df3f99fda5c0b184d07dd7b485a50d25`, 2025-07-13
- Areas: Gamerules, Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: The nuclear-operative war declarator now inherits `BaseSyndicateContraband` alongside `BaseItem`, exposing its intended contraband classification to scanners, examinations, and other policy consumers.
- RMC/CMU divergence: RMC retains the standard nuclear-operations item and contraband hierarchy but runs its primary distress-signal mode through separate antagonist rules. The added marker does not change declaration timing, access, shuttle delay, telecrystal rewards, or announcement behavior.
- Decision and rationale: Port the pinned target's additional prototype parent and preserve all concrete war-declarator components. Contraband identity belongs in prototype data rather than special cases in each scanner.
- Files changed: `Resources/Prototypes/Entities/Objects/Devices/Syndicate_Gadgets/war_declarator.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static comparison confirms the pinned target retains the syndicate-contraband parent and the parent resolves locally. Prototype loading and the accumulated focused suite are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Audit other antagonist-only rule devices for missing faction contraband parents as a separate prototype pass.

## CS-0067 — Resolve zombie emote sounds on demand

- Upstream: [space-wizards/space-station-14#38979](https://github.com/space-wizards/space-station-14/pull/38979), `45fe7d5093636949b6d060b48d7850d0d00d8438`, 2025-07-13
- Areas: Medical, Gamerules, Interactions
- Status: Adapted
- Risk: Low
- Behavior/API delta: `ZombieComponent` now stores a typed emote-sounds prototype ID instead of caching an `EmoteSoundsPrototype` object during startup. Each zombie emote resolves the live prototype, so prototype reloads cannot leave existing zombies using stale sound definitions.
- RMC/CMU divergence: RMC adds flammability and other infection behavior to the standard zombie system but does not replace zombie emote ownership. Those medical/combat additions remain intact, and only the emote lookup moves from component startup to the existing emote handler.
- Decision and rationale: Adapt the target-final identity-based state to the current system's explicit prototype-manager dependency. Remove the now-unnecessary startup subscription and cached object while preserving emote ordering and handled semantics.
- Files changed: `Content.Server/Zombies/ZombieSystem.cs`, `Content.Shared/Zombies/ZombieComponent.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static review confirms component state contains only the typed ID and the emote handler resolves it immediately before playback. Shared/server compilation and the accumulated focused suite are queued for the first 1,000-upstream-commit checkpoint; deterministic prototype reload coverage is not currently exposed by the zombie test seam.
- Follow-up/debt: Add a live-reload emote regression when prototype reloads are controllable in integration tests, and audit the remaining legacy string prototype fields on `ZombieComponent` separately.

## CS-0068 — Give observers clear distant whispers

- Upstream: [space-wizards/space-station-14#38202](https://github.com/space-wizards/space-station-14/pull/38202), `bd853b60de27889e56033370eb82bdba5266d7db`, 2025-07-14
- Areas: Interactions
- Status: Adapted
- Risk: Low
- Behavior/API delta: Observer recipients now take the clear-whisper branch regardless of distance, so ghosts no longer receive random readability fragments for whispers they are permitted to monitor.
- RMC/CMU divergence: RMC moved whisper delivery into its language-aware partial chat system. The guard is applied there, preserving RMC listener-specific language transformation, line-of-sight policy, identity naming, replay capture, and xeno filtering while bypassing only distance-based fragment obfuscation for observers.
- Decision and rationale: Adapt the target-final `data.Observer` condition at the equivalent RMC branch rather than restoring the obsolete base whisper method. Living listeners retain both clear and muffled distance thresholds.
- Files changed: `Content.Server/_RMC14/Chat/Chat/ChatSystem.Language.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static control-flow review confirms observers still pass the existing range-transmit and language checks but can no longer enter either distance-obfuscated branch. Server compilation and the accumulated focused suite are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add multi-session chat coverage for nearby living, distant living, and observer recipients, including an RMC language the observer does not ordinarily understand.

## CS-0069 — Prevent permanent carp suicide

- Upstream: [space-wizards/space-station-14#39033](https://github.com/space-wizards/space-station-14/pull/39033), `93e04de36bc965b51bc086cd88fb5a4b332c2782`, 2025-07-17
- Areas: Medical, Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: The base space-carp family now carries `CannotSuicide`. A player controlling a carp may still ghost through the shared suicide flow, but the body is not killed and the mind may return, matching other special ghost-role creatures.
- RMC/CMU divergence: RMC gates suicide with its own configuration variable but retains the shared tag policy once an attempt is allowed. Applying the tag to `BaseMobCarp` covers inherited carp variants without changing their combat AI, health, ghost-role assignment, or RMC suicide enablement.
- Decision and rationale: Port the target-final tag at the common carp prototype rather than special-casing carp in `SuicideSystem`. Existing generic `CannotSuicide` semantics remain the single policy source.
- Files changed: `Resources/Prototypes/Entities/Mobs/NPCs/carp.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static comparison confirms the pinned target retains the tag on the base carp and the shared suicide system checks it before body death. Prototype loading and the accumulated focused suite are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Audit other returnable ghost-role creatures for consistent `CannotSuicide` tagging and add a mind/body suicide regression when the ghost test harness supports session transfer.

## CS-0070 — Configure general-population access

- Upstream: [space-wizards/space-station-14#39043](https://github.com/space-wizards/space-station-14/pull/39043), `1bc1d71d4253c93950736c2d18cda8a4d2d4f9b3`, 2025-07-18
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: The standard access configurator can now assign both `GenpopEnter` and `GenpopLeave`, allowing authorized users to configure the two directional general-population permissions on supported doors.
- RMC/CMU divergence: RMC defines additional access levels and devices but does not override the inherited standard configurator or these two security access prototypes. Universal/admin configurator behavior and RMC-specific access policy remain unchanged.
- Decision and rationale: Port the two target-final allowlist entries to the normal configurator. This exposes already-defined access prototypes without widening privilege checks or teaching the tool every RMC access level implicitly.
- Files changed: `Resources/Prototypes/Entities/Objects/Tools/access_configurator.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static review confirms both access prototypes resolve locally and the pinned target retains them in the configurator list. Prototype loading and the accumulated focused suite are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Define an explicit RMC configurator policy before adding any faction-specific access IDs; add a resolved allowlist test if access-prototype validation is not already covered by YAML linting.

## CS-0071 — Honor resistance bypass on wide melee swings

- Upstream: [space-wizards/space-station-14#38496](https://github.com/space-wizards/space-station-14/pull/38496), `80c6650730d3d019b318ca0af8ed1269431746f8`, 2025-07-10
- Areas: Shooting, Medical, Interactions
- Status: Adapted
- Risk: Medium
- Behavior/API delta: Heavy/wide melee attacks now resolve the weapon's effective resistance-bypass policy and pass it to every target's damage application. Weapons configured or modified to bypass resistances behave consistently between light and wide attacks.
- RMC/CMU divergence: RMC subscribers can alter damage and `ResistanceBypass` through the existing melee-damage event, and the current fork additionally supplies the attacking tool to damage processing. The port uses that event-derived bypass value while retaining RMC range, skill, lunge, stamina, and tool-attribution behavior.
- Decision and rationale: Adapt the target-final two-line fix at the current heavy-attack path. Do not copy the older target call verbatim because dropping CMU's `tool: meleeUid` argument would regress downstream attribution.
- Files changed: `Content.Shared/Weapons/Melee/SharedMeleeWeaponSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static control-flow comparison confirms light and heavy attacks now call the same bypass resolver and both forward the result to `TryChangeDamage`. Shared compilation and the accumulated focused suite are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add a predicted melee regression using a resistant target and an event-modified bypass weapon, covering both light and multi-target heavy attacks.

## CS-0072 — Reject stale mouse-rotation requests

- Upstream: [space-wizards/space-station-14#39071](https://github.com/space-wizards/space-station-14/pull/39071), `cfb0a950359662489cc36115c3de8bad741649f4`, 2025-07-19
- Areas: Movement, Interactions, GameTicking
- Status: Ported
- Risk: Low
- Behavior/API delta: Each predicted mouse-rotation request now identifies the entity controlled when the client generated it. The shared handler ignores the request if the session has attached to a different entity before processing, preventing stale input from rotating the new body or producing component error spam.
- RMC/CMU divergence: RMC does not override the mouse-rotator request or shared handler. Turrets, vehicles, and other fork systems using the component keep their rotation speed, tolerance, cardinal mode, and prediction behavior; only cross-body stale requests are discarded.
- Decision and rationale: Port the pinned target's coordinated event-schema, client population, and server validation changes together. Comparing the serialized network entity to the session attachment keeps the sender authoritative only over its current body.
- Files changed: `Content.Client/MouseRotator/MouseRotatorSystem.cs`, `Content.Shared/MouseRotator/MouseRotatorComponent.cs`, `Content.Shared/MouseRotator/SharedMouseRotatorSystem.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static review confirms both client request sites populate `User` and the handler validates it before component lookup or mutation. Client/shared compilation and the accumulated focused suite are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add a two-body prediction regression that queues a request for body A, attaches the session to body B, and proves B's goal rotation is unchanged.

## CS-0073 — Clarify nuclear-threat elimination announcements

- Upstream: [space-wizards/space-station-14#39158](https://github.com/space-wizards/space-station-14/pull/39158), `0ab0dadb1d4fc4034d3de04f9ab471fdc653b2f7`, 2025-07-22
- Areas: Gamerules
- Status: Ported
- Risk: Low
- Behavior/API delta: Both nuclear-operative elimination announcements now use concise wording that distinguishes a newly called emergency shuttle from one already en route while preserving the ETA variables and recall guidance.
- RMC/CMU divergence: RMC has separate primary-mode round flow but does not override these inherited Fluent keys. Standard nuclear-operations rules and admin-triggered scenarios receive the wording update without changing elimination detection or shuttle timing.
- Decision and rationale: Port the pinned target's two retained localization values exactly. No game-rule code or announcement dispatch API changes are involved.
- Files changed: `Resources/Locale/en-US/nukeops/nuke-ops.ftl` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static comparison confirms both strings and their `$time`/`$units` placeholders match the pinned target. Fluent validation is queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: None beyond normal localization review for non-English locales.

## CS-0074 — Include custom-vote titles in results

- Upstream: [space-wizards/space-station-14#39137](https://github.com/space-wizards/space-station-14/pull/39137), `24b75d89a501eda4462537c6fbc33c5bcc92c168`, 2025-07-23
- Areas: Gamerules, GameTicking
- Status: Ported
- Risk: Low
- Behavior/API delta: Custom-vote win and tie announcements now include the vote title as well as the result, so players can identify which question finished when votes overlap or chat history is busy.
- RMC/CMU divergence: RMC does not override custom-vote creation or these Fluent keys. Vote eligibility, timing, webhooks, administrative logs, and result calculation are unchanged.
- Decision and rationale: Port both localization arguments and both corresponding message templates atomically so Fluent never receives a missing `$title`. Preserve the existing title text without additional escaping or policy changes.
- Files changed: `Content.Server/Voting/VoteCommands.cs`, `Resources/Locale/en-US/voting/vote-commands.ftl`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static review confirms the win and tie paths both supply `title` and their templates consume it. Server compilation, Fluent validation, and the accumulated focused suite are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add a vote-manager regression that completes one winning vote and one tie and captures both server announcements with their original titles.

## CS-0075 — Initialize and pace SSD sleep state

- Upstream: [space-wizards/space-station-14#38891](https://github.com/space-wizards/space-station-14/pull/38891), `f16175a6e314e0f8534cda18d4a6eda234468e79`, 2025-07-22
- Areas: Medical, GameTicking
- Status: Adapted
- Risk: Medium
- Behavior/API delta: SSD entities now initialize networked, pause-aware sleep and polling deadlines during map initialization and dirty the component immediately. Once eligible, the system refreshes the permanent SSD sleep effect at a one-second interval instead of attempting to recreate it every tick.
- RMC/CMU divergence: RMC also places `SSDIndicator` on its training dummy and retains the current shared status-effect compatibility system. The port preserves that API and all attach/detach behavior while giving both standard mobs and RMC prototypes correctly replicated deadlines.
- Decision and rationale: Adapt the target-final component state and update cadence to the fork's `SharedStatusEffectsSystem` type. Use `TimeOffsetSerializer` plus auto-pause fields so map pauses and late joins observe consistent absolute times.
- Files changed: `Content.Shared/SSDIndicator/SSDIndicatorComponent.cs`, `Content.Shared/SSDIndicator/SSDIndicatorSystem.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static review confirms MapInit initializes and dirties both deadlines, the update loop respects SSD state, pause-aware times, deletion, and polling cadence, and the current status API exposes `TryUpdateStatusEffectDuration`. Shared compilation and the accumulated focused suite are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add an integration regression for map initialization, attach/detach, pause/unpause, delayed sleep, and single permanent status-effect ownership, including the RMC training dummy.

## CS-0076 — Dispose stale atmosphere-monitor pipe networks

- Upstream: [space-wizards/space-station-14#38974](https://github.com/space-wizards/space-station-14/pull/38974), `002afe8056cb3e6e554bec98343506a13eaeacf6`, 2025-07-23
- Areas: Physics, GameTicking, Interactions
- Status: Adapted
- Risk: Medium
- Behavior/API delta: Removing a pipe network now broadcasts its grid and network ID so atmosphere-monitor caches can delete every matching subnet before topology is rebuilt. Replicated subnet keys also carry `Color` directly, avoiding string serialization and client-side hex reparsing.
- RMC/CMU divergence: RMC does not override the atmosphere-monitor console or pipe-net lifecycle, while its maps use the same colored piping and grid atmosphere systems. The port preserves CMU's server-owned pipe-color architecture and changes only cache disposal plus the shared network-state representation.
- Decision and rationale: Port the complete retained four-file contract atomically because the event producer, cache consumer, serialized record, and renderer must agree. Keep the event broadcast before actual set removal so listeners can still associate the old network with its grid.
- Files changed: `Content.Server/Atmos/EntitySystems/AtmosphereSystem.API.cs`, `Content.Server/Atmos/Consoles/AtmosMonitoringConsoleSystem.cs`, `Content.Shared/Atmos/Consoles/Components/AtmosMonitoringConsoleComponent.cs`, `Content.Client/Atmos/Consoles/AtmosMonitoringConsoleNavMapControl.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static review confirms the producer raises for a network with a grid, the console restricts cleanup to that grid and network ID, all subnet construction uses typed color, and the client consumes it directly. Client/shared/server compilation and the accumulated focused suite are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add a topology regression that removes a monitored pipe net, verifies every affected cached chunk drops the old ID, and round-trips a non-white pipe color through component state.

## CS-0077 — Inherit the default parrot name

- Upstream: [space-wizards/space-station-14#39131](https://github.com/space-wizards/space-station-14/pull/39131), `378fbb0ba91355417750d31ae094b5622649de97`, 2025-07-22
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: The default `parrot` name now lives on `MobParrotBase` rather than only `MobParrot`, so derived parrot prototypes inherit a valid display name unless they intentionally override it.
- RMC/CMU divergence: RMC does not override the standard parrot base; named special parrots such as Polly keep their concrete names. Accent, memory, ghost-role, petting, and movement behavior are unchanged.
- Decision and rationale: Port the target-final metadata move as one prototype change. Keeping the duplicate concrete name would obscure whether future parrot children correctly inherit base identity.
- Files changed: `Resources/Prototypes/Entities/Mobs/NPCs/animals.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static comparison confirms the pinned target retains the base name and removes it from `MobParrot`, while named descendants still override it. Prototype loading is queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: None beyond normal prototype-inheritance linting.

## CS-0078 — Correct debug command permissions

- Upstream: [space-wizards/space-station-14#39167](https://github.com/space-wizards/space-station-14/pull/39167), `de576a429d9461badb3e8a2a01721f62e0ed4b2d`, 2025-07-23
- Areas: Movement, Physics
- Status: Ported
- Risk: Low
- Behavior/API delta: The debug permission group now grants the active `showvel`, `showrot`, `showangvel`, and `showplayervelocity` commands instead of the obsolete `showvelocities` command.
- RMC/CMU divergence: CMU keeps its additional debug permissions and only updates the five upstream command entries; no RMC-specific command implementation is changed.
- Decision and rationale: Port the retained permission-list correction exactly. This restores access to the engine's split velocity and rotation diagnostics without broadening non-debug roles.
- Files changed: `Resources/engineCommandPerms.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static comparison confirms the five entries match the pinned target-final permission list. Permission loading and command availability are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Verify each diagnostic command remains registered after future RobustToolbox pointer updates so stale permission names do not accumulate again.

## CS-0079 — Name the remote station-AI eye

- Upstream: [space-wizards/space-station-14#39177](https://github.com/space-wizards/space-station-14/pull/39177), `01a57c9a1715bd3349fd981a58b01bb6edb5bb04`, 2025-07-24
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: Attaching a station AI to its remote movement entity now renames that eye to `AI eye - <AI name>`, making multiple remote eyes distinguishable in entity-facing tools and interactions.
- RMC/CMU divergence: CMU retains its older station-AI lifecycle, but the attach path, metadata dependency, and remote entity are compatible. RMC silicon behavior is otherwise unchanged.
- Decision and rationale: Port the retained two-file target behavior at the existing attach boundary, where both the inserted AI and spawned remote eye are known.
- Files changed: `Content.Shared/Silicons/StationAi/SharedStationAiSystem.cs`, `Resources/Locale/en-US/silicons/station-ai.ftl`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static review confirms the localized name is applied after movement relaying and matches the pinned target-final key and format. Shared compilation and station-AI lifecycle coverage are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add a lifecycle regression that inserts a custom-named AI, asserts its eye name, and verifies replacement eyes receive the same derived name.

## CS-0080 — Make round-end event waits tick-deterministic

- Upstream: [space-wizards/space-station-14#39133](https://github.com/space-wizards/space-station-14/pull/39133), `65b4b41928adca08247227844d376567c13374d6`, 2025-07-22
- Areas: GameTicking, Gamerules
- Status: Ported
- Risk: Low
- Behavior/API delta: `RoundEndTest` now waits for round-system transitions for at most 60 synchronized simulation ticks instead of racing a ten-second wall-clock task while advancing five ticks at a time.
- RMC/CMU divergence: The older CMU test fixture shape is retained; only the target-final event counter and deterministic wait loop are adapted. Production round flow is unchanged.
- Decision and rationale: Port the retained test correction because simulation-time bounds are reproducible, faster on failure, and exercise each intermediate tick rather than depending on host scheduling.
- Files changed: `Content.IntegrationTests/Tests/RoundEndTest.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static review confirms the loop returns only after the subscribed round-end event changes the counter and otherwise fails after exactly 60 synchronized ticks. The integration test is queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: If legitimate round transitions exceed 60 ticks after future rule changes, derive the bound from configured durations while keeping it in simulation time.

## CS-0081 — Exercise delayed entity update loops in integration tests

- Upstream: [space-wizards/space-station-14#38901](https://github.com/space-wizards/space-station-14/pull/38901), `c3ff6c9184889bf009a27e736cd4639a8d05ef93`, 2025-07-23
- Areas: GameTicking
- Status: Ported
- Risk: Low
- Behavior/API delta: The broad entity spawn/delete integration cases now simulate 450 ticks (15 seconds at the expected test tick rate) instead of 15 ticks, allowing most delayed system update loops to execute before entity-state assertions.
- RMC/CMU divergence: The existing CMU entity-test fixture and fork-specific prototypes remain intact; only the retained target-final simulation windows and adjacent whitespace correction are applied.
- Decision and rationale: Port the longer tick horizon so cleanup and lifecycle faults hidden behind periodic updates become visible at the checkpoint rather than escaping a short smoke window.
- Files changed: `Content.IntegrationTests/Tests/EntityTest.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static review confirms both target-final spawn/delete waits are 450 ticks. The expanded integration cases are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Track runtime at the checkpoint; if these cases become a bottleneck, retain the behavioral horizon while optimizing fixture batching rather than shortening coverage.

## CS-0082 — Supply the blocked magic-mirror target identity

- Upstream: [space-wizards/space-station-14#38907](https://github.com/space-wizards/space-station-14/pull/38907), `cce239dd93b31735707e40713cae038f9d34deb3`, 2025-07-10; target-final correction [#42948](https://github.com/space-wizards/space-station-14/pull/42948), `0b81cfb99eeb264f5d0ef4b01176915818a36597`, 2026-02-16
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: Every hat-blocked magic-mirror operation now supplies the required `$target` localization argument. The identity is the person whose clothing blocks the operation, matching the later target-final correction, rather than the acting barber.
- RMC/CMU divergence: CMU retains four server-side mirror handlers while target-final consolidates the check into the shared system. The final behavior is adapted consistently across all four existing paths without importing the larger appearance refactor.
- Decision and rationale: Port the missing localization argument and immediately fold in its retained target-final semantic correction so messages format successfully and use the blocked target's identity and grammar.
- Files changed: `Content.Server/MagicMirror/MagicMirrorSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static review confirms all four `magic-mirror-blocked-by-hat-self-target` calls provide the actual target entity. Localization formatting and self/other interaction cases are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: When the shared magic-mirror refactor is assessed, preserve this target-identity behavior while removing the duplicated server checks.

## CS-0083 — Pass entity-aware delivery popup identities

- Upstream: [space-wizards/space-station-14#38909](https://github.com/space-wizards/space-station-14/pull/38909), `a97223bc70471140a5507e2523a9b4e7ad0df291`, 2025-07-10
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: Predicted delivery unlock and open messages now pass `Identity.Entity` as `$recipient` rather than flattening the actor through `Identity.Name`, preserving entity-aware Fluent capitalization, grammar, and identity masking.
- RMC/CMU divergence: The standard delivery system has no RMC override in these paths; CMU's existing popup prediction and possessive entity argument remain unchanged.
- Decision and rationale: Port the retained two-call target-final correction exactly because the localization contract expects an entity-like value, not an already-rendered name.
- Files changed: `Content.Shared/Delivery/SharedDeliverySystem.cs`, `docs/upstream-sync/inventory-wave-0001.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static review confirms both other-viewer delivery messages use the entity-aware identity while their self messages and `$possadj` argument are unchanged. Shared compilation and delivery popup formatting are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add masked-identity coverage so delivery popups prove that observer-specific identity rendering remains intact.

## CS-0084 — Preserve thieving capability through cloning

- Upstream: [space-wizards/space-station-14#38914](https://github.com/space-wizards/space-station-14/pull/38914), `773299bd07278b05b6952063c3e4c38ddeb72966`, 2025-07-10
- Areas: Interactions, GameTicking
- Status: Ported
- Risk: Low
- Behavior/API delta: `BaseClone` now copies `ThievingComponent`, preserving stealth-stripping capability and its alert state when a character receives a cloned body.
- RMC/CMU divergence: CMU retains the older monolithic cloning-settings prototype, so the target-final `Special`-settings entry is adapted into its equivalent job/special component list. RMC cloning consumers remain unchanged.
- Decision and rationale: Port the retained component entry at the existing fork boundary; cloning already shallow-copies the other special capabilities listed beside it.
- Files changed: `Resources/Prototypes/Entities/Mobs/Player/clone.yml`, `docs/upstream-sync/inventory-wave-0001.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static comparison confirms `Thieving` sits in the equivalent special-capability section and the component is registered in CMU. Prototype loading and a component-copy regression are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Cover cloning a thieving actor with both enabled and disabled stealth state to confirm shallow copying preserves the intended runtime flag and alert behavior.

## CS-0085 — Gate thrown melee knockback by use delay

- Upstream: [space-wizards/space-station-14#39018](https://github.com/space-wizards/space-station-14/pull/39018), `975ebac202deb696d1cfb9e6903e1eed62485786`, 2025-07-16
- Areas: Physics, Interactions
- Status: Ported
- Risk: Medium
- Behavior/API delta: `MeleeThrowOnHitComponent` now networks per-throw hit and cooldown state. A legal throw clears both flags, a collision records a hit, landing starts the use delay and closes the throw window, and throws attempted during that delay cannot apply knockback.
- RMC/CMU divergence: CMU retains the older boolean unanchor and stun APIs in the same system; those are deliberately preserved. The new state machine is otherwise the retained target-final implementation used by Mjollnir.
- Decision and rationale: Port the two-field lifecycle gate as one coordinated component/system change because either half alone would permit stale or unreplicated throw activation.
- Files changed: `Content.Shared/Weapons/Melee/Components/MeleeThrowOnHitComponent.cs`, `Content.Shared/Weapons/Melee/MeleeThrowOnHitSystem.cs`, `docs/upstream-sync/inventory-wave-0001.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static review traces clean throw, hit, land, and delayed rethrow paths and confirms both fields use delta-generated component state. Shared compilation and a Mjollnir throw-delay regression are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Test a clean miss, a hit followed by immediate rethrow, multiple collision events in one flight, and predicted client reconciliation of both flags.

## CS-0086 — Make admin ghosts and satchels explosion-proof

- Upstream: [space-wizards/space-station-14#38384](https://github.com/space-wizards/space-station-14/pull/38384), `2a496bf93f56b7fb3b765840ad5bc1112f9e2843`, 2025-07-16
- Areas: Physics
- Status: Ported
- Risk: Low
- Behavior/API delta: Admin ghosts now have zero-coefficient explosion resistance, and their loadout uses a dedicated administration satchel with the same resistance instead of the ordinary satchel of holding.
- RMC/CMU divergence: CMU's extra admin interaction, pull, and skill bypass components remain unchanged. The dedicated bag inherits the fork's existing holding-satchel storage behavior.
- Decision and rationale: Port all three retained prototype links together so the admin entity and its carried diagnostic contents survive the same explosion event.
- Files changed: `Resources/Prototypes/Entities/Clothing/Back/satchel.yml`, `Resources/Prototypes/Entities/Mobs/Player/admin_ghost.yml`, `Resources/Prototypes/Roles/Jobs/Fun/misc_startinggear.yml`, `docs/upstream-sync/inventory-wave-0001.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static prototype tracing confirms `MobAghostGear` resolves the new satchel and both entities have `damageCoefficient: 0`. Prototype loading and explosion-damage coverage are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Verify container contents inherit the intended blast protection semantics rather than only preserving the satchel entity itself.

## CS-0087 — Restore Plasma bar mail routing

- Upstream: [space-wizards/space-station-14#38098](https://github.com/space-wizards/space-station-14/pull/38098), `d8881ad4c6f2c880fd4234546c447ec8dc781b9a`, 2025-07-18
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: Plasma station's bar mailing unit now has both runtime `MailingUnit.tag` and persisted `Configuration.config.tag` set to `Bar`, allowing mail routing to recognize and retain that destination.
- RMC/CMU divergence: The exact map entity UID, coordinates, neighboring departmental units, and target-final component structure all match CMU, so the five-line map hunk applies without remapping fork entities.
- Decision and rationale: Port the retained target-final map override only after verifying UID `15708` still identifies the untagged bar mailing unit in CMU.
- Files changed: `Resources/Maps/plasma.yml`, `docs/upstream-sync/inventory-wave-0001.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static map comparison confirms UID `15708` now matches the pinned target-final `Bar` components while adjacent Botany and Security units are untouched. Map/prototype loading and routing behavior are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add a map-level assertion that every departmental mailing unit has matching runtime and persisted routing tags.

## CS-0088 — Localize generated name-identifier formats

- Upstream: [space-wizards/space-station-14#39035](https://github.com/space-wizards/space-station-14/pull/39035), `ffbc813179291286dda2dcfdfd58648f909ab1c2`, 2025-07-18
- Areas: Interactions
- Status: Ported
- Risk: Medium
- Behavior/API delta: `NameIdentifierGroupPrototype` replaces raw `Prefix` strings with optional `LocId Format` values. Fresh and restored identifiers now format through Fluent with `$number`, while prefixless groups continue returning the numeric value alone.
- RMC/CMU divergence: CMU's additional `Bounty` group is prefixless and remains compatible. No RMC-specific name-identifier group uses the removed field; the eight inherited formatted groups are migrated together.
- Decision and rationale: Port code, prototype schema, values, and locale keys atomically to avoid a load-time field mismatch or partially localized identifiers.
- Files changed: `Content.Server/NameIdentifier/NameIdentifierSystem.cs`, `Content.Shared/NameIdentifier/NameIdentifierGroupPrototype.cs`, `Resources/Locale/en-US/name-identifier.ftl`, `Resources/Prototypes/name_identifier_groups.yml`, `docs/upstream-sync/inventory-wave-0001.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static comparison confirms both generation paths use `Format`, every removed prefix has a matching Fluent key, and prefixless groups remain untouched. Shared/server compilation, prototype loading, and formatted/restored identifier cases are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add locale completeness coverage for every non-null name-identifier format and verify hot-reloaded prototypes rebuild identifier pools without changing existing names.

## CS-0089 — Stop recursive humanoid-profile equality

- Upstream: [space-wizards/space-station-14#39333](https://github.com/space-wizards/space-station-14/pull/39333), `e307fd69b0153f0172f77e5003c4446077236a6f`, 2025-08-02
- Areas: Gamerules
- Status: Ported
- Risk: Low
- Behavior/API delta: `HumanoidCharacterProfile` now has a typed equality overload that handles null, reference identity, and memberwise comparison. The object override delegates to it instead of recursively redispatching to itself for distinct profile instances.
- RMC/CMU divergence: CMU's `MemberwiseEquals` includes squad, rank, armor, named-item, perk, and xeno preference fields beyond upstream. The typed overload deliberately preserves that fork-specific comparison surface.
- Decision and rationale: Port the retained target-final overload exactly to eliminate stack exhaustion while continuing to treat every CMU preference field as part of profile equality.
- Files changed: `Content.Shared/Preferences/HumanoidCharacterProfile.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static call tracing confirms distinct profile instances reach `MemberwiseEquals` once, identical references return immediately, and non-profile objects return false. Shared compilation and equality cases are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add explicit tests for cloned equal profiles, each RMC-specific inequality field, null, same-reference, and unrelated-object comparisons, and verify hash/equality expectations remain aligned.

## CS-0090 — Guard bed cleanup for terminating occupants

- Upstream: [space-wizards/space-station-14#39410](https://github.com/space-wizards/space-station-14/pull/39410), `4a466c5dbe5885c9d80decadc290725581bff4e4`, 2025-08-06
- Areas: Medical, Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: Heal-on-buckle unstrapping no longer removes an action or wakes an occupant whose entity is already terminating, when those dependent components may have been removed. Bed healing state is still cleaned unconditionally.
- RMC/CMU divergence: RMC bed prototypes use the shared `HealOnBuckle` flow and have no overriding unstrap system, so the lifecycle guard applies without altering fork-specific medical values.
- Decision and rationale: Port the retained teardown guard at the action/sleep boundary while keeping bed-side cleanup outside the guard to avoid leaking `HealOnBuckleHealingComponent`.
- Files changed: `Content.Shared/Bed/SharedBedSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static control-flow review confirms terminating occupants skip entity mutations, normal occupants still lose the sleep action and wake, and the bed marker is always removed. Shared compilation and both unstrap paths are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add a teardown regression that deletes a buckled occupant during unstrap and a normal control proving action removal, wake-up, and bed cleanup.

## CS-0091 — Hide timer cycling for empty option lists

- Upstream: [space-wizards/space-station-14#39388](https://github.com/space-wizards/space-station-14/pull/39388), `96d25402c7ee9a5f10f60bd3dfb006815792a0a9`, 2025-08-05
- Areas: Interactions, GameTicking
- Status: Ported
- Risk: Low
- Behavior/API delta: Legacy on-use timer triggers now expose and execute delay cycling only when more than one option exists. Null, empty, and single-entry lists all return before verb construction or list indexing.
- RMC/CMU divergence: The pinned target uses the post-trigger-refactor non-null list, while CMU still permits nullable options. Both old-system guards therefore retain the null check and adopt target-final's `Count <= 1` semantics; RMC's configured three-option timers remain interactive.
- Decision and rationale: Apply the boundary check to both verb creation and `CycleDelay` so an empty list is safe even if cycling is invoked outside the verb path.
- Files changed: `Content.Server/Explosion/EntitySystems/TriggerSystem.OnUse.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static control-flow review confirms no empty list reaches sorting or index access, while lists with two or more options retain their existing cycle order. Server compilation and null/empty/single/multiple cases are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Preserve these semantics when the trigger refactor is integrated and add a prototype-lint warning for explicitly empty delay-option lists.

## CS-0092 — Remove the redundant cryopod item toggle

- Upstream: [space-wizards/space-station-14#39197](https://github.com/space-wizards/space-station-14/pull/39197), `a2c9612e29d270197bf9045d452a56e7c739f8b1`, 2025-08-05
- Areas: Medical, Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: The standard medical `CryoPod` no longer has `ItemToggleComponent`; cryogenic interaction remains owned by the pod system, eliminating a redundant toggle event path with order-dependent behavior.
- RMC/CMU divergence: RMC requisitions cryostorage and its atmos devices use separate prototypes and do not inherit the standard medical cryopod, so their intentional item-toggle behavior is untouched.
- Decision and rationale: Port the retained one-component deletion exactly rather than trying to coordinate two independent interaction owners on the same machine.
- Files changed: `Resources/Prototypes/Entities/Structures/Machines/Medical/cryo_pod.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static prototype review confirms only `ItemToggle` is removed and the cryopod's UI, health analyzer, cryogenic solution, and interaction components remain. Prototype loading and open/close interaction coverage are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add a regression that one interaction produces one cryopod state transition and verify no `ItemToggleComponent` is inherited through future parent changes.

## CS-0093 — Make the trading outpost anchor indestructible

- Upstream: [space-wizards/space-station-14#39389](https://github.com/space-wizards/space-station-14/pull/39389), `1599a6b2713ec8824d81a96c432ff4b59fa2a5c1`, 2025-08-05
- Areas: Physics
- Status: Ported
- Risk: Low
- Behavior/API delta: The Automated Trade Station's map anchor now uses `StationAnchorIndestructible`, preventing damage or machine teardown from disabling the fixed outpost grid.
- RMC/CMU divergence: CMU has the exact target-final map context at UID `887` and no RMC override for this standard shuttle map or anchor prototype.
- Decision and rationale: Port the retained one-prototype map substitution after verifying the UID, coordinates, and indestructible prototype all match the fork.
- Files changed: `Resources/Maps/Shuttles/trading_outpost.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static map tracing confirms only UID `887` changes prototype and remains at `7.5,-22.5` under the same map parent. Map/prototype loading and destruction resistance are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add a map invariant that static service outposts use indestructible anchors unless a destructible anchor is explicitly documented as gameplay.

## CS-0094 — Preserve pending MIDI note events when switching songs

- Upstream: [space-wizards/space-station-14#39335](https://github.com/space-wizards/space-station-14/pull/39335), `90f4f365dfa99209aa76b1d4b1daa737c80907d4`, 2025-08-02
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: Opening or switching a MIDI file no longer clears the instrument's pending event buffer before subscribing the new renderer, allowing already-generated note-off/reset events to reach remote listeners instead of leaving notes stuck.
- RMC/CMU divergence: CMU's instrument and network-event flow matches the retained target-final method. Buffer clears for explicit close, input open, and player-tick seeking remain unchanged because they represent different lifecycle boundaries.
- Decision and rationale: Port only the retained `OpenMidi` deletion so a song transition does not discard cleanup events while preserving intentional buffer resets elsewhere.
- Files changed: `Content.Client/Instruments/InstrumentSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static lifecycle review confirms song open retains queued events and attaches the new renderer, while full close still clears the buffer. Client compilation and a two-client song-switch/note-off regression are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Cover rapid file switching and verify the retained buffer drains without replaying stale note-on events after the renderer transition.

## CS-0095 — Default missing rotation visuals to vertical

- Upstream: [space-wizards/space-station-14#39338](https://github.com/space-wizards/space-station-14/pull/39338), `615f63e13bb03f14befba9866169d9e4958cf28e`, 2025-08-02
- Areas: Movement
- Status: Ported
- Risk: Low
- Behavior/API delta: When appearance state lacks `RotationVisuals.RotationState`, the client visualizer now applies the normal vertical state rather than leaving the sprite's previous rotation and offset untouched.
- RMC/CMU divergence: RMC movement and downed-state systems still publish explicit rotation states during live play. The fallback only covers missing or older appearance data, notably replay frames, and does not change authoritative movement state.
- Decision and rationale: Port the retained one-line fallback so absent data produces the component's neutral visual orientation instead of stale horizontal presentation.
- Files changed: `Content.Client/Rotation/RotationVisualizerSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static branch review confirms missing state enters the existing `Vertical` switch case while explicit horizontal and vertical values remain unchanged. Client compilation and replay/live appearance cases are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add replay coverage with an old frame lacking rotation state and verify later explicit horizontal state still overrides the fallback.

## CS-0096 — Localize the store refund control

- Upstream: [space-wizards/space-station-14#39346](https://github.com/space-wizards/space-station-14/pull/39346), `819e342a4f4dfef0953ebfc76a030a196765ce0c`, 2025-08-03
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: The store refund button now resolves `store-ui-refund-text` through Fluent instead of embedding English in XAML.
- RMC/CMU divergence: RMC stores reuse the inherited `StoreMenu`; no fork-specific label or refund behavior is replaced.
- Decision and rationale: Port the retained XAML/key pair atomically so every locale can translate the interaction without changing its layout or message handling.
- Files changed: `Content.Client/Store/Ui/StoreMenu.xaml`, `Resources/Locale/en-US/store/store.ftl`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static XAML and locale comparison confirms the key exists and matches the pinned target-final binding. Client compilation and locale/XAML loading are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Audit the adjacent hardcoded search placeholder in the later store-localization wave rather than expanding this focused port.

## CS-0097 — Correct protected-grid footprint checks

- Upstream: [space-wizards/space-station-14#39271](https://github.com/space-wizards/space-station-14/pull/39271), `9be68a6846f6c529e39ce0e51d6d15d107f892c1`, 2025-07-29
- Areas: Interactions, Physics
- Status: Ported
- Risk: Low
- Behavior/API delta: Floor edits on protected grids are now cancelled when the requested tile is absent from the captured initial-footprint bitmask, rather than cancelling edits to tiles that are inside it.
- RMC/CMU divergence: The shared protected-grid system has no RMC override. CMU arrivals and emergency shuttle setup use it directly, so both inherit the corrected boundary semantics.
- Decision and rationale: Port the retained missing negation exactly; the surrounding chunk lookup already rejects chunks outside the initial grid.
- Files changed: `Content.Shared/Tiles/ProtectedGridSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static truth-table review confirms initial footprint tiles pass and missing tiles/chunks cancel. Shared compilation plus inside/outside tile-edit cases are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add coverage for negative chunk coordinates and grids whose initial footprint has holes so bitmask translation remains correct.

## CS-0098 — Preserve tile orientation when variantizing grids

- Upstream: [space-wizards/space-station-14#39314](https://github.com/space-wizards/space-station-14/pull/39314), `392f4ea8f6080fed9cd5af76ed3de529263ed7f6`, 2025-08-01
- Areas: Physics
- Status: Ported
- Risk: Low
- Behavior/API delta: The `variantize` mapping command now carries each tile's existing rotation and mirroring into the replacement tile while randomizing only its sprite variant.
- RMC/CMU divergence: CMU uses the inherited command and tile representation without an RMC override, so fork-specific grids retain their authored orientation metadata.
- Decision and rationale: Port the retained constructor argument exactly; dropping `RotationMirroring` could visibly rotate or mirror directional tiles across an entire mapped grid.
- Files changed: `Content.Server/Administration/Commands/VariantizeCommand.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static constructor comparison confirms type, flags, and randomized variant remain unchanged while rotation/mirroring is copied from the source tile. Server compilation and a rotated/mirrored mapping-command case are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add command coverage that variant IDs can change without altering any tile's rotation/mirroring bits.

## CS-0099 — Avoid adding sleeping state during prediction reset

- Upstream: [space-wizards/space-station-14#39061](https://github.com/space-wizards/space-station-14/pull/39061), `1afb37669d4c508f10aec73d496453314cc79178`, 2025-07-24
- Areas: Medical, GameTicking
- Status: Ported
- Risk: Low
- Behavior/API delta: Applying a forced-sleep status effect during authoritative state restoration no longer calls `TrySleeping` and adds a fresh `SleepingComponent`; ordinary status-effect application still starts sleep.
- RMC/CMU divergence: CMU's sleeping system adds pain-numbness event ordering and retains both legacy and newer status-effect systems. The guard is confined to the shared forced-sleep callback and preserves those fork-specific paths.
- Decision and rationale: Port the retained `IGameTiming.ApplyingState` guard exactly so prediction reset remains state application rather than triggering new gameplay mutations.
- Files changed: `Content.Shared/Bed/Sleep/SleepingSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static control-flow review confirms only applying-state callbacks skip `TrySleeping`; normal forced sleep and CMU's surrounding event subscriptions are unchanged. Shared compilation plus predicted-reset and ordinary forced-sleep cases are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add a prediction-reset regression proving restoration neither creates a sleeping component nor emits duplicate sleep-state side effects.

## CS-0100 — Stop last-words callbacks for deleted mobs

- Upstream: [space-wizards/space-station-14#39245](https://github.com/space-wizards/space-station-14/pull/39245), `901cef43c96ce97c0ab6a43e312a8cb4fb619473`, 2025-07-28
- Areas: Medical, Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: The asynchronous critical-state last-words callback now exits when its mob was gibbed or deleted while the quick dialog was open, before reading critical state or attempting chat and ghost commands on that entity.
- RMC/CMU divergence: CMU retains the inherited critical-mob action flow and has no RMC override at this callback; RMC-specific death and gib paths can therefore hit the same delayed-dialog lifecycle race.
- Decision and rationale: Port the retained deletion guard at the start of the callback so all subsequent entity-dependent operations share one safe lifecycle boundary.
- Files changed: `Content.Server/Mobs/CritMobActionsSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static callback tracing confirms deleted mobs return before component lookup, speech, or ghosting, while attached critical mobs retain the exact last-words flow. Server compilation plus delete/gib-before-submit and normal-submit cases are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add an asynchronous regression that opens the dialog, deletes the mob, then submits without errors or emitted chat.

## CS-0101 — Allocate tabletop worlds on a correct Ulam spiral

- Upstream: [space-wizards/space-station-14#39327](https://github.com/space-wizards/space-station-14/pull/39327), `c376e695184ec53f3d0a7e0966aad1bfa2eee013`, 2025-08-01
- Areas: Physics
- Status: Ported
- Risk: Low
- Behavior/API delta: Tabletop grids now start at the one-based entry required by the Ulam mapping and calculate each ring by dividing before applying `Ceiling`; successive minigames therefore receive distinct, correctly spaced coordinates.
- RMC/CMU divergence: CMU retains the inherited isolated tabletop map and round-cleanup counter. No RMC tabletop placement override exists, so only the flawed coordinate allocator changes.
- Decision and rationale: Port both retained corrections atomically because fixing either the initial index or the arithmetic precedence alone does not restore the intended spiral sequence.
- Files changed: `Content.Server/Tabletop/TabletopSystem.Map.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static evaluation of the initial sequence confirms one-based inputs and distinct positions separated by `TabletopSeparation`; map creation and round-reset behavior remain unchanged. Server compilation plus multi-tabletop placement and reset cases are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add a deterministic sequence test covering enough values to cross several spiral corners and assert no duplicate coordinates.

## CS-0102 — Return the removed reagent volume

- Upstream: [space-wizards/space-station-14#39266](https://github.com/space-wizards/space-station-14/pull/39266), `d4e77423caf57cc8e3bf34ef4912bc4a467e6c66`, 2025-07-29
- Areas: Chemistry
- Status: Ported
- Risk: Low
- Behavior/API delta: All `SharedSolutionContainerSystem.RemoveReagent` overloads now return the exact `FixedPoint2` volume removed instead of reducing the result to a Boolean; unsuccessful removal returns zero and still avoids unnecessary chemical updates.
- RMC/CMU divergence: RMC has additional medical, xeno, refill, and repair callers of these overloads, but all currently discard the return value. Their behavior is unchanged while future callers can distinguish full, partial, and zero removal.
- Decision and rationale: Port the retained target-final API as a unit across all three overloads so the wrapper preserves the underlying `Solution.RemoveReagent` quantity without inconsistent signatures.
- Files changed: `Content.Shared/Chemistry/EntitySystems/SharedSolutionContainerSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static usage review confirms no CMU/RMC caller consumes these overloads as Boolean, and the success path returns the same quantity used to decide whether `UpdateChemicals` runs. Shared compilation and zero/partial/full removal cases are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Migrate quantity-sensitive RMC consumers to use the returned amount where accounting currently assumes the requested volume was available.

## CS-0103 — Include entity-effect threshold boundaries

- Upstream: [space-wizards/space-station-14#36289](https://github.com/space-wizards/space-station-14/pull/36289), `f6475bd26419cd46a7eb3fe553ac0262f15f2909`, 2025-07-30
- Areas: Medical, Chemistry, Interactions
- Status: Ported
- Risk: Medium
- Behavior/API delta: Entity-effect conditions for entity temperature, solution temperature, total damage, and hunger now accept values exactly equal to configured minimum or maximum thresholds. Missing required components or reagent sources still fail the condition.
- RMC/CMU divergence: RMC adds many reagent and medical effects that reuse these shared predicates. Their configured endpoints now behave as inclusive limits, matching guidebook wording and pinned target semantics, without changing the fork's threshold values.
- Decision and rationale: Port all four retained comparisons together so the same min/max contract applies across effect types rather than leaving chemically equivalent conditions inconsistent.
- Files changed: `Content.Server/EntityEffects/EntityEffectSystem.cs`, `Content.Shared/EntityEffects/EffectConditions/SolutionTemperature.cs`, `Content.Shared/EntityEffects/EffectConditions/TotalDamage.cs`, `Content.Shared/EntityEffects/EffectConditions/TotalHunger.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static truth-table review confirms values below/above the interval fail, endpoints and interior values pass, and missing data still fails. Shared/server compilation plus endpoint cases for all four predicates and representative RMC reagent effects are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Audit RMC prototypes that may have compensated for the old exclusive behavior by offsetting min/max values, especially overdose and critical-health boundaries.

## CS-0104 — Serialize vending inventory entries in saved grids

- Upstream: [space-wizards/space-station-14#38406](https://github.com/space-wizards/space-station-14/pull/38406), `623ea3dd63ae2c1196c2723a9f3dbaec3e3ccf6b`, 2025-07-31
- Areas: Interactions, GameTicking
- Status: Ported
- Risk: Low
- Behavior/API delta: `VendingMachineInventoryEntry` is now a generated data definition with serialized `Type`, `ID`, and `Amount` fields, allowing runtime vending inventories to survive post-map-init grid serialization and reload.
- RMC/CMU divergence: RMC vending machines add custom inventories but use the inherited entry class and dictionaries. The metadata therefore covers fork-specific stock without changing its values or dispensing rules.
- Decision and rationale: Port the retained type and field annotations exactly; this is the minimum schema correction needed for save-grid persistence while preserving network serialization and constructors.
- Files changed: `Content.Shared/VendingMachines/VendingMachineComponent.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static schema review confirms all three persisted entry fields are data fields and the class is partial for source generation, while existing `Serializable` and `NetSerializable` contracts remain. Shared compilation plus save/reload of regular, contraband, and emagged stock are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add a post-init save-grid regression using an RMC vending machine with modified stock counts and verify exact inventory restoration.

## CS-0105 — Give DoAfter distance thresholds explicit null semantics

- Upstream: [space-wizards/space-station-14#39276](https://github.com/space-wizards/space-station-14/pull/39276), `c4016b97c5f4df1877ff63246a67a99af44a717c`, 2025-08-06
- Areas: Movement, Interactions, GameTicking
- Status: Ported
- Risk: Medium
- Behavior/API delta: New DoAfters now explicitly default to a 1.5-tile distance threshold. Setting `DistanceThreshold` to null truly disables the target/tool distance check instead of silently falling back to the interaction system's implicit range.
- RMC/CMU divergence: CMU adds `RangeCheck` for xeno plasma transfer and vehicle climbing. That fork switch remains around the target check, while RMC's explicit extended thresholds for retrieval, vehicles, and powerloaders remain unchanged.
- Decision and rationale: Adapt the retained target semantics without removing `RangeCheck`; a nullable field must distinguish default construction from an intentional no-distance-check request.
- Files changed: `Content.Shared/DoAfter/DoAfterArgs.cs`, `Content.Shared/DoAfter/SharedDoAfterSystem.Update.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static call-site review confirms existing RMC custom ranges remain explicit, `RangeCheck = false` paths still bypass target checks, and no current CMU caller explicitly assigns null. Shared compilation plus default, custom, null, target-only, used-item, and RMC range-disabled cases are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Reconcile the RMC `RangeCheck` Boolean with nullable threshold semantics in a dedicated DoAfter integration review so one mechanism eventually owns distance cancellation.

## CS-0106 — Reform Diona nymphs with safe adjacent placement

- Upstream: [space-wizards/space-station-14#39505](https://github.com/space-wizards/space-station-14/pull/39505), `3654fcf5ddb194aa749dd6ab9b324a8934e0f70f`, 2025-08-11
- Areas: Interactions, Physics
- Status: Ported
- Risk: Low
- Behavior/API delta: Completing a Diona reform now spawns the new body beside the nymph or drops it through the normal placement helper instead of placing it directly at the old transform coordinates, which can be invalid or inside a container.
- RMC/CMU divergence: CMU's reform path uses a divergent stun call but otherwise retains upstream spawn and mind-transfer behavior. The stun behavior is preserved; only final entity placement changes.
- Decision and rationale: Port the retained placement helper exactly because it already handles containment and nearby valid coordinates before the old entity is queued for deletion.
- Files changed: `Content.Shared/Species/Systems/ReformSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static lifecycle review confirms server-only spawning, mind transfer, and deletion ordering remain intact while placement is delegated to the shared safe helper. Shared compilation plus open-tile, obstructed-tile, and contained-nymph reform cases are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add a regression proving reform from a container yields an accessible body and retains the same mind exactly once.

## CS-0107 — Audit battery breaker interactions

- Upstream: [space-wizards/space-station-14#39208](https://github.com/space-wizards/space-station-14/pull/39208), `ff7713eceaac2b9439528643f41c69ce4c243a8d`, 2025-07-25
- Areas: Interactions, Physics
- Status: Ported
- Risk: Low
- Behavior/API delta: Changing SMES or substation input/output breakers through the battery UI now writes an action log containing the actor, requested Boolean state, and target machine.
- RMC/CMU divergence: RMC power machines use the inherited battery interface and network battery components. No fork-specific breaker handler is bypassed or replaced.
- Decision and rationale: Port the retained logs immediately after each authoritative state mutation so operational power changes are attributable without altering charge or discharge behavior.
- Files changed: `Content.Server/Power/EntitySystems/BatteryInterfaceSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static handler review confirms both breaker paths log after assigning their existing fields, while rate changes remain outside this upstream change. Server compilation and one input/one output toggle with actor/target log assertions are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Evaluate logging charge/discharge rate changes in the deeper power-interface audit, with throttling to avoid slider spam.

## CS-0108 — Audit entertainment-camera renames

- Upstream: [space-wizards/space-station-14#39239](https://github.com/space-wizards/space-station-14/pull/39239), `8fdfb9deaeb985731c2375d473a84f37fe0dfeaf`, 2025-07-27
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: A successful wireless entertainment-camera rename now records a low-impact chat log containing the actor, camera entity, and accepted name.
- RMC/CMU divergence: CMU uses the inherited surveillance-camera setup message and validation flow, with no RMC rename handler to reconcile.
- Decision and rationale: Port the retained log after validation and interface-state update so rejected or malformed names do not create misleading audit events.
- Files changed: `Content.Server/SurveillanceCamera/Systems/SurveillanceCameraSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static handler review confirms only accepted names within the existing limit are logged and camera/network behavior is untouched. Server compilation plus accepted, empty, and overlength rename cases are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Review the retained punctuation inside the quoted name and add equivalent audit coverage for camera network changes if administrators need full configuration history.

## CS-0109 — Disable lock verbs rejected by attempt events

- Upstream: [space-wizards/space-station-14#39605](https://github.com/space-wizards/space-station-14/pull/39605), `99ad34ed06985e665bb24fcc1fc9d92eece1fa1b`, 2025-08-13
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: Lock and unlock alternative verbs now render disabled when `CanToggleLock` rejects the user or target state, instead of presenting an active control that can only fail when invoked.
- RMC/CMU divergence: RMC pre-hijack lockers cancel `LockToggleAttemptEvent`; the quiet capability check now reflects that rule in the verb UI while preserving the fork's popup on an actual non-silent attempt.
- Decision and rationale: Port the retained disabled predicate rather than hiding the verb, preserving discoverability while accurately representing authoritative interaction availability.
- Files changed: `Content.Shared/Lock/LockSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static event tracing confirms verb construction raises the quiet attempt checks, RMC cancellation disables the verb without a popup, and enabled verbs still invoke existing lock/unlock paths. Shared compilation plus allowed, action-blocked, and pre-hijack-locker cases are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add predicted verb-state coverage and verify components with stateful attempt handlers do not mutate during quiet capability checks.
