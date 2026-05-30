using Content.Server.AU14.Round;
using Content.Shared.AU14.Threats;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using NUnit.Framework;
using Robust.Shared.Prototypes;

namespace Content.Tests.Server.AU14.Round;

[TestFixture]
public sealed class AuJobSelectionTest
{
    [Test]
    public void ThreatJobEligibilityAllowsEmptyThreatPreferenceList()
    {
        var threatMember = new ProtoId<JobPrototype>("AU14JobThreatMember");
        var xeno = new ProtoId<ThreatPrototype>("XenoThreat");

        var profile = HumanoidCharacterProfile.DefaultWithSpecies()
            .WithGamemodeJobPriority("DistressSignal", threatMember, JobPriority.High);

        Assert.That(
            AuJobSelectionSystem.CanAssignThreatJob(profile, "DistressSignal", threatMember, xeno),
            Is.True);
    }

    [Test]
    public void ThreatJobEligibilityRequiresSelectedThreatPreference()
    {
        var threatMember = new ProtoId<JobPrototype>("AU14JobThreatMember");
        var abomination = new ProtoId<ThreatPrototype>("abominationsThreat");
        var xeno = new ProtoId<ThreatPrototype>("XenoThreat");

        var profile = HumanoidCharacterProfile.DefaultWithSpecies()
            .WithGamemodeJobPriority("DistressSignal", threatMember, JobPriority.High)
            .WithGamemodeThreatPreference("DistressSignal", abomination, false)
            .WithGamemodeThreatPreference("DistressSignal", xeno, true);

        Assert.That(
            AuJobSelectionSystem.CanAssignThreatJob(profile, "DistressSignal", threatMember, abomination),
            Is.False);

        Assert.That(
            AuJobSelectionSystem.CanAssignThreatJob(profile, "DistressSignal", threatMember, xeno),
            Is.True);
    }
}
