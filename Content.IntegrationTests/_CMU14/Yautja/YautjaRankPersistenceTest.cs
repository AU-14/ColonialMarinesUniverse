using Content.Server.Database;
using Content.Server.CMU14.Yautja;
using Content.Shared.CMU14.Yautja;
using Robust.Shared.Network;

namespace Content.IntegrationTests.CMU14.Yautja;

// A clean return releases the pair before the final await-using disposal.
// Serialize this fixture so disposal cannot race another fixture borrowing that pair.
[TestFixture, NonParallelizable]
public sealed class YautjaRankPersistenceTest
{

    [Test]
    public async Task RankRoundTripsThroughSqlite()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var db = pair.Server.ResolveDependency<IServerDbManager>();
        var userId = pair.Player!.UserId.UserId;

        await db.SetYautjaRank(userId, YautjaRank.Elder);

        Assert.That(await db.GetYautjaRank(userId), Is.EqualTo(YautjaRank.Elder));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ClanlessAncientUsesStatusForActiveRankButKeepsAncientEntitlements()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var db = pair.Server.ResolveDependency<IServerDbManager>();
        var manager = pair.Server.ResolveDependency<YautjaRankManager>();
        var userId = pair.Player!.UserId;

        await db.SetYautjaRank(userId.UserId, YautjaRank.Ancient);
        await manager.Refresh(userId);
        var capabilities = manager.ResolveProfileCapabilitiesCached(userId);

        Assert.Multiple(() =>
        {
            Assert.That(capabilities.Rank, Is.EqualTo(YautjaRank.Ancient));
            Assert.That(capabilities.CanUseUnique, Is.True);
            Assert.That(capabilities.CanUseCouncilStatus, Is.True);
            Assert.That(
                capabilities.ForStatus(YautjaProfileStatus.Normal).Rank,
                Is.EqualTo(YautjaRank.Blooded));
        });

        await pair.CleanReturnAsync();
    }
}
