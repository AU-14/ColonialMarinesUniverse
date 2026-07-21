using Content.Shared._RMC14.Armor;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._RMC14.Armor;

[TestFixture]
[TestOf(typeof(CMArmorSystem))]
public sealed class RMCAllowSuitStorageTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          parent: MobHuman
          id: RMCAllowSuitStorageTestHuman
          components:
          - type: RMCAllowSuitStorage
        """;

    [Test]
    public async Task DeletingWearerWithWhitelistJacketCompletesCleanly()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = server.System<InventorySystem>();
            var wearer = entMan.SpawnEntity("RMCAllowSuitStorageTestHuman", MapCoordinates.Nullspace);
            var jacket = entMan.SpawnEntity("RMCHazardVest", MapCoordinates.Nullspace);

            Assert.That(entMan.HasComponent<RMCAllowSuitStorageUserWhitelistComponent>(jacket), Is.True);

            Assert.That(inventory.TryEquip(wearer, jacket, "outerClothing", force: true), Is.True);

            entMan.DeleteEntity(wearer);

            Assert.That(entMan.Deleted(jacket), Is.True);
        });

        await pair.CleanReturnAsync();
    }
}
