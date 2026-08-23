# Yautja Moving-Pad Landing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Hunter Shuttle destinations A and B resolve against the Hunter Ship landing pad's current pose at FTL arrival instead of its stale world pose from departure.

**Architecture:** Preserve each Yautja destination as `EntityCoordinates` relative to the Hunter Ship grid while travelling. The existing exact Yautja arrival path then converts that target through the grid's current transform, aligns the shuttle hull center, and leaves the shuttle static on the destination map.

**Tech Stack:** C# 14, RobustToolbox entity transforms and shuttle FTL systems, NUnit integration tests, YAML-backed Hunter Ship maps.

## Global Constraints

- Ordinary dropship destinations must retain the existing proximity fallback.
- The arriving Hunter Shuttle must remain parented to the map, not nested under the Hunter Ship grid.
- The arrived shuttle must remain static, on-ground, and fixed-rotation.
- The fix must not freeze Hunter Ship grids, add grid joints, alter Z-level synchronization, or move map markers.
- Preserve all unrelated dirty-worktree changes.

---

### Task 1: Reproduce a moving A/B pad and keep the FTL target grid-relative

**Files:**
- Modify: `Content.IntegrationTests/_CMU14/Yautja/HunterShipDropshipLandingTest.cs:40-168`
- Modify: `Content.Server/_RMC14/Dropship/DropshipSystem.cs:697-712`
- Verify: `Content.Server/Shuttles/Systems/ShuttleSystem.FasterThanLight.cs:537-610`

**Interfaces:**
- Consumes: `DropshipSystem.FlyTo(...)`, `FTLComponent.TargetCoordinates`, `SharedTransformSystem.SetLocalPositionRotation(...)`, and the existing Yautja no-docking-config branch in `UpdateFTLArriving(...)`.
- Produces: an FTL target whose `EntityId` is the Hunter Ship grid and an arrival pose whose hull center matches the destination's current world pose.

- [ ] **Step 1: Rewrite the regression expectation before changing production code**

Keep `hunterGrid` outside the map-loading closure, remove `selectedDestinationPoses`, and assert that each launched shuttle retains a grid-relative target:

```csharp
EntityUid hunterShip = default;
EntityUid hunterGrid = default;
var destinations = new Dictionary<string, EntityUid>();
var shuttles = new List<(EntityUid Shuttle, EntityUid Console, EntityUid Destination)>();
```

Inside the map-loading closure, assign the loaded grid rather than declaring a shadowing local:

```csharp
hunterGrid = hunterGrids!.Single().Owner;
```

Immediately after each successful `FlyTo`, replace the stale-pose snapshot with this assertion:

```csharp
var ftl = entMan.GetComponent<FTLComponent>(shuttle);
Assert.That(ftl.TargetCoordinates.EntityId, Is.EqualTo(hunterGrid),
    $"{entMan.ToPrettyString(shuttle)} must keep the selected Hunter Ship destination grid-relative during FTL.");
Assert.That(docking.GetDockingConfigAt(
    shuttle,
    ftl.TargetCoordinates.EntityId,
    ftl.TargetCoordinates,
    ftl.TargetAngle), Is.Null);
```

After all flights have started, move and rotate the destination grid, then freeze its test pose so later physics does not add an unrelated second movement:

```csharp
var hunterTransform = entMan.GetComponent<TransformComponent>(hunterGrid);
transform.SetLocalPositionRotation(
    hunterGrid,
    hunterTransform.LocalPosition + new Vector2(4f, -3f),
    hunterTransform.LocalRotation + Angle.FromDegrees(90),
    hunterTransform);

var hunterBody = entMan.GetComponent<PhysicsComponent>(hunterGrid);
var hunterFixtures = entMan.GetComponent<FixturesComponent>(hunterGrid);
var physics = entMan.System<PhysicsSystem>();
physics.SetLinearVelocity(hunterGrid, Vector2.Zero, body: hunterBody);
physics.SetAngularVelocity(hunterGrid, 0f, body: hunterBody);
physics.SetBodyType(hunterGrid, BodyType.Static, manager: hunterFixtures, body: hunterBody);
physics.SetFixedRotation(hunterGrid, true, manager: hunterFixtures, body: hunterBody);
```

