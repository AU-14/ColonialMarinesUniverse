using System;
using Content.Shared._CMU14.Yautja;
using NUnit.Framework;
using Robust.Shared.Network;

namespace Content.Tests.Shared._CMU14.Yautja;

[TestFixture]
public sealed class YautjaClanPolicyTest
{
    [TestCase(YautjaRank.Unblooded, YautjaClanPermission.AdminModify, null, null)]
    [TestCase(YautjaRank.Blooded, YautjaClanPermission.UserModify, null, null)]
    [TestCase(YautjaRank.Elite, YautjaClanPermission.UserModify, 5, null)]
    [TestCase(YautjaRank.Elder, YautjaClanPermission.UserModify, null, 12)]
    [TestCase(YautjaRank.Leader, YautjaClanPermission.AdminModify, 1, null)]
    [TestCase(YautjaRank.Ancient, YautjaClanPermission.AdminAncient, null, null)]
    public void RankRulesMatchCmss13(
        YautjaRank rank,
        YautjaClanPermission permission,
        int? absoluteLimit,
        int? membersPerRankLimit)
    {
        var rule = YautjaClanPolicy.GetRule(rank);

        Assert.Multiple(() =>
        {
            Assert.That(rule.RequiredPermission, Is.EqualTo(permission));
            Assert.That(rule.AbsoluteLimit, Is.EqualTo(absoluteLimit));
            Assert.That(rule.MembersPerRankLimit, Is.EqualTo(membersPerRankLimit));
        });
    }

    [TestCase(YautjaRank.Unblooded, "predhud")]
    [TestCase(YautjaRank.YoungBlood, "predhud")]
    [TestCase(YautjaRank.Blooded, "predhud")]
    [TestCase(YautjaRank.Elite, "predhud")]
    [TestCase(YautjaRank.Elder, "predhud")]
    [TestCase(YautjaRank.Leader, "leaderhud")]
    [TestCase(YautjaRank.Ancient, "councilhud")]
    public void RankHudStatesMatchCmss13(YautjaRank rank, string expectedIconState)
    {
        Assert.That(YautjaRankMetadata.For(rank).IconState, Is.EqualTo(expectedIconState));
    }

    [Test]
    public void ActorCannotTargetSelfOrEqualOrHigherRank()
    {
        var actor = Member(1, YautjaRank.Leader, YautjaClanPermission.UserAll);

        Assert.Multiple(() =>
        {
            Assert.That(YautjaClanPolicy.CanTarget(actor, actor), Is.False);
            Assert.That(YautjaClanPolicy.CanTarget(
                actor,
                Member(2, YautjaRank.Leader, YautjaClanPermission.UserAll)), Is.False);
            Assert.That(YautjaClanPolicy.CanTarget(
                actor,
                Member(3, YautjaRank.Ancient, YautjaClanPermission.AdminAncient)), Is.False);
        });
    }

    [Test]
    public void ManagerStillCannotTargetAncientAdministrator()
    {
        var actor = Member(1, YautjaRank.Ancient, YautjaClanPermission.All);
        var target = Member(2, YautjaRank.Leader, YautjaClanPermission.AdminAncient);

        Assert.That(YautjaClanPolicy.CanTarget(actor, target), Is.False);
    }

    [Test]
    public void AncientManagerCanDemoteClanAncient()
    {
        var actor = Member(1, YautjaRank.Ancient, YautjaClanPermission.All);
        var target = Member(2, YautjaRank.Ancient, YautjaClanPermission.AdminAncient);

        Assert.Multiple(() =>
        {
            Assert.That(YautjaClanPolicy.CanSetAncient(actor, target, true), Is.False);
            Assert.That(YautjaClanPolicy.CanSetAncient(actor, target, false), Is.True);
        });
    }

    [Test]
    public void CouncilAncientCannotChangeAncientStatus()
    {
        var council = Member(1, YautjaRank.Ancient, YautjaClanPermission.AdminAncient);
        var target = Member(2, YautjaRank.Blooded, YautjaClanPermission.UserAll);

        Assert.That(YautjaClanPolicy.CanSetAncient(council, target, true), Is.False);
    }

