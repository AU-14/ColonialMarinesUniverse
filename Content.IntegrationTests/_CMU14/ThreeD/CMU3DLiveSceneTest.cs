#nullable enable
using System.Reflection;
using Content.Client.Administration.Managers;
using Content.Client.CMU14.ThreeD;
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
