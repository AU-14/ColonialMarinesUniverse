using System.Numerics;
using Content.Shared.CMU14.ForceOnForce;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Shared.CMU14;

[TestFixture]
public sealed class ForceOnForceTest
{
    [Test]
    public void ImpactMustClearEveryUnitIncludingOtherMembersOfAGroup()
    {
        Vector2[] group = [Vector2.Zero, new(9, 0), new(-3, 4)];
        Assert.That(ForceOnForceBombardment.IsSafe(new(10, 0), group, 6), Is.False);
        Assert.That(ForceOnForceBombardment.IsSafe(new(0, -8), group, 6), Is.True);
        Assert.That(ForceOnForceBombardment.IsSafe(new(0, -6), group, 6), Is.True);
        Assert.That(ForceOnForceBombardment.IsSafe(new(0, -5.99f), group, 6), Is.False);
    }

    [TestCase(ForceOnForceFallback.StayInLobby)]
    [TestCase(ForceOnForceFallback.OtherSide)]
    [TestCase(ForceOnForceFallback.OtherRole)]
    [TestCase(ForceOnForceFallback.Both)]
    public void PreferencesSurviveCloningAndStaySeparateFromOtherGamemodes(ForceOnForceFallback fallback)
    {
        var original = new HumanoidCharacterProfile { Appearance = new(Color.Black, Color.Beige, new()) }
            .WithForceOnForcePreferences(ForceOnForceSide.Opfor, fallback)
            .WithGamemodeJobPriority("forceonforce", "AU14JobOPFORSquadSergeant", JobPriority.High)
            .WithGamemodeJobPriority("DistressSignal", "AU14JobGOVFORSquadRifleman", JobPriority.High);
        var copy = new HumanoidCharacterProfile(original).WithName("Other Name");
        Assert.That(copy.FoFSide, Is.EqualTo(ForceOnForceSide.Opfor));
        Assert.That(copy.FoFFallback, Is.EqualTo(fallback));
        Assert.That(copy.GetJobPriorityForGamemode("ForceOnForce", "AU14JobOPFORSquadSergeant"), Is.EqualTo(JobPriority.High));
        Assert.That(copy.GetJobPriorityForGamemode("ForceOnForce", "AU14JobGOVFORSquadRifleman"), Is.EqualTo(JobPriority.Never));
        Assert.That(copy.GetJobPriorityForGamemode("DistressSignal", "AU14JobGOVFORSquadRifleman"), Is.EqualTo(JobPriority.High));
    }
}
