using Content.Shared.CMU14.Threats;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class SurvivorPartySpawnRegressionTest
{
    [TestCase("CLFSurvsSpawnSmall")]
    [TestCase("CLFSurvsSpawnMedium")]
    [TestCase("CLFSurvsSpawnBig")]
    public async Task SurvivorPartyMembersAreSpawnableEntities(string partyId)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();
            var party = prototypes.Index<PartySpawnPrototype>(partyId);
            foreach (var members in new[] { party.LeadersToSpawn, party.GruntsToSpawn })
            {
                foreach (var id in members.Keys)
                {
                    Assert.That(prototypes.TryIndex<EntityPrototype>(id, out var prototype), Is.True,
                        $"{partyId} must name entity prototypes, not job prototypes: {id}");
                    Assert.That(prototype.Abstract, Is.False);
                    var entity = pair.Server.EntMan.SpawnEntity(id, MapCoordinates.Nullspace);
                    Assert.That(pair.Server.EntMan.GetComponent<MetaDataComponent>(entity).EntityPrototype!.ID, Is.EqualTo(id));
                }
            }
        });
        await pair.CleanReturnAsync();
    }
}
