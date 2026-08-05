using Content.Shared._CMU14.Threats;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using NUnit.Framework;
using Robust.Shared.Prototypes;

namespace Content.Tests.Shared.Preferences.Humanoid;

[TestFixture]
[TestOf(typeof(HumanoidCharacterProfile))]
public sealed class GamemodePreferenceEqualityTest
{
    private const string Gamemode = "Insurgency";

    [Test]
    public void GamemodeJobPriorityMarksProfileAsChanged()
    {
        var original = new HumanoidCharacterProfile();
        var changed = original.WithGamemodeJobPriority(
            Gamemode,
            new ProtoId<JobPrototype>("CMUJobTest"),
            JobPriority.High);

        Assert.That(original.MemberwiseEquals(changed), Is.False);
    }

    [Test]
    public void GamemodeAntagPreferenceMarksProfileAsChanged()
    {
        var original = new HumanoidCharacterProfile();
        var changed = original.WithGamemodeAntagPreference(
            Gamemode,
            new ProtoId<AntagPrototype>("CMUAntagTest"),
            true);

        Assert.That(original.MemberwiseEquals(changed), Is.False);
    }

    [Test]
    public void GamemodeThreatPreferenceMarksProfileAsChanged()
    {
        var original = new HumanoidCharacterProfile();
        var changed = original.WithGamemodeThreatPreference(
            Gamemode,
            new ProtoId<ThreatPrototype>("CMUThreatTest"),
            true);

        Assert.That(original.MemberwiseEquals(changed), Is.False);
    }
}
