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
