using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server.Camera;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Server.Power;
using Content.Server.Power.Components;
using Content.Server.Speech;
using Content.Server._RMC14.Camera;
using Content.Server.SurveillanceCamera;
using Content.Server.Wires;
using Content.Shared._RMC14.Camera;
using Content.Shared.Camera;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Item;
using Content.Shared.Interaction;
using Content.Shared.Power;
using Content.Shared.Pinpointer;
using Content.Shared.Speech;
using Content.Shared.SurveillanceCamera;
using Content.Shared.SurveillanceCamera.Components;
using Content.Shared.Verbs;
using Content.Shared.Wires;
using Robust.Server.GameObjects;
using Robust.Shared;
using Robust.Shared.EntitySerialization;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.UnitTesting;

namespace Content.IntegrationTests._CMU14.Camera;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public sealed class CameraNetworkSystemTest
{

    private const string NetworkA = "CMUTestCameraNetworkA";
    private const string NetworkB = "CMUTestCameraNetworkB";

    [Test]
    public async Task RuntimeNetworkAndSourceOwnedGrantUseCanonicalIdentity()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);
        try
        {
            await LoadPrototypes(server);
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var networks = entMan.System<CameraNetworkSystem>();
                var receiver = entMan.SpawnEntity("CMUTestCameraDynamicReceiver", MapCoordinates.Nullspace);
                var camera = entMan.SpawnEntity("CMUTestCameraStandardA", MapCoordinates.Nullspace);
                var source = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
                var runtime = networks.CreateNetwork("Runtime test", source);

                try
                {
                    Assert.That(networks.AddNetwork(camera, runtime), Is.True);
                    Assert.That(networks.GrantNetwork(receiver, runtime, source), Is.True);
                    Assert.Multiple(() =>
                    {
                        Assert.That(networks.GetEffectiveNetworkEntities(receiver), Does.Contain(runtime));
                        Assert.That(networks.GetAccessibleCameras(
                            (receiver, entMan.GetComponent<CameraNetworkReceiverComponent>(receiver))),
                            Does.Contain(camera));
                    });

                    entMan.DeleteEntity(source);

                    Assert.Multiple(() =>
                    {
                        Assert.That(networks.GetEffectiveNetworkEntities(receiver), Does.Not.Contain(runtime));
                        Assert.That(networks.CanAccess(receiver, camera), Is.False);
                    });
                }
                finally
                {
                    entMan.DeleteEntity(runtime);
                    entMan.DeleteEntity(receiver);
                    entMan.DeleteEntity(camera);
                }
            });
        }
        finally
        {
            server.Dispose();
        }
    }

    [Test]
    public async Task RmcEditorOwnedNetworkIsIsolatedBetweenConsoles()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);
        try
        {
            await LoadPrototypes(server);
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var rmc = entMan.System<RMCCameraSystem>();
                var networks = entMan.System<CameraNetworkSystem>();
                var firstUid = entMan.SpawnEntity("CMUTestDualNetworkRmcComputer", MapCoordinates.Nullspace);
                var secondUid = entMan.SpawnEntity("CMUTestDualNetworkRmcComputer", MapCoordinates.Nullspace);
                var actor = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

                try
                {
                    var first = entMan.GetComponent<RMCCameraComputerComponent>(firstUid);
                    var second = entMan.GetComponent<RMCCameraComputerComponent>(secondUid);
                    Assert.That(rmc.TryCreateEditorNetwork(
                        (firstUid, first), actor, 0, "Console one", out _), Is.True);
                    var created = rmc.BuildEditorState((firstUid, first)).Networks
                        .Single(network => network.Origin == RMCCameraNetworkEditorOrigin.Owned);
                    var createdUid = entMan.GetEntity(created.Id);

                    Assert.Multiple(() =>
                    {
                        Assert.That(networks.GetEffectiveNetworkEntities(firstUid), Does.Contain(createdUid));
                        Assert.That(networks.GetEffectiveNetworkEntities(secondUid), Does.Not.Contain(createdUid));
                        Assert.That(rmc.BuildEditorState((secondUid, second)).Networks.Select(network => network.Id),
                            Does.Not.Contain(created.Id));
                    });
                }
                finally
                {
                    entMan.DeleteEntity(firstUid);
                    entMan.DeleteEntity(secondUid);
                    entMan.DeleteEntity(actor);
                }
            });
        }
        finally
        {
            server.Dispose();
        }
    }

    [Test]
    public async Task RmcEditorRuntimeGrantIsSelectableButNotEditable()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);
        try
        {
            await LoadPrototypes(server);
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var rmc = entMan.System<RMCCameraSystem>();
                var networks = entMan.System<CameraNetworkSystem>();
                var consoleUid = entMan.SpawnEntity("CMUTestRmcMapComputer", MapCoordinates.Nullspace);
                var actor = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
                var source = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

                try
                {
                    var console = entMan.GetComponent<RMCCameraComputerComponent>(consoleUid);
                    Assert.That(networks.GrantNetwork(consoleUid, NetworkB, source), Is.True);
                    var editor = rmc.BuildEditorState((consoleUid, console));
                    var networkB = networks.ResolveNetwork(NetworkB);

                    Assert.Multiple(() =>
                    {
                        Assert.That(networks.GetEffectiveNetworkEntities(consoleUid), Does.Contain(networkB));
                        Assert.That(editor.Networks.Select(network => network.Id),
                            Does.Not.Contain(entMan.GetNetEntity(networkB)));
                        Assert.That(rmc.TryRenameEditorNetwork(
                            (consoleUid, console), actor, editor.Revision, networkB, "No", out var renameError), Is.False);
                        Assert.That(renameError, Is.EqualTo(RMCCameraNetworkEditorError.InvalidNetwork));
                        Assert.That(rmc.TryDeleteEditorNetwork(
                            (consoleUid, console), actor, editor.Revision, networkB, out var deleteError), Is.False);
                        Assert.That(deleteError, Is.EqualTo(RMCCameraNetworkEditorError.InvalidNetwork));
                        Assert.That(rmc.TrySetSeededNetworkHidden(
                            (consoleUid, console), actor, editor.Revision, networkB, true, out var hideError), Is.False);
                        Assert.That(hideError, Is.EqualTo(RMCCameraNetworkEditorError.InvalidNetwork));
                    });
                }
                finally
                {
                    entMan.DeleteEntity(consoleUid);
                    entMan.DeleteEntity(actor);
                    entMan.DeleteEntity(source);
                }
            });
        }
        finally
        {
            server.Dispose();
        }
    }

    [Test]
    public async Task RmcEditorPreservesHiddenForeignAndGrantedMemberships()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);
        try
        {
            await LoadPrototypes(server);
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var rmc = entMan.System<RMCCameraSystem>();
                var networks = entMan.System<CameraNetworkSystem>();
                var firstUid = entMan.SpawnEntity("CMUTestDualNetworkRmcComputer", MapCoordinates.Nullspace);
                var secondUid = entMan.SpawnEntity("CMUTestDualNetworkRmcComputer", MapCoordinates.Nullspace);
                var camera = entMan.SpawnEntity("CMUTestEditableRmcCameraA", MapCoordinates.Nullspace);
                var actor = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

                try
                {
                    var first = entMan.GetComponent<RMCCameraComputerComponent>(firstUid);
                    var second = entMan.GetComponent<RMCCameraComputerComponent>(secondUid);
                    Assert.That(rmc.TryCreateEditorNetwork((firstUid, first), actor, 0, "First owned", out _), Is.True);
                    var firstOwned = rmc.BuildEditorState((firstUid, first)).Networks
                        .Single(network => network.Origin == RMCCameraNetworkEditorOrigin.Owned).Id;
                    var firstOwnedUid = entMan.GetEntity(firstOwned);
                    var networkA = networks.ResolveNetwork(NetworkA);
                    var networkB = networks.ResolveNetwork(NetworkB);
                    Assert.That(rmc.TrySetSeededNetworkHidden((firstUid, first), actor, 1, networkB, true, out _), Is.True);
                    Assert.That(rmc.TryCreateEditorNetwork((secondUid, second), actor, 0, "Foreign owned", out _), Is.True);
                    var foreign = rmc.BuildEditorState((secondUid, second)).Networks
                        .Single(network => network.Origin == RMCCameraNetworkEditorOrigin.Owned).Id;
                    var foreignUid = entMan.GetEntity(foreign);
                    Assert.That(networks.SetMemberNetworkEntities(camera, [networkA, networkB, foreignUid]), Is.True);

                    Assert.That(rmc.TrySaveEditorCamera((firstUid, first), actor, 2, entMan.GetNetEntity(camera),
                        "Preserved", [firstOwnedUid], out _), Is.True);

                    var member = entMan.GetComponent<CameraNetworkMemberComponent>(camera);
                    Assert.Multiple(() =>
                    {
                        Assert.That(member.Networks, Is.EquivalentTo(new[] { (ProtoId<CameraNetworkPrototype>) NetworkB }));
                        Assert.That(member.RuntimeNetworks, Is.EquivalentTo(new[] { foreignUid, firstOwnedUid }));
                    });
                }
                finally
                {
                    entMan.DeleteEntity(firstUid);
                    entMan.DeleteEntity(secondUid);
                    entMan.DeleteEntity(camera);
                    entMan.DeleteEntity(actor);
                }
            });
        }
        finally
        {
            server.Dispose();
        }
    }

    [Test]
    public async Task RmcEditorRequiresActorSpecificOpenUiAndConsoleAccess()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);
        try
        {
            await LoadPrototypes(server);
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var ui = entMan.System<SharedUserInterfaceSystem>();
                var rmc = entMan.System<RMCCameraSystem>();
                var consoleUid = entMan.SpawnEntity("CMUTestEditableRmcBuiComputer", MapCoordinates.Nullspace);
                var secureUid = entMan.SpawnEntity("CMUTestSecureEditableRmcBuiComputer", MapCoordinates.Nullspace);
                var openedActor = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
                var otherActor = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

                try
                {
                    Assert.That(ui.TryOpenUi(consoleUid, RMCCameraUiKey.Key, openedActor), Is.True);
                    Assert.That(ui.TryOpenUi(secureUid, RMCCameraUiKey.Key, openedActor), Is.True);

                    entMan.EventBus.RaiseLocalEvent(consoleUid,
                        new RMCCameraNetworkEditorCreateBuiMsg(0, "Forged other actor")
                            { Actor = otherActor, UiKey = RMCCameraUiKey.Key });
                    Assert.That(rmc.BuildEditorState((consoleUid,
                        entMan.GetComponent<RMCCameraComputerComponent>(consoleUid))).Revision, Is.Zero);

                    entMan.EventBus.RaiseLocalEvent(secureUid,
                        new RMCCameraNetworkEditorCreateBuiMsg(0, "No access")
                            { Actor = openedActor, UiKey = RMCCameraUiKey.Key });
                    Assert.That(rmc.BuildEditorState((secureUid,
                        entMan.GetComponent<RMCCameraComputerComponent>(secureUid))).Revision, Is.Zero);

                    var console = entMan.GetComponent<RMCCameraComputerComponent>(consoleUid);
                    Assert.That(rmc.TryCreateEditorNetwork(
                        (consoleUid, console), openedActor, 0, "Accepted", out _), Is.True);
                    Assert.That(rmc.BuildEditorState((consoleUid,
                        entMan.GetComponent<RMCCameraComputerComponent>(consoleUid))).Revision, Is.EqualTo(1));
                }
                finally
                {
                    entMan.DeleteEntity(consoleUid);
                    entMan.DeleteEntity(secureUid);
                    entMan.DeleteEntity(openedActor);
                    entMan.DeleteEntity(otherActor);
                }
            });
        }
        finally { server.Dispose(); }
    }

    [Test]
    public async Task RmcEditorStaleCommandCannotPartiallyRenameOrReassign()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);
        try
        {
            await LoadPrototypes(server);
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var rmc = entMan.System<RMCCameraSystem>();
                var consoleUid = entMan.SpawnEntity("CMUTestDualNetworkRmcComputer", MapCoordinates.Nullspace);
                var camera = entMan.SpawnEntity("CMUTestEditableRmcCameraA", MapCoordinates.Nullspace);
                var actor = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
                try
                {
                    var console = entMan.GetComponent<RMCCameraComputerComponent>(consoleUid);
                    var networkB = entMan.System<CameraNetworkSystem>().ResolveNetwork(NetworkB);
                    Assert.That(rmc.TryCreateEditorNetwork((consoleUid, console), actor, 0, "Advance", out _), Is.True);
                    var beforeName = entMan.GetComponent<RMCCameraComponent>(camera).NameOverride;
                    var beforeNetworks = entMan.GetComponent<CameraNetworkMemberComponent>(camera).Networks.ToHashSet();

                    Assert.That(rmc.TrySaveEditorCamera((consoleUid, console), actor, 0, entMan.GetNetEntity(camera),
                        "Must not apply", [networkB], out var error), Is.False);
                    Assert.Multiple(() =>
                    {
                        Assert.That(error, Is.EqualTo(RMCCameraNetworkEditorError.StaleRevision));
                        Assert.That(entMan.GetComponent<RMCCameraComponent>(camera).NameOverride, Is.EqualTo(beforeName));
                        Assert.That(entMan.GetComponent<CameraNetworkMemberComponent>(camera).Networks,
                            Is.EquivalentTo(beforeNetworks));
                    });
                }
                finally
                {
                    entMan.DeleteEntity(consoleUid);
                    entMan.DeleteEntity(camera);
                    entMan.DeleteEntity(actor);
                }
            });
        }
        finally { server.Dispose(); }
    }

    [Test]
    public async Task StandardMonitorMapDoesNotReplicateUnrelatedRemoteEntities()
    {
        // dirty: toggling pvs leaves a stale send list behind that the next pvs test on this pair would trip over
        var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var monitor = EntityUid.Invalid;
        var camera = EntityUid.Invalid;
        var viewer = EntityUid.Invalid;
        var consoleGrid = EntityUid.Invalid;
        var remoteGrid = EntityUid.Invalid;
        var remoteProbe = EntityUid.Invalid;
        var remoteProbeNet = NetEntity.Invalid;
        EntityUid? previousAttached = null;
        try
        {
            if (!server.ProtoMan.HasIndex<CameraNetworkPrototype>(NetworkA))
                await LoadPrototypes(server);
            await server.WaitPost(() => server.CfgMan.SetCVar(CVars.NetPVS, true));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var mapSystem = entMan.System<SharedMapSystem>();
                var monitors = entMan.System<SurveillanceCameraMonitorSystem>();
                var session = server.PlayerMan.Sessions.Single();
                previousAttached = session.AttachedEntity;
                mapSystem.CreateMap(out var consoleMapId);
                mapSystem.CreateMap(out var remoteMapId);
                var consoleGridEntity = mapSystem.CreateGridEntity(consoleMapId);
                var remoteGridEntity = mapSystem.CreateGridEntity(remoteMapId);
                consoleGrid = consoleGridEntity.Owner;
                remoteGrid = remoteGridEntity.Owner;
                mapSystem.SetTile(consoleGridEntity.Owner, consoleGridEntity.Comp, Vector2i.Zero, new Tile(1));
                mapSystem.SetTile(remoteGridEntity.Owner, remoteGridEntity.Comp, Vector2i.Zero, new Tile(1));

                var navMap = entMan.AddComponent<NavMapComponent>(remoteGrid);
                var chunk = new NavMapChunk(Vector2i.Zero);
                chunk.TileData[0] = SharedNavMapSystem.FloorMask;
                navMap.Chunks[Vector2i.Zero] = chunk;

                monitor = entMan.SpawnEntity("CMUTestSurveillanceMonitor",
                    new EntityCoordinates(consoleGrid, Vector2.Zero));
                camera = entMan.SpawnEntity("CMUTestSurveillanceCameraStandard",
                    new EntityCoordinates(remoteGrid, Vector2.Zero));
                remoteProbe = entMan.SpawnEntity(null, new EntityCoordinates(remoteGrid, new Vector2(4, 4)));
                viewer = entMan.SpawnEntity(null, new EntityCoordinates(consoleGrid, Vector2.Zero));
                entMan.AddComponent<CameraMapMarkerComponent>(camera);
                server.PlayerMan.SetAttachedEntity(session, viewer);
                remoteProbeNet = entMan.GetNetEntity(remoteProbe);
            });

            await client.WaitAssertion(() =>
            {
                Assert.That(client.EntMan.TryGetEntity(remoteProbeNet, out _), Is.False);
            });

            await server.WaitAssertion(() =>
            {
                var monitors = server.EntMan.System<SurveillanceCameraMonitorSystem>();
                monitors.AfterOpenUserInterface(monitor, viewer);
            });

            await pair.RunTicksSync(10);

            await client.WaitAssertion(() =>
            {
                Assert.That(client.EntMan.TryGetEntity(remoteProbeNet, out _), Is.False);
            });
        }
        finally
        {
            await server.WaitPost(() => server.CfgMan.SetCVar(CVars.NetPVS, false));
            await server.WaitAssertion(() =>
            {
                var session = server.PlayerMan.Sessions.Single();
                server.PlayerMan.SetAttachedEntity(session, previousAttached);
                foreach (var uid in new[] { viewer, remoteProbe, camera, monitor, consoleGrid, remoteGrid })
                {
                    if (server.EntMan.EntityExists(uid))
                        server.EntMan.DeleteEntity(uid);
                }
            });
            await pair.CleanReturnAsync();
        }
    }

    [Test]
    public async Task RmcMonitorSelectionUsesPrivateCameraOnlyLease()
    {
        var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        try
        {
            if (!server.ProtoMan.HasIndex<CameraNetworkPrototype>(NetworkA))
                await LoadPrototypes(server);
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var mapSystem = entMan.System<SharedMapSystem>();
                var rmc = entMan.System<RMCCameraSystem>();
                var cameraSessions = entMan.System<CameraSessionSystem>();
                var userInterface = entMan.System<SharedUserInterfaceSystem>();
                var playerSession = server.PlayerMan.Sessions.Single();
                var previousAttached = playerSession.AttachedEntity;
                mapSystem.CreateMap(out var mapId);
                var consoleGrid = mapSystem.CreateGridEntity(mapId);
                var remoteGrid = mapSystem.CreateGridEntity(mapId);
                entMan.System<SharedTransformSystem>().SetLocalPosition(remoteGrid, new Vector2(50, 50));
                mapSystem.SetTile(consoleGrid.Owner, consoleGrid.Comp, Vector2i.Zero, new Tile(1));
                mapSystem.SetTile(remoteGrid.Owner, remoteGrid.Comp, Vector2i.Zero, new Tile(1));
                var monitor = entMan.SpawnEntity("CMUTestEditableRmcBuiComputer",
                    new EntityCoordinates(consoleGrid.Owner, Vector2.Zero));
                var camera = entMan.SpawnEntity("CMUTestRmcShipCamera",
                    new EntityCoordinates(remoteGrid.Owner, Vector2.Zero));
                var viewer = entMan.SpawnEntity(null, new EntityCoordinates(consoleGrid.Owner, Vector2.Zero));

                try
                {
                    entMan.AddComponent<CameraMapMarkerComponent>(camera);
                    server.PlayerMan.SetAttachedEntity(playerSession, viewer);

                    userInterface.OpenUi(monitor, RMCCameraUiKey.Key, viewer);
                    Assert.That(userInterface.IsUiOpen(monitor, RMCCameraUiKey.Key, viewer), Is.True);

                    Assert.That(cameraSessions.TryGetSession(playerSession, monitor, out var cameraSession), Is.True);
                    Assert.That(rmc.TrySelectCameraFor(
                        (monitor, entMan.GetComponent<RMCCameraComputerComponent>(monitor)), viewer, camera), Is.True);
                    Assert.Multiple(() =>
                    {
                        Assert.That(cameraSession.SelectedCamera, Is.EqualTo(camera));
                        Assert.That(playerSession.ViewSubscriptions, Does.Contain(camera));
                        Assert.That(playerSession.ViewSubscriptions, Does.Not.Contain(remoteGrid.Owner));
                        Assert.That(cameraSessions.HasActiveViewers(camera), Is.True);
                        Assert.That(cameraSessions.GetSessionsForCamera(camera).Single(), Is.SameAs(cameraSession));
                    });

                    entMan.EventBus.RaiseLocalEvent(monitor,
                        new BoundUIClosedEvent(RMCCameraUiKey.Key, monitor, viewer));
                    server.RunTicks(1);
                    Assert.Multiple(() =>
                    {
                        Assert.That(cameraSessions.TryGetSession(playerSession, monitor, out _), Is.False);
                        Assert.That(playerSession.ViewSubscriptions, Does.Not.Contain(camera));
                        Assert.That(playerSession.ViewSubscriptions, Does.Not.Contain(remoteGrid.Owner));
                        Assert.That(cameraSessions.HasActiveViewers(camera), Is.False);
                    });
                }
                finally
                {
                    server.PlayerMan.SetAttachedEntity(playerSession, previousAttached);
                    entMan.DeleteEntity(viewer);
                    entMan.DeleteEntity(camera);
                    entMan.DeleteEntity(monitor);
                    entMan.DeleteEntity(consoleGrid);
                    entMan.DeleteEntity(remoteGrid);
                }
            });
        }
        finally { await pair.CleanReturnAsync(); }
    }

    [Test]
    public async Task StandardMonitorSelectionUsesPrivateCameraOnlyLease()
    {
        var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        try
        {
            if (!server.ProtoMan.HasIndex<CameraNetworkPrototype>(NetworkA))
                await LoadPrototypes(server);
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var mapSystem = entMan.System<SharedMapSystem>();
                var monitors = entMan.System<SurveillanceCameraMonitorSystem>();
                var cameraSessions = entMan.System<CameraSessionSystem>();
                var cameraNetworks = entMan.System<CameraNetworkSystem>();
                var microphones = entMan.System<SurveillanceCameraMicrophoneSystem>();
                var appearances = entMan.System<SharedAppearanceSystem>();
                var userInterface = entMan.System<SharedUserInterfaceSystem>();
                var playerSession = server.PlayerMan.Sessions.Single();
                var previousAttached = playerSession.AttachedEntity;
                mapSystem.CreateMap(out var mapId);
                var consoleGrid = mapSystem.CreateGridEntity(mapId);
                var remoteGrid = mapSystem.CreateGridEntity(mapId);
                entMan.System<SharedTransformSystem>().SetLocalPosition(remoteGrid, new Vector2(50, 50));
                mapSystem.SetTile(consoleGrid.Owner, consoleGrid.Comp, Vector2i.Zero, new Tile(1));
                mapSystem.SetTile(remoteGrid.Owner, remoteGrid.Comp, Vector2i.Zero, new Tile(1));
                var monitor = entMan.SpawnEntity("CMUTestSurveillanceMonitor",
                    new EntityCoordinates(consoleGrid.Owner, Vector2.Zero));
                var camera = entMan.SpawnEntity("CMUTestSurveillanceCameraStandard",
                    new EntityCoordinates(remoteGrid.Owner, Vector2.Zero));
                var viewer = entMan.SpawnEntity(null, new EntityCoordinates(consoleGrid.Owner, Vector2.Zero));
                var speaker = entMan.SpawnEntity(null, new EntityCoordinates(remoteGrid.Owner, Vector2.Zero));

                try
                {
                    entMan.AddComponent<CameraMapMarkerComponent>(camera);
                    cameraNetworks.Update(0f);
                    var microphone = entMan.AddComponent<SurveillanceCameraMicrophoneComponent>(camera);
                    var speechProbe = entMan.AddComponent<CameraSpeechProbeComponent>(monitor);
                    server.PlayerMan.SetAttachedEntity(playerSession, viewer);
                    userInterface.OpenUi(monitor, SurveillanceCameraMonitorUiKey.Key, viewer);
                    Assert.That(userInterface.IsUiOpen(
                        monitor,
                        SurveillanceCameraMonitorUiKey.Key,
                        viewer), Is.True);
                    monitors.AfterOpenUserInterface(monitor, viewer);

                    Assert.That(cameraSessions.TryGetSession(playerSession, monitor, out var cameraSession), Is.True);
                    Assert.That(cameraSessions.SelectCamera(cameraSession.Id, camera), Is.True);
                    var revisionBeforeMovement = cameraSession.Revision;
                    entMan.System<SharedTransformSystem>().SetLocalPosition(camera, Vector2.One);
                    cameraNetworks.Update(0f);
                    Assert.That(appearances.TryGetData(camera, SurveillanceCameraVisualsKey.Key,
                        out SurveillanceCameraVisuals selectedVisual), Is.True);

                    Assert.Multiple(() =>
                    {
                        Assert.That(cameraSession.SelectedCamera, Is.EqualTo(camera));
                        Assert.That(playerSession.ViewSubscriptions, Does.Contain(camera));
                        Assert.That(playerSession.ViewSubscriptions, Does.Not.Contain(remoteGrid.Owner));
                        Assert.That(cameraSessions.HasActiveViewers(camera), Is.True);
                        Assert.That(cameraSessions.GetSessionsForCamera(camera).Single(), Is.SameAs(cameraSession));
                        Assert.That(cameraSession.Revision, Is.EqualTo(revisionBeforeMovement));
                        Assert.That(entMan.HasComponent<ActiveSurveillanceCameraMonitorComponent>(monitor), Is.True);
                        Assert.That(selectedVisual, Is.EqualTo(SurveillanceCameraVisuals.InUse));
                    });

                    microphones.RelayEntityMessage(camera, microphone, new ListenEvent("before close", speaker));
                    Assert.Multiple(() =>
                    {
                        Assert.That(speechProbe.Events, Is.EqualTo(1));
                        Assert.That(speechProbe.Speaker, Is.EqualTo(speaker));
                        Assert.That(speechProbe.Message, Is.EqualTo("before close"));
                    });

                    entMan.EventBus.RaiseLocalEvent(monitor,
                        new BoundUIClosedEvent(SurveillanceCameraMonitorUiKey.Key, monitor, viewer));

                    microphones.RelayEntityMessage(camera, microphone, new ListenEvent("after close", speaker));
                    Assert.That(appearances.TryGetData(camera, SurveillanceCameraVisualsKey.Key,
                        out SurveillanceCameraVisuals closedVisual), Is.True);

                    Assert.Multiple(() =>
                    {
                        Assert.That(cameraSessions.TryGetSession(playerSession, monitor, out _), Is.False);
                        Assert.That(playerSession.ViewSubscriptions, Does.Not.Contain(camera));
                        Assert.That(cameraSessions.HasActiveViewers(camera), Is.False);
                        Assert.That(entMan.HasComponent<ActiveSurveillanceCameraMonitorComponent>(monitor), Is.False);
                        Assert.That(speechProbe.Events, Is.EqualTo(1));
                        Assert.That(closedVisual, Is.EqualTo(SurveillanceCameraVisuals.Active));
                    });
                }
                finally
                {
                    server.PlayerMan.SetAttachedEntity(playerSession, previousAttached);
                    entMan.DeleteEntity(viewer);
                    entMan.DeleteEntity(speaker);
                    entMan.DeleteEntity(camera);
                    entMan.DeleteEntity(monitor);
                    entMan.DeleteEntity(consoleGrid);
                    entMan.DeleteEntity(remoteGrid);
                }
            });
        }
        finally { await pair.CleanReturnAsync(); }
    }

    [TestPrototypes]
    private const string Prototypes = """
        - type: cameraNetwork
          id: CMUTestCameraNetworkA
          name: cmu-test-camera-network-a
          configurable: true

        - type: cameraNetwork
          id: CMUTestCameraNetworkB
          name: cmu-test-camera-network-b
          configurable: false

        - type: entity
          id: CMUTestCameraStandard
          components:
          - type: CameraNetworkMember
            networks: [CMUTestCameraNetworkA, CMUTestCameraNetworkB]
            sourceKinds: Standard

        - type: entity
          id: CMUTestCameraRmc
          components:
          - type: CameraNetworkMember
            networks: [CMUTestCameraNetworkA]
            sourceKinds: Rmc

        - type: entity
          id: CMUTestCameraStandardA
          components:
          - type: CameraNetworkMember
            networks: [CMUTestCameraNetworkA]
            sourceKinds: Standard

        - type: entity
          id: CMUTestCameraStandardB
          components:
          - type: CameraNetworkMember
            networks: [CMUTestCameraNetworkB]
            sourceKinds: Standard

        - type: entity
          id: CMUTestCameraReceiver
          components:
          - type: CameraNetworkReceiver
            networks: [CMUTestCameraNetworkA]
            supportedSources: Standard

        - type: entity
          id: CMUTestCameraDynamicReceiver
          components:
          - type: CameraNetworkReceiver
            supportedSources: Standard

        - type: entity
          id: CMUTestSurveillanceMonitor
          components:
          - type: CameraNetworkReceiver
            networks: [CMUTestCameraNetworkA]
            supportedSources: Standard
          - type: SurveillanceCameraMonitor
          - type: Eye
          - type: UserInterface
            interfaces:
              enum.SurveillanceCameraMonitorUiKey.Key:
                type: SurveillanceCameraMonitorBoundUserInterface

        - type: entity
          id: CMUTestSurveillanceCameraStandard
          name: CMU Test Surveillance Camera
          components:
          - type: SurveillanceCamera
          - type: Appearance
          - type: CameraNetworkMember
            networks: [CMUTestCameraNetworkA]
            sourceKinds: Standard

        - type: entity
          id: CMUTestRmcMapComputer
          components:
          - type: RMCCameraComputer
          - type: CameraNetworkReceiver
            networks: [CMUTestCameraNetworkA]
            supportedSources: Rmc

        - type: entity
          id: CMUTestDualNetworkRmcComputer
          components:
          - type: RMCCameraComputer
          - type: CameraNetworkReceiver
            networks: [CMUTestCameraNetworkA, CMUTestCameraNetworkB]
            supportedSources: Rmc

        - type: entity
          id: CMUTestEditableRmcBuiComputer
          components:
          - type: UserInterface
            interfaces:
              enum.RMCCameraUiKey.Key:
                type: RMCCameraBui
                interactionRange: 0
          - type: RMCCameraComputer
          - type: CameraNetworkReceiver
            networks: [CMUTestCameraNetworkA, CMUTestCameraNetworkB]
            supportedSources: Rmc

        - type: entity
          parent: CMUTestEditableRmcBuiComputer
          id: CMUTestSecureEditableRmcBuiComputer
          components:
          - type: AccessReader
            access:
            - [Engineering]

        - type: entity
          id: CMUTestRmcShipCamera
          name: CMU Test RMC Ship Camera
          components:
          - type: RMCCamera
          - type: CameraNetworkMember
            networks: [CMUTestCameraNetworkA]
            sourceKinds: Rmc

        - type: entity
          id: CMUTestEditableRmcCameraA
          name: Editable RMC camera A
          components:
          - type: RMCCamera
            nameOverride: Old camera A
          - type: SurveillanceCamera
          - type: CameraNetworkMember
            networks: [CMUTestCameraNetworkA]
            sourceKinds: Rmc
          - type: CameraMapMarker
          - type: Eye

        """;

    [Test]
    public async Task ClosingCameraPanelClosesSetupUiAndRejectsStaleNetworkRequest()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);
        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var wires = entMan.System<WiresSystem>();
                var ui = entMan.System<UserInterfaceSystem>();
                var camera = entMan.SpawnEntity("CMUSurveillanceCameraColonyCMB", MapCoordinates.Nullspace);
                var user = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
                var component = entMan.GetComponent<SurveillanceCameraComponent>(camera);
                var panel = entMan.GetComponent<WiresPanelComponent>(camera);

                try
                {
                    Assert.That(wires.TogglePanel(camera, panel, true, user), Is.True);

                    var verbs = new GetVerbsEvent<AlternativeVerb>(user, camera, null, null, true, true, true, []);
                    entMan.EventBus.RaiseLocalEvent(camera, verbs, true);
                    verbs.Verbs.Single(verb => verb.Text == Loc.GetString("surveillance-camera-setup")).Act!();
                    Assert.That(ui.IsUiOpen(camera, SurveillanceCameraSetupUiKey.Camera, user), Is.True);

                    Assert.That(wires.TogglePanel(camera, panel, false, user), Is.True);
                    entMan.EventBus.RaiseLocalEvent(camera, new SurveillanceCameraSetupSetNetwork(1)
                    {
                        UiKey = SurveillanceCameraSetupUiKey.Camera,
                    });
                    entMan.EventBus.RaiseLocalEvent(camera, new SurveillanceCameraSetupSetName("stale-panel-name")
                    {
                        UiKey = SurveillanceCameraSetupUiKey.Camera,
                    });

                    Assert.Multiple(() =>
                    {
                        Assert.That(ui.IsUiOpen(camera, SurveillanceCameraSetupUiKey.Camera, user), Is.False);
                        Assert.That(component.NameSet, Is.False);
                        Assert.That(component.NetworkSet, Is.False);
                    });
                }
                finally
                {
                    entMan.DeleteEntity(user);
                    entMan.DeleteEntity(camera);
                }
            });
        }
        finally { server.Dispose(); }
    }

    [Test]
    public async Task MapStateContainsOnlyAuthorizedVisibleMarkersAcrossGrids()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);

        try
        {
            await LoadPrototypes(server);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var mapSystem = entMan.System<SharedMapSystem>();
                var networks = entMan.System<CameraNetworkSystem>();
                mapSystem.CreateMap(out var mapId);
                var firstGridEntity = mapSystem.CreateGridEntity(mapId);
                var secondGridEntity = mapSystem.CreateGridEntity(mapId);
                var firstGrid = firstGridEntity.Owner;
                var secondGrid = secondGridEntity.Owner;
                entMan.System<SharedTransformSystem>().SetLocalPosition(secondGrid, new Vector2(50, 50));
                var firstTiles = new List<(Vector2i GridIndices, Tile Tile)>
                {
                    (Vector2i.Zero, new Tile(1)),
                    (new Vector2i(1, 0), new Tile(1)),
                    (Vector2i.One, new Tile(1)),
                };

                for (var y = 1; y <= 3; y++)
                {
                    firstTiles.Add((new Vector2i(2, y), new Tile(1)));
                }

                for (var x = 3; x <= 6; x++)
                {
                    firstTiles.Add((new Vector2i(x, 3), new Tile(1)));
                }

                for (var y = 4; y <= 7; y++)
                {
                    firstTiles.Add((new Vector2i(6, y), new Tile(1)));
                }

                var secondTiles = new List<(Vector2i GridIndices, Tile Tile)>();
                for (var x = -4; x <= 1; x++)
                {
                    secondTiles.Add((new Vector2i(x, 0), new Tile(1)));
                }

                for (var y = 1; y <= 5; y++)
                {
                    secondTiles.Add((new Vector2i(-4, y), new Tile(1)));
                }

                secondTiles.Add((Vector2i.One, new Tile(1)));
                mapSystem.SetTiles(firstGridEntity.Owner, firstGridEntity.Comp, firstTiles);
                mapSystem.SetTiles(secondGridEntity.Owner, secondGridEntity.Comp, secondTiles);
                var receiver = entMan.SpawnEntity("CMUTestCameraReceiver", new EntityCoordinates(firstGrid, Vector2.Zero));
                var firstCamera = entMan.SpawnEntity("CMUTestCameraStandardA", new EntityCoordinates(firstGrid, new Vector2(2, 3)));
                var secondCamera = entMan.SpawnEntity("CMUTestCameraStandardA", new EntityCoordinates(secondGrid, new Vector2(-4, 5)));
                var unmarkedCamera = entMan.SpawnEntity("CMUTestCameraStandardA", new EntityCoordinates(firstGrid, Vector2.One));
                var hiddenCamera = entMan.SpawnEntity("CMUTestCameraStandardA", new EntityCoordinates(firstGrid, new Vector2(6, 7)));
                var otherNetworkCamera = entMan.SpawnEntity("CMUTestCameraStandardB", new EntityCoordinates(secondGrid, Vector2.One));

                try
                {
                    entMan.AddComponent<CameraMapMarkerComponent>(firstCamera);
                    entMan.AddComponent<CameraMapMarkerComponent>(secondCamera);
                    entMan.AddComponent(hiddenCamera, new CameraMapMarkerComponent { Visible = false });
                    entMan.AddComponent<CameraMapMarkerComponent>(otherNetworkCamera);

                    var state = networks.BuildMapState(receiver);

                    Assert.Multiple(() =>
                    {
                        Assert.That(state.ConsoleGrid, Is.EqualTo(entMan.GetNetEntity(firstGrid)));
                        Assert.That(state.Grids, Has.Count.EqualTo(2));
                        Assert.That(state.Grids[0].Grid, Is.EqualTo(entMan.GetNetEntity(firstGrid)));
                        Assert.That(state.Grids.SelectMany(grid => grid.Markers).Select(marker => marker.Camera),
                            Is.EquivalentTo(new[] { entMan.GetNetEntity(firstCamera), entMan.GetNetEntity(secondCamera) }));
                        Assert.That(state.Grids.Single(grid => grid.Grid == entMan.GetNetEntity(firstGrid)).Markers.Single().Position,
                            Is.EqualTo(new Vector2(2, 3)));
                        Assert.That(state.Grids.Single(grid => grid.Grid == entMan.GetNetEntity(secondGrid)).Markers.Single().Position,
                            Is.EqualTo(new Vector2(-4, 5)));
                    });
                }
                finally
                {
                    entMan.DeleteEntity(receiver);
                    entMan.DeleteEntity(firstCamera);
                    entMan.DeleteEntity(secondCamera);
                    entMan.DeleteEntity(unmarkedCamera);
                    entMan.DeleteEntity(hiddenCamera);
                    entMan.DeleteEntity(otherNetworkCamera);
                    entMan.DeleteEntity(firstGrid);
                    entMan.DeleteEntity(secondGrid);
                }
            });
        }
        finally
        {
            server.Dispose();
        }
    }

    [Test]
    public async Task MemberRegistrationAndSourceKindGateAccess()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);

        try
        {
            await LoadPrototypes(server);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var networks = entMan.System<CameraNetworkSystem>();
                var receiver = entMan.SpawnEntity("CMUTestCameraReceiver", MapCoordinates.Nullspace);
                var standardCamera = entMan.SpawnEntity("CMUTestCameraStandard", MapCoordinates.Nullspace);
                var rmcCamera = entMan.SpawnEntity("CMUTestCameraRmc", MapCoordinates.Nullspace);

                try
                {
                    Assert.That(networks.CanAccess(receiver, standardCamera), Is.True);
                    Assert.That(networks.CanAccess(receiver, rmcCamera), Is.False);
                    Assert.That(
                        networks.GetAccessibleCameras(new Entity<CameraNetworkReceiverComponent>(
                            receiver,
                            entMan.GetComponent<CameraNetworkReceiverComponent>(receiver))),
                        Is.EquivalentTo(new[] { standardCamera }));

                    networks.RemoveNetwork(standardCamera, NetworkA);
                    Assert.That(networks.CanAccess(receiver, standardCamera), Is.False);
                    Assert.That(
                        networks.GetAccessibleCameras(new Entity<CameraNetworkReceiverComponent>(
                            receiver,
                            entMan.GetComponent<CameraNetworkReceiverComponent>(receiver))),
                        Does.Not.Contain(standardCamera));
                }
                finally
                {
                    entMan.DeleteEntity(receiver);
                    entMan.DeleteEntity(standardCamera);
                    entMan.DeleteEntity(rmcCamera);
                }
            });
        }
        finally
        {
            server.Dispose();
        }
    }

    [Test]
    public async Task RuntimeGrantsAreSourceAwareAndReferenceCounted()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);

        try
        {
            await LoadPrototypes(server);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var networks = entMan.System<CameraNetworkSystem>();
                var receiver = entMan.SpawnEntity("CMUTestCameraDynamicReceiver", MapCoordinates.Nullspace);
                var camera = entMan.SpawnEntity("CMUTestCameraStandard", MapCoordinates.Nullspace);
                var firstSource = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
                var secondSource = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

                try
                {
                    Assert.That(networks.CanAccess(receiver, camera), Is.False);
                    Assert.That(networks.GrantNetwork(receiver, NetworkA, firstSource), Is.True);
                    Assert.That(networks.GrantNetwork(receiver, NetworkA, secondSource), Is.True);
                    Assert.That(networks.CanAccess(receiver, camera), Is.True);

                    Assert.That(networks.RevokeNetwork(receiver, NetworkA, firstSource), Is.True);
                    Assert.That(networks.CanAccess(receiver, camera), Is.True);

                    Assert.That(networks.RevokeNetwork(receiver, NetworkA, secondSource), Is.True);
                    Assert.That(networks.CanAccess(receiver, camera), Is.False);
                }
                finally
                {
                    entMan.DeleteEntity(receiver);
                    entMan.DeleteEntity(camera);
                    entMan.DeleteEntity(firstSource);
                    entMan.DeleteEntity(secondSource);
                }
            });
        }
        finally
        {
            server.Dispose();
        }
    }

    [Test]
    public async Task DeletingGrantSourceRevokesOnlyItsGrant()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);

        try
        {
            await LoadPrototypes(server);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var networks = entMan.System<CameraNetworkSystem>();
                var receiver = entMan.SpawnEntity("CMUTestCameraDynamicReceiver", MapCoordinates.Nullspace);
                var cameraA = entMan.SpawnEntity("CMUTestCameraStandardA", MapCoordinates.Nullspace);
                var cameraB = entMan.SpawnEntity("CMUTestCameraStandardB", MapCoordinates.Nullspace);
                var sourceA = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
                var sourceB = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

                try
                {
                    Assert.That(networks.GrantNetwork(receiver, NetworkA, sourceA), Is.True);
                    Assert.That(networks.GrantNetwork(receiver, NetworkB, sourceB), Is.True);
                    Assert.That(networks.CanAccess(receiver, cameraA), Is.True);
                    Assert.That(networks.CanAccess(receiver, cameraB), Is.True);

                    entMan.DeleteEntity(sourceA);

                    Assert.Multiple(() =>
                    {
                        Assert.That(networks.CanAccess(receiver, cameraA), Is.False);
                        Assert.That(networks.CanAccess(receiver, cameraB), Is.True);
                        Assert.That(networks.GetEffectiveNetworks(receiver), Does.Not.Contain(NetworkA));
                        Assert.That(networks.GetEffectiveNetworks(receiver), Does.Contain(NetworkB));
                    });
                }
                finally
                {
                    entMan.DeleteEntity(receiver);
                    entMan.DeleteEntity(cameraA);
                    entMan.DeleteEntity(cameraB);
                    entMan.DeleteEntity(sourceB);
                }
            });
        }
        finally
        {
            server.Dispose();
        }
    }

    private static async Task LoadPrototypes(
        RobustIntegrationTest.IntegrationInstance server,
        bool enableMap = true,
        bool enableEditor = true)
    {
        server.CfgMan.SetCVar(CCVars.CMUCameraMapEnabled, enableMap);
        server.CfgMan.SetCVar(CCVars.CMUCameraEditorEnabled, enableEditor);

        if (server.ProtoMan.HasIndex<CameraNetworkPrototype>(NetworkA))
            return;

        var changed = new Dictionary<Type, HashSet<string>>();
        server.ProtoMan.LoadString(Prototypes, changed: changed);
        await server.WaitPost(() => server.ProtoMan.ReloadPrototypes(changed));
    }
}



[RegisterComponent]
public sealed partial class CameraSpeechProbeComponent : Component
{
    public int Events;
    public EntityUid? Speaker;
    public string? Message;
}

public sealed class CameraSpeechProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CameraSpeechProbeComponent, SurveillanceCameraSpeechSendEvent>(OnSpeech);
    }

    private void OnSpeech(
        Entity<CameraSpeechProbeComponent> ent,
        ref SurveillanceCameraSpeechSendEvent args)
    {
        ent.Comp.Events++;
        ent.Comp.Speaker = args.Speaker;
        ent.Comp.Message = args.Message;
    }
}
