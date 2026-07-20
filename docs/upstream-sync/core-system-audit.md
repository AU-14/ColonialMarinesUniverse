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
- For every merge commit, inspect and classify its effective first-parent tree delta with `git diff <merge>^1 <merge>`; default merge display is not evidence that the merge is empty.
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

## CS-0110 — Mutate the correct plant gas dictionaries

- Upstream: [space-wizards/space-station-14#39688](https://github.com/space-wizards/space-station-14/pull/39688), `201bc6cc5ce8b6132f663c501a96866478acf26b`, 2025-08-17
- Areas: Chemistry, Physics
- Status: Ported
- Risk: Low
- Behavior/API delta: `PlantMutateConsumeGasses` now changes a seed's `ConsumeGasses` values, while `PlantMutateExudeGasses` changes `ExudeGasses`; the old handlers applied each effect to the opposite dictionary.
- RMC/CMU divergence: No RMC entity effect or seed implementation overrides these inherited mutation handlers. Fork-specific plants retain their gas values and receive the corrected mutation direction.
- Decision and rationale: Port the retained two-reference swap atomically so effect names, prototype intent, and modified runtime state agree.
- Files changed: `Content.Server/EntityEffects/EntityEffectSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static handler comparison confirms both mutation algorithms and random ranges are unchanged and only their selected dictionaries are corrected. Server compilation plus distinct consume/exude dictionary mutation cases are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add deterministic tests with seeded randomness proving each effect mutates only its namesake dictionary.

## CS-0111 — Throttle bananium horn activation

- Upstream: [space-wizards/space-station-14#39674](https://github.com/space-wizards/space-station-14/pull/39674), `01f4f0cf1492a80eee16dfb88a09078b7c49729f`, 2025-08-17
- Areas: Interactions, GameTicking
- Status: Ported
- Risk: Low
- Behavior/API delta: The bananium horn now has a three-second `UseDelay`, preventing immediate repeated activation while leaving its use, land, trigger, activate, collide, and melee sounds unchanged.
- RMC/CMU divergence: No RMC/CMU prototype overrides `BananiumHorn`; inherited clown equipment receives the same retained throttle.
- Decision and rationale: Port the isolated target-final component rather than modifying shared horn systems, keeping the cooldown specific to the unusually disruptive bananium horn.
- Files changed: `Resources/Prototypes/Entities/Objects/Fun/bike_horn.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static prototype review confirms the delay is scoped to `BananiumHorn` and ordinary bike horns are untouched. Prototype loading plus immediate repeat rejection and post-delay success are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Verify non-use sound triggers are intentionally outside `UseDelay`; if they remain spam vectors, handle them through their owning trigger systems rather than expanding this prototype port.

## CS-0112 — Guard toggled-clothing reinsertion by entity lifecycle

- Upstream: [space-wizards/space-station-14#39191](https://github.com/space-wizards/space-station-14/pull/39191), `8034cabbaeed60f7f476d1be193c5d476eac8309`, 2025-08-17
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: When attached clothing is unequipped during deletion, the handler now checks the owning toggleable entity's lifecycle and exits once it is beyond map initialization, avoiding access to teardown-state component lifecycle data and container reinsertion.
- RMC/CMU divergence: RMC hardsuits and attached helmets use this inherited system extensively but do not override the teardown callback, so the guard protects fork-specific equipment without changing normal equip behavior.
- Decision and rationale: Port the retained entity-lifecycle check at the exact reinsertion boundary; an entity-level query remains valid while its components are being dismantled.
- Files changed: `Content.Shared/Clothing/EntitySystems/ToggleableClothingSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static teardown tracing confirms applying-state and attached-component guards remain, terminating owners return before container insertion, and map-initialized owners retain existing behavior. Shared compilation plus delete-while-toggled and ordinary unequip/reinsert cases are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add a regression deleting an equipped RMC hardsuit with its helmet toggled and assert no lifecycle or container exception.

## CS-0113 — Allow temporarily unbound menu controls

- Upstream: [space-wizards/space-station-14#39732](https://github.com/space-wizards/space-station-14/pull/39732), `5a5b81f7dc8434a2ca5000cb3e3a4e031e56c4b2`, 2025-08-18
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: Top-menu buttons now accept a nullable bound-key function and display an empty shortcut label while unbound, preventing key-binding add/remove or input-mode refresh events from dereferencing a missing function.
- RMC/CMU divergence: RMC adds a language menu button through the same control and assigns a concrete key normally. It remains compatible while gaining safety during rebinding and transient control construction.
- Decision and rationale: Port the retained nullable property and all three label-refresh guards together so no update path can call `ShortKeyName` without a key.
- Files changed: `Content.Client/UserInterface/Controls/MenuButton.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static usage review confirms every current XAML/RMC caller may still assign a non-null key and no external code requires a non-null getter. Client compilation plus clear/rebind/input-mode changes with all top-menu buttons mounted are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add a focused control test that clears and restores `BoundKey` while binding notifications fire, asserting label text and absence of exceptions.

## CS-0114 — Raise loadout completion once after full equipment

- Upstream: [space-wizards/space-station-14#35067](https://github.com/space-wizards/space-station-14/pull/35067), `c59f7a53633f69eb091fe43aa05cda705e189257`, 2025-08-18
- Areas: Interactions, GameTicking
- Status: Ported
- Risk: Low
- Behavior/API delta: `LoadoutSystem` now suppresses the starting-gear helper's immediate `StartingGearEquippedEvent` and emits its own event once after starting gear and any role loadout are both applied, preventing duplicate post-equip side effects such as automatic internals handling.
- RMC/CMU divergence: RMC has additional starting-gear listeners and several callers that already explicitly suppress and later raise the event. This change brings the inherited `LoadoutComponent` path in line with that established fork pattern without altering custom callers.
- Decision and rationale: Port the retained `raiseEvent: false` argument at the nested helper call because `GearEquipped` is already the owning completion boundary for this composite loadout operation.
- Files changed: `Content.Shared/Clothing/LoadoutSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static event tracing confirms every branch through `Equip` reaches one `GearEquipped` call, while the starting-gear helper no longer emits an earlier duplicate. Shared compilation plus jetpack/internals, starting-gear-only, and combined role-loadout cases are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Audit all RMC `EquipStartingGear` callers for explicit ownership of the completion event and document which layer should raise it.

## CS-0115 — Upload active silicon laws instead of stale prototypes

- Upstream: [space-wizards/space-station-14#39756](https://github.com/space-wizards/space-station-14/pull/39756), `a26a18243f0bcbefdf75c830d38ec0183a38e43f`, 2025-08-19
- Areas: Interactions, Gamerules
- Status: Ported
- Risk: Low
- Behavior/API delta: Inserting a silicon law provider into an upload console now copies its active runtime `Lawset` when present, falling back to its configured lawset prototype only for an uninitialized provider.
- RMC/CMU divergence: CMU retains upstream silicon law providers and adds role interactions around subverted silicons. Runtime modifications such as ion, emag, or custom laws are now preserved through upload without changing those fork-specific role hooks.
- Decision and rationale: Port the retained null-coalescing selection at the upload boundary; `provider.Laws` identifies the default prototype, while `provider.Lawset` is the authoritative mutable state.
- Files changed: `Content.Server/Silicons/Laws/SiliconLawSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static data-flow review confirms initialized runtime laws reach every updater target and an uninitialized provider still resolves its prototype. Server compilation plus prototype, custom runtime, ion-modified, and emag-modified provider uploads are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add an upload regression that modifies a provider at runtime and verifies law order, text, obedience target, and notification sound on every connected silicon.

## CS-0116 — Prioritize cane-sheath item-slot verbs

- Upstream: [space-wizards/space-station-14#39795](https://github.com/space-wizards/space-station-14/pull/39795), `b124d0def58aea3fa16489c6b5bc85c3b1351095`, 2025-08-20
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: The cane sheath's blade slot now uses verb priority 3, allowing its insert/eject interaction to win ordering against lower-priority verbs when several actions are available.
- RMC/CMU divergence: CMU retains an older sheath parent/component layout but uses the same `ItemSlots` entry and cane-blade tag. Only that slot's priority is adapted; no newer voice-lock or slot-lock behavior is imported.
- Decision and rationale: Port the retained field at the existing slot definition so the interaction fix does not drag in unrelated prototype evolution.
- Files changed: `Resources/Prototypes/Entities/Objects/Weapons/Melee/cane.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static prototype review confirms whitelist, sounds, and item mapping are unchanged and only the blade slot receives priority 3. Prototype loading plus insert/eject ordering with competing verbs is queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Compare CMU's older cane-sheath parent set with target-final separately before importing voice-lock or item-slot-lock behavior.

## CS-0117 — Skip emergency-shuttle ticking in the lobby

- Upstream: [space-wizards/space-station-14#38732](https://github.com/space-wizards/space-station-14/pull/38732), `b317d7514f34c56a989c661668290857fdef6f57`, 2025-08-20
- Areas: GameTicking, Gamerules
- Status: Ported
- Risk: Low
- Behavior/API delta: `EmergencyShuttleSystem.Update` no longer advances emergency-console logic while the game ticker is in `PreRoundLobby`; normal round states continue updating it every server tick.
- RMC/CMU divergence: RMC uses different primary round objectives and shuttle content but retains the inherited emergency shuttle system and ticker run levels. The guard affects only pre-round execution and does not enable the system when its CVar is disabled.
- Decision and rationale: Port the retained run-level check at the outer update boundary so no console timer or associated emergency-shuttle work executes before round start.
- Files changed: `Content.Server/Shuttles/Systems/EmergencyShuttleSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static update tracing confirms `base.Update` still runs, only `PreRoundLobby` skips console work, and every other run level preserves the existing call. Server compilation plus idle-lobby, round-start, call, recall, and round-cleanup cases are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Review whether RMC's custom pre-round run levels or lobby maps require a broader `InRound` predicate when the game-ticker integration is audited deeply.

## CS-0118 — Preserve docking-port radar colors through the BUI

- Upstream: [space-wizards/space-station-14#38942](https://github.com/space-wizards/space-station-14/pull/38942), `9b6cb79fa2e2d554480c1f617529ad3a3d7b4484`, 2025-08-12
- Areas: Movement, Physics, Interactions
- Status: Ported
- Risk: Medium
- Behavior/API delta: `DockingPortState` now carries each docking component's normal and highlighted radar colors. The server populates both fields, and shuttle navigation/docking controls render them instead of fixed purple and magenta values.
- RMC/CMU divergence: RMC does not replace the shuttle BUI state or either inherited radar control. Custom docking prototypes can now expose their authored colors without changing docking geometry or authority.
- Decision and rationale: Port the retained serialized state and all producers/consumers atomically so clients never receive a partial color contract. A magenta fallback remains only when no viewed dock state exists.
- Files changed: `Content.Shared/Shuttles/BUIStates/DockingPortState.cs`, `Content.Server/Shuttles/Systems/ShuttleConsoleSystem.cs`, `Content.Client/Shuttles/UI/ShuttleDockControl.xaml.cs`, `Content.Client/Shuttles/UI/ShuttleNavControl.xaml.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static data-flow review confirms both component colors enter every generated dock state and each rendering branch consumes the intended field. Shared/server/client compilation plus BUI round-trip, normal, hovered, selected, nav-radar, and null-view fallback cases are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add a serialized BUI regression with non-default colors and visually verify color-space conversion remains consistent across both controls.

## CS-0119 — Keep grenade trigger sounds after source deletion

- Upstream: [space-wizards/space-station-14#39815](https://github.com/space-wizards/space-station-14/pull/39815), `d61ebf2c87547f7ed6fb2a6e0041d8ad8b5875aa`, 2025-08-28
- Areas: Shooting, Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: The eight projectile and scattering grenade prototypes that use `EmitSoundOnTrigger` now request positional playback, so their detonation sound survives deletion of the grenade entity and remains anchored to the trigger coordinates.
- RMC/CMU divergence: Four other affected upstream grenade families still use CMU's older `SoundOnTrigger`, which already calls positional PVS playback and has no `positional` data field. Those prototypes were deliberately left unchanged.
- Decision and rationale: Port only the retained data flags supported by CMU's current shared emit-sound component. This fixes silent detonations without migrating the fork's broader trigger architecture.
- Files changed: `Resources/Prototypes/Entities/Objects/Weapons/Throwable/projectile_grenades.yml`, `Resources/Prototypes/Entities/Objects/Weapons/Throwable/scattering_grenades.yml`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static prototype review confirms exactly three projectile-grenade and five scattering-grenade `EmitSoundOnTrigger` components gained the flag, while all legacy `SoundOnTrigger` users remain untouched. Prototype loading plus one-shot spatial playback after source deletion are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Reconcile the fork's old and keyed trigger sound components as one migration before adopting later trigger-key-dependent grenade changes.

## CS-0120 — Empty reagent dispensers during deconstruction

- Upstream: [space-wizards/space-station-14#39676](https://github.com/space-wizards/space-station-14/pull/39676), `2ebdd9d4cd04a5fdfd671db5e3ff05e52b3c8976`, 2025-09-02
- Areas: Chemistry, Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: `ReagentDispenserBase` now empties both its bulk `storagebase` container and inserted `beakerSlot` when the machine is deconstructed, instead of deleting or trapping their contents with the machine entity.
- RMC/CMU divergence: CMU retains the same two dispenser container identifiers and already has the shared machine-deconstruction emptying system, so no fork-specific code path was replaced.
- Decision and rationale: Add the retained component wiring at the common dispenser base so all inheriting chemical dispensers receive consistent deconstruction behavior.
- Files changed: `Resources/Prototypes/Entities/Structures/Dispensers/base_structuredispensers.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static inheritance and container-ID review confirms both named containers exist on the base and the component/system contract is already used by other CMU machines. Prototype loading plus deconstruction with stored bottles and an inserted beaker are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Audit RMC-specific dispenser descendants for extra private containers that should also be emptied.

## CS-0121 — Prevent forensic-scan verbs from creating evidence

- Upstream: [space-wizards/space-station-14#39964](https://github.com/space-wizards/space-station-14/pull/39964), `6a22ee7d39be79f9929dde64e1e66b847ca6d640`, 2025-09-05
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: The forensic scanner's utility verb explicitly disables the generic contact-interaction callback, preventing a scan from depositing the user's fingerprints or fibers on the scanned object.
- RMC/CMU divergence: CMU retains the same server scanner verb and shared verb execution default, so the upstream one-property fix applies without changing RMC forensic data or scan timing.
- Decision and rationale: Mark only the observational verb as non-contact. Direct interaction paths and all other utility verbs preserve their existing contact semantics.
- Files changed: `Content.Server/Forensics/Systems/ForensicScannerSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static execution tracing confirms `SharedVerbSystem` skips `DoContactInteraction` when the nullable override is false while still invoking the scan action. Server compilation plus verb-scan evidence preservation and direct-contact controls are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add a forensic regression that compares evidence before and after verb scanning to protect this interaction contract.

## CS-0122 — Stop derelict cyborg ghost-role duplication

- Upstream: [space-wizards/space-station-14#39992](https://github.com/space-wizards/space-station-14/pull/39992), `c7a10e8bce0d80db8a0ae480b4aa5ef4b2df63a0`, 2025-09-06
- Areas: Gamerules
- Status: Ported
- Risk: Low
- Behavior/API delta: The retained derelict cyborg ghost role no longer re-registers after being taken, preventing duplicate ghost-role listings for the same cyborg entity.
- RMC/CMU divergence: Upstream later defines several derelict cyborg variants; current CMU has one matching ghost-role prototype. RMC already uses `reregister: false` broadly for one-shot event roles, confirming the schema and intended lifecycle.
- Decision and rationale: Apply the lifecycle flag only to CMU's existing `PlayerBorgDerelictGhostRole`; absent upstream variants are not introduced as part of this bug fix.
- Files changed: `Resources/Prototypes/Entities/Mobs/Player/silicon.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static prototype review confirms the flag is nested under the existing `GhostRole` component and leaves raffle settings and takeover availability unchanged. Prototype loading plus take/release/deletion ghost-role lifecycle checks are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: When importing later derelict cyborg variants, preserve this one-shot registration contract on every variant.

## CS-0123 — Play clown-bag insertion sounds

- Upstream: [space-wizards/space-station-14#39931](https://github.com/space-wizards/space-station-14/pull/39931), `9e22aa4cd5c05d2cabdfb79cc1c1b2b22660cfe8`, 2025-09-06
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: Clown backpacks, duffels, and satchels now play the clown-footstep collection when an item is inserted. Their existing bike-horn open sound and new insertion sound both use small pitch variation.
- RMC/CMU divergence: The three inherited station prototypes and storage audio fields are unchanged by RMC; CMU-specific bags are unaffected.
- Decision and rationale: Port the complete retained prototype delta across all three clown bag families so storage interactions remain consistent.
- Files changed: `Resources/Prototypes/Entities/Clothing/Back/backpacks.yml`, `Resources/Prototypes/Entities/Clothing/Back/duffel.yml`, `Resources/Prototypes/Entities/Clothing/Back/satchel.yml`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static inheritance review confirms only the three clown variants override these sounds and the `FootstepClown` collection already exists. Prototype loading plus open/insert playback and mime-bag silence controls are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: None.

## CS-0124 — Prefer military-boot item-slot interactions

- Upstream: [space-wizards/space-station-14#40049](https://github.com/space-wizards/space-station-14/pull/40049), `817a2973e57d745655a86883409ebe85a2bb7265`, 2025-09-08
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: The knife/sidearm slot inherited by military boots now has interaction priority 4, ensuring slot insertion and removal wins over lower-priority species or clothing interactions.
- RMC/CMU divergence: RMC adds species and footwear interactions, including moth behavior, but retains this upstream military-boot base and item-slot priority contract.
- Decision and rationale: Port the retained one-field ordering fix at the shared base so every military-boot descendant resolves the same ambiguity.
- Files changed: `Resources/Prototypes/Entities/Clothing/Shoes/base_clothingshoes.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static prototype inheritance review confirms the priority applies only to the named boot item slot and does not alter its knife/sidearm whitelist. Prototype loading plus human and moth insertion/removal interaction selection are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Audit RMC footwear with additional item slots for explicit priority collisions.

## CS-0125 — Scale topical self-treatment from patient damage

- Upstream: [space-wizards/space-station-14#39883](https://github.com/space-wizards/space-station-14/pull/39883), `e8320cc9d8c96eda04420e74b26b1b4802b8633c`, 2025-09-01
- Areas: Medical, Interactions, GameTicking
- Status: Ported
- Risk: Medium
- Behavior/API delta: Topical healing delays are now stored as `TimeSpan`. Self-treatment calculates its penalty from the patient's damage and configured multiplier instead of querying the healing item, updates the delay between repeated applications as damage falls, and always shows the completion popup when repetition ends.
- RMC/CMU divergence: RMC's separate wound-treatment system and skill multipliers do not use `HealingComponent`; the station topical path remains structurally compatible and no RMC-specific treatment timing was replaced.
- Decision and rationale: Port the complete retained timing fix atomically because correcting only the initial calculation would leave repeat applications stale, while correcting only the helper would preserve the accidental base-delay squaring fallback.
- Files changed: `Content.Shared/Medical/Healing/HealingComponent.cs`, `Content.Shared/Medical/Healing/HealingSystem.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static control-flow review confirms other-target treatment retains the base delay, self-treatment uses patient damage, repeats recalculate after each heal, and terminal/depleted-stack paths show completion feedback. Shared compilation plus zero/partial/critical damage, self/other, repeat, depleted-stack, movement-cancel, and prediction reconciliation cases are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add focused healing-system tests for the computed multiplier and repeated DoAfter delay mutation before deeper integration with RMC wound treatment.

## CS-0126 — Treat Diona sap as artifact blood

- Upstream: [space-wizards/space-station-14#40211](https://github.com/space-wizards/space-station-14/pull/40211), `905935e6edb61311db105bb195fe6872f9804cc5`, 2025-09-15
- Areas: Chemistry, Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: Xenoartifact blood-reactive nodes now include the `Sap` reagent, allowing Diona sap contact to satisfy the same artifact trigger as other species blood reagents.
- RMC/CMU divergence: CMU retains both the Sap reagent and upstream xenoartifact trigger table without fork-specific overrides.
- Decision and rationale: Add the retained reagent identifier to the existing whitelist; no reaction quantities or other blood chemistry are changed.
- Files changed: `Resources/Prototypes/XenoArch/triggers.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static prototype review confirms `Sap` exists and is added only to `TriggerBlood`. Prototype loading plus positive Sap and negative non-blood reagent artifact activation cases are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Audit RMC-specific blood substitutes against this trigger list during the deeper chemistry pass.

## CS-0127 — Restore role identity before loadout validation

- Upstream: [space-wizards/space-station-14#40263](https://github.com/space-wizards/space-station-14/pull/40263), `1666e302c29b2700e7a6bf91b00e371ce8c3159b`, 2025-09-18
- Areas: Gamerules
- Status: Ported
- Risk: Low
- Behavior/API delta: Character-profile validation now resets each `RoleLoadout.Role` value from its already-validated dictionary key before validating the loadout contents, repairing stale or mismatched role identity loaded from persistence.
- RMC/CMU divergence: CMU extends character profiles with ranks, named items, armor, and Xeno preferences, but retains the upstream loadout dictionary and validation loop unchanged.
- Decision and rationale: Port only the retained semantic line from the stable merge and omit its unrelated food-sequence whitespace change. The dictionary key is the authoritative role selected by profile deserialization and prototype validation.
- Files changed: `Content.Shared/Preferences/HumanoidCharacterProfile.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static data-flow review confirms invalid dictionary keys are still removed first and valid loadouts receive matching role identity before `EnsureValid`. Shared compilation plus mismatched, valid, and missing-role loadout profiles are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add a profile deserialization regression where the dictionary key and embedded role differ.

## CS-0128 — Cancel DoAfters across inaccessible containers

- Upstream: [space-wizards/space-station-14#39880](https://github.com/space-wizards/space-station-14/pull/39880), `327f217e18925d03f72b898e038592cdd548da95`, 2025-09-18
- Areas: Movement, Physics, Interactions, GameTicking
- Status: Ported
- Risk: Medium
- Behavior/API delta: Ongoing target-based DoAfters now require the target to remain both in range and accessible, so placing either participant behind an incompatible container boundary cancels the action instead of allowing it to complete through storage.
- RMC/CMU divergence: RMC adds the per-DoAfter `RangeCheck` escape hatch. That outer guard is preserved exactly; only enabled target checks use the stronger shared accessibility predicate.
- Decision and rationale: Replace the target predicate at the existing distance-check seam while leaving movement thresholds, tool-range checks, lag compensation, and RMC opt-outs unchanged.
- Files changed: `Content.Shared/DoAfter/SharedDoAfterSystem.Update.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static cancellation-path review confirms `RangeCheck = false` still bypasses the target check and enabled checks now account for container accessibility. Shared compilation plus same-container, nested-accessible, sealed-container, out-of-range, and RMC opt-out cases are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Extend DoAfter regression coverage for container transitions during prediction and reconciliation.

## CS-0129 — Reset hidden clothing layers in both fold states

- Upstream: [space-wizards/space-station-14#40251](https://github.com/space-wizards/space-station-14/pull/40251), `c7406f65abfbd068403130f2da6148e22d2757e2`, 2025-09-17
- Areas: Interactions
- Status: Ported
- Risk: Medium
- Behavior/API delta: Foldable clothing now updates hidden inventory layers whenever either its folded or unfolded layer set is configured. An empty set for the destination state explicitly clears the prior state, fixing stale body-part hiding in both fold directions.
- RMC/CMU divergence: RMC already added the null reset and dirtying path when unfolding, but the folding path still retained stale layers. The adaptation preserves RMC's nullable reset and replication call in both branches while adopting upstream's combined activation condition.
- Decision and rationale: Reconcile the two implementations into symmetric destination-state assignment rather than replacing the RMC branch with upstream's non-null-only assignment.
- Files changed: `Content.Shared/Clothing/EntitySystems/FoldableClothingSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static state-transition review covers nonempty-to-empty and empty-to-nonempty layer sets in both directions and confirms `Dirty` follows every mutation. Shared compilation plus fold/unfold appearance, no-config, equipped-cancellation, and prediction reconciliation cases are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Replace the component-to-component hidden-layer override with an event or saved previous state during the deeper clothing interaction audit.

## CS-0130 — Respect pause state in APC battery receivers

- Upstream: [space-wizards/space-station-14#40188](https://github.com/space-wizards/space-station-14/pull/40188), `499dde1ec1b43c2cb52468200e2493b0adfc2ef0`, 2025-09-07
- Areas: Physics, GameTicking
- Status: Ported
- Risk: Medium
- Behavior/API delta: APC receivers with internal batteries now stop before changing their requested load, charge, enabled state, appearance, or power events while their entity is paused. Toggling `NeedsPower` no longer forces a redundant `PowerChangedEvent` when the computed powered state is unchanged.
- RMC/CMU divergence: CMU excludes RMC power receivers from this upstream loop. That fork-specific early exit remains first, while the pause guard is added only to the retained station APC-battery path.
- Decision and rationale: Port the target-final pause ordering and remove the obsolete recalculation flag together. Checking pause after battery mutation allowed paused maps and containers to advance power state, while keeping the flag would preserve duplicate events upstream deliberately removed.
- Files changed: `Content.Server/Power/Components/ApcPowerReceiverComponent.cs`, `Content.Server/Power/EntitySystems/PowerNetSystem.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static control-flow review confirms RMC receivers remain excluded, internal-battery receivers check pause before all mutation, and ordinary receivers retain the existing pre-event pause check. Server compilation plus paused/unpaused battery discharge, recharge, enabled-state, and unchanged-`NeedsPower` event cases are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add a focused power-net regression that pauses an internally powered receiver across multiple updates and asserts charge, load, appearance, and event counts remain stable.

## CS-0131 — Correct the nuclear operative medic title

- Upstream: [space-wizards/space-station-14#40055](https://github.com/space-wizards/space-station-14/pull/40055), `05a4e6d00cd9e1794caded0f1c402d098f492219`, 2025-09-01
- Areas: Gamerules
- Status: Ported
- Risk: Low
- Behavior/API delta: The `NukeopsMedic` round-start metadata now formats its generated title as `Corpsman` instead of the generic `Agent` title.
- RMC/CMU divergence: CMU retains the upstream nuclear-operative medic prototype and metadata format key unchanged, so the localization-only correction applies without adapting RMC roles.
- Decision and rationale: Port the retained one-line title correction because the role-specific format already exists and no gameplay permissions or loadout behavior changes.
- Files changed: `Resources/Locale/en-US/random-metadata/random-metadata-formats.ftl` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static reference review confirms only the nuclear-operative medic uses this format key. Localization loading and resolved round-start metadata are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: None.

## CS-0132 — Prevent overlapping wall-light placement

- Upstream: [space-wizards/space-station-14#39939](https://github.com/space-wizards/space-station-14/pull/39939), `e8583da476ee50b77264c6892c1cb84112c772b0`, 2025-09-01
- Areas: Interactions, Physics
- Status: Ported
- Risk: Low
- Behavior/API delta: Always-powered wall lights and their inherited variants now share the `lights` placement-replacement key, preventing map placement from stacking multiple fixtures in the same wall-mounted location.
- RMC/CMU divergence: RMC uses the same placement-replacement component for its structures and retains the upstream wall-light prototype hierarchy, so no collision masks or RMC light behavior are changed.
- Decision and rationale: Add the retained prototype component at the shared base so empty, powered, and colored inherited fixtures receive the same editor placement rule without duplicating data.
- Files changed: `Resources/Prototypes/Entities/Structures/Lighting/base_lighting.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static inheritance review confirms the key reaches wall-light variants while leaving runtime collision and construction unchanged. Prototype loading and overlapping map-editor placement are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: None.

## CS-0133 — Correct the Inspector revolver action description

- Upstream: [space-wizards/space-station-14#40072](https://github.com/space-wizards/space-station-14/pull/40072), `103c3983df4631fa8c57d22974342211e4f5ce7d`, 2025-09-03
- Areas: Shooting
- Status: Ported
- Risk: Low
- Behavior/API delta: The Inspector revolver description now correctly identifies its existing double-action behavior instead of claiming it is single-action.
- RMC/CMU divergence: CMU retains this upstream weapon prototype and firing behavior unchanged; the correction does not touch RMC weapon balance, ammunition, or fire modes.
- Decision and rationale: Port the retained description-only correction so player-facing weapon information matches the implementation.
- Files changed: `Resources/Prototypes/Entities/Objects/Weapons/Guns/Revolvers/revolvers.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static prototype review confirms the Inspector is the sole changed entity and no component data changes. Prototype loading and resolved description text are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: None.

## CS-0134 — Stabilize gas-analyzer precision

- Upstream: [space-wizards/space-station-14#40081](https://github.com/space-wizards/space-station-14/pull/40081), `893f4f14036b34505c47bff43f287a19ab4a4d67`, 2025-09-03
- Areas: Chemistry
- Status: Ported
- Risk: Low
- Behavior/API delta: Gas-analyzer pressure now always displays two decimal places and Kelvin/Celsius temperatures always display one, preventing values in the same readout from shifting precision as they change.
- RMC/CMU divergence: The client gas-analyzer window is unchanged by RMC and consumes the same gas-mix state, so only presentation precision changes; atmospheric calculations and thresholds remain untouched.
- Decision and rationale: Port the target-final formatting strings exactly while retaining optional precision for volume, gas amount, and percentage fields that upstream did not change.
- Files changed: `Content.Client/Atmos/UI/GasAnalyzerWindow.xaml.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static review confirms only pressure and temperature display formats changed. Client compilation plus zero, fractional, and negative-Celsius display cases are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Consider centralizing atmos display precision when the broader analyzer and air-alarm UI migration is handled.

## CS-0135 — Make the golden knuckledusters objective selectable

- Upstream: [space-wizards/space-station-14#40096](https://github.com/space-wizards/space-station-14/pull/40096), `348f462b122cfb6cb91be19d0cfa3b533bec9ce3`, 2025-09-04
- Areas: Gamerules
- Status: Ported
- Risk: Low
- Behavior/API delta: The existing Quartermaster golden-knuckledusters theft objective now participates in the traitor steal-objective weighted group instead of being defined but unreachable.
- RMC/CMU divergence: CMU retains the upstream traitor objective group and objective prototype alongside its RMC gamerules. This only restores selection for the station traitor ruleset and does not add it to RMC role objectives.
- Decision and rationale: Add the retained weight of `1`, matching the ordinary steal objectives and preserving the objective's existing job exclusion and steal condition.
- Files changed: `Resources/Prototypes/Objectives/objectiveGroups.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static prototype review confirms the referenced objective exists and its target steal group remains defined. Prototype loading and weighted objective selection are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: None.

## CS-0136 — Preserve the purchasing account on telepad orders

- Upstream: [space-wizards/space-station-14#39975](https://github.com/space-wizards/space-station-14/pull/39975), `ed12c1d3f5607db906712e3a5d13d7342dec7fc0`, 2025-09-04
- Areas: Interactions
- Status: Ported
- Risk: Medium
- Behavior/API delta: When a cargo telepad fulfills a queued order, the spawned shipment label and receipt now use the account recorded on that order rather than whichever cargo console is currently linked to the telepad.
- RMC/CMU divergence: CMU retains the upstream multi-account cargo order model and telepad loop. RMC cargo additions do not alter this fulfillment seam, and the linked console is still required to operate the pad.
- Decision and rationale: Pass the order's authoritative account into `FulfillOrder`; using mutable console state could mislabel a queued purchase after relinking or when accounts differ.
- Files changed: `Content.Server/Cargo/Systems/CargoSystem.Telepad.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static data-flow review confirms fulfillment, label creation, and receipt generation receive `currentOrder.Account`, while telepad linkage and queue removal remain unchanged. Server compilation plus a two-account telepad fulfillment case are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Audit shutdown recovery at the same telepad seam, which still supplies the linked console account to `TryFulfillOrder` in this older cargo implementation.

## CS-0137 — Throttle scurret petting interactions

- Upstream: [space-wizards/space-station-14#40097](https://github.com/space-wizards/space-station-14/pull/40097), `df4d923a9b709c9f3b5f123ce743db57c713351a`, 2025-09-04
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: Scurret petting now observes a 2.25-second interaction delay, preventing rapid repeated heart effects, popups, and animal sounds.
- RMC/CMU divergence: CMU retains the upstream scurret prototype and shared `InteractionPopup` cooldown field. RMC-specific mobs and their interaction timing are unaffected.
- Decision and rationale: Add the retained prototype value at the scurret base's existing popup component so all inherited scurrets share the same spam guard.
- Files changed: `Resources/Prototypes/Entities/Mobs/NPCs/scurret.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static inheritance review confirms the delay applies to scurret variants without changing success chance, strings, effects, or sounds. Prototype loading and interactions immediately before and after 2.25 seconds are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: None.

## CS-0138 — Suppress duplicate wield-drop popups

- Upstream: [space-wizards/space-station-14#40032](https://github.com/space-wizards/space-station-14/pull/40032), `69b3df03d8ee6a624b675537ec1542c4008bcdb2`, 2025-09-03
- Areas: Interactions
- Status: Ported
- Risk: Medium
- Behavior/API delta: Virtual-item hand allocation can now drop an obstructing held item silently. Wielding uses that option so players receive the wield-success popup without an overlapping automatic-drop popup; all other callers remain noisy by default.
- RMC/CMU divergence: RMC adds virtual-hand users for power loaders and multi-handed holders. The new optional argument defaults to `false`, preserving their existing feedback and positional `empty` argument behavior.
- Decision and rationale: Extend both overloads with a trailing optional flag and opt in only at the wielding call site, matching target-final behavior without changing existing fork callers.
- Files changed: `Content.Shared/Inventory/VirtualItem/SharedVirtualItemSystem.cs`, `Content.Shared/Wieldable/SharedWieldableSystem.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static call-site review confirms RMC and cuff/pulling callers retain popups, while wielding passes `silent: true` and still drops the item before creating virtual hands. Shared compilation plus wield success, failed allocation, multi-hand rollback, and default noisy-drop cases are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add a predicted wield regression asserting exactly one user-facing popup when an occupied hand must be cleared.

## CS-0139 — Localize crew-monitor map coordinates

- Upstream: [space-wizards/space-station-14#40247](https://github.com/space-wizards/space-station-14/pull/40247), `da210e812b0c4d8af906090b5a6e59f950d54fd3`, 2025-09-09
- Areas: Medical
- Status: Ported
- Risk: Low
- Behavior/API delta: The focused crew-monitor nav-map label now formats its location through the `navmap-location` localization key instead of embedding the English `Location` label in client code.
- RMC/CMU divergence: CMU retains the upstream crew-monitor nav-map control and RMC medical tracking does not override this label, so coordinate rounding and tracked-entity selection stay unchanged.
- Decision and rationale: Port the retained localization seam and English fallback text together, leaving the separately hardcoded unknown-name fallback for its own localization pass.
- Files changed: `Content.Client/Medical/CrewMonitoring/CrewMonitoringNavMapControl.cs`, `Resources/Locale/en-US/ui/navmap.ftl`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static review confirms both rounded coordinates are passed as Fluent arguments and the message retains its name/newline layout. Client compilation and localization loading are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Localize the `Unknown` tracked-name fallback when its target-final replacement is reached.

## CS-0140 — Restore RGB staff target validation

- Upstream: [space-wizards/space-station-14#40258](https://github.com/space-wizards/space-station-14/pull/40258), `960174acc5f90e6735f877d1715db699878814e6`, 2025-09-10
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: The RGB staff action now includes the base `TargetAction` component required by entity-target validation, allowing its existing point-light whitelist and component-change spell to execute.
- RMC/CMU divergence: RMC extends shared target validation with interaction and storage-access rules. Adding the missing base component makes this action participate in those fork checks instead of bypassing or failing them.
- Decision and rationale: Port the retained prototype component exactly; `EntityTargetAction` supplies the target/event while shared validation reads range and access settings from `TargetAction`.
- Files changed: `Resources/Prototypes/Magic/staves.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static system review confirms `ValidateEntityTarget` resolves `TargetActionComponent` after whitelist checks and the RGB action already inherits `BaseAction`. Prototype loading plus valid light, invalid entity, inaccessible target, and predicted activation cases are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Audit other `EntityTargetAction` prototypes for the same missing base component during the deeper actions migration.

## CS-0141 — Localize verb confirmation actions

- Upstream: [space-wizards/space-station-14#40248](https://github.com/space-wizards/space-station-14/pull/40248), `a5ef016f1e3afc0d4cd89a5c1b810e13834bd09c`, 2025-09-10
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: Context-menu verbs that require confirmation now label their confirmation action through the existing `generic-confirm` localization key instead of hardcoded English text.
- RMC/CMU divergence: RMC adds verbs and confirmation use cases but shares this menu controller and generic localization key, so all fork verbs gain localization without changing execution or confirmation policy.
- Decision and rationale: Replace only the displayed label; submenu construction, debug-mode bypass, and verb execution remain unchanged.
- Files changed: `Content.Client/Verbs/UI/VerbMenuUIController.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static review confirms `generic-confirm` exists in the English locale and is resolved only when a confirmation submenu is created. Client compilation and a confirmation-popup interaction are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: None.

## CS-0142 — Apply weightless movement to large cardboard boxes

- Upstream: [space-wizards/space-station-14#40260](https://github.com/space-wizards/space-station-14/pull/40260), `2601853791d8fc318d1e57e9420acb2eb7d9eac9`, 2025-09-11
- Areas: Movement, Physics
- Status: Reverted by CS-0165
- Risk: Low
- Behavior/API delta: Large cardboard boxes now participate in gravity-dependent movement behavior, including the correct weightless state when used or moved in zero gravity.
- RMC/CMU divergence: CMU retains the upstream `BaseBigBox` mover and physics hierarchy. The marker is added to that station-content base only and does not alter RMC-specific crates or storage entities.
- Decision and rationale: Add the retained `GravityAffected` component at the common box base so stealth and inherited variants share the same gravity semantics.
- Files changed: `Resources/Prototypes/Entities/Structures/Storage/Closets/big_boxes.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static inheritance review confirms every large-box variant receives the marker while existing body type, fixtures, and input mover remain unchanged. Prototype loading plus gravity/zero-gravity movement cases are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Superseded by CS-0165 after checkpoint prototype loading proved the marker belongs to the deferred event-based weightlessness architecture.

## CS-0143 — Predict APC breaker toggle state

- Upstream: [space-wizards/space-station-14#40273](https://github.com/space-wizards/space-station-14/pull/40273), `164f8a2fad42c459a8d1dff6ee35b536f4e8a10d`, 2025-09-10
- Areas: Interactions, GameTicking
- Status: Ported
- Risk: Low
- Behavior/API delta: The APC main-breaker button now operates as a toggle control, so its pressed state changes immediately and reconciles with authoritative APC state instead of presenting a momentary push-button state.
- RMC/CMU divergence: CMU retains the older button-based APC UI while target-final later uses a switch control. `ToggleMode` is the compatible semantic fix for this UI version and does not touch RMC power receivers.
- Decision and rationale: Add only the retained toggle flag; the existing state update already writes `BreakerButton.Pressed` from `MainBreaker` and remains the reconciliation source.
- Files changed: `Content.Client/Power/APC/UI/ApcMenu.xaml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static UI review confirms click handling, access disabling, and authoritative pressed-state updates remain intact. Client XAML compilation plus click prediction, denial, and state reconciliation are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Adopt target-final `SwitchButton` styling when the broader APC UI migration is reconciled.

## CS-0144 — Release invalid pulls when handcuffed

- Upstream: [space-wizards/space-station-14#40233](https://github.com/space-wizards/space-station-14/pull/40233), `49fb6fdd6c5fc5c8d8dd4f8f525362f8dc227915`, 2025-09-11
- Areas: Movement, Interactions, Physics
- Status: Ported
- Risk: Medium
- Behavior/API delta: Cuff insertion now completes before `TargetHandcuffedEvent` is raised, allowing pull validation to observe the new occupied hands. An active pull that is no longer valid is stopped, and a cuffed player may always request release of an existing pull even though ordinary interaction is blocked.
- RMC/CMU divergence: CMU adds RMC fireman-carry and pulling systems around the upstream joint path. The adaptation keeps those dependencies and existing `CanPull` checks, using the common active-puller marker so fork-specific pull eligibility is respected.
- Decision and rationale: Port the three coordinated changes atomically: correct event ordering, react on active pullers, and remove the interaction gate only from stopping a pull. Starting and continuing pulls still use their existing blocker and RMC validation.
- Files changed: `Content.Shared/Cuffs/SharedCuffableSystem.cs`, `Content.Shared/Movement/Pulling/Systems/PullingSystem.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static control-flow review confirms the cuff exists before eligibility is recomputed, valid handless pullers remain active, invalid hand-dependent pulls stop, and `AttemptStopPullingEvent` can still cancel release. Shared compilation plus self-release, cuff-during-pull, `NeedsHands = false`, cancellation, prediction, and RMC fireman-carry cases are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add focused pull/cuff prediction tests covering event order and virtual-hand cleanup.

## CS-0145 — Restore movable physics after unanchoring input movers

- Upstream: [space-wizards/space-station-14#37960](https://github.com/space-wizards/space-station-14/pull/37960), `ab40b1ab734f664d18f96066e2c6e65a515866c9`, 2025-09-13
- Areas: Movement, Physics
- Status: Ported
- Risk: Medium
- Behavior/API delta: When an entity with `InputMover` becomes unanchored, its physics body is restored to `KinematicController`, preventing projected or temporarily anchored movers from remaining immobile with a static body.
- RMC/CMU divergence: RMC extends mover relays and prediction but uses the same shared mover controller and physics service. The handler runs only on unanchor and leaves fork relay selection and `CanMove` state untouched.
- Decision and rationale: Subscribe at the common input-mover seam and restore the body type through `PhysicsSystem`; anchoring remains owned by the transform/physics path, while unanchoring must re-establish a controller body for movement input.
- Files changed: `Content.Shared/Movement/Systems/SharedMoverController.Input.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static event review confirms anchored transitions are ignored and unanchored movers receive the expected body type without changing fixtures or prediction ownership. Shared compilation plus chameleon projection, normal humanoid, relay mover, repeated anchor/unanchor, and client reconciliation cases are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Audit input-mover prototypes that intentionally require a non-kinematic unanchored body before broadening this rule further.

## CS-0146 — Recharge limited-charge wizard wands

- Upstream: [space-wizards/space-station-14#40347](https://github.com/space-wizards/space-station-14/pull/40347), `9c3af67cd1535c3d9060bc74ec14b5c712ed783b`, 2025-09-14
- Areas: Shooting, Interactions
- Status: Ported
- Risk: Medium
- Behavior/API delta: The wizard recharge spell now replenishes held wands backed by `LimitedChargesComponent` in addition to legacy ammo-provider wands, restoring recharge behavior for both current staff implementations.
- RMC/CMU divergence: CMU retains RMC charge consumers and the same shared charge system. The new branch is limited to held items carrying the existing wizard-wand tag, and legacy entity-ammo providers keep precedence.
- Decision and rationale: Resolve the tagged wand first, update legacy ammo when available, then fall back to `SharedChargesSystem.AddCharges`; this preserves existing behavior while supporting the retained wand architecture.
- Files changed: `Content.Shared/Magic/SharedMagicSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static data-flow review confirms the action remains handled after prerequisites, null/no-provider wands remain no-ops, ammo wands use their old path, and limited charges respect the shared cap. Shared compilation plus both provider types, capped recharge, untagged item, and prediction cases are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add a focused recharge-spell test covering mixed held items and both charge providers.

## CS-0147 — Add counterplay to ninja-glove stuns

- Upstream: [space-wizards/space-station-14#39707](https://github.com/space-wizards/space-station-14/pull/39707), `09a197eb9162b94a7ee1f3cc78772a6b7783c47a`, 2025-09-16
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: The space ninja glove's generated stun provider now has a ten-second cooldown, preventing repeated no-delay stun interactions.
- RMC/CMU divergence: CMU retains the upstream ninja glove ability bundle; RMC melee and stun systems are not changed, and the cooldown is scoped to this generated provider only.
- Decision and rationale: Port the retained prototype balance value at the provider definition while preserving its power drain, whitelist, popup, and other ninja abilities.
- Files changed: `Resources/Prototypes/Entities/Clothing/Hands/gloves.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static prototype review confirms only the ninja-generated `StunProvider` receives the cooldown. Prototype loading and repeated interactions before/after ten seconds are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: None.

## CS-0148 — Separate lethal and practice laser-rifle inheritance

- Upstream: [space-wizards/space-station-14#40253](https://github.com/space-wizards/space-station-14/pull/40253), `46f59300acb99f3c2373bbc83ab446e0aa77621c`, 2025-09-10
- Areas: Shooting, Interactions
- Status: Ported
- Risk: Medium
- Behavior/API delta: The lethal and practice laser rifles are now sibling prototypes under a shared abstract base. Lethal red-laser ammunition and security contraband are defaults only for the lethal rifle, while the practice rifle independently overrides harmless ammunition and price.
- RMC/CMU divergence: CMU still uses `HitscanBatteryAmmoProvider`, Huge rifle sizing, and the pre-resize `laser rifle` names. Those fork-compatible/current-era choices are preserved; later power-cell prediction, weapon-size, tag, and naming migrations are deliberately not pulled forward.
- Decision and rationale: Extract only the common current components into `BaseLaserRifle` and remove the unsafe lethal-from-practice inheritance. This prevents practice-only markers and future balance fields from leaking into the lethal weapon while keeping resolved behavior otherwise equivalent.
- Files changed: `Resources/Prototypes/Entities/Objects/Weapons/Guns/Battery/battery_guns.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static resolved-prototype review confirms both rifles retain their sprite, wielding, clothing, firing mode, charge cost, and current size; only lethal inherits security contraband, and only practice resolves `RedLaserPractice` at price 300. Prototype loading plus resolved-component assertions, firing, contraband, and inherited-tag checks are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Revisit item size/naming with index 0417 and migrate the provider/tag fields with the later predicted battery-gun architecture.

## CS-0149 — Allow pacifists to use nonlethal energy guns

- Upstream: [space-wizards/space-station-14#37164](https://github.com/space-wizards/space-station-14/pull/37164), `3d35435747bb6080a2cdcfe65c6d34afcfdf2be0`, 2025-08-17
- Areas: Shooting, Interactions
- Status: Ported
- Risk: Medium
- Behavior/API delta: Practice laser rifles, practice disablers, standard disablers, and disabler SMGs now carry `PacifismAllowedGun`, allowing pacifist users to fire their nonlethal ammunition.
- RMC/CMU divergence: The earlier CMU hierarchy made the lethal laser rifle inherit the practice rifle, which would have leaked this permission. CS148 first made the rifles siblings, so the marker now remains confined to the harmless practice laser while the lethal rifle stays blocked.
- Decision and rationale: Apply the upstream marker to the four explicitly approved nonlethal weapons after resolving the inheritance hazard. Preserve all current ammo providers, charge costs, contraband, and RMC gun behavior.
- Files changed: `Resources/Prototypes/Entities/Objects/Weapons/Guns/Battery/battery_guns.yml`, `docs/upstream-sync/inventory-wave-0003.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static inheritance review confirms the practice laser and all three disablers resolve the marker, while `WeaponLaserCarbine` does not inherit it. Prototype loading and pacifist fire-permission checks for lethal versus nonlethal weapons are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: None.

## CS-0150 — Exclude drone laws from ion storms

- Upstream: [space-wizards/space-station-14#40374](https://github.com/space-wizards/space-station-14/pull/40374), `1dd977effde65714476abfcdfe36d40849d32601`, 2025-09-17
- Areas: Gamerules
- Status: Ported
- Risk: Low
- Behavior/API delta: Ion storms no longer select the drone lawset as a random replacement for a silicon's laws.
- RMC/CMU divergence: CMU retains the drone lawset itself and all fork-specific silicon roles; only its accidental inclusion in the ion-storm random pool is removed.
- Decision and rationale: Port the isolated weighted-random correction because drone laws are role-specific and unsuitable as a general ion-storm outcome.
- Files changed: `Resources/Prototypes/silicon-laws.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static prototype review confirms `Drone` remains defined but is absent from `IonStormLawsets`. Prototype loading and weighted-random resolution are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: None.

## CS-0151 — Bind stun runes to their rune fixture

- Upstream: [space-wizards/space-station-14#40432](https://github.com/space-wizards/space-station-14/pull/40432), `b41ce9cce666110f4f76d3f4d17695e20efbd1ff`, 2025-09-18
- Areas: Interactions, Physics
- Status: Ported
- Risk: Low
- Behavior/API delta: `StunRune` now tells `StunOnCollide` to listen to its `rune` fixture instead of the component's projectile-fixture default, restoring collision-triggered stuns.
- RMC/CMU divergence: CMU retains the current server-side collision stun implementation and rune fixture layout; the fix only aligns their existing fixture identifiers.
- Decision and rationale: Port the isolated prototype field because `TriggerOnCollide` already watches `rune`, while `StunOnCollide` silently ignored those contacts under its default `projectile` fixture.
- Files changed: `Resources/Prototypes/Magic/Fixtures/runes.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static fixture-flow review confirms both trigger and stun components now select `rune`. Prototype loading and a living-entity collision assertion are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: None.

## CS-0152 — Stock chemical-analysis goggles in ChemDrobe

- Upstream: [space-wizards/space-station-14#40236](https://github.com/space-wizards/space-station-14/pull/40236), `8cf5c3f6bc9f62eb7dc76de180ad029e0c79a2f6`, 2025-09-18; target-final quantity from [#42423](https://github.com/space-wizards/space-station-14/pull/42423), `820fdca6efa9ea4c8390a5b9b8bb783b2759791b`
- Areas: Chemistry, Medical
- Status: Adapted
- Risk: Low
- Behavior/API delta: ChemDrobe now stocks one pair of chemical-analysis goggles, making solution-scanning equipment available through the chemistry wardrobe.
- RMC/CMU divergence: CMU keeps its current ChemDrobe inventory and job loadouts. The pinned target's final quantity of one is used instead of transient upstream quantity two, without importing the later broad medical equipment rebalance.
- Decision and rationale: Port the isolated stock entry in its target-final form to provide the intended chemistry tool while avoiding later inventory churn.
- Files changed: `Resources/Prototypes/Catalog/VendingMachines/Inventories/chemdrobe.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static inventory review confirms the referenced goggles prototype exists and appears once in starting inventory. Prototype loading and vending-inventory resolution are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Reassess the rest of ChemDrobe when auditing target commit `820fdca6ef`.

## CS-0153 — Remove hidden no-slip behavior from snakeskin boots

- Upstream: [space-wizards/space-station-14#40201](https://github.com/space-wizards/space-station-14/pull/40201), `d9d968a4793a3d00694f13d2720127efad3915b9`, 2025-09-18
- Areas: Movement, Physics
- Status: Ported
- Risk: Low
- Behavior/API delta: Snakeskin boots no longer grant unadvertised slip immunity, and their description reflects their reduced value.
- RMC/CMU divergence: CMU retains all RMC footwear and slip mechanics; only the upstream snakeskin prototype's accidental `NoSlip` component is removed.
- Decision and rationale: Port the isolated prototype correction so a cosmetic rare drop cannot silently bypass wet-floor movement hazards.
- Files changed: `Resources/Prototypes/Entities/Clothing/Shoes/misc.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static resolved-prototype review confirms the boots no longer contain `NoSlip`. Prototype loading and a standard slipping interaction are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: None.

## CS-0154 — Clarify the implant extractor's behavior

- Upstream: [space-wizards/space-station-14#40375](https://github.com/space-wizards/space-station-14/pull/40375), `0e0f01542210e8103001ca4746c5de3bd64e07c3`, 2025-09-18
- Areas: Medical, Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: The reusable MedFab implanter is now named `implant extractor`; its description and revolutionary guide explain extraction, re-administration, and catastrophic damage after an invalid selection.
- RMC/CMU divergence: No implant logic changes are imported. The text documents CMU's existing `DrawCatastrophicFailure` path and preserves all RMC surgery and implant behavior.
- Decision and rationale: Port the name and guidance together so players can identify the tool and understand the already-existing failure consequence before using it.
- Files changed: `Resources/Prototypes/Entities/Objects/Misc/implanters.yml`, `Resources/ServerInfo/Guidebook/Antagonist/Revolutionaries.xml`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static code/prototype review confirms the extractor remains injectable after storing an implant and invalid extraction still applies `DeimplantFailureDamage`. Prototype loading and guidebook parsing are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: None.

## CS-0155 — Silence mime storage interactions

- Upstream: [space-wizards/space-station-14#40317](https://github.com/space-wizards/space-station-14/pull/40317), `128d06518efbcdae2dd5e0e48a5c01010ab21a0c`, 2025-09-18
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: Mime backpacks, satchels, and duffel bags explicitly suppress their inherited open and insert sounds.
- RMC/CMU divergence: CMU retains its current storage capacities, sprites, and RMC storage interactions. The duffel's old sound keys were incorrectly nested under `Sprite`; the port places all overrides on `Storage`.
- Decision and rationale: Port the three prototype overrides together because they express one consistent mime-equipment behavior and correct the ineffective duffel serialization at the same time.
- Files changed: `Resources/Prototypes/Entities/Clothing/Back/backpacks.yml`, `Resources/Prototypes/Entities/Clothing/Back/duffel.yml`, `Resources/Prototypes/Entities/Clothing/Back/satchel.yml`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static resolved-prototype review confirms each mime bag has `Storage` with both sounds set to null. Prototype loading plus open and insert interaction checks are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: None.

## CS-0156 — Distribute ichor healing evenly within damage groups

- Upstream: [space-wizards/space-station-14#39466](https://github.com/space-wizards/space-station-14/pull/39466), `fbb9c9c524e0e8cc01a90941d61e61e2275ed863`, 2025-09-18
- Areas: Medical, Chemistry
- Status: Ported
- Risk: Medium
- Behavior/API delta: Ichor now spends its Burn, Brute, and Toxin healing budgets proportionally across damage types the consumer actually has, while bloodloss healing is reduced from five to three.
- RMC/CMU divergence: CMU already contains the shared `EvenHealthChange` effect used by the pinned upstream implementation. Dragon metabolism, blood restoration, bleed reduction, and fork-specific damage systems are otherwise unchanged.
- Decision and rationale: Replace broad group healing with the retained even-distribution effect so ichor does not over-heal every subtype in a group; use upstream's final per-tick budgets.
- Files changed: `Resources/Prototypes/Reagents/biological.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static effect review confirms group healing is handled only by `EvenHealthChange` and bloodloss only by `HealthChange`. Prototype loading plus single- and mixed-damage metabolism assertions are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Add focused effect coverage for zero-damage groups and mixed damage distributions if the checkpoint exposes edge cases.

## CS-0157 — Let tarantulas pull without hands

- Upstream: [space-wizards/space-station-14#40433](https://github.com/space-wizards/space-station-14/pull/40433), `5a67e3c26a23f0d6432c5a88e4b8df7e5dbf1f51`, 2025-09-18
- Areas: Movement, Interactions, Physics
- Status: Ported
- Risk: Low
- Behavior/API delta: All descendants of `MobSpiderBase` can initiate pulling despite lacking hands, enabling player-controlled tarantulas and their variants to drag entities.
- RMC/CMU divergence: The capability is scoped to the retained upstream spider base; RMC xenonid pulling and fork-specific mob movement are unchanged.
- Decision and rationale: Add `Puller` with `needsHands: false` at the shared spider base, matching the existing component contract and intentionally covering pet, hostile, clown, and wizard-derived spiders.
- Files changed: `Resources/Prototypes/Entities/Mobs/NPCs/animals.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static inheritance review confirms the component reaches all `MobSpiderBase` descendants without altering `Pullable` or collision fixtures. Prototype loading and a no-hands pull interaction are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: None.

## CS-0158 — Identify conveyor fixtures with the canonical collision mask

- Upstream: [space-wizards/space-station-14#40439](https://github.com/space-wizards/space-station-14/pull/40439), `ed89c0e06196a3b3ab0b466ec6dd7ebee2742c9d`, 2025-09-18
- Areas: Movement, Physics, Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: Conveyor fixtures now declare the canonical `ConveyorMask` symbol instead of spelling out its four constituent collision flags. Current resolved collision bits and door-closing behavior remain equivalent.
- RMC/CMU divergence: CMU's door system already compares the resolved layer with `ConveyorMask`, and the expanded prototype flags already produced that numeric value. The server controller's fallback fixture omits `DoorPassable`, but it runs only when no fixture already exists and remains unchanged in the pinned target.
- Decision and rationale: Replace the expanded flag list with the canonical named mask so the prototype and the door system share one identity and future mask changes cannot silently diverge.
- Files changed: `Resources/Prototypes/Entities/Structures/conveyor.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static collision review confirms `ConveyorMask` resolves to the same prior bits and is the exact value checked by `SharedDoorSystem`. Prototype loading plus door closure over a conveyor are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Reconcile the divergent fallback mask if CMU later permits conveyors without prototype fixtures; the pinned target does not yet remove that path.

## CS-0159 — Destroy welded disposal pipes without deleting contents

- Upstream: [space-wizards/space-station-14#40451](https://github.com/space-wizards/space-station-14/pull/40451), `7c650da7d7659eec0be135ccd3eaef9787e9fb34`, 2025-09-20
- Areas: Interactions, Physics
- Status: Ported
- Risk: Medium
- Behavior/API delta: Welding any of the fourteen disposal-pipe graph nodes into materials now uses the destructible lifecycle instead of directly deleting the entity, allowing normal destruction handling to preserve/eject contained entities.
- RMC/CMU divergence: CMU retains its existing disposal graph, container, and destructible systems. Only the graph completion action changes; material yields, welding times, and construction nodes remain untouched.
- Decision and rationale: Replace `DeleteEntity` with `DestroyEntity` consistently across every disposal-pipe exit because direct deletion recursively removes contained entities without their destruction/ejection path.
- Files changed: `Resources/Prototypes/Recipes/Construction/Graphs/utilities/disposal_pipes.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static graph review confirms all fourteen disposal exits now use `DestroyEntity` and no `DeleteEntity` action remains in this graph. Prototype loading plus welding an occupied pipe and asserting content survival are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: None.

## CS-0160 — Clarify the nuclear-operative corpsman objective

- Upstream: [space-wizards/space-station-14#40486](https://github.com/space-wizards/space-station-14/pull/40486), `eabb00a1e2906e32221781ce91c28608db4d6609`, 2025-09-21
- Areas: Medical, Gamerules
- Status: Ported
- Risk: Low
- Behavior/API delta: The nuclear-operative corpsman's role text now clearly identifies them as the team's medic and directs them to keep the team alive.
- RMC/CMU divergence: This is the upstream nuclear-operative role, not CMU's separate RMC hospital-corpsman jobs. Their localization and game rules are untouched.
- Decision and rationale: Port the retained objective copy as the companion to CS131's corrected role title, replacing a vague and grammatically broken description.
- Files changed: `Resources/Locale/en-US/prototypes/roles/antags.ftl` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static localization review confirms the existing role key resolves to the new text. Fluent parsing and nuclear-operative role prototype resolution are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: None.

## CS-0161 — Correct standalone fire-helmet temperature protection

- Upstream: [space-wizards/space-station-14#40481](https://github.com/space-wizards/space-station-14/pull/40481), `3f575a64f3ff0fbcaa308ad55670c76ec7b2a5d8`, 2025-09-23
- Areas: Medical, Physics
- Status: Ported
- Risk: Medium
- Behavior/API delta: A regular fire helmet now passes through half of environmental heating and an atmos fire helmet passes through thirty percent; both cool at eighty percent of the unprotected rate. Wearing only a helmet no longer almost freezes body temperature while the wearer burns.
- RMC/CMU divergence: The values apply only to retained upstream fire helmets. RMC firefighter helmets use separate prototypes and CM armor behavior and are not changed.
- Decision and rationale: Port the tested upstream coefficients as one balance correction while preserving each helmet's existing fire-damage and pressure protection.
- Files changed: `Resources/Prototypes/Entities/Clothing/Head/helmets.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static resolved-prototype review confirms regular/atmos coefficients are `0.5/0.8` and `0.3/0.8`, respectively. Prototype loading plus burning-temperature comparisons for helmet-only and full-suit wear are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: None.

## CS-0162 — Keep portal ghost-verb subjects stable

- Upstream: [space-wizards/space-station-14#37540](https://github.com/space-wizards/space-station-14/pull/37540), `7102da139b74776b5d1b0875ccaab2bad0fe141f`, 2025-09-23
- Areas: Movement, Interactions, Physics
- Status: Adapted
- Risk: Low
- Behavior/API delta: The portal traversal alternative verb captures the requesting ghost before registering its delayed action, then refuses client prediction when the linked destination does not exist locally or is in Nullspace.
- RMC/CMU divergence: The upstream commit also refactors portal entities and client prediction. CMU retains its older portal API and imports only the lifetime-safe capture needed to prevent the developer crash; collision, pull-breaking, random teleport, and sound behavior are unchanged.
- Decision and rationale: Capture `args.User` into a stable local and guard the selected destination before transforming it. This is the minimal compatible correction for both the transient verb-event lifetime and linked portals outside client PVS.
- Files changed: `Content.Shared/Teleportation/Systems/SharedPortalSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static closure review confirms the delayed action no longer captures `args`, and the client checks existence before reading the destination transform. Shared compilation plus valid, outside-PVS, Nullspace, unlinked, and multi-output ghost traversal cases are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: Revisit the upstream prediction helper and typed-entity refactor when their dependent portal architecture enters scope.

## CS-0163 — Stock diagnostic HUDs in EngiVend

- Upstream: [space-wizards/space-station-14#40461](https://github.com/space-wizards/space-station-14/pull/40461), `7c39b4595f9512aa49ae5085fce5f39988b89d7f`, 2025-09-23
- Areas: Medical, Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: EngiVend starting inventory now includes four diagnostic HUDs, giving engineering direct access to inorganic and silicon integrity displays.
- RMC/CMU divergence: The retained upstream vendor and HUD prototypes remain separate from RMC engineering vendors and medical HUD overlays; no fork-specific inventory is replaced.
- Decision and rationale: Port the isolated inventory entry because the referenced diagnostic HUD already resolves and its functionality directly supports engineering maintenance.
- Files changed: `Resources/Prototypes/Catalog/VendingMachines/Inventories/engivend.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static inventory review confirms `ClothingEyesHudDiagnostic` exists and EngiVend stocks four. Prototype loading and vending inventory resolution are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: None.

## CS-0164 — Enter shuttle pilot mode only after UI opening

- Upstream: [space-wizards/space-station-14#40491](https://github.com/space-wizards/space-station-14/pull/40491), `a26bafacb1b2d81b40a19274edda40aca14cb696`, 2025-09-21
- Areas: Movement, Interactions, Physics
- Status: Ported
- Risk: Medium
- Behavior/API delta: Shuttle consoles now call `TryPilot` after their activatable UI actually opens, rather than during a cancellable open attempt. Failed or intercepted attempts can no longer put a user into pilot mode, and inability to pilot no longer incorrectly cancels read-only UI access.
- RMC/CMU divergence: CMU retains its current shuttle console, drone console, pilot component, and movement-blocking systems. Only the standard shuttle console's event phase changes; RMC dropship controls are untouched.
- Decision and rationale: Subscribe to `AfterActivatableUIOpenEvent` and document the attempt event's pre-open contract so piloting state follows real UI ownership instead of speculative interaction.
- Files changed: `Content.Server/Shuttles/Systems/ShuttleConsoleSystem.cs`, `Content.Shared/UserInterface/ActivatableUIEvents.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static event-flow review confirms `TryPilot` runs only after opening and UI close still removes the pilot. Server/shared compilation plus successful, denied, intercepted, and close/reopen console cases are queued for the first 1,000-upstream-commit checkpoint.
- Follow-up/debt: None.

## CS-0165 — Defer the large-box gravity marker with its architecture

- Upstream: [space-wizards/space-station-14#40260](https://github.com/space-wizards/space-station-14/pull/40260), `2601853791d8fc318d1e57e9420acb2eb7d9eac9`, 2025-09-11; prerequisite [#37971](https://github.com/space-wizards/space-station-14/pull/37971), `9de76e70c71097241b3b2a2720eef0c1d34aba89`
- Areas: Movement, Physics
- Status: Deferred; reverts CS-0142
- Risk: Medium
- Behavior/API delta: Remove the unregistered `GravityAffected` marker from `BaseBigBox`. CMU's current gravity system still derives weightlessness for dynamic physics bodies directly, so large boxes retain their pre-rewrite gravity behavior without this newer cached-state component.
- RMC/CMU divergence: SS14 introduced `GravityAffectedComponent` in its broad event-based weightlessness rewrite at inventory index 0506. CMU deliberately deferred that rewrite because it crosses RMC pulling and movement behavior; importing a later consumer without the component definition makes prototype composition fail.
- Decision and rationale: Keep the content boundary coherent by reverting only the premature marker and deferring #40260 alongside #37971. Do not copy the component in isolation because its semantics depend on the new gravity system, event subscriptions, movers, throwing, shooting, friction, conveyors, magboots, and RMC-sensitive pulling paths.
- Files changed: `Resources/Prototypes/Entities/Structures/Storage/Closets/big_boxes.yml`, `docs/upstream-sync/inventory-wave-0005.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: The first checkpoint integration run reproduced `UnknownComponentException: GravityAffected` for `BigBox` and `StealthBox`. Static review confirms the current `SharedGravitySystem.IsWeightless` handles non-static bodies without the marker; prototype loading and integration tests are rerun after this removal.
- Follow-up/debt: Port #37971 and #40260 together during the deeper Movement/Physics reconciliation, with explicit regression coverage for boxes, RMC pulls, magboots, projectiles, throws, conveyors, and zero-gravity movement.

## CS-0166 — Complete the golden-plunger storage tag contract

- Upstream: [space-wizards/space-station-14#38494](https://github.com/space-wizards/space-station-14/pull/38494), `cd0960fbd760a9eebf51474616437ab7eee73cc6`; [#39213](https://github.com/space-wizards/space-station-14/pull/39213), `8b104d30d5428682acf4edf15547b033625554e7`; target-final tag inheritance from [#40619](https://github.com/space-wizards/space-station-14/pull/40619), `e1c03249fa0d73f528f8b988f00bb64530278002`
- Areas: Interactions
- Status: Adapted
- Risk: Low
- Behavior/API delta: `GoldenPlunger` is now a registered tag and the golden-plunger entity explicitly carries it while retaining the parent's `Plunger` and `WhitelistChameleon` tags. Janibelts can accept the item without prototype initialization failing on an unknown whitelist tag.
- RMC/CMU divergence: CMU already retained the golden-plunger entity and assets, then ported the later janibelt whitelist in isolation. It does not yet carry the upstream janicart mapper or bucket-carp variants, which remain independently deferred.
- Decision and rationale: Complete the minimal target-final tag contract needed by the accepted janibelt port. Explicitly repeat inherited tags because the derived `Tag` component replaces its parent's serialized tag set.
- Files changed: `Resources/Prototypes/tags.yml`, `Resources/Prototypes/Entities/Objects/Specific/Janitorial/janitor.yml`, both affected wave inventories, and `docs/upstream-sync/core-system-audit.md`.
- Validation: The first 1,000-commit integration checkpoint reproduced `Unknown tag: GoldenPlunger` while spawning lathe products. Static review confirms the tag is now declared and assigned with the target-final inherited set; the failing lathe test and full integration suite are rerun after this correction.
- Follow-up/debt: Reassess the remaining janicart and bucket-carp portion of #38494 separately from this storage-contract fix.

## Upstream checkpoint — indices 0000–0999

Date completed: 2026-07-20

- Scope: The first five 200-commit inventory waves, covering 1,000 pinned SS14 first-parent commits from indices 0000 through 0999 and every accepted CS decision through CS-0166. This checkpoint exercises all eight audited areas: Movement, Shooting, Medical, Chemistry, Interactions, Physics, GameTicking, and Gamerules.
- Unit tests: `dotnet test Content.Tests/Content.Tests.csproj --no-restore --nologo --verbosity:minimal` completed with 377 passed, 1 skipped, and 0 failed.
- Targeted regression: `TestLatheRecipeIngredientsFitLathe` passed 1/1 after CS-0166 completed the golden-plunger tag contract.
- Integration tests: the full `Content.IntegrationTests` suite completed with 418 passed, 17 skipped, and 0 failed (435 total) in 39 minutes 13 seconds. The passing rerun used `NUnit.ConsoleOut=0` and `NUnit.MapWarningTo=Failed`; its TRX was written outside the repository.
- Solution build: `dotnet build SpaceStation14.slnx --configuration DebugOpt --no-restore --no-incremental --nologo --verbosity:minimal --disable-build-servers` completed in 1 minute 54 seconds with 0 warnings and 0 errors.
- Resource validation: `dotnet run --project Content.YAMLLinter/Content.YAMLLinter.csproj --configuration DebugOpt --no-build` completed with `No errors found` in 89.6 seconds.
- Defects caught: integration compilation exposed invalid analyzer access in `DefaultAutomaticFireModeTest` and was corrected in `f012d2923c`; prototype loading exposed the dependency-gated `GravityAffected` marker and produced CS-0165 (`d88b459437`); lathe entity spawning exposed the incomplete `GoldenPlunger` tag contract and produced CS-0166 (`1c07b0bff9`). Each correction was committed before its focused or full rerun.
- Disposition: The 0000–0999 checkpoint is closed. Continue with inventory wave 0006 at index 1000 and defer routine build/test execution until index 1999 unless a specific risk makes earlier validation necessary.

## CS-0167 — Preserve physics during the chess-dimension smite

- Upstream: [space-wizards/space-station-14#40583](https://github.com/space-wizards/space-station-14/pull/40583), `21a29212ab2c664ad016218bb2802ddace84e909`, 2025-09-29
- Areas: Movement, Interactions, Physics, Gamerules
- Status: Ported
- Risk: Low
- Behavior/API delta: The chess-dimension smite now keeps the victim's `PhysicsComponent` when making them tabletop-draggable and moving them into the spawned board session. The smite no longer strips the controlled mob's physics state as a prerequisite for dragging.
- RMC/CMU divergence: CMU retains the standard administrative smite and tabletop systems without an RMC override. RMC movement and physics systems may still observe the victim, so preserving the established body is safer than reconstructing fork-specific state after the transfer.
- Decision and rationale: Remove only the premature component removal retained by the pinned target. `TabletopDraggableComponent` already supplies tabletop drag behavior; deleting the mob's physics component is unnecessary and invalidates systems that expect its body and fixtures to remain present.
- Files changed: `Content.Server/Administration/Systems/AdminVerbSystem.Smites.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static flow review confirms the victim keeps physics through `SetMapCoordinates` and tabletop dragging, while the smite still enables godmode, adds tabletop drag state, creates a board session, and resets rotation. Compilation plus smiting, dragging, returning, and RMC movement-state cases are queued for the index-1999 checkpoint.
- Follow-up/debt: None.

## CS-0168 — Open imported content files read-only

- Upstream: [space-wizards/space-station-14#37779](https://github.com/space-wizards/space-station-14/pull/37779), `52430df55f20578817df29638752debb219a4a0d`, 2025-09-29
- Areas: Interactions
- Status: Adapted
- Risk: Low
- Behavior/API delta: Fax text, MIDI instruments, character-profile imports, and RMC cassette audio now request read-only streams from the platform file picker instead of accepting its read/write default.
- RMC/CMU divergence: Upstream has three consumers; CMU adds the RMC cassette OGG importer, which also only reads its selected stream and therefore receives the same explicit access mode. File parsing, size limits, and UI flow are unchanged.
- Decision and rationale: Pass `FileAccess.Read` at every content-side import call site. None of these paths writes to the selected file, and unnecessarily requesting write access can reject otherwise readable files or grant broader handles than the operation needs.
- Files changed: the fax, instrument, profile-editor, and RMC cassette client importers plus `docs/upstream-sync/core-system-audit.md`.
- Validation: Static call-site review confirms all four selected streams are only read and the pinned RobustToolbox file-dialog API supports explicit `FileAccess.Read`; no content-side `OpenFile` call remains on the default access mode. Client compilation and importing read-only TXT, MIDI, YAML, and OGG files are queued for the index-1999 checkpoint.
- Follow-up/debt: Recheck new file-dialog consumers as later upstream and RMC changes are ported.

## CS-0169 — Log construction of every grille variant

- Upstream: [space-wizards/space-station-14#40603](https://github.com/space-wizards/space-station-14/pull/40603), `768870ac686196b946179f5e77959f623b0791a0`, 2025-09-29
- Areas: Interactions, Gamerules
- Status: Ported
- Risk: Low
- Behavior/API delta: Completing the initial construction edge for clockwork, diagonal, or diagonal clockwork grilles now emits the same high-impact construction admin log as a standard grille. Cutting and rebuilding existing grilles retain their current actions.
- RMC/CMU divergence: No RMC-specific construction graph duplicates these three standard nodes. RMC construction logging infrastructure consumes the same `AdminLog` graph action, so no fork-only code path changes.
- Decision and rationale: Add the retained target-final completion action to each missing start edge. Grilles affect movement and secure areas, so variant choice should not bypass the audit trail already applied to standard grille construction.
- Files changed: `Resources/Prototypes/Recipes/Construction/Graphs/structures/grille_clockwork.yml`, `Resources/Prototypes/Recipes/Construction/Graphs/structures/grille_diagonal.yml`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static graph review confirms all three start edges emit exactly one `Construction` log at `High` impact and cancelled/incomplete paths do not reach the completion action. Graph deserialization and completed/cancelled construction cases are queued for the index-1999 checkpoint.
- Follow-up/debt: None.

## CS-0170 — Default status signals to toggle inputs

- Upstream: [space-wizards/space-station-14#37690](https://github.com/space-wizards/space-station-14/pull/37690), `13294a951a665ffcf1a98da38182cbfa391f33f2`, 2025-10-01
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: A device-link source exposing the standard `Status` port now automatically proposes the standard `Toggle` sink when default links are created. Manual linking and every other source/sink combination are unchanged.
- RMC/CMU divergence: RMC doors and lighting reuse the standard `Toggle` sink alongside upstream devices, so they gain the same default-pairing behavior without changing their device-network or access logic.
- Decision and rationale: Add the single target-final default link to the shared port prototype. Status transmitters represent an on/off state, making `Toggle` the established compatible sink while retaining explicit user configuration.
- Files changed: `Resources/Prototypes/DeviceLinking/source_ports.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static prototype review confirms `Toggle` is a registered sink and no other `Status` defaults exist. Prototype loading plus standard and RMC device-link auto-pairing are queued for the index-1999 checkpoint.
- Follow-up/debt: None.

## CS-0171 — Log makeshift stun-prod construction

- Upstream: [space-wizards/space-station-14#40709](https://github.com/space-wizards/space-station-14/pull/40709), `24753a78db1720b70dd4195e29a0887dd61b6a3c`, 2025-10-05
- Areas: Interactions, Gamerules
- Status: Ported
- Risk: Low
- Behavior/API delta: Completing a makeshift stun prod now emits a high-impact construction admin log. Merely starting or cancelling the fifteen-second assembly does not log a completed weapon.
- RMC/CMU divergence: CMU uses the retained standard stun-prod graph and construction logging action; no RMC-specific duplicate graph or alternate completion path is changed.
- Decision and rationale: Add the target-final completion action to close an audit gap for an improvised incapacitating weapon, matching the established logging policy for other dangerous constructions.
- Files changed: `Resources/Prototypes/Recipes/Crafting/Graphs/improvised/makeshiftstunprod.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static graph review confirms the log runs exactly once on the `start` to `msstunprod` completion edge with `High` impact. Graph deserialization and complete/cancelled construction cases are queued for the index-1999 checkpoint.
- Follow-up/debt: None.

## CS-0172 — Clamp portable heaters to their own limits

- Upstream: [space-wizards/space-station-14#40453](https://github.com/space-wizards/space-station-14/pull/40453), `80c66c02bedf47da0b96d4aec0594d3a286c1b74`, 2025-10-07
- Areas: Interactions, Physics
- Status: Ported
- Risk: Low
- Behavior/API delta: Portable-heater UI temperature changes now clamp the target against `SpaceHeaterComponent.MinTemperature` and `MaxTemperature`, rather than the broader limits of the attached generic gas thermo-machine.
- RMC/CMU divergence: `RMCSpaceHeater` is an independent item-toggle structure and does not use either the standard `SpaceHeater` or `GasThermoMachine` component, so its behavior is unchanged.
- Decision and rationale: Use the bounds owned by the UI-facing portable-heater component. Those values define the device's supported control range; accepting the generic machine range can set targets the portable heater was not designed to expose.
- Files changed: `Content.Server/Atmos/Portable/SpaceHeaterSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static data-flow review confirms only the clamp bounds change and power, mode, appearance, and UI dirtying remain intact. Server compilation plus requests below, within, and above both limits are queued for the index-1999 checkpoint.
- Follow-up/debt: None.

## CS-0173 — Move the ATS warp point clear of its wall

- Upstream: [space-wizards/space-station-14#40755](https://github.com/space-wizards/space-station-14/pull/40755), `0805943c9879352aced2b73e2414a4b0ec8ee06f`, 2025-10-08
- Areas: Movement, Interactions, Physics
- Status: Ported
- Risk: Low
- Behavior/API delta: The Automated Trade Station warp point moves two tiles east, from `(5.5, -4.5)` to `(7.5, -4.5)`, so wizard and administrative teleports arrive in open station space instead of inside the ATS wall.
- RMC/CMU divergence: The retained standard trading-outpost shuttle map is byte-compatible with this upstream hunk and has no RMC-specific override at the affected entity. No other shuttle entity or coordinate changes.
- Decision and rationale: Apply the exact target-final coordinate correction for warp-point entity 955. Moving only the destination marker fixes teleport placement without altering wall geometry, access, or the shuttle layout.
- Files changed: `Resources/Maps/Shuttles/trading_outpost.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static map review confirms entity 955 remains parented to grid 2 with the same `Automated Trade Station` warp identity and only its position changes. Map deserialization plus warp arrival and local collision checks are queued for the index-1999 checkpoint.
- Follow-up/debt: None.

## CS-0174 — Share the hostile faction with space adders

- Upstream: [space-wizards/space-station-14#37424](https://github.com/space-wizards/space-station-14/pull/37424), `3f115fa1d48e3da132119fb5f96ed8a776559a1a`, 2025-10-08
- Areas: Interactions, GameTicking
- Status: Ported
- Risk: Low
- Behavior/API delta: The standard space adder now belongs to both `Xeno` and `SimpleHostile`, preventing simple-hostile cobras from selecting it as an enemy while preserving its xeno-aligned relationships.
- RMC/CMU divergence: RMC xenonids use the separate `RMCXeno` faction and do not inherit `MobPurpleSnake`; their hive targeting and hostility rules are untouched.
- Decision and rationale: Add the single target-final faction membership to the affected NPC. The adder uses the simple-hostile HTN task, so sharing that faction aligns target selection with its behavior instead of causing hostile snakes to fight each other.
- Files changed: `Resources/Prototypes/Entities/Mobs/NPCs/xeno.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static prototype review confirms `MobPurpleSnake` retains `Xeno` and gains only `SimpleHostile`, with no RMC descendant. Prototype loading plus cobra/adder and adder/xeno hostility cases are queued for the index-1999 checkpoint.
- Follow-up/debt: None.

## CS-0175 — Correct PDA flashlight falloff

- Upstream: [space-wizards/space-station-14#40687](https://github.com/space-wizards/space-station-14/pull/40687), `250c1392fc590d9ef24c106320b32e06b4f86256`, 2025-10-03
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: PDA flashlights now use radius `1.7` and falloff `3`, replacing the shorter radius with an unspecified/default falloff. Their disabled initial state, softness, rotation, and toggle behavior remain unchanged.
- RMC/CMU divergence: `RMCAdminPDA` inherits the standard PDA light through `CentcomPDA` and has no point-light override, so it intentionally receives the same corrected beam profile. Other RMC lights are independent.
- Decision and rationale: Port the retained two-field visual correction at the shared PDA base so every derivative resolves one consistent flashlight profile.
- Files changed: `Resources/Prototypes/Entities/Objects/Devices/pda.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static inheritance review confirms the base and RMC admin PDA inherit radius `1.7` and falloff `3` without changing enabled state. Prototype loading and visual range checks are queued for the index-1999 checkpoint.
- Follow-up/debt: None.

## CS-0176 — Initialize HTN owners at component startup

- Upstream: [space-wizards/space-station-14#40244](https://github.com/space-wizards/space-station-14/pull/40244), `4b51b2953d780fba56fd9721b4a91f44d3f8fbfa`, 2025-10-07
- Areas: Movement, Interactions, GameTicking
- Status: Ported
- Risk: Medium
- Behavior/API delta: Every `HTNComponent` now records its entity in `NPCBlackboard.Owner` during component startup. Map initialization continues to wake the NPC, but no longer owns the identity initialization that post-map-init component additions would skip.
- RMC/CMU divergence: RMC HTN operators, including `LeapOperator`, read the same owner key and gain a valid identity when HTN is added dynamically. Their planning tasks, wake policy, and faction behavior are otherwise unchanged.
- Decision and rationale: Split identity setup from map lifecycle exactly as retained upstream. Component startup runs for both map-spawned and dynamically added HTN components, while waking remains a map-init concern.
- Files changed: `Content.Server/NPC/HTN/HTNSystem.cs`, `Content.Server/NPC/Systems/NPCSystem.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static event-order review confirms startup assigns the owner before map-init wakes a normal NPC and also covers post-map-init additions. Server compilation plus map-spawned, dynamically added, RMC leap, shutdown, and player attach/detach cases are queued for the index-1999 checkpoint.
- Follow-up/debt: The upstream full-game-save serialization TODOs are nonfunctional notes and remain outside this port.

## CS-0177 — Network magnet-pickup scan timing

- Upstream: [space-wizards/space-station-14#39988](https://github.com/space-wizards/space-station-14/pull/39988), `745c6d0edc2a1271431a4e797134de21fc331e52`, 2025-10-07
- Areas: Interactions, Physics, GameTicking
- Status: Ported
- Risk: Medium
- Behavior/API delta: `MagnetPickupComponent.NextScan` is now auto-networked and dirtied whenever the shared system advances its one-second scan deadline. Predicted clients receive the authoritative cadence instead of independently drifting or repeating pickup scans.
- RMC/CMU divergence: RMC motion and intel detectors have separate components and scan fields; no RMC prototype or system uses `MagnetPickupComponent`, so their refresh behavior is unchanged.
- Decision and rationale: Network only the mutable deadline and retain the existing shared scan algorithm, inventory-slot gate, range, storage, and physics checks. This is the minimal target-final prediction correction.
- Files changed: `Content.Shared/Storage/Components/MagnetPickupComponent.cs`, `Content.Shared/Storage/EntitySystems/MagnetPickupSystem.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static state-flow review confirms the component now generates network and pause state and every runtime deadline advance calls `Dirty`. Shared compilation plus server/client cadence, paused entities, containment, and full-storage cases are queued for the index-1999 checkpoint.
- Follow-up/debt: None.

## CS-0178 — Correct temperature-bolt collision and reflection

- Upstream: [space-wizards/space-station-14#37581](https://github.com/space-wizards/space-station-14/pull/37581), `92082f80914856cc608817d0449afb7430af99ab`, 2025-10-08; target-final collision state from [#37920](https://github.com/space-wizards/space-station-14/pull/37920), `1b62863e52f129dcc88386b508afbb41c741966b`, and [#40782](https://github.com/space-wizards/space-station-14/pull/40782), `df6307fe66f71944c5b3d5ed1e683a2723953181`
- Areas: Shooting, Physics
- Status: Adapted to target-final state
- Risk: Medium
- Behavior/API delta: Watcher and temperature-gun bolts now collide with opaque, impassable, and bullet-impassable fixtures; hot and cold bolts reflect from energy-reflective surfaces; the magmawing bolt inherits the watcher collision lifecycle; and watcher/magmawing shots use their intended muzzle flashes.
- RMC/CMU divergence: No RMC projectile or weapon references these standard projectile IDs. Existing RMC energy ammunition, collision masks, reflection policy, and effects are untouched.
- Decision and rationale: Port the final combined contract instead of #37581's transient removal of `Opaque`. Retaining all three collision bits covers windows and holographic creatures, while shared watcher inheritance prevents the magmawing variant from silently missing the same physics behavior.
- Files changed: `Resources/Prototypes/Entities/Objects/Weapons/Guns/Projectiles/projectiles.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static resolved-prototype review confirms watcher masks contain all three target bits, cold/hot bolts reflect `Energy`, and magmawing overrides only its sprite, projectile temperature behavior, and muzzle flash atop `WatcherBolt`. Prototype loading plus windows, walls, holos, reflectors, hot/cold inheritance, and muzzle effects are queued for the index-1999 checkpoint.
- Follow-up/debt: Revisit only if the later projectile fly-by fixture-anchor architecture is ported; CMU does not currently define that anchor.

## CS-0179 — Propagate pull-stop cancellation by reference

- Upstream: [space-wizards/space-station-14#40369](https://github.com/space-wizards/space-station-14/pull/40369), `e0fd44da662d74bfd3fdbbe6663d2f801252cd61`, 2025-09-15; side-branch hotfix [#40368](https://github.com/space-wizards/space-station-14/pull/40368), `9b5f9c3fd6aa400f47e9875cd7ba1f3ebb40e1fd`
- Areas: Movement, Interactions, Physics
- Status: Ported
- Risk: Medium
- Behavior/API delta: `AttemptStopPullingEvent` is now a by-reference local event, its cuff subscriber receives it by reference, and `TryStopPull` raises the same mutable instance by reference. A handcuffed actor's self-release cancellation now reaches the caller instead of being written to a copied struct.
- RMC/CMU divergence: The current pulling system contains RMC-specific behavior around the shared stop path, while CS-0144 already supplied the preceding cuff-state eligibility fix. This follow-up changes only event mutation transport and preserves all RMC stop/pull logic.
- Decision and rationale: Port the complete three-file event contract atomically. Changing only the subscriber or only the raise site would leave incompatible dispatch semantics; retaining pass-by-value silently defeats the cancellation guard.
- Files changed: the shared cuff handler, pulling event and pulling system, `docs/upstream-sync/inventory-wave-0005.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static event-flow review confirms the same `msg` instance is raised, mutated by the cuff handler, and checked before `StopPulling`. Shared compilation plus self-release, normal release, cancellable third-party release, prediction, and RMC fireman-carry cases are queued for the index-1999 checkpoint.
- Follow-up/debt: Add the upstream UX feedback for a blocked self-release only as a separate interaction design change.

## CS-0180 — Reject invalid chameleon-projector disguises

- Upstream: [space-wizards/space-station-14#40512](https://github.com/space-wizards/space-station-14/pull/40512), `8e9aa1dbb6121c4fdf04db51ebe79611f1f871c1`, 2025-09-23; side-branch hotfix [#40509](https://github.com/space-wizards/space-station-14/pull/40509), `add531a434aca51152f0d9f2df4749c67be712c9`
- Areas: Interactions, Physics
- Status: Ported
- Risk: Low
- Behavior/API delta: Chameleon projectors now reject targets carrying `Door` or `SubFloorHide` and targets tagged `Catwalk`, `Wall`, or `Window`. Existing bans on disguises, minds, and PDAs remain, preventing structural copies that become invisible or behave invalidly.
- RMC/CMU divergence: RMC has no separate chameleon-projector prototype or override. The five referenced standard tags/components are registered locally, and no RMC disguise behavior changes.
- Decision and rationale: Port the complete retained blacklist expansion from the merge's effective first-parent delta. These target types rely on structural rendering, subfloor, or door state that the hard-light disguise cannot safely reproduce.
- Files changed: `Resources/Prototypes/Entities/Objects/Devices/chameleon_projector.yml`, `docs/upstream-sync/inventory-wave-0005.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static whitelist review confirms all five target-final exclusions are present and existing exclusions remain. Prototype loading plus attempted disguises for each rejected family and a normal item control are queued for the index-1999 checkpoint.
- Follow-up/debt: Revisit blacklist breadth only alongside a deliberate chameleon-projector rendering/state refactor.

## CS-0181 — Prevent grinder material recycling

- Upstream: [space-wizards/space-station-14#39694](https://github.com/space-wizards/space-station-14/pull/39694), `bd05e10a2e9a11317de212a5708a4f1c4ba02307`, 2025-08-16; side-branch fix [#39690](https://github.com/space-wizards/space-station-14/pull/39690), `85158a3d4826a83e0e9ac3ad93d175a7a2995423`
- Areas: Chemistry, Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: `MaterialReclaimerComponent` now independently controls material and solution recovery, defaulting both switches to enabled. The industrial reagent grinder disables only material recovery, while still extracting solutions, so its `Extractable` whitelist cannot consume material output and feed it back into an infinite recycling loop.
- RMC/CMU divergence: No RMC-specific material-reclaimer prototype or system was found. The standard recycler retains both default recovery paths, and the industrial grinder keeps its CMU solution container, whitelist, blacklist, drainability, and efficiency settings.
- Decision and rationale: Port the complete effective first-parent merge delta atomically. Component defaults preserve all existing reclaimers; the single prototype opt-out fixes the affected machine without weakening chemical extraction or changing generic recycler behavior.
- Files changed: the shared reclaimer component, server reclaimer system, industrial reagent-grinder prototype, `docs/upstream-sync/inventory-wave-0003.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static control-flow and prototype review confirms material and solution recovery are separately guarded, both defaults remain enabled, and only `ReagentGrinderIndustrial` opts out of material recovery. Compilation, prototype loading, normal recycling, chemical extraction, and anti-loop cases are queued for the index-1999 checkpoint.
- Follow-up/debt: None.

## CS-0182 — Add the highly-illegal contraband contract

- Upstream: [space-wizards/space-station-14#39729](https://github.com/space-wizards/space-station-14/pull/39729), `ac0c1d518e895fa6863e37cd27220f5118e2032a`, 2025-08-18; side-branch feature [#38176](https://github.com/space-wizards/space-station-14/pull/38176), `9a4247c609e93925b66495cde9c56ee1a0d51f1e`
- Areas: Interactions, Gamerules
- Status: Adapted to target-final state
- Risk: Low
- Behavior/API delta: Contraband prototypes can now use the distinct `HighlyIllegal` severity directly or inherit `BaseHighlyIllegalContraband`. Its examination text uses the target-final crimson classification, and the retained magical-contraband text capitalization is corrected.
- RMC/CMU divergence: Existing RMC contraband severities and allowed-job/department rules are untouched. The new severity and abstract base have no consumers until separately accepted upstream item migrations are ported.
- Decision and rationale: Extract the retained data contract from the merge instead of replaying its obsolete Space Law guidebook wording. The severity, base prototype, and localization survive in the pinned target and form a prerequisite for xenoborg, ninja, and later highly-illegal item classifications.
- Files changed: contraband severity and base prototypes, English contraband localization, `docs/upstream-sync/inventory-wave-0003.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static prototype/localization review confirms the new severity resolves, the abstract base references it, existing severities remain unchanged, and the contract has no unresolved consumer. Prototype and localization loading are queued for the index-1999 checkpoint.
- Follow-up/debt: Port each retained item migration independently; do not bulk-reclassify RMC equipment without a CMU policy review.

## CS-0183 — Classify xenoborg equipment as highly illegal

- Upstream: [space-wizards/space-station-14#39856](https://github.com/space-wizards/space-station-14/pull/39856), `d699a4e985374c6c624d6ef9ccecf75c0ac86dc5`, 2025-09-24
- Areas: Interactions, Gamerules
- Status: Ported
- Risk: Low
- Behavior/API delta: Every prototype inheriting `BaseXenoborgContraband` now examines as `HighlyIllegal` instead of ordinary `Major` contraband. This covers xenoborg weapons, tools, modules, devices, and specialist equipment through their existing shared parent.
- RMC/CMU divergence: No `_RMC14` prototype inherits `BaseXenoborgContraband`; RMC marine and xenonid equipment classifications are unchanged. The port relies on the separately committed CS-0182 severity contract.
- Decision and rationale: Replace the explicit placeholder severity with the retained upstream classification at the common parent. This applies the policy consistently without touching each xenoborg item or altering allowed departments and jobs.
- Files changed: `Resources/Prototypes/Entities/Objects/base_contraband.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static inheritance review confirms all current xenoborg consumers resolve through the shared base, `HighlyIllegal` now exists locally, and no RMC consumer is affected. Prototype loading and legality examination are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1008 as `Ported (CS-0183)` when wave 0006 is committed.

## CS-0184 — Keep occupied rollerbeds visually folded out

- Upstream: [space-wizards/space-station-14#40550](https://github.com/space-wizards/space-station-14/pull/40550), `4ea0d517cf8312bef89c2db21eaceea09d8a0881`, 2025-09-26
- Areas: Medical, Interactions
- Status: Adapted
- Risk: Low
- Behavior/API delta: Rollerbed `unfoldedLayer` visibility is now driven by strap occupancy as well as folding state. Buckling hides the empty-bed layer and unbuckling restores it, while the folded-state visualizer continues to own the folded layer and suppresses the unfolded layer when packed.
- RMC/CMU divergence: `CMRollerBed` and `RMCMedevacStretcher` replace the parent's complete `GenericVisualizer` mapping, so they would not inherit the standard fix. Their two equivalent mappings receive the same state ownership while preserving medevac beacon/winch visuals and all RMC interaction restrictions.
- Decision and rationale: Port the target-final mapping to the standard rollerbed and adapt it to both RMC overrides. Leaving the old `FoldedVisuals.False` mapping active lets fold-state updates override an occupied stretcher's strap visual and expose the empty-bed sprite.
- Files changed: standard and RMC rollerbed prototypes and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static visualizer review confirms all three complete mappings hide `unfoldedLayer` when strapped, restore it when unstrapped, and retain the folded `True` suppression. Prototype loading plus folded, deployed, occupied, and medevac-state visuals are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1023 as `Ported (CS-0184)` when wave 0006 is committed.

## CS-0185 — Move Desoxyephedrine production to glasstle

- Upstream: [space-wizards/space-station-14#40638](https://github.com/space-wizards/space-station-14/pull/40638), `a9e272a6cfa8b1fdde9b3d2f3e4fc0930c9740c3`, 2025-10-01; produce parity follow-up [#40639](https://github.com/space-wizards/space-station-14/pull/40639), `faf8881a879dcf37e0c5354335649b9f9d30f8ec`
- Areas: Medical, Chemistry
- Status: Adapted
- Risk: Medium
- Behavior/API delta: Ambrosia vulgaris and ambrosia deus seeds and harvested produce no longer yield Desoxyephedrine. Glasstle now yields up to ten units from seed potency and contains ten units when spawned, with all three produce solution capacities reduced or expanded to their exact reagent totals.
- RMC/CMU divergence: CMU's ambrosia vulgaris produce uses `CMBicaridine` and `CMKelotane` and has an RMC `TimedDespawn`; both are preserved. No RMC reagent, hydroponics mutation, product identity, or lifecycle component is replaced by the upstream standard prototype state.
- Decision and rationale: Apply the seed migration and its immediate produce-parity correction as one coherent chemistry decision. Porting only one half would make harvested contents disagree with the plant's chemical production contract, while replacing whole upstream blocks would erase RMC medical reagents.
- Files changed: hydroponics seed and produce prototypes and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static solution accounting confirms vulgaris totals 14u, deus totals 10u, and glasstle totals 25u; Desoxyephedrine is removed from both ambrosia seed/produce pairs and added to glasstle's pair. Prototype loading, growth yield, grinding, and RMC despawn behavior are queued for the index-1999 checkpoint.
- Follow-up/debt: Record indices 1065 and 1066 as `Ported (CS-0185)` when wave 0006 is committed.

## CS-0186 — Prefer network configuration on wireless devices

- Upstream: [space-wizards/space-station-14#38938](https://github.com/space-wizards/space-station-14/pull/38938), `37ee54621a492bac36e581d23f0e72a6c5e52763`, 2025-10-01
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: When a multitool target supports both device linking and device networking, automatic mode selection now prefers networking so the device can be saved or configured wirelessly. An already-active linking session remains active through the existing early-return guard, and link-only targets still select linking mode.
- RMC/CMU divergence: RMC sentries, laptops, requisitions, faxes, and doors reuse the standard device-network components and configurator system. No RMC network identifiers, power requirements, access checks, or linking ports change; mixed-capability RMC devices receive the same mode-selection rule.
- Decision and rationale: Port the retained ordering change without altering the surrounding interaction flow. Checking networking before falling back to linking exposes wireless configuration while preserving explicit link-mode continuity.
- Files changed: `Content.Server/DeviceNetwork/Systems/NetworkConfiguratorSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static branch review covers networking-only, linking-only, mixed-capability, device-list, and active-link-mode targets; only mixed targets outside an active linking session choose differently. Server compilation and multitool interaction cases are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1069 as `Ported (CS-0186)` when wave 0006 is committed.

## CS-0187 — Prioritize reaction mixing over utensil use

- Upstream: [space-wizards/space-station-14#40704](https://github.com/space-wizards/space-station-14/pull/40704), `326eaad18dc784e66970d0cb04bcd360324f2e9f`, 2025-10-05
- Areas: Chemistry, Interactions
- Status: Adapted
- Risk: Low
- Behavior/API delta: A reaction mixer used on a mixable solution now runs before the current utensil handler and marks the interaction handled after starting its mixing do-after. Spoon-like mixers no longer fall through into feeding or drinking behavior on the same click.
- RMC/CMU divergence: CMU retains the older `UtensilSystem` interaction boundary instead of the target's later `IngestionSystem`, so event ordering is adapted to that local consumer. RMC foods and utensils continue using their existing feeding rules whenever the reaction mixer declines the target.
- Decision and rationale: Port all three parts of the retained fix together: ordering alone would still allow fallthrough, and setting handled without first verifying a mixable target would suppress legitimate utensil use. The discarded first probe result was never consumed.
- Files changed: `Content.Server/Chemistry/EntitySystems/ReactionMixerSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static event-flow review confirms failed reach/mix checks leave the event untouched, a valid mix starts the existing do-after and claims the click, and `UtensilSystem` observes `Handled`. Server compilation plus mixable, non-mixable, cancelled, and RMC food targets are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1110 as `Ported (CS-0187)` when wave 0006 is committed; revisit the ordering type only when the later shared ingestion migration is integrated.

## CS-0188 — Preserve the martyr module's self-destruct tool

- Upstream: [space-wizards/space-station-14#40224](https://github.com/space-wizards/space-station-14/pull/40224), `3764a719bfcd444b050639a57a06d593136c91f8`, 2025-10-03
- Areas: Interactions, Physics, Gamerules
- Status: Ported
- Risk: Medium
- Behavior/API delta: `SelfDestructSeq`, the virtual tool supplied by the martyr cyborg module, is no longer deleted by its explosion and no longer latches its `ExplosiveComponent` into a permanently exploded state. The supplied tool remains a valid module item after activation and can be triggered again if its cyborg survives.
- RMC/CMU divergence: RMC has no separate martyr module or `SelfDestructSeq` override. RMC grenade explosion types, shrapnel, deletion policy, and reusable explosives are untouched; only the standard hidden cyborg tool changes.
- Decision and rationale: Port both retained explosive flags together. Preventing deletion alone would leave an inert virtual item, while repeatability alone would still allow the trigger path to delete it and invalidate the module-provided hand item.
- Files changed: `Resources/Prototypes/Entities/Objects/Weapons/Throwable/grenades.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static prototype/component review confirms both fields exist in CMU's current explosion contract, the blast intensity and timer remain unchanged, and only `BorgModuleMartyr` provides this entity. Prototype loading plus activation, module-hand cycling, survival, and repeat-trigger cases are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1093 as `Ported (CS-0188)` when wave 0006 is committed.

## CS-0189 — Give the Ian suit a bark accent

- Upstream: [space-wizards/space-station-14#40694](https://github.com/space-wizards/space-station-14/pull/40694), `d9b296a64049343ff5bd493310bee381d5750588`, 2025-10-05
- Areas: Interactions
- Status: Adapted
- Risk: Medium
- Behavior/API delta: Equipping `ClothingOuterSuitIan` now adds `BarkAccent` to the wearer and removing it removes the accent when this clothing supplied it. The hood, construction graph, step-trigger protection, and chameleon/corgi tags remain unchanged.
- RMC/CMU divergence: The pinned target later expresses this behavior through a relay-capable `BarkAccent` component, but CMU retains the older `AddAccentClothing` contract. The suit uses that established local pattern; RMC parasite clothing and RMC speech/accent systems are untouched.
- Decision and rationale: Adapt the retained player behavior to the available clothing-accent API instead of importing the later speech relay architecture as an incidental dependency. `BarkAccent` is registered locally and already used by another clothing prototype.
- Files changed: `Resources/Prototypes/Entities/Clothing/OuterClothing/suits.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static prototype/system review confirms the component resolves, applies only while equipped when it owns the accent, and does not alter the suit's existing components. Prototype loading plus equip, unequip, pre-existing accent, and clone cases are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1103 as `Ported (CS-0189)` when wave 0006 is committed. Reconcile this suit with the target's later inventory-relay accent architecture; the current cloning settings can preserve a clothing-supplied bark accent on a clone.

## CS-0190 — Let gorillas pull without hands

- Upstream: [space-wizards/space-station-14#40700](https://github.com/space-wizards/space-station-14/pull/40700), `f1e5d1eb07d0cf943f8addfff21371c13a056d7c`, 2025-10-07
- Areas: Movement, Interactions, Physics
- Status: Ported
- Risk: Low
- Behavior/API delta: Standard gorillas now have `Puller` with `needsHands: false`, allowing both AI- and player-controlled gorillas to begin and maintain pull relationships despite having no hands component.
- RMC/CMU divergence: RMC has no gorilla override or descendant. Existing handless RMC pullers, including xenonids and power loaders, keep their own component configuration and are unaffected.
- Decision and rationale: Add the retained capability directly to `MobGorilla`. The current pull system already supports handless pullers, and the gorilla's interaction-capable HTN and force-prying behavior establish that moving objects is intentional rather than a humanoid-hands dependency.
- Files changed: `Resources/Prototypes/Entities/Mobs/NPCs/animals.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static inheritance review confirms the component is added only to `MobGorilla`, uses an established local data contract, and does not change its faction, HTN, damage, or movement values. Prototype loading plus pull start, movement, release, incapacitation, and AI cases are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1147 as `Ported (CS-0190)` when wave 0006 is committed.

## CS-0191 — Clear residual blindness when its permanent source is removed

- Upstream: [space-wizards/space-station-14#40517](https://github.com/space-wizards/space-station-14/pull/40517), `8e3243a15648077aad082d5fc299e71d5267defe`, 2025-10-09
- Areas: Medical, Gamerules
- Status: Ported
- Risk: Medium
- Behavior/API delta: Removing `PermanentBlindnessComponent` now clears both its minimum eye-damage floor and the accumulated eye damage. Changelings transforming away from a blind form no longer remain ordinarily blind and require oculine despite losing the permanent-blindness source.
- RMC/CMU divergence: No RMC prototype or system directly uses `PermanentBlindnessComponent`; RMC eye damage, vision, and medical reagents remain unchanged. Standard trait removal and any future component-removal path receive the same cleanup.
- Decision and rationale: Port only the retained medical cleanup call and explanatory context, leaving unrelated formatting and dependency changes out. Resetting the minimum alone removes permanence but preserves the damage that permanence forced to its maximum.
- Files changed: `Content.Shared/Traits/Assorted/PermanentBlindnessSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static shutdown-flow review confirms the cleanup runs only when the blindness component is removed and a `BlindableComponent` remains, after lowering its minimum damage. Shared compilation plus blind-form transformation, direct trait removal, normal eye damage, and oculine cases are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1205 as `Ported (CS-0191)` when wave 0007 is committed.

## CS-0192 — Scan past obstructions for singularity containment

- Upstream: [space-wizards/space-station-14#39593](https://github.com/space-wizards/space-station-14/pull/39593), `766c2b875948851c1944fd22275b267d0b1131d0`, 2025-10-10
- Areas: Physics, GameTicking
- Status: Ported
- Risk: Medium
- Behavior/API delta: A singularity generator's failsafe raycast now skips non-containment hits and continues to the first containment field within range. An unrelated fixture in the ray result set no longer makes the generator report that a valid static field is missing.
- RMC/CMU divergence: RMC has no singularity-generator or containment-field override. Fork maps and machinery are untouched; any standard containment setup used by CMU receives the corrected query behavior.
- Decision and rationale: Move the loop break onto the successful containment-field path exactly as retained upstream. The ray is intentionally filtered by component after physics intersection, so stopping on a hit that fails that filter defeats the scan.
- Files changed: `Content.Server/Singularity/EntitySystems/SingularityGeneratorSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static loop review confirms empty/no-field results still fail, unrelated hits are skipped, the first containment hit is selected, and the existing static-body check remains authoritative. Server compilation plus clear, obstructed, movable, out-of-range, and rotated-grid containment cases are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1227 as `Ported (CS-0192)` when wave 0007 is committed.

## CS-0193 — Apply the configured votekick cooldown in seconds

- Upstream: [space-wizards/space-station-14#40622](https://github.com/space-wizards/space-station-14/pull/40622), `6fbcc6d0fb797651fba29ded7ad126b8aeb7176f`, 2025-10-15
- Areas: GameTicking, Gamerules
- Status: Ported
- Risk: Low
- Behavior/API delta: `votekick.timeout`, documented and defaulted in seconds, now controls both the initiator's cooldown and the same-type votekick timeout using seconds. The default `60` no longer becomes a sixty-minute initiator cooldown, and votekicks no longer fall back to the generic same-vote timeout.
- RMC/CMU divergence: CMU defines no fork-specific override for either timeout and uses the standard vote manager. Votekick eligibility, admin-online policy, thresholds, webhooks, and RMC round/game rules are unchanged.
- Decision and rationale: Port the unit correction and optional timeout override together. Fixing only the `VoteOptions` field would leave the server-wide votekick gate on a different CVar; overriding all standard votes would incorrectly change restart and preset cooldowns.
- Files changed: `Content.Server/Voting/Managers/VoteManager.DefaultVotes.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static call-site review confirms ordinary standard votes keep `vote.same_type_timeout`, while votekicks use `votekick.timeout` consistently for both gates. Server compilation plus default, custom, repeated, failed, admin-blocked, and non-votekick vote cases are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1364 as `Ported (CS-0193)` when wave 0007 is committed.

## CS-0194 — Clear completed magic-mirror interactions

- Upstream: [space-wizards/space-station-14#40329](https://github.com/space-wizards/space-station-14/pull/40329), `6aa0812fa25fde4d20d4287132308a406aa0ab0b`, 2025-10-13
- Areas: Interactions, GameTicking
- Status: Ported
- Risk: Low
- Behavior/API delta: Every magic-mirror do-after callback now clears the component's stored do-after identifier before handling success, cancellation, invalid-target, or already-handled exits. A finished barber action can no longer leave a stale identifier that is cancelled again by the next action.
- RMC/CMU divergence: CMU uses the upstream magic-mirror system and shared do-after component without a fork override. RMC hairstyles, species markings, clothing checks, timing, sounds, and UI behavior are unchanged.
- Decision and rationale: Port the four callback resets together because all four actions share the same single active-operation field. Clearing only successful callbacks would preserve the cancellation failure; clearing only one action would leave the same bug in the other paths.
- Files changed: `Content.Server/MagicMirror/MagicMirrorSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static callback review confirms the stored identifier is cleared before every early return while active-operation replacement still cancels the prior identifier before starting a new action. Server compilation plus completed, cancelled, invalid-target, repeated, and cross-action barber cases are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1313 as `Ported (CS-0194)` when wave 0007 is committed.

## CS-0195 — Guard wrapped-parcel interaction verbs

- Upstream: [space-wizards/space-station-14#40838](https://github.com/space-wizards/space-station-14/pull/40838), `b78bfded443ecf4f9f0ee6b6952b1cf4db318133`, 2025-10-11
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: The unwrap interaction verb now requires complex-interaction capability and is withheld from an actor contained inside the parcel. Simple animals cannot unwrap parcels through the verb path, and packaged actors cannot unwrap their own container from within it.
- RMC/CMU divergence: CMU retains upstream parcel containers and verb handling with no fork-specific override. RMC inventory, hand, container, and do-after behavior is unchanged, including ordinary use-in-hand unwrapping for an actor that can actually hold the parcel.
- Decision and rationale: Port the upstream verb guards at the presentation boundary. This prevents invalid actions before the verb is exposed while preserving destruction-based unwrapping and legitimate hand use.
- Files changed: `Content.Shared/ParcelWrap/Systems/ParcelWrappingSystem.WrappedParcel.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static path review confirms access and complex-interaction checks run before verb capture, the containment query uses the parcel's authoritative slot, and normal external users still receive the verb. Shared compilation plus human, simple-animal, contained-user, inaccessible, and destroyed-parcel cases are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1268 as `Ported (CS-0195)` when wave 0007 is committed.

## CS-0196 — Hide projection activation on Station AI cores

- Upstream: [space-wizards/space-station-14#39937](https://github.com/space-wizards/space-station-14/pull/39937), `3df66219d6d3be46e1a167ab18dcd3d47f440637`, 2025-10-10
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: A holopad entity that is also a Station AI core no longer exposes the alternative verb for activating its own projector. Remote holopads continue to expose that verb when powered, available, and used by a valid AI.
- RMC/CMU divergence: CMU retains the standard Station AI core/holopad composition and has no RMC override in this verb path. AI holder, telephone, power, hologram, and remote projection behavior are unchanged.
- Decision and rationale: Port the component guard before telephone and user eligibility work. A core is a projection source/controller, not a remote destination, so suppressing the impossible verb is safer than allowing it to enter the later activation path.
- Files changed: `Content.Server/Holopad/HolopadSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static verb-flow review confirms only entities carrying `StationAiCoreComponent` return early and ordinary holopads retain all existing power, engagement, held-AI, and control-lock checks. Server compilation plus core, powered remote, unpowered remote, engaged, and non-AI-user cases are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1247 as `Ported (CS-0196)` when wave 0007 is committed.

## CS-0197 — Isolate research server lookup state

- Upstream: [space-wizards/space-station-14#40917](https://github.com/space-wizards/space-station-14/pull/40917), `68f9d748a2f4398a9d60bc42e19e272b7162c358`, 2025-10-15
- Areas: GameTicking
- Status: Ported
- Risk: Low
- Behavior/API delta: Research server discovery now returns a fresh set for each call instead of clearing and reusing one static mutable set. Concurrent or nested lookups can no longer modify a collection while another caller enumerates it; first-server registration and name/id projections consume the per-call result directly.
- RMC/CMU divergence: CMU uses the upstream research client/server systems and has no RMC-specific replacement for grid lookup. Research point generation, technology databases, recipes, access checks, and console synchronization are unchanged.
- Decision and rationale: Port the complete lookup cleanup because every adjusted caller depended on the shared scratch set's lifetime. Per-call allocation establishes ownership clearly and removes the cross-call race without introducing locks around game-state queries.
- Files changed: `Content.Server/Research/Systems/ResearchSystem.cs`, `Content.Server/Research/Systems/ResearchSystem.Client.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static ownership review confirms no static mutable lookup collection remains, gridless clients receive an independent empty set, and all callers enumerate only their own result. Server compilation plus simultaneous UI, map-init, re-anchor, gridless, and multiple-server cases are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1368 as `Ported (CS-0197)` when wave 0007 is committed.

## CS-0198 — Predict destructible entity deletion

- Upstream: [space-wizards/space-station-14#40856](https://github.com/space-wizards/space-station-14/pull/40856), `11525673ba352cdbe0edbafd06c3749d39151ed8`, 2025-10-12
- Areas: Interactions, GameTicking
- Status: Ported
- Risk: Medium
- Behavior/API delta: `SharedDestructibleSystem.DestroyEntity` now queues deletion through the prediction-aware lifecycle. Client-predicted destruction can remove the entity immediately while retaining the engine's rollback/reconciliation semantics, instead of waiting on an ordinary authoritative queue deletion.
- RMC/CMU divergence: RMC calls the shared destruction API from fork gameplay but does not override its deletion implementation. Destruction-attempt cancellation, destruction events, threshold behaviors, drops, and CMU projectile/damage rules are unchanged.
- Decision and rationale: Port only the behavioral line from the upstream commit and omit its unrelated XML-comment formatting. The shared system already uses predicted events and CMU's pinned engine exposes `PredictedQueueDel` throughout shared gameplay code.
- Files changed: `Content.Shared/Destructible/SharedDestructibleSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static lifecycle review confirms cancellation still occurs before the destruction event and deletion, event order is unchanged, and only the queueing primitive changes. Shared compilation plus predicted destruction, cancellation, server authority, rollback, repeated-call, and RMC threshold cases are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1296 as `Ported (CS-0198)` when wave 0007 is committed.

## CS-0199 — Make standard SmartFridges airtight

- Upstream: [space-wizards/space-station-14#40196](https://github.com/space-wizards/space-station-14/pull/40196), `931a3dd8ddba39bedf95d9be457f6f1f89bf1408`, 2025-10-13
- Areas: Physics, Chemistry
- Status: Ported
- Risk: Low
- Behavior/API delta: The standard `SmartFridge` prototype now participates in tile airtightness, preventing atmosphere from flowing through its solid machine fixture. The component is attached directly to the current pre-refactor SmartFridge prototype rather than depending on later construction-machine changes.
- RMC/CMU divergence: `RMCSmartFridge` is a separate RMC smart chemical storage prototype with its own fixture and gameplay system; it is intentionally unchanged. Standard SmartFridge storage, damage thresholds, lighting, advertisements, and contents behavior remain fork-compatible.
- Decision and rationale: Port the independent physical component despite the current prototype predating upstream's larger SmartFridge rework. Airtightness depends only on the existing anchored static fixture, which the CMU prototype already supplies.
- Files changed: `Resources/Prototypes/Entities/Structures/Machines/smartfridge.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static prototype review confirms `Airtight` is added only to `SmartFridge`, the fixture remains static and machine-layered, and RMC smart storage is untouched. YAML validation plus atmosphere flow, open/closed storage, destruction, inherited prototype, and RMC fridge cases are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1318 as `Ported (CS-0199)` when wave 0007 is committed; separately audit the prerequisite standard SmartFridge rework before adopting its construction and vending behavior.

## CS-0200 — Scope air-sensor tags to the concrete sensor

- Upstream: [space-wizards/space-station-14#36326](https://github.com/space-wizards/space-station-14/pull/36326), `1e14b94da66b1a7f75e1015f8bf58581bac0f41b`, 2025-10-14
- Areas: Interactions, Physics
- Status: Ported
- Risk: Low
- Behavior/API delta: `AirSensor` and `ForceFixRotations` tags now live on the concrete `AirSensor` prototype instead of the shared `AirSensorBase`. Gas vents, scrubbers, and other devices that inherit the network-monitoring base no longer masquerade as buildable air sensors or inherit the sensor-only rotation repair tag.
- RMC/CMU divergence: CMU inherits the standard atmospheric prototype graph and retains RMC-specific atmos devices separately. Device networking, monitoring thresholds, construction graphs, vent/scrubber-specific tags, and map prototypes are unchanged.
- Decision and rationale: Port the tag relocation as an inseparable remove/add pair. The abstract base represents monitoring capability, while the two tags describe only the concrete wall/floor sensor's construction and transform behavior.
- Files changed: `Resources/Prototypes/Entities/Structures/Specific/Atmospherics/sensor.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static inheritance review confirms concrete `AirSensor` keeps both tags, `AirSensorBase` keeps monitoring components, and multi-parent vents/scrubbers no longer inherit sensor identity. YAML validation plus construction, rotation repair, special vent, scrubber, gas-pipe sensor, and map-load cases are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1345 as `Ported (CS-0200)` when wave 0007 is committed.

## CS-0201 — Log deployable-turret configuration changes

- Upstream: [space-wizards/space-station-14#40884](https://github.com/space-wizards/space-station-14/pull/40884), `e92b48c1fa90b95bf694feeba2d2bc97618f2efe`, 2025-10-14
- Areas: Shooting, Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: Deployable-turret controllers now emit medium-impact `ItemConfigure` admin logs when a user changes armament state or enables/disables each access exemption. Logs identify the actor, controller, requested state, and affected access prototype before the device-network update is broadcast.
- RMC/CMU divergence: This is the standard deployable turret controller, not RMC vehicle hardpoints or fork sentry systems. RMC weapon targeting, ammunition, IFF, access semantics, network payloads, and actual turret firing behavior are unchanged.
- Decision and rationale: Port logging at the authoritative controller methods so UI-originated changes share one audit point and the recorded values match the payload. One access log per requested exemption preserves useful granularity.
- Files changed: `Content.Server/TurretController/DeployableTurretControllerSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static flow review confirms logging occurs only after required network/targeting components resolve, before the unchanged packet queue, and covers both armament and access paths. The index-1999 solution build caught the injected admin logger being declared `readonly`; removing that modifier restores the established dependency-injection contract and clears analyzer RA0051. Armament, single/multiple exemption, missing-device, null-user, and RMC turret cases remain covered by the checkpoint integration suite.
- Follow-up/debt: Record index 1361 as `Ported (CS-0201)` when wave 0007 is committed.

## CS-0202 — Read power sensors from their selected cable network

- Upstream: [space-wizards/space-station-14#40934](https://github.com/space-wizards/space-station-14/pull/40934), `b10dd2edca91c99c6590ff974edca2414a7d5b36`, 2025-10-16
- Areas: GameTicking, Interactions
- Status: Ported
- Risk: Medium
- Behavior/API delta: A power sensor now reads statistics directly from its configured `CableDeviceNode.NodeGroup` instead of walking reachable cable nodes and using the first grouped node encountered. Its charging/discharging output therefore follows the selected electrical network even when the sensor can reach multiple networks.
- RMC/CMU divergence: CMU uses the standard power sensor and device-linking system; RMC's area-power abstraction is separate and is not modified. Port selection, input/output mode, signal ports, cable topology, and network-statistics calculations remain unchanged.
- Decision and rationale: Port the direct-node lookup and remove the now-unnecessary reachable-node loop as one change. The selected node is already authoritative for the sensor configuration, while traversal order cannot reliably identify the intended network. CMU's newer tuple-based cable traversal adaptation disappears with that obsolete traversal.
- Files changed: `Content.Server/DeviceLinking/Systems/PowerSensorSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static update-flow review confirms gridless or ungrouped sensors still return safely, input/output statistics select the same fields, and transition comparisons and signals are unchanged. Server compilation plus one-network, multi-network, node-switch, gridless, charging, discharging, and steady-state cases are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1378 as `Ported (CS-0202)` when wave 0007 is committed.

## CS-0203 — Preserve forensic evidence when using rags

- Upstream: [space-wizards/space-station-14#40818](https://github.com/space-wizards/space-station-14/pull/40818), `86880a31942c54b7a092e0418185925eb3804d12`, 2025-10-18
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: `RagItem` no longer carries `CleansForensics`. It can still absorb solutions and clean ordinary mop-compatible messes, but it no longer removes fingerprints, DNA, fibers, or other forensic traces through the dedicated forensic-cleaning path.
- RMC/CMU divergence: RMC supply crates reference the same standard rag, so they receive this correction without changing RMC mop, cleaner, evidence, or janitorial prototypes. Soap and purpose-built forensic cleaners retain their components.
- Decision and rationale: Port the isolated component removal exactly. A common rag is intentionally a low-tier cleaning tool, while forensic erasure remains an explicit capability on specialized items.
- Files changed: `Resources/Prototypes/Entities/Objects/Specific/Janitorial/janitor.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static prototype review confirms only `RagItem` loses `CleansForensics`, its Mop tag and absorbent solution remain, and soaps retain forensic cleaning. YAML validation plus ordinary cleaning, absorption, fingerprints, DNA, fibers, soap, and RMC crate-spawn cases are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1404 as `Ported (CS-0203)` when wave 0008 is committed.

## CS-0204 — Resolve the Space Villain tie message

- Upstream: [space-wizards/space-station-14#40958](https://github.com/space-wizards/space-station-14/pull/40958), `68ea91d070d24d626f4b485acd74b3b496c598dd`, 2025-10-18
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: The simultaneous player/enemy death branch now requests the actual `space-villain-game-enemy-dies-with-player-message` localization key. Removing the trailing space prevents a missing-localization result and displays the enemy name in the intended tie text.
- RMC/CMU divergence: RMC arcade prototypes reuse the standard Space Villain game component, so the display fix applies without changing RMC arcade rewards, sprites, power, or map placement. The localization resource already exists unchanged.
- Decision and rationale: Port the exact one-character key correction. The branch and localization contract are otherwise identical, and no fallback or new string is needed.
- Files changed: `Content.Server/Arcade/SpaceVillainGame/SpaceVillainGame.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static key lookup confirms the corrected identifier exactly matches the existing Fluent message and still supplies `enemyName`. Server compilation plus simultaneous-death, player-loss, enemy-death, localization, and RMC arcade cases are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1407 as `Ported (CS-0204)` when wave 0008 is committed.

## CS-0205 — Guard AME estimates when no cores exist

- Upstream: [space-wizards/space-station-14#41026](https://github.com/space-wizards/space-station-14/pull/41026), `04a2c2e9685dc78c4eebc61442ea6661fe571b91`, 2025-10-21
- Areas: Physics, GameTicking, Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: The AME controller calculates target output only when its node group contains at least one core. An empty group now reports zero cores and zero targeted power instead of passing a zero core count into the power formula and exposing NaN or infinity in the UI.
- RMC/CMU divergence: CMU uses the standard AME controller and node group with no RMC replacement in this calculation. Injection controls, containment, fuel use, power-net supply, core discovery, and active AME simulation are unchanged.
- Decision and rationale: Port the single guard at the UI-state calculation boundary. This preserves the existing no-group behavior and avoids inventing a value inside the shared power formula for a physically absent generator.
- Files changed: `Content.Server/Ame/EntitySystems/AmeControllerSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static branch review confirms populated groups still calculate with their real core count and empty or missing groups retain zero-initialized output. Server compilation plus zero-core, one-core, multi-core, node-loss, injection-change, and UI refresh cases are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1474 as `Ported (CS-0205)` when wave 0008 is committed.

## CS-0206 — Correct the .50 uranium projectile sprite

- Upstream: [space-wizards/space-station-14#41068](https://github.com/space-wizards/space-station-14/pull/41068), `737a4f308eddb26b5bcbadef859c6f310feedb80`, 2025-10-26
- Areas: Shooting
- Status: Ported
- Risk: Low
- Behavior/API delta: `PelletShotgunUranium` now renders with the animated green `uranium` state and an unshaded layer instead of the static `depleted-uranium` state. Projectile damage, spread, collision, range, and ammunition behavior are unchanged.
- RMC/CMU divergence: This is the standard .50 uranium shotgun pellet; RMC ammunition and projectile prototypes remain separate and untouched. CMU's retained `projectiles2.rsi` already contains the target animated state, so no binary resource port is required.
- Decision and rationale: Port the exact sprite-layer correction because the referenced state exists in the current RSI metadata. The unshaded shader preserves the intended emissive visibility without altering projectile mechanics.
- Files changed: `Resources/Prototypes/Entities/Objects/Weapons/Guns/Ammunition/Projectiles/shotgun.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static prototype/RSI review confirms the `uranium` state exists, the layer schema matches other projectiles, and the spread child still inherits the corrected sprite. YAML validation plus direct pellet, spread shell, lighting, collision, and RMC ammunition cases are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1535 as `Ported (CS-0206)` when wave 0008 is committed.

## CS-0207 — Spill water cups when worn

- Upstream: [space-wizards/space-station-14#41148](https://github.com/space-wizards/space-station-14/pull/41148), `85f607f1e67e398df169e21f5b27a1ec4e1daabd`, 2025-10-27
- Areas: Chemistry, Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: Wearing `DrinkWaterCup` in the head slot now drains its `drink` solution into a splash at the wearer's position and blocks access to that solution while the empty cup remains worn. Empty cups continue to function as the novelty water-cup hat.
- RMC/CMU divergence: CMU's older water-cup prototype already exposes the same head clothing slot and `drink` solution, while the shared spill component/system is present. RMC-specific drinkware and clothing remain untouched.
- Decision and rationale: Add the retained `SpillWhenWorn` component directly to the current prototype rather than importing upstream's broader drink-parent refactor. The component's existing solution contract matches CMU's cup exactly.
- Files changed: `Resources/Prototypes/Entities/Objects/Consumable/Drinks/drinks_cups.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static component review confirms the configured solution is `drink`, the cup remains head-wearable, and the shared system drains before marking it worn. YAML validation plus filled, empty, equip, unequip, refill-while-worn, spill placement, and RMC drinkware cases are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1549 as `Ported (CS-0207)` when wave 0008 is committed.

## CS-0208 — Phase the artifact instead of its effect node

- Upstream: [space-wizards/space-station-14#41160](https://github.com/space-wizards/space-station-14/pull/41160), `39fc0052a44c0fb6a3aeab628b7791c772a6d66e`, 2025-10-28
- Areas: Physics, Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: The remove-collision artifact effect now resolves fixtures from `args.Artifact` and marks those fixtures non-hard. The effect-node entity remains configuration/state only; activation actually phases the physical artifact as intended.
- RMC/CMU divergence: CMU retains the standard xenoartifact node/effect architecture and has no RMC override for this effect. Artifact triggers, node discovery, fixture shapes, collision masks/layers, and other RMC physics systems are unchanged.
- Decision and rationale: Port both owner substitutions together. Looking up fixtures on the effect node normally returns none, and mixing the artifact's fixtures with the node owner would pass inconsistent entity/component ownership into physics.
- Files changed: `Content.Shared/Xenoarchaeology/Artifact/XAE/XAERemoveCollisionSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static ownership review confirms fixture lookup and `SetHard` now use the same artifact entity while iteration and false-hard state remain unchanged. Shared compilation plus artifact-with-fixtures, missing-fixtures, multi-fixture, repeated activation, movement/collision, and RMC physics cases are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1559 as `Ported (CS-0208)` when wave 0008 is committed.

## CS-0209 — Reset limited charges on rejuvenation

- Upstream: [space-wizards/space-station-14#41165](https://github.com/space-wizards/space-station-14/pull/41165), `5cbc1cba48ba12067b7a3053393f9cdc4654331b`, 2025-10-30
- Areas: Medical, Interactions, GameTicking
- Status: Ported
- Risk: Low
- Behavior/API delta: Entities with `LimitedChargesComponent` now subscribe to `RejuvenateEvent` and reset to their configured maximum charges using the existing charge API. The reset also restores the recharge timestamp and dirties state through the established path.
- RMC/CMU divergence: CMU and RMC already use limited charges for standard actions and fork abilities, and several RMC systems perform explicit charge resets for their own state transitions. Those specialized flows remain unchanged; this adds the missing general rejuvenation contract.
- Decision and rationale: Route rejuvenation through `ResetCharges` rather than assigning fields in the handler. This preserves auto-recharge accounting, networking, maximum-charge configuration, and the existing no-op when already full.
- Files changed: `Content.Shared/Charges/Systems/SharedChargesSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static event/API review confirms one subscription is added, reset resolves the existing component, and action attempts/consumption/recharge behavior is unchanged. Shared compilation plus empty, partial, full, auto-recharging, repeated rejuvenation, standard action, and RMC charge cases are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1575 as `Ported (CS-0209)` when wave 0008 is committed.

## CS-0210 — Register the cut-wire roundstart variation

- Upstream: [space-wizards/space-station-14#41191](https://github.com/space-wizards/space-station-14/pull/41191), `e74e0b5c03009bacdc6581573e740081cf022d5b`, 2025-10-30
- Areas: GameTicking, Gamerules, Interactions
- Status: Ported
- Risk: Medium
- Behavior/API delta: `BasicRoundstartVariation` now includes `CutWireVariationPass`. On standard station presets, the existing pass may mark eligible wired devices at its configured one-percent chance, capped at twenty, so their established map-init handler cuts one random wire.
- RMC/CMU divergence: The change affects presets that schedule the standard `BasicRoundstartVariation`; RMC distress-signal rules do not reference that rule in their fork prototype set. The pass already blacklists particle-accelerator control boxes, and its systems/components are present unchanged.
- Decision and rationale: Port only the target-final one-line registration. Although the upstream PR title mentions a CVar, that part was reverted before merge and is not in the pinned commit delta; inventing a fork-only gate here would not match upstream.
- Files changed: `Resources/Prototypes/GameRules/roundstart.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static rule-graph review confirms the referenced prototype and handler exist, the pass appears once, and RMC rules do not explicitly schedule `BasicRoundstartVariation`. YAML validation plus standard roundstart, eligibility, blacklist, cap, random selection, map-init ordering, and RMC preset cases are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1573 as `Ported (CS-0210)` when wave 0008 is committed; consider a CMU-specific enable CVar separately only if operators want policy control beyond pinned upstream behavior.

## CS-0211 — Make suffocation damage bypass resistances

- Upstream: [space-wizards/space-station-14#41556](https://github.com/space-wizards/space-station-14/pull/41556), `5b0730b9138fafc017121e406537baac1c52bf05`, 2025-11-24
- Areas: Medical
- Status: Ported
- Risk: Medium
- Behavior/API delta: Periodic damage from the respirator system now calls `TryChangeDamage` with `ignoreResistances: true`. Suffocation therefore applies its configured damage directly instead of being reduced or transformed by the target's damage modifier set and pre-resistance damage-modification events.
- RMC/CMU divergence: CMU retains RMC's extended `TryChangeDamage` parameters for armor penetration and claw logic, but its leading `ignoreResistances` contract matches the pinned upstream API. The change is limited to damage while suffocating; oxygen recovery, suffocation-cycle timing, alerts, and RMC wound handling remain unchanged.
- Decision and rationale: Adapt the upstream named argument to CMU's `TryChangeDamage` call. Asphyxiation represents lack of breathable gas rather than an external hit, so armor and species resistance modifiers must not negate the core respiratory hazard.
- Files changed: `Content.Server/Body/Systems/RespiratorSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static signature and call-path review confirms the named argument binds to `ignoreResistances`, `interruptsDoAfters` remains false, and recovery still follows its prior path. Server compilation plus normal, resistant-species, damage-event, recovery, alert-threshold, and RMC wound cases are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1786 as `Ported (CS-0211)` when wave 0009 is committed.

## CS-0212 — Suppress rename events for internal solution entities

- Upstream: [space-wizards/space-station-14#41400](https://github.com/space-wizards/space-station-14/pull/41400), `c079fdfbba477a2dbd4df17ad3be97d1c0e97084`, 2025-11-12
- Areas: Chemistry, Interactions, GameTicking
- Status: Ported
- Risk: Low
- Behavior/API delta: Naming a newly created contained-solution entity now sets its metadata with `raiseEvents: false`. The internal entity still receives and networks its diagnostic name, but its creation no longer broadcasts an `EntityRenamedEvent` to unrelated identity, PDA, station-record, mind, and name-modifier listeners.
- RMC/CMU divergence: RMC's ID-card system globally subscribes to rename events and queues non-card entities for resolution during its next update. Suppressing the internal solution rename also prevents every lazily created solution from entering that RMC-specific per-tick queue; genuine actor and item renames remain eventful.
- Decision and rationale: Port the upstream named argument directly. A contained solution's generated metadata label is implementation detail rather than a player-visible rename, so publishing it creates false interaction work without a valid consumer.
- Files changed: `Content.Shared/Chemistry/EntitySystems/SharedSolutionContainerSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static API review confirms the pinned RobustToolbox `SetEntityName` overload accepts `raiseEvents`, still dirties metadata, and only the generated solution-name call suppresses events. Shared compilation plus solution creation, metadata replication, rename-listener, PDA/ID queue, station-record, map initialization, and client prediction cases are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1689 as `Ported (CS-0212)` when wave 0009 is committed.

## CS-0213 — Normalize inherited lighting and railing AABBs

- Upstream: [space-wizards/space-station-14#41381](https://github.com/space-wizards/space-station-14/pull/41381), `3dc0d0080d9aea1adc0c68b3de0d2ea5cc646c37`, 2025-11-10
- Areas: Movement, Interactions, Physics
- Status: Ported
- Risk: Medium
- Behavior/API delta: Six `PhysShapeAabb` fixture bounds now use the required left-bottom-right-top ordering. Two wall-light fixtures, the ground light, the strobe light, and two directional railing fixtures therefore deserialize as valid boxes with their intended footprint rather than inverted horizontal or vertical extents.
- RMC/CMU divergence: CMU retains RMC structures, maps, collision layers, and physics behavior around these upstream base prototypes. Their children inherit these fixture definitions, so the coordinate correction applies without replacing RMC masks, layers, densities, anchoring, or directional railing variants.
- Decision and rationale: Port the target-final coordinate reorder exactly. Each old box violates `left <= right` or `bottom <= top`; reordering the same endpoints restores the authored geometry without changing its size or collision policy.
- Files changed: `Resources/Prototypes/Entities/Structures/Lighting/base_lighting.yml`, `Resources/Prototypes/Entities/Structures/Lighting/ground_lighting.yml`, `Resources/Prototypes/Entities/Structures/Lighting/strobe_lighting.yml`, `Resources/Prototypes/Entities/Structures/Walls/railing.yml`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static review confirms all six target bounds now satisfy left-bottom-right-top ordering and no neighboring valid railing fixtures changed. YAML/prototype validation plus fixture creation, directional placement, collision, movement obstruction, interaction reach, map-load, and inherited RMC prototype cases are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1671 as `Ported (CS-0213)` when wave 0009 is committed.

## CS-0214 — Log validated RCD radial-mode selections

- Upstream: [space-wizards/space-station-14#40986](https://github.com/space-wizards/space-station-14/pull/40986), `0ed111d307bdee5a1b967c3490fa01c4b73d293f`, 2025-11-05
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: A valid RCD radial-menu selection now emits a low-impact `LogType.RCD` entry containing the actor, resolved mode, and construction prototype. Prototype existence checking now resolves the selected `RCDPrototype` once so the same validated object supplies the log data.
- RMC/CMU divergence: CMU keeps RMC's RCD recipes, charge behavior, construction validation, and existing high-impact execution logs. This adds selection intent to the shared audit trail without changing the selected prototype, charging, do-after, or construction outcome; invalid or unavailable selections still return before logging.
- Decision and rationale: Port the upstream validation-and-log delta exactly and remove the now-unused charges-component import. Mode changes are meaningful administrative context when investigating later RCD construction or deletion logs.
- Files changed: `Content.Shared/RCD/Systems/RCDSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static event-flow review confirms availability and prototype checks precede the log, the resolved prototype matches the assigned ID, and existing execution logs remain intact. Shared compilation plus valid, invalid, unavailable, null-construction-prototype, client/server, radial-menu, charge, and admin-log cases are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1641 as `Ported (CS-0214)` when wave 0009 is committed.

## CS-0215 — Report combined gas-pipe manifold volume

- Upstream: [space-wizards/space-station-14#41325](https://github.com/space-wizards/space-station-14/pull/41325), `53083ef771634e97dfed25467e7cd2a3eb6896b0`, 2025-11-06
- Areas: Interactions, Physics, GameTicking
- Status: Ported
- Risk: Medium
- Behavior/API delta: Each of the manifold's six pipe nodes now contributes 50 liters, and gas-analyzer sampling scales the shared mixture to the sum of all configured inlet and outlet nodes. The manifold therefore has a 300-liter device volume and reports that combined volume instead of presenting one node as the whole device.
- RMC/CMU divergence: CMU retains RMC atmospheric networks, maps, analyzer UI, and device monitoring around the upstream manifold prototype. The port changes only the inherited manifold node volumes and its scan projection; layer routing, always-reachable links, pipe gas composition, and RMC map placements remain intact.
- Decision and rationale: Port the system and prototype halves together. Counting all six names without reducing the per-node volume would inflate the analyzer sample, while changing node volumes alone would still under-report the shared device; the paired delta preserves pressure/composition while exposing the intended aggregate capacity.
- Files changed: `Content.Server/Atmos/Piping/EntitySystems/GasPipeManifoldSystem.cs`, `Resources/Prototypes/Entities/Structures/Piping/Atmospherics/pipes.yml`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static node-graph review confirms the union contains the three inlet and three outlet names, every corresponding node is 50 liters, and the sample scales moles and volume by the same aggregate factor. Server compilation and YAML validation plus isolated, connected, partially connected, multi-layer, analyzer, pressure, monitoring-console, map-load, and RMC atmos-tick cases are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1645 as `Ported (CS-0215)` when wave 0009 is committed.

## CS-0216 — Correct the burn-decal variation prototype ID

- Upstream: [space-wizards/space-station-14#41444](https://github.com/space-wizards/space-station-14/pull/41444), `fd2f5f7e450ef6de26411acf6f33404da157adca`, 2025-11-15
- Areas: GameTicking, Gamerules
- Status: Ported
- Risk: Low
- Behavior/API delta: The burn-decal variation pass and its roundstart reference are renamed from the misspelled `BasicDecalBrunsVariationPass` to `BasicDecalBurnsVariationPass`. The referenced pass, probability, exclusivity group, density, and spawned burn decals are otherwise unchanged.
- RMC/CMU divergence: Standard station variation rules retain this pass, while RMC distress-signal presets do not explicitly schedule `BasicRoundstartVariation`. Repository-wide lookup found no CMU or RMC references to the misspelled ID beyond the definition and matching standard-rule entry changed together.
- Decision and rationale: Port both sides of the upstream identifier correction atomically. Keeping a fork-only typo adds needless prototype-name divergence and makes future rule reconciliation harder even though the current paired typo resolves internally.
- Files changed: `Resources/Prototypes/GameRules/roundstart.yml`, `Resources/Prototypes/GameRules/variation.yml`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static reference review confirms the old ID has no remaining repository references and the corrected ID has exactly one definition and one roundstart reference. YAML/prototype validation plus standard variation selection, probability/or-group, decal spawning, map initialization, and RMC preset cases are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1713 as `Ported (CS-0216)` when wave 0009 is committed.

## CS-0217 — Run activatable UI before food ingestion

- Upstream: [space-wizards/space-station-14#41547](https://github.com/space-wizards/space-station-14/pull/41547), `fa2e4309cc95356152d8df51591714b385275219`, 2025-11-23
- Areas: Medical, Interactions
- Status: Ported
- Risk: Medium
- Behavior/API delta: `FoodSystem` now handles `UseInHandEvent` after `ActivatableUISystem` as well as openable and inventory systems. An entity that is both food and an activatable UI—most notably standard paper—therefore gives its UI the default interaction first; ingestion still runs when the UI declines to handle the event.
- RMC/CMU divergence: Pinned upstream had already replaced `FoodComponent` with `EdibleComponent` and moved this handler into `IngestionSystem`. CMU retains the older food architecture, so the functional ordering constraint is adapted to `FoodSystem`; RMC's separate `CMBasePaper` is not a food entity and remains unaffected.
- Decision and rationale: Port only the behavior-bearing dependency addition. The upstream change to collection-expression syntax and its pre-existing `AfterInteractEvent` tool-ordering declaration are not prerequisites for fixing CMU's use-in-hand race and would add unrelated refactor delta.
- Files changed: `Content.Shared/Nutrition/EntitySystems/FoodSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static subscription and handler review confirms UI executes first, both handlers honor `Handled`, and failed UI activation can still fall through to existing ingestion. Shared compilation plus ordinary food, standard paper, RMC paper, UI accepted/rejected, openable, inventory, eat-verb, prediction, and force-feed cases are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1773 as `Ported (CS-0217)` when wave 0009 is committed; revisit the adaptation when CMU adopts upstream's `EdibleComponent`/`IngestionSystem` refactor.

## CS-0218 — Narrow clumsy and untrained-bible burns to heat

- Upstream: [space-wizards/space-station-14#41307](https://github.com/space-wizards/space-station-14/pull/41307), `5a5031750200fa349e1803fa51268df46b54043a`, 2025-11-05
- Areas: Shooting, Medical, Interactions
- Status: Ported
- Risk: Medium
- Behavior/API delta: Clumsy gun-failure damage on the affected monkey, kobold, guardian-host, and clown definitions now adds three Heat damage instead of three damage to every type in the Burn group. Untrained standard-bible use similarly deals ten Heat rather than ten each of Heat, Shock, Cold, and Caustic.
- RMC/CMU divergence: CMU retains RMC's extended damage modification, armor, wound, and weapon behavior, but these standard `DamageSpecifier` prototypes still feed those paths. Narrowing the authored damage types prevents accidental multi-type amplification while preserving all RMC-side processing for the resulting Heat damage.
- Decision and rationale: Port the four target-final prototype corrections exactly. These effects describe an explosion burn or holy sizzle, not simultaneous electrical, cold, and acid injury; using the broad group multiplied their intended burn value across four independent damage types.
- Files changed: `Resources/Prototypes/Entities/Mobs/NPCs/animals.yml`, `Resources/Prototypes/Entities/Mobs/Player/guardian.yml`, `Resources/Prototypes/Entities/Objects/Specific/Chapel/bibles.yml`, `Resources/Prototypes/Roles/Jobs/Civilian/clown.yml`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static prototype review confirms only the four upstream-selected damage specifiers changed and each retains its existing blunt/piercing or bible behavior. YAML/prototype validation plus clumsy firing for each source, resistance and wound handling, trained/untrained bible use, failure/success healing, damage totals, and RMC mob cases are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1642 as `Ported (CS-0218)` when wave 0009 is committed.

## CS-0219 — Preserve authored regal-rat movement speeds

- Upstream: [space-wizards/space-station-14#41420](https://github.com/space-wizards/space-station-14/pull/41420), `162a17411febcd048d5ca7bce13a0cfdb5593802`, 2025-11-15
- Areas: Movement, Medical
- Status: Ported
- Risk: Low
- Behavior/API delta: The rat king and rat servant bodies now set `requiredLegs: 0`, which opts them out of body-part-derived base-speed recalculation. Their explicit movement speeds—most notably the king's five-tile sprint speed—are therefore no longer overwritten by the placeholder single-leg rat body definition.
- RMC/CMU divergence: CMU retains RMC movement modifiers and its older upstream body-part implementation, whose `UpdateMovementSpeed` already returns when `RequiredLegs <= 0`. The port leaves input, mover, combat, slowdown, and RMC status modifiers intact; it only prevents the body system from replacing these prototypes' authored base speeds.
- Decision and rationale: Port both affected body overrides exactly. The rat body still lacks a proper multi-leg model, so treating its placeholder leg as authoritative produces an unintended base-speed reset; zero is the existing documented opt-out until that body model is corrected.
- Files changed: `Resources/Prototypes/Entities/Mobs/NPCs/regalrat.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static body/movement review confirms both prototypes explicitly set their base speeds, the shared body system treats zero as an early return, and no other rat definitions changed. YAML/prototype validation plus spawn, map-init, sprint/walk, body initialization, limb changes, slowdown/status stacking, player control, AI movement, and RMC movement-modifier cases are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1710 as `Ported (CS-0219)` when wave 0009 is committed; restore limb-derived speed only when the rat body gains an accurate leg model and matching movement values.

## CS-0220 — Rate-limit nuclear-code submissions

- Upstream: [space-wizards/space-station-14#41831](https://github.com/space-wizards/space-station-14/pull/41831), `6fc487531cabba44c361873e8d3faa04619f603d`, 2025-12-12
- Areas: Interactions, GameTicking, Gamerules
- Status: Ported
- Risk: Low
- Behavior/API delta: The server now accepts `NukeKeypadEnterMessage` attempts at most once per second per nuclear device while it awaits a code. Each accepted submission records its game-time timestamp before code validation, preventing a modified client from checking candidate codes every tick.
- RMC/CMU divergence: RMC disables the standard NukeOps preset and also has a separate `RMCNukeSystem` for map-wide destruction; neither changes this standard nuclear-device keypad path. The cooldown is stored only on `NukeComponent`, so RMC detonation logic, keypad digits/clear, arming, disarming, and countdown behavior remain untouched.
- Decision and rationale: Port the target-final server-side guard, shared duration, and time-offset serialization together. Client-only button throttling would not constrain crafted network messages, while a per-device authoritative timestamp closes the brute-force path without maintaining session-global state.
- Files changed: `Content.Server/Nuke/NukeComponent.cs`, `Content.Server/Nuke/NukeSystem.cs`, `Content.Shared/Nuke/NukeUiMessages.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static message-flow review confirms status validation precedes the clock check, failed codes consume the same cooldown as correct codes, and keypad digit and clear messages retain their prior paths. Server/shared compilation plus first, repeated, boundary-time, wrong/correct code, multi-user, multi-device, save/load time-offset, arming/disarming, and RMC nuke cases are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1974 as `Ported (CS-0220)` when wave 0010 is committed.

## CS-0221 — Stop pulls after committed mob-state transitions

- Upstream: [space-wizards/space-station-14#41835](https://github.com/space-wizards/space-station-14/pull/41835), `75bb75539bbe84964be0f3303dc8473d135c4a4a`, 2025-12-12
- Areas: Movement, Medical, Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: `PullingSystem` now reacts to `MobStateChangedEvent` and its final `NewMobState` rather than the mutable pre-transition `UpdateMobStateEvent`. A pull is released when the puller actually enters critical or dead state, not merely when a state update proposes one.
- RMC/CMU divergence: RMC adds critical-grace, suicide, parasite, pheromone, damage-on-pull, fireman-carry, and pull-retargeting behavior around the same mob-state and pulling systems. Waiting for the committed transition lets those pre-update modifiers finish before base pulling cleanup while preserving `TryStopPull` and RMC's pull-stop event propagation.
- Decision and rationale: Port the target-final event subscription, handler type, and field access together. The post-transition event is raised only after allowed-state checks and state assignment, eliminating premature pull cancellation when another system changes or rejects the proposed state.
- Files changed: `Content.Shared/Movement/Pulling/Systems/PullingSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static state-machine review confirms `MobStateChangedEvent` carries the assigned final state, fires only on an actual transition, and the existing critical/dead cleanup still resolves the pulled entity before stopping. Shared compilation plus alive-to-critical/dead, unchanged/rejected, revival, RMC critical-grace, fireman carry, retargeting, prediction, joint, hand, and pull-event cases are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1983 as `Ported (CS-0221)` when wave 0010 is committed.

## CS-0222 — Use pixel coordinates for shuttle FTL controls

- Upstream: [space-wizards/space-station-14#40933](https://github.com/space-wizards/space-station-14/pull/40933), `3944268fe48a76293ecc4820616cc87fff8caacc`, 2025-12-05
- Areas: Movement, Interactions, Physics
- Status: Ported
- Risk: Low
- Behavior/API delta: Free-position FTL clicks now pass `RelativePixelPosition` into the shuttle map's inverse transform, and parallax tiling uses the control's `PixelSize`. Input, world-coordinate conversion, and rendering therefore share the same scaled coordinate space when UI scale differs from one.
- RMC/CMU divergence: CMU retains RMC shuttle and dropship systems around the standard shuttle map control, but no RMC override replaces these two client coordinate conversions. Beacon selection already uses pixel coordinates and `PixelRect`; this brings free-position FTL and the background extent into alignment without changing server FTL validation or destination policy.
- Decision and rationale: Port the two target-final substitutions exactly. `MapGridControl` derives its midpoint and minimap scale in pixels, so feeding logical UI coordinates skews click destinations, while drawing pixel-scaled map objects against logical `Size` leaves parallax gaps or overdraw.
- Files changed: `Content.Client/Shuttles/UI/ShuttleMapControl.xaml.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static coordinate-flow review confirms beacon and free-position clicks now use the same pixel basis, `InverseMapPosition` expects that basis, and parallax bounds match the drawing surface. Client compilation plus UI-scale 1/non-1, free and beacon FTL, zoom, pan, rotation, parallax tiling, edge clicks, standard shuttle, and RMC shuttle-console cases are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1890 as `Ported (CS-0222)` when wave 0010 is committed.

## CS-0223 — Ignore predicted client-only RCD placement sources

- Upstream: [space-wizards/space-station-14#41648](https://github.com/space-wizards/space-station-14/pull/41648), `61c58a6341821f8d8b988da1899a5e5c0726a1ae`, 2025-12-01
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: The client RCD construction-ghost updater now returns while the active-hand RCD is a client-side predicted entity. Temporary predicted RCDs are no longer installed as placement sources or used for rotation network events before their authoritative entity is reconciled.
- RMC/CMU divergence: RMC's borg modules and predicted inventory behavior make transient client-only RCDs a practical path in CMU. The guard is limited to that transient ownership state; authoritative handheld and borg-provided RCDs still update recipes, direction, range, tile/object mode, and placement overlays through the retained system.
- Decision and rationale: Port the target-final client-entity guard before component and prototype resolution. Beginning placement against an entity that the server cannot identify leaves permission state tied to an entity that prediction later deletes, which is the source of the stuck overlay.
- Files changed: `Content.Client/RCD/RCDConstructionGhostSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static update-flow review confirms the guard runs only for non-null client-side entities, authoritative RCDs keep the existing path, and non-RCD hands still clear an active RCD placer. Client compilation plus predicted spawn/reconciliation, borg module, handheld RCD, item switch/drop, recipe/direction change, overlay clear, range, and rotation-event cases are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1834 as `Ported (CS-0223)` when wave 0010 is committed.

## CS-0224 — Conserve mass-weighted fire stacks on collision

- Upstream: [space-wizards/space-station-14#41636](https://github.com/space-wizards/space-station-14/pull/41636), `3d32dab66116e5bab547f4b571a4c11da4503fb5`, 2025-11-30
- Areas: Medical, Physics, GameTicking
- Status: Ported
- Risk: Medium
- Behavior/API delta: When two fire-spreading entities collide and either is burning, the system now averages their mass-weighted fire-stack quantities and sets each entity's stacks to that shared quantity divided by its own mass. Before per-entity clamps, total fire-stack mass is conserved and lighter entities receive more stacks than heavier ones.
- RMC/CMU divergence: RMC extends `SetFireStacks` with ignition-attempt cancellation, stack caps, fire intensity/duration, damage, and stop-drop-roll behavior. The new collision calculation still enters that retained setter once per entity, so RMC cancellation and clamping remain authoritative while the erroneous source/destination delta math is replaced.
- Decision and rationale: Port the target-final equalization formula as one block. The previous average divided unweighted stacks by combined mass and then applied cross-mass deltas, which could create or remove fire and move stacks in the wrong direction; directly setting the two mass-normalized results expresses the intended conservation rule.
- Files changed: `Content.Server/Atmos/EntitySystems/FlammableSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static arithmetic review confirms the two unclamped final mass-weighted quantities sum to the original total, collision de-duplication and fixture checks are unchanged, and both values still pass through RMC-aware `SetFireStacks`. Server compilation plus equal/unequal mass, one/both burning, stack caps, ignition cancellation, non-physics, collision ordering, fire damage, extinguish, and RMC fire cases are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1832 as `Ported (CS-0224)` when wave 0010 is committed.

## CS-0225 — Make base simple mobs blindable

- Upstream: [space-wizards/space-station-14#41788](https://github.com/space-wizards/space-station-14/pull/41788), `dc616f67e7436d65ba28b0e118de21b37c1f9885`, 2025-12-09
- Areas: Medical, Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: `BaseSimpleMob` now includes `Blindable`, allowing its existing temporary-blindness status and eye-damage systems to maintain blindness state, vision overlays, eye PVS scaling, UI vision restrictions, blindfolds, and eye healing for derived NPCs.
- RMC/CMU divergence: RMC's `RMCSimpleMob` derives from `BaseSimpleMob` and already permits `TemporaryBlindness`, `Blinded`, and related medical statuses but lacked the component that implements their eye state. RMC species and xeno bases that explicitly define `Blindable` retain their existing component data through prototype composition.
- Decision and rationale: Port the target-final base-component addition. Advertising blindness statuses without a `BlindableComponent` makes flashes, chemicals, eye damage, and cures silently ineffective on many simple mobs; the shared base is the narrowest common point that restores the contract.
- Files changed: `Resources/Prototypes/Entities/Mobs/NPCs/simplemob.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static inheritance review confirms `RMCSimpleMob` and standard simple-mob families derive from the changed base, the blindness systems are component-gated, and no status or threshold data changed. YAML/prototype validation plus flash, temporary/permanent blindness, eye damage/healing, blindfold, UI vision gate, player-controlled NPC, AI, robotic simple mob, RMC simple mob, species, and xeno cases are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1959 as `Ported (CS-0225)` when wave 0010 is committed; prototype-specific opt-outs can remove `Blindable` if a genuinely sightless simple-mob family is identified.

## CS-0226 — Remove the reagent-dispenser base resale price

- Upstream: [space-wizards/space-station-14#41756](https://github.com/space-wizards/space-station-14/pull/41756), `6f5e6445b6c65d15300b5e5098cda64abc295931`, 2025-12-13
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: `ReagentDispenserBase` no longer contributes a fixed 1,000-credit `StaticPrice`. Constructed dispensers therefore cannot duplicate value by being sold or deconstructed while child prototypes remain free to declare intentional prices.
- RMC/CMU divergence: CMU retains the standard dispenser construction, storage, and cargo-pricing components alongside RMC chemistry content. No RMC override depends on the inherited base price, and explicit prices on unrelated RMC medical containers and machines are unchanged.
- Decision and rationale: Port the target-final two-line removal exactly. A price on the abstract reusable base rewards the completed structure independently of its materials and creates an economy arbitrage path; removing only that inherited component preserves dispenser operation and explicit child pricing.
- Files changed: `Resources/Prototypes/Entities/Structures/Dispensers/base_structuredispensers.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static prototype review confirms the only behavioral delta is removal of the base `StaticPrice`, while power, UI, storage, construction, wires, and deconstruction containers are unchanged. YAML/prototype validation, solution compilation, and the full integration suite are queued for the index-1999 checkpoint.
- Follow-up/debt: Record index 1986 as `Ported (CS-0226)` when wave 0010 is committed; review the separate five-bounty reward corrections at index 1995 against CMU economy policy.

## Upstream checkpoint — indices 1000–1999

Date completed: 2026-07-20

- Scope: Inventory waves 0006 through 0010, covering the second 1,000 pinned SS14 first-parent commits from indices 1000 through 1999 and every accepted decision through CS-0226. The tranche includes all eight audited areas: Movement, Shooting, Medical, Chemistry, Interactions, Physics, GameTicking, and Gamerules.
- Unit tests: `dotnet test Content.Tests/Content.Tests.csproj --configuration DebugOpt --no-build --no-restore --nologo --verbosity:minimal` completed with 377 passed, 1 skipped, and 0 failed.
- Integration tests: `dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --configuration DebugOpt --no-build --no-restore --nologo --verbosity:minimal --logger "trx;LogFileName=checkpoint-0002.trx" --results-directory <temporary-directory> -- NUnit.ConsoleOut=0 NUnit.MapWarningTo=Failed` completed with 418 passed, 17 skipped, and 0 failed (435 total) in 34 minutes 11 seconds. Its TRX was written outside the repository.
- Solution build: after the checkpoint correction below, `dotnet build SpaceStation14.slnx --configuration DebugOpt --no-restore --no-incremental --nologo --verbosity:minimal --disable-build-servers` completed in 1 minute 53 seconds with 0 warnings and 0 errors.
- Resource validation: `dotnet run --project Content.YAMLLinter/Content.YAMLLinter.csproj --configuration DebugOpt --no-build` completed with `No errors found` in 91.9 seconds.
- Defects caught: the initial full build reported analyzer RA0051 because CS-0201 declared its injected admin logger `readonly`. Commit `a039a53b4b` restored the established dependency-injection declaration and updated the durable audit; the complete non-incremental build then passed cleanly. Unit, resource, and integration validation found no further defects.
- Disposition: The 1000–1999 checkpoint is closed. Continue with inventory wave 0011 at index 2000 and defer routine full build/test execution until index 2999 unless a specific risk justifies earlier focused validation.

## CS-0227 — Reactivate neighboring spreaders after deletion

- Upstream: [space-wizards/space-station-14#42016](https://github.com/space-wizards/space-station-14/pull/42016), `503052bca7b5b78aed783d001149eb6553196656`, 2025-12-23
- Areas: Physics, GameTicking
- Status: Ported
- Risk: Medium
- Behavior/API delta: `ActivateSpreadableNeighbors` now distinguishes the terminating origin from its grid and checks `EdgeSpreaderComponent` on each enumerated anchored entity. Non-terminating spreaders on the same tile and four adjacent tiles are reactivated when an origin disappears.
- RMC/CMU divergence: The standard spreader drives puddles, smoke, and kudzu, while RMC smoke also uses `ActiveEdgeSpreaderComponent` and RMC xeno weeds have additional custom spreading logic. The fix changes only generic neighbor requeueing; RMC smoke lifetime transfer and xeno-specific spread timing remain intact.
- Decision and rationale: Port the complete target-final method correction, including the origin/grid variable split. The legacy code queried the grid UID in both loops and compared same-tile entities against that grid, so it could never recognize neighboring edge spreaders and could requeue the terminating origin incorrectly.
- Files changed: `Content.Server/Spreader/SpreaderSystem.cs`, `docs/upstream-sync/inventory-wave-0011.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static review confirms both anchored-entity loops query their current entity, the origin is skipped only on its own tile, deleted entities remain excluded, and explicit-position callers still resolve the supplied grid/tile. Server compilation plus puddle, smoke, kudzu, RMC smoke, same-tile windoor, adjacent deletion, terminating neighbor, and explicit-position cases are queued for the index-2999 checkpoint.
- Follow-up/debt: The custom RMC xeno-weed spread scheduler remains separate and should be assessed with its own target-final dependency chain rather than folded into this generic fix.

## CS-0228 — Serialize loadout-specific entity names

- Upstream: [space-wizards/space-station-14#41891](https://github.com/space-wizards/space-station-14/pull/41891), `b4fa6f4a07a1cf6f1871cebaaa3d677ef94f7f8c`, 2025-12-18
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: `RoleLoadout.EntityName` is now marked as a data field. A customized entity name attached to a role loadout is included in serialization and therefore survives preference export/import instead of silently reverting to null.
- RMC/CMU divergence: CMU retains SS14's role-loadout preference object alongside extensive RMC job and equipment data. The field and equality/copy behavior already exist locally; this adds only the missing serialization metadata and does not change loadout selection, slot validation, or RMC role policy.
- Decision and rationale: Port the target-final attribute exactly. The serializer cannot persist an unannotated public field in this data definition, so the current in-memory value works only until the loadout crosses the save/export boundary.
- Files changed: `Content.Shared/Preferences/Loadouts/RoleLoadout.cs`, `docs/upstream-sync/inventory-wave-0011.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static data-contract review confirms the field is already consumed by role-loadout equality and entity customization and that nearby persisted members use the same attribute. Serialization round-trip coverage and the shared/content build are queued for the index-2999 checkpoint.
- Follow-up/debt: None; this is the complete target-final persistence fix.

## CS-0229 — Keep flammability overlap fixtures massless

- Upstream: [space-wizards/space-station-14#41803](https://github.com/space-wizards/space-station-14/pull/41803), `2455dbbdb093006e7ef0516869c9001214522c33`, 2025-12-17
- Areas: Physics
- Status: Ported
- Risk: Low
- Behavior/API delta: The non-hard fixture created for flammable collision events now uses zero density. Adding `FlammableComponent` no longer increases a physics body's aggregate mass, while the fixture continues to generate overlap events for fire-stack transfer.
- RMC/CMU divergence: RMC makes many mobs and objects flammable and extends their fire behavior, so an incidental sensor-fixture mass affects movement, impacts, dragging, and CS-0224's intentional mass-weighted fire equalization more broadly than upstream. The density change does not alter RMC ignition, resistance, damage, caps, or extinguishing logic.
- Decision and rationale: Port the target-final named argument at fixture creation. This fixture is an event sensor rather than physical material; assigning it density contaminates the owning body's mass and feeds that artificial value back into unrelated physics and fire calculations.
- Files changed: `Content.Server/Atmos/EntitySystems/FlammableSystem.cs`, `docs/upstream-sync/inventory-wave-0011.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static review confirms fixture shape, identifier, collision mask, non-hard state, and body association are unchanged, and only density becomes zero. Server compilation plus mass-before/after, collision fire transfer, CS-0224 unequal-mass equalization, movement, pulling, throwing, RMC mob, ignition, and extinguishing cases are queued for the index-2999 checkpoint.
- Follow-up/debt: None; preserve explicit physical fixture density on real collision fixtures rather than copying this sensor-only setting elsewhere.

## CS-0230 — Stop the opposite internals audio stream on state changes

- Upstream: [space-wizards/space-station-14#42304](https://github.com/space-wizards/space-station-14/pull/42304), `350c67c73ee0188e948da9de70d675c1d7d82784`, 2026-01-08
- Areas: Interactions, Physics
- Status: Ported
- Risk: Low
- Behavior/API delta: Connecting a gas tank to internals stops any outstanding disconnect sound before playing the predicted connect sound; disconnecting performs the inverse. Rapid toggles no longer cancel the sound that is about to be replayed while leaving the opposite transition audible.
- RMC/CMU divergence: RMC breathing gear and loadouts use the shared gas-tank/internals state machine, but add their own equipment and atmosphere balance. This changes only the two predicted audio stream handles after the authoritative connection state succeeds; tank ownership, pressure, breath tools, and RMC equipment behavior are unchanged.
- Decision and rationale: Port the target-final two stream assignments exactly. Each transition must stop audio from the previous state, whereas the legacy code stopped its own same-state handle and could overlap or suppress feedback during quick connect/disconnect sequences.
- Files changed: `Content.Shared/Atmos/EntitySystems/SharedGasTankSystem.cs`, `docs/upstream-sync/inventory-wave-0012.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static review confirms audio changes remain after successful state mutation, stream handles are cleared through `Stop`, predicted playback still uses the same owner/user, and UI updates are unchanged. Shared compilation plus connect, disconnect, rapid toggle, forced disconnect, prediction reconciliation, null sounds, RMC masks, and tank transfer cases are queued for the index-2999 checkpoint.
- Follow-up/debt: None; this is the complete target-final audio-stream correction.

## CS-0231 — Align electrification sounds with the resulting state

- Upstream: [space-wizards/space-station-14#42294](https://github.com/space-wizards/space-station-14/pull/42294), `80d38c51b376f9185eb1e8a8d0f5b96f03d53ec5`, 2026-01-08
- Areas: Interactions, Physics
- Status: Ported (adapted)
- Risk: Low
- Behavior/API delta: `AirlockElectrifyEnabled` and `AirlockElectrifyDisabled` now reference the matching on/off assets, and the Station AI path chooses the sound corresponding to `ElectrifiedComponent.Enabled` after mutation. Field semantics and runtime selection are consistent for defaults and prototype overrides.
- RMC/CMU divergence: CMU retains the shared electrification component and Station AI radial behavior, but its older `SharedDoorRemoteSystem` has no upstream electrify operating mode or sound-selection path. This adaptation changes every applicable local caller without importing that separate remote feature; shock eligibility, power checks, damage, access, and RMC door behavior are unchanged.
- Decision and rationale: Port the target-final data-field swap and Station AI selector together. Swapping only the assets or only the selector would leave an inversion, while changing both makes the named component fields a reliable prototype contract. The absent door-remote hunk cannot apply and is dependency debt rather than silently invented behavior.
- Files changed: `Content.Shared/Electrocution/Components/ElectrifiedComponent.cs`, `Content.Shared/Silicons/StationAi/SharedStationAiSystem.Airlock.cs`, `docs/upstream-sync/inventory-wave-0012.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static review confirms Station AI mutates state before selecting sound, enabled maps to `airlock_electrify_on.ogg`, disabled maps to `airlock_electrify_off.ogg`, and shock sounds are separate. Shared compilation plus AI enable/disable, prototype override, prediction, access denial, power loss, and RMC airlock cases are queued for the index-2999 checkpoint.
- Follow-up/debt: When the upstream door-remote electrify mode is integrated, port its selector using these corrected field semantics. Index 2099's Station AI access/logging hardening remains separate and must preserve this selection.

## CS-0232 — Commit locker resistance state only after DoAfter startup

- Upstream: [space-wizards/space-station-14#42313](https://github.com/space-wizards/space-station-14/pull/42313), `f8ff3a92aa97a5a13d32296c7606698cb464769e`, 2026-01-08
- Areas: Movement, Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: A locker escape attempt now returns when its DoAfter cannot start. `ResistLockerComponent.IsResisting` and the start popup are committed only after successful scheduling, so rejected attempts do not leave the user permanently treated as already resisting.
- RMC/CMU divergence: CMU retains the standard locker-resistance system alongside RMC restraints, storage prototypes, and interaction restrictions. The change respects whatever local DoAfter blockers reject the attempt and does not alter escape duration, damage, cancellation rules, storage opening, or RMC restraint policy.
- Decision and rationale: Port the target-final ordering exactly. The DoAfter scheduler is the authority on whether an attempt exists; setting state before checking its return value creates a latch with no completion/cancellation event available to clear it.
- Files changed: `Content.Server/Resist/ResistLockerSystem.cs`, `docs/upstream-sync/inventory-wave-0012.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static flow review confirms failure returns before state/popup changes, success retains the same DoAfter arguments, and completion/cancellation handling is unchanged. Server compilation plus cuffed, blocked, successful, cancelled, repeated, deleted-locker, bluespace-locker, and RMC restraint cases are queued for the index-2999 checkpoint.
- Follow-up/debt: None; other DoAfter callers should be audited independently before applying the same ordering pattern.

## CS-0233 — Prevent chemical reactions inside pill solutions

- Upstream: [space-wizards/space-station-14#41457](https://github.com/space-wizards/space-station-14/pull/41457), `766f429fd9a0604e5cc82d27ee829b27f542a541`, 2026-01-15
- Areas: Medical, Chemistry
- Status: Ported (adapted)
- Risk: Medium
- Behavior/API delta: The `food` solutions on both `Pill` and the fork-specific `CMPill` base now set `canReact: false`. Reagents stored together in a pill remain the prescribed mixture rather than running container reactions before the pill is consumed.
- RMC/CMU divergence: RMC duplicates the standard pill prototype as `CMPill` and derives its marine medicine catalog from that base, so the one-line upstream change would otherwise miss CMU's main medical pills. Applying the same solution flag to both bases preserves the target rule across standard and RMC content without changing RMC doses, skill-gated examination, sprites, storage, or reagent effects.
- Decision and rationale: Port the target-final flag and mirror it at the RMC pill base. Pills are delivery containers, not reaction vessels; allowing their contents to react can silently alter a manufactured dose between creation and ingestion. Reaction and metabolism after transfer into an eligible target solution remain controlled by that destination.
- Files changed: `Resources/Prototypes/Entities/Objects/Specific/chemistry.yml`, `Resources/Prototypes/_RMC14/Entities/Objects/Medical/pills.yml`, `docs/upstream-sync/inventory-wave-0012.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static inheritance review confirms standard pill families derive from `Pill`, RMC pill families derive from `CMPill`, and only the source solution's reaction permission changes. YAML/prototype validation plus mixed-reagent storage, pill creation, ChemMaster output, ingestion, metabolism, grinding, standard medicine, and RMC medicine cases are queued for the index-2999 checkpoint.
- Follow-up/debt: Audit any independent pill-like prototypes that duplicate both bases before assuming they inherit this contract; stomach solution policy remains a separate medical/chemistry migration.

## CS-0234 — Refresh containment-generator point lights with connection state

- Upstream: [space-wizards/space-station-14#42289](https://github.com/space-wizards/space-station-14/pull/42289), `d857acfc078098dd09b0f28d47c13444161c530e`, 2026-01-14
- Areas: Interactions, Physics
- Status: Ported
- Risk: Low
- Behavior/API delta: `ChangeOnLightVisualizer` now also calls `UpdateConnectionLights`. Whenever a containment generator's connected-state appearance changes, its actual point light is enabled or disabled from the current connection count instead of remaining stale.
- RMC/CMU divergence: This is the standard singularity containment system and has no RMC replacement in the affected path. The added refresh is presentation/state synchronization only; field generation, ray casts, power thresholds, breach prevention, connection topology, and fork physics remain unchanged.
- Decision and rationale: Port the target-final hook exactly. `ChangeOnLightVisualizer` is already invoked for both endpoints and removal paths, making it the complete state-transition point; the previous direct refresh covered only selected source-generator connection flows.
- Files changed: `Content.Server/Singularity/EntitySystems/ContainmentFieldGeneratorSystem.cs`, `docs/upstream-sync/inventory-wave-0012.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static call-flow review confirms refreshes cover both newly connected generators and all callers that change the on-light appearance, while `UpdateConnectionLights` safely no-ops without a light. Server compilation plus first/multiple connection, remote endpoint, disconnect, grid change, power change, and missing-light cases are queued for the index-2999 checkpoint.
- Follow-up/debt: None; the existing explicit source refresh may become redundant but is harmless and remains target-compatible.

## CS-0235 — Stop relocalizing resolved action tooltip metadata

- Upstream: [space-wizards/space-station-14#42361](https://github.com/space-wizards/space-station-14/pull/42361), `716e5ace87e4c0d44015e767adfd413057f477a7`, 2026-01-11
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: Action-button tooltips now pass `MetaDataComponent.EntityName` and `EntityDescription` directly to permissive markup parsing. These values are already resolved display text, so the client no longer treats dynamic names/descriptions as localization keys or emits lookup warnings.
- RMC/CMU divergence: RMC frequently assigns action metadata dynamically for xeno abilities, vehicles, equipment, and marine systems. Direct parsing preserves those runtime strings and their markup instead of attempting a second localization pass; action activation, cooldowns, charges, requirements, and icons are unchanged.
- Decision and rationale: Port the target-final removal of both localization calls while retaining CMU's local description-variable name. Entity metadata is the presentation boundary and may contain localized or runtime-generated text; resolving it again is semantically incorrect and noisy.
- Files changed: `Content.Client/UserInterface/Systems/Actions/Controls/ActionButton.cs`, `docs/upstream-sync/inventory-wave-0012.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static review confirms name/description still use permissive markup, null metadata still returns early, and charge/requirement text remains on its existing path. Client compilation plus prototype action, dynamic RMC action, localized text, markup, missing-key warning, charges, cooldown, and empty-description cases are queued for the index-2999 checkpoint.
- Follow-up/debt: The nearby charge strings interpolate resolved runtime values into `Loc.GetString`; review that separate legacy path when its upstream replacement reaches the pinned history.

## CS-0236 — Generate network-link colors from byte channels

- Upstream: [space-wizards/space-station-14#42335](https://github.com/space-wizards/space-station-14/pull/42335), `319617f6ba923f31c8a14b5cc12e0a0f42d0c23d`, 2026-01-10
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: The network-configurator link overlay now generates each random RGB channel with `IRobustRandom.NextByte`. Byte arguments select `Color(byte, byte, byte)`, which normalizes channels to the 0–1 rendering range; the previous integer values selected the float constructor and supplied values as high as 254 directly.
- RMC/CMU divergence: CMU retains the standard device-network overlay and uses it with both upstream and fork-specific networked machinery. This changes only the client-side color assigned per linked source entity; device discovery, link state, interaction range, network packets, and RMC machinery behavior are unchanged.
- Decision and rationale: Port the target-final three-call replacement exactly. The random range was expressed in byte units, so constructing byte channels is the type-correct boundary and avoids mostly over-range colors being clamped or rendered incorrectly.
- Files changed: `Content.Client/NetworkConfigurator/NetworkConfiguratorLinkOverlay.cs`, `docs/upstream-sync/inventory-wave-0012.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static overload review confirms `NextByte` selects the byte color constructor while preserving the same exclusive upper bound and one cached color per source. Client compilation plus multiple-link color diversity, persistence, deletion, nullspace, reconnect, and fork-specific device cases are queued for the index-2999 checkpoint.
- Follow-up/debt: None; other random-color callers should be evaluated independently because normalized-float callers are valid when their ranges are already 0–1.

## CS-0237 — Reject incomplete flatpacker material costs

- Upstream: [space-wizards/space-station-14#42445](https://github.com/space-wizards/space-station-14/pull/42445), `d2ac15c76f714144b6ffc583f87b3b097610fb0f`, 2026-01-16
- Areas: Interactions
- Status: Ported
- Risk: Medium
- Behavior/API delta: Machine-board material pricing is now a `Try` operation that includes both component and tag requirements and fails if any required prototype has neither a physical composition nor a lathe recipe. Flatpacker pricing now takes the inserted board entity, output prototypes are validated independently, and the cost is rechecked by the server at both packing start and completion. The client disables packing and displays an invalid-board message when a complete cost cannot be calculated.
- RMC/CMU divergence: RMC adds machine boards and construction ingredients with fork-specific component and tag requirements. Those requirements now contribute their real material costs; an RMC board whose required item has no price source will fail closed instead of producing a discounted flatpack. Flatpacker duration, power behavior, base machine/computer costs, board deletion, output setup, and RMC construction recipes are otherwise unchanged.
- Decision and rationale: Port the target-final five-file validation as one unit. The legacy cost loop concatenated component requirements with themselves, omitting tag requirements, and silently skipped every unpriceable requirement. Partial pricing is unsafe because the authoritative server then accepts and charges that incomplete dictionary, so the API must communicate failure through every caller rather than manufacture a lower price.
- Files changed: `Content.Shared/Construction/MachinePartSystem.cs`, `Content.Shared/Construction/SharedFlatpackSystem.cs`, `Content.Server/Construction/FlatpackSystem.cs`, `Content.Client/Construction/UI/FlatpackCreatorMenu.xaml.cs`, `Resources/Locale/en-US/construction/components/flatpack.ftl`, `docs/upstream-sync/inventory-wave-0012.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static call-site review finds no remaining legacy cost API callers. Both server phases now reject incomplete pricing, result prototypes are validated independently, component and tag requirement sequences are each traversed once, and the UI exposes the same failure without becoming authoritative. Shared/server/client compilation plus machine board, computer board, component requirement, tag requirement, physical composition, lathe recipe, missing-price, insufficient-material, mid-pack mutation, power-loss, and representative RMC board cases are queued for the index-2999 checkpoint.
- Follow-up/debt: At the checkpoint, identify any retained RMC board rejected because a legitimate default ingredient lacks both pricing sources; fix that ingredient's material data or recipe rather than weakening the fail-closed calculation.

## CS-0238 — Conserve reactants during tritium fires

- Upstream: [space-wizards/space-station-14#41870](https://github.com/space-wizards/space-station-14/pull/41870), `c7e4f20f02871641bb5cc00da7dbc4d7fe3c0d12`, 2026-01-13; corrected by [space-wizards/space-station-14#42407](https://github.com/space-wizards/space-station-14/pull/42407), `6cae5d9c4ae533f460088a09aa864fdeef851f53`, 2026-01-14
- Areas: Chemistry, Physics
- Status: Ported
- Risk: Medium
- Behavior/API delta: Tritium combustion now defines an explicit `TritiumBurnFuelRatio` of 2. Both reaction branches remove oxygen alongside burned tritium, and the energetic branch removes only the burned tritium instead of resetting it from the post-mutation amount. Its burn quantity uses the corrected `Min` limit across initial tritium and oxygen-derived availability before applying the existing tritium burn factor.
- RMC/CMU divergence: CMU retains upstream gas identifiers, heat-capacity APIs, reaction scheduling, and tritium constants in this path; no RMC-specific tritium reaction replaces it. RMC maps, ordnance, fire sources, and atmosphere tuning can produce different mixtures, so the corrected reactant accounting may change resulting pressure, temperature, water vapor, and explosive yield while leaving gas thresholds and energy constants intact.
- Decision and rationale: Port the pinned target's final state as one unit. The first upstream commit establishes oxygen consumption and direct fuel removal but briefly used `Max`, which could select more fuel than the limiting reactant; the follow-up changes that operator to `Min`. Integrating both avoids deliberately introducing a known intermediate reaction bug while restoring consistent consumption accounting.
- Files changed: `Content.Server/Atmos/Reactions/TritiumFireReaction.cs`, `Content.Shared/Atmos/Atmospherics.cs`, `docs/upstream-sync/inventory-wave-0012.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static branch analysis confirms every positive `burnedFuel` path removes the same quantity of tritium, removes oxygen using the new ratio, produces the existing equal quantity of water vapor, and reports fire from that quantity. The energetic branch is bounded by both available reactants before the burn-factor scaling. Shared/server compilation plus oxygen-limited, tritium-limited, low-energy, energetic, zero-reactant, heat-scale, closed-volume pressure, water-vapor, temperature, fire-result, and representative RMC ordnance cases are queued for the index-2999 checkpoint.
- Follow-up/debt: The pinned target contains later broad atmos and max-cap rewrites after these commits. Keep those deferred until their complete target-final clusters are inventoried; do not infer their behavior from this focused conservation fix.

## CS-0239 — Exempt projectiles from tile friction

- Upstream: [space-wizards/space-station-14#42320](https://github.com/space-wizards/space-station-14/pull/42320), `96d23393450a42c239582fd1107f166159c790d4`, 2026-01-09
- Areas: Shooting, Physics
- Status: Ported (adapted)
- Risk: Medium
- Behavior/API delta: `BaseBullet`, the water projectile, and the grappling hook now use `TileFrictionModifier` with a zero multiplier instead of setting physics damping fields to zero. Meteors and the immovable rod retain their existing zero tile-friction modifier while dropping redundant damping overrides. The tile-friction controller can therefore no longer decelerate these projectiles as they cross floor tiles.
- RMC/CMU divergence: RMC marine ammunition derives from `BaseBullet` and inherits the upstream correction. `XenoBaseProjectile`, `XenoAcidBallProjectile`, and the standalone `XenoSpikeProjectile` instead duplicated the old zero-damping pattern, so the same modifier was applied directly to those fork bases. Damage, accuracy, falloff, maximum range, fixed-distance triggers, collision masks, and xeno-friendly collision policy are unchanged.
- Decision and rationale: Port the target-final prototype contract and mirror it only to fork projectiles that expressed the same intent with explicit zero damping. CMU's `TileFrictionController` computes and writes damping during physics updates, making prototype damping values the wrong control point; its dedicated modifier is the stable way to opt projectiles out of tile-derived friction.
- Files changed: `Resources/Prototypes/Entities/Objects/Fun/immovable_rod.yml`, `Resources/Prototypes/Entities/Objects/Weapons/Guns/Projectiles/meteors.yml`, `Resources/Prototypes/Entities/Objects/Weapons/Guns/Projectiles/projectiles.yml`, `Resources/Prototypes/_RMC14/Entities/Objects/Xeno/xeno_projectiles.yml`, `Resources/Prototypes/_RMC14/Entities/Objects/Weapons/Projectiles/xeno_spike.yml`, `docs/upstream-sync/inventory-wave-0012.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static prototype review confirms all three upstream projectile replacements match the pinned diff, the relocated local immovable-rod prototype retains its existing modifier, meteors retain theirs, RMC marine bullets inherit `BaseBullet`, and each standalone RMC zero-damping projectile now has the equivalent modifier. YAML/prototype validation plus bullet velocity over tiles, water shot, grappling hook, meteor, rod, marine bullet travel, xeno spit, acid ball, spike, prediction, gravity, space, range termination, and collision cases are queued for the index-2999 checkpoint.
- Follow-up/debt: Audit future standalone projectile prototypes for the dedicated modifier instead of copying physics damping fields; do not apply zero tile friction to thrown items or moving entities whose deceleration is intentional.

## CS-0240 — Preserve sibling dragon rifts after destruction

- Upstream: [space-wizards/space-station-14#42234](https://github.com/space-wizards/space-station-14/pull/42234), `fa7c2be1640f27f3ea79b68d1f55fdaeb75cb34f`, 2026-01-05
- Areas: Interactions, Gamerules
- Status: Ported
- Risk: Low
- Behavior/API delta: `RiftDestroyed` no longer calls `DeleteRifts(..., resetRole: true)`. Crew destruction of one dragon rift therefore leaves the dragon's other spawned rifts in place and does not reset its charged-rift objective, while still applying the full weakened duration, refreshing movement speed, and showing the destruction popup.
- RMC/CMU divergence: CMU retains the standard space-dragon antagonist systems without an RMC replacement in this lifecycle path. The change does not affect RMC xenos or similarly named weapons and vehicles; dragon spawn limits, rift charging, carp spawning, proximity checks, and objective scoring rules are otherwise unchanged.
- Decision and rationale: Port the target-final three-line removal exactly. `DeleteRifts` is aggregate owner cleanup and also clears objective progress, so calling it for a single child entity's shutdown turns one successful crew interaction into deletion of every sibling. Dragon death and component shutdown still call aggregate cleanup with objective preservation.
- Files changed: `Content.Server/Dragon/DragonSystem.cs`, `docs/upstream-sync/inventory-wave-0012.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static lifecycle review confirms only the crew-destruction call was removed; dragon death and shutdown still delete all owned rifts, the destroyed rift still completes its own entity shutdown, and the weakened movement refresh remains. Server compilation plus first/middle/final rift destruction, multiple active/finished rifts, objective progress, dragon death, component shutdown, unanchoring, and repeated-shutdown cases are queued for the index-2999 checkpoint.
- Follow-up/debt: The retained `DragonComponent.Rifts` list continues to record spawned rift identities until aggregate cleanup, matching the pinned target. Revisit that ownership model only with its later upstream lifecycle changes rather than pruning it speculatively here.

## CS-0241 — Log player connection lifecycle events

- Upstream: [space-wizards/space-station-14#42363](https://github.com/space-wizards/space-station-14/pull/42363), `9338834b1b8d21c78b4159bc3b9086919fcf9f6c`, 2026-01-11
- Areas: GameTicking
- Status: Ported
- Risk: Low
- Behavior/API delta: `GameTicker.PlayerStatusChanged` now emits low-impact `Connection` admin logs when a session enters the game and when it disconnects. Each record includes the formatted player identity and either the currently attached entity or an explicit `nothing` marker. `LogType.Connection` occupies upstream value 104.
- RMC/CMU divergence: RMC connection, lobby, reconnect, mind, and character flows pass through the retained GameTicker status handler, so they gain the same audit trail. CMU's RMC-specific log types begin at 10000, leaving value 104 collision-free; session attachment, database callbacks, admin announcements, and join/spawn behavior are unchanged.
- Decision and rationale: Port the target-final enum member and all three call sites together. The no-mind and existing-mind branches exit differently and each needs one connection record, while disconnect logging belongs after the user database notification. A dedicated type lets administrators filter lifecycle events without overloading action or round-join logs.
- Files changed: `Content.Server/GameTicking/GameTicker.Player.cs`, `Content.Shared.Database/LogType.cs`, `docs/upstream-sync/inventory-wave-0012.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static control-flow review confirms every `InGame` path logs exactly once, every handled disconnect logs once, attached-entity formatting is null-safe, and the numeric enum addition does not overlap retained RMC values. Shared/server compilation plus lobby join, no-mind spawn wait, existing-mind reattach, observer fallback, first connection, reconnect, disconnect with/without an entity, database cancellation, and admin-log filtering cases are queued for the index-2999 checkpoint.
- Follow-up/debt: These records describe session status transitions, not successful character spawning. Keep round-start, late-join, respawn, and RMC spawn logs separate when interpreting the audit trail.

## CS-0242 — Predict GenPop locker configuration

- Upstream: [space-wizards/space-station-14#42365](https://github.com/space-wizards/space-station-14/pull/42365), `94071a63508ed4d187652bb60d444ccd027258dc`, 2026-01-12
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: Completing the GenPop locker form now sends `GenpopLockerIdConfiguredMessage` through the predicted BUI path. The shared configuration handler passes `args.Actor` into `LockSystem.Lock` instead of `null`, allowing the lock transition, its popup, and its predicted audio to identify the initiating user on both client and server.
- RMC/CMU divergence: CMU retains the standard GenPop locker, ID-expiration, access-reader, entity-storage, and lock systems. RMC access sets and maps may control where the locker appears, but configuration validation, sentence data, ID creation, storage closing, and prisoner release policy are unchanged.
- Decision and rationale: Port both target-final one-line changes together. Predicting only the message while discarding its actor would leave user-scoped lock feedback unpredicted; passing an actor on a server-only message would not remove the interaction delay. The shared handler already performs access and input validation before either state change.
- Files changed: `Content.Client/Security/Ui/GenpopLockerBoundUserInterface.cs`, `Content.Shared/Security/Systems/SharedGenpopSystem.cs`, `docs/upstream-sync/inventory-wave-0012.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static control-flow review confirms the same serializable message and validation gates are retained, the actor comes from the BUI event, and `LockSystem.Lock` uses it only for scoped popup/audio while dirtying the authoritative lock state normally. Client/shared compilation plus valid configuration, denied access, invalid fields, prediction reconciliation, lock popup/audio, storage closing, ID creation, duplicate input, and disconnect cases are queued for the index-2999 checkpoint.
- Follow-up/debt: The nearby comment notes that verb-driven entity-storage opening is not predicted; keep that separate until its own upstream prediction path is integrated.

## CS-0243 — Log player-authorized APC breaker toggles

- Upstream: [space-wizards/space-station-14#41839](https://github.com/space-wizards/space-station-14/pull/41839), `f20288046193abbf67a940f1faee73e88a3a41a8`, 2025-12-14
- Areas: Interactions
- Status: Ported (adapted)
- Risk: Low
- Behavior/API delta: `ApcToggleBreaker` accepts an optional initiating user. The authorized APC UI handler supplies its actor and records a medium-impact `ItemConfigure` admin log containing the player, APC, and resulting enabled/disabled state after a successful toggle. Non-user callers retain the same toggle behavior without a log attribution.
- RMC/CMU divergence: CMU keeps the shared APC system but has event-rule and EMP callers that programmatically flip breakers. Leaving the new parameter optional preserves those paths and prevents them from being misreported as player actions. The injected logger is non-`readonly` to satisfy CMU's current dependency-injection analyzer; access, battery discharge, UI state, sound, power-network behavior, and RMC maps are unchanged.
- Decision and rationale: Port the target-final logging boundary at the successful state mutation. Logging in the BUI handler before toggling could record rejected or failed changes, while logging every `ApcToggleBreaker` call would lack an accountable actor. Passing only the access-approved actor produces a filterable audit record with the authoritative resulting state.
- Files changed: `Content.Server/Power/EntitySystems/ApcSystem.cs`, `docs/upstream-sync/inventory-wave-0011.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static call-site review confirms the player BUI path supplies `args.Actor`, access denial returns before mutation, and all four automated/event/EMP call sites omit the optional user. Server compilation plus breaker enable/disable, access denial, repeated UI input, EMP, power-grid event, breaker-flip event, battery discharge, UI refresh, audio, and admin-log formatting cases are queued for the index-2999 checkpoint.
- Follow-up/debt: If a future administrative or remote-control caller has a real initiating player, pass that actor deliberately; do not fabricate users for autonomous grid events.

## CS-0244 — Key emergency-shuttle authorizations by ID entity

- Upstream: [space-wizards/space-station-14#42640](https://github.com/space-wizards/space-station-14/pull/42640), `ae5f8d0a6c77b736917c9eed261e254dfc26b777`, 2026-01-25
- Areas: Interactions, GameTicking, Gamerules
- Status: Ported (adapted)
- Risk: Medium
- Behavior/API delta: Emergency-shuttle early-launch authorizations are now a dictionary keyed by the physical ID card's `EntityUid`, with its display name captured as the value. Authorizing rejects an already-used card regardless of later renames, repealing removes that same card identity, and console state exposes only the captured display-name values.
- RMC/CMU divergence: CMU keeps `EmergencyShuttleConsoleComponent` server-side rather than at the upstream shared path, so the data-contract change was applied in place. Standard and RMC ID cards discovered through the retained ID-card system receive identical identity semantics; access checks, authorization count, repeal-all access, announcements, launch timing, and RMC evacuation policy are unchanged.
- Decision and rationale: Port the target-final identity model. A metadata name is mutable and non-unique: renaming one authorized card makes its old authorization impossible to repeal and allows the same physical card to add another name. Entity identity closes both paths while a separate captured string preserves the existing operator-facing list.
- Files changed: `Content.Server/Shuttles/Systems/EmergencyShuttleSystem.Console.cs`, `Content.Server/Shuttles/Components/EmergencyShuttleConsoleComponent.cs`, `docs/upstream-sync/inventory-wave-0013.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static call-site review confirms every add, duplicate check, single repeal, clear, count, launch threshold, and UI enumeration now uses the appropriate dictionary operation. Server compilation plus authorize, rename then reauthorize, rename then repeal, same-name different cards, multiple consoles, repeal all, access denial, deleted card, RMC ID, threshold crossing, and console display cases are queued for the index-2999 checkpoint.
- Follow-up/debt: Authorization remains tied to the card entity rather than a person or account, matching the pinned target. Any future cloning or card-transfer policy change should be evaluated at the game-rule level rather than weakening this identity key.

## CS-0245 — Block hand pickup of chameleon disguises

- Upstream: [space-wizards/space-station-14#42656](https://github.com/space-wizards/space-station-14/pull/42656), `a237493841100673de05dc05c018fc0d02afd3a0`, 2026-01-26
- Areas: Interactions, Gamerules
- Status: Ported (adapted)
- Risk: Low
- Behavior/API delta: Attempts to insert a projected chameleon disguise entity into a hand container are now cancelled and reveal the disguised user. This closes the context-menu pickup route that bypasses the existing `InteractHandEvent` handler and could move or hold the visual disguise entity directly.
- RMC/CMU divergence: CMU retains the hands implementation from before upstream index 2491, so `BeforeGettingEquippedHandEvent` is unavailable. The adaptation uses the retained `ContainerGettingInsertedAttemptEvent` and verifies the destination through `SharedHandsSystem.TryGetHand` before cancelling. Chameleon selection, validity checks, damage mirroring, actions, anchoring, rotation, entity-storage handling, and RMC interaction behavior are unchanged.
- Decision and rationale: Port the target behavior at CMU's equivalent pre-insertion boundary. A generic container cancellation would be broader than upstream and could interfere with non-hand lifecycle operations, while checking the container owner and hand ID precisely reproduces the intended pickup restriction. Revealing after cancellation matches every existing invalid disguise interaction.
- Files changed: `Content.Shared/Polymorph/Systems/SharedChameleonProjectorSystem.cs`, `docs/upstream-sync/inventory-wave-0013.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static pickup-flow review confirms normal hand interaction remains handled before item pickup, direct/context-menu `TryPickup` reaches the cancellable container event, only real hand containers are rejected, and entity-storage insertion retains its separate cancellation. Shared compilation plus direct click, context verb, drag pickup, alternate hand, full hands, remote pickup, storage insertion, disguise reveal, action cleanup, prediction reconciliation, and RMC hand behavior cases are queued for the index-2999 checkpoint.
- Follow-up/debt: When index 2491's hands API is integrated, replace this compatibility hook with upstream `BeforeGettingEquippedHandEvent` and remove the temporary `SharedHandsSystem` dependency after equivalent regression coverage.

## CS-0246 â€” Stop processing followers after invalid band-leader cleanup

- Upstream: [space-wizards/space-station-14#42331](https://github.com/space-wizards/space-station-14/pull/42331), `093257280bd7ea71516553d825aa581f598da570`, 2026-01-26
- Areas: Interactions, GameTicking
- Status: Ported
- Risk: Low
- Behavior/API delta: Instrument updates now stop processing a follower immediately after clearing a deleted leader, a leader without an active-instrument component, or a leader outside the ten-tile band range. Cleanup no longer falls through into component or transform queries using an invalid leader reference.
- RMC/CMU divergence: CMU retains the standard instrument band lifecycle without an RMC replacement in this update path. RMC instruments and maps use the same leader/follower state; MIDI limits, finger-cramp handling, UI requests, range, playback, and cleanup semantics are unchanged.
- Decision and rationale: Port the target-final control-flow fix exactly. `Clean` removes the follower's master relationship, so the rest of that iteration is both stale and unsafe. Continuing the outer entity query prevents deleted-entity exceptions while leaving valid linked instruments on the existing update path.
- Files changed: `Content.Server/Instruments/InstrumentSystem.cs`, `docs/upstream-sync/inventory-wave-0013.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static control-flow review confirms all three cleanup branches continue the outer instrument query and valid leaders still reach range and playback processing. Server compilation plus leader deletion, component removal, out-of-range separation, valid bands, simultaneous cleanup, MIDI-limit cleanup, and repeated update cases are queued for the index-2999 checkpoint.
- Follow-up/debt: None; this is the pinned target's final behavior for the affected loop.

## CS-0247 â€” Preserve perishable scheduling when rot is removed

- Upstream: [space-wizards/space-station-14#42472](https://github.com/space-wizards/space-station-14/pull/42472), `fd0f52592788f0e1b0d7485c8c4dd83161d905f2`, 2026-01-26
- Areas: Medical, Chemistry, GameTicking
- Status: Ported
- Risk: Low
- Behavior/API delta: Shutting down `RottingComponent` no longer resets `PerishableComponent.RotNextUpdate` to zero. Opporozidone and other rot-removal paths therefore retain their normal scheduled perishing tick instead of processing one historical interval per server update until they catch up with current game time.
- RMC/CMU divergence: CMU retains upstream perishing and Opporozidone behavior alongside RMC's tactical-map listener for rotting removal. That listener subscribes independently to `ComponentRemove`, so corpse-marker cleanup still occurs; RMC corpse handling, ammonia generation, cold-storage checks, rejuvenation, and medicine values are unchanged.
- Decision and rationale: Remove the vestigial shutdown subscription and handler exactly as in the pinned target. The server update advances `RotNextUpdate` by only one interval per frame, so zeroing it after a long-running round creates frame-rate catch-up decay and can immediately undo medical rot reduction. Existing map-init and mob-state transitions already initialize legitimate schedules from current time.
- Files changed: `Content.Shared/Atmos/Rotting/SharedRottingSystem.cs`, `docs/upstream-sync/inventory-wave-0013.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static lifecycle review confirms no remaining rot-removal path requires the zero reset, map initialization and living/dead transitions still schedule from current game time, and RMC tactical-map removal remains subscribed. Shared/server compilation plus Opporozidone early/late-round treatment, rejuvenation, revival, repeated rot removal, cold storage, map initialization, tactical-map marker cleanup, and ordinary perishing are queued for the index-2999 checkpoint.
- Follow-up/debt: None; the removed handler was vestigial and the pinned target contains no replacement scheduling hook.

## CS-0248 â€” Allow doors to close over clown spider webs

- Upstream: [space-wizards/space-station-14#42589](https://github.com/space-wizards/space-station-14/pull/42589), `52155802e38c10612ca8197ebe98feec7b334053`, 2026-01-26
- Areas: Interactions, Physics
- Status: Ported
- Risk: Low
- Behavior/API delta: `SpiderWebClown` now uses one non-hard, density-7 `MidImpassable` fixture like the standard web instead of a separate slip-layer trigger plus a density-1000 fixture masked as an item. Doors can close across the web while contact still reaches the web's slippery step-trigger behavior.
- RMC/CMU divergence: RMC resin structures and xeno weeds use separate prototypes and collision rules; only the retained upstream clown-spider web changes. Destruction, food solution, flavor, placement, slip effect, and standard spider webs are unchanged.
- Decision and rationale: Port the pinned fixture definition exactly. The old item-masked fixture made a lightweight floor web participate in door obstruction, while the dedicated slip fixture duplicated contact geometry. A single non-hard web layer preserves overlap/contact semantics without treating the web as a solid item obstacle.
- Files changed: `Resources/Prototypes/Entities/Structures/spider_web.yml`, `docs/upstream-sync/inventory-wave-0013.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static prototype comparison confirms the clown-web fixture now matches the target-final collision block and later upstream edits do not replace it. YAML lint plus door closing/opening, clown-web slipping, walking/running contact, item throws, web destruction, standard webs, RMC resin doors, and map-load cases are queued for the index-2999 checkpoint.
- Follow-up/debt: None; later target changes add construction, damage, and solution behavior but retain this fixture unchanged.

## CS-0249 â€” Initialize gas-canister UI state

- Upstream: [space-wizards/space-station-14#42616](https://github.com/space-wizards/space-station-14/pull/42616), `256ecd3c468e023ae5f4071e62b6b0f2e356c999`, 2026-01-26
- Areas: Interactions, Physics
- Status: Ported
- Risk: Low
- Behavior/API delta: Gas canisters now refresh their bound UI state on map initialization and each UI-open event, so empty or unchanged canisters populate pressure, port, and tank fields without waiting for an atmosphere update. Server-side state generation quietly returns when a partial canister lacks its node container.
- RMC/CMU divergence: CMU retains the shared standard canister UI and server atmosphere implementation. RMC canister prototypes use the same component contract, so they receive the initialization fix without changing gas simulation, port connectivity, release valves, tank slots, appearance, or admin logging.
- Decision and rationale: Port all target-final lines from the upstream commit together. Map initialization supplies the first authoritative state, while UI-open refresh covers pre-map-init mapper use and avoids stale latency for players. Suppressing missing-component logging is required because those lifecycle hooks can legitimately encounter lightweight test or prototype entities that cannot produce full server state.
- Files changed: `Content.Shared/Atmos/Piping/Unary/Systems/SharedGasCanisterSystem.cs`, `Content.Server/Atmos/Piping/Unary/EntitySystems/GasCanisterSystem.cs`, `docs/upstream-sync/inventory-wave-0013.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static event-flow review confirms the new hooks call the existing client/server `DirtyUI` overrides, full canisters still resolve authoritative node state, and partial entities return without mutation or error logs. Shared/client/server compilation plus empty and filled map canisters, UI open before/after map init, connected/disconnected ports, inserted tanks, unchanged pressure, partial test entities, and RMC canister prototypes are queued for the index-2999 checkpoint.
- Follow-up/debt: A later pinned-target commit also refreshes initial pressure appearance; integrate it at its own audited history position rather than broadening this UI-state port.

## CS-0250 â€” Remove duplicate localization lookups

- Upstream: [space-wizards/space-station-14#42648](https://github.com/space-wizards/space-station-14/pull/42648), `7b1ed2bd29eb797c594e9354747f5564d0138cfd`, 2026-01-25
- Areas: Interactions, Gamerules
- Status: Ported
- Risk: Low
- Behavior/API delta: Ten action, objective, map, nuke, forensic-cleaning, and breaker-event strings now pass a localization key through `Loc.GetString` exactly once. Translated output is no longer incorrectly reused as a second key, so errors, completion hints, verb labels, verb descriptions, and event announcement data resolve reliably in every locale.
- RMC/CMU divergence: CMU retains these standard administrative commands, forensic utility verb, and breaker-flip station event alongside RMC-specific commands and game rules. The change is limited to string lookup boundaries; command permissions and mutations, objective assignment, nuke state, evidence cleaning, random selection, and RMC announcements are unchanged.
- Decision and rationale: Port the upstream eight-file cleanup as one atomic unit. Every affected expression has the same defect and the pinned target contains no nested lookup at these sites. Keeping the first lookup preserves interpolation and locale behavior while avoiding a second lookup whose key depends on translated user-facing text.
- Files changed: eight affected `Content.Server` command/system files, `docs/upstream-sync/inventory-wave-0013.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static repository search confirms all ten upstream-targeted `Loc.GetString(Loc.GetString(...))` patterns are gone and no unrelated localization expressions changed. Server compilation plus invalid command arguments, action/objective completion, force-map help, nuke completion, forensic utility verbs, breaker-flip announcements, non-English locales, and RMC command/event coexistence are queued for the index-2999 checkpoint.
- Follow-up/debt: Continue treating any future nested lookup as suspicious, but audit it independently when the inner call intentionally returns a dynamic localization key.

## CS-0251 â€” Include emitter state in toggle logs

- Upstream: [space-wizards/space-station-14#42736](https://github.com/space-wizards/space-station-14/pull/42736), `ce97c45dc29de1333702c4b71846843736289d21`, 2026-02-01
- Areas: Interactions, Physics, Gamerules
- Status: Ported
- Risk: Low
- Behavior/API delta: Successful in-world emitter toggles now record whether the emitter ended `on` or `off` in the existing `FieldGeneration` admin log. The state is read after `SwitchOn` or `SwitchOff`, so the record describes the authoritative result rather than only the attempted interaction.
- RMC/CMU divergence: CMU retains the standard emitter system alongside RMC maps and machinery prototypes. Only player complex-interaction logging changes; anchoring, locking, power, device signals, projectile selection, appearance, popup feedback, and automated emitter state transitions are unchanged and do not gain misleading player logs.
- Decision and rationale: Port the target-final two-line observability improvement exactly. The existing impact level already distinguishes enabling from disabling, but operators should not need to infer state from impact metadata. Capturing `component.IsOn` after mutation makes the record explicit and resilient to future impact-policy changes.
- Files changed: `Content.Server/Singularity/EntitySystems/EmitterSystem.cs`, `docs/upstream-sync/inventory-wave-0013.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static control-flow review confirms the log remains inside the successful anchored/unlocked player interaction path, state text follows completed mutation, and rejected or signal-driven changes are not attributed to a user. Server compilation plus on/off toggles, locked/unanchored rejection, power loss, signal control, RMC emitters, impact filtering, and log formatting are queued for the index-2999 checkpoint.
- Follow-up/debt: Later target emitter-alert features are independent and should retain this explicit state suffix when integrated.

## CS-0252 â€” Log criminal-record status changes

- Upstream: [space-wizards/space-station-14#42691](https://github.com/space-wizards/space-station-14/pull/42691), `1f8365fe9db8a1f7ccc9fb5d18c92a9e6c9eda32`, 2026-01-29
- Areas: Interactions, Gamerules
- Status: Ported (adapted)
- Risk: Low
- Behavior/API delta: After a criminal-record status mutation succeeds, the console now emits a low-impact `Identity` admin log containing the operating mob, target record name, and resulting status transition key. Rejected requests and no-op changes still return before logging.
- RMC/CMU divergence: CMU's current dependency-injection generator requires the logger field to remain non-`readonly`, unlike the historical upstream diff. The standard criminal-record console and security radio flow are retained alongside RMC security systems; access checks, reasons, automatic history, officer identity, radio announcements, record storage, and RMC-specific records are unchanged.
- Decision and rationale: Port the pinned logging boundary immediately after the successful record update and radio notification. Logging earlier could record invalid, unauthorized, or unchanged requests; logging only the UI action would omit the resolved record name and authoritative resulting state. `Identity` keeps these changes filterable in the admin panel.
- Files changed: `Content.Server/CriminalRecords/Systems/CriminalRecordsConsoleSystem.cs`, `docs/upstream-sync/inventory-wave-0013.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static control-flow review confirms access, selected-record, duplicate-state, and reason validation all precede mutation and logging, while the actor and target name come from validated server state. Server compilation plus wanted/detained/suspected/paroled/discharged/none changes, rejected access, malformed reasons, no-op input, radio/history behavior, log formatting, and RMC security coexistence are queued for the index-2999 checkpoint.
- Follow-up/debt: The pinned target later predicts station records and refactors identity lookup; preserve this successful-mutation log when those structural changes are reconciled.

## CS-0253 â€” Toggle a single vote-call menu

- Upstream: [space-wizards/space-station-14#42450](https://github.com/space-wizards/space-station-14/pull/42450), `5b9ff83ce5ed68aeda6f2b0b17d273e42c5030a9`, 2026-01-26
- Areas: Interactions
- Status: Ported (adapted)
- Risk: Low
- Behavior/API delta: The vote-call button is now a toggle that owns one `VoteCallMenu`. Pressing it again closes the open menu, closing the window clears the button's pressed state, and removing the button from the UI tree closes its menu instead of leaving an orphaned window.
- RMC/CMU divergence: CMU's dependency field already uses the current non-`readonly` source-generation form, so only the target behavior was added. Standard and RMC lobby/game UIs using this control share the same vote permissions; available vote types, server authorization, cooldowns, vote creation, and tallying are unchanged.
- Decision and rationale: Port the pinned target's stateful button behavior while preserving CMU's DI declaration. Constructing a fresh window on every press allowed duplicate vote menus and gave the button no way to represent or close the active window. Tracking the instance aligns visual pressed state with the actual menu lifecycle.
- Files changed: `Content.Client/Voting/UI/VoteCallMenuButton.cs`, `docs/upstream-sync/inventory-wave-0013.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static UI lifecycle review confirms at most one tracked open menu is created per button, second press closes it, all close paths clear `Pressed`, and tree removal closes the window. Client compilation plus repeated clicks, title-bar close, tree removal/re-entry, vote permission changes, active vote creation, lobby and RMC UI use, and focus behavior are queued for the index-2999 checkpoint.
- Follow-up/debt: `ExitedTree` retains the pinned target's pre-existing `CanCallVoteChanged += UpdateCanCall` line; its apparent subscribe-versus-unsubscribe issue requires a separate upstream audit rather than being silently changed here.

## CS-0254 â€” Restore typed sneeze emotes

- Upstream: [space-wizards/space-station-14#41479](https://github.com/space-wizards/space-station-14/pull/41479), `5a2da2679e11dd9cb5fb1f04c4fe83bf9eff8c45`, 2026-02-11
- Areas: Interactions
- Status: Ported
- Risk: Low
- Behavior/API delta: The `Sneeze` emote now accepts `sneeze`, `sneezes`, and `sneezed` chat triggers with bare, period, and exclamation variants. It is limited to entities with `VocalComponent` and excludes the `SiliconEmotes` tag, matching other biological disease emotes; the accidental trailing space on the bare `coughed` trigger is also removed.
- RMC/CMU divergence: CMU has one shared sneeze prototype and no RMC override. Human, xeno, and other RMC voice sets continue to control which emotes they expose through their normal vocal configuration, while silicon-specific emote policy remains authoritative. Disease symptoms, involuntary emotes, chat parsing outside these exact triggers, and sound selection are unchanged.
- Decision and rationale: Port the target-final YAML exactly. Without triggers the existing sneeze definition cannot be invoked through typed chat, and without the whitelist/blacklist it would not follow the biological-versus-silicon eligibility boundary used by cough and yawn. Trimming `coughed` restores the intended bare cough match without adding new behavior.
- Files changed: `Resources/Prototypes/Voice/disease_emotes.yml`, `docs/upstream-sync/inventory-wave-0014.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static prototype review confirms all nine target trigger forms are present once, `Vocal` and `SiliconEmotes` dependencies exist locally, and no duplicate sneeze or RMC override conflicts. YAML/prototype lint plus every punctuation variant, non-vocal entities, silicons, RMC species voice sets, disease-triggered emotes, and bare `coughed` are queued for the index-2999 checkpoint.
- Follow-up/debt: Upstream index 2736 adds species-specific sneeze sounds and should remain a separate content/policy port; this base trigger fix does not depend on it.

## CS-0255 â€” Defer APC charge-state refreshes

- Upstream: [space-wizards/space-station-14#42852](https://github.com/space-wizards/space-station-14/pull/42852), `a01e7dcf40e8a25028062b763f9147a81809a2e4`, 2026-02-09
- Areas: Physics, GameTicking
- Status: Ported
- Risk: Low
- Behavior/API delta: `ChargeChangedEvent` now marks an APC for state refresh instead of immediately reading its network battery and updating UI/appearance. The existing APC update loop consumes that invalidation on the next tick after `PowerNetSystem`, preventing map-start races that left APC sprites displaying a fully drained state.
- RMC/CMU divergence: CMU retains the standard APC update loop and its earlier CS-0243 user-attributed breaker logging. Standard and RMC APC prototypes share this state path; battery simulation, breaker mutations, UI-open refresh, EMP behavior, access policy, and logging remain unchanged.
- Decision and rationale: Port the target-final two-line deferral at the event boundary. Startup already uses the same invalidation because power-network state is not valid synchronously, and charge changes during startup have the same ordering hazard. The next-tick query runs after power-net processing and clears `NeedStateUpdate` through the existing authoritative refresh.
- Files changed: `Content.Server/Power/EntitySystems/ApcSystem.cs`, `docs/upstream-sync/inventory-wave-0014.md`, and `docs/upstream-sync/core-system-audit.md`.
- Validation: Static scheduling review confirms `UpdatesAfter` includes `PowerNetSystem`, every invalidated APC with its required components is refreshed, and `UpdateApcState` clears the flag. Server compilation plus empty/charged APC map start, subsequent charge transitions, breaker toggles, UI-open refresh, delayed appearance thresholds, EMP, event-rule flips, and RMC maps are queued for the index-2999 checkpoint.
- Follow-up/debt: None; the pinned target retains this invalidation model through later APC structural changes.

## Upstream checkpoint â€” indices 2000â€“2999

Date completed: 2026-07-20

- Scope: Five committed 200-commit inventories covering pinned SS14 first-parent indices 2000 through 2999, plus accepted ports through CS-0255 across Movement, Shooting, Medical, Chemistry, Interactions, Physics, GameTicking, and Gamerules.
- Solution build: `dotnet build SpaceStation14.slnx --configuration DebugOpt --no-restore --no-incremental --nologo --verbosity:minimal --disable-build-servers` completed in 2 minutes 5 seconds with 0 warnings and 0 errors.
- Unit tests: `dotnet test Content.Tests/Content.Tests.csproj --configuration DebugOpt --no-build --no-restore --nologo --verbosity:minimal` completed with 377 passed, 1 skipped, and 0 failed.
- Resource validation: `dotnet run --project Content.YAMLLinter/Content.YAMLLinter.csproj --configuration DebugOpt --no-build` completed with `No errors found` in 94.4 seconds.
- Integration tests: `dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --configuration DebugOpt --no-build --no-restore --nologo --verbosity:minimal --logger "trx;LogFileName=checkpoint-0003.trx" --results-directory <temporary-directory> -- NUnit.ConsoleOut=0 NUnit.MapWarningTo=Failed` completed with 418 passed, 17 skipped, and 0 failed (435 total) in 34 minutes 59 seconds. Its TRX is outside the repository.
- Disposition: The 2000â€“2999 checkpoint is closed. Per the updated integration strategy, subsequent upstream history will be merged directly with fork-conflict resolution instead of receiving per-commit inventory audit.

## Fast-port checkpoint - direct SS14 merge

Date recorded: 2026-07-20

This is a non-audit checkpoint for the direct fast-port. It does not add per-commit
entries or claim deeper behavioral parity.

- SS14 target: `fbb3c79b2d206eede2210fbbf5ca1c237c262767`.
- Direct merge commits: `3376262730`, `a3f7de91f5`.
- RobustToolbox remains untouched at `7bfa10ec04bfc8f00956419609bd6ec370f9bbac`.
- Integrated range: 3,853 SS14 first-parent commits; the target is an ancestor of the current branch.

| Area | Implemented or deferred semantic note |
| --- | --- |
| Movement | Typed refresh, direction, and climbing adapters are in place. |
| Shooting | Prediction can return an empty projectile list; assisted reload is immediate; legacy vehicle and attachable lookup is not retained. |
| Medical | Fork code uses current damage, battery, body-organ, entity-storage, defibrillator, and cryostorage APIs; synth brains now use the flat organ container. |
| Chemistry | Medicine solution handling moved to `Bloodstream`; transfers, injectors, and blood regulation use current solution APIs. |
| Interactions | Legacy lag-compensation and user-aware storage prechecks are not retained; dynamic alert text remains deferred. The removed upstream gateway system has no remaining RMC/CMU references. |
| Physics | Fork code uses current collision and projectile APIs; deeper behavior comparison is deferred. |
| GameTicking | The upstream base is aligned; deeper game-ticking behavior comparison is deferred. |
| Gamerules | The Distress start-attempt hook moved to a global event; mind roles and scuttle temperature use current components. Removed per-station job-slot scaling falls back to `RMCJobSlotScaling`. |

Validation at this checkpoint: `Content.Shared`, `Content.Client`, `Content.Server`,
and `Content.Server.Database` compile successfully with zero errors. Tests were not
run because this port is at 853 of the 1,000-upstream-commit test checkpoint.

## CS-0256 - Restore RMC gun resolution paths

- Upstreams compared: SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767` and RMC `b6d677947dd8ebcb06194a66798938645fed5a54`.
- Areas: Shooting, Interactions, Vehicles
- Classification: Missing -> Adapted
- Risk: High before the fix; low-to-medium after it.
- Behavior/API delta: The merged `SharedGunSystem.TryGetGun` only resolved an active-hand gun or the queried entity itself. The client shoot-input path uses this resolver before emitting `RequestShootEvent`, so vehicle port guns, an operator's selected hardpoint weapon, an in-hand attachable that supersedes its host gun, and a remotely controlled emplacement could no longer receive ordinary player shoot requests.
- RMC/CMU divergence: These are retained RMC control modes, not intentional divergence. Ordinary SS14 active-hand and self-gun resolution remains unchanged. The RMC paths keep their previous precedence: port gun, selected vehicle weapon, superceding attachable, normal hand/self, then controlled emplacement fallback.
- Decision and rationale: Add two narrow hooks to the current SS14 resolver and implement the fork-specific lookups in `SharedGunSystem.RMC.cs`. This preserves current `Entity<GunComponent>` APIs and central request validation while avoiding duplicate input handlers or broad changes to vehicle, attachable, and emplacement systems.
- Files changed: `Content.Shared/Weapons/Ranged/Systems/SharedGunSystem.cs`, `Content.Shared/Weapons/Ranged/Systems/SharedGunSystem.RMC.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static call-path review covers the sole client `RequestShootEvent` producer and both shared request handlers. Each restored lookup revalidates the operator/weapon relationship and a live `GunComponent`. `dotnet build Content.Shared/Content.Shared.csproj --configuration DebugOpt --no-restore --nologo --verbosity:minimal --disable-build-servers` succeeded with 0 errors and 6 pre-existing warnings; Client/Server wave builds remain required before closing Shooting. Tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Prediction still has duplicate legacy/current request consumers and no longer returns RMC projectile correlation identifiers. That architectural behavior is classified separately as `Behavior changed` and remains deferred pending a prediction-authority reconciliation.

## CS-0257 - Migrate retained RMC battery weapons

- Upstreams compared: SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767` and RMC `b6d677947dd8ebcb06194a66798938645fed5a54`.
- Areas: Shooting, Prototypes
- Classification: Missing -> Adapted
- Risk: High before the fix because the prototypes referenced an unregistered component; low after it.
- Behavior/API delta: `RMCWeaponTaser` and `RMCWeaponBoilgun` still declared the deleted `ProjectileBatteryAmmoProvider`, and `RMCRecharger` still whitelisted that deleted component. Current SS14 unifies projectile and hitscan battery weapons under `BatteryAmmoProviderComponent`, retaining the same `proto` and `fireCost` contract.
- RMC/CMU divergence: The taser's charge cost, projectile, visuals, skills, and melee behavior and the boilgun's selectable projectile modes, charge, and self-recharge remain unchanged. The recharger continues to accept these battery weapons through the current provider marker.
- Decision and rationale: Rename only the two component declarations and their recharger whitelist entry. No compatibility component is needed because all three fields map directly to the current provider and `BatteryWeaponFireModesSystem` already consumes it.
- Files changed: `Resources/Prototypes/_RMC14/Entities/Objects/Weapons/Guns/Energy/taser.yml`, `Resources/Prototypes/_RMC14/Entities/Objects/Weapons/Guns/Other/boilgun.yml`, `Resources/Prototypes/_RMC14/Entities/Structures/Power/recharger.yml`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Repository-wide static search finds no remaining `ProjectileBatteryAmmoProvider` reference and confirms `BatteryAmmoProviderComponent` exposes the retained `proto` and `fireCost` data fields. Targeted builds are recorded at the Shooting-wave boundary; tests and prototype-linter execution remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Runtime battery firing, mode switching, recharging, ammo-counter reconciliation, and charger insertion need focused coverage after the checkpoint.

## CS-0258 - Preserve RMC firearm projectile speed

- Upstreams compared: SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767` and RMC `b6d677947dd8ebcb06194a66798938645fed5a54`.
- Areas: Shooting, Physics, Prototypes
- Classification: Behavior changed -> Adapted
- Risk: Medium before the fix; low after it.
- Behavior/API delta: RMC's `GunComponent` default projectile speed was 53, while the current SS14 component inherits the upstream global default of 40. The bulk merge therefore slowed ordinary firearms inheriting `CMBaseWeaponGun`, changing travel time, leading, collision exposure, and the tuning used with RMC's custom projectile fixture.
- RMC/CMU divergence: The retained speed of 53 is an intentional fork boundary already recorded in `inventory-wave-0010.md`; changing the current SS14 global default would incorrectly alter non-RMC weapons. Specialized RMC launchers, sentries, xeno attacks, and vehicle weapons with explicit speeds continue to override the base value.
- Decision and rationale: Set `projectileSpeed: 53` on the fork-owned `CMBaseWeaponGun` prototype. This expresses the intended divergence at the narrow inheritance root while keeping the current SS14 `GunComponent` and modifier APIs.
- Files changed: `Resources/Prototypes/_RMC14/Entities/Objects/Weapons/Guns/cm_base_gun.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static inheritance review confirms the main RMC pistol, rifle, shotgun, revolver, LMG, smart-gun, launcher, flamethrower, and HMG bases derive from `CMBaseWeaponGun`, while explicit per-weapon speeds still win. Targeted builds are recorded at the Shooting-wave boundary; tests and runtime projectile timing remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Runtime collision/tunneling coverage should exercise both the 53-speed RMC projectile fixture and specialized explicit-speed projectiles after tests are authorized.

## CS-0259 - Reconcile projectile collision authority

- Upstreams compared: SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767` and RMC `b6d677947dd8ebcb06194a66798938645fed5a54`.
- Areas: Shooting, Physics, Networking
- Classification: Missing -> Adapted
- Risk: Critical before the fix; medium after it.
- Behavior/API delta: The fast-port registered both the shared RMC compatibility handler and current server `ProjectileSystem` as owners of `ProjectileComponent` `StartCollideEvent`. The shared handler could mark a projectile spent or queue deletion first, causing the authoritative handler to skip current SS14 destruction-aware penetration, impact sound/effect, camera recoil, red flash, and `BulletHit` logging. Removing the RMC path outright would instead lose damage-popup and RMC multi-hit penetration events.
- RMC/CMU divergence: Client-side RMC predicted collision and explicit xeno/custom calls keep the shared `ProjectileCollide` compatibility entry point. Ordinary server collision now has one authoritative owner, augmented with the retained RMC `ProjectileDamageDealtEvent` and `AfterProjectileHitEvent`; the latter can clear `ProjectileSpent` for an RMC penetrating projectile before deletion. A handled `ProjectileHitEvent` now stops the authoritative path as required by RMC duplicate-hit suppression.
- Decision and rationale: Register the shared RMC physics-collision handler only on clients and add narrow fork hooks to the current server collision implementation. This preserves current damage, logging, feedback, and native penetration APIs without replaying RMC's obsolete whole collision routine or leaving two unordered authoritative owners.
- Files changed: `Content.Shared/Projectiles/SharedProjectileSystem.RMC.cs`, `Content.Server/Projectiles/ProjectileSystem.cs`, `Content.Server/_RMC14/Projectiles/ProjectileSystem.RMC.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Exact lifecycle-pair search confirms these were the two `ProjectileComponent`/`StartCollideEvent` owners on the server. Static event-flow review confirms reflection still precedes hit processing, handled hits return before damage, RMC post-damage events precede deletion, and native impact/logging behavior remains authoritative. The committed Server project currently stops on 11 unrelated missing-reference errors for retained Serilog, Discord.Net, and ImageSharp source; a diagnostic build supplying those three existing baseline dependencies compiled the touched Shared/Server path with 0 errors and 4 unrelated warnings, after which `Content.Server.csproj` was restored unchanged. Client wave validation is still required; tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Runtime coverage must verify normal, reflected, RMC penetrating, xeno-predicted, deleted-target, shooter-deleted, and impact-effect collisions. The legacy client prediction/correlation architecture remains a separate `Behavior changed`/`Deferred` item.

## CS-0260 - Preserve remote-gun target coordinates

- Upstreams compared: SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767` and RMC `b6d677947dd8ebcb06194a66798938645fed5a54`.
- Areas: Shooting, Vehicles, Networking
- Classification: Missing -> Adapted
- Risk: High for moving remote weapons before the fix; low after it.
- Behavior/API delta: Client shoot requests always encoded the mouse target relative to the player entity. RMC vehicle hardpoints retain `GunUseGunOriginComponent`, and the server already chooses the gun as the shot origin for that marker. Encoding from a moving operator could therefore reconstruct a different target on the server when the vehicle and operator transforms diverged during network latency.
- RMC/CMU divergence: Ordinary held guns keep user-relative target coordinates. Only retained RMC weapons explicitly marked `GunUseGunOrigin` use gun-relative coordinates, matching their server projectile-origin policy and previous RMC input behavior.
- Decision and rationale: Add one fork-owned client helper and call it at the current SS14 request-construction boundary. The request schema, target selection, prediction, and server authority remain unchanged.
- Files changed: `Content.Client/Weapons/Ranged/Systems/GunSystem.cs`, `Content.Client/_RMC14/Weapons/Ranged/GunSystem.RMC.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static prototype search finds 16 retained APC, tank, and Humvee hardpoints with `GunUseGunOrigin`; the server uses the same marker when selecting `fromCoordinates`. The committed Client project first stopped on one unrelated missing TerraFX reference; a diagnostic build supplying that existing baseline dependency compiled the touched Client/Shared path with 0 errors and 8 unrelated warnings, after which `Content.Client.csproj` was restored unchanged. Tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Vehicle turret muzzle presentation still has orphaned RMC before-muzzle-flash/tracking hooks and is classified `Missing`/`Deferred` for a separate client presentation adaptation.

## CS-0261 - Honor RMC click-to-fire weapons

- Upstreams compared: SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767` and RMC `b6d677947dd8ebcb06194a66798938645fed5a54`.
- Areas: Shooting, Input, Prediction
- Classification: Missing -> Adapted
- Risk: Medium before the fix; low after it.
- Behavior/API delta: The current client copied the global hold-to-fire preference directly into every `RequestShootEvent`. `GunClickToFireComponent` survived on the SU-6 and M1984 pistols but had no consumer, so holding the attack key repeatedly rearmed their semi-automatic shot counter instead of requiring a release and new click.
- RMC/CMU divergence: Current SS14 hold-to-fire behavior remains unchanged for every unmarked gun. The two retained RMC click-only weapons suppress only the request's `Continuous` flag and continue to use current predicted request, cooldown, and shot-counter APIs.
- Decision and rationale: Apply the marker as a narrow client request policy in the fork-owned gun partial. Restoring the deleted `RearmSemiAuto` field or the old request schema would conflict with current SS14 prediction; suppressing `Continuous` expresses the same release-to-rearm behavior through the live API.
- Files changed: `Content.Client/Weapons/Ranged/Systems/GunSystem.cs`, `Content.Client/_RMC14/Weapons/Ranged/GunSystem.RMC.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Repository search confirms exactly two current prototypes carry `GunClickToFire` and no other consumer existed. Static shot-counter review confirms a non-continuous semi-auto request remains capped after one shot until `RequestStopShootEvent` resets it. Client wave compilation and runtime press/hold/release coverage remain required; tests are deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: The broader old RMC rearm predicate also varied with selected/available fire modes. This adaptation deliberately preserves current SS14 behavior for unmarked guns; mode-wide policy changes are deferred pending playtesting.

## CS-0262 - Ignore remote weapon container owners

- Upstreams compared: SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767` and RMC `b6d677947dd8ebcb06194a66798938645fed5a54`.
- Areas: Shooting, Physics, Vehicles
- Classification: Missing -> Adapted
- Risk: High for mounted weapons before the fix; low after it.
- Behavior/API delta: Current projectile collision prevention ignored only the shooter and weapon. Sixteen retained RMC hardpoints still carry `GunIgnoreContainerOwnerCollisionComponent`, but its containing-container traversal was lost, allowing newly fired vehicle projectiles to collide with the hardpoint's vehicle or another enclosing mount before leaving it.
- RMC/CMU divergence: Unmarked guns retain current SS14 shooter/weapon collision behavior. Marked RMC weapons ignore only owners in their actual containing-container chain; unrelated nearby entities and vehicle occupants remain valid collision candidates.
- Decision and rationale: Call a fork-specific helper from the existing `ProjectileComponent`/`PreventCollideEvent` owner and walk current container APIs from the projectile's weapon outward. This avoids a second unordered collision-policy system and preserves the original marker contract.
- Files changed: `Content.Shared/Projectiles/SharedProjectileSystem.cs`, `Content.Shared/Projectiles/SharedProjectileSystem.RMC.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Exact event-pair review confirms the helper runs in the existing projectile collision-prevention owner. Static prototype and container review finds 16 active hardpoint markers and verifies each owner comparison is limited to the weapon's live containment chain. `dotnet build Content.Shared/Content.Shared.csproj --configuration DebugOpt --no-restore --nologo --verbosity:minimal --disable-build-servers` succeeded with 0 errors and 6 unrelated warnings. Client/Server wave validation and runtime tests remain deferred as recorded for this checkpoint.
- Remaining debt: Runtime coverage should fire each mounted weapon while nested in its hardpoint and vehicle, then verify collisions resume after the projectile exits and still affect unrelated targets.

## CS-0263 - Restore RMC ballistic action timing

- Upstreams compared: SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767` and RMC `b6d677947dd8ebcb06194a66798938645fed5a54`.
- Areas: Shooting, Interactions, Timing, Prototypes
- Classification: Missing -> Adapted
- Risk: High before the fix; medium after it.
- Behavior/API delta: The fast-port discarded the `insertDelay` and `cycleDelay` ballistic-provider contract. Both rocket launchers still declared six-second load/unload actions, assisted reload still supplied a three-second delay, and the serialized delayed event types survived, but all paths inserted or cycled immediately. The HMG magazines also retained two no-op `deleteWhenEmpty: false` fields from a removed optional contract.
- RMC/CMU divergence: Providers without a delay retain current SS14 immediate insertion/cycling. The M5 ATL and HJRA-12 again use their prototype-defined six-second actions, while assisted reload uses `AssistedReloadAmmoComponent.InsertDelay`. Current SS14 `TryBallisticInsert` remains the sole completion-time authority for stack splitting, `BeforeAmmoLoadedEvent`, whitelist/capacity validation, containment, appearance, sound, and ammo counters.
- Decision and rationale: Restore only `TimeSpan` delay fields in a fork-owned component partial, route direct and assisted insertion through current DoAfter APIs, and subscribe the two surviving delayed event types from the gun partial. Completion revalidates through `TryBallisticInsert`; movement cancels, damage does not, and the used round must remain in hand. Remove explicit-false deletion fields rather than reviving unused deletion behavior.
- Files changed: `Content.Shared/Weapons/Ranged/Systems/SharedGunSystem.Ballistic.cs`, `Content.Shared/Weapons/Ranged/Systems/SharedGunSystem.RMC.cs`, `Content.Shared/_RMC14/Weapons/Ranged/BallisticAmmoProviderComponent.RMC.cs`, `Content.Shared/_RMC14/Weapons/Ranged/SharedGunSystem.Ballistic.RMC.cs`, `Resources/Locale/en-US/_RMC14/weapons/guns.ftl`, `Resources/Prototypes/_RMC14/Entities/Objects/Weapons/Guns/HMGs/m2c.yml`, `Resources/Prototypes/_RMC14/Entities/Objects/Weapons/Guns/HMGs/ml66d.yml`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Exact component/event pair search found no other subscribers for either delayed event. Static call-path review covers direct loading, assisted reload, use-in-hand cycling, and cycle verbs; zero-delay providers take the unchanged immediate path, while delayed completion checks capacity/whitelist before delegating to current insertion/cycle methods. `dotnet build Content.Shared/Content.Shared.csproj --configuration DebugOpt --no-restore --nologo --verbosity:minimal --disable-build-servers` succeeded with 0 errors and 6 unrelated warnings. Client/Server wave validation and runtime tests remain deferred as recorded for this checkpoint.
- Remaining debt: Old RMC loading also rejected primed ordnance (restored separately in CS-0264) and non-brand-new expendable lights, while generic provider-to-provider transfer batches differed from current SS14. Light-state rejection remains `Missing` and transfer batching remains `Behavior changed`; runtime cancellation, prediction reconciliation, stack splitting, full-at-completion, dropped-ammo, and launcher/assisted reload coverage is required after the checkpoint.

## CS-0264 - Reject primed ballistic ammunition

- Upstreams compared: SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767` and RMC `b6d677947dd8ebcb06194a66798938645fed5a54`.
- Areas: Shooting, Interactions, Explosives
- Classification: Missing -> Adapted
- Risk: High before the fix; low after it.
- Behavior/API delta: Old RMC direct loading rejected ammunition carrying `ActiveTimerTriggerComponent`, preventing an armed grenade or other timed ordnance from being inserted into a weapon. The fast-port's compatibility loader delegated directly to current ballistic insertion and lost that live-state safety check.
- RMC/CMU divergence: The guard applies only to RMC direct/assisted loading paths routed through `TryAmmoInsert`; unprimed ammunition and current provider-to-provider transfers retain their existing behavior. Timer-trigger activation and explosive behavior are unchanged.
- Decision and rationale: Check the current shared active-timer marker before starting a delayed action or splitting/inserting ammunition, and restore the retained RMC rejection message. This uses live component state instead of prototype identity and remains valid for future primable ammunition types.
- Files changed: `Content.Shared/Weapons/Ranged/Systems/SharedGunSystem.RMC.cs`, `Resources/Locale/en-US/_RMC14/weapons/guns.ftl`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static call-path review confirms both direct interaction and assisted reload reach this guard before DoAfter creation and completion-time stack splitting. Shared wave compilation remains required; tests are deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Non-brand-new expendable-light rejection still needs a cross-client/server adaptation because the current concrete light component is split by project; it remains `Missing` rather than being approximated with prototype checks.

## CS-0265 - Restore RMC client ammo corrections

- Upstreams compared: SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767` and RMC `b6d677947dd8ebcb06194a66798938645fed5a54`.
- Areas: Shooting, UI, Prediction
- Classification: Missing -> Adapted
- Risk: Medium before the fix; low after it.
- Behavior/API delta: Aimed shots, air shots, and tackle-triggered firing still raise `UpdateClientAmmoEvent`, including an artificial `-1` correction when server-side actions consume a round. The fast-port retained those producers and the current ammo-counter artificial-delta API but dropped the sole client consumer, leaving the displayed count stale until later state reconciliation.
- RMC/CMU divergence: Only retained RMC correction events trigger this listener. Standard SS14 predicted ammo updates, control creation, provider counts, and authoritative state reconciliation are unchanged.
- Decision and rationale: Subscribe from the fork-owned client gun partial during the current gun-system lifecycle and forward the event's artificial delta to the existing private ammo-counter updater. No new network event or duplicate ammo state is introduced.
- Files changed: `Content.Client/Weapons/Ranged/Systems/GunSystem.cs`, `Content.Client/_RMC14/Weapons/Ranged/GunSystem.RMC.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Repository-wide search finds three live event producers and no pre-fix consumer; the current client updater already carries `ArtificialIncrease` into `UpdateAmmoCounterEvent`. Client wave compilation remains required; tests are deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Runtime UI coverage must verify the correction is applied once under prediction and reconciles cleanly for aimed shots, air shots, and tackle shots.

## CS-0266 - Remove duplicate RMC shoot-request owners

- Upstreams compared: SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767` and RMC `b6d677947dd8ebcb06194a66798938645fed5a54`.
- Areas: Shooting, Networking, Prediction, Vehicles
- Classification: Behavior changed -> Adapted
- Risk: Critical before the fix; medium after it.
- Behavior/API delta: `SharedGunSystem` and both legacy RMC gun-prediction systems consumed the same `RequestShootEvent`. A request could therefore call `AttemptShoot` twice on client and server; cooldown usually suppressed the second attempt, but event ordering determined whether RMC's vehicle ride-surface target remap reached the actual shot. The old handlers could no longer correlate projectile IDs because the current request schema has no shot list.
- RMC/CMU divergence: Current SS14 shared prediction/server authority is now the sole shoot-request owner. The still-needed RMC rule that converts a clicked vehicle surface into the rider occupying those coordinates runs in that same path on client and server. Xeno/custom projectile collision processing, prediction CVars, and retained predicted-component handlers are not removed.
- Decision and rationale: Move target remapping into a fork helper called by the current request handler, then unsubscribe only the two stale parallel request consumers. This eliminates duplicate mutation without pretending that the old projectile-correlation pipeline works or replaying its deleted request fields.
- Files changed: `Content.Shared/Weapons/Ranged/Systems/SharedGunSystem.cs`, `Content.Shared/Weapons/Ranged/Systems/SharedGunSystem.RMC.cs`, `Content.Client/_RMC14/Weapons/Ranged/Prediction/GunPredictionSystem.cs`, `Content.Server/_RMC14/Weapons/Ranged/Prediction/GunPredictionSystem.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Repository-wide event-owner search found exactly the current shared owner plus the two removed legacy handlers. Static flow review confirms gun identity/combat-mode checks still precede target assignment, and rider selection uses the decoded request coordinates before the one remaining `AttemptShoot`. `dotnet build Content.Shared/Content.Shared.csproj --configuration DebugOpt --no-restore --nologo --verbosity:minimal --disable-build-servers` succeeded with 0 errors and 6 unrelated warnings. Client/Server wave validation remains required; tests are deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: `SharedGunPredictionSystem.ShootRequested` and ordinary-gun predicted projectile components are now compatibility/dead-code debt. Projectile-ID correlation, last-real-tick propagation, server hiding, hit reports, and configurable RMC lag compensation remain `Missing`/`Deferred` for a dedicated redesign; xeno prediction must be preserved during that work.

## CS-0267 - Shooting subsystem behavioral audit wave

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0266.
- Scope and map: client input/effects in `Content.Client/Weapons/Ranged/Systems/GunSystem.cs`; shared request, timing, modifiers, and ammo-provider dispatch in `Content.Shared/Weapons/Ranged/Systems/SharedGunSystem*.cs`; authoritative damage/feedback in `Content.Server/Projectiles/ProjectileSystem.cs`; RMC skills, IFF, dual wield, pointblank, falloff, assisted reload, prediction, attachables, vehicles, emplacements, aimed/air shots, projectile penetration, and muzzle offsets under `Content.Shared/_RMC14/Weapons/Ranged`, `Content.Shared/_RMC14/Projectiles`, `Content.Shared/_RMC14/Vehicle`, `Content.Shared/_RMC14/Attachable`, and their Client/Server counterparts.
- Networking/prediction contract: current `RequestShootEvent` carries gun, coordinates, target, and `Continuous`; generated gun/ammo-provider states retain current SS14 prediction. The removed RMC shot-ID list and last-real-tick fields have no current equivalent. Server `ProjectileSystem` owns ordinary authoritative collision; shared compatibility collision is client-only or explicitly invoked by retained custom prediction.
- Timing/lifecycle contract: `NextFire`, burst state, shot counters, modifiers, and zero-delay providers follow current SS14. RMC ballistic insert/cycle delays now use current predicted DoAfter lifecycle with completion-time validation. Exact `ProjectileComponent` collision-owner and delayed-event subscription pairs were reviewed before changing ownership.
- Prototype/CVar contract: `CMBaseWeaponGun` owns the intentional 53-speed fork boundary; retained battery guns use `BatteryAmmoProvider`; rocket launchers consume restored `TimeSpan` delays. RMC gun-prediction and auto-eject CVars still exist, but not every legacy consumer does.

| Classification | Behavior and evidence | Risk / remaining action |
| --- | --- | --- |
| Aligned | Current SS14 combat-mode/gun identity validation, fire/burst timing, ammo dispatch, generated provider state, muzzle audio/recoil, and server-authoritative spawn path remain in `SharedGunSystem` and Client/Server `GunSystem`. | Runtime parity is not inferred from compilation; ordinary semi/burst/full-auto and provider matrices remain checkpoint test debt. |
| Aligned | Current hitscan dispatch and `HitscanBasicRaycastSystem`, including `RequireProjectileTargetComponent`, remain the live target-aware hitscan path. | Exercise trace selection, damage, effects, and RMC IFF/falloff interaction after tests are authorized. |
| Adapted | CS-0256 through CS-0266 restore remote/attachable resolution, battery provider prototypes, RMC speed, single collision authority plus RMC post-hit hooks, moving-gun coordinates, click-only policy, container-owner exemptions, ballistic timing, primed-ammo rejection, ammo-counter corrections, and one shoot-request owner. | These are high-confidence content-side adaptations to current APIs; each still needs its recorded runtime matrix. |
| Adapted | RMC skills, accuracy, recoil, IFF, falloff, dual-wield, pointblank, aimed/air shot, and attachable relays still subscribe through current `AttemptShootEvent`, `AmmoShotEvent`, `GunShotEvent`, and related modifier events. | Pointblank obstruction/range behavior no longer exactly matches old user-aware checks and should be compared under latency. |
| Missing | Non-brand-new expendable lights can be loaded because concrete light components are split between Client/Server and no shared load-veto adaptation exists. | High explosive/flare-state risk; add a current `BeforeAmmoLoadedEvent` policy in both concrete light systems or a new shared state contract. |
| Missing | `ItemPickupSystem.RecentItemPickUp` has no gun-input guard, and the retained `ItemPickedUpEvent` is itself not raised by the current item lifecycle. | Medium input-exploit risk; repair the pickup lifecycle during Interactions before reconnecting the 0.15-second shooting veto. |
| Missing | `RMCBeforeMuzzleFlashEvent` consumers and `VehicleTurretTrackedMuzzleFlashComponent` remain, but current muzzle-flash creation never raises/creates their data. | Medium presentation risk for vehicles/emplacements; adapt current `MuzzleFlashEvent`/effect creation without changing projectile authority. |
| Missing | `RMCAutoEjectMagazines` remains defined while empty-magazine handling auto-ejects unconditionally and no current options control restores the preference. | Medium UX/policy risk; restore the replicated preference and per-session gate separately. |
| Behavior changed | Current provider-to-provider fill moves one round per repeated DoAfter; old RMC moved up to 20. RMC `BulletBoxSystem` retains its own bulk transfer path. | Keep current generic behavior until inventory/interaction timing is playtested; do not reintroduce batching globally from historical code alone. |
| Behavior changed | Unmarked guns retain current SS14 hold-to-fire behavior rather than the old RMC mode-wide rearm predicate; only explicit `GunClickToFire` weapons regain click-only semantics. | Intentional narrow adaptation; revisit only with balance/playtest evidence. |
| Deferred | Ordinary RMC projectile-ID correlation, `PredictedProjectileClient/ServerComponent` creation, last-real-tick propagation, server projectile hiding, hit reports, and RMC-configurable gun lag compensation are absent. `SharedGunPredictionSystem.ShootRequested` is compatibility/dead-code debt, while xeno prediction still uses adjacent infrastructure. | Critical latency-sensitive redesign. Preserve current SS14 authority and xeno prediction; do not revive deleted request fields piecemeal. |
| Deferred | `Content.IntegrationTests/_RMC14/Weapons/Ranged/RMCLagCompensationTest.cs` is commented out and there is no active RMC shooting behavior suite. | Per instruction, no tests or prototype linter were run before the 1,000-upstream-commit checkpoint. Add focused remote gun, reload cancellation, collision, battery, click-only, ammo UI, and reconciliation coverage then. |

- Validation summary: Shared targeted builds after the final shared changes succeeded with 0 errors and 6 unrelated warnings. Diagnostic Client and Server builds compiled the touched paths with 0 errors after supplying baseline dependencies referenced by existing source but missing from their committed project files; the committed project files were restored unchanged. Direct committed-project Client/Server builds therefore remain baseline-blocked and are reported, not hidden. `git diff --check` ran before every logical commit. No tests were run.
- Audit disposition: Shooting is subsystem-audited but not behaviorally identical. High-confidence accidental losses were fixed; explicit missing and deferred items above prevent a parity claim. The next subsystem is Interactions and Physics, beginning with broken grid-vehicle input relay ownership and then interaction/prototype contracts.

## CS-0268 - Restore grid-vehicle operator input ownership

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU after the Shooting wave.
- Areas: Interactions, Physics, Movement, Vehicles, Prediction
- Classification: Missing -> Adapted
- Risk: Critical before the fix; medium after it.
- Behavior/API delta: Current `Content.Shared.Vehicle.VehicleSystem.TrySetOperator` unconditionally installs an input relay from operator to vehicle. Relay processing clears the operator's own move buttons, but retained `GridVehicleMoverSystem.GetMoverInput` reads those operator buttons directly for tile/grid movement. RMC's old grid branch instead ensured grid mover/operator markers and removed the relay, so the bulk merge left APC/tank/van grid vehicles unable to receive their intended driver input.
- RMC/CMU divergence: Standard SS14 vehicles keep the current relay path. Only `VehicleComponent.MovementKind == Grid` removes the movement relay, keeps direct operator input, ensures `GridVehicleMoverComponent`/`GridVehicleOperatorComponent` on entry, and removes the operator marker on exit. Vehicle eye/view, lock, buckle, access, damage transfer, and operator-set events remain on their current paths.
- Decision and rationale: Reuse the surviving RMC `OnVehicleEnteredEvent`/`OnVehicleExitedEvent` lifecycle subscribers and implement the grid branch in a fork-owned partial. This avoids rewriting current upstream vehicle ownership while restoring the exact input-source distinction consumed by `GridVehicleMoverSystem`.
- Files changed: `Content.Shared/_RMC14/Vehicle/Core/VehicleSystem.cs`, `Content.Shared/_RMC14/Vehicle/Grid/VehicleSystem.Grid.RMC.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static flow review traces base `SetRelay` through `SharedMoverController` input clearing and then traces grid movement's direct `InputMoverComponent` read. Entry cleanup removes both relay endpoints; exit removes the grid operator marker and base cleanup still clears any remaining relay state. `dotnet build Content.Shared/Content.Shared.csproj --configuration DebugOpt --no-restore --nologo --verbosity:minimal --disable-build-servers` succeeded with 0 errors and 6 unrelated warnings. Client/Server builds and tests remain deferred as recorded for this checkpoint.
- Remaining debt: Runtime coverage must drive, stop, exit, re-enter, switch operators, lose the driver component, and reconcile client prediction for every grid vehicle. Standard vehicles need a regression check confirming their relays remain intact.

## CS-0269 - Restore RMC innate step-trigger immunity

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and current CMU.
- Areas: Interactions, Physics, Mobs, Prototypes
- Classification: Missing -> Adapted
- Risk: High before the fix; low after it.
- Behavior/API delta: Human and xeno base prototypes still carry `ImmuneToClothingRequiredStepTriggerComponent`, but current `StepTriggerImmuneSystem` checked only the standard protection component on the tripper or equipped inventory. The retained RMC marker was therefore inert, allowing clothing-gated floor hazards to trigger on species intended to be innately exempt.
- RMC/CMU divergence: Standard SS14 clothing protection and examination remain unchanged. The fork marker is an additional innate exemption used by the two retained RMC species bases; ordinary entities without either protection still trigger hazards normally.
- Decision and rationale: Add one fork-owned predicate to the existing `PreventableStepTriggerComponent`/`StepTriggerAttemptEvent` owner. This preserves one authoritative cancellation point and avoids a second event subscriber with ordering ambiguity.
- Files changed: `Content.Shared/StepTrigger/Systems/StepTriggerImmuneSystem.cs`, `Content.Shared/_RMC14/StepTrigger/StepTriggerImmuneSystem.RMC.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Exact event-pair review confirms the existing system remains the sole owner of this protection decision. Prototype search finds the marker on the retained human-species and xeno bases, and component registration/networking remain live. `dotnet build Content.Shared/Content.Shared.csproj --configuration DebugOpt --no-restore --nologo --verbosity:minimal --disable-build-servers` succeeded with 0 errors and 6 unrelated warnings. Runtime tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Verify representative glass/shard and other `PreventableStepTrigger` hazards against barefoot humans, xenos, protected non-RMC mobs, and unprotected control entities after tests are authorized.

## CS-0270 - Restore emplacement anchoring over barricades

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and current CMU.
- Areas: Interactions, Physics, Construction, Emplacements
- Classification: Missing -> Adapted
- Risk: High before the fix; low after it.
- Behavior/API delta: `SharedWeaponMountSystem` still subscribed to `RMCCheckTileFreeEvent` and explicitly allowed a weapon mount to overlap an anchored `BarricadeComponent`, but the current anchoring collision scan never raised that retained event. Deployed emplacements were therefore rejected as occupying a blocked tile when positioned over the intended barricade support.
- RMC/CMU divergence: Current SS14 collision-layer and hard-body checks remain authoritative for every ordinary anchorable and every non-barricade obstruction. Only an anchoring entity whose fork-owned handler explicitly accepts the particular blocking entity can bypass that one overlap.
- Decision and rationale: Thread the anchoring entity through the current `TileFree` API and invoke a fork-owned overlap predicate from the existing collision branch. Both the initial `CanAnchorAt` precheck and the post-tool completion check pass the entity, preserving the same policy across the full action lifecycle and avoiding a second anchoring owner.
- Files changed: `Content.Shared/Construction/EntitySystems/AnchorableSystem.cs`, `Content.Shared/_RMC14/Construction/Anchored/AnchorableSystem.RMC.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Repository-wide event-pair review found one retained producer contract and one `WeaponMountComponent` consumer; the pre-fix producer was absent. Static flow review confirms both collision checks now evaluate the same per-obstruction event and that the default remains blocked. Shared targeted compilation succeeded with 0 errors and 6 unrelated baseline warnings; runtime tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Runtime coverage must deploy a weapon mount over a barricade, reject it over another hard obstruction, recheck after the tool delay when occupancy changes, and confirm ordinary anchorables still reject barricades.

## CS-0271 - Restore the RMC direct-pickup lifecycle event

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and current CMU.
- Areas: Interactions, Hands, Prediction, Item presentation
- Classification: Missing -> Adapted
- Risk: Medium before the fix; low after it.
- Behavior/API delta: The retained broadcast `ItemPickedUpEvent` had live client and shared consumers, but current `SharedItemSystem.OnHandInteract` only assigned the result of `TryPickup` and no longer raised the event after a successful direct world interaction. The client pickup timer never started and fork items with a stored world-sprite offset never reset that offset when picked up through this path.
- RMC/CMU divergence: The event is raised only after the same direct `InteractHandEvent` pickup that old RMC covered. Programmatic hand insertion, pickup verbs, inventory transfers, and failed interactions do not gain a new notification, so current SS14 hand/container authority and prediction breadth remain unchanged.
- Decision and rationale: Keep current `SharedHandsSystem.TryPickup` as the sole state mutation and call a fork-owned event helper only when it succeeds. This restores the historical event timing without introducing a parallel pickup implementation or raising success before the hand container accepts the item.
- Files changed: `Content.Shared/Item/SharedItemSystem.cs`, `Content.Shared/_RMC14/Hands/SharedItemSystem.RMC.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Exact producer/consumer review found no producer before this change, one local-player timing consumer, and one component-targeted sprite-offset consumer. The raise point matches old RMC's post-success branch and broadcasts against the picked-up item. `dotnet build Content.Shared/Content.Shared.csproj --configuration DebugOpt --no-restore --nologo --verbosity:minimal --disable-build-servers` succeeded with 0 errors and 6 unrelated warnings; tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: The restored client timer is not yet consulted by current gun input; reconnect that veto separately. Pickup verbs and programmatic pickups intentionally retain current behavior unless playtest evidence requires a broader event contract.

## CS-0272 - Restore the post-pickup shooting veto

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and current CMU after CS-0271.
- Areas: Shooting, Interactions, Client input, Prediction, Timing
- Classification: Missing -> Adapted
- Risk: Medium before the fix; low after it.
- Behavior/API delta: RMC's client pickup system still tracked a successful local direct pickup for 0.15 seconds, but current gun input no longer consulted that state before raising `RequestShootEvent`. A player could therefore combine pickup and firing in the interval the fork intentionally reserved for hand-state reconciliation.
- RMC/CMU divergence: The veto remains client-input-only, lasts the retained 0.15 seconds, and is reset by the retained `RequestStopShootEvent` path. Server gun authority, current cooldown/burst state, non-gun interactions, and pickup paths that do not raise `ItemPickedUpEvent` are unchanged.
- Decision and rationale: Query the retained fork timer from a small client gun partial immediately before request emission. All current target/coordinate calculation remains intact, but no predicted request is sent during the protected interval; this matches old RMC without reviving its deleted projectile-correlation request schema.
- Files changed: `Content.Client/Weapons/Ranged/Systems/GunSystem.cs`, `Content.Client/_RMC14/Weapons/Ranged/GunSystem.RMC.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static flow review traces CS-0271's post-success pickup event to the local-client timer and this guard to the sole current `RequestShootEvent` producer. A diagnostic `Content.Client` targeted build compiled the touched Client and Shared paths with 0 errors and 14 unrelated warnings after temporarily supplying the baseline TerraFX reference used by retained source; the committed project file was restored unchanged. Tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Runtime prediction coverage must pick up and immediately fire, fire after 0.15 seconds, release fire during the veto, and verify programmatic/verb pickups retain their current scope.

## CS-0273 - Restore contained-user drop cleanup

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and current CMU.
- Areas: Interactions, Hands, Containers, Inventory, Item lifecycle
- Classification: Missing -> Adapted
- Risk: High before the fix; low after it.
- Behavior/API delta: Current hand dropping returns early after `DropNextTo` when the user is inside another container, bypassing the normal `DroppedInteraction` notification. Old RMC raised `RMCDroppedEvent` in that branch, and retained consumers still depend on it to deactivate targeting, motion/intel detectors and rangefinders, return magnetic equipment, and track RMC pickup-managed items.
- RMC/CMU divergence: Ordinary world drops continue through current `DoDrop` and its standard `DroppedEvent`; they do not receive an extra fork event. The restored notification is limited to the contained-user early-return branch where the standard dropped interaction is absent, matching old RMC event scope and avoiding duplicate cleanup.
- Decision and rationale: Keep current transform/container mutation first, then broadcast the retained event against the dropped item immediately before the early return. A fork-owned helper isolates the event contract while leaving hand authority, range calculation, logging, and normal drop sequencing untouched.
- Files changed: `Content.Shared/Hands/EntitySystems/SharedHandsSystem.Drop.cs`, `Content.Shared/_RMC14/Hands/SharedHandsSystem.Drop.RMC.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Exact lifecycle review confirms all live `RMCDroppedEvent` consumers also handle ordinary drop-related state, while only this contained-user path skips `DroppedInteraction`. The raise point matches old RMC after `DropNextTo` and before return. `Content.Shared` targeted compilation succeeded with 0 errors and 6 unrelated warnings; tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Runtime coverage must drop each retained consumer item while buckled/inside a vehicle or other container, verify cleanup occurs once, and confirm ordinary world drops still emit only the standard interaction event.

## CS-0274 - Restore construction tool duplicate conditions

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and current CMU.
- Areas: Interactions, Construction, DoAfter, Prototypes, Server authority
- Classification: Missing -> Adapted
- Risk: High before the fix; low after it.
- Behavior/API delta: Forty-eight retained RMC construction graph steps still serialize `duplicateConditions: All`, but the current `ToolConstructionGraphStep` contract no longer declared the field and the server tool path could not pass it into current `DoAfterArgs`. This left a live YAML contract unknown and prevented graph authors from controlling construction-tool DoAfter deduplication.
- RMC/CMU divergence: Current SS14 defaults tool DoAfters to `All`; old RMC defaulted omitted CM graph fields to `None` and explicitly set 48 of 254 retained tool steps to `All`. The adaptation preserves both policies: an explicit field wins, omitted `IsCM` graphs use `None`, and omitted non-CM graphs retain current `All`. Tool validation, fuel consumption, timing, cancellation, and server completion remain on the current tool system.
- Decision and rationale: Declare a nullable serialized field in a fork partial, add an optional duplicate-condition parameter to the current `UseTool` overload with the current `All` default, and resolve omitted values from the active construction graph's inherited `IsCM` contract at the authoritative server interaction. This restores the data contract and historical CM default without changing upstream recipes, reinstating old prediction flags, or creating a parallel DoAfter implementation.
- Files changed: `Content.Shared/_RMC14/Construction/Steps/ToolConstructionGraphStep.RMC.cs`, `Content.Shared/Tools/Systems/SharedToolSystem.cs`, `Content.Server/Construction/ConstructionSystem.Interactions.cs`, `Content.Server/_RMC14/Construction/ConstructionSystem.DuplicateConditions.RMC.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Repository-wide prototype review found 254 retained RMC tool steps: 48 explicitly `All` and 206 omitted. Current `SharedDoAfterSystem` consumes `DoAfterArgs.DuplicateCondition` during duplicate detection. `Content.Shared` targeted compilation succeeded with 0 errors and 6 unrelated warnings. A diagnostic `Content.Server` build compiled the touched Server and Shared paths with 0 errors and 10 unrelated warnings after temporarily supplying baseline dependencies referenced by retained source; the committed project file was restored unchanged. Prototype validation and runtime tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: CM construction filtering, capability/attempt hooks, and the `rmcPrototype` barricade route remain critical `Missing` findings. After tests are authorized, validate duplicate construction attempts for same/different event, target, and tool masks.

## CS-0275 - Interactions and Physics behavioral audit wave

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0274.
- Scope and map: interaction/access/verbs in `Content.Shared/Interaction`, `Content.Shared/Ghost`, and `Content.Shared/Verbs`; hands/inventory/storage in `Content.Shared/Hands`, `Content.Shared/Inventory`, `Content.Shared/Storage`, and their `_RMC14` extensions; construction in Client/Shared/Server `Construction`; DoAfter in Client/Shared `DoAfter`; collision, anchoring, pulling, buckle, throwing, step triggers, and grid vehicles in the corresponding Shared/Server systems plus `_RMC14/Movement`, `_RMC14/Vehicle`, `_RMC14/Buckle`, `_RMC14/Fireman`, and `_RMC14/Xenonids`.
- Networking, prediction, and authority: current shared storage messages and server validation, construction requests/acknowledgements, predicted DoAfters, buckle component states, pulling events, and throw lifecycle remain the authoritative SS14 paths. Fork pickup/drop events are local broadcasts, while CS-0274 resolves construction deduplication only on the server. Several retained RMC fields/events compile but have no current producer or consumer, as classified below.
- Timing and lifecycle: current construction/storage/hand mutation remains state-owner; CS-0271/CS-0273 restore notifications only after successful mutation. CS-0270 checks anchor overlap before and after tool use. RMC DoAfter cancellation/effects, storage-open delay, buckle delay, pull-toggle, and remote buckle-state presentation still have broken or absent lifecycle seams.
- Prototype and CVar contract: 254 RMC construction tool steps now preserve graph-aware duplicate defaults; 26 direct `LimitedStorage`, 3 `StorageStoreSkillRequired`, 48 `IgnoreContentsSize`, and 112 direct `FixedItemSizeStorage` declarations remain disconnected from normal storage policy/geometry. `buckleDelay`, `clickUnbuckle`, and nullable weapon-mount alert values remain live YAML against missing or narrowed component contracts. Standard `storage.limit` and retained RMC storage-label controls are live; no buckle/pull-specific CVar was found.

| Classification | Behavior and exact evidence | Risk / remaining action |
| --- | --- | --- |
| Aligned | Current construction ghost/request/acknowledgement and server state machine remain in `Content.Client/Construction/ConstructionSystem.cs`, `Content.Shared/Construction/Events.cs`, and `Content.Server/Construction/ConstructionSystem.Initial.cs`. | Generic SS14 construction remains authoritative; RMC eligibility/routes below are separate missing policy. |
| Aligned | Current predictive storage message and authoritative validation remain in `Content.Shared/Storage/StorageComponent.cs` and `Content.Shared/Storage/EntitySystems/SharedStorageSystem.cs`; `StorageInteractAttemptEvent` still reaches `Content.Shared/_RMC14/Storage/RMCStorageSystem.cs`. | Standard lock/capacity behavior survives, but RMC insert policy and geometry do not. |
| Aligned | `AccessibleOverrideEvent`, verb equipment access, `CountFreeableHands`, and DoAfter `RootEntity` behavior remain connected through `SharedInteractionSystem`, `SharedVerbSystem`, `SharedHandsSystem`, and `Content.Shared/_RMC14/Movement/DoAfterMobCollisionSystem .cs`. | Retain these current improvements; do not replay obsolete gateways or SmartEquip messages. |
| Aligned | RMC `PullAttemptEvent`, pull start/stop cleanup, current throw authority, `BeforeThrowEvent`, and `ThrowerImpulseEvent` remain live in `PullingSystem`, `ThrowingSystem`, `ThrownItemSystem`, and server hands. | Fireman/devour entry and rollerbed retargeting are missing producers, not a failure of the surviving base lifecycle. |
| Aligned | `Content.Shared/_RMC14/Movement/RMCMovementSystem.cs` still owns RMC mob-collision fixture creation/lifecycle, and xeno collision/resin-window consumers remain connected. | The separate collision-mass marker is orphaned below. |
| Adapted | CS-0268 through CS-0274 restore grid-vehicle operator input, innate step immunity, emplacement/barricade anchoring, direct pickup plus shooting timing, contained-user drop cleanup, and graph-aware construction duplicate conditions. | High-confidence current-API fixes; each retains the runtime matrices recorded in its decision. |
| Adapted | Current shared/predicted SmartEquip command plumbing and current SS14 access checks replace old RMC custom network messages. | Keep the modern plumbing; restore only the missing RMC equipment destinations and authorization policy after storage integration. |
| Missing | Client `ConstructionMenuPresenter` enumerates all recipes and Server `ConstructionSystem.Initial` uses ordinary `TryIndex`; retained `CMPrototypeExtensions.FilterCM`, `ConstructionPrototype.IsCM`, `RMCConstructionAttemptEvent`, `CanConstruct`, and `DisableConstruction` no longer gate both client preview and server acceptance. | Critical accidental authorization loss. Restore `EnumerateCM`/`TryCM`, user-aware attempt events, and client/server capability checks as one paired wave. |
| Missing | Five live `rmcPrototype` barricade recipes bypass `RMCConstructionSystem.Build` and use generic construction timing/placement. | Critical accidental behavior loss: skill, material, collision, movement, damage-cancellation, and server-completion rules differ. Restore the presenter/server bridge together. |
| Missing | Normal `SharedStorageSystem.CanInsert` does not call retained RMC limit/skill/size helpers; `FixedItemSizeStorage`'s adapter is absent from six authoritative shape paths and client grid preview; `CMStorageItemFillEvent` has consumers but no fill producers. | Critical accidental storage divergence. Thread actor-aware policy and storage-aware geometry through shared/client paths before SmartEquip or bulk-transfer fixes. |
| Missing | `CMKeyFunctions` and consumers survive, but `Content.Client/Input/ContentContexts.cs`, `Resources/keybinds.yml`, and the rebind UI do not expose RMC holster, dropped-item, other-hand, smart-equip, rest/resist, attachable, or xeno inputs. | Critical cross-subsystem usability loss. Review current binding collisions before restoring context/defaults/rebind exposure. |
| Missing | `BuckleComponent` lost networked `BuckleDelay`/`ClickUnbuckle`; `StrapComponent.BuckledAlertType` is non-null despite weapon-mount YAML `null`; current buckle preview/authority ignores `StrapComponent.Enabled`, RMC xeno policy, and per-entity offsets. | Critical accidental prototype and authority loss. Restore fields/nullability first, then preview/server policy and all three offset sites together. |
| Missing | `PullingSystem.TogglePull` no longer raises retained `RMCPullToggleEvent`; pull start/buckle paths no longer raise/consume `RMCGetPullTargetEvent`; Server `PullController` lacks fireman-carried guards. | Critical/high accidental loss: aggressive fireman grab and pull-toggle devour are unreachable, rollerbeds pull the occupant, and carried targets can receive pull impulses. |
| Missing | `SharedInteractionSystem` never raises retained combat-mode override; ghost action/access/range paths ignore `RMCIgnoreGhostInteractionLimits`; `GameplayStateBase` does not consult `RMCClientInteractionSystem` transparency; `RotateToFaceSystem` does not call the retained sentry clamp. | High accidental gameplay loss across devour, observer vehicle/ladder use, vehicle/interior clicking, and sentry arcs. Restore narrow fork hooks without replacing current access authority. |
| Missing | `RMCHandsSystem` storage-eject helper is absent from hand interaction, inventory equip, and storage eject seams. Storage-open DoAfter, nested health-analyzer keep-open/access, quick-insert serialization/default timing, and two-sided bulk-transfer authorization are also disconnected. | High broad inventory loss. Repair after the core insertion/geometry contract so user-aware checks are not duplicated. |
| Missing | Retained DoAfter fields `BreakOnRest`, `TargetEffect`, and `ForceVisible` have incomplete execution/presentation: `SharedDoAfterSystem.Update` skips the RMC cancel hook, periodic target effects have no server owner, and `DoAfterOverlay` ignores fork visibility/occlusion policy. | High cross-subsystem debt, especially Medical/Chemistry. Split cancellation, authoritative effects, and client overlay into separate fixes. |
| Missing | `RMCMobCollisionMassComponent` remains on the xeno base but `RMCMovementSystem` never reads it; current fixture creation uses the current fixed mass policy. | High big-xeno collision behavior loss. Reconcile mass and the retained collision CVar/component against current physics APIs; do not modify RobustToolbox. |
| Missing | `RMCSprayAmmoProviderComponent.HitUser` is passed by the flamer but discarded by Server `RMCSpraySystem`; `ThrownItemSystem` always exempts the thrower and `ThrownHitUserComponent` has no live producer. | High accidental self-hit loss. Add an opt-in marker only for RMC spray entities so ordinary SS14 throws remain exempt. |
| Missing | `RMCAdminVerb` remains defined/subscribed but is absent from `Verb.VerbTypes` and `SharedVerbSystem` collection. | Medium accidental admin-UX loss. Restore type registration and collection using current access values. |
| Behavior changed | Legacy RMC interaction last-real-tick/rewind compensation is absent; current interaction range/access uses present state. | Intentional fast-port approximation recorded at the checkpoint. Redesign against current server authority rather than replaying deleted fields. |
| Behavior changed | “Interact with other hand” now uses `UIHandClick` and may switch the active hand; remote RMC buckle draw depth lacks an after-state reconciliation signal; RMC hand-throw facing/audio/lunge/popup presentation was replaced by current recoil behavior. | Medium presentation/input differences. Other-hand is currently masked by missing bindings; throw restoration needs an explicit hand-throw flag because guns also use throwing. |
| Behavior changed | Current physics lag compensation follows current SS14 timing rather than retained RMC configurable last-real-tick data. | Deferred latency-sensitive divergence; preserve current authority until a measured redesign exists. |
| Deferred | Examination-specific differences, admin-ghost inventory bypass, item-aware drop blocking, fixed-storage icon/border presentation, and stale `RMCAllowStrapMovementComponent` need narrower audits. | Do not infer parity from this bounded wave. Wheelchair/powerloader relay input may already supersede the stale strap marker. |
| Deferred | Upstream interaction, hands, storage, construction, buckle, pulling, throwing, and DoAfter tests exist, but no active RMC matrices cover the missing policies above. | No tests or prototype linter were run at 853/1,000 commits. Add paired client/server tests after the checkpoint. |

- Validation summary: repeated `Content.Shared` targeted builds after the final Shared changes succeeded with 0 errors and 6 unrelated warnings. Diagnostic Client compilation of touched Client/Shared paths succeeded with 0 errors and 14 unrelated warnings after temporarily supplying the baseline TerraFX reference; diagnostic Server compilation of touched Server/Shared paths succeeded with 0 errors and 10 unrelated warnings after temporarily supplying retained-source baseline package references. Both committed project files were restored unchanged. Direct committed Client/Server builds remain baseline-blocked as recorded in CS-0267. `git diff --check` ran before every logical commit. No tests or prototype validation were run.
- Audit disposition: this is a deep but bounded Interactions/Physics wave, not a parity claim and not completion of the subsystem. It maps the highest-risk authority, prediction, timing, lifecycle, prototype, CVar, and test gaps and closes several high-confidence losses, but the critical construction, storage, input, buckle, and pulling findings keep Interactions/Physics open.
- Next recommended subsystem: continue Interactions/Physics with CM construction filtering plus authoritative capability/attempt hooks and the five `rmcPrototype` bridges; then repair storage policy/geometry before moving to Medical and Chemistry, whose DoAfter behavior depends on this unfinished interaction foundation.

## CS-0276 - Restore RMC pull lifecycle hooks

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0275.
- Areas: Interactions, Physics, Pulling, Buckle, Fireman carry, Xeno devour
- Classification: Missing -> Adapted
- Risk: Critical before the fix because retained pull consumers were unreachable; low-to-medium after it.
- Behavior/API delta: The current `PullingSystem` no longer raised `RMCPullToggleEvent` when a user toggled an entity they already pulled, and it never raised the retained `RMCGetPullTargetEvent` before starting a pull or after the target became buckled. `FiremanCarrySystem`, `XenoDevourSystem`, and `RMCPullingSystem` still subscribe to those events. Aggressive fireman grab and pull-toggle devour could therefore never take ownership, while pulling an occupant onto an `RMCRetargetBucklePull` rollerbed stopped the pull instead of transferring it to the bed.
- RMC/CMU divergence: Ordinary SS14 pull start/stop, validation, joints, virtual hands, alerts, and prediction remain authoritative. A toggle is intercepted only when a retained RMC consumer marks the directed event handled. Retargeting changes the target only when a live RMC handler supplies a different entity; all other targets continue through the current pull path unchanged.
- Decision and rationale: Add three small fork helpers in a `_RMC14` partial and invoke them at the current owner boundaries: before a same-target stop, before `TryStartPull` resolves components, and when a pulled entity becomes buckled. This reconnects the domain events without reintroducing a second pulling system or replaying obsolete physics code.
- Files changed: `Content.Shared/Movement/Pulling/Systems/PullingSystem.cs`, `Content.Shared/_RMC14/Pulling/PullingSystem.RMC.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static subscription review confirms `FiremanCarrySystem` and `XenoDevourSystem` consume `RMCPullToggleEvent`, while `RMCPullingSystem` retargets only buckled entities whose strap carries `RMCRetargetBucklePullComponent`. `dotnet build Content.Shared/Content.Shared.csproj --configuration DebugOpt --no-restore --nologo --verbosity:minimal --disable-build-servers` succeeded with 0 errors and 6 unrelated warnings. `git diff --check` passed. Tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Runtime coverage must exercise ordinary stop toggles, fireman aggressive grab, xeno devour pull toggles, pulling a patient onto and off a rollerbed, failed retarget resolution, and client reconciliation. Server `PullController` still needs a separate fireman-carried impulse guard.

## CS-0277 - Exclude fireman-carried entities from pull motion

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0276.
- Areas: Interactions, Physics, Pulling, Fireman carry, Server authority
- Classification: Missing -> Adapted
- Risk: High before the fix because two movement owners could act on a carried target; low after it.
- Behavior/API delta: Current server `PullController` accepted pointer-directed pull movement and applied its per-tick acceleration/settling impulses to entities carrying `BeingFiremanCarriedComponent`. The retained fireman system already owns their attachment and movement state, so a carried target could be pushed away from its carrier, accumulate velocity, or reconcile against competing authoritative transforms.
- RMC/CMU divergence: Ordinary pulled entities retain current SS14 drag range, joint, rotation, acceleration, settling, conveyor, gravity, and action-blocker behavior. Only targets actively marked as fireman-carried reject a new pull-move request and have any stale `PullMovingComponent` removed before the physics solve.
- Decision and rationale: Keep `PullController` as the sole pull-motion authority and add a fork helper queried at its input and fixed-step boundaries. This matches the retained component lifecycle without adding a second controller or modifying RobustToolbox physics.
- Files changed: `Content.Server/Movement/Systems/PullController.cs`, `Content.Server/_RMC14/Movement/PullController.RMC.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static review confirms both guards run before mutation or impulse application and remove stale directed-motion state. `git diff --check` passed. The first targeted Server build reached the touched files but was temporarily blocked by incomplete, disjoint construction-agent edits in the shared worktree; Server compilation must be rerun after that patch is complete. Tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Runtime coverage must begin a pull move immediately before fireman carry, ensure the queued motion is cleared, verify no carried-target impulses occur, and confirm normal pulled entities retain current motion behavior.

## CS-0278 - Restore RMC buckle prototype policy

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0277.
- Areas: Interactions, Buckle, Prediction, Prototypes, Alerts
- Classification: Missing -> Adapted
- Risk: Critical before the fix because live YAML fields were absent and disabled straps could still accept buckles; low-to-medium after it.
- Behavior/API delta: `buckleDelay` and `clickUnbuckle` remained on RMC bodybags and xenos but were absent from the generated `BuckleComponent` contract. Drag buckling therefore always used the strap delay, and empty-hand clicks could unbuckle entities that intentionally disabled that route. Weapon mounts still serialized `buckledAlertType: null` against a non-null component field. Although `StrapComponent.Enabled` remained networked, drag preview and the authoritative `CanBuckle` path did not both reject disabled straps. The retained xeno-user dexterity rule also survived only in an orphaned `RMCBuckleSystem.CanBuckle` adapter.
- RMC/CMU divergence: Current SS14 buckle validation, predicted DoAfter, component state generation, attempt events, containment/range checks, and mutation remain authoritative. Fork fields only override the strap delay or click behavior when explicitly configured. Unmarked buckles retain current defaults, non-null alerts behave normally, and the RMC user restriction applies only to xenos.
- Decision and rationale: Restore the two networked fork fields in a `_RMC14` component partial, make the alert prototype nullable at the live owner, and consume all policy at the current preview/authority seams. Move the xeno-user check into a narrow `SharedBuckleSystem` partial and remove the dead wrapper method so there is one buckle decision path.
- Files changed: `Content.Shared/Buckle/Components/StrapComponent.cs`, `Content.Shared/Buckle/SharedBuckleSystem.Interaction.cs`, `Content.Shared/Buckle/SharedBuckleSystem.Buckle.cs`, `Content.Shared/_RMC14/Buckle/BuckleComponent.RMC.cs`, `Content.Shared/_RMC14/Buckle/SharedBuckleSystem.RMC.cs`, `Content.Shared/_RMC14/Buckle/RMCBuckleSystem.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static prototype review confirms `buckleDelay`/`clickUnbuckle` are used by two bodybag variants and the xeno base, while the nullable alert is used by the retained weapon-mount base. The Shared compiler accepted the touched buckle types and generated fields before stopping on three incomplete, disjoint storage-agent errors; a clean Shared build must be rerun after storage integration. `git diff --check` passed. Tests and prototype validation remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Per-buckle spatial offsets and remote client draw-depth reconciliation are separate fixes. Runtime coverage must test disabled straps, bodybag interaction routes, zero-delay xeno/bodybag buckling, click-disabled unbuckling through verbs/alerts versus hand interaction, null alerts, and xeno-user denial.

## CS-0279 - Compose RMC buckle offsets with current transforms

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0278.
- Areas: Interactions, Physics, Buckle, Transforms, Prototypes
- Classification: Missing -> Adapted
- Risk: High before the fix for xeno and parasite positioning; low after it.
- Behavior/API delta: `RMCBuckleOffsetComponent` remained networked on the xeno base, larvae, and parasites, but current buckle transform validation, buckle placement, and unbuckle placement used only `StrapComponent.BuckleOffset`. Marked entities were centered incorrectly, and any external correction to their intended fork offset caused the authoritative transform check to unbuckle them.
- RMC/CMU divergence: Standard entities retain the current SS14 strap-only offset. Marked RMC entities add their per-entity offset to the strap offset. The current transform APIs and owner ordering remain unchanged; the old RMC unbuckle routine is not replayed.
- Decision and rationale: Centralize the composed offset in the existing `_RMC14` `SharedBuckleSystem` partial and use it at all three current owner sites. This preserves current reparenting and placement semantics while restoring the live fork data contract.
- Files changed: `Content.Shared/Buckle/SharedBuckleSystem.Buckle.cs`, `Content.Shared/_RMC14/Buckle/SharedBuckleSystem.RMC.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static search confirms all live `RMCBuckleOffset` prototypes now reach the single composed helper in transform validation, buckle placement, and unbuckle placement. `git diff --check` passed. Shared compilation is queued behind the disjoint storage patch currently being completed in the shared worktree; no buckle-specific compiler error was reported by the preceding build. Tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Runtime coverage must buckle/unbuckle each marked xeno size against nests, bodybags, rollerbeds, and vehicle straps, including rotated parents and state reconciliation. Remote draw-depth refresh remains a separate client fix.

## CS-0280 - Reconcile remote RMC buckle draw depth

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0279.
- Areas: Interactions, Buckle, Client presentation, Networking, Lifecycle
- Classification: Missing -> Adapted
- Risk: Medium before the fix; low after it.
- Behavior/API delta: The current RMC buckle visualizer refreshed fork draw-depth policy only from local `BuckledEvent`, `UnbuckledEvent`, `StrappedEvent`, and `UnstrappedEvent` notifications. Remote clients receive generated `BuckleComponent` and `StrapComponent` state without necessarily running those local mutation events, so xenos, weapon mounts, seats, and straps could retain stale layering until another visual event occurred.
- RMC/CMU divergence: Local predicted and authoritative domain events remain the immediate refresh path. The restored after-state handlers only recompute RMC draw depth after generated network state is applied; they do not mutate buckle state or compete with the shared buckle owner.
- Decision and rationale: Restore the old RMC after-state presentation seam alongside the current domain-event refreshes. Exact lifecycle-pair search found no other `AfterAutoHandleStateEvent` subscriber for either component, satisfying the single-owner constraint.
- Files changed: `Content.Client/_RMC14/Buckle/RMCBuckleVisualsSystem.cs` and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static review confirms both generated component states now trigger the existing `RMCSpriteSystem.UpdateDrawDepth` path, while local events still update both buckle and strap immediately. `git diff --check` passed. Client compilation is queued behind the disjoint construction patch in the shared worktree; tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Runtime coverage must compare local prediction and a remote observer while buckling, unbuckling, rotating a strap, changing vehicle seats, and applying explicit RMC buckle/strap draw-depth components.

## CS-0281 - Restore the RMC admin context verb

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0280.
- Areas: Interactions, Verbs, Admin UI, Networking
- Classification: Missing -> Adapted
- Risk: Medium before the fix; low after it.
- Behavior/API delta: `RMCAdminVerb` and `SharedRMCAdminSystem` still defined the debug-admin action and subscribed to `GetVerbsEvent<RMCAdminVerb>`, but the type was absent from `Verb.VerbTypes` and `SharedVerbSystem` never collected it. The verb could therefore neither participate in the network type mapping nor be requested for the context menu, making the retained RMC actions UI unreachable through its intended entry point.
- RMC/CMU divergence: Current SS14 access, interaction, hand, and context-menu collection values are passed unchanged. The RMC system still performs its own `AdminFlags.Debug` authorization before adding the action; no privilege is inferred from the generic verb access result.
- Decision and rationale: Restore the type registration and one current-style collection branch. This reconnects the existing authorized consumer without altering generic verb execution or introducing a parallel menu path.
- Files changed: `Content.Shared/Verbs/Verb.cs`, `Content.Shared/Verbs/SharedVerbSystem.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static flow review now reaches the sole `GetVerbsEvent<RMCAdminVerb>` subscriber and retains its debug-admin check. `git diff --check` passed. Shared compilation is queued behind the disjoint storage patch; tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Runtime coverage must compare authorized and unauthorized sessions, local and server-provided verb requests, target deletion, and BUI opening on representative RMC entities.

## CS-0282 - Restore RMC rotation limits

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0281.
- Areas: Interactions, Physics, Rotation, Sentries
- Classification: Missing -> Adapted
- Risk: High before the fix for sentry firing arcs; low after it.
- Behavior/API delta: `MaxRotationComponent` and `RMCInteractionSystem.TryCapWorldRotation` remained live, and retained sentry/emplacement systems continued setting their center and deviation, but current `RotateToFaceSystem.TryRotateTo` never applied the clamp. Automated or assisted rotation could therefore turn a limited weapon beyond its intended arc before current speed/tolerance processing.
- RMC/CMU divergence: Entities without `MaxRotationComponent` retain current SS14 behavior exactly. Marked entities clamp only the requested world-space goal; current action blockers, rotation speed, tolerance, buckle rotation, and transform authority remain unchanged.
- Decision and rationale: Invoke a fork helper immediately after resolving the current transform and before calculating angular distance. The helper delegates to the retained RMC policy system, avoiding duplicated angle math or a second rotation owner.
- Files changed: `Content.Shared/Interaction/RotateToFaceSystem.cs`, `Content.Shared/_RMC14/Interaction/RotateToFaceSystem.RMC.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static call-path review confirms the cap now precedes both finite-speed and immediate rotation branches, while the retained helper is a no-op for unmarked entities. `git diff --check` passed. Shared compilation is queued behind the disjoint storage patch; tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Runtime coverage must exercise both sides of the deviation boundary, wraparound near plus/minus pi, finite-speed convergence at the cap, component removal, and replicated sentry rotation.

## CS-0283 - Restore RMC input contexts and rebind exposure

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0282.
- Areas: Interactions, Movement, Shooting, Input, Client UI
- Classification: Missing -> Adapted
- Risk: Critical before the fix because retained commands were unreachable; medium after it until default bindings and three SmartEquip destinations are closed separately.
- Behavior/API delta: `CMKeyFunctions`, their command-bind consumers, the xeno prototype `InputMover.context: xenonid`, and localization all survived, but no current input context registered the fork functions and the options UI did not expose them. Holsters, attachables, fire-mode/unique actions, dropped-item pickup, other-hand interaction, RMC rest/resist, xeno wide swing/rest, and the three retained SmartEquip destinations could not be activated or configured.
- RMC/CMU divergence: Current SS14 common/human functions and current input manager remain authoritative. The RMC human functions are added to the existing human context, while the xeno context inherits human and adds only its two overrides, matching the live xeno prototype contract. `ToggleKnockdown` is deliberately removed from the human context because RMC retains distinct rest/resist commands and its old `C` binding is reserved for other-hand interaction.
- Decision and rationale: Restore the fork function registration in one client helper, make `ContentKeyFunctions` partial so the three RMC equipment destinations live under `_RMC14`, and expose every retained function in the current rebind UI. Default key changes are isolated for an explicit collision-reviewed commit.
- Files changed: `Content.Shared/Input/ContentKeyFunctions.cs`, `Content.Shared/_RMC14/Input/ContentKeyFunctions.RMC.cs`, `Content.Client/Input/ContentContexts.cs`, `Content.Client/Options/UI/Tabs/KeyRebindTab.xaml.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static consumer review finds live command binds for the CM/RMC functions, and `Resources/Prototypes/_RMC14/Entities/Mobs/Xeno/base_xeno.yml` selects the restored `xenonid` context. Every registered function now has an existing localized rebind label. `git diff --check` passed. Client/Shared compilation is queued behind the disjoint construction/storage patches; tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Default bindings require the collision-reviewed RMC map, and `SmartEquipUniform`, `SmartEquipArmor`, and `SmartEquipHelmet` need current shared SmartEquip handlers. Runtime coverage must also verify context switching on spawn, ghosting, and xeno evolution.

## CS-0284 - Restore the collision-reviewed RMC key map

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0283.
- Areas: Interactions, Movement, Shooting, Input, Resources
- Classification: Missing -> Adapted
- Risk: High before the fix for new/default profiles; medium after it because runtime command combinations remain untested.
- Behavior/API delta: Restored input functions had no default bindings. Directly appending the RMC map to current SS14 defaults would collide with `ToggleKnockdown`, both pocket SmartEquip keys, the character menu, and the sandbox window. New players would otherwise receive unreachable core RMC actions, while a blind merge would fire competing commands in the human context.
- RMC/CMU divergence: The retained RMC layout intentionally uses distinct rest/resist commands instead of upstream `ToggleKnockdown`; reserves `C` for other-hand interaction; moves the character menu to Shift+C; moves pockets to Shift+N/Shift+M so F/G combinations remain available for holsters/attachments; and keeps sandbox on Control+B so unmodified B remains resist. Existing user-custom bindings are not overwritten by the defaults file.
- Decision and rationale: Restore the live RMC default block and the four collision-avoidance changes from `rmc/master`, while retaining unrelated current SS14 additions such as client/server component inspection. Shared-key pairs that are intentionally context- or state-dependent—xeno wide swing versus unique action, xeno rest versus human rest, and storage rotation versus dropped-item pickup—remain as in RMC.
- Files changed: `Resources/keybinds.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static key/context review confirms the direct human collisions with `ToggleKnockdown`, pocket SmartEquip, character menu, and sandbox were removed before adding the RMC block. `git diff --check` passed. Resource/prototype validation and runtime input tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: `SmartEquipUniform`, `SmartEquipArmor`, and `SmartEquipHelmet` still need current shared handlers. Runtime coverage must verify held versus UI-focused contexts, xeno inherited bindings, storage rotation/pickup overlap, and preservation of existing customized profiles.

## CS-0285 - Restore CM construction authorization and RMC build routes

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0284.
- Areas: Interactions, Construction, Prototypes, Client preview, Server authority, Networking
- Classification: Missing -> Adapted
- Risk: Critical before the fix; medium after it pending runtime and prototype validation.
- Behavior/API delta: Current client menus enumerated every SS14 recipe and current server entry points used unfiltered prototype lookup, despite `CMPrototypeExtensions.FilterCM` and `IsCM` remaining live. `DisableConstruction`, vehicle-occupant denial, and `RMCConstructionAttemptEvent` no longer gated both prediction and authority. Five retained barricade recipes still declared `rmcPrototype` but were routed through generic graph construction, bypassing RMC skill, material, timing, collision, placement, and stack-consumption behavior.
- RMC/CMU divergence: Current SS14 request/acknowledgement messages, ghosts, conditions, graph state machine, predicted presentation, and server mutation remain authoritative for ordinary CM graph recipes. CM filtering is applied at menu/category, item/structure start, guide, and graph-change lookups. Fork capability and placement policy is evaluated on both client preview and authoritative server acceptance with the actual user. Only recipes carrying `RMCPrototype` take the retained material-build lifecycle.
- Decision and rationale: Add narrow client/server construction partials around the current owners. The direct RMC route selects the largest valid held material stack, correcting an old RMC early-selection bug while preserving buildable whitelists. The current menu receives an optional action label so the five direct recipes again show the localized "Build Here" action without forking the UI.
- Files changed: `Content.Client/Construction/ConstructionSystem.cs`, `Content.Client/Construction/UI/ConstructionMenu.xaml.cs`, `Content.Client/Construction/UI/ConstructionMenuPresenter.cs`, `Content.Client/_RMC14/Construction/ConstructionMenuPresenter.RMC.cs`, `Content.Client/_RMC14/Construction/ConstructionSystem.RMC.cs`, `Content.Server/Construction/ConstructionSystem.Graph.cs`, `Content.Server/Construction/ConstructionSystem.Guided.cs`, `Content.Server/Construction/ConstructionSystem.Initial.cs`, `Content.Server/_RMC14/Construction/ConstructionSystem.Initial.RMC.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Full `Content.Server` build succeeded with 0 errors and 10 unrelated warnings. Final `Content.Client --no-dependencies` build succeeded with 0 errors and 8 unrelated warnings, and an earlier full Client dependency build also succeeded with 0 errors. Owned paths pass `git diff --check`. No tests or prototype validation were run at 853/1,000 commits.
- Remaining debt: Runtime/prototype coverage must exercise CM versus non-CM visibility and rejection, xeno and vehicle denial, actor-aware placement cancellation, all five barricade recipes, stack selection/consumption, cancellation, acknowledgements, and graph changes. `ConstructionPrototype.IconColor` has 43 live magazine-box declarations but no current client consumer and is a separate low-to-medium `Missing` presentation finding. Old `Content.IntegrationTests/PoolManager.cs` disabled `FilterCM` for vanilla tests; the rewritten pool lacks that exception, so test infrastructure must be adapted when tests resume.

## CS-0286 - Enable generated buckle-state lifecycle callbacks

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0285.
- Areas: Interactions, Buckle, Client presentation, Networking, Lifecycle
- Classification: Missing -> Adapted; follow-up correction to CS-0280.
- Risk: High before the fix because the client project failed analyzer validation and the intended remote-state refresh could not be compiled; low after it.
- Behavior/API delta: CS-0280 restored `AfterAutoHandleStateEvent` consumers for `BuckleComponent` and `StrapComponent`, but the current generated-state contract did not request those callbacks. The analyzer correctly rejected both subscriptions. Old RMC explicitly enabled the callback on `BuckleComponent`; its older tooling did not diagnose the equivalent missing declaration on `StrapComponent`, even though the visualizer subscribed to both.
- RMC/CMU divergence: Both current generated component states now opt into their post-apply callback. This adds no new network field or mutation path: it only notifies the existing client visualizer after authoritative/predicted state is applied, while local buckle/strap domain events remain the immediate path.
- Decision and rationale: Enable `raiseAfterAutoHandleState` at the two state owners rather than weaken analyzer validation or replace state reconciliation with frame polling. `StrapComponent` also requires the callback because its replicated `BuckledEntities` collection directly determines fork strap draw depth.
- Files changed: `Content.Shared/Buckle/Components/BuckleComponent.cs`, `Content.Shared/Buckle/Components/StrapComponent.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: The first full Client build failed only with `RA0041` on the two CS-0280 subscriptions. After enabling the generated callbacks, `dotnet build Content.Client/Content.Client.csproj --configuration DebugOpt --no-restore --nologo --verbosity:minimal --disable-build-servers` succeeded with 0 errors and 14 unrelated warnings. Tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Runtime coverage must confirm event ordering and draw-depth reconciliation for local prediction and remote observation across buckle, unbuckle, strap rotation, vehicle seats, and explicit RMC buckle/strap depth components.

## CS-0287 - Restore RMC storage contracts on current authority paths

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0286.
- Areas: Interactions, Storage, Inventory, Client UI, Prediction, Server authority, Timing, Prototypes
- Classification: Missing -> Adapted.
- Risk: Critical before the fix because live capacity and authorization contracts were bypassed; medium after it pending prototype/runtime coverage.
- Behavior/API delta: Current generic storage had retained SS14 lock, whitelist, grid, predicted-message, and server-validation paths, but it no longer called the surviving RMC policy or shape helpers. Twenty-six direct `LimitedStorage`, three `StorageStoreSkillRequired`, 48 `IgnoreContentsSize`, and 112 `FixedItemSizeStorage` declarations were therefore behaviorally disconnected. Fixed slots did not control authoritative capacity, occupancy, fitting, removal, aggregate area, or client previews. Actor-aware store skills were not checked before stack merging. `quickInsertCooldown` was no longer serialized; adding named storage `UseDelay` entries could also inherit the component's unrelated one-second default delay. `StorageOpenDoAfterComponent`, the nested analyzer keep-open marker, and two-sided bulk-transfer policy all lacked their current owner seams.
- RMC/CMU divergence: Current SS14 storage state messages, component/container ownership, deterministic network-entity transfer ordering, stack implementation, grid rotation, and authoritative mutation remain in place. Fork policy is a validation extension on those paths, not a parallel storage system. Actorless prototype/fill operations retain actorless skill behavior; player operations pass their real actor. Fixed-size storage retains current rotation math and all live direct declarations use the historical square default. Delayed-open completion and starting-gear auto-open use an explicit bypass to avoid recursive DoAfters, while duplicate user open attempts remain consumed and cannot fall through to an immediate open.
- Decision and rationale: Add one actor-aware `CanInsert` overload and validate it at the authoritative `Insert` boundary before stack/container mutation; keep the actorless overload for existing non-player callers. Thread the storage entity through the existing item-shape calculations and client previews so one effective geometry feeds every capacity decision. Reconnect open timing and nested access at the current UI owners. Zero only the unnamed `UseDelay` created by storage itself, preserving pre-existing component policy and named quick/open delays.
- Files changed: `Content.Shared/Storage/EntitySystems/SharedStorageSystem.cs`, `Content.Shared/Storage/StorageComponent.cs`, `Content.Shared/_RMC14/Storage/RMCStorageSystem.cs`, `Content.Shared/_RMC14/Storage/SharedItemSystem.RMC.cs`, `Content.Shared/Interaction/SharedInteractionSystem.cs`, `Content.Client/UserInterface/Systems/Storage/Controls/ItemGridPiece.cs`, `Content.Client/UserInterface/Systems/Storage/Controls/StorageWindow.cs`, `Content.Client/UserInterface/Systems/Storage/StorageUIController.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static review follows player, area-insert, stack-merge, fixed-grid, delayed-open, starting-gear, nested-UI, and bulk-transfer paths through the current state owners. `dotnet build Content.Shared/Content.Shared.csproj --configuration DebugOpt --no-restore --nologo --verbosity:minimal --disable-build-servers` succeeded with 0 errors and 6 unrelated warnings. After CS-0286 corrected the disjoint buckle analyzer issue revealed by the first attempt, the equivalent full Client build succeeded with 0 errors and 14 unrelated warnings; the full Server build succeeded with 0 errors and 10 unrelated warnings. No test suite or prototype validation was run at 853/1,000 commits.
- Remaining debt: `CMStorageItemFillEvent` still lacks producer seams, so fill-time expansion for fixed grids is `Missing`. `SmartEquipSystem` and `SharedCMInventorySystem` still use actorless preflight calls and can disagree with final authorization. RMC hand/storage-ejection integrations and fixed-storage border/icon presentation remain separate findings. Runtime coverage must exercise every live policy class, partial/full stack merges, prediction rejection, rotation, cross-storage drag, delayed opening, starting gear, analyzer nesting, and transfers in both directions.

## CS-0288 - Restore RMC storage-fill lifecycle producers

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0287.
- Areas: Interactions, Storage, Prototype spawning, Starting gear, Lifecycle
- Classification: Missing -> Adapted.
- Risk: High before the fix because fixed-grid contents and PDT-kit linking could fail during spawn; low-to-medium after it pending prototype/runtime validation.
- Behavior/API delta: `RMCStorageSystem` still consumed `CMStorageItemFillEvent` to expand a too-small grid before insertion, and `PDTSystem` consumed it to link a kit's locator and bracelet, but all current producers had been lost. Legacy `StorageFill`, `EntityTableContainerFill`, and starting-gear storage therefore spawned contents without invoking either policy. Old RMC's legacy `StorageFill` path raised the same event twice per item after a merge workaround.
- RMC/CMU divergence: Current spawn selection, entity-table sorting, container insertion, starting-gear equipment, and storage authority remain unchanged. Each real item now receives one event after spawn and immediately before its insertion. This is sufficient for both retained consumers, avoids duplicate notifications, and lets grid expansion observe already inserted siblings. Arbitrary non-item container contents and direct `ContainerFill` paths without a historical fixed-storage contract remain untouched.
- Decision and rationale: Add fork-owned partial helpers and one call at each current fill owner rather than recreate a parallel filler or preserve the redundant legacy double raise. The event is still raised on the destination storage entity so both `StorageComponent` and owner-specific subscribers receive it.
- Files changed: `Content.Server/Storage/EntitySystems/StorageSystem.Fill.cs`, `Content.Server/_RMC14/Storage/StorageSystem.Fill.RMC.cs`, `Content.Shared/Containers/ContainerFillSystem.cs`, `Content.Shared/_RMC14/Storage/ContainerFillSystem.RMC.cs`, `Content.Shared/Station/SharedStationSpawningSystem.cs`, `Content.Shared/_RMC14/Storage/SharedStationSpawningSystem.RMC.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static producer/consumer review confirms the three pre-insert seams now reach both retained consumers and run once per item. `dotnet build Content.Shared/Content.Shared.csproj --configuration DebugOpt --no-restore --nologo --verbosity:minimal --disable-build-servers` succeeded with 0 errors and 6 unrelated warnings. The equivalent full Server build succeeded with 0 errors and 4 unrelated warnings. No tests or prototype validation were run at 853/1,000 commits.
- Remaining debt: Runtime/prototype coverage must spawn legacy `StorageFill`, the fixed-size donut entity-table storage, starting-gear fixed storages, and PDT kits; verify expansion is minimal enough, insertion succeeds, links are correct, and failed/non-item spawns remain safe. The grid-expansion algorithm's bounded three retries and horizontal growth policy are retained behavior, not proven optimal packing.

## CS-0289 - Restore RMC SmartEquip policy on the shared predicted path

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0288.
- Areas: Interactions, Inventory, Storage, Input, Prediction, Detectors
- Classification: Missing -> Adapted.
- Risk: High before the fix because live RMC bindings were unreachable and storage authorization could disagree between preview and mutation; low-to-medium after it pending prediction/runtime coverage.
- Behavior/API delta: The current shared SmartEquip owner bound only backpack, belt, pockets, and suit storage despite restored uniform, armor, and helmet key functions. It no longer shut down active motion/intel detectors when stowed. Storage shortcuts did not validate the equipped container's action-blocker/interaction policy and used actorless `CanInsert`, bypassing RMC store-skill checks during preflight. The RMC holster selector had the same actorless preflight mismatch at both candidate selection and final insertion.
- RMC/CMU divergence: Current SS14 shared command binding and predicted equip/unequip/container mutation remain authoritative. The obsolete RMC private `SmartEquipEvent` networking schema is not restored. The three fork keys map to the retained `jumpsuit`, `outerClothing`, and `head` slot contracts. Detector toggles run after the same equip attempt as old RMC, while ordinary items retain current behavior. A fork-owned storage wrapper exposes the current private lock/range/attempt-event owner without widening or duplicating its implementation.
- Decision and rationale: Extend the existing command builder and keep fork handlers/policy in partials. Pass the actual user through every affected storage preflight so prediction and the CS-0287 authoritative insertion boundary evaluate the same skill policy. Reuse detector systems' current networked toggle methods instead of directly mutating their fields.
- Files changed: `Content.Shared/Interaction/SmartEquipSystem.cs`, `Content.Shared/_RMC14/Interaction/SmartEquipSystem.RMC.cs`, `Content.Shared/_RMC14/Storage/SharedStorageSystem.RMC.cs`, `Content.Shared/_RMC14/Inventory/SharedCMInventorySystem.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static flow review confirms all eight current/fork SmartEquip functions share one handler, both storage preflights carry `uid`, the authoritative `Insert` also receives `uid`, and both holster checks carry `user`. The first Shared build identified the upstream-private `CanInteract` seam; the fork wrapper was then added on the same partial owner. The final `dotnet build Content.Shared/Content.Shared.csproj --configuration DebugOpt --no-restore --nologo --verbosity:minimal --disable-build-servers` succeeded with 0 errors and 6 unrelated warnings. Tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: The retained `TryEquipClothing(..., doRangeCheck)` option no longer maps to a current inventory API. Historical RMC's implementation did not actually bypass item accessibility for the self-equip vendor path, so restoring a broad range bypass without a proven failure is `Deferred`. Runtime coverage must exercise every shortcut on client/server prediction, locked/skill-gated storage, partial stacks, equip DoAfters, and enabled/disabled detector states.

## CS-0290 - Restore RMC hand/storage-ejection integration

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0289.
- Areas: Interactions, Hands, Inventory, Storage, Prediction, Lifecycle
- Classification: Missing -> Adapted.
- Risk: High before the fix across common pouches, belts, webbing, canisters, pill bottles, boxes, and weapon storage; low-to-medium after it pending prediction/runtime coverage.
- Behavior/API delta: `RMCHandsSystem.TryStorageEjectHand` and 30 direct `RMCStorageEjectHand` prototype declarations remained live, but the three owners that historically consulted it no longer did. Moving a non-active hand item moved the outer container; clicking equipped storage with an empty hand unequipped it; clicking a marked item inside storage picked up that item. Intended first/last/whitelist ejection, open-in-place, activate-on-click, nested policy, skill denial, and owner-specific `RMCStorageEjectHandItemEvent` behavior were unreachable from those paths.
- RMC/CMU divergence: Current validated hand, inventory-slot, and storage UI messages remain the entry points and current hands/container systems remain the mutation owners. The fork helper is queried only before each owner's outer-container fallback. Returning false preserves current move/unequip/pickup behavior; returning true consumes the request after the helper opens, activates, ejects, or reports an intentional empty/denied result.
- Decision and rationale: Add fork-owned partial dependencies/helpers to `SharedHandsSystem`, `InventorySystem`, and `SharedStorageSystem`, then call them at the historical pre-fallback points. This reconnects all retained state modes without duplicating ejection logic or restoring old input/network plumbing.
- Files changed: `Content.Shared/Hands/EntitySystems/SharedHandsSystem.Interactions.cs`, `Content.Shared/_RMC14/Hands/SharedHandsSystem.Interactions.RMC.cs`, `Content.Shared/Inventory/InventorySystem.Equip.cs`, `Content.Shared/_RMC14/Hands/InventorySystem.Equip.RMC.cs`, `Content.Shared/Storage/EntitySystems/SharedStorageSystem.cs`, `Content.Shared/_RMC14/Storage/SharedStorageSystem.RMC.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static lifecycle review confirms each helper runs only after the current message/session or storage-input validation and before outer-container mutation. The existing fork helper remains the single owner of state, whitelist, nested, skill, popup, and pickup selection. `dotnet build Content.Shared/Content.Shared.csproj --configuration DebugOpt --no-restore --nologo --verbosity:minimal --disable-build-servers` succeeded with 0 errors and 6 unrelated warnings. Tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Runtime coverage must exercise all four ejection states, activate-on-click, event-handled cards/cassettes, nested whitelists, empty policy, skill denial, full hands, inventory clicks, storage UI clicks, hand-to-hand moves, and client/server resimulation. Current generic pickup/unequip remains the intentional fallback for unmarked or `Unequip` entities.

## CS-0291 - Restore the RMC combat-mode interaction override

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0290.
- Areas: Interactions, Combat mode, Xeno devour, Prediction
- Classification: Missing -> Adapted.
- Risk: High before the fix for devour/breakout controls; low after it pending runtime prediction coverage.
- Behavior/API delta: `RMCCombatModeInteractOverrideUserEvent` and both `XenoDevourSystem` consumers survived, but current `CombatModeCanHandInteract` never raised the event. Devoured users could be forced down normal attack selection instead of the intended hand-interaction/breakout path, and a devouring xeno interacting with itself while pulling a devourable target could not request its retained override.
- RMC/CMU divergence: Current SS14 empty-hand/item checks and `CombatModeShouldHandInteractEvent` remain the default policy. The fork event is raised first on the user and only replaces the result when a consumer explicitly sets `Handled`; unhandled users retain current behavior exactly.
- Decision and rationale: Put event construction/dispatch in a fork-owned `SharedInteractionSystem` partial and add one early handled-result branch at the current decision owner. This restores the old extension seam without duplicating combat-mode input or attack logic.
- Files changed: `Content.Shared/Interaction/SharedInteractionSystem.cs`, `Content.Shared/_RMC14/Interaction/SharedInteractionSystem.RMC.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Exact producer/consumer review confirms the restored call is the sole producer and both live consumers are component-targeted on the user. The initial fast compile caught and corrected the partial's abstract/sealed declaration mismatch. The final `dotnet build Content.Shared/Content.Shared.csproj --configuration DebugOpt --no-restore --no-dependencies --nologo --verbosity:minimal --disable-build-servers` succeeded with 0 errors and 6 unrelated warnings. Tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Runtime coverage must compare ordinary combat-mode pickup/attack behavior, a devoured user with and without usable weapons, xeno self-interaction while pulling devourable/non-devourable targets, and client/server resimulation.

## CS-0292 - Restore marked ghost interaction exceptions

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0291.
- Areas: Interactions, Ghosts, Vehicles, Ladders, Access, Range, Prediction
- Classification: Missing -> Adapted.
- Risk: High before the fix for observer/admin vehicle and interior workflows; low-to-medium after it pending runtime authorization coverage.
- Behavior/API delta: `RMCIgnoreGhostInteractionLimitsComponent` remained networked on 30 direct vehicle, interior, viewport, ladder, and blocker declarations, but ghost action blocking and both current interaction/activation entry points ignored it. Non-interactive ghosts were cancelled before the target policy, and marked remote/interior targets still failed ordinary accessibility and range checks.
- RMC/CMU divergence: Current SS14 ghost restrictions remain authoritative for every unmarked target. A real `GhostComponent` user targeting the fork marker bypasses only can-interact, accessibility, and range gates at the historical interaction/activation owners; deletion, input/session validation, use delay, actual target event handling, and contact/logging remain current. Non-ghost users do not gain any bypass.
- Decision and rationale: Centralize the user/target predicate in a fork-owned `SharedInteractionSystem` partial, restore the target exception at `SharedGhostSystem`'s `InteractionAttemptEvent`, and condition the existing gates rather than adding a parallel interaction path. This retains current upstream access improvements for all ordinary interactions.
- Files changed: `Content.Shared/Ghost/SharedGhostSystem.cs`, `Content.Shared/_RMC14/Ghost/SharedGhostSystem.RMC.cs`, `Content.Shared/Interaction/SharedInteractionSystem.cs`, `Content.Shared/_RMC14/Interaction/SharedInteractionSystem.RMC.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static flow review covers empty-hand/used-item `UserInteraction`, direct `InteractionActivate`, and the ghost action-blocker event; each requires both ghost user and marked target before bypassing. `dotnet build Content.Shared/Content.Shared.csproj --configuration DebugOpt --no-restore --no-dependencies --nologo --verbosity:minimal --disable-build-servers` succeeded with 0 errors and 6 unrelated warnings. Tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Runtime coverage must use ordinary ghosts and admin ghosts against every marked target family, confirm unmarked controls remain blocked, exercise relayed vehicle input and BUI opening, and verify client/server prediction cannot use the marker to interact through unrelated entities.

## CS-0293 - Restore interaction-transparent client click filtering

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0292.
- Areas: Interactions, Client targeting, Vehicles, Sprite fade, Presentation
- Classification: Missing -> Adapted.
- Risk: High before the fix for vehicle/interior/tent usability; low after it pending viewport/runtime coverage.
- Behavior/API delta: `InteractionTransparencyComponent` and `RMCClientInteractionSystem.IsInteractionTransparency` survived, but current `GameplayStateBase.GetClickableEntities` added every clickable sprite without consulting them. A marked enclosing overlay whose bounds contain the local player could therefore sort above and capture gameplay clicks intended for interior entities. RMC sprite-fade probing needs to see those same sprites even when gameplay selection excludes them.
- RMC/CMU divergence: Current SS14 sprite-tree query, `ClickableSystem.CheckClick`, draw-depth/render-order/y sorting, faded-sprite option, and null-component lookup remain authoritative. The fork filter runs before the current click test only on the normal path. A named bypass remains available and is used solely by `RMCSpriteFadeSystem` so fade discovery can inspect transparent overlays without making them gameplay targets.
- Decision and rationale: Add the optional bypass to the current enumerator, put the target predicate in a fork-owned `GameplayStateBase` partial, and update the one historical bypass caller. This retains upstream's improved clickable-component resolution and avoids duplicating enumeration or sorting.
- Files changed: `Content.Client/Gameplay/GameplayStateBase.cs`, `Content.Client/_RMC14/Interaction/GameplayStateBase.RMC.cs`, `Content.Client/_RMC14/Sprite/RMCSpriteFadeSystem.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Repository-wide call review finds ordinary gameplay, drag/drop, and standard fade callers retain the default filter; only the fork fade probe passes `ignoreInteractionTransparency: true`. `dotnet build Content.Client/Content.Client.csproj --configuration DebugOpt --no-restore --no-dependencies --nologo --verbosity:minimal --disable-build-servers` succeeded with 0 errors and 8 unrelated warnings. Tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Runtime coverage must click through each vehicle/interior/tent overlay from inside and outside, compare different eye rotations and viewed grids, verify overlapping transparent sprites sort correctly, and confirm both standard and RMC fade reactions still work.

## CS-0294 - Restore RMC DoAfter rest cancellation

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0293.
- Areas: Interactions, DoAfter, Xeno rest, Prediction, Timing
- Classification: Missing -> Adapted.
- Risk: High before the fix for xeno abilities/construction and other retained actions; low after it pending resimulation coverage.
- Behavior/API delta: `DoAfterArgs.BreakOnRest` remained serialized/copied with a default of true, explicit exceptions remained in xeno evolution/parasite/egg paths, and `RMCDoAfterSystem.ShouldCancel` still detected `XenoRestingComponent`. Current `SharedDoAfterSystem.Update` never consulted that policy, so resting during an action did not cancel it and the explicit false overrides had no behavioral purpose.
- RMC/CMU divergence: All current SS14 cancellation checks—entity lifetime, movement, target/tool distance, hand state, damage and attempt-event policy—run first and remain authoritative. The fork cancellation is an additional post-check on active DoAfters; it uses current `InternalCancel` cleanup, dirtiness, prediction, and retention timing rather than maintaining separate state.
- Decision and rationale: Inject the retained policy through a fork-owned `SharedDoAfterSystem` partial and add one cancellation branch after current `ShouldCancel`. This preserves current exception tolerance/update structure and gives the existing field contract one owner.
- Files changed: `Content.Shared/DoAfter/SharedDoAfterSystem.Update.cs`, `Content.Shared/_RMC14/DoAfter/SharedDoAfterSystem.RMC.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static review confirms every current `BreakOnRest` assignment reaches the restored check and that cancellation uses the same completion/cancel lifecycle as upstream. `dotnet build Content.Shared/Content.Shared.csproj --configuration DebugOpt --no-restore --no-dependencies --nologo --verbosity:minimal --disable-build-servers` succeeded with 0 errors and 6 unrelated warnings. Tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Runtime coverage must start representative default/true/false DoAfters, enter and leave xeno rest on client/server, verify cancellation fires once, and cover repeat/duplicate/predicted actions. `TargetEffect` timing and `ForceVisible` overlay policy remain separate fixes.

## CS-0295 - Restore server-authoritative DoAfter target effects

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0294.
- Areas: Interactions, DoAfter, Medical, Xeno abilities, Networking, Timing
- Classification: Missing -> Adapted.
- Risk: High before the fix because retained healing/plasma telegraphs never spawned; low-to-medium after it pending timing/runtime coverage.
- Behavior/API delta: `DoAfterArgs.TargetEffect` remained serialized/copied and was still assigned by CPR, IV, wounds, surgery, xeno plasma/fruit/recovery/plasma-tree and related actions, but current DoAfter update never consumed it. The old per-DoAfter cadence field had also been lost, leaving no way to bound a restored periodic effect.
- RMC/CMU divergence: Current SS14 DoAfter ownership, prediction, exception handling, cancellation, completion, repetition and component state remain intact. Active, non-completed DoAfters with a target effect advance a one-second cadence on both sides, but only the server spawns the effect entity. The timing field is serialized and copied with the DoAfter so clone/state paths do not reset the cadence.
- Decision and rationale: Add the timing field in a fork partial, copy it at the current copy constructor, and call a fork-owned updater from the existing active loop before cancellation as historical RMC did. Server-only spawning prevents duplicate predicted entities while preserving immediate and periodic telegraph behavior.
- Files changed: `Content.Shared/DoAfter/DoAfter.cs`, `Content.Shared/DoAfter/SharedDoAfterSystem.Update.cs`, `Content.Shared/_RMC14/DoAfter/DoAfter.RMC.cs`, `Content.Shared/_RMC14/DoAfter/SharedDoAfterSystem.RMC.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static assignment review confirms every retained `TargetEffect` writer now reaches the single updater, missing/deleted targets do not spawn, and `INetManager.IsServer` guards entity creation. `dotnet build Content.Shared/Content.Shared.csproj --configuration DebugOpt --no-restore --no-dependencies --nologo --verbosity:minimal --disable-build-servers` succeeded with 0 errors and 6 unrelated warnings. Tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Runtime coverage must verify immediate and one-second cadence, server-only entity counts, target deletion, cancellation on the first tick, repeated DoAfters, state copy/resimulation, attachment coordinates, and all medical/xeno effect prototypes. `ForceVisible` overlay visibility remains separate.

## CS-0297 - Reconcile RMC DoAfter visibility with current presentation

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0296.
- Areas: Interactions, DoAfter, Client presentation, Night vision, Stealth
- Classification: Missing -> Adapted.
- Risk: High before the fix because hidden actions and cloaked actors leaked progress bars; low-to-medium after it pending runtime presentation coverage.
- Behavior/API delta: Current SS14 retained improved fade-in/fade-out and sprite-bounds positioning, but the bulk merge removed RMC's sprite visibility, xeno line-of-sight, active-invisibility opacity, night-vision render-space, and `ForceVisible` policies. Progress bars could reveal invisible actors or occluded non-xeno actions to xenos, while bars rendered below the field-of-view mask even when night vision was active.
- RMC/CMU divergence: Current alpha animation, hidden/container handling, stacked offsets, color policy, and dynamic vertical placement remain authoritative. Fork visibility now filters only after current hidden/container policy. `ForceVisible` bypasses sprite, xeno-occlusion, and stealth opacity checks as in RMC, but does not expose an upstream-hidden or contained action to other players. Alpha caps compose with the current local hidden-action hint instead of overwriting it.
- Decision and rationale: Keep the current overlay owner and add a narrow `_RMC14` partial for fork queries and render-space selection. This retains upstream presentation improvements while restoring information-hiding contracts without a duplicate overlay or frame-update system.
- Files changed: `Content.Client/DoAfter/DoAfterOverlay.cs`, `Content.Client/DoAfter/DoAfterSystem.cs`, `Content.Client/_RMC14/DoAfter/DoAfterOverlay.RMC.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static flow review confirms visibility policy is evaluated per active DoAfter after hidden/container ownership, `NightVisionOverlay` selects world-space rendering, and invisibility opacity only reduces the current maximum alpha. `dotnet build Content.Client/Content.Client.csproj --no-dependencies` succeeded with 0 errors and 8 unrelated warnings after the isolated CS-0296 stale-import cleanup. Tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Runtime coverage must compare local and remote actions with hidden/container flags, visible/hidden sprites, marine/xeno occlusion, active cloak opacity, `ForceVisible`, night-vision toggling, cancellation fades, and multiple stacked DoAfters.

## CS-0298 - Restore opt-in RMC spray self-collision

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0297.
- Areas: Interactions, Physics, Fluids, Flamers, Throwing, Server authority
- Classification: Missing -> Adapted.
- Risk: High before the fix because back-spray could not hit its user; low after it pending collision/runtime coverage.
- Behavior/API delta: `RMCSprayAmmoProviderComponent.HitUser` remained live and was passed through the fork spray facade, while current `SpraySystem` discarded it and current `ThrownItemSystem` always cancelled contact with the thrower. RMC flamer vapor therefore received a blanket self-collision exemption even when its prototype policy required self-hits.
- RMC/CMU divergence: Current SS14 spray solution splitting, vapor trajectories, pushback, timing, audio, and ordinary thrown-item thrower immunity remain authoritative. Only vapor spawned from an RMC spray provider whose `HitUser` value is true receives the retained `ThrownHitUserComponent`; only that marker bypasses the generic thrower collision cancellation.
- Decision and rationale: Adapt at the current vapor-spawn owner and the current thrower-collision owner with two narrow partial helpers. Reading the live provider field avoids copying the old spray routine or maintaining transient global state around the current API.
- Files changed: `Content.Server/Fluids/EntitySystems/SpraySystem.cs`, `Content.Server/_RMC14/Fluids/SpraySystem.RMC.cs`, `Content.Shared/Throwing/ThrownItemSystem.cs`, `Content.Shared/_RMC14/Throwing/ThrownItemSystem.RMC.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static call review confirms the sole RMC flamer spray producer carries `RMCSprayAmmoProviderComponent` into the current server spawn loop, and unmarked thrown entities retain their existing thrower exemption. A serial `Content.Shared` DebugOpt build succeeded with 0 errors and 6 unrelated warnings; `Content.Server` DebugOpt `--no-dependencies` succeeded with 0 errors and 4 unrelated warnings. Tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Runtime coverage must compare `HitUser` true/false, close-range and reverse-direction spray, multiple vapor puffs, ordinary spray bottles, ordinary thrown items, server collision ownership, and fire/acid/cloak vapor-hit consumers.

## CS-0299 - Preserve the active hand for RMC other-hand interaction

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0298.
- Areas: Interactions, Hands, Client input, Prediction
- Classification: Behavior changed -> Adapted.
- Risk: Medium before the fix because an interaction command could silently change equipment state; low after it pending input/runtime coverage.
- Behavior/API delta: The restored RMC other-hand binding called current `UIHandClick`, whose empty-other-hand branch switches the active hand. The original command deliberately disabled that branch: it interacts with or moves an item from the other hand but does nothing when that hand is empty.
- RMC/CMU divergence: Normal hand UI clicks retain current SS14 behavior and switch to an empty selected hand. The optional non-switching policy is used only by `RMCInteractWithOtherHand`; all request messages and shared server validation remain current and predicted.
- Decision and rationale: Restore the old optional `switchHand` argument with a true default and pass false at the sole fork caller. This is the smallest compatible seam and does not introduce another input or hand-mutation owner.
- Files changed: `Content.Client/Hands/Systems/HandsSystem.cs`, `Content.Client/_RMC14/Hands/ClientRMCHandsSystem.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static branch review confirms only the empty, non-active-hand branch is suppressed for the fork command; held-item use and move branches remain identical. `dotnet build Content.Client/Content.Client.csproj --configuration DebugOpt --no-restore --no-dependencies --nologo --verbosity:minimal --disable-build-servers` succeeded with 0 errors and 8 unrelated warnings. Tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Runtime coverage must compare empty/occupied active and other hands, two-handed and multi-hand mobs, key repeat, prediction resimulation, ordinary GUI hand clicks, and current default/rebound input contexts.

## CS-0300 - Reconcile RMC mob-collision mass and displacement policy

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0299.
- Areas: Interactions, Physics, Movement, Xenos, CVars, Prediction
- Classification: Missing -> Adapted.
- Risk: High before the fix because large xenos used ordinary fixture mass and displaced smaller xenos incorrectly; medium after it pending client/server collision coverage.
- Behavior/API delta: `RMCMobCollisionMassComponent` remained networked on the xeno base and both RMC collision CVars remained registered, but current `SharedMobCollisionSystem` used only fixture mass, a hard-coded 0.7 penetration basis, and always applied the full contact displacement. The retained mass override, 0.8 default overlap basis, and big-xeno/smaller-xeno directional cancellation were inert.
- RMC/CMU divergence: Current SS14 buffered collision state, velocity-product filtering, attempt events, movement cap, minimum speed modifier, contact enumeration, impulse ownership, and network message remain authoritative. Fork helpers substitute mass only for marked collision targets, source the overlap basis from the replicated RMC CVar, and subtract only the smaller-xeno contact contribution from a big xeno's own displacement. Ordinary mobs retain fixture-mass behavior.
- Decision and rationale: Insert three fork-owned policy queries into the current collision calculation rather than replay the old full system. Direct `RMCSizeComponent` queries preserve the old size comparison without adding another lifecycle owner or altering current physics state.
- Files changed: `Content.Shared/Movement/Systems/SharedMobCollisionSystem.cs`, `Content.Shared/_RMC14/Movement/SharedMobCollisionSystem.RMC.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static flow review confirms the mass override affects the same ratio operand as old RMC, the RMC penetration CVar replaces only the old geometry constant, and cancellation is limited to big-xeno mover versus smaller-xeno target. `dotnet build Content.Shared/Content.Shared.csproj --configuration DebugOpt --no-restore --no-dependencies --nologo --verbosity:minimal --disable-build-servers` succeeded with 0 errors and 6 unrelated warnings after one missing namespace import was corrected. Tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Runtime coverage must compare human/human, human/xeno, small/big xeno in both movement directions, zero/large mass caps, both RMC CVar values, overlapping centers, stationary contacts, prediction reconciliation, and fixture-mass changes.

## CS-0301 - Restore RMC construction icon tint contracts

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0300.
- Areas: Interactions, Construction, Client UI, Prototypes
- Classification: Missing -> Adapted.
- Risk: Low-to-medium before the fix because multiple magazine-box recipes became visually indistinguishable; low after it.
- Behavior/API delta: `ConstructionPrototype.IconColor` and 43 retained RMC YAML assignments survived, but the current list, grid, and selected-recipe views rendered every target with the prototype's untinted sprite. Recipe variants that share art but rely on color modulation lost their authored visual identity.
- RMC/CMU divergence: Current SS14 recipe filtering, sorting, history, favorites, entity prototype views, and build action remain unchanged. The retained fork field only modulates the three existing preview controls and defaults to white, making non-RMC recipes behaviorally identical.
- Decision and rationale: Thread the existing color through the current passive view contract and set `EntityPrototypeView.Modulate` at each render owner. This restores a pure presentation contract without changing recipe authority or duplicating the menu.
- Files changed: `Content.Client/Construction/UI/ConstructionMenu.xaml.cs`, `Content.Client/Construction/UI/ConstructionMenuPresenter.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static reference review accounts for all three construction preview surfaces and confirms white remains the default. `dotnet build Content.Client/Content.Client.csproj --configuration DebugOpt --no-restore --no-dependencies --nologo --verbosity:minimal --disable-build-servers` succeeded with 0 errors and 8 unrelated warnings. Tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Runtime/prototype coverage must open list and grid modes, select tinted and untinted recipes, switch categories/search/history, and confirm all 43 magazine-box declarations render their intended colors.

## CS-0302 - Interactions and Physics subsystem closeout

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0301.
- Scope completed: systems, components, events, prototype fields, CVars, networking/prediction, authority, timing, lifecycle hooks, and deferred tests across interaction/access, hands, inventory/storage, construction, DoAfter, pulling/fireman carry, buckling, anchoring, throwing/spray, step triggers, mob collision, grid vehicles, verbs, ghost targeting, and client click selection.
- Classification: subsystem audit completed; behavioral parity is not claimed without runtime/prototype tests.
- `Aligned`: current two-sided accessibility overrides, equipment-aware verb access, free-hand accounting, DoAfter root-entity collision policy, pull start/stop cleanup, current throw authority and before/impulse events, standard construction request/acknowledgement, and standard storage message/server validation remain connected in `SharedInteractionSystem`, `SharedVerbSystem`, `SharedHandsSystem`, `SharedDoAfterSystem`, `PullingSystem`, `ThrowingSystem`, Client/Server `ConstructionSystem`, and `SharedStorageSystem`.
- `Adapted`: CS-0268 through CS-0301 retain current SS14 owners while reconnecting RMC grid-vehicle input, step immunity, barricade anchoring, pickup/drop timing, construction duplicate policy/filtering/authorization/direct routes/tints, pull/fireman lifecycle, buckle data/authority/offsets/presentation, RMC input/SmartEquip/storage contracts, admin verbs, combat/ghost/transparency/rotation hooks, DoAfter rest/effects/visibility, spray self-hit, and collision mass/CVars.
- `Behavior changed`: CMU intentionally retains current SS14 interaction and physics timing instead of old RMC last-real-tick coordinate rewind; current hand throws retain upstream recoil/presentation rather than old RMC facing/audio/lunge/popups; current buckle/standing/fixture cleanup remains the state owner where it is safer than the old fork routines.
- `Missing`: no additional high-confidence authority or prototype-contract loss remains in the audited map. This is a bounded source audit finding, not proof that runtime-only ordering or content combinations are correct.
- `Deferred`: examination-specific presentation differences, admin-ghost inventory policy, item-aware drop blocking, fixed-storage label/border presentation, the stale `RMCAllowStrapMovementComponent`, RMC hand-throw-only presentation, interaction/physics lag-compensation redesign, and all runtime/prototype test matrices. `DoAfterArgs.LagCompensated` was already inert on live RMC apart from a retained assignment and is classified stale/deferred rather than a proven bulk-merge loss.
- Upstream improvements retained for performance/predictability: current shared predicted SmartEquip and DoAfter ownership; construction request acknowledgement and server revalidation; storage's current message and mutation owners; clickable-entity sorting; generated buckle state; current pull/throw cleanup; current mob-collision buffering, contact filtering, and capped impulses; and grid-vehicle fixed-step/local-prediction/remote-smoothing behavior. Fork fixes extend those paths rather than restoring parallel legacy systems.
- Validation evidence: targeted Shared, Client, and Server builds recorded in CS-0268 through CS-0301 completed with zero errors after each final logical change. `git diff --check` ran before every commit. Tests and prototype validation remain deliberately deferred at 853/1,000 upstream commits.
- Remaining risk: high-risk runtime matrices remain for prediction resimulation, simultaneous lifecycle events, nested containers, vehicle/interior access, collision contact ordering, and all retained YAML contracts. Compilation is evidence of API compatibility only.
- Next subsystem: Medical and Chemistry, beginning with solution-manager migration correctness because it gates 376 retained legacy solution declarations and several medical eligibility paths.

## CS-0303 - Correct legacy solution migration and current capability gates

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0302.
- Areas: Medical, Chemistry, Solutions, Hyposprays, Skills, Repairables, Lifecycle, Prototypes
- Classification: Missing -> Adapted.
- Risk: Critical before the fix because migrated solutions lost their new owner component; medium after it because the compatibility layer still fronts 376 legacy declarations.
- Behavior/API delta: The compatibility MapInit handler ensured a current `SolutionManagerComponent`, migrated contained or inline legacy solutions, and then erroneously removed that new manager rather than `SolutionContainerManagerComponent`. Named solution lookups could fail immediately after migration. Three retained callers also gated current behavior on the legacy manager: generic hypospray eligibility, skilled reagent examination, and RMC welder-fuel consumption.
- RMC/CMU divergence: The temporary legacy loader remains so existing RMC YAML and maps can initialize. After migration it now removes only the legacy component and retains the current solution manager/container. Hyposprays use current `InjectableSolutionComponent` capability, skilled examination enumerates current solution entities, and repairables resolve the named welder solution through the current API. Solution mutation and server/shared ownership remain unchanged.
- Decision and rationale: Fix the incorrect lifecycle target first, then remove direct compatibility-manager gates from the three proven call paths. This lets current capability and solution APIs operate after MapInit without attempting a risky bulk rewrite of 376 declarations in the same commit.
- Files changed: `Content.Shared/Chemistry/EntitySystems/SharedSolutionContainerSystem.Compatibility.cs`, `Content.Shared/Chemistry/EntitySystems/HypospraySystem.cs`, `Content.Shared/_RMC14/Marines/Skills/SkillsSystem.cs`, `Content.Shared/_RMC14/Repairable/RMCRepairableSystem.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static lifecycle review confirms the new manager and its solution container survive both inline and saved-entity migration branches, while the legacy component is removed after its data is consumed. Direct manager-gate search identifies the three migrated callers above. `dotnet build Content.Shared/Content.Shared.csproj --configuration DebugOpt --no-restore --no-dependencies --nologo --verbosity:minimal --disable-build-servers` succeeded with 0 errors and 6 unrelated warnings after restoring the required current capability namespace. Tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Migrate all 376 legacy YAML declarations to explicit current solution prototypes/capabilities and then delete the compatibility layer. Runtime/prototype coverage must load inline and saved solutions, inject/draw, examine reagents at skill thresholds, consume welder fuel, and verify duplicate-name migration behavior. The deferred `HyposprayIdentityPopupTest` should exercise part of this path.

## CS-0304 - Separate RMC detached-mover policy from current active-mover state

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0303.
- Areas: Movement, Input, ECS registration, Networking, Lifecycle, Prototypes, Performance
- Classification: Missing -> Adapted.
- Risk: Critical before the fix because duplicate component registration could abort prototype/component initialization; low after it pending runtime lifecycle coverage.
- Behavior/API delta: Current SS14 introduced `Content.Shared.Movement.Components.ActiveInputMoverComponent` as the mover controller's cached active-query marker. The retained RMC input namespace still registered a different component under the same serialized name and used it to remove `InputMoverComponent` while a player was detached. Both types compiled, but component-factory registration and YAML resolution were ambiguous and could fail at startup.
- RMC/CMU divergence: The upstream `ActiveInputMover` remains untouched on the current human brain/input path and retains its efficient query/cache semantics. The fork marker is renamed `RMCActiveInputMover`; only the RMC human and xeno bases use it, and `RMCInputSystem` retains its server-side MapInit plus attach/detach optimization controlled by `rmc.active_input_mover_enabled`.
- Decision and rationale: Give the two distinct lifecycle policies distinct ECS identities instead of deleting either optimization or coupling RMC detach behavior to upstream controller internals. This preserves current query performance and the fork's reduced detached-mob input state.
- Files changed: renamed `Content.Shared/_RMC14/Input/ActiveInputMoverComponent.cs` to `Content.Shared/_RMC14/Input/RMCActiveInputMoverComponent.cs`; updated `Content.Shared/_RMC14/Input/RMCInputSystem.cs`, `Resources/Prototypes/_RMC14/Entities/Mobs/Species/base.yml`, `Resources/Prototypes/_RMC14/Entities/Mobs/Xeno/base_xeno.yml`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Exact prototype search finds two `RMCActiveInputMover` declarations and one remaining upstream `ActiveInputMover` declaration on the human brain, each resolving to one registered type. The delegated targeted Shared build succeeded with 0 errors and 6 unrelated warnings; subsequent serial Shared DebugOpt builds also succeeded with 0 errors and the same 6 warnings. `git diff --check` passed. Tests/prototype loading remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Runtime coverage must spawn/attach/detach/reattach human and xeno players with the CVar on/off, replace brains, transfer minds, apply movement relays, and verify both active queries avoid duplicate or stale membership.

## CS-0305 - Migrate RMC bodies and organs to the current flat body model

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0304.
- Areas: Medical, Body, Organs, Surgery, Metabolism, Prototypes, Lifecycle
- Classification: Missing -> Adapted.
- Risk: Critical before the fix because RMC mobs had no current runtime organ graph and duplicate prototypes could abort loading; medium after it pending prototype/runtime validation.
- Behavior/API delta: RMC human, xeno, animal, rodent, and small-host bases still supplied deleted `Body.prototype`/`requiredLegs` fields. Humans and xenos had no `InitialBody`, so current organ-category surgery, organ metabolism, respiration, digestion, and body queries had nothing to operate on. The merge also retained the old 254-line human organ file alongside current Nubody definitions, duplicating ten live `OrganHuman*` IDs.
- RMC/CMU divergence: RMC humans now receive the same current twenty-category human organ graph as `AppearanceHuman`; the current human organ base retains RMC's intentional 80u edible solution with 75u GreyMatter and 5u uncooked proteins. Xenos receive a minimal head/heart graph matching their historical surgery contract, with the retained head and acidic heart converted to current `OrganComponent` categories. RMC simple mobs use current animal/rat entity-table organ fills; small hosts inherit the animal graph. Current Body/container lifecycle remains authoritative.
- Decision and rationale: Port data to current `InitialBody`/`EntityTableContainerFill` contracts and delete the obsolete duplicate definitions instead of reviving the removed body-prototype API. The old human organ file is recoverable from Git history; its intentional GreyMatter behavior was transferred before deletion.
- Files changed: deleted `Resources/Prototypes/Body/Organs/human.yml`; updated `Resources/Prototypes/Body/Species/human.yml`, `Resources/Prototypes/_RMC14/Body/Organs/xeno.yml`, `Resources/Prototypes/_RMC14/Body/Parts/xeno.yml`, `Resources/Prototypes/_RMC14/Entities/Mobs/NPCs/simplemob.yml`, `Resources/Prototypes/_RMC14/Entities/Mobs/Species/base.yml`, `Resources/Prototypes/_RMC14/Entities/Mobs/Xeno/base_xeno.yml`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static searches find no remaining `Body.prototype` or `requiredLegs` field under `_RMC14`; all previously duplicated human organ IDs now have one definition; human `InitialBody` names twenty current organ prototypes; xeno head/heart expose current Head/Heart categories; animal and rat fills use current organ prototypes. `git diff --check` passed. A C# build cannot validate resource deserialization, and prototype/test suites remain deliberately deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: The resurrected legacy `Resources/Prototypes/Entities/Mobs/Species/base.yml` still supplies extensive fork-modified parent behavior and requires a separate, broad parent-tree migration before it can be removed safely. Runtime/prototype validation must spawn every RMC human/xeno caste/simple mob, inspect organ insertion and categories, exercise surgery/respiration/digestion/metabolism, gib/remove organs, and verify GreyMatter quantities and entity-table fill ordering.

## CS-0306 - Migrate RMC blood-volume contracts

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0305.
- Areas: Medical, Chemistry, Bloodstream, Scanners, Autodoc, Prototypes
- Classification: Missing -> Adapted.
- Risk: Critical before the fix for transfusion/scanner/autodoc thresholds; low-to-medium after it pending prototype/runtime validation.
- Behavior/API delta: RMC humans, generic simple mobs, and rodents still declared removed `bloodReagent`/`bloodMaxVolume` fields. Current `BloodstreamComponent` therefore fell back to a 600u Blood reference with a 2x maximum: humans could initialize as 600/1200 rather than the intended 560/560, making percentage-based scanner and autodoc behavior disagree with RMC tuning. The 150u and 50u simple-mob contracts were likewise lost.
- RMC/CMU divergence: Each affected prototype now expresses its intended normal blood amount through current `bloodReferenceSolution` and sets `maxVolumeModifier: 1`, reproducing the old initial/max volume contract. Current blood reagent data, solution entities, DNA updates, metabolite filtering, regeneration, bleeding, and networking remain authoritative.
- Decision and rationale: Translate the three explicit legacy values directly instead of changing global current defaults, which preserves SS14 overfill behavior for unrelated entities while restoring RMC thresholds.
- Files changed: `Resources/Prototypes/_RMC14/Entities/Mobs/Species/base.yml`, `Resources/Prototypes/_RMC14/Entities/Mobs/NPCs/simplemob.yml`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static search finds no remaining `bloodReagent` or `bloodMaxVolume` fields under `_RMC14`; the translated reference quantities are exactly 560, 150, and 50 with a maximum modifier of 1. `git diff --check` passed. Resource deserialization and behavioral tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Runtime/prototype coverage must verify initial and maximum volumes, blood percentage, bleeding/regeneration, IV transfer, scanner display, autodoc completion, cloning/DNA reagent data, and simple-mob death/revival for each volume class.

## CS-0307 - Translate retained RMC reagents to current metabolism stages

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0306.
- Areas: Medical, Chemistry, Reagents, Metabolism, Prototypes, Timing
- Classification: Missing -> Adapted.
- Risk: Critical before the fix because 29 retained metabolism entries referenced six deleted group prototypes and could not enter the current organ-stage pipeline; medium after it pending prototype/runtime validation.
- Behavior/API delta: Current SS14 replaced the old `Medicine`, `Poison`, `Narcotic`, `Food`, `Drink`, and `Alcohol` organ groups with `Digestion`, `Bloodstream`, `Metabolites`, and `Respiration` stages. Twenty-nine RMC entries still used deleted IDs, leaving their nutrition, toxin, narcotic, ethanol, overdose, and custom RMC effects without a valid current stage contract.
- RMC/CMU divergence: Food and drink entries now execute during `Digestion`; poison and narcotic entries execute during `Bloodstream`; RMC ethanol executes during `Metabolites`. Its effective removal rate remains `0.01` units per tick, preserving the old `0.1` reagent rate multiplied by the RMC liver group's `0.1` rate modifier. Current staged transfers, per-organ reagent caps, predicted effect randomness, and auto-paused timing remain authoritative.
- Decision and rationale: Translate only the obsolete stage keys whose organ ownership is explicit in live RMC and preserve the one non-unit group-rate multiplier numerically. Do not revive the deleted metabolism-group prototypes or flatten current SS14's staged pipeline.
- Files changed: `Resources/Prototypes/_RMC14/Reagents/toxins.yml`, `Resources/Prototypes/_RMC14/Reagents/pyrotechnic.yml`, `Resources/Prototypes/_RMC14/Reagents/other.yml`, `Resources/Prototypes/_RMC14/Reagents/narcotics.yml`, `Resources/Prototypes/_RMC14/Reagents/medicine.yml`, `Resources/Prototypes/_RMC14/Reagents/elements.yml`, `Resources/Prototypes/_RMC14/Reagents/Consumable/ingredients.yml`, `Resources/Prototypes/_RMC14/Reagents/Consumable/Drink/packaged_drinks.yml`, `Resources/Prototypes/_RMC14/Reagents/Consumable/Drink/base.yml`, `Resources/Prototypes/_RMC14/Entities/Objects/Consumables/Food/condiments.yml`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: A scoped search finds no retained RMC reagent metabolism map using any of the six deleted group IDs; all replacements resolve to stage IDs declared by the current metabolism-stage prototypes. `git diff --check` passed. This resource-only change is not treated as runtime parity, and prototype/test suites remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Wire RMC's separate `chemicals` injectable solution into current organs and connect `RMCChemicalEffect` to the current entity-effect dispatcher. Runtime coverage must exercise oral and injected delivery, per-stage transfers, reagent caps, overdose thresholds, dead-target policy, and metabolism while paused or in stasis.

## CS-0308 - Route RMC chemicals through current organ stages

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0307.
- Areas: Medical, Chemistry, Body, Organs, Solutions, Metabolism, Lifecycle, Prototypes
- Classification: Missing -> Adapted.
- Risk: Critical before the fix because RMC injection targeted `chemicals` while inherited current hearts metabolized `bloodstream`; medium after it pending prototype/runtime validation.
- Behavior/API delta: CS-0305 supplied RMC humans with current SS14 organs, but their default stage routes did not know about RMC's intentionally separate injectable `chemicals` solution. Injected medicines accumulated without heart metabolism, digestion transferred unmatched oral reagents to `bloodstream` where the RMC route did not consume them, and respiratory transfers had the same split-stream problem.
- RMC/CMU divergence: RMC humans now use narrow derived lung, heart, and stomach prototypes. The lungs and stomach retain current local source solutions and staged processing but transfer onward to `chemicals`; the heart reads `chemicals`, retains live RMC's ten-reagent processing cap, executes `Bloodstream`, and transfers unmatched or declared metabolites into the current body `metabolites` solution for the inherited liver stage. Standard SS14 species and organ prototypes remain unchanged.
- Decision and rationale: Adapt the RMC body graph through `_RMC14` derived organ prototypes instead of changing global SS14 organ defaults or collapsing the new staged model. This retains current ingestion, respiration, deterministic/predicted stage scheduling, and metabolite cleanup while preserving the fork's injection/scanner/autodoc solution contract.
- Files changed: `Resources/Prototypes/_RMC14/Body/Organs/human.yml`, `Resources/Prototypes/_RMC14/Entities/Mobs/Species/base.yml`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static graph review confirms the RMC human `InitialBody` selects all three derived organs; every declared solution route names a solution created by the current stomach, lung, or bloodstream lifecycle; the inherited liver consumes `metabolites`; and global `OrganHuman*` prototypes are untouched. `git diff --check` passed. Prototype loading and behavioral tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: `RMCChemicalEffect` still needs explicit current metabolism context dispatch. Runtime coverage must verify injection, eating, inhalation, dialysis, vomiting, metabolite production, organ removal/reinsertion, stage caps, and cached solution cleanup.

## CS-0309 - Dispatch RMC reagent effects with current metabolism context

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0308.
- Areas: Medical, Chemistry, Entity effects, Metabolism, Prediction, Authority, Timing
- Classification: Missing -> Adapted.
- Risk: Critical before the fix because every retained `RMCChemicalEffect` declaration was inert in metabolism; medium after it because effect outcomes still require prediction/runtime matrices.
- Behavior/API delta: RMC's old metabolizer called each `EntityEffect` with `EntityEffectReagentArgs`, which supplied the target, organ, source solution, metabolized quantity, reagent prototype, and scale. The current generic entity-effect event carries only target and scale. CMU had already added `RMCChemicalEffectSystem.ApplyMetabolismEffect` to bridge that context synchronously, but the bulk merge left it with no caller; seventeen custom RMC effect types referenced by retained reagent prototypes therefore received null reagent/source context and returned without applying their primary behavior.
- RMC/CMU divergence: The current metabolizer now offers a narrow fork hook before its standard target-routing switch. Only `RMCChemicalEffect` instances use the synchronous context bridge; current lung-gas, solution-targeted, and ordinary entity effects retain upstream routing, conditions, predicted randomness, dead-target policy, scale calculation, reagent removal, and metabolite production.
- Decision and rationale: Keep the compatibility dispatch in an `_RMC14` partial and add one explicit extension call at the current effect boundary. This restores the context contract without reintroducing the deleted reagent-args hierarchy or forking the whole metabolism loop.
- Files changed: `Content.Shared/Metabolism/MetabolizerSystem.cs`, `Content.Shared/_RMC14/Chemistry/MetabolizerSystem.RMC.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static call-graph review finds one authoritative current caller of `ApplyMetabolismEffect`; all non-RMC effects fall through to the unchanged upstream switch; and the bridge restores previous context in `finally`, preserving synchronous nested-effect safety. `dotnet build Content.Shared/Content.Shared.csproj --configuration DebugOpt --no-restore --no-dependencies --nologo --verbosity:minimal --disable-build-servers` succeeded with 0 errors and 6 unrelated warnings. `git diff --check` passed. Tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Add focused effect cases for potency/scale, reagent boost, conversion, nutrition, overdose and critical overdose, random conditions, nested effects, client resimulation, dead targets, and source mutation. The mutable synchronous context must not be used by delayed/asynchronous effect implementations.

## CS-0310 - Restore RMC stasis across metabolism, blood, and respiration

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0309.
- Areas: Medical, Chemistry, Stasis, Metabolism, Bloodstream, Respiration, Timing, Lifecycle
- Classification: Missing -> Adapted.
- Risk: High before the fix because bagged patients continued consuming reagents, breathing, bleeding, regenerating blood, and taking bloodloss/suffocation effects; low-to-medium after it pending predicted lifecycle coverage.
- Behavior/API delta: Live RMC advances each subsystem timer and then raises `CMMetabolizeAttemptEvent`, allowing `CMInStasisComponent` to cancel the tick without accumulating a catch-up burst. The current SS14 metabolism, bloodstream, and respirator loops retained no call to that fork policy after the merge, even though the stasis event, component, and public body/organ checks survived.
- RMC/CMU divergence: All three current loops now call narrow `_RMC14` partial gates after advancing their auto-paused/current timers and before mutating patient state. Non-stasis entities immediately pass through. Stasis continues to suppress RMC wound bleeding and parasite incubation keeps its retained multiplier behavior; current stage scheduling, blood networking, respiration state, dead-body policy, and event relays remain authoritative outside the cancelled tick.
- Decision and rationale: Restore the proven live-RMC cancellation boundary through partial extension methods instead of approximating hard stasis with the retained but unused 1000x interval field. Advancing timers while cancelled preserves predictable resume behavior and matches live RMC rather than replaying missed ticks on removal.
- Files changed: `Content.Shared/Metabolism/MetabolizerSystem.cs`, `Content.Shared/Body/Systems/SharedBloodstreamSystem.cs`, `Content.Server/Body/Systems/RespiratorSystem.cs`, `Content.Shared/_RMC14/Medical/Stasis/MetabolizerSystem.RMC.cs`, `Content.Shared/_RMC14/Medical/Stasis/SharedBloodstreamSystem.RMC.cs`, `Content.Server/_RMC14/Medical/Stasis/RespiratorSystem.RMC.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static comparison against all three live-RMC call sites confirms the gate remains after timer advancement and before metabolism, blood regulation/bleeding, saturation, breathing, and damage. `dotnet build Content.Shared/Content.Shared.csproj --configuration DebugOpt --no-restore --no-dependencies --nologo --verbosity:minimal --disable-build-servers` succeeded with 0 errors and 6 unrelated warnings; the matching Server build succeeded with 0 errors and 4 unrelated warnings. `git diff --check` passed. Tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Runtime coverage must insert/eject at tick boundaries, exhaust and replace bags, delete containers, pause maps, remove organs, resimulate predicted shared ticks, and verify no metabolism, blood, breathing, or delayed catch-up occurs while stasis is active. The unused `CMStasisBagComponent.MetabolismMultiplier` is compatibility debt.

## CS-0311 - Keep RMC wound bleeding authoritative

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0310.
- Areas: Medical, Wounds, Bloodstream, Damage, Prediction, Authority, Timing
- Classification: Missing -> Adapted.
- Risk: High before the fix because one hit could create both an RMC timed wound and upstream generic bleed/critical blood loss; low after it pending predicted damage matrices.
- Behavior/API delta: Live RMC raises `CMBleedEvent` at the start of generic damage-derived bleeding. `WoundableComponent` handles that event because its own wound list, durations, treatment, stasis cancellation, and server recomputation are the intended source of bleed rate. The current bloodstream merge lost the event boundary while `SharedWoundsSystem` retained its handler, so RMC humans could receive both models at once.
- RMC/CMU divergence: The current bloodstream damage handler now invokes a narrow `_RMC14` partial gate after rejecting replicated state and before generic bleed calculation. Woundable RMC entities suppress only upstream damage-derived bleed; their damage event still creates/heals RMC wounds, and `WoundsSystem` remains responsible for setting `BleedAmount`. Entities without the RMC handler retain current positive-damage filtering, modifier sets, predicted critical blood loss, cauterization, audio, and popups.
- Decision and rationale: Restore the existing capability event rather than checking `WoundableComponent` directly in upstream code. This keeps ownership extensible, preserves current prediction behavior for non-RMC entities, and prevents two authorities from racing over the same networked bleed field.
- Files changed: `Content.Shared/Body/Systems/SharedBloodstreamSystem.cs`, `Content.Shared/_RMC14/Medical/Wounds/SharedBloodstreamSystem.RMC.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static event-flow review confirms the gate runs once per non-replicated `DamageChangedEvent`; `SharedWoundsSystem.OnWoundableBleed` handles it; and unhandled entities enter the unchanged upstream calculation. A targeted Shared build is recorded with this commit; `git diff --check` passed. Tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Runtime coverage must compare woundable/non-woundable hits, mixed positive/healing deltas, critical blood loss, cauterization, wound expiry/treatment, stasis, death, rejuvenation, prediction resimulation, and simultaneous server wound recomputation.

## CS-0312 - Restore RMC defibrillator eligibility and action timing

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0311.
- Areas: Medical, Defibrillators, Inventory, Skills, DoAfter, Prediction, Prototypes
- Classification: Missing -> Adapted.
- Risk: High before the fix because explicitly unrevivable targets/protective outerwear were defibrillatable and medical skill no longer affected the action; low-to-medium after it pending client/server matrices.
- Behavior/API delta: Live RMC rejects targets carrying `RMCDefibrillatorBlockedComponent`, including blocking outer clothing, reports the component-specific popup, applies the medical skill delay, and uses a rooted single-event DoAfter with a visible target effect and 0.5 movement threshold. The retained component fields, blocker prototypes, skill, and effect survived the merge but current `SharedDefibrillatorSystem` did not consume them.
- RMC/CMU divergence: Current shared/predicted `CanZap` now calls an `_RMC14` eligibility hook after upstream power, mob-state, and cooldown checks. The shared DoAfter retains upstream prediction and hand/range validation while restoring RMC's skill-derived duration, same-event duplicate policy, hand-switch tolerance, root marker, movement threshold, and `RMCEffectHealBusy`. Non-blocking outerwear and targets pass through unchanged.
- Decision and rationale: Adapt the live policy at current shared validation and DoAfter construction boundaries instead of restoring the old server-only defibrillator implementation. This retains upstream client prediction and authoritative completion revalidation while making RMC blockers and skill tuning effective again.
- Files changed: `Content.Shared/Medical/SharedDefibrillatorSystem.cs`, `Content.Shared/_RMC14/Medical/Defibrillator/SharedDefibrillatorSystem.RMC.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static review confirms both initial and completion-time `CanZap` calls run the blocker hook; the outer-clothing scan uses the current inventory enumerator; and the duration matches live RMC's base plus skill-multiplied extra time. `dotnet build Content.Shared/Content.Shared.csproj --configuration DebugOpt --no-restore --no-dependencies --nologo --verbosity:minimal --disable-build-servers` succeeded with 0 errors and 6 unrelated warnings. `git diff --check` passed. Tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Runtime coverage must exercise blocker changes during the DoAfter, prediction rollback, every medical skill level, nested/removed clothing, hand changes, movement/rooting, duplicate attempts, power removal, crit/dead targets, and target redirection. Charging-audio ownership remains intentionally on current predicted audio pending a presentation audit.

## CS-0313 - Restore RMC defibrillator healing without wasting charge

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0312.
- Areas: Medical, Defibrillators, Damage, Chemistry, Power cells, Events, Prediction, Authority
- Classification: Missing -> Adapted.
- Risk: High before the fix because RMC group healing/electrogenetic bonuses were inert and cancelled redirections consumed power; low-to-medium after it pending rollback and effect coverage.
- Behavior/API delta: The retained `RMCDefibrillatorDamageModifyEvent` distributes configured healing across RMC damage groups and consumes one unit of the strongest present electrogenetic reagent for its bonus, but current defibrillation applied `ZapHeal` directly and never raised the event. Current code also consumed a power-cell activation before self/target redirect events and their final validation, unlike live RMC's last-charge-safe ordering.
- RMC/CMU divergence: A cloned heal specifier now passes through the existing RMC event immediately before dead-target healing, avoiding mutation of prototype-owned `ZapHeal`. Power is consumed only after both redirect/cancellation events, completion-time eligibility checks, and final mob-state resolution succeed, but still before zap audio, electrocution, healing, cooldown, and revival effects. Upstream secondary-operator electrocution and shared predicted power APIs remain intact.
- Decision and rationale: Restore the event at the current damage boundary and move only the charge mutation across pure validation. This recovers RMC medical chemistry and prevents cancelled actions from wasting cells without reverting to the old server-only defibrillator system.
- Files changed: `Content.Shared/Medical/SharedDefibrillatorSystem.cs`, `Content.Shared/_RMC14/Medical/Defibrillator/SharedDefibrillatorSystem.RMC.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static order review confirms every cancellation/redirect revalidation precedes charge consumption and every gameplay effect follows it; the RMC event has one current caller and clones the base damage specifier; and standard defibrillators with no RMC group data/electrogenetic reagent retain base healing. `dotnet build Content.Shared/Content.Shared.csproj --configuration DebugOpt --no-restore --no-dependencies --nologo --verbosity:minimal --disable-build-servers` succeeded with 0 errors and 6 unrelated warnings. `git diff --check` passed. Tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Runtime coverage must exercise final-cell success, every cancellation and redirection point, prediction rollback, electrogenetic selection/consumption, grouped healing, rotten/unrevivable/no-mind/training-dummy targets, thresholds, cooldown, cell ejection, and secondary operator electrocution. RMC's old server-owned charging-audio cleanup is classified superseded/deferred while current predicted audio is retained.

## CS-0314 - Migrate RMC syringe modes to the current injector contract

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0313.
- Areas: Medical, Chemistry, Syringes, Prototypes, DoAfter timing, Prediction
- Classification: Missing -> Adapted.
- Risk: High before the fix because retained prefilled syringes referenced the deleted `toggleState` field and the lethal syringe's capacity, fixed dose, inject-only policy, and 30-second mob action were expressed through deleted per-component fields; low after the static migration, pending prototype/runtime validation.
- Behavior/API delta: Current SS14 moved injector behavior, transfer choices, and mob/container timing into inheritable `injectorMode` prototypes while `InjectorComponent` retains only the active/allowed mode IDs and current transfer amount. The stale RMC declarations therefore did not establish their intended starting state or lethal-injection contract.
- RMC/CMU divergence: Ordinary `CMSyringe` behavior remains inherited from current `Syringe`, including its predicted shared interaction owner and draw/inject modes. Prefilled syringes now select the current inject mode. The lethal syringe uses one RMC-owned inject-only mode with a single 50-unit amount, 30-second mob delay, and no per-volume delay, preserving its authored capacity and preventing blood drawing without forking `InjectorSystem`.
- Decision and rationale: Translate the obsolete data into the current mode prototype extension point. This preserves current validation, DoAfter cancellation, solution capability checks, networking, verbs, logging, and reactive effects while keeping the special policy data-owned under `_RMC14`.
- Files changed: `Resources/Prototypes/_RMC14/Entities/Objects/Medical/syringes.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static schema review confirms every `Injector` field now maps to `InjectorComponent`, `RMCInjectorModeLethal` inherits the required inject-mode localization and behavior, and the sole allowed mode cannot toggle to drawing. `git diff --check` passed for the exact staged paths. Prototype and behavioral tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Runtime/prototype coverage must spawn empty and prefilled variants, validate 50-unit capacity and transfer, target self/standing/downed mobs and containers, interrupt the 30-second DoAfter, inspect prediction rollback, and confirm ordinary CM syringes still draw and inject normally.

## CS-0315 - Migrate CM pills to the current edible contract

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0314.
- Areas: Medical, Chemistry, Nutrition, Pills, Solutions, Prototypes, DoAfter timing
- Classification: Missing -> Adapted.
- Risk: Critical before the fix because every `CMPill` inherited a removed `Food` component and therefore had no current ingestion owner; low-to-medium after the migration pending prototype/runtime validation.
- Behavior/API delta: Current SS14 represents consumables with `EdibleComponent` plus an `EdiblePrototype`, and current pills inherit the `SolutionPill` capability contract. RMC pill contents and examination data survived behind the temporary legacy solution migration, but swallowing, force-feeding, pill sound, whole-solution transfer, and destroy-on-empty behavior were disconnected.
- RMC/CMU divergence: CM pills now inherit current `SolutionPill` capabilities and use the current `Edible` component with the `Pill` behavior prototype while retaining RMC's instant self-use, one-second force-feed delay, whole-dose transfer, pill sound, 60-unit base capacity, child reagent fills, medical-skill examination, storage tags, and smart-fridge policy. Current ingestion prediction, target validation, solution transfer, reactive effects, and deletion lifecycle remain authoritative.
- Decision and rationale: Adopt the current capability parent and ingestion component rather than recreate the deleted `FoodSystem`. The existing compatibility migration still overlays each retained RMC `food` solution at map initialization, including child-specific capacities and contents, so this change restores behavior without a broad data rewrite in the same audit fix.
- Files changed: `Resources/Prototypes/_RMC14/Entities/Objects/Medical/pills.yml` and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static inheritance review confirms `CMPill` now has the same current solution/edibility capabilities as upstream pills, all RMC children still override the named `food` solution, and every retained `Edible` field maps to the current component. `git diff --check` passed for the exact staged paths. Prototype and behavioral tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: The retained `SolutionContainerManager` declarations still depend on the explicitly temporary compatibility migration and should be translated to solution-prototype inheritance after the checkpoint. Runtime coverage must verify all pill doses/capacities, self-use, force-feeding, interruption, reagent reactions, examination, empty deletion, smart-fridge insertion, and prediction rollback.

## CS-0316 - Restore CM reagent identity and guide filtering

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0315.
- Areas: Chemistry, Reagent prototypes, Guidebook, Client presentation, Prototype filtering
- Classification: Missing -> Adapted.
- Risk: High before the fix because the retained `isCM` resource field had no current schema owner and CM guide reagent groups included every upstream reagent; low after it pending prototype/guide runtime validation.
- Behavior/API delta: The bulk merge replaced CMU's `ReagentPrototype` declaration with current SS14's version, dropping its `ICMSpecific` marker and `IsCM` data field. It also replaced the reagent-group guide control with unconditional upstream enumeration, deleting the retained `IncludeUpstream` XML opt-in contract. RMC reagent inheritance still authored `isCM: true`, so resource intent and UI selection were disconnected.
- RMC/CMU divergence: The RMC reagent partial once again implements the existing fork prototype-filter capability and consumes `isCM`. Reagent group embeds use `EnumerateCM` by default and enumerate all current SS14 reagents only when the document explicitly supplies `IncludeUpstream="True"`. Outside CM filtering mode, `EnumerateCM` continues to expose all prototypes, preserving standard SS14 guide behavior.
- Decision and rationale: Reconnect the existing generic CM prototype extension through an `_RMC14` partial and one narrow client enumeration helper. This avoids a second guide registry, keeps current reagent rendering/search/reaction/source behavior, and makes the already-authored RMC base reagent contract effective again.
- Files changed: `Content.Shared/_RMC14/Chemistry/Reagent/ReagentPrototype.cs`, `Content.Client/Guidebook/Controls/GuideReagentGroupEmbed.xaml.cs`, `Content.Client/_RMC14/Guidebook/GuideReagentGroupEmbed.RMC.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static inheritance review confirms all RMC reagents derive from `CMReagent`, the sole retained `IncludeUpstream` use is the RMC drinks guide, and non-CM filtering falls through the existing `CMPrototypeExtensions.FilterCM` policy. Targeted Shared and Client DebugOpt `--no-dependencies` builds succeeded with 0 errors and 6/8 pre-existing warnings respectively; `git diff --check` passed for the exact staged paths. Tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Runtime/prototype coverage must load every `isCM` reagent, open each RMC chemical group, verify upstream-only reagents are absent by default, verify the drinks opt-in includes both sets, switch CM filtering mode, reload prototypes, and exercise localization/search/reaction/source displays.

## CS-0317 - Preserve authored RMC solution-transfer choices

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0316.
- Areas: Chemistry, Solution transfer, Verbs, UI, Networking, Prediction, Prototypes
- Classification: Behavior changed -> Adapted.
- Risk: Medium before the fix because eleven retained RMC `SolutionTransfer` declarations authored a deleted `transferAmounts` field and current SS14 silently substituted its global menu; low after it pending prototype/runtime validation.
- Behavior/API delta: Current SS14 derives transfer verbs from one global amount array constrained only by minimum and maximum values. RMC containers deliberately use irregular amounts such as 20, 25, 40, 45, 70, 80, 90, 100, 140, 180, 200, 300, 400, and 500, so bounds alone cannot represent their menus. The stale declarations were neither consumed by the component nor the verb owner.
- RMC/CMU divergence: `SolutionTransferComponent` now has an optional RMC compatibility list in its `_RMC14` partial and includes it in current component state. The current shared transfer system chooses that list when present and otherwise retains SS14's global defaults; its custom-amount UI, bounds clamp, access checks, shared verbs, logging, DoAfter transfers, solution capabilities, and server validation remain unchanged.
- Decision and rationale: Restore the old field as an optional data extension at the current enumeration boundary instead of duplicating the transfer system or converting authored menus into lossy bounds. This keeps a single authoritative transfer pipeline and makes all retained prototype contracts explicit and predictable.
- Files changed: `Content.Shared/Chemistry/EntitySystems/SolutionTransferSystem.cs`, `Content.Shared/_RMC14/Chemistry/Components/SolutionTransferComponent.RMC.cs`, `Content.Shared/_RMC14/Chemistry/SolutionTransferSystem.RMC.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Affected resource contracts: `Resources/Prototypes/_RMC14/Entities/Objects/Medical/bottles.yml`, `Resources/Prototypes/_RMC14/Entities/Objects/Medical/beaker.yml`, `Resources/Prototypes/_RMC14/Entities/Objects/Consumables/Drinks/canteen.yml`, `Resources/Prototypes/_RMC14/Entities/Objects/Tools/gas_tanks.yml`, `Resources/Prototypes/_RMC14/Entities/Objects/Tools/bucket.yml`, and `Resources/Prototypes/_RMC14/Entities/Objects/patron_figurines.yml` required no textual changes.
- Validation evidence: Static enumeration accounts for all eleven retained `SolutionTransfer.transferAmounts` declarations; each remains subject to current min/max checks, and components without the optional list take the unchanged upstream path. `dotnet build Content.Shared/Content.Shared.csproj --configuration DebugOpt --no-restore --no-dependencies --nologo --verbosity:minimal --disable-build-servers` succeeded with 0 errors and 6 pre-existing warnings; `git diff --check` passed for the exact staged implementation/docs paths. Tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Runtime/prototype coverage must inspect every custom menu, use preset and custom UI values at/around bounds, transfer in both directions, resimulate prediction, hot-reload prototypes, and confirm ordinary SS14 containers retain the current default menu. Longer term, upstream's planned transfer-mode prototype could replace this compatibility field.

## CS-0318 - Migrate retained RMC food to unified ingestion

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0317.
- Areas: Medical, Chemistry, Nutrition, Metabolism, Food prototypes, DoAfter timing, Prediction, Lifecycle
- Classification: Missing -> Adapted; implicit upstream default timing remains Behavior changed by design.
- Risk: Critical before the fix because 78 retained `Food` component declarations referenced a component and system deleted by current SS14, so prototype loading or ingestion could fail despite successful C# compilation; medium after the data migration pending prototype/runtime validation.
- Behavior/API delta: Current SS14 unified food and drink under the shared/predicted `IngestionSystem` and data-driven `EdiblePrototype` presentation contract. All authored RMC solution IDs, transfer quantities, trash, special-digestion flags, living-mob rejection, and custom sounds now flow through `EdibleComponent`; current stomach validation, solution transfer, reaction, DoAfter, deletion, appearance, verbs, and networking remain authoritative.
- RMC/CMU divergence: Explicit RMC fields are preserved exactly, including the soap solution, Nyx plush sound, special-digestion clothing/toy policies, fractional MRE portions, and snack trash. Declarations that relied only on the removed upstream `Food` defaults adopt current SS14's one-second self-ingestion default and `edible-nom` presentation instead of preserving the former 0.5-second/`food-nom` defaults; those were upstream defaults rather than intentional fork-authored divergence. The current Food edible prototype retains the eating sound family.
- Decision and rationale: Translate the component contract in place rather than revive `FoodSystem` or duplicate current ingestion. This applies upstream's consolidated authority/prediction path and removes an entire stale component family while retaining every explicit CM/RMC data choice.
- Files changed: `Resources/Prototypes/_RMC14/Entities/Clothing/Uniforms/base.yml`, `Resources/Prototypes/_RMC14/Entities/Mobs/NPCs/simplemob.yml`, `Resources/Prototypes/_RMC14/Entities/Objects/Fishing/fishing.yml`, `Resources/Prototypes/_RMC14/Entities/Objects/Fun/toys.yml`, `Resources/Prototypes/_RMC14/Entities/Objects/Tools/soap.yml`, `Resources/Prototypes/_RMC14/Entities/Objects/Consumables/Food/food.yml`, `Resources/Prototypes/_RMC14/Entities/Objects/Consumables/Food/snacks.yml`, `Resources/Prototypes/_RMC14/Entities/Objects/Consumables/Food/prepared.yml`, `Resources/Prototypes/_RMC14/Entities/Objects/Consumables/Food/pizza.yml`, `Resources/Prototypes/_RMC14/Entities/Objects/Consumables/Food/meat.yml`, `Resources/Prototypes/_RMC14/Entities/Objects/Consumables/Food/donuts.yml`, `Resources/Prototypes/_RMC14/Entities/Objects/Consumables/Food/MRE/mre_snack.yml`, `Resources/Prototypes/_RMC14/Entities/Objects/Consumables/Food/MRE/mre_side.yml`, `Resources/Prototypes/_RMC14/Entities/Objects/Consumables/Food/MRE/mre_main.yml`, `Resources/Prototypes/_RMC14/Entities/Objects/Consumables/Food/MRE/mre_dessert.yml`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static schema search finds zero `_RMC14` entity declarations whose component type is `Food`; all 78 replacements map their retained fields to current `EdibleComponent` fields. Scoped `git diff --check` passed. A C# build cannot validate resource deserialization, and prototype/behavioral test suites remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Runtime/prototype coverage must spawn every converted family, ingest and force-feed full/partial servings, validate special stomachs and living mobs, consume soap/clothing/toys, spawn trash, empty/delete items, run reactions and staged metabolism, interrupt/resimulate DoAfters, and compare the adopted current timing/presentation with CM balance expectations. Legacy inline solution declarations remain separate compatibility debt.

## CS-0319 - Migrate retained RMC drinks to unified ingestion

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0318.
- Areas: Medical, Chemistry, Nutrition, Drinks, Solutions, Examination, DoAfter timing, Prediction, Lifecycle
- Classification: Missing -> Adapted; empty-container interaction remains Behavior changed/Deferred.
- Risk: Critical before the fix because 14 retained `Drink` declarations referenced a deleted component and system, disconnecting direct drinking even though C# projects compiled; low-to-medium after the migration pending prototype/runtime validation.
- Behavior/API delta: Current SS14 routes both eating and drinking through shared/predicted `IngestionSystem` and distinguishes presentation with the `Drink` edible prototype. Every migrated declaration now explicitly selects its `drink`, `food`, or `bucket` solution, disables utensil requirements, and keeps reusable containers alive. Live RMC's 5-unit transfer, 0.5-second self-use, three-second force-feed, and drink sound are retained instead of inheriting current Edible defaults. Five direct bases also regain their old default examination capability through current `ExaminableSolution`; nine DrinkBase-derived entities retain the inherited current capability.
- RMC/CMU divergence: Current stomach validation, reactions, solution mutation, appearance, verbs, shared DoAfter, and prediction are authoritative. The bucket's removed `ignoreEmpty: true` has no exact data equivalent: current empty ingestion still returns unhandled so equip/use fallthrough survives, but it emits the current empty popup. The other thirteen reusable containers now also return unhandled after that popup, whereas old `DrinkSystem` handled the empty interaction. Unified ingestion's special-exclusive-stomach rules are retained.
- Decision and rationale: Express the full live-RMC reusable drink contract through current `Edible` and solution-examination capabilities rather than revive `DrinkSystem`. Preserve the user-visible timing and dose where the mapping is exact, and explicitly defer the minor empty-event consumption distinction rather than add a global ingestion fork without runtime evidence.
- Files changed: `Resources/Prototypes/_RMC14/Entities/Objects/Consumables/Drinks/alcohol.yml`, `Resources/Prototypes/_RMC14/Entities/Objects/Consumables/Drinks/canteen.yml`, `Resources/Prototypes/_RMC14/Entities/Objects/Consumables/Drinks/coffee.yml`, `Resources/Prototypes/_RMC14/Entities/Objects/Consumables/Drinks/cups.yml`, `Resources/Prototypes/_RMC14/Entities/Objects/Consumables/Drinks/glasses.yml`, `Resources/Prototypes/_RMC14/Entities/Objects/Consumables/Drinks/wy_water_bottle.yml`, `Resources/Prototypes/_RMC14/Entities/Objects/Consumables/Food/MRE/mre_drink.yml`, `Resources/Prototypes/_RMC14/Entities/Objects/Consumables/Food/condiments.yml`, `Resources/Prototypes/_RMC14/Entities/Objects/Tools/bucket.yml`, `Resources/Prototypes/_RMC14/Entities/Objects/patron_figurines.yml`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static schema search finds zero `_RMC14` entity declarations whose component type is `Drink`; all 14 replacements map only current `EdibleComponent` fields, and every direct non-DrinkBase solution retains approximate current examination. Scoped `git diff --check` passed. A C# build cannot validate resource deserialization, and prototype/behavioral test suites remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Runtime/prototype coverage must drink and force-feed each solution family, inspect full/partial/empty containers, verify bucket use/equip fallthrough and popups, test open/closed containers and recognizable reagents, resimulate DoAfters, exercise special-exclusive stomachs, and confirm reusable containers survive emptying. Legacy inline solution declarations remain separate compatibility debt.

## CS-0320 - Restore RMC vapor-hit hooks on current solution entities

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0319.
- Areas: Chemistry, Vapors, Fire, Acid, Stealth, Collision events, Solutions, Server authority
- Classification: Missing -> Adapted.
- Risk: High before the fix because `VaporHitEvent` retained seven subscriptions but no current raiser, leaving water-vapor fire suppression, acid cleanup/resistance, and vapor-triggered cloak/ghillie reveal inert; low-to-medium after it pending collision/runtime coverage.
- Behavior/API delta: Live RMC raised the hook after touch reactions with a legacy multi-solution manager and an extinguisher power value. Current SS14 gives each vapor one direct `SolutionComponent`, performs the authoritative touch reaction on collision, and no longer registers the legacy manager. The event payload and four solution-inspecting consumers still expected that deleted owner, so the merge could not reconnect the hook by compilation alone.
- RMC/CMU divergence: Current vapor throw, lifetime, tile reactions, impassable deletion, solution splitting, and touch-reaction order remain authoritative. A narrow server partial now raises `VaporHitEvent` immediately after the current touch reaction with the direct solution entity and retained default/override power. Tile fire, timed/damageable acid, spray acid, and user-acid handlers inspect that one solution directly; thermal cloak and passive ghillie consumers require no payload change.
- Decision and rationale: Migrate the local event contract to the current one-solution invariant rather than attach a compatibility manager to every vapor. This removes repeated legacy solution enumeration, keeps collision authority server-side, and restores all retained extension hooks without forking `VaporSystem`.
- Files changed: `Content.Server/Chemistry/EntitySystems/VaporSystem.cs`, `Content.Server/_RMC14/Chemistry/VaporSystem.RMC.cs`, `Content.Shared/_RMC14/Chemistry/VaporHitEvent.cs`, `Content.Shared/_RMC14/Atmos/SharedRMCFlammableSystem.cs`, `Content.Shared/_RMC14/Xenonids/Acid/SharedXenoAcidSystem.cs`, `Content.Shared/_RMC14/Xenonids/Spray/XenoSprayAcidSystem.cs`, `Content.Shared/_RMC14/Xenonids/Projectile/Spit/XenoSpitSystem.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static call-graph review finds one authoritative raiser after current touch reaction and accounts for every retained subscriber; all solution-sensitive handlers now consume `SolutionComponent.Solution` directly, and extinguisher power still defaults to 7 or uses `RMCExtinguisherPowerComponent`. Targeted Shared and Server DebugOpt `--no-dependencies` builds succeeded with 0 errors and 6/4 pre-existing warnings respectively; `git diff --check` passed for the exact implementation/docs paths. Tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Runtime coverage must collide water/non-water vapors with ordinary and instant tile fire, extinguish each acid state with/without gun second wind and grace periods, reveal both stealth systems, vary extinguisher power, hit hard impassables and the spray user, and verify one collision produces one hook after its current touch reaction.

## CS-0321 - Medical and Chemistry subsystem closeout

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0320.
- Scope completed: systems, components, events, resource/prototype contracts, CVars, networking/prediction, authority, timing, lifecycle hooks, and deferred tests across solution storage/transfer, injectors/hyposprays, ingestion, organs/body graphs, metabolism, bloodstream/wounds, respiration/stasis, defibrillation, vapor reactions, CPR/IV, surgery, scanners/autodoc/sleepers, ChemMaster/dispenser/fridge paths, and reagent guide presentation.
- Classification: subsystem source audit completed; behavioral parity is not claimed without prototype and runtime tests.
- `Aligned`: Current SS14's solution-entity/capability model, shared solution transfer and reaction APIs, unified predicted ingestion, flat body/container lifecycle, organ-category surgery, staged/predicted metabolism scheduling, bloodstream replication, shared defibrillator completion revalidation, secondary-operator electrocution, current vapor touch/tile reactions, and current medical UI/machine owners remain authoritative. Static review found the retained CPR, IV, surgery, health scanner, autodoc, sleeper, ChemMaster, dispenser, fridge, and refill systems connected to current components/events rather than duplicate global owners.
- `Adapted`: CS-0303 and CS-0305 through CS-0320 repair legacy solution migration/capability gates; RMC body, blood-volume, organ-stage, and separate `chemicals` routes; `RMCChemicalEffect` context; stasis tick cancellation; RMC wound bleed authority; defibrillator blockers/skills/DoAfter/healing/charge order; current syringe and pill contracts; CM reagent identity/guide filtering; authored solution-transfer menus; all removed Food/Drink declarations; and all retained vapor-hit consumers. The adaptations use current SS14 owners with `_RMC14` partials/events/data where a narrow extension exists.
- `Missing`: No additional high-confidence Medical/Chemistry hook with a current content-side mapping remains known after the final stale-component, event-raiser, and data-field sweeps. This classification is bounded to reviewed source and resource contracts, not runtime proof.
- `Behavior changed`: CM food declarations that carried no explicit timing adopt current SS14's one-second unified-ingestion default and current Food presentation; RMC drinks retain their prior 0.5-second timing but use current unified digestion. Empty reusable drinks now return unhandled after the current popup, including the bucket whose old `ignoreEmpty` also suppressed that popup. Current predicted defibrillator charging audio and secondary-operator shock supersede the old server audio lifecycle. Current staged metabolism, flat organs, one-solution vapors, and solution-entity examination intentionally replace their deleted counterparts.
- `Deferred`: 102 legacy `SolutionContainerManager` declarations remain in the six Medical resource families (`chemical-containers`, `auto_injectors`, `pills`, `beaker`, `bottles`, and `syringes`) and are serviced by the explicitly temporary compatibility loader; 376 remain repository-wide. Defibrillator training-dummy/no-mind/rotten-target nuances, the old charging-audio cleanup contract, broad parent-tree migration, and all prototype/runtime matrices remain for the checkpoint. The compatibility manager adds spawn-time migration and duplicate-solution warning risk and should be removed family by family, not by blind replacement.
- Intentional CM/RMC divergence retained: separate injectable `chemicals` flow; ten-reagent heart cap; RMC blood volumes; wound-owned bleed rates and both replicated bleed CVars (`rmc.bloodloss_multiplier`, `rmc.bleed_time_multiplier`); hard stasis semantics; medical-skill defibrillation; blocker clothing/targets; grouped/electrogenetic healing; RMC ChemMaster client presets; special syringe timing; pill timing/capacity; drink timing/reusability; custom transfer menus; reagent filtering; and vapor-driven fire/acid/stealth hooks.
- Upstream betterment retained: current entity-owned solutions remove manager lookups from hot paths; flat organs and explicit stage routing make lifecycle/authority boundaries inspectable; auto-paused staged metabolism avoids catch-up timing surprises; unified shared ingestion removes duplicate Food/Drink authorities; current shared/predicted defibrillation and solution transfer keep client feedback aligned with server revalidation; and current single-solution vapors let RMC consumers avoid legacy enumeration.
- Validation evidence: targeted Shared builds after the final code waves succeeded with 0 errors and 6 pre-existing warnings; targeted Server builds succeeded with 0 errors and 4 pre-existing warnings; the guide wave's Client build succeeded with 0 errors and 8 pre-existing warnings. Exact-path `git diff --check` ran before every Medical/Chemistry commit. Static searches now find no `_RMC14` entity component declaration named `Food` or `Drink`, no stale RMC metabolism group key, one current `RMCChemicalEffect` dispatch, one current `VaporHitEvent` raiser, and all retained subscribers accounted for.
- Tests: no test or prototype suite was run at 853/1,000 upstream commits, per the checkpoint rule. Compilation demonstrates API compatibility only.
- Remaining risk: High-risk runtime matrices remain for solution migration/prototype loading, organ insertion/removal, oral/injected/inhaled metabolism, stasis boundaries, wound/prediction resimulation, defibrillator cancellation/redirection/final-cell behavior, edible inheritance, empty containers, vapor collisions, machine UI round trips, map serialization, and all authored reagent doses/capacities.
- Next subsystem: Movement, beginning with climb obstacle policy, grounded walk-speed calculation, resting post-processing, and conditional RMC water contacts/occlusion.

## CS-0322 - Restore RMC climb obstacle policy

- Upstreams compared: live SS14 `fbb3c79b2d206eede2210fbbf5ca1c237c262767`, live RMC `b6d677947dd8ebcb06194a66798938645fed5a54`, and CMU through CS-0321.
- Areas: Movement, Climbing, Collision masks, Interaction events, Prediction
- Classification: Missing -> Adapted.
- Risk: Medium before the fix because the merged climb owner ignored the RMC barricade collision layer and the RMC obstacle preflight; low-to-medium after it pending runtime collision/prediction coverage.
- Behavior/API delta: Current SS14 owns the shared climb lifecycle and raises `AttemptClimbEvent` on the selected target. Live RMC additionally includes `BarricadeImpassable` in the climb collision mask and rejects paths blocked by RMC obstacles before starting the climb. The bulk merge retained `RMCMovementSystem.CanClimbOver` but disconnected it from the current owner.
- Decision and behavior: Keep current SS14 climb timing, DoAfter, virtual-controller, animation, cancellation, target event, and prediction paths. A narrow `_RMC14` partial invokes the retained obstacle policy before the current target event and excludes the target from that scan so `AttemptClimbEvent` is not raised twice; the collision mask again includes RMC barricades.
- Files changed: `Content.Shared/Climbing/Systems/ClimbSystem.cs`, `Content.Shared/_RMC14/Movement/ClimbSystem.RMC.cs`, and `docs/upstream-sync/core-system-audit.md`.
- Validation evidence: Static flow review confirms the RMC preflight runs once before current climb startup, the selected target is still validated by the current event owner, and the mask covers table, low-impassable, and barricade layers. Targeted Shared DebugOpt `--no-dependencies` build succeeded with 0 errors and 6 pre-existing warnings; exact-path `git diff --check` passed. Tests remain deferred until the 1,000-upstream-commit checkpoint.
- Remaining debt: Runtime coverage must exercise adjacent and intervening barricades, climbable/non-climbable obstacles, cancelled target events, predicted rollback, moving targets, buckled entities, and simultaneous climbs. Live RMC's older buckle-specific preflight ordering is not reproduced without evidence that current buckle validation loses behavior.
