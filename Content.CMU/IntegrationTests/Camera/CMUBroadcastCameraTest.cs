using Content.Server.Camera;
using Content.Server.CMU14.Camera.Television;
using Content.Shared.Camera;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Verbs;
using Content.Shared.Wieldable;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.Camera;

[TestFixture]
public sealed class CMUBroadcastCameraTest
{
    [Test]
    public async Task BroadcastVerbStopsAndRestartsFeedAndUpdatesIndicator()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var entities = server.EntMan;
        var player = pair.Player!;
        var originalActor = player.AttachedEntity;
        var networks = server.System<CameraNetworkSystem>();
        var sessions = server.System<CameraSessionSystem>();
        var televisions = server.System<CMUTelevisionSystem>();
        var verbs = server.System<SharedVerbSystem>();
        EntityUid camera = default;
        EntityUid tv = default;
        EntityUid actor = default;
        NetEntity cameraNet = default;
        CameraViewerSession session = null!;

        try
        {
            await server.WaitAssertion(() =>
            {
                camera = entities.SpawnEntity("RMCCameraBroadcasting", map.GridCoords);
                tv = entities.SpawnEntity("RMCTelevision", map.GridCoords);
                actor = entities.SpawnEntity("CMMobHuman", map.GridCoords);
                cameraNet = entities.GetNetEntity(camera);
                server.PlayerMan.SetAttachedEntity(player, actor);
                Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(actor, camera, animate: false), Is.True);
                Assert.That(server.System<SharedWieldableSystem>().TryWield(camera, actor), Is.True);

                session = sessions.OpenSession(player, actor, tv, CameraSessionCapabilities.LiveView)!;
                Assert.That(session, Is.Not.Null);
                Assert.That(sessions.SelectCamera(session.Id, camera), Is.True);
                televisions.ChannelChanged(tv, session);
                Assert.That(televisions.GetTunedTo(camera), Does.Contain(tv));
            });
            await AssertIndicator("icon", "_RMC14/Objects/Devices/broadcasting_camera.rsi");

            await server.WaitAssertion(() =>
            {
                Execute("cmu-camera-stop-broadcast");
                Assert.That(networks.CanAccess(tv, camera), Is.False);
                Assert.That(session.SelectedCamera, Is.Null);
                Assert.That(player.ViewSubscriptions, Does.Not.Contain(camera));
                Assert.That(televisions.GetTunedTo(camera), Does.Not.Contain(tv));
            });
            await AssertIndicator("icon-off", "_CMU14/Objects/Devices/broadcasting_camera_off.rsi");

            await server.WaitAssertion(() =>
            {
                Execute("cmu-camera-start-broadcast");
                Assert.That(networks.CanAccess(tv, camera), Is.True);
                Assert.That(session.SelectedCamera, Is.EqualTo(camera));
                Assert.That(player.ViewSubscriptions, Does.Contain(camera));
                Assert.That(televisions.GetTunedTo(camera), Does.Contain(tv));
            });
            await AssertIndicator("icon", "_RMC14/Objects/Devices/broadcasting_camera.rsi");
        }
        finally
        {
            await server.WaitPost(() => server.PlayerMan.SetAttachedEntity(player, originalActor));
            await pair.CleanReturnAsync();
        }

        void Execute(string name)
        {
            var verb = verbs.GetLocalVerbs(camera, actor, typeof(Verb))
                .Single(candidate => candidate.Text == Loc.GetString(name));
            verbs.ExecuteVerb(verb, actor, camera);
        }

        async Task AssertIndicator(string icon, string heldRsi)
        {
            await pair.RunUntilSynced();
            var client = pair.Client;
            await client.WaitAssertion(() =>
            {
                var clientCamera = client.EntMan.GetEntity(cameraNet);
                var sprites = client.System<SpriteSystem>();
                client.System<AppearanceSystem>().FrameUpdate(0);
                Assert.That(sprites.LayerMapTryGet(clientCamera, "broadcast", out var layer, false), Is.True);
                Assert.That(sprites.TryGetLayer(clientCamera, layer, out var indicator, false), Is.True);
                Assert.That(indicator!.State.Name, Is.EqualTo(icon));

                var held = new GetInhandVisualsEvent(clientCamera, HandLocation.Left);
                client.EntMan.EventBus.RaiseLocalEvent(clientCamera, held);
                var handLayer = held.Layers.Single().Item2;
                Assert.That(handLayer.State, Is.EqualTo("wielded-inhand-left"));
                Assert.That(handLayer.RsiPath, Does.EndWith(heldRsi));
                var resources = client.ResolveDependency<IResourceCache>();
                var handSprite = resources.GetResource<RSIResource>(new ResPath("/Textures") / handLayer.RsiPath!).RSI;
                Assert.That(handSprite.TryGetState(handLayer.State!, out _), Is.True);
            });
        }
    }
}
