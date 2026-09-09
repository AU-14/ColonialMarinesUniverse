using Content.Shared.CMU14.Intel;
using Content.Shared._RMC14.Intel;
using NUnit.Framework;

namespace Content.Tests.Shared.CMU14.Intel;

[TestFixture]
public sealed class ClaimableIntelConsoleComponentTest
{
    [Test]
    public void ClaimRequiresConfiguredTeamAndPreset()
    {
        var component = new ClaimableIntelConsoleComponent
        {
            ClaimingTeam = Team.GovFor,
            RequiredPreset = "Insurgency",
        };

        Assert.Multiple(() =>
        {
            Assert.That(component.CanStartClaim("GOVFOR"), Is.True);
            Assert.That(component.CanStartClaim(Team.CLF), Is.False);
            Assert.That(component.CanCompleteClaim(Team.GovFor, "insurgency"), Is.True);
            Assert.That(component.CanCompleteClaim(Team.GovFor, "DistressSignal"), Is.False);
        });
    }

    [Test]
    public void ClaimedConsoleCannotBeClaimedAgain()
    {
        var component = new ClaimableIntelConsoleComponent
        {
            ClaimingTeam = Team.GovFor,
            RequiredPreset = "Insurgency",
            Claimed = true,
        };

        Assert.That(component.CanCompleteClaim(Team.GovFor, "Insurgency"), Is.False);
    }
}
