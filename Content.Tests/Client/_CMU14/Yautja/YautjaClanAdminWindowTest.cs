using System;
using Content.Client._CMU14.Yautja;
using Content.Shared._CMU14.Yautja;
using NUnit.Framework;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Network;
using Robust.UnitTesting;

namespace Content.Tests.Client._CMU14.Yautja;

[TestFixture]
public sealed class YautjaClanAdminWindowTest : RobustUnitTest
{
    public override UnitTestProject Project => UnitTestProject.Client;

    [OneTimeSetUp]
    public void Setup()
    {
        IoCManager.Resolve<IUserInterfaceManager>().InitializeTesting();
    }

    [Test]
    public void SelectorSelectionUpdatesSelectedIdUsedByAdminAction()
    {
        var option = new OptionButton();
        option.AddItem("Blooded", 1);
        option.AddItem("Ancient", 6);

        YautjaClanAdminWindow.ApplySelectorSelection(
            option,
            new OptionButton.ItemSelectedEventArgs(6, option));

        Assert.That(option.SelectedId, Is.EqualTo(6));
    }

    [Test]
    public void ContextualTooltipIsAppliedToControl()
    {
        var field = new LineEdit();

        YautjaClanAdminWindow.ApplyTooltip(field, "cmu-yautja-clan-admin-name-tooltip");

        Assert.That(field.ToolTip, Is.EqualTo(Loc.GetString("cmu-yautja-clan-admin-name-tooltip")));
    }

    [Test]
    public void DefaultWindowSizeIsCompact()
    {
        Assert.That(YautjaClanAdminWindow.DefaultWindowSize.X, Is.LessThanOrEqualTo(760));
        Assert.That(YautjaClanAdminWindow.DefaultWindowSize.Y, Is.LessThanOrEqualTo(560));
    }

    [Test]
    public void TogglingClanRosterOpensAndCollapsesOneClan()
    {
        Assert.Multiple(() =>
        {
            Assert.That(YautjaClanAdminWindow.ToggleExpandedClan(null, 7), Is.EqualTo(7));
            Assert.That(YautjaClanAdminWindow.ToggleExpandedClan(7, 7), Is.Null);
            Assert.That(YautjaClanAdminWindow.ToggleExpandedClan(7, 8), Is.EqualTo(8));
        });
    }

    [Test]
    public void RosterScrollHeightIsBounded()
    {
        Assert.That(YautjaClanAdminWindow.RosterMaxHeight, Is.LessThanOrEqualTo(220));
    }

    [Test]
    public void ClanlessScrollHeightIsBounded()
    {
        Assert.That(YautjaClanAdminWindow.ClanlessMaxHeight, Is.LessThanOrEqualTo(220));
    }

    [Test]
    public void BoundedRosterScrollMeasuresItsContent()
    {
        var scroll = YautjaClanAdminWindow.CreateBoundedRosterScroll(180);

        Assert.Multiple(() =>
        {
            Assert.That(scroll.ReturnMeasure, Is.True);
            Assert.That(scroll.MaxHeight, Is.EqualTo(180));
        });
    }

    [Test]
    public void RosterActionTargetUsesMemberId()
    {
        var id = new NetUserId(Guid.Parse("55555555-5555-5555-5555-555555555555"));
        var member = new YautjaClanAdminMemberState(id, "Target", YautjaRank.Blooded, true);

        Assert.That(YautjaClanAdminWindow.GetRosterActionTarget(member), Is.EqualTo(id));
    }

    [Test]
    public void WhitelistClearActionFollowsSnapshotFlags()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                YautjaClanAdminWindow.CanClearWhitelist(YautjaWhitelistFlags.Yautja),
                Is.True);
            Assert.That(
                YautjaClanAdminWindow.CanClearWhitelist(YautjaWhitelistFlags.None),
                Is.False);
        });
    }
}
