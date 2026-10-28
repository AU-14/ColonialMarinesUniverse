using System;
using System.Linq;
using Content.Shared.CMU14.Yautja;
using NUnit.Framework;
using Robust.Shared.Network;

namespace Content.Tests.Shared.CMU14.Yautja;

// the clan hall's lore ladder: every rank adds a power and only ever acts on ranks below it
[TestFixture]
public sealed class YautjaClanPolicyTest
{
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

    [TestCase(YautjaRank.YoungBlood, false)]
    [TestCase(YautjaRank.Unblooded, false)]
    [TestCase(YautjaRank.Blooded, true)]
    [TestCase(YautjaRank.Elite, true)]
    public void OnlyBloodedLookInsideOtherClans(YautjaRank rank, bool expected)
    {
        var viewer = Actor(1, rank, clanId: null);
        Assert.That(YautjaClanHallPolicy.CanViewClanPage(viewer, 2), Is.EqualTo(expected));
    }

    [Test]
    public void EveryoneSeesTheirOwnClan()
    {
        Assert.That(YautjaClanHallPolicy.CanViewClanPage(Actor(1, YautjaRank.Unblooded), 1), Is.True);
    }

    [TestCase(YautjaRank.Blooded, false)]
    [TestCase(YautjaRank.Elite, true)]
    [TestCase(YautjaRank.Leader, true)]
    public void EliteNotesStartAtElite(YautjaRank rank, bool expected)
    {
        Assert.Multiple(() =>
        {
            Assert.That(YautjaClanHallPolicy.CanSeeEliteNotes(Actor(1, rank), 1), Is.EqualTo(expected));
            // and never another clan's
            Assert.That(YautjaClanHallPolicy.CanSeeEliteNotes(Actor(1, rank), 2), Is.False);
        });
    }

    [Test]
    public void ElderHandlesUnbloodedAndBloodedOnly()
    {
        var elder = Actor(1, YautjaRank.Elder);
        var blooded = Member(2, YautjaRank.Blooded);
        var elite = Member(3, YautjaRank.Elite);

        Assert.Multiple(() =>
        {
            Assert.That(YautjaClanHallPolicy.AssignableRanks(elder, blooded), Is.EquivalentTo(new[] { YautjaRank.Unblooded }));
            Assert.That(YautjaClanHallPolicy.CanSetRank(elder, blooded, YautjaRank.Elite), Is.False);
            // an Elite is above an Elder's band, even though the Elder outranks them
            Assert.That(YautjaClanHallPolicy.CanSetRank(elder, elite, YautjaRank.Blooded), Is.False);
            Assert.That(YautjaClanHallPolicy.CanEditNotice(elder, 1), Is.True);
            Assert.That(YautjaClanHallPolicy.CanEditPresentation(elder, 1), Is.False);
            Assert.That(YautjaClanHallPolicy.CanExile(elder, blooded), Is.False);
            Assert.That(YautjaClanHallPolicy.CanAnswerPetitions(elder, 1), Is.True);
        });
    }

    [Test]
    public void LeaderRunsTheirClanUpToElder()
    {
        var leader = Actor(1, YautjaRank.Leader);
        var elite = Member(2, YautjaRank.Elite);
        var otherClan = Member(3, YautjaRank.Blooded, clanId: 2);

        Assert.Multiple(() =>
        {
            Assert.That(YautjaClanHallPolicy.CanSetRank(leader, elite, YautjaRank.Elder), Is.True);
            Assert.That(YautjaClanHallPolicy.CanSetRank(leader, elite, YautjaRank.Leader), Is.False);
            Assert.That(YautjaClanHallPolicy.CanExile(leader, elite), Is.True);
            Assert.That(YautjaClanHallPolicy.CanEditPresentation(leader, 1), Is.True);
            Assert.That(YautjaClanHallPolicy.CanRename(leader), Is.False);
            Assert.That(YautjaClanHallPolicy.CanSetRank(leader, otherClan, YautjaRank.Unblooded), Is.False);
            Assert.That(YautjaClanHallPolicy.CanExile(leader, otherClan), Is.False);
            Assert.That(YautjaClanHallPolicy.CanMove(leader, elite), Is.False);
        });
    }

    [Test]
    public void NobodyActsOnTheirEqualsOrThemselves()
    {
        var leader = Actor(1, YautjaRank.Leader);
        var otherLeader = Member(2, YautjaRank.Leader);

        Assert.Multiple(() =>
        {
            Assert.That(YautjaClanHallPolicy.Outranks(leader, leader.Member), Is.False);
            Assert.That(YautjaClanHallPolicy.Outranks(leader, otherLeader), Is.False);
            Assert.That(YautjaClanHallPolicy.CanExile(leader, otherLeader), Is.False);
        });
    }

