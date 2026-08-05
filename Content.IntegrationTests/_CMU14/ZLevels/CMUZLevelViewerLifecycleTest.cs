using System.Linq;
using System.Numerics;
using Content.Server._CMU14.ZLevels.Core;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._CMU14.ZLevels;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.ZLevels;

[TestFixture]
[NonParallelizable]
public sealed class CMUZLevelViewerLifecycleTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Dirty = true,
    };

    [Test]
    public async Task RuntimeEnableAndDisableReconcileViewerState()
    {
        await OverrideCVar(Side.Server, CMUZLevelsCVars.Enabled, false, sync: false);
        var map = await Pair.CreateTestMap();
        var lifecycle = SEntMan.System<CMUZNetworkLifecycleSystem>();
        var mapSystem = SEntMan.System<SharedMapSystem>();
        var shooting = SEntMan.System<CMUZLevelShootingSystem>();
        var zLevels = SEntMan.System<CMUZLevelsSystem>();
        var playerManager = Server.ResolveDependency<IPlayerManager>();
        var session = playerManager.Sessions.Single();
        EntityUid player = default;
        EntityUid shooterEntity = default;
        EntityUid upperMap = default;
        MapId upperMapId = default;

        await Server.WaitAssertion(() =>
        {
            upperMap = mapSystem.CreateMap(out upperMapId);
            Assert.That(lifecycle.TryCombineLevels([map.MapUid, upperMap], out _, out var error), Is.True, error);

            player = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            shooterEntity = SEntMan.SpawnEntity(
                "MobHuman",
                new EntityCoordinates(upperMap, Vector2.Zero));
            SEntMan.EnsureComponent<CMUZLevelShooterComponent>(shooterEntity);
            zLevels.EnsureZLevelViewer(shooterEntity);
            Assert.That(playerManager.SetAttachedEntity(session, player), Is.True);
            Assert.That(SEntMan.HasComponent<CMUZLevelViewerComponent>(player), Is.False);
            Assert.That(zLevels.SetLookUp(shooterEntity, true), Is.False,
                "Look-up was enabled while Multi-Z was disabled.");
            Assert.That(shooting.SetShootDown(shooterEntity, true), Is.False,
                "Shoot-down was enabled while Multi-Z was disabled.");
        });

        await OverrideCVar(Side.Server, CMUZLevelsCVars.Enabled, true, sync: false);
        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<CMUZLevelViewerComponent>(player), Is.True);
            Assert.That(zLevels.SetLookUp(shooterEntity, true), Is.False,
                "Look-up was enabled without an adjacent upper level.");
            Assert.That(shooting.SetShootDown(player, true), Is.False,
                "Shoot-down was enabled without an adjacent lower level.");

            Assert.That(zLevels.SetLookUp(player, true), Is.True);
            Assert.That(SEntMan.GetComponent<CMUZLevelViewerComponent>(player).LookUp, Is.True);

            Assert.That(shooting.SetShootDown(shooterEntity, true), Is.True);
            Assert.That(SEntMan.GetComponent<CMUZLevelShooterComponent>(shooterEntity).ShootDown, Is.True);
        });
        await Pair.RunTicksSync(2);

        await OverrideCVar(Side.Server, CMUZLevelsCVars.Enabled, false, sync: false);
        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<CMUZLevelViewerComponent>(player).LookUp, Is.False);
                Assert.That(SEntMan.GetComponent<CMUZLevelShooterComponent>(shooterEntity).ShootDown, Is.False);
            });
        });

        await OverrideCVar(Side.Server, CMUZLevelsCVars.Enabled, true, sync: false);
        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(zLevels.SetLookUp(player, true), Is.True,
                "The reconciled player could not re-enable a valid cross-Z view mode.");
        });
        await Pair.RunTicksSync(10);

        await Server.WaitAssertion(() =>
        {
            var viewer = SEntMan.GetComponent<CMUZLevelViewerComponent>(player);
            Assert.That(viewer.Eyes, Is.Not.Empty,
                "Re-enabling Multi-Z did not rebuild the attached player's probe eyes.");
            foreach (var probe in viewer.Eyes)
            {
                Assert.That(SEntMan.GetComponent<TransformComponent>(probe).MapUid, Is.EqualTo(upperMap),
                    $"Rebuilt probe {probe} was not placed on the requested upper level.");
                Assert.That(session.ViewSubscriptions, Does.Contain(probe),
                    $"The attached player was not subscribed to rebuilt probe {probe}.");
            }
        });

        await Server.WaitPost(() =>
        {
            if (mapSystem.MapExists(upperMapId))
                mapSystem.DeleteMap(upperMapId);
        });
    }

    [Test]
    public async Task RemoteViewOpenedWhileDisabledIsReconciledOnEnable()
    {
        await OverrideCVar(Side.Server, CMUZLevelsCVars.Enabled, false, sync: false);
        var lifecycle = SEntMan.System<CMUZNetworkLifecycleSystem>();
        var mapSystem = SEntMan.System<SharedMapSystem>();
        var subscribers = SEntMan.System<ViewSubscriberSystem>();
        var transform = SEntMan.System<SharedTransformSystem>();
        var playerManager = Server.ResolveDependency<IPlayerManager>();
        var session = playerManager.Sessions.Single();
        var maps = new EntityUid[2];
        var mapIds = new MapId[2];
        EntityUid offNetworkMap = default;
        MapId offNetworkMapId = default;
        EntityUid remoteView = default;

        await Server.WaitAssertion(() =>
        {
            for (var i = 0; i < maps.Length; i++)
                maps[i] = mapSystem.CreateMap(out mapIds[i]);

            Assert.That(lifecycle.TryCombineLevels(maps, out _, out var error), Is.True, error);

            offNetworkMap = mapSystem.CreateMap(out offNetworkMapId);
            remoteView = SEntMan.SpawnEntity(null, new EntityCoordinates(maps[1], Vector2.Zero));
            SEntMan.AddComponent<EyeComponent>(remoteView);
            subscribers.AddViewSubscriber(remoteView, session);

            Assert.That(
                SEntMan.HasComponent<CMUZLevelViewerComponent>(remoteView),
                Is.True,
                "A remote view subscription opened while disabled was not retained.");
            Assert.That(
                SEntMan.GetComponent<CMUZLevelViewerComponent>(remoteView).Eyes,
                Is.Empty,
                "A remote view opened while Multi-Z was disabled unexpectedly created probe eyes.");
            Assert.That(session.ViewSubscriptions, Does.Contain(remoteView),
                "The session was not subscribed to the remote view itself.");
        });

        await OverrideCVar(Side.Server, CMUZLevelsCVars.Enabled, true, sync: false);
        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            var viewer = SEntMan.GetComponent<CMUZLevelViewerComponent>(remoteView);
            Assert.That(viewer.Eyes, Is.Not.Empty,
                "Re-enabling Multi-Z did not build probe eyes for the retained remote view.");
            foreach (var probe in viewer.Eyes)
            {
                Assert.That(SEntMan.GetComponent<TransformComponent>(probe).MapUid, Is.EqualTo(maps[0]),
                    $"Rebuilt remote-view probe {probe} was not placed on the lower level.");
                Assert.That(session.ViewSubscriptions, Does.Contain(probe),
                    $"The remote-view session was not subscribed to rebuilt probe {probe}.");
            }
        });

        await Server.WaitPost(() =>
            transform.SetCoordinates(remoteView, new EntityCoordinates(offNetworkMap, Vector2.Zero)));
        await Pair.RunTicksSync(2);

        await Server.WaitPost(() => subscribers.RemoveViewSubscriber(remoteView, session));
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<CMUZLevelViewerComponent>(remoteView), Is.False);
        });

        await Server.WaitPost(() =>
        {
            foreach (var mapId in mapIds)
            {
                if (mapSystem.MapExists(mapId))
                    mapSystem.DeleteMap(mapId);
            }

            if (mapSystem.MapExists(offNetworkMapId))
                mapSystem.DeleteMap(offNetworkMapId);
        });
    }
}
