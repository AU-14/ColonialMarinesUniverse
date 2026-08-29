using Content.IntegrationTests.Fixtures;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using Content.Shared.Speech.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Chat;

[TestFixture]
[TestOf(typeof(Content.Shared.Speech.EntitySystems.VocalSystem))]
public sealed class VocalInitializationTest : GameTest
{
    private static readonly object[][] ProfileVoices =
    [
        ["CMMobHuman", "Human", "RMCMaleHuman"],
        ["CMMobAvali", "Avali", "RMCMaleAvali"],
        ["CMMobArachnid", "Arachnid", "RMCUnisexArachnid"],
        ["CMMobDiona", "Diona", "UnisexDiona"],
        ["CMMobDwarf", "Dwarf", "RMCMaleDwarf"],
        ["CMMobFelinid", "Felinid", "RMCMaleFelinid"],
        ["RMCMobFeroxi", "Feroxi", "RMCMaleFeroxi"],
        ["CMMobMoth", "Moth", "RMCMaleMoth"],
        ["CMMobReptilian", "Reptilian", "RMCMaleReptilian"],
        ["CMMobRodentia", "Rodentia", "RMCMaleRodentia"],
        ["RMCMobSkrell", "Skrell", "RMCMaleSkrell"],
        ["CMMobSlimePerson", "SlimePerson", "RMCMaleSlime"],
        ["CMMobVox", "Vox", "UnisexVox"],
        ["RMCMobVulpkanin", "Vulpkanin", "RMCMaleVulpkanin"],
        ["CMUMobYautja", "Yautja", "CMUMaleYautja"],
    ];

    private static readonly object[][] SeededNonProfileVoices =
    [
        ["AU14MobWorkingJoeColony", "AU14WorkingJoeSounds"],
        ["CMMobSmallHostKobold", "MaleReptilian"],
        ["RMCMobSmallHostFarwa", "RMCMaleVulpkanin"],
        ["RMCMobSmallHostNeaera", "RMCMaleSkrell"],
        ["RMCMobSmallHostStok", "UnisexVox"],
        ["RMCMobSmallHostYiren", "RMCMaleAvali"],
        ["CMMobMouse", "Mouse"],
    ];

    [Test]
    [TestCaseSource(nameof(ProfileVoices))]
    public async Task ProfileVoiceInitializesVocalOnMapInit(
        string prototype,
        string species,
        string expectedVoice)
    {
        await Server.WaitIdleAsync();
        await Server.WaitAssertion(() =>
        {
            var uid = SEntMan.Spawn(prototype);
            try
            {
                var profile = SEntMan.GetComponent<HumanoidProfileComponent>(uid);
                var vocal = SEntMan.GetComponent<VocalComponent>(uid);
                ProtoId<EmoteSoundsPrototype> voice = expectedVoice;

                Assert.Multiple(() =>
                {
                    Assert.That(profile.Species.Id, Is.EqualTo(species), prototype);
                    Assert.That(profile.Voice, Is.EqualTo(voice), prototype);
                    Assert.That(vocal.EmoteSounds, Is.EqualTo(profile.Voice),
                        $"{prototype} must initialize Vocal from its authoritative profile voice during MapInit");
                });
            }
            finally
            {
                SEntMan.DeleteEntity(uid);
            }
        });
    }

    [Test]
    [TestCaseSource(nameof(SeededNonProfileVoices))]
    public async Task NonProfileVocalUsesExplicitSeed(string prototype, string expectedVoice)
    {
        await Server.WaitIdleAsync();
        await Server.WaitAssertion(() =>
        {
            var uid = SEntMan.Spawn(prototype);
            try
            {
                var vocal = SEntMan.GetComponent<VocalComponent>(uid);
                ProtoId<EmoteSoundsPrototype> voice = expectedVoice;

                Assert.Multiple(() =>
                {
                    Assert.That(SEntMan.HasComponent<HumanoidProfileComponent>(uid), Is.False, prototype);
                    Assert.That(vocal.EmoteSounds, Is.EqualTo(voice), prototype);
                });
            }
            finally
            {
                SEntMan.DeleteEntity(uid);
            }
        });
    }

    [Test]
    public async Task ApplyingSexSpecificProfileTransitionsVocalBank()
    {
        await Server.WaitIdleAsync();
        await Server.WaitAssertion(() =>
        {
            var uid = SEntMan.Spawn("CMMobHuman");
            try
            {
                var profiles = SEntMan.System<HumanoidProfileSystem>();
                var component = SEntMan.GetComponent<HumanoidProfileComponent>(uid);
                var vocal = SEntMan.GetComponent<VocalComponent>(uid);
                var female = HumanoidCharacterProfile.DefaultWithSpecies("Human", Sex.Female)
                    .WithVoice("RMCFemaleHuman");

                profiles.ApplyProfileTo((uid, component), female);
                Assert.Multiple(() =>
                {
                    Assert.That(component.Sex, Is.EqualTo(Sex.Female));
                    Assert.That(component.Voice.Id, Is.EqualTo("RMCFemaleHuman"));
                    Assert.That(vocal.EmoteSounds, Is.EqualTo(component.Voice));
                });

                var male = HumanoidCharacterProfile.DefaultWithSpecies("Human", Sex.Male)
                    .WithVoice("RMCMaleHuman");
                profiles.ApplyProfileTo((uid, component), male);
                Assert.Multiple(() =>
                {
                    Assert.That(component.Sex, Is.EqualTo(Sex.Male));
                    Assert.That(component.Voice.Id, Is.EqualTo("RMCMaleHuman"));
                    Assert.That(vocal.EmoteSounds, Is.EqualTo(component.Voice));
                });
            }
            finally
            {
                SEntMan.DeleteEntity(uid);
            }
        });
    }
}
