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
