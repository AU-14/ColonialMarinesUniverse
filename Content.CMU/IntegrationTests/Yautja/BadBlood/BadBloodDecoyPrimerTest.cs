using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.util;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// FLOW-611: the marines' threat intel can be swapped for another threat's, so a Bad Blood round may read as xenos
[TestFixture]
public sealed class BadBloodDecoyPrimerTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
- type: lorePrimer
  id: CMUTestPrimerAlwaysDecoy
  KnowledgeLevelsGovforThreat:
    "The real threat.": 1
  decoyChance: 1
  decoyPrimers:
  - AU14PrimerThreatXeno
- type: lorePrimer
  id: CMUTestPrimerNeverDecoy
  KnowledgeLevelsGovforThreat:
    "The real threat.": 1
  decoyPrimers:
  - AU14PrimerThreatXeno
""";

    public override PoolSettings PoolSettings => new() { Connected = false };

    [Test]
    public async Task IntelSwapsToADecoyPrimerOnlyByChance()
    {
        await Server.WaitAssertion(() =>
        {
            var protos = Server.ProtoMan;
            var random = Server.ResolveDependency<IRobustRandom>();

            var always = protos.Index<LorePrimerPrototype>("CMUTestPrimerAlwaysDecoy");
            var never = protos.Index<LorePrimerPrototype>("CMUTestPrimerNeverDecoy");
            Assert.Multiple(() =>
            {
                Assert.That(LorePrimerDecoy.Resolve(always, protos, random).ID, Is.EqualTo("AU14PrimerThreatXeno"),
                    "a certain decoy always reads as the decoy threat");
                Assert.That(LorePrimerDecoy.Resolve(never, protos, random).ID, Is.EqualTo("CMUTestPrimerNeverDecoy"),
                    "no chance, no decoy");
            });

            // the Bad Blood primer may pass as xenos or neomorphs, and decoys still carry intel to show
            var badBlood = protos.Index<LorePrimerPrototype>("AU14PrimerThreatBadBlood");
            Assert.That(badBlood.DecoyChance, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(badBlood.DecoyPrimers, Is.EquivalentTo(new[]
            {
                new ProtoId<LorePrimerPrototype>("AU14PrimerThreatXeno"),
                new ProtoId<LorePrimerPrototype>("AU14PrimerThreatNeomorphs"),
            }));
            foreach (var decoy in badBlood.DecoyPrimers!)
            {
                Assert.That(protos.Index(decoy).KnowledgeLevels, Is.Not.Null.And.Not.Empty, $"{decoy} has intel to show");
            }

            var seen = new HashSet<string>();
            for (var i = 0; i < 400; i++)
            {
                seen.Add(LorePrimerDecoy.Resolve(badBlood, protos, random).ID);
            }

            Assert.That(seen, Does.Contain("AU14PrimerThreatBadBlood"), "most rounds keep the real intel");
            Assert.That(seen.Count, Is.GreaterThan(1), "some rounds read as another threat");
        });
    }
}
