using System;
using Content.Server.Database;
using Content.Server._CMU14.Yautja;
using Content.Shared._CMU14.Yautja;
using NUnit.Framework;
using Robust.Shared.Network;

namespace Content.Tests.Server._CMU14.Yautja;

[TestFixture]
public sealed class YautjaClanAdminEuiTest
{
    [Test]
    public void ToMemberStateSanitizesRankAndRetainsDisplayData()
    {
        var playerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var record = new YautjaClanMemberRecord(playerId, 7, 255, 0, 0, false);

        var state = YautjaClanAdminEui.ToMemberState(
            record,
            "Unknown hunter",
            false,
            YautjaWhitelistFlags.Council);

        Assert.Multiple(() =>
        {
            Assert.That(state.PlayerId, Is.EqualTo(new NetUserId(playerId)));
            Assert.That(state.Name, Is.EqualTo("Unknown hunter"));
            Assert.That(state.Rank, Is.EqualTo(YautjaRank.Blooded));
            Assert.That(state.Online, Is.False);
            Assert.That(state.WhitelistFlags, Is.EqualTo(YautjaWhitelistFlags.Council));
        });
    }

    [Test]
    public void RemoveFromClanPreservesMemberData()
    {
        var source = new YautjaClanMemberRecord(
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            12,
            5,
            11,
            42,
            true);
        var detached = YautjaClanAdminEui.RemoveFromClan(source);

        Assert.Multiple(() =>
        {
            Assert.That(detached.ClanId, Is.Null);
            Assert.That(detached.PlayerUserId, Is.EqualTo(source.PlayerUserId));
            Assert.That(detached.Rank, Is.EqualTo(source.Rank));
            Assert.That(detached.Permissions, Is.EqualTo(source.Permissions));
            Assert.That(detached.Honor, Is.EqualTo(source.Honor));
            Assert.That(detached.IsLegacy, Is.EqualTo(source.IsLegacy));
        });
    }

    [Test]
    public void IsClanlessOnlyAcceptsRecordsWithoutClan()
    {
        Assert.That(
            YautjaClanAdminEui.IsClanless(
                new YautjaClanMemberRecord(Guid.NewGuid(), null, 2, 3, 0, false)),
            Is.True);
        Assert.That(
            YautjaClanAdminEui.IsClanless(
                new YautjaClanMemberRecord(Guid.NewGuid(), 9, 2, 3, 0, false)),
            Is.False);
    }
}
