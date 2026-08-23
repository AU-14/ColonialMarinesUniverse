# Yautja Dropship Landing Alignment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Yautja Hunter Shuttle landings align the shuttle hull center and original orientation with the selected Hunter Ship landing marker.

**Architecture:** Keep the existing Yautja-only exact-coordinate branch in `ShuttleSystem.FasterThanLight`. Convert the destination to map space, derive the grid origin from the shuttle `LocalAABB.Center`, and apply the destination world rotation while parenting the shuttle directly to the map. Extend the real FTL integration test to assert hull-center alignment, map parenting, and cardinal orientation for A, B, and Hangar.

**Tech Stack:** C#/.NET 10, Robust.Shared transform and map-grid APIs, NUnit integration tests, `dotnet test` and `dotnet build`.

## Global Constraints

- Exact alignment applies only when `DropshipDestinationComponent.FactionController` is `yautja`.
- Ordinary dropship destinations continue to use the existing docking/proximity fallback.
- The shuttle grid must be parented directly to the destination map; nested grid parenting is invalid.
- The selected marker denotes the visible hull center, not the shuttle grid origin.
- Position assertions use a maximum tolerance of `0.01` tile.

---

### Task 1: Add the failing hull-center regression assertion

**Files:**
- Modify: `Content.IntegrationTests/_CMU14/Yautja/HunterShipDropshipLandingTest.cs:116-139`

**Interfaces:**
- Consumes: The existing real FTL setup for A beacon, B beacon, and Hangar destinations.
- Produces: A regression test that fails against the current origin-on-marker implementation because the shuttle hull center is offset by its local AABB center.

- [ ] **Step 1: Replace the origin-distance assertion with a hull-center assertion**

After the existing `MapUid` and `ParentUid` assertions, read the shuttle grid and compute its world-space AABB:

```csharp
var shuttleGrid = entMan.GetComponent<MapGridComponent>(shuttle);
var shuttleAabb = transform.GetWorldMatrix(shuttle).TransformBox(shuttleGrid.LocalAABB);
var destinationPosition = transform.GetMapCoordinates(destination).Position;

Assert.That(Vector2.Distance(shuttleAabb.Center, destinationPosition), Is.EqualTo(0f).Within(0.01f),
    $"{entMan.ToPrettyString(shuttle)} hull center must match its selected destination.");
Assert.That(transform.GetWorldRotation(shuttle).GetCardinalDir(),
    Is.EqualTo(transform.GetWorldRotation(destination).GetCardinalDir()),
    $"{entMan.ToPrettyString(shuttle)} must preserve the selected destination orientation.");
```

Remove the old assertion comparing `GetMapCoordinates(shuttle).Position` directly to the marker, because that compares the grid origin with a hull-center marker.

- [ ] **Step 2: Run the focused test and verify the expected RED failure**

Run:

```powershell
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --no-build --filter "FullyQualifiedName~HunterShipDropshipLandingTest.HunterShuttlesArriveAtTheirSelectedHunterShipDestinations" --logger "console;verbosity=minimal"
```

Expected: the test fails at the hull-center assertion for the current implementation, with an offset approximately equal to the 7x13 shuttle grid center `(3.5, 6.5)`.

### Task 2: Center the exact Yautja arrival on the destination marker

**Files:**
- Modify: `Content.Server/Shuttles/Systems/ShuttleSystem.FasterThanLight.cs:540-558`

**Interfaces:**
- Consumes: `mapCoordinates`, the destination `EntityCoordinates`, `entity.Comp1.TargetAngle`, and the arriving shuttle's `MapGridComponent.LocalAABB`.
- Produces: A map-parented shuttle transform whose world-space hull center equals the selected Yautja marker and whose world rotation equals the marker's world rotation.

- [ ] **Step 1: Compute the desired world rotation and grid-origin offset**

Inside the existing Yautja-only branch, resolve the destination parent rotation and shuttle local AABB center:

```csharp
var mapUid = _mapSystem.GetMap(mapCoordinates.MapId);
var destinationRotation = entity.Comp1.TargetAngle + _transform.GetWorldRotation(target.EntityId);
var shuttleCenter = Comp<MapGridComponent>(uid).LocalAABB.Center;
var gridOrigin = mapCoordinates.Position - destinationRotation.RotateVec(shuttleCenter);
```

Keep the existing `mapUid` parenting and call `SetCoordinates` with `new EntityCoordinates(mapUid, gridOrigin)` and `rotation: destinationRotation`.

- [ ] **Step 2: Run the focused test and verify GREEN**

Run the same focused command from Task 1. Expected: A beacon, B beacon, and Hangar all pass the hull-center, map-parent, and orientation assertions.

### Task 3: Run regression coverage and build validation

**Files:**
- No additional source changes expected.

**Interfaces:**
- Consumes: The corrected arrival path and existing destination-isolation tests.
- Produces: Evidence that Yautja alignment works and ordinary destination behavior remains isolated.

- [ ] **Step 1: Run the Yautja destination isolation test**

Run:

```powershell
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --no-build --filter "FullyQualifiedName~HunterShipDropshipDestinationIsolationTest" --logger "console;verbosity=minimal"
```

Expected: 2/2 tests pass, including rejection of non-Yautja access to Yautja destinations.

- [ ] **Step 2: Build the integration test project and check whitespace**

Run:

```powershell
dotnet build Content.IntegrationTests/Content.IntegrationTests.csproj --no-restore -m:1 -p:UseSharedCompilation=false -v:minimal
git diff --check
```

Expected: build succeeds with zero errors and warnings; `git diff --check` emits no diagnostics.

- [ ] **Step 3: Verify the running client and server**

Check that the existing `Content.Server` and `Content.Client` processes are responding and that the server log contains a completed connection for `localhost@QA_Yautja`.

### Task 4: Update the status record

**Files:**
- Modify: `Yautja_Systems_Status.md` in the Hunter Ship dropship verification section.

**Interfaces:**
- Consumes: The verified landing behavior and test command results.
- Produces: A concise record that destinations denote hull centers and the shuttle retains map-parenting and destination orientation.

- [ ] **Step 1: Update the status text**

State that A/B/Hangar exact arrivals subtract the rotated shuttle `LocalAABB.Center` from the selected marker, preserve marker orientation, and keep ordinary destinations on proximity fallback.

- [ ] **Step 2: Run the focused landing test once more after documentation changes**

Run the focused landing command from Task 1 and record its final pass count before reporting completion.
