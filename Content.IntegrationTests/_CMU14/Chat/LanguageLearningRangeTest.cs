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
        await Server.WaitAssertion(() =>
        {
            var learning = Server.System<LanguageLearningSystem>();
            var speaker = SEntMan.SpawnEntity(null, map.GridCoords);
            var near = SEntMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(3, 0)));
            var far = SEntMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(30, 0)));
            learning.AddLearnableLanguage(near, Latin);
            learning.AddLearnableLanguage(far, Latin);
            // a fresh test server's clock is younger than the learning cooldown
            SEntMan.GetComponent<LanguageLearningComponent>(near).MinTimeBetweenLearning = TimeSpan.Zero;
            SEntMan.GetComponent<LanguageLearningComponent>(far).MinTimeBetweenLearning = TimeSpan.Zero;
            for (var i = 0; i < 50; i++)
                SEntMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(1, i % 5)));

            SEntMan.EventBus.RaiseEvent(EventSource.Local,
                new EntitySpokeEvent(speaker, "veni vidi vici", null, null, Latin));

            Assert.That(SEntMan.GetComponent<LanguageLearningComponent>(near).Languages[Latin].LearnedWords, Is.Not.Empty,
                "A learner within hearing range should pick up words");
            Assert.That(SEntMan.GetComponent<LanguageLearningComponent>(far).Languages[Latin].LearnedWords, Is.Empty,
                "A learner out of hearing range should not learn anything");
        });
    }
}
