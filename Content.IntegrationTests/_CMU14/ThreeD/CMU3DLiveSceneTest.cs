#nullable enable
using System.Reflection;
using Content.Client.Administration.Managers;
using Content.Client.CMU14.ThreeD.Scene;
using Content.IntegrationTests.Fixtures;
using Content.Server.Administration.Managers;
using Content.Server.CMU14.ZLevels.Core;
using Content.Server.CMU14.ThreeD;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Administration;
using Content.Shared.CMU14.ThreeD;
using Robust.Client.UserInterface;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.ThreeD;

[TestFixture]
[TestOf(typeof(CMU3DLiveSceneSystem))]
public sealed class CMU3DLiveSceneTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Dirty = true,
    };

    [Test]
    public async Task SparseFloorsDoNotHideFurnitureOnTheOccupiedFloor()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;
        var original = session.AttachedEntity;
        EntityUid actor = default;
        await Server.WaitPost(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var levels = Server.System<CMUZLevelsSystem>();
            var network = levels.CreateZNetwork();
            var floors = new Dictionary<EntityUid, int>
                { [SComp<TransformComponent>(map.GridCoords.EntityId).MapUid!.Value] = 0 };
            for (var depth = 1; depth <= 6; depth++)
                floors.Add(maps.CreateMap(runMapInit: true), depth);
            Assert.That(levels.TryAddMapsIntoZNetwork(network, floors), Is.True);
            SEntMan.EnsureComponent<CMU3DMapComponent>(SComp<TransformComponent>(map.GridCoords.EntityId).MapUid!.Value);
            actor = SSpawnAtPosition("MobHuman", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(session, actor);
            Server.ResolveDependency<IAdminManager>().PromoteHost(session);
        });
        await WaitForDebugFlag(true);
        await Pair.RunUntilSynced();
        try
        {
            await Client.WaitAssertion(() =>
            {
                var coordinates = CComp<TransformComponent>(ToClientUid(actor)).Coordinates;
                var chairs = new HashSet<EntityUid>();
                for (var x = -20; x < 20; x++)
                for (var y = -20; y < 20; y++)
                    chairs.Add(CEntMan.SpawnEntity("CMChair", coordinates.Offset(new Vector2(x, y))));
                var scene = Client.System<CMU3DLiveSceneSystem>();
                Assert.That(scene.Open(), Is.True);
                var view = new CMU3DSceneControl(true);
                typeof(CMU3DLiveSceneSystem).GetField("_firstPersonView", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(scene, view);
                var refresh = typeof(CMU3DLiveSceneSystem).GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic)!;
                refresh.Invoke(scene, null);
                refresh.Invoke(scene, null);
                var shown = Snapshot(view).Where(box => box.Source is { } uid && chairs.Contains(uid))
                    .Select(box => box.Source!.Value).Distinct().ToHashSet();
                Assert.That(shown, Is.EquivalentTo(chairs),
                    "Already available furniture must not disappear while other floors leave the global geometry budget unused.");
            });
        }
        finally
        {
            await Client.WaitPost(() => Client.System<CMU3DLiveSceneSystem>().Close());
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, original));
        }
    }

    [Test]
    public async Task FirstPersonLookRemainsLocalUntilPredictionTick()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;
        var original = session.AttachedEntity;
        EntityUid actor = default;
        try
        {
            await Server.WaitPost(() =>
            {
                actor = SSpawnAtPosition("MobHuman", map.GridCoords);
                Server.PlayerMan.SetAttachedEntity(session, actor);
            });
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() =>
            {
                var local = ToClientUid(actor);
                var mover = CComp<Content.Shared.Movement.Components.InputMoverComponent>(local);
                var look = Client.System<Content.Client.Movement.Systems.CameraMouseRotationSystem>();
                // Supply an already captured relative gesture without requiring an OS window.
                typeof(Content.Client.Movement.Systems.CameraMouseRotationSystem)
                    .GetField("_rotating", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(look, true);
                typeof(Content.Client.Movement.Systems.CameraMouseRotationSystem)
                    .GetField("_externalMouse", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(look, true);
                typeof(Content.Client.Movement.Systems.CameraMouseRotationSystem)
                    .GetField("_targetRotation", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(look, mover.RelativeRotation);
                var previous = mover.RelativeRotation;
                look.RelativeLook(120);
                Assert.That(mover.RelativeRotation, Is.EqualTo(previous),
                    "A render-frame mouse sample must not dirty/rewrite a networked prediction component; only its sequenced prediction event may do that.");
                Assert.That(look.TryGetRelativeLookRotation(local, out var presented), Is.True);
                Assert.That(Angle.ShortestDistance(previous, presented).Degrees, Is.EqualTo(-18).Within(.001),
                    "The view must consume the new mouse angle before any server tick or acknowledgement.");
                look.StopRotating();
                Assert.That(look.TryGetRelativeLookRotation(local, out var released), Is.True);
                Assert.That(released, Is.EqualTo(presented), "Releasing capture must not snap to an older replicated angle.");
                look.FrameUpdate(1f / 60);
                Assert.That(mover.RelativeRotation, Is.EqualTo(previous),
                    "Waiting for acknowledgement must not reapply presentation angles to simulation state on render frames.");
            });
            await RunTicksSync(12);
            await Server.WaitAssertion(() =>
                Assert.That(Angle.ShortestDistance(Angle.Zero,
                    SComp<Content.Shared.Movement.Components.InputMoverComponent>(actor).RelativeRotation).Degrees,
                    Is.EqualTo(-18).Within(.001), "The sequenced look event must still reach server movement."));
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, original));
        }
    }

    [Test]
    public async Task PrefetchedGeometryIsNotTargetableOutsideVisibleRange()
    {
        await Client.WaitAssertion(() =>
        {
            using var view = new CMU3DSceneControl(true);
            view.VisibleRadius = 2;
            view.SetCameraOverride(new Content.Client.CMU14.ThreeD.CMU3DCameraFrame(
                new Vector3(0, 0, 1), Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ, 100, Vector2.Zero));
            var source = new EntityUid(999999);
            var box = new CMU3DSceneBox(new Vector3(0, 4, 1), new Vector3(.4f), 0, Color.Red, source);
            view.SetScene([box]);
            var miss = view.Aim(Vector2.Zero, out var target);
            Assert.That(target, Is.Null, "Buffered geometry outside the drawn view must not receive targeting.");
            Assert.That(miss.Position.Y, Is.EqualTo(2).Within(.002));
            view.SetCameraOverride(new Content.Client.CMU14.ThreeD.CMU3DCameraFrame(
                new Vector3(0, 2, 1), Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ, 100, Vector2.Zero));
            var hit = view.Aim(Vector2.Zero, out target);
            Assert.That(target, Is.EqualTo(source), "Walking into range must reveal the already packed source without a scene rebuild.");
            Assert.That(hit.Position.Y, Is.EqualTo(3.6f).Within(.002));
        });
    }

    [Test]
    public async Task PendingSceneKeepsPreviousGeometryUntilCompleteAndCanBeCancelled()
    {
        await Client.WaitAssertion(() =>
        {
            using var view = new CMU3DSceneControl(true);
            var near = new CMU3DSceneBox(new Vector3(0, 2, 1), new Vector3(.4f), 0, Color.Red);
            var far = near with { Center = new Vector3(0, 4, 1), Color = Color.Blue };
            view.SetScene([near]);
            void AssertDistance(float distance)
            {
                var encoding = GetPrivate<CMU3DSceneEncoding>(view, "_encoding");
                Assert.That(encoding.TryPick(new Vector3(0, 0, 1), Vector3.UnitY, out var hit), Is.True);
                Assert.That(hit.Distance, Is.EqualTo(distance).Within(.002f));
            }
            using (var cancelled = view.StageScene([far], () => true).GetEnumerator())
            {
                Assert.That(cancelled.MoveNext(), Is.True);
                AssertDistance(1.6f);
            }
            var yields = 0;
            foreach (var step in view.StageScene([far], () => true))
            {
                yields++;
                AssertDistance(1.6f);
                Assert.That(Snapshot(view), Is.EqualTo(new[] { near }),
                    "Neither drawing nor picking may see an incomplete replacement.");
            }
            Assert.That(yields, Is.GreaterThan(1));
            AssertDistance(3.6f);
            Assert.That(Snapshot(view), Is.EqualTo(new[] { far }));
            view.SetScene([near]);
            AssertDistance(1.6f);
        });
    }

    [Test]
    public async Task WalkingKeepsStaticGeometryStableBetweenSceneOriginChanges()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;
        var originalActor = session.AttachedEntity;
        EntityUid actor = default;
        EntityUid chair = default;
        try
        {
            await Server.WaitPost(() =>
            {
                actor = SSpawnAtPosition("MobHuman", map.GridCoords);
                chair = SSpawnAtPosition("CMChair", map.GridCoords.Offset(Vector2.UnitX));
                SEntMan.EnsureComponent<CMU3DMapComponent>(SComp<TransformComponent>(map.GridCoords.EntityId).MapUid!.Value);
                Server.PlayerMan.SetAttachedEntity(session, actor);
                Server.ResolveDependency<IAdminManager>().PromoteHost(session);
            });
            await WaitForDebugFlag(true);
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() =>
            {
                var scene = Client.System<CMU3DLiveSceneSystem>();
                Assert.That(scene.Open(), Is.True);
                // Sample the actual first-person adapter without native window/mouse APIs in the headless client.
                var view = new CMU3DSceneControl(true);
                typeof(CMU3DLiveSceneSystem).GetField("_firstPersonView", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(scene, view);
                var refresh = typeof(CMU3DLiveSceneSystem).GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic)!;
                refresh.Invoke(scene, null);
                var clientChair = ToClientUid(chair);
                var before = Snapshot(view).Where(box => box.Source == clientChair).ToArray();
                var initialOrigin = GetPrivate<Vector2>(scene, "_sceneOrigin");
                Assert.That(before, Is.Not.Empty);
                var transform = Client.System<SharedTransformSystem>();
                var clientActor = ToClientUid(actor);
                var coordinates = CComp<TransformComponent>(clientActor).Coordinates;
                transform.SetCoordinates(clientActor, coordinates.Offset(new Vector2(.125f, 0)));
                refresh.Invoke(scene, null);
                transform.SetCoordinates(clientActor, coordinates.Offset(new Vector2(3, 0)));
                refresh.Invoke(scene, null);
                Assert.That(GetPrivate<Vector2>(scene, "_sceneOrigin"), Is.EqualTo(initialOrigin),
                    "Walking through the buffered region must not recenter and repack the scene.");
                var after = Snapshot(view).Where(box => box.Source == clientChair).ToArray();
                Assert.That(after, Is.EqualTo(before),
                    "Sub-tile walking must move the camera without repacking every stationary model.");
                transform.SetCoordinates(clientActor, coordinates.Offset(new Vector2(6, 0)));
                refresh.Invoke(scene, null);
                var newOrigin = GetPrivate<Vector2>(scene, "_sceneOrigin");
                var shifted = Snapshot(view).Where(box => box.Source == clientChair).ToArray();
                Assert.That(newOrigin, Is.Not.EqualTo(initialOrigin));
                Assert.That(shifted.Length, Is.EqualTo(before.Length));
                for (var i = 0; i < shifted.Length; i++)
                    Assert.That(Vector3.Distance(shifted[i].Center + new Vector3(newOrigin, 0),
                        before[i].Center + new Vector3(initialOrigin, 0)), Is.LessThan(.00001f),
                        "Changing the scene origin must preserve the model's physical world position.");
                var chairCoordinates = CComp<TransformComponent>(clientChair).Coordinates;
                transform.SetCoordinates(clientChair, chairCoordinates.Offset(Vector2.UnitY));
                refresh.Invoke(scene, null);
                var moved = Snapshot(view).Where(box => box.Source == clientChair).ToArray();
                Assert.That(moved.Length, Is.EqualTo(shifted.Length));
                for (var i = 0; i < moved.Length; i++)
                    Assert.That(Vector3.Distance(moved[i].Center, shifted[i].Center + Vector3.UnitY), Is.LessThan(.00001f),
                        "Reusing a local model assembly must not freeze the entity's position.");
                transform.SetWorldRotation(clientChair, Angle.FromDegrees(90));
                refresh.Invoke(scene, null);
                var turned = Snapshot(view).Where(box => box.Source == clientChair).ToArray();
                Assert.That(turned, Is.Not.Empty);
                Assert.That(turned, Is.Not.EqualTo(moved), "Turning the source must replace its cached pose.");
                Client.System<Robust.Client.GameObjects.SpriteSystem>().SetColor((clientChair, null), Color.Red);
                refresh.Invoke(scene, null);
                var tinted = Snapshot(view).Where(box => box.Source == clientChair).ToArray();
                Assert.That(tinted.Length, Is.EqualTo(turned.Length));
                Assert.That(tinted.Select(box => box.Color), Is.Not.EqualTo(turned.Select(box => box.Color)),
                    "Changing the source tint must replace the cached assembly's colors.");
                var advance = typeof(CMU3DLiveSceneSystem).GetMethod("AdvanceRefresh", BindingFlags.Instance | BindingFlags.NonPublic)!;
                transform.SetCoordinates(clientActor, coordinates.Offset(new Vector2(12, 0)));
                advance.Invoke(scene, [clientActor, CComp<TransformComponent>(clientActor)]);
                if (GetPrivate<object?>(scene, "_refreshSteps") != null)
                {
                    Assert.That(GetPrivate<Vector2>(scene, "_sceneOrigin"), Is.EqualTo(newOrigin),
                        "The old snapshot must retain its origin while a new region is being assembled.");
                    Assert.That(Snapshot(view).Where(box => box.Source == clientChair), Is.EqualTo(tinted));
                }
                // Allow different frame counts on slower hosts, but keep changing regions
                // so cancelling each in-flight sample would never publish a replacement.
                for (var frame = 0; frame < 100 && GetPrivate<Vector2>(scene, "_sceneOrigin") == newOrigin; frame++)
                {
                    transform.SetCoordinates(clientActor, coordinates.Offset(new Vector2(frame % 2 == 0 ? 16 : 20, 0)));
                    advance.Invoke(scene, [clientActor, CComp<TransformComponent>(clientActor)]);
                }
                var publishedOrigin = GetPrivate<Vector2>(scene, "_sceneOrigin");
                Assert.That(publishedOrigin, Is.Not.EqualTo(newOrigin), "Walking must not indefinitely cancel an in-flight snapshot.");
                var published = Snapshot(view).Where(box => box.Source == clientChair).ToArray();
                Assert.That(published.Length, Is.EqualTo(tinted.Length));
                for (var i = 0; i < published.Length; i++)
                    Assert.That(Vector3.Distance(published[i].Center + new Vector3(publishedOrigin, 0),
                        tinted[i].Center + new Vector3(newOrigin, 0)), Is.LessThan(.00001f));
            });
        }
        finally
        {
            await Client.WaitPost(() => Client.System<CMU3DLiveSceneSystem>().Close());
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, originalActor));
        }
    }

    [Test]
    public async Task SceneSubscribesToDistantFloorsAndFollowsActor()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;
        var originalActor = session.AttachedEntity;
        EntityUid upper = default;
        EntityUid lower = default;
        EntityUid network = default;
        EntityUid actor = default;
        EntityUid chair = default;
        EntityUid distantChair = default;
        EntityUid lowerChair = default;
        var originalPvs = false;
        try
        {
            await Server.WaitAssertion(() =>
            {
                var maps = Server.System<SharedMapSystem>();
                var zLevels = Server.System<CMUZLevelsSystem>();
                // The shared integration pool normally disables PVS, which would hide this regression.
                originalPvs = Server.CfgMan.GetCVar(Robust.Shared.CVars.NetPVS);
                Server.CfgMan.SetCVar(Robust.Shared.CVars.NetPVS, true);
                upper = maps.CreateMap(runMapInit: true);
                lower = maps.CreateMap(runMapInit: true);
                var zNetwork = zLevels.CreateZNetwork();
                network = zNetwork.Owner;
                Assert.That(zLevels.TryAddMapsIntoZNetwork(zNetwork,
                    new() { [SComp<TransformComponent>(map.GridCoords.EntityId).MapUid!.Value] = 0, [upper] = 2, [lower] = -2 }), Is.True);
                actor = SSpawnAtPosition("MobHuman", map.GridCoords);
                var position = Server.System<SharedTransformSystem>().GetWorldPosition(actor);
                // This test measures replicated floor height, so support the chairs instead of letting
                // Z physics drop them through empty maps while the subscription is being established.
                var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
                foreach (var level in new[] { upper, lower })
                {
                    var grid = SEntMan.EnsureComponent<MapGridComponent>(level);
                    foreach (var offset in new[] { 1, 65 })
                        maps.SetTile(level, grid, maps.WorldToTile(level, grid, position + new Vector2(offset, 0)), floor);
                }
                maps.SetTile(map.Grid, map.Grid.Comp,
                    maps.WorldToTile(map.Grid, map.Grid.Comp, position + new Vector2(64, 0)), floor);
                chair = SSpawnAtPosition("CMChair", new EntityCoordinates(upper, position + Vector2.UnitX));
                lowerChair = SSpawnAtPosition("CMChair", new EntityCoordinates(lower, position + Vector2.UnitX));
                distantChair = SSpawnAtPosition("CMChair", new EntityCoordinates(upper, position + new Vector2(65, 0)));
                Server.PlayerMan.SetAttachedEntity(session, actor);
                Server.ResolveDependency<IAdminManager>().PromoteHost(session);
            });
            await WaitForDebugFlag(true);
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() => Assert.That(Client.System<CMU3DLiveSceneSystem>().Open(), Is.True));
            await RunTicksSync(30);
            await Pair.RunUntilSynced();
            await Server.WaitAssertion(() =>
            {
                var probes = session.ViewSubscriptions.Where(SEntMan.HasComponent<CMU3DViewProbeComponent>).ToArray();
                Assert.That(probes, Has.Length.EqualTo(3), "Exactly one origin per rendered map, including the actor's map.");
                Assert.That(probes.All(probe => !SEntMan.HasComponent<CMUZLevelViewerComponent>(probe)), Is.True,
                    "3D origins must not recursively spawn ordinary Z probes.");
            });
            await Client.WaitAssertion(() =>
            {
                var scene = Client.System<CMU3DLiveSceneSystem>();
                scene.FrameUpdate(1);
                var boxes = Snapshot(View(scene)).Where(box => box.Source == ToClientUid(chair)).ToArray();
                Assert.That(boxes, Is.Not.Empty, "The 3D view needs geometry beyond the adjacent 2D stair preview.");
                Assert.That(boxes.All(box => box.Center.Z > 5), Is.True, "The replicated floor must retain its height.");
                var lowerBoxes = Snapshot(View(scene)).Where(box => box.Source == ToClientUid(lowerChair)).ToArray();
                Assert.That(lowerBoxes, Is.Not.Empty);
                Assert.That(lowerBoxes.All(box => box.Center.Z < -4), Is.True);
            });

            await Server.WaitPost(() => Server.System<SharedTransformSystem>()
                .SetCoordinates(actor, map.GridCoords.Offset(new Vector2(64, 0))));
            await RunTicksSync(30);
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() =>
            {
                var scene = Client.System<CMU3DLiveSceneSystem>();
                scene.FrameUpdate(1);
                var snapshot = Snapshot(View(scene));
                Assert.That(snapshot.Any(box => box.Source == ToClientUid(distantChair)), Is.True,
                    "Moving the actor must move the remote-floor PVS origins too.");
                Assert.That(snapshot.Any(box => box.Source == ToClientUid(chair)), Is.False);
            });
            await Client.WaitPost(() => Client.System<CMU3DLiveSceneSystem>().Close());
            await RunTicksSync(5);
            await Server.WaitAssertion(() => Assert.That(
                session.ViewSubscriptions.Any(SEntMan.HasComponent<CMU3DViewProbeComponent>), Is.False));
            await Client.WaitAssertion(() => Assert.That(Client.System<CMU3DLiveSceneSystem>().Open(), Is.True));
            await RunTicksSync(5);
            await Server.WaitAssertion(() =>
            {
                Assert.That(session.ViewSubscriptions.Any(SEntMan.HasComponent<CMU3DViewProbeComponent>), Is.True);
                Server.ResolveDependency<IAdminManager>().DeAdmin(session);
                Assert.That(session.ViewSubscriptions.Any(SEntMan.HasComponent<CMU3DViewProbeComponent>), Is.False,
                    "The server must revoke expanded replication without trusting the client to close its view.");
            });
        }
        finally
        {
            await Client.WaitPost(() => Client.System<CMU3DLiveSceneSystem>().Close());
            await Server.WaitPost(() =>
            {
                Server.PlayerMan.SetAttachedEntity(session, originalActor);
                Server.CfgMan.SetCVar(Robust.Shared.CVars.NetPVS, originalPvs);
            });
            if (upper.IsValid())
                await Pair.DeleteEntityTreeLeafFirst(upper);
            if (lower.IsValid())
                await Pair.DeleteEntityTreeLeafFirst(lower);
            if (network.IsValid())
                await Pair.DeleteEntityTreeLeafFirst(network);
        }
    }

    [Test]
    public async Task DeadminClearsRetainedSceneAndDeniesDirectReopen()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;
        var originalActor = session.AttachedEntity;
        var admins = Server.ResolveDependency<IAdminManager>();
        EntityUid chair = default;
        CMU3DSceneControl? oldView = null;
        try
        {
            await Server.WaitPost(() =>
            {
                var actor = SSpawnAtPosition("MobHuman", map.GridCoords);
                chair = SSpawnAtPosition("CMChair", map.GridCoords.Offset(new Vector2(1, 0)));
                Server.PlayerMan.SetAttachedEntity(session, actor);
                admins.PromoteHost(session);
            });
            await WaitForDebugFlag(true);
            await Pair.RunUntilSynced();

            await Client.WaitAssertion(() =>
            {
                var scene = Client.System<CMU3DLiveSceneSystem>();
                Assert.That(scene.Open(), Is.True);
                oldView = View(scene);
                Assert.That(Snapshot(oldView).Any(box => box.Source == ToClientUid(chair)), Is.True,
                    "The permission test must begin with real replicated scene content.");
            });

            await Server.WaitPost(() => admins.DeAdmin(session));
            await WaitForDebugFlag(false);
            await Client.WaitAssertion(() =>
            {
                var scene = Client.System<CMU3DLiveSceneSystem>();
                Assert.Multiple(() =>
                {
                    Assert.That(scene.IsOpen, Is.False,
                        "AdminStatusUpdated must revoke the window without waiting for the next scene refresh.");
                    Assert.That(Snapshot(oldView!), Is.Empty,
                        "A retained reference to the old control must not retain the revoked scene.");
                    Assert.That(oldView!.RenderedBoxes, Is.Zero);
                    Assert.That(scene.Open(), Is.False, "Direct callers must obey the same gate as the command.");
                });
                scene.FrameUpdate(1);
                Assert.That(scene.IsOpen, Is.False);
                Assert.That(Snapshot(oldView!), Is.Empty);
            });

            await Server.WaitPost(() => admins.ReAdmin(session));
            await WaitForDebugFlag(true);
            await Client.WaitAssertion(() =>
            {
                var scene = Client.System<CMU3DLiveSceneSystem>();
                Assert.That(scene.Open(), Is.True);
                Assert.That(View(scene), Is.Not.SameAs(oldView),
                    "Reopening must create a usable control instead of reusing released GPU resources.");
                Assert.That(Snapshot(View(scene)).Any(box => box.Source == ToClientUid(chair)), Is.True);
            });
        }
        finally
        {
            await Client.WaitPost(() => Client.System<CMU3DLiveSceneSystem>().Close());
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, originalActor));
        }
    }

    [Test]
    public async Task MapChangeReplacesGeometryAndActorLossClosesScene()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;
        var originalActor = session.AttachedEntity;
        EntityUid otherMap = default;
        EntityUid actor = default;
        EntityUid oldChair = default;
        EntityUid newChair = default;
        EntityUid contained = default;
        try
        {
            await Server.WaitAssertion(() =>
            {
                actor = SSpawnAtPosition("MobHuman", map.GridCoords);
                oldChair = SSpawnAtPosition("CMChair", map.GridCoords.Offset(new Vector2(1, 0)));
                var containerHost = SSpawnAtPosition(null, map.GridCoords);
                contained = SSpawnAtPosition("Wrench", map.GridCoords);
                var containers = Server.System<SharedContainerSystem>();
                var container = containers.EnsureContainer<ContainerSlot>(containerHost, "live-scene-test");
                Assert.That(containers.Insert(contained, container), Is.True);

                var maps = Server.System<SharedMapSystem>();
                otherMap = maps.CreateMap(runMapInit: true);
                var grid = SEntMan.EnsureComponent<MapGridComponent>(otherMap);
                var tile = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
                maps.SetTile(otherMap, grid, Vector2i.Zero, tile);
                maps.SetTile(otherMap, grid, new Vector2i(1, 0), tile);
                newChair = SSpawnAtPosition("CMChair", new EntityCoordinates(otherMap, new Vector2(1, 0)));
                Server.PlayerMan.SetAttachedEntity(session, actor);
                Server.ResolveDependency<IAdminManager>().PromoteHost(session);
            });
            await WaitForDebugFlag(true);
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() =>
            {
                var scene = Client.System<CMU3DLiveSceneSystem>();
                Assert.That(scene.Open(), Is.True);
                var snapshot = Snapshot(View(scene));
                Assert.That(snapshot.Any(box => box.Source == ToClientUid(oldChair)), Is.True);
                Assert.That(snapshot.Any(box => box.Source == ToClientUid(contained)), Is.False,
                    "Replicated container contents are excluded from the inspectable world scene.");
            });

            await Server.WaitPost(() => Server.System<SharedTransformSystem>()
                .SetCoordinates(actor, new EntityCoordinates(otherMap, Vector2.Zero)));
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() =>
            {
                var scene = Client.System<CMU3DLiveSceneSystem>();
                scene.FrameUpdate(1);
                Assert.That(scene.IsOpen, Is.True);
                var snapshot = Snapshot(View(scene));
                Assert.Multiple(() =>
                {
                    Assert.That(snapshot.Any(box => box.Source == ToClientUid(newChair)), Is.True);
                    Assert.That(snapshot.Any(box => box.Source == ToClientUid(oldChair)), Is.False,
                        "Changing maps must replace the old map geometry in the same refresh.");
                });
            });

            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, null));
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() =>
            {
                var scene = Client.System<CMU3DLiveSceneSystem>();
                scene.FrameUpdate(1);
                Assert.That(scene.IsOpen, Is.False);
                Assert.That(scene.Open(), Is.False, "Debug permission alone does not supply an attached map actor.");
            });
        }
        finally
        {
            await Client.WaitPost(() => Client.System<CMU3DLiveSceneSystem>().Close());
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, originalActor));
            if (otherMap.IsValid())
                await Pair.DeleteEntityTreeLeafFirst(otherMap);
        }
    }

    private async Task WaitForDebugFlag(bool expected)
    {
        for (var i = 0; i < 30; i++)
        {
            await RunTicksSync(1);
            var matches = false;
            await Client.WaitPost(() => matches = Client.ResolveDependency<IClientAdminManager>()
                .HasFlag(AdminFlags.Debug) == expected);
            if (matches)
                return;
        }
        await Client.WaitAssertion(() => Assert.That(Client.ResolveDependency<IClientAdminManager>()
            .HasFlag(AdminFlags.Debug), Is.EqualTo(expected), "The server permission change did not reach the client."));
    }

    private static CMU3DSceneControl View(CMU3DLiveSceneSystem scene) =>
        GetPrivate<CMU3DLiveSceneWindow>(scene, "_window").FindControl<CMU3DSceneControl>("View");

    private static IReadOnlyList<CMU3DSceneBox> Snapshot(CMU3DSceneControl view) =>
        GetPrivate<List<CMU3DSceneBox>>(view, "_snapshot");

    private static T GetPrivate<T>(object instance, string field) =>
        (T) instance.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;
}
