using Content.IntegrationTests.Fixtures;
using Content.Server.GameTicking;
using Content.Shared.Follower;
using Content.Shared.Follower.Components;
using Content.Shared.GameTicking;
using Robust.Client.GameStates;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Follower;

[TestFixture]
[TestOf(typeof(FollowerSystem))]
public sealed class FollowerClientStateResetTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Dirty = true,
    };

    [Test]
    public async Task RoundRestartCleanupDetachesFollowerBeforeFullStateReset()
    {
        Pair.ClientLogHandler.FailureLevel = LogLevel.Fatal;
        Pair.ServerLogHandler.FailureLevel = LogLevel.Fatal;
        var map = await Pair.CreateTestMap();
        NetEntity followedNet = default;
        NetEntity followerNet = default;

        await Server.WaitPost(() =>
        {
            var coordinates = map.GridCoords;
            var followed = SEntMan.SpawnEntity(GameTicker.ObserverPrototypeName, coordinates);
            var follower = SEntMan.SpawnEntity(GameTicker.ObserverPrototypeName, coordinates);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, follower);
            SEntMan.System<FollowerSystem>().StartFollowingEntity(follower, followed);
            followedNet = SEntMan.GetNetEntity(followed);
            followerNet = SEntMan.GetNetEntity(follower);
        });
        await Pair.RunUntilSynced();

        await Client.WaitPost(() =>
        {
            var followed = CEntMan.GetEntity(followedNet);
            var follower = CEntMan.GetEntity(followerNet);
            Assert.That(CEntMan.GetComponent<TransformComponent>(follower).ParentUid, Is.EqualTo(followed));

            CEntMan.EventBus.RaiseEvent(EventSource.Network, new RoundRestartCleanupEvent());

            Assert.That(CEntMan.HasComponent<FollowerComponent>(follower), Is.False);
            Assert.That(CEntMan.GetComponent<TransformComponent>(follower).ParentUid, Is.Not.EqualTo(followed));

            var state = new GameState(
                GameTick.Zero,
                new GameTick(1),
                0,
                Array.Empty<EntityState>(),
                Array.Empty<SessionState>(),
                Array.Empty<NetEntity>());
            Client.ResolveDependency<IClientGameStateManager>().PartialStateReset(state, resetAllEntities: true);
        });
    }
}
