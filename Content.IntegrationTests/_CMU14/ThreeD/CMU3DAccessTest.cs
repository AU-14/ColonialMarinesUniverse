#nullable enable
using Content.Client.Administration.Managers;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Client.Gameplay;
using Content.IntegrationTests.Fixtures;
using Content.Server.Administration.Managers;
using Content.Server.CMU14.ThreeD;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.Administration;
using Content.Shared.CMU14.ThreeD;
using Content.Shared.Maps;
using Robust.Client.Graphics;
using Robust.Client.State;
using Robust.Shared.Console;

namespace Content.IntegrationTests.CMU14.ThreeD;

[TestFixture]
[TestOf(typeof(CMU3DLiveSceneSystem))]
public sealed class CMU3DAccessTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task PlayerCanToggleReduxViewAndLeavingMapRevokesSubscription()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;
        var original = session.AttachedEntity;
        EntityUid actor = default;
        EntityUid upper = default;
        EntityUid unsupported = default;
        EntityUid root = default;
        try
        {
            await Server.WaitPost(() =>
            {
                root = SComp<TransformComponent>(map.GridCoords.EntityId).MapUid!.Value;
                var maps = Server.System<SharedMapSystem>();
                upper = maps.CreateMap(runMapInit: true);
                unsupported = maps.CreateMap(runMapInit: true);
                var levels = Server.System<CMUZLevelsSystem>();
                Assert.That(levels.TryAddMapsIntoZNetwork(levels.CreateZNetwork(),
                    new() { [root] = 0, [upper] = 1 }), Is.True);
                actor = SSpawnAtPosition("MobHuman", map.GridCoords);
                Server.PlayerMan.SetAttachedEntity(session, actor);
                Server.ResolveDependency<IAdminManager>().DeAdmin(session);
            });
            await RunTicksSync(10);
            await Pair.RunUntilSynced();
            // The dummy ticker does not send the gameplay-screen transition.
            await Client.WaitPost(() => Client.ResolveDependency<IStateManager>().RequestStateChange<GameplayState>());
            await RunTicksSync(1);
            var eyes = Client.ResolveDependency<IEyeManager>();
            var previous = eyes.MainViewport;
            await Client.WaitAssertion(() =>
            {
                var admins = Client.ResolveDependency<IClientAdminManager>();
                Assert.That(admins.HasFlag(AdminFlags.Debug), Is.False);
                Assert.That(admins.CanCommand("cmu3d"), Is.True, "The toggle must be available to ordinary players.");
                Assert.That(Client.System<CMU3DLiveSceneSystem>().ToggleFirstPerson(), Is.False,
                    "Ordinary maps must reject the player toggle.");
            });
            await Server.WaitPost(() =>
            {
                // Apply the same registry that the Redux map loader applies to every floor.
                var components = SProtoMan.Index<GameMapPrototype>("StableGarrisonRedux").ZLevelsComponentOverrides;
                SEntMan.AddComponents(root, components);
                SEntMan.AddComponents(upper, components);
            });
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() =>
            {
                Assert.That(CEntMan.HasComponent<CMU3DMapComponent>(ToClientUid(root)), Is.True,
                    "The map opt-in must reach the client before the toggle can open its view.");
                Client.ResolveDependency<IConsoleHost>().ExecuteCommand("cmu3d");
                Assert.That(eyes.MainViewport, Is.TypeOf<CMU3DSceneControl>());
                Assert.That(Client.System<CMU3DLiveSceneSystem>().IsOpen, Is.True);
                Assert.That(Client.System<CMU3DLiveSceneSystem>().Open(), Is.False,
                    "Player first person must not grant access to the administrator workbench.");
            });
            await RunTicksSync(10);
            await Server.WaitAssertion(() => Assert.That(
                session.ViewSubscriptions.Count(SEntMan.HasComponent<CMU3DViewProbeComponent>), Is.EqualTo(2)));
            await Server.WaitPost(() => Server.System<SharedTransformSystem>()
                .SetCoordinates(actor, new EntityCoordinates(upper, Vector2.Zero)));
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() =>
            {
                var scene = Client.System<CMU3DLiveSceneSystem>();
                scene.FrameUpdate(1);
                Assert.That(scene.IsOpen, Is.True, "Changing to another Redux floor must keep first person active.");
                Client.ResolveDependency<IConsoleHost>().ExecuteCommand("cmu3d");
                Assert.That(scene.IsOpen, Is.False);
                Assert.That(eyes.MainViewport, Is.SameAs(previous), "Toggling off must restore the normal viewport.");
                scene.FrameUpdate(1);
                Assert.That(scene.IsOpen, Is.False, "A saved enabled setting must not reopen the view.");
            });
            await RunTicksSync(10);
            await Server.WaitAssertion(() => Assert.That(
                session.ViewSubscriptions.Any(SEntMan.HasComponent<CMU3DViewProbeComponent>), Is.False));
            await Client.WaitPost(() => Client.ResolveDependency<IConsoleHost>().ExecuteCommand("cmu3d"));
            await RunTicksSync(10);
            await Server.WaitAssertion(() =>
            {
                Assert.That(session.ViewSubscriptions.Any(SEntMan.HasComponent<CMU3DViewProbeComponent>), Is.True);
                Server.System<SharedTransformSystem>().SetCoordinates(actor, new EntityCoordinates(unsupported, Vector2.Zero));
                // Exercise revocation before the client can send a close request.
                Server.System<CMU3DViewSystem>().Update(0);
                Assert.That(session.ViewSubscriptions.Any(SEntMan.HasComponent<CMU3DViewProbeComponent>), Is.False);
            });
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() =>
            {
                var scene = Client.System<CMU3DLiveSceneSystem>();
                scene.FrameUpdate(1);
                Assert.That(scene.IsOpen, Is.False);
                Assert.That(eyes.MainViewport, Is.SameAs(previous));
            });
        }
        finally
        {
            await Client.WaitPost(() => Client.System<CMU3DLiveSceneSystem>().Close());
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, original));
        }
    }
}
