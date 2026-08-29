using Content.Shared.Body;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using NUnit.Framework;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.Tests.Shared.Preferences;

[TestFixture]
[NonParallelizable]
public sealed class HumanoidCharacterProfileTest : ContentUnitTest
{
    protected override System.Type[] ExtraComponents => [typeof(InitialBodyComponent)];

    private const string ProfilePrototypes = """
        - type: entity
          id: TestHuman

        - type: skinColoration
          id: HumanToned
          strategy: !type:HumanTonedSkinColoration {}

        - type: species
          id: Human
          name: test-species-human
          roundStart: true
          prototype: TestHuman
          dollPrototype: TestHuman
          skinColoration: HumanToned
        """;

    [OneTimeSetUp]
    public void InitializeProfilePrototypes()
    {
        IoCManager.Resolve<ISerializationManager>().Initialize();

        var prototypeManager = IoCManager.Resolve<IPrototypeManager>();
        prototypeManager.Initialize();
        prototypeManager.LoadString(ProfilePrototypes);
        prototypeManager.ResolveResults();
    }

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
}