    [TestCase(YautjaClanPermission.UserView, true)]
    [TestCase(YautjaClanPermission.AdminView, true)]
    [TestCase(YautjaClanPermission.AdminModify, false)]
    public void ClanInfoRequiresViewPermission(YautjaClanPermission permissions, bool expected)
    {
        Assert.That(YautjaClanPolicy.CanView(Member(1, YautjaRank.Blooded, permissions)), Is.EqualTo(expected));
    }

    [Test]
    public void NormalRankOptionsExcludeYoungBloodAndAncient()
    {
        var options = YautjaClanPolicy.GetNormalAssignableRanks();

        Assert.Multiple(() =>
        {
            Assert.That(options, Does.Not.Contain(YautjaRank.YoungBlood));
            Assert.That(options, Does.Not.Contain(YautjaRank.Ancient));
            Assert.That(options, Does.Contain(YautjaRank.Unblooded));
            Assert.That(options, Does.Contain(YautjaRank.Leader));
        });
    }

    [Test]
    public void AllIncludesUserAndAdminPermissionGroups()
    {
        Assert.That(
            YautjaClanPermission.All,
            Is.EqualTo(YautjaClanPermission.UserAll |
                       YautjaClanPermission.AdminAncient |
                       YautjaClanPermission.AdminManager));
    }

    [Test]
    public void OrdinaryLeaderCanOnlyManageOwnClan()
    {
        var leader = Member(1, YautjaRank.Leader, YautjaClanPermission.UserAll);

        Assert.Multiple(() =>
        {
            Assert.That(YautjaClanPolicy.CanManageClan(leader, 1, YautjaClanPermission.UserModify), Is.True);
            Assert.That(YautjaClanPolicy.CanManageClan(leader, 2, YautjaClanPermission.UserModify), Is.False);
            Assert.That(YautjaClanPolicy.CanManageClan(leader, 1, YautjaClanPermission.AdminModify), Is.False);
        });
    }

    [Test]
    public void CouncilCanManageRanksAcrossClansButNotAncientStatus()
    {
        var council = Member(1, YautjaRank.Ancient, YautjaClanPermission.AdminAncient);
        var target = Member(2, YautjaRank.Blooded, YautjaClanPermission.UserAll, 2);

        Assert.Multiple(() =>
        {
            Assert.That(
                YautjaClanPolicy.CanModifyRank(council, target, YautjaRank.Elder, 1, 0),
                Is.True);
            Assert.That(YautjaClanPolicy.CanSetAncient(council, target, true), Is.False);
        });
    }

    [TestCase(YautjaRank.Elite, 5, 5, false)]
    [TestCase(YautjaRank.Elite, 4, 5, true)]
    [TestCase(YautjaRank.Elder, 1, 12, false)]
    [TestCase(YautjaRank.Elder, 1, 13, true)]
    [TestCase(YautjaRank.Leader, 1, 1, false)]
    public void RankLimitsUsePostChangeOccupancy(
        YautjaRank rank,
        int currentOccupancy,
        int clanSize,
        bool expectedAllowed)
    {
        var actor = Member(1, YautjaRank.Leader, YautjaClanPermission.UserAll | YautjaClanPermission.AdminModify);
        var target = Member(2, YautjaRank.Blooded, YautjaClanPermission.UserModify);

        Assert.That(
            YautjaClanPolicy.CanModifyRank(actor, target, rank, clanSize, currentOccupancy),
            Is.EqualTo(expectedAllowed));
    }

    private static YautjaClanMemberSnapshot Member(
        int id,
        YautjaRank rank,
        YautjaClanPermission permissions,
        int clanId = 1)
    {
        return new YautjaClanMemberSnapshot(
            new NetUserId(new Guid(id, 0, 0, new byte[8])),
            clanId,
            rank,
            permissions,
            false,
            0);
    }
}
