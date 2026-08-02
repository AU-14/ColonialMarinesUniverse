using System;
using System.Collections.Generic;
using Content.Shared._CMU14.Yautja;
using NUnit.Framework;
using Robust.Shared.Network;

namespace Content.Tests.Shared._CMU14.Yautja;

[TestFixture]
public sealed class YautjaClanInfoEuiStateTest
{
    [Test]
    public void StateRetainsSelectedClanOptionsAndCapabilities()
    {
        var options = new List<YautjaClanInfoOption>
        {
            new(null, "Players without a clan"),
            new(7, "Hunt clan"),
        };
        var state = new YautjaClanInfoEuiState(
            7,
            "Hunt clan",
            "Description",
            10,
            "#ffffff",
            YautjaRank.Ancient,
            YautjaClanPermission.All,
            options,
            true,
            true,
            true,
            true,
            true,
            true,
            true,
            true,
            [],
            "ready");

        Assert.Multiple(() =>
        {
            Assert.That(state.ClanId, Is.EqualTo(7));
            Assert.That(state.AvailableClans, Is.EqualTo(options));
            Assert.That(state.CanEditDescription, Is.True);
            Assert.That(state.CanDeleteClan, Is.True);
            Assert.That(state.StatusMessage, Is.EqualTo("ready"));
        });
    }

    [Test]
    public void MutationMessagesRetainServerCheckedPayloads()
    {
        var player = new NetUserId(Guid.NewGuid());

        Assert.Multiple(() =>
        {
            Assert.That(new YautjaClanInfoSelectClanMessage(7).ClanId, Is.EqualTo(7));
            Assert.That(new YautjaClanInfoUpdateDescriptionMessage(7, "New").Description, Is.EqualTo("New"));
            Assert.That(new YautjaClanInfoUpdateAppearanceMessage(7, "Name", "#123456").Color, Is.EqualTo("#123456"));
            Assert.That(new YautjaClanInfoSetHonorMessage(7, 12).Honor, Is.EqualTo(12));
            Assert.That(new YautjaClanInfoPurgeMemberMessage(player).Target, Is.EqualTo(player));
            Assert.That(new YautjaClanInfoDeleteClanMessage(7).ClanId, Is.EqualTo(7));
        });
    }
}
