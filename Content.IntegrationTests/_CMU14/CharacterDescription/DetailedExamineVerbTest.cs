using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.CharacterDescription;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.CharacterDescription;

[TestFixture]
public sealed class DetailedExamineVerbTest : GameTest
{
    [Test]
    public async Task DetailedExamineIsAnExamineVerbBeyondDetailsRange()
    {
        var map = await Pair.CreateTestMap();
        var previous = ServerSession!.AttachedEntity;
        try
        {
            await Server.WaitAssertion(() =>
            {
                var viewer = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                // Past the old details range (8 tiles) but within normal examine range (16 tiles).
                var target = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(12, 0)));
                SEntMan.EnsureComponent<CharacterDescriptionComponent>(target);
                Server.PlayerMan.SetAttachedEntity(ServerSession, viewer);

                var text = Loc.GetString("detailed-examine-verb-text");
                var verbs = Server.System<SharedVerbSystem>().GetLocalVerbs(target, viewer, typeof(ExamineVerb));
                var verb = verbs.OfType<ExamineVerb>().SingleOrDefault(v => v.Text == text);

                Assert.That(verb, Is.Not.Null, "detailed examine must be offered from beyond details range");
                Assert.That(verb!.Category, Is.EqualTo(VerbCategory.Examine));
                Assert.That(verb.ShowOnExamineTooltip, Is.True);
                Assert.That(verb.Icon, Is.Not.Null);
                Assert.That(verb.Disabled, Is.False);
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession, previous));
        }
    }
}
