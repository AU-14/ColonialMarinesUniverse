using Content.IntegrationTests.Fixtures;
using Content.Server.GameTicking;
using Content.Shared.Mind;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class CMURoundEndVisibilityTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task ScoreboardDoesNotReplicateAnotherPlayersRemoteInventory()
    {
        var map = await Pair.CreateTestMap();
        NetEntity contents = default;
        await Server.WaitAssertion(() =>
        {
            // The integration pool normally sends every entity, masking global-override regressions.
            Server.CfgMan.SetCVar(Robust.Shared.CVars.NetPVS, true);
            var viewer = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, viewer);
            var remoteMap = SEntMan.System<SharedMapSystem>().CreateMap(out _, runMapInit: true);
            var body = SEntMan.SpawnEntity(null, new EntityCoordinates(remoteMap, Vector2.Zero));
            var inventoryItem = SEntMan.SpawnEntity(null, new EntityCoordinates(body, Vector2.Zero));
            contents = SEntMan.GetNetEntity(inventoryItem);
            var mind = SEntMan.System<SharedMindSystem>().CreateMind(null, "Remote player");
            SEntMan.System<SharedMindSystem>().TransferTo(mind, body);
            mind.Comp.OriginalOwnedEntity = SEntMan.GetNetEntity(body);
        });
        await Pair.RunTicksSync(10);
        await Client.WaitAssertion(() => Assert.That(CEntMan.TryGetEntity(contents, out _), Is.False));

        await Server.WaitAssertion(() => SEntMan.System<GameTicker>().ShowRoundEndScoreboard());
        await Pair.RunTicksSync(15);

        await Client.WaitAssertion(() => Assert.That(CEntMan.TryGetEntity(contents, out _), Is.False,
            "A scoreboard portrait must not make a remote body's entire inventory globally visible."));
    }
}