In the final assertion, compare the arrived hull with the destination's current pose:

```csharp
var destinationPosition = transform.GetMapCoordinates(destination).Position;
var centerDistance = Vector2.Distance(shuttleAabb.Center, destinationPosition);
Assert.That(centerDistance, Is.EqualTo(0f).Within(0.01f),
    $"{entMan.ToPrettyString(shuttle)} hull center must match the landing pad's current position.");
Assert.That(transform.GetWorldRotation(shuttle).GetCardinalDir(),
    Is.EqualTo(transform.GetWorldRotation(destination).GetCardinalDir()),
    $"{entMan.ToPrettyString(shuttle)} must match the landing pad's current orientation.");
```

- [ ] **Step 2: Stop the manually running binaries before rebuilding**

Run:

```powershell
$runDir = 'D:\RussianCM\tmp-cmu14-dropship-landing'
@('server-final.pid', 'client-final.pid') |
    ForEach-Object { Join-Path $runDir $_ } |
    Where-Object { Test-Path $_ } |
    ForEach-Object { Stop-Process -Id ([int](Get-Content $_)) -ErrorAction SilentlyContinue }
```

Expected: the `Content.Server` and `Content.Client` instances started for manual verification exit, releasing build outputs.

- [ ] **Step 3: Build and run the focused test to verify RED**

Run:

```powershell
dotnet build Content.IntegrationTests/Content.IntegrationTests.csproj --no-restore -m:1 -p:UseSharedCompilation=false -v:quiet
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --no-build --filter "FullyQualifiedName~HunterShipDropshipLandingTest.HunterShuttlesArriveAtTheirSelectedHunterShipDestinations" --logger "console;verbosity=minimal"
```

Expected: FAIL because `FTLComponent.TargetCoordinates.EntityId` is the Hunter Ship map entity, not `hunterGrid`. This proves the test detects the stale map-coordinate conversion.

- [ ] **Step 4: Remove the departure-time Yautja map snapshot**

In `DropshipSystem.FlyTo`, retain the ordinary mover coordinates and local destination rotation:

```csharp
var destTransform = Transform(destination);
var destCoords = _transform.GetMoverCoordinates(destination, destTransform);
var rotation = destTransform.LocalRotation;
```

Delete only the following Yautja-specific conversion:

```csharp
var exactYautjaLanding = newDestination != null &&
    string.Equals(newDestination.FactionController, "yautja", StringComparison.OrdinalIgnoreCase);

if (exactYautjaLanding)
{
    var mapCoordinates = _transform.ToMapCoordinates(destCoords);
    destCoords = new EntityCoordinates(_map.GetMap(mapCoordinates.MapId), mapCoordinates.Position);
    rotation = _transform.GetWorldRotation(destination);
}
```

Do not change the arrival-side Yautja branch: when `TargetCoordinates.EntityId` is a grid, it already resolves the target's current map coordinates and world rotation before subtracting the rotated `LocalAABB.Center`.

- [ ] **Step 5: Build and verify GREEN**

Run:

```powershell
dotnet build Content.Server/Content.Server.csproj --no-restore -m:1 -p:UseSharedCompilation=false -v:quiet
dotnet build Content.IntegrationTests/Content.IntegrationTests.csproj --no-restore -m:1 -p:UseSharedCompilation=false -v:quiet
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --no-build --filter "FullyQualifiedName~HunterShipDropshipLandingTest.HunterShuttlesArriveAtTheirSelectedHunterShipDestinations" --logger "console;verbosity=minimal"
```

Expected: builds exit with 0 errors and the landing test passes for A, B, and Hangar after the Hunter Ship grid is moved during FTL.

- [ ] **Step 6: Verify destination access isolation**

Run:

```powershell
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --no-build --filter "FullyQualifiedName~HunterShipDropshipDestinationIsolationTest" --logger "console;verbosity=minimal"
```

