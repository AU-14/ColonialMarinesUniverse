using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared._CMU14.Yautja;
using NUnit.Framework;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Tests.Shared.Preferences;

[TestFixture]
public sealed class HumanoidCharacterProfileTest
{
    [Test]
    public void GamemodeJobPrioritiesFallbackToGlobalWhenNoGamemodeOverridesExist()
    {
        var miner = new ProtoId<JobPrototype>("AU14JobCivilianMiner");

        var profile = HumanoidCharacterProfile.DefaultWithSpecies()
            .WithJobPriority(miner, JobPriority.High);

        Assert.That(profile.GetJobPriorityForGamemode("ColonyFall", miner), Is.EqualTo(JobPriority.High));
        Assert.That(profile.GetJobPrioritiesForGamemode("ColonyFall")[miner], Is.EqualTo(JobPriority.High));
    }

    [Test]
    public void GamemodeJobPrioritiesDoNotInheritGlobalOnceGamemodeOverrideExists()
    {
        var colonist = new ProtoId<JobPrototype>("AU14JobCivilianColonist");
        var miner = new ProtoId<JobPrototype>("AU14JobCivilianMiner");

        var profile = HumanoidCharacterProfile.DefaultWithSpecies()
            .WithJobPriority(miner, JobPriority.High)
            .WithGamemodeJobPriority("ColonyFall", colonist, JobPriority.Never);

        var priorities = profile.GetJobPrioritiesForGamemode("ColonyFall");

        Assert.That(profile.GetJobPriorityForGamemode("ColonyFall", miner), Is.EqualTo(JobPriority.Never));
        Assert.That(priorities.ContainsKey(miner), Is.False);
        Assert.That(priorities.ContainsKey(colonist), Is.False);
    }

    [Test]
    public void SettingGamemodeHighDoesNotCopyGlobalHighAsMedium()
    {
        var colonist = new ProtoId<JobPrototype>("AU14JobCivilianColonist");
        var miner = new ProtoId<JobPrototype>("AU14JobCivilianMiner");

        var profile = HumanoidCharacterProfile.DefaultWithSpecies()
            .WithJobPriority(miner, JobPriority.High)
            .WithGamemodeJobPriority("ColonyFall", colonist, JobPriority.High);

        var priorities = profile.GetJobPrioritiesForGamemode("ColonyFall");

        Assert.That(priorities[colonist], Is.EqualTo(JobPriority.High));
        Assert.That(profile.GetJobPriorityForGamemode("ColonyFall", miner), Is.EqualTo(JobPriority.Never));
        Assert.That(priorities.ContainsKey(miner), Is.False);
    }

    [Test]
    public void MemberwiseEqualsIncludesNestedYautjaSexAndGender()
    {
        var male = HumanoidCharacterProfile.DefaultWithSpecies()
            .WithYautjaProfile(YautjaCharacterProfile.Default);
        var female = male.WithYautjaProfile(YautjaCharacterProfile.Default.WithGender(Gender.Female));
        var femaleClone = female.WithYautjaProfile(female.YautjaProfile);

        Assert.Multiple(() =>
        {
            Assert.That(male.MemberwiseEquals(female), Is.False);
            Assert.That(female.MemberwiseEquals(femaleClone), Is.True);
        });
    }
}
