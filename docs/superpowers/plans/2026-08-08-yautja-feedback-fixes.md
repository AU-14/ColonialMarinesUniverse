# Yautja Feedback Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Исправить подтверждённые Yautja/Hunter Ship дефекты PR #1648 и проверить их на runtime-путях.

**Architecture:** Изменения разделяются на FTL arrival, combat/leap, Medicomp, radio и ghost-role configuration. Каждый блок меняется локально и получает собственный regression test до production fix; общий client/server smoke выполняется после сборки.

**Tech Stack:** C# 13/.NET 10, RobustToolbox ECS, NUnit integration tests, YAML prototypes, PowerShell launch scripts.

## Global Constraints

- Рабочая ветка: `codex/pr1648-fixes`, основанная на PR head `1dbece3bc469038754412ca31c9681f823668129`.
- Не изменять чужие worktree и незакоммиченные файлы `master`.
- Keycode Yautja: английский `#r`, если targeted collision scan не обнаружит занятие; при collision выбрать ближайший свободный CMSS13-compatible английский keycode и зафиксировать его в тесте.
- Self-destruct production logic не менять без воспроизводимого action/UI failure.

---

### Task 1: Dropship exact-position arrival

**Files:**
- Modify: `Content.Server/Shuttles/Systems/ShuttleSystem.FasterThanLight.cs`
- Test: `Content.IntegrationTests/_CMU14/Yautja/YautjaMedicompDropshipEndToEndDiagnosticTest.cs` or a focused `HunterShipDropshipLandingTest.cs`

**Interfaces:**
- Consumes: FTL `TargetCoordinates`, destination `TransformComponent`, docking config and map/grid components.
- Produces: exact-position arrival for grid-only `DropshipDestination` targets; docking still uses proximity only when a docking config exists.

- [ ] Write a failing runtime test loading `huntership_upper.yml` and three real `hunter_shuttle.yml` grids; assert final distance to A/B/Hangar < 2 tiles.
- [ ] Run the focused test and record the existing 103/123/116-tile failure.
- [ ] Change arrival branching so missing docking config falls back to the original target coordinates on the destination map, not `TryFTLProximity` around the parent grid.
- [ ] Run the focused test and verify all three destinations pass.
- [ ] Run existing dropship destination isolation tests.

### Task 2: Xeno melee and Yautja fire resistance

**Files:**
- Inspect/modify Yautja damage modifier prototype under `Resources/Prototypes/_CMU14/Threats/Yautja` only if the live event test identifies a mismatch.
- Test: `Content.IntegrationTests/_CMU14/Yautja/YautjaResistanceParityTest.cs` and a focused live melee/fire test.

**Interfaces:**
- Consumes: actual xeno melee event, `DamageableSystem`, `FlammableComponent`/fire tick systems.
- Produces: nonzero reduced damage and fire tick behavior matching the documented CMSS13 coefficients.

- [ ] Add a real Ravager/T1 attack test against spawned Yautja and assert damage delta > 0 and coefficient-consistent.
- [ ] Add a deterministic fire-stack/tick test comparing equivalent human and Yautja targets.
- [ ] Run tests before production edits; if coefficients already pass, keep production code unchanged and only retain the regression coverage.
- [ ] If a live mismatch exists, change only the modifier/flammable source causing it and rerun both tests.

### Task 3: Leap self-damage and audio chain

**Files:**
- Modify the Yautja leap system and/or Yautja prototype component registration where `RMCObstacleSlamming` is inherited.
- Test: `Content.IntegrationTests/_CMU14/Yautja/YautjaLeapTest.cs` plus focused trajectory/collision tests.

**Interfaces:**
- Consumes: leap action, throw collision events, obstacle slamming and `EmoteOnDamage`.
- Produces: Yautja can leap through open space and collide with a wall without self damage/bleed/pain emote; the intended leap sound remains.

- [ ] Add free-flight and wall-impact runtime tests asserting damage and external bleeding remain unchanged.
- [ ] Run tests and capture current self-slam failure.
- [ ] Add temporary leap-specific obstacle-slam immunity or remove self collision damage at the leap source, without disabling damage to leap targets.
- [ ] Run leap, damage-emote and xeno movement tests.

### Task 4: Medicomp open incision and full-cycle regression

**Files:**
- Modify: `Resources/Prototypes/_CMU14/Medical/Treatment/Surgery/Surgeries/yautja_medicomp.yml`
- Modify: `Resources/Prototypes/_CMU14/Medical/Treatment/Surgery/surgery_step_metadata.yml`
- Modify: `Content.Server/_CMU14/Yautja/YautjaMedicompSurgerySystem.cs`
- Test: focused Yautja Medicomp integration test in `Content.IntegrationTests/_CMU14/Yautja`

**Interfaces:**
- Consumes: CMU surgery metadata, incision depth, wound ledger and timed session flow.
- Produces: normal 5/15/10 cycle, shallow-open 15/10 cycle, clamp restoration of selected incision depth.

- [ ] Add a timed three-step test and a shallow-incision test that starts at healing gun.
- [ ] Run tests and verify the open path fails because only one surgery definition exists.
- [ ] Add the open surgery definition/metadata and make clamp set the selected part depth to surface while retaining all-body external bleed cleanup.
- [ ] Run both Medicomp tests and existing clamp/healing parity tests.

### Task 5: Communicator keycode and faction frequency

**Files:**
- Modify Yautja communicator/headset prototypes under `Resources/Prototypes/_CMU14/Threats/Yautja/Equipment`.
- Modify the Yautja faction/communications prototype under `Resources/Prototypes/_CMU14`.
- Test: focused radio integration test under `Content.IntegrationTests/_CMU14/Yautja`.

**Interfaces:**
- Consumes: chat prefix parser, radio channel registry, `FactionFrequencies`, comms tower tuning.
- Produces: `#r` Yautja channel, no `:h` conflict, tower discoverability and two-entity transmission.

- [ ] Scan all keycodes and write a failing test for `#r` ownership and Yautja frequency absence.
- [ ] Change communicator keycode and add Yautja faction frequency using existing RMC faction conventions.
- [ ] Run the parser/tower/transmission test and verify the message is received by another Yautja.

### Task 6: CMU hunting-ground ghost roles

**Files:**
- Modify: `Resources/Prototypes/_CMU14/.../hunting_ground_roles.yml` and any CMU role/settings prototypes it inherits.
- Test: focused ghost-role eligibility test under `Content.IntegrationTests/_CMU14/Yautja`.

**Interfaces:**
- Consumes: ghost-role requirements and CMU/RMC playtime tracker IDs.
- Produces: hunting-ground roles using CMU-owned role requirements without inherited RMC `CMJobRifleman` hours.

- [ ] Add an eligibility test with CMU tracker time set and RMC tracker time zero; verify current role is rejected.
- [ ] Replace inherited random humanoid settings/requirements with CMU-owned prototypes or explicit empty requirements.
- [ ] Run all hunting-ground and ghost-role tests.

### Task 7: Build, client/server smoke and review

**Files:**
- Modify: only files proven necessary by Tasks 1–6.
- Verify: `Content.IntegrationTests`, Release build, `runserver.bat`, `runclient.bat`.

- [ ] Run targeted tests for every changed block.
- [ ] Run full Release build and record warnings/errors.
- [ ] Start server with a bounded timeout, verify listening/startup readiness, then start client against it.
- [ ] Inspect server/client logs for startup exceptions and connection state.
- [ ] Run `git diff --check`, review diff scope, and summarize remaining unconfirmed self-destruct behavior.