Expected: 2 tests pass; ordinary and third-party navigation consoles still cannot use Yautja destinations.

- [ ] **Step 7: Commit the focused code and test change**

Run:

```powershell
git add -- Content.IntegrationTests/_CMU14/Yautja/HunterShipDropshipLandingTest.cs Content.Server/_RMC14/Dropship/DropshipSystem.cs Content.Server/Shuttles/Systems/ShuttleSystem.FasterThanLight.cs
git diff --cached --check
git commit -m "fix: track moving Yautja landing pads"
```

Expected: the commit contains only the landing regression and FTL landing implementation files.

### Task 2: Correct the status record and relaunch the verified game

**Files:**
- Modify: `Yautja_Systems_Status.md:385`

**Interfaces:**
- Consumes: the verified grid-relative FTL behavior from Task 1.
- Produces: an accurate implementation record and a running local server/client pair built from the corrected source.

- [ ] **Step 1: Correct the landing status description**

Replace the departure-time map snapshot claim with the verified behavior:

```markdown
- Посадка Hunter Ship dropship на A/B beacon и Hangar сохраняет destination в локальных координатах Hunter Ship grid во время FTL и вычисляет актуальную map-позицию только при прибытии. Центр корпуса выравнивается по текущему beacon с учётом `LocalAABB.Center` и поворота; shuttle остаётся отдельным static/fixed-rotation grid карты. Обычные shuttle destinations продолжают использовать proximity fallback. Регрессионный тест перемещает и поворачивает Hunter Ship во время FTL и проверяет все три точки с точностью до `0.01` тайла.
```

- [ ] **Step 2: Run final source and whitespace verification**

Run:

```powershell
dotnet build Content.IntegrationTests/Content.IntegrationTests.csproj --no-restore -m:1 -p:UseSharedCompilation=false -v:minimal
git diff --check
```

Expected: 0 build errors and no whitespace errors. Existing analyzer warnings in the dirty branch must be reported separately rather than described as new failures.

- [ ] **Step 3: Launch the server from the rebuilt binary**

Run:

```powershell
$worktree = 'D:\RussianCM\.worktrees\pr1648-fixes'
$runDir = 'D:\RussianCM\tmp-cmu14-dropship-landing'
$server = Start-Process -FilePath (Join-Path $worktree 'bin\Content.Server\Content.Server.exe') `
    -WorkingDirectory $worktree `
    -ArgumentList '--data-dir', (Join-Path $runDir 'data'), '--cvar', 'net.port=1212' `
    -RedirectStandardOutput (Join-Path $runDir 'server-final.out.log') `
    -RedirectStandardError (Join-Path $runDir 'server-final.err.log') `
    -PassThru
$server.Id | Set-Content (Join-Path $runDir 'server-final.pid')
```

Wait until `server-final.out.log` contains `Server Version 278.0.0.0 -> Ready` and `Socket bound to 0.0.0.0:1212: True`.

- [ ] **Step 4: Launch and verify the client connection**

Run:

```powershell
$client = Start-Process -FilePath (Join-Path $worktree 'bin\Content.Client\Content.Client.exe') `
    -WorkingDirectory $worktree `
    -ArgumentList '--connect', '--connect-address', 'localhost', '--username', 'CodexQA' `
    -RedirectStandardOutput (Join-Path $runDir 'client-final.out.log') `
    -RedirectStandardError (Join-Path $runDir 'client-final.err.log') `
    -PassThru
$client.Id | Set-Content (Join-Path $runDir 'client-final.pid')
```

Expected server log evidence:

```text
Approved "[::1]:..." with username "localhost@CodexQA"
Connected
```

Expected client log evidence:

```text
Runlevel changed to: InGame
Attaching local player
```

- [ ] **Step 5: Preserve the existing dirty status document change**

Do not stage `Yautja_Systems_Status.md` automatically because it already contains other user-requested status edits. Report the corrected line as an uncommitted worktree change together with the running server/client process state.
