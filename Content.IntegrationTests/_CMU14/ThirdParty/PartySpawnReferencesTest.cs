using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Intel;
using Content.Shared._RMC14.Intel.Tech;
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

    // BUG-943: the CLF commando tech pointed at the partySpawn id, so buying it silently spawned nobody
    [Test]
    public async Task TechPartySpawnsReferenceThirdParties()
    {
        await Server.WaitAssertion(() =>
        {
            var factory = SEntMan.ComponentFactory;
            foreach (var entity in SProtoMan.EnumeratePrototypes<EntityPrototype>())
            {
                if (!entity.TryGetComponent<IntelTechTreeComponent>(out var tree, factory))
                    continue;

                foreach (var option in tree.Tree.Options.SelectMany(tier => tier))
                {
                    foreach (var spawn in option.Events.OfType<TechPartySpawnEvent>())
                    {
                        Assert.That(SProtoMan.HasIndex<ThirdPartyPrototype>(spawn.ThirdPartyId), Is.True,
                            $"{entity.ID} tech '{option.Name}' spawns unknown third party '{spawn.ThirdPartyId}'");
                    }
                }
            }
        });
    }
}
