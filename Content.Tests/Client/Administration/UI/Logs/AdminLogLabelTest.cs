using System;
using Content.Client.Administration.UI.CustomControls;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using NUnit.Framework;
using Robust.Client.UserInterface;
using Robust.Shared.IoC;
using Robust.UnitTesting;

namespace Content.Tests.Client.Administration.UI.Logs;

[TestFixture]
[TestOf(typeof(AdminLogLabel))]
public sealed class AdminLogLabelTest : RobustUnitTest
{
    public override UnitTestProject Project => UnitTestProject.Client;

    [OneTimeSetUp]
    public void Setup()
    {
        IoCManager.Resolve<IUserInterfaceManager>().InitializeTesting();
    }

    [Test]
    public void ConstructorSplitsLogMetadataFromMessage()
    {
        var player = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var log = new SharedAdminLog(
            12,
            LogType.Damaged,
            LogImpact.High,
            new DateTime(2026, 6, 7, 13, 2, 11),
            "Hudson took [color=red]32 brute[/color].",
            new[] { player });
        var separator = new HSeparator();

        var control = new AdminLogLabel(ref log, separator);

        Assert.Multiple(() =>
        {
            Assert.That(control.Log, Is.EqualTo(log));
            Assert.That(control.TimeText, Is.EqualTo("13:02:11"));
            Assert.That(control.ImpactText, Is.EqualTo("High"));
            Assert.That(control.TypeText, Is.EqualTo("Damaged"));
        });
    }

    [Test]
    public void VisibilityKeepsSeparatorInSync()
    {
        var log = new SharedAdminLog(
            13,
            LogType.Action,
            LogImpact.Medium,
            new DateTime(2026, 6, 7, 13, 3, 0),
            "Johnson attacked Drone with M41A.",
            Array.Empty<Guid>());
        var separator = new HSeparator();
        var control = new AdminLogLabel(ref log, separator);

        control.Visible = false;

        Assert.That(separator.Visible, Is.False);

        control.Visible = true;

        Assert.That(separator.Visible, Is.True);
    }
}
