using Content.Server.Database;
using Content.Server._CMU14.Yautja;
using Content.Shared._CMU14.Yautja;
using Robust.Shared.Network;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaRankPersistenceTest
{
    [TestCase(null, YautjaRank.Blooded)]
    [TestCase(YautjaRank.YoungBlood, YautjaRank.Blooded)]
    [TestCase((YautjaRank) 99, YautjaRank.Blooded)]
    [TestCase(YautjaRank.Unblooded, YautjaRank.Unblooded)]
    [TestCase(YautjaRank.Ancient, YautjaRank.Ancient)]
    public void StoredRankSanitizationKeepsYoungbloodRoleSeparate(YautjaRank? stored, YautjaRank expected)
    {
        Assert.That(YautjaRankManager.Sanitize(stored), Is.EqualTo(expected));
    }

    [TestCase(YautjaRank.Unblooded, YautjaRank.Blooded)]
    [TestCase(YautjaRank.Blooded, YautjaRank.Blooded)]
    [TestCase(YautjaRank.Elite, YautjaRank.Elite)]
    public void OrdinaryHunterSpawnUsesCmss13RankFallback(YautjaRank stored, YautjaRank expected)
    {
        Assert.That(YautjaRankManager.CanonicalHunterSpawnRank(stored), Is.EqualTo(expected));
    }

    [TestCase(0, 0, true)]
    [TestCase(1, 1, true)]
    [TestCase(1, 2, false)]
    public void StaleDatabaseResultsCannotUpdateNewerCacheVersion(
        long requestVersion,
        long currentVersion,
        bool expectedCurrent)
    {
        Assert.That(
            YautjaRankManager.IsCacheVersionCurrent(requestVersion, currentVersion),
            Is.EqualTo(expectedCurrent));
    }

    [Test]
    public void InvalidatedClanResolutionRejectsStaleInFlightCompletion()
    {
        var versions = new YautjaClanCacheVersions();
        var userId = new NetUserId(Guid.NewGuid());
        var inFlightVersion = versions.Capture(userId);

        versions.Increment(userId);

        Assert.That(versions.IsCurrent(userId, inFlightVersion), Is.False);
    }

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
    public async Task RankCacheMissFailsClosedWithoutThrowing()
    {
        await using var pair = await PoolManager.GetServerClient();
        var manager = pair.Server.ResolveDependency<YautjaRankManager>();
        var userId = new NetUserId(Guid.NewGuid());

        Assert.That(manager.ResolveCached(userId), Is.EqualTo(YautjaRank.Blooded));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ProfileCapabilitiesCacheMissFailsClosedWithoutThrowing()
    {
        await using var pair = await PoolManager.GetServerClient();
        var manager = pair.Server.ResolveDependency<YautjaRankManager>();
        var userId = new NetUserId(Guid.NewGuid());

        var capabilities = manager.ResolveProfileCapabilitiesCached(userId);
        Assert.Multiple(() =>
        {
            Assert.That(capabilities.Rank, Is.EqualTo(YautjaRank.Blooded));
            Assert.That(capabilities.CanUseUnique, Is.False);
            Assert.That(capabilities.CanUseLegacy, Is.False);
            Assert.That(capabilities.CanUseCouncilStatus, Is.False);
            Assert.That(capabilities.CanUseLeaderStatus, Is.False);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlayerDataLoadPrimesRankAndProfileCapabilityCaches()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var manager = pair.Server.ResolveDependency<YautjaRankManager>();
        var userId = pair.Player!.UserId;

        Assert.Multiple(() =>
        {
            Assert.That(manager.ResolveCached(userId), Is.EqualTo(YautjaRank.Blooded));
            Assert.That(
                manager.ResolveProfileCapabilitiesCached(userId).Rank,
                Is.EqualTo(YautjaRank.Blooded));
        });
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
