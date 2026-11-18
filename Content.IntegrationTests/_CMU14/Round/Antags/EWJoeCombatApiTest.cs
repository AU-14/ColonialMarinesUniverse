using Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared.CMU14.Threats;
using Content.Shared.Humanoid;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._CMU14.Round.Antags;

/// <summary>
/// COUPLING-03 (node N2): the Joe's close-quarters effects are callable without their actions, and the
/// shared xenofauna rule (R1) answers the basic cases.
/// </summary>
[TestFixture]
public sealed class EWJoeCombatApiTest
{
    private const string ColonyJoe = "AU14MobWorkingJoeColony";
    private const string Human = "CMMobHuman";
    private const string Xeno = "CMXenoDrone";

    [Test]
    public async Task SweepCalledDirectlyKnocksDown()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var combat = entities.System<EWJoeCombatSystem>();
            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            var comp = entities.EnsureComponent<EWJoeComponent>(joe);
            var victim = entities.SpawnEntity(Human, map.GridCoords);

            Assert.That(entities.HasComponent<KnockedDownComponent>(victim), Is.False, "standing before the sweep");

            combat.Sweep((joe, comp), victim);

            Assert.That(entities.HasComponent<KnockedDownComponent>(victim), "victim knocked down by the API call");

            // the choke API refuses a target that is not in the Joe's grab, and starts nothing
            Assert.That(combat.StartChoke((joe, comp), victim), Is.False, "no choke without an aggressive grab");
            Assert.That(entities.HasComponent<EWJoeChokeComponent>(joe), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task IsXenofaunaTruthTable()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var xeno = entities.SpawnEntity(Xeno, map.GridCoords);
            var human = entities.SpawnEntity(Human, map.GridCoords);

            // a threat-faction body with no humanoid profile (ape, biomorph, neomorph, wendigo)
            var beast = entities.SpawnEntity(null, map.GridCoords);
            entities.EnsureComponent<ThreatComponent>(beast);

            // a humanoid threat (cultist, tribal) is not xenofauna
            var cultist = entities.SpawnEntity(Human, map.GridCoords);
            entities.EnsureComponent<ThreatComponent>(cultist);

            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<HumanoidProfileComponent>(beast), Is.False, "the beast has no humanoid profile");
                Assert.That(entities.HasComponent<HumanoidProfileComponent>(cultist), "the cultist stand-in is humanoid");

                Assert.That(EWJoeTargetRules.IsXenofauna(entities, xeno), Is.True, "a xeno is xenofauna");
                Assert.That(EWJoeTargetRules.IsXenofauna(entities, human), Is.False, "a human is not xenofauna");
                Assert.That(EWJoeTargetRules.IsXenofauna(entities, beast), Is.True, "a non-humanoid threat is xenofauna");
                Assert.That(EWJoeTargetRules.IsXenofauna(entities, cultist), Is.False, "a humanoid threat is not xenofauna");
            });
        });

        await pair.CleanReturnAsync();
    }
}
