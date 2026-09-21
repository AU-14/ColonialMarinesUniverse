using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Threats;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.ThirdParty;

[TestFixture]
public sealed class PartySpawnReferencesTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [TestCase("ProfessorVonBandolierSpawn")]
    [TestCase("ProdigySurvSpawnLarge")]
    public async Task SpawnEntriesReferenceConcreteEntities(string id)
    {
        await Server.WaitAssertion(() =>
        {
            var spawn = SProtoMan.Index<PartySpawnPrototype>(id);
            foreach (var prototype in spawn.LeadersToSpawn.Keys.Concat(spawn.GruntsToSpawn.Keys).Concat(spawn.EntitiesToSpawn.Keys))
            {
                Assert.That(SProtoMan.TryIndex<EntityPrototype>(prototype, out var entity), Is.True, prototype);
                Assert.That(entity.Abstract, Is.False, prototype);
            }
        });
    }
}
