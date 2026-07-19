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
