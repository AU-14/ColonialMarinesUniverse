using System.Linq;
using Content.Server._CMU14.Yautja;
using Content.Server.Database;
using Content.Shared._CMU14.Yautja;
using Robust.Shared.Network;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaClanMutationPersistenceTest
{
    [Test]
    public async Task UpdateChangesEditableFieldsOnly()
    {
        await using var pair = await PoolManager.GetServerClient();
        var db = pair.Server.ResolveDependency<IServerDbManager>();
        var clanId = await db.CreateYautjaClanAsync("Old", "Old description", 42, "#111111");

        var updated = await db.UpdateYautjaClanAsync(clanId, "New", "New description", "#AABBCC");
        var clan = await db.GetYautjaClanAsync(clanId);

        Assert.Multiple(() =>
        {
            Assert.That(updated, Is.True);
            Assert.That(clan, Is.Not.Null);
            Assert.That(clan!.Name, Is.EqualTo("New"));
            Assert.That(clan.Description, Is.EqualTo("New description"));
            Assert.That(clan.Color, Is.EqualTo("#AABBCC"));
            Assert.That(clan.Honor, Is.EqualTo(42));
            Assert.That(clan.Active, Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UpdateRejectsInactiveAndMissingClans()
    {
        await using var pair = await PoolManager.GetServerClient();
        var db = pair.Server.ResolveDependency<IServerDbManager>();
        var inactiveId = await db.CreateYautjaClanAsync(
            "Inactive",
            "Inactive description",
            0,
            "#111111",
            active: false);

        var inactiveUpdated =
            await db.UpdateYautjaClanAsync(inactiveId, "Changed", "Changed", "#222222");
        var missingUpdated =
            await db.UpdateYautjaClanAsync(int.MaxValue, "Missing", "Missing", "#333333");

        Assert.Multiple(() =>
        {
            Assert.That(inactiveUpdated, Is.False);
            Assert.That(missingUpdated, Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ClearingWhitelistRemovesMembershipAndPersistentYautjaRank()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var db = pair.Server.ResolveDependency<IServerDbManager>();
        var playerId = pair.Player!.UserId.UserId;
        var clanId = await db.CreateYautjaClanAsync("Whitelist reset", "Whitelist reset test", 0, "#123456");

        await db.UpsertYautjaClanMemberAsync(new YautjaClanMemberRecord(
            playerId,
            clanId,
            (int) YautjaRank.Elder,
            (int) (YautjaClanPermission.UserModify | YautjaClanPermission.UserView),
            0,
            false));
        await db.SetYautjaWhitelistFlagsAsync(playerId, (int) YautjaWhitelistFlags.Council);

        await db.SetYautjaWhitelistFlagsAsync(playerId, (int) YautjaWhitelistFlags.None);

        var member = await db.GetYautjaClanMemberAsync(playerId);
        var playerRank = await db.GetYautjaRank(playerId);
        var flags = await db.GetYautjaWhitelistFlagsAsync(playerId);

        Assert.Multiple(() =>
        {
            Assert.That(flags, Is.EqualTo((int) YautjaWhitelistFlags.None));
            Assert.That(playerRank, Is.Null);
            Assert.That(member, Is.Null);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ClearingWhitelistRemovesLegacyRankWithoutClanMembership()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var db = pair.Server.ResolveDependency<IServerDbManager>();
        var playerId = pair.Player!.UserId.UserId;

        await db.SetYautjaRank(playerId, YautjaRank.Elder);
        await db.SetYautjaWhitelistFlagsAsync(playerId, (int) YautjaWhitelistFlags.Council);

        await db.SetYautjaWhitelistFlagsAsync(playerId, (int) YautjaWhitelistFlags.None);

        Assert.That(await db.GetYautjaRank(playerId), Is.Null);

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DeleteDeactivatesClanAndResetsDetachedMember()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var db = pair.Server.ResolveDependency<IServerDbManager>();
        var playerId = pair.Player!.UserId.UserId;
        var clanId = await db.CreateYautjaClanAsync("Delete me", "Deletion test", 7, "#123456");
        await db.UpsertYautjaClanMemberAsync(new YautjaClanMemberRecord(
            playerId,
            clanId,
            (int) YautjaRank.Elder,
            (int) (YautjaClanPermission.UserModify | YautjaClanPermission.UserView),
            13,
            true));

        var first = await db.DeactivateYautjaClanAsync(clanId);
        var second = await db.DeactivateYautjaClanAsync(clanId);
        var clan = await db.GetYautjaClanAsync(clanId);
        var member = await db.GetYautjaClanMemberAsync(playerId);
        var activeClans = await db.GetYautjaClansAsync();

        Assert.Multiple(() =>
        {
            Assert.That(first.Succeeded, Is.True);
            Assert.That(first.DetachedPlayers, Is.EqualTo(new[] { playerId }));
            Assert.That(second.Succeeded, Is.False);
            Assert.That(second.DetachedPlayers, Is.Empty);
            Assert.That(clan!.Active, Is.False);
            Assert.That(activeClans.All(entry => entry.Id != clanId), Is.True);
            Assert.That(member!.ClanId, Is.Null);
            Assert.That(member.Rank, Is.EqualTo((int) YautjaRank.Blooded));
            Assert.That(member.Permissions, Is.EqualTo((int) YautjaClanPermission.UserAll));
            Assert.That(member.Honor, Is.EqualTo(13));
            Assert.That(member.IsLegacy, Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AssignmentRejectsInactiveAndMissingClanIds()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var db = pair.Server.ResolveDependency<IServerDbManager>();
        var playerId = pair.Player!.UserId.UserId;
        var inactiveId = await db.CreateYautjaClanAsync(
            "Inactive assignment",
            "Inactive assignment test",
            0,
            "#111111",
            active: false);

        var inactiveAssigned = await db.UpsertYautjaClanMemberAsync(Member(playerId, inactiveId));
        var missingAssigned = await db.UpsertYautjaClanMemberAsync(Member(playerId, int.MaxValue));
        var member = await db.GetYautjaClanMemberAsync(playerId);

        Assert.Multiple(() =>
        {
            Assert.That(inactiveAssigned, Is.False);
            Assert.That(missingAssigned, Is.False);
            Assert.That(member, Is.Null);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DeleteMemberClearsCompatibilityProjectionAtomically()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var db = pair.Server.ResolveDependency<IServerDbManager>();
        var playerId = pair.Player!.UserId.UserId;
        var clanId = await db.CreateYautjaClanAsync("Purge", "Purge test", 0, "#123456");

        await db.UpsertYautjaClanMemberAsync(new YautjaClanMemberRecord(
            playerId,
            clanId,
            (int) YautjaRank.Leader,
            (int) YautjaClanPermission.UserAll,
            4,
            false));

        var deleted = await db.DeleteYautjaClanMemberAsync(playerId);

        Assert.Multiple(() =>
        {
            Assert.That(deleted, Is.True);
            Assert.That(db.GetYautjaClanMemberAsync(playerId).GetAwaiter().GetResult(), Is.Null);
            Assert.That(db.GetYautjaRank(playerId).GetAwaiter().GetResult(), Is.Null);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HonorUpdatePreservesOtherClanFields()
    {
        await using var pair = await PoolManager.GetServerClient();
        var db = pair.Server.ResolveDependency<IServerDbManager>();
        var clanId = await db.CreateYautjaClanAsync("Honor", "Description", 3, "#123456");

        var updated = await db.UpdateYautjaClanHonorAsync(clanId, 99);
        var clan = await db.GetYautjaClanAsync(clanId);

        Assert.Multiple(() =>
        {
            Assert.That(updated, Is.True);
            Assert.That(clan!.Honor, Is.EqualTo(99));
            Assert.That(clan.Name, Is.EqualTo("Honor"));
            Assert.That(clan.Description, Is.EqualTo("Description"));
            Assert.That(clan.Color, Is.EqualTo("#123456"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WhitelistLeaderResolutionKeepsPersistentClanScope()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var db = pair.Server.ResolveDependency<IServerDbManager>();
        var manager = pair.Server.ResolveDependency<YautjaClanManager>();
        var playerId = pair.Player!.UserId;
        var clanId = await db.CreateYautjaClanAsync("Scope", "Scope test", 0, "#123456");

        await db.UpsertYautjaClanMemberAsync(new YautjaClanMemberRecord(
            playerId.UserId,
            clanId,
            (int) YautjaRank.Leader,
            (int) YautjaClanPermission.UserAll,
            17,
            true));
        await db.SetYautjaWhitelistFlagsAsync(playerId.UserId, (int) YautjaWhitelistFlags.Leader);
        manager.InvalidateCache(playerId);

        var resolution = await manager.Resolve(playerId);

        Assert.Multiple(() =>
        {
            Assert.That(resolution.Rank, Is.EqualTo(YautjaRank.Ancient));
            Assert.That(resolution.Permissions, Is.EqualTo(YautjaClanPermission.All));
            Assert.That(resolution.ClanId, Is.EqualTo(clanId));
            Assert.That(resolution.Honor, Is.EqualTo(17));
            Assert.That(resolution.IsLegacy, Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ConcurrentDeleteAndAssignmentNeverLeaveMemberInInactiveClan()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var db = pair.Server.ResolveDependency<IServerDbManager>();
        var playerId = pair.Player!.UserId.UserId;
        var clanId = await db.CreateYautjaClanAsync(
            "Concurrent assignment",
            "Concurrent assignment test",
            0,
            "#222222");

        var assignment = db.UpsertYautjaClanMemberAsync(Member(playerId, clanId));
        var deletion = db.DeactivateYautjaClanAsync(clanId);
        await Task.WhenAll(assignment, deletion);
        var deletionResult = await deletion;

        var clan = await db.GetYautjaClanAsync(clanId);
        var member = await db.GetYautjaClanMemberAsync(playerId);

        Assert.Multiple(() =>
        {
            Assert.That(deletionResult.Succeeded, Is.True);
            Assert.That(clan, Is.Not.Null);
            Assert.That(clan!.Active, Is.False);
            Assert.That(member?.ClanId, Is.Null);
        });

        await pair.CleanReturnAsync();
    }

    private static YautjaClanMemberRecord Member(Guid playerId, int clanId)
    {
        return new(
            playerId,
            clanId,
            (int) YautjaRank.Blooded,
            (int) YautjaClanPermission.UserAll,
            0,
            false);
    }
}
