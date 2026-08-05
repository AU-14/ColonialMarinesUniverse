using Content.IntegrationTests.Fixtures;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Speech.Components;

namespace Content.IntegrationTests._RMC14.Emotes;

[TestFixture]
public sealed class RMCVocalEmoteTest : GameTest
{
    private static readonly string[] RMCSpecies =
    [
        "CMMobArachnid",
        "CMMobAvali",
        "CMMobDiona",
        "CMMobDwarf",
        "CMMobFelinid",
        "CMMobHuman",
        "CMMobMoth",
        "CMMobReptilian",
        "CMMobRodentia",
        "CMMobSlimePerson",
        "CMMobVox",
        "RMCMobFeroxi",
        "RMCMobSkrell",
        "RMCMobVulpkanin",
    ];

    [Test]
    public async Task SpeciesSpawnWithVocalSounds()
    {
        await Server.WaitIdleAsync();

        await Server.WaitAssertion(() =>
        {
            foreach (var prototype in RMCSpecies)
            {
                var mob = SEntMan.Spawn(prototype);
                var vocal = SEntMan.GetComponent<VocalComponent>(mob);
                Assert.That(vocal.EmoteSounds, Is.Not.Null,
                    $"{prototype} spawned without an emote-sounds prototype.");
                SEntMan.DeleteEntity(mob);
            }
        });
    }

    [Test]
    public async Task HumanVocalEmotesResolveSounds()
    {
        await Server.WaitIdleAsync();

        await Server.WaitAssertion(() =>
        {
            var human = SEntMan.Spawn("CMMobHuman");
            var vocal = SEntMan.GetComponent<VocalComponent>(human);

            Assert.That(vocal.EmoteSounds, Is.Not.Null,
                "CMMobHuman spawned without an emote-sounds prototype.");

            foreach (var emoteId in new[] { "Scream", "Laugh", "Cough" })
            {
                var emote = SProtoMan.Index<EmotePrototype>(emoteId);
                var ev = new EmoteEvent(emote);
                SEntMan.EventBus.RaiseLocalEvent(human, ref ev);
                Assert.That(ev.Handled, Is.True, $"{emoteId} did not resolve or play a vocal sound.");
            }

            SEntMan.DeleteEntity(human);
        });
    }
}
