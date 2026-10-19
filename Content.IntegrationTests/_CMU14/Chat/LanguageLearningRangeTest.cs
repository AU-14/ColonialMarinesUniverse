using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.Language.Systems;
using Content.Shared._RMC14.Language.Components;
using Content.Shared._RMC14.Language.Prototypes;
using Content.Shared.Chat;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Chat;

// speech learning only gathers learners now instead of every entity within hearing range.
// learners in range must still pick up words, learners out of range must not, and the clutter
// that used to be scanned (here: a pile of plain entities) must not get in the way
[TestFixture, NonParallelizable]
public sealed class LanguageLearningRangeTest : GameTest
{
    private static readonly ProtoId<LanguagePrototype> Latin = "Latin";

    public override PoolSettings PoolSettings => new() { Dirty = true };

    [Test]
    public async Task OnlyLearnersInHearingRangeLearnFromSpeech()
    {
        var map = await Pair.CreateTestMap();
        var speaker = EntityUid.Invalid;
        var near = EntityUid.Invalid;
        var far = EntityUid.Invalid;
        await Server.WaitPost(() =>
        {
            var learning = Server.System<LanguageLearningSystem>();
            speaker = SEntMan.SpawnEntity(null, map.GridCoords);
            near = SEntMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(3, 0)));
            far = SEntMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(30, 0)));
            learning.AddLearnableLanguage(near, Latin);
            learning.AddLearnableLanguage(far, Latin);
            for (var i = 0; i < 50; i++)
                SEntMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(1, i % 5)));
        });

        // a fresh test server's clock is younger than the learning cooldown, so run it past that
        await RunSeconds(3);

        await Server.WaitAssertion(() =>
        {
            SEntMan.EventBus.RaiseEvent(EventSource.Local,
                new EntitySpokeEvent(speaker, "veni vidi vici", null, null, Latin));

            Assert.That(SEntMan.GetComponent<LanguageLearningComponent>(near).Languages[Latin].LearnedWords, Is.Not.Empty,
                "A learner within hearing range should pick up words");
            Assert.That(SEntMan.GetComponent<LanguageLearningComponent>(far).Languages[Latin].LearnedWords, Is.Empty,
                "A learner out of hearing range should not learn anything");
        });
    }
}
