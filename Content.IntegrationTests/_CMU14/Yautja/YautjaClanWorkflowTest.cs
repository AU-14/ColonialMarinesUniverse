using Content.IntegrationTests.Pair;
using Content.Server.Database;
using Content.Server.EUI;
using Content.Server._CMU14.Yautja;
using Content.Shared._CMU14.Yautja;
using NUnit.Framework;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaClanWorkflowTest
{
    [Test]
    public void CouncilWhitelistResolvesAncientWithoutManagerPermission()
    {
        var resolution = YautjaClanManager.ResolveSpecial(YautjaWhitelistFlags.Council, false, 7, 12, true);

        Assert.Multiple(() =>
        {
            Assert.That(resolution.Rank, Is.EqualTo(YautjaRank.Ancient));
            Assert.That(resolution.Permissions, Is.EqualTo(YautjaClanPermission.AdminAncient));
            Assert.That(resolution.ClanId, Is.EqualTo(7));
            Assert.That(resolution.Honor, Is.EqualTo(12));
            Assert.That(resolution.IsLegacy, Is.True);
        });
    }

    [Test]
    public void WhitelistLeaderWinsOverCouncil()
    {
        var resolution = YautjaClanManager.ResolveSpecial(
            YautjaWhitelistFlags.Leader | YautjaWhitelistFlags.Council,
            false,
            7,
            12,
            false);

        Assert.Multiple(() =>
        {
            Assert.That(resolution.Rank, Is.EqualTo(YautjaRank.Ancient));
            Assert.That(resolution.Permissions, Is.EqualTo(YautjaClanPermission.All));
            Assert.That(resolution.ClanId, Is.EqualTo(7));
        });
    }

    [Test]
    public void YoungBloodSpecialRoleNeverInheritsPersistentAncientStatus()
    {
        var resolution = YautjaClanManager.ResolveSpecial(YautjaWhitelistFlags.Leader, true);

        Assert.Multiple(() =>
        {
            Assert.That(resolution.Rank, Is.EqualTo(YautjaRank.YoungBlood));
            Assert.That(resolution.Permissions, Is.EqualTo(YautjaClanPermission.None));
        });
    }

    [TestCase(YautjaRank.Unblooded, YautjaClanPermission.AdminModify)]
    [TestCase(YautjaRank.Blooded, YautjaClanPermission.UserAll)]
    [TestCase(YautjaRank.Elite, YautjaClanPermission.UserAll)]
    [TestCase(YautjaRank.Elder, YautjaClanPermission.UserAll)]
    [TestCase(YautjaRank.Leader, YautjaClanPermission.UserAll)]
    [TestCase(YautjaRank.Ancient, YautjaClanPermission.AdminAncient)]
    public void ApplyingRankRestoresCmss13Permissions(YautjaRank rank, YautjaClanPermission expected)
    {
        Assert.That(YautjaClanManager.PermissionsForRank(rank), Is.EqualTo(expected));
    }

    [TestCase(null, YautjaRank.Blooded)]
    [TestCase(1, YautjaRank.Blooded)]
    [TestCase(2, YautjaRank.Blooded)]
    [TestCase(99, YautjaRank.Blooded)]
    [TestCase(5, YautjaRank.Leader)]
    public void StoredRankValuesSanitizeToNormalGameplayRank(int? value, YautjaRank expected)
    {
        Assert.That(YautjaClanManager.SanitizeStoredRank(value), Is.EqualTo(expected));
    }

    [Test]
    public async Task ClanInfoEuiOpensForClanMemberWithoutServerException()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var session = pair.Player!;
        var db = server.ResolveDependency<IServerDbManager>();
        var clanManager = server.ResolveDependency<YautjaClanManager>();
        var euiManager = server.ResolveDependency<EuiManager>();
        var clanId = await db.CreateYautjaClanAsync("Info EUI", "Information EUI test", 12, "#123456");
        await db.UpsertYautjaClanMemberAsync(new YautjaClanMemberRecord(
            session.UserId.UserId,
            clanId,
            (int) YautjaRank.Elder,
            (int) YautjaClanPermission.UserAll,
            7,
            false));
        clanManager.InvalidateCache(session.UserId);

        YautjaClanInfoEui? eui = null;
        await server.WaitPost(() =>
        {
            eui = new YautjaClanInfoEui();
            euiManager.OpenEui(eui, session);
        });
        await pair.RunTicksSync(10);
        await server.WaitAssertion(() =>
        {
            Assert.That(eui!.IsShutDown, Is.False);
            var state = (YautjaClanInfoEuiState) eui.GetNewState();
            Assert.That(state.ClanId, Is.EqualTo(clanId));
            Assert.That(state.Members, Has.Count.EqualTo(1));
            Assert.That(state.Members[0].PlayerId, Is.EqualTo(session.UserId));
        });

        await pair.CleanReturnAsync();
    }
}