    [Test]
    public void AncientWorksAcrossClansButNotOnAncients()
    {
        var ancient = Actor(1, YautjaRank.Ancient, YautjaClanPermission.AdminAncient, clanId: null);
        var leader = Member(2, YautjaRank.Leader, clanId: 5);
        var otherAncient = Member(3, YautjaRank.Ancient, YautjaClanPermission.AdminAncient, clanId: 5);

        Assert.Multiple(() =>
        {
            Assert.That(YautjaClanHallPolicy.CanSetRank(ancient, leader, YautjaRank.Elder), Is.True);
            Assert.That(YautjaClanHallPolicy.CanMove(ancient, leader), Is.True);
            Assert.That(YautjaClanHallPolicy.CanFoundOrDisband(ancient), Is.True);
            Assert.That(YautjaClanHallPolicy.CanSetHonor(ancient), Is.True);
            Assert.That(YautjaClanHallPolicy.CanSeeEliteNotes(ancient, 5), Is.True);
            Assert.That(YautjaClanHallPolicy.CanMove(ancient, otherAncient), Is.False);
            Assert.That(YautjaClanHallPolicy.CanSetAncient(ancient, leader), Is.False);
            Assert.That(YautjaClanHallPolicy.CanManageWhitelist(ancient), Is.False);
        });
    }

    [Test]
    public void CouncilManagerRaisesAndDropsAncients()
    {
        var manager = Actor(1, YautjaRank.Ancient, YautjaClanPermission.All, clanId: null);
        var ancient = Member(2, YautjaRank.Ancient, YautjaClanPermission.AdminAncient);
        var otherManager = Member(3, YautjaRank.Ancient, YautjaClanPermission.All);

        Assert.Multiple(() =>
        {
            Assert.That(YautjaClanHallPolicy.CanSetAncient(manager, ancient), Is.True);
            Assert.That(YautjaClanHallPolicy.CanMove(manager, ancient), Is.True);
            Assert.That(YautjaClanHallPolicy.CanSetAncient(manager, otherManager), Is.False);
        });
    }

    [Test]
    public void StaffCanDoEverythingButActOnThemselves()
    {
        var staff = Actor(1, YautjaRank.Blooded, clanId: null, staff: true);
        var manager = Member(2, YautjaRank.Ancient, YautjaClanPermission.All);

        Assert.Multiple(() =>
        {
            Assert.That(YautjaClanHallPolicy.CanManageWhitelist(staff), Is.True);
            Assert.That(YautjaClanHallPolicy.CanSetAncient(staff, manager), Is.False);
            Assert.That(YautjaClanHallPolicy.CanPurge(staff, manager), Is.True);
            Assert.That(YautjaClanHallPolicy.CanPurge(staff, staff.Member), Is.False);
            Assert.That(YautjaClanHallPolicy.CanEditRound(staff), Is.False);
            Assert.That(YautjaClanHallPolicy.CanEditRound(staff with { Host = true }), Is.True);
        });
    }

    [Test]
    public void OnlyClanlessNonYoungbloodsPetition()
    {
        Assert.Multiple(() =>
        {
            Assert.That(YautjaClanHallPolicy.CanPetition(Actor(1, YautjaRank.Blooded, clanId: null)), Is.True);
            Assert.That(YautjaClanHallPolicy.CanPetition(Actor(1, YautjaRank.YoungBlood, clanId: null)), Is.False);
            Assert.That(YautjaClanHallPolicy.CanPetition(Actor(1, YautjaRank.Blooded)), Is.False);
        });
    }

    [Test]
    public void SigilsRoundTripAndRejectJunk()
    {
        Assert.Multiple(() =>
        {
            foreach (var material in YautjaClanSigils.Materials)
            {
                for (var style = 1; style <= YautjaClanSigils.Styles; style++)
                {
                    var sigil = YautjaClanSigils.Make(style, material);
                    Assert.That(YautjaClanSigils.TryParse(sigil, out var parsedStyle, out var parsedMaterial), Is.True, sigil);
                    Assert.That((parsedStyle, parsedMaterial), Is.EqualTo((style, material)));
                }
            }

            Assert.That(YautjaClanSigils.IsValid("pred_mask0_ebony"), Is.False);
            Assert.That(YautjaClanSigils.IsValid("pred_mask21_ebony"), Is.False);
            Assert.That(YautjaClanSigils.IsValid("pred_mask_ancient"), Is.False);
            Assert.That(YautjaClanSigils.IsValid("../../etc"), Is.False);
            Assert.That(YautjaClanSigils.OrDefault(""), Is.EqualTo(YautjaClanSigils.Default));
        });
    }

    private static YautjaClanHallActor Actor(
        int id,
        YautjaRank rank,
        YautjaClanPermission permissions = YautjaClanPermission.UserAll,
        int? clanId = 1,
        bool staff = false)
    {
        return new YautjaClanHallActor(Member(id, rank, permissions, clanId), staff, false);
    }

    private static YautjaClanMemberSnapshot Member(
        int id,
        YautjaRank rank,
        YautjaClanPermission permissions = YautjaClanPermission.UserAll,
        int? clanId = 1)
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
