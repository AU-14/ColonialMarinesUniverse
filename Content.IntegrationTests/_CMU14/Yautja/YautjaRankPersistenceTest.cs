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

    // Ancient comes from the council whitelist only. a rank stored above Elder (old hall Ancients, the old
    // yautjarank ancient/leader) is capped, it must not hand out the Ancient tag or council status
    [Test]
    public async Task StoredAncientWithoutWhitelistIsCappedAtElder()
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
            Assert.That(capabilities.Rank, Is.EqualTo(YautjaRank.Elder));
            Assert.That(capabilities.CanUseUnique, Is.False);
            Assert.That(capabilities.CanUseCouncilStatus, Is.False);
            Assert.That(capabilities.CanUseAncientSets, Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CouncilWhitelistIsAncientAndUsesStatusForActiveRank()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var db = pair.Server.ResolveDependency<IServerDbManager>();
        var manager = pair.Server.ResolveDependency<YautjaRankManager>();
        var userId = pair.Player!.UserId;

        await db.SetYautjaWhitelistFlagsAsync(userId.UserId, (int) YautjaWhitelistFlags.Council);
        await manager.Refresh(userId);
        var capabilities = manager.ResolveProfileCapabilitiesCached(userId);

        Assert.Multiple(() =>
        {
            Assert.That(capabilities.Rank, Is.EqualTo(YautjaRank.Ancient));
            Assert.That(capabilities.CanUseUnique, Is.True);
            Assert.That(capabilities.CanUseCouncilStatus, Is.True);
            Assert.That(capabilities.CanUseLeaderStatus, Is.False);
            Assert.That(capabilities.CanUseAncientSets, Is.True);
            Assert.That(
                capabilities.ForStatus(YautjaProfileStatus.Normal).Rank,
                Is.EqualTo(YautjaRank.Blooded));
        });

        await db.SetYautjaWhitelistFlagsAsync(userId.UserId, 0);
        await manager.Refresh(userId);
        await pair.CleanReturnAsync();
    }
}
