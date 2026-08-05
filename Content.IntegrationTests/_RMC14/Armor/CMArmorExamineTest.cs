using Content.Server.Verbs;
using Content.Shared._RMC14.Armor;
using Content.Shared.Verbs;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._RMC14.Armor;

[TestFixture]
[TestOf(typeof(CMArmorSystem))]
public sealed class CMArmorExamineTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: CMArmorExamineTestTarget
          components:
          - type: CMArmor
        """;

    [Test]
    public async Task DetailedExamineVerbUsesRsiStateIcon()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var target = server.EntMan.SpawnEntity("CMArmorExamineTestTarget", MapCoordinates.Nullspace);
            var verbs = server.System<VerbSystem>()
                .GetLocalVerbs(target, target, typeof(ExamineVerb), force: true);

            Assert.That(verbs, Has.Count.EqualTo(1));
            var verb = verbs.Single();
            Assert.That(verb.Icon, Is.TypeOf<SpriteSpecifier.Rsi>());

            var icon = (SpriteSpecifier.Rsi) verb.Icon!;
            Assert.Multiple(() =>
            {
                Assert.That(icon.RsiPath,
                    Is.EqualTo(new ResPath("/Textures/Interface/Actions/actions_fakemindshield.rsi")));
                Assert.That(icon.RsiState, Is.EqualTo("icon-on"));
            });
        });

        await pair.CleanReturnAsync();
    }
}
