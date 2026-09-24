using System.Linq;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Storage;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._CMU14;

[TestFixture]
public sealed class ReportedResourceRegressionTest
{
    [Test]
    public async Task FlyAmanitaGrindsIntoBotanicalAmatoxin()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var mushroom = entities.SpawnEntity("FoodFlyAmanita", map.GridCoords);
            Assert.That(entities.System<SharedSolutionContainerSystem>().TryGetExtractableSolution(mushroom, out _, out var solution), Is.True);
            Assert.That(solution.ContainsReagent(new("CMUAmatoxin", null)), Is.True);
            Assert.That(solution.ContainsReagent(new("Amatoxin", null)), Is.False);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HjraKitContainsItsLauncherTube()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var kit = entities.SpawnEntity("AU14KitUPPHATHJRA12", map.GridCoords);
            var storage = entities.GetComponent<StorageComponent>(kit);
            Assert.That(storage.Container.ContainedEntities.Any(uid =>
                entities.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == "RMCWeaponLauncherHJRA12"), Is.True);
        });
        await pair.CleanReturnAsync();
    }
}
