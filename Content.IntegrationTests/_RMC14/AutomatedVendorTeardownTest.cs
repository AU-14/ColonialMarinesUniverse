using Content.Shared._RMC14.Deploy;
using Content.Shared._RMC14.Vendors;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class AutomatedVendorTeardownTest
{
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task DeployableDestructionEjectsStockOnlyOntoLiveParent(bool deletingMap, bool onGrid)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var coords = onGrid ? map.GridCoords : new EntityCoordinates(map.MapUid, 100, 100);
            var deployable = entMan.SpawnEntity(null, coords);
            var deployment = entMan.EnsureComponent<RMCDeployableComponent>(deployable);
#pragma warning disable RA0002 // Seed an already-deployed setup to isolate the real shutdown path.
            deployment.DeploySetups.Add(new RMCDeploySetup
            {
                Prototype = "CMVendorMedical",
                Mode = RMCDeploySetupMode.Reactive,
            });

            var vendor = entMan.SpawnEntity(null, coords);
            var stock = entMan.EnsureComponent<CMAutomatedVendorComponent>(vendor);
            stock.EjectContentsOnDestruction = true;
            stock.Sections.Add(new CMVendorSection
            {
                Name = "Test",
                Entries = [new CMVendorEntry { Id = "CMTricordrazineAutoInjector", Amount = 2 }],
            });
            var deployed = entMan.EnsureComponent<RMCDeployedEntityComponent>(vendor);
            deployed.OriginalEntity = deployable;
            deployed.SetupIndex = 0;
#pragma warning restore RA0002

            var spawnedStock = new List<EntityUid>();
            void OnEntityInitialized(Entity<MetaDataComponent> entity)
            {
                if (entity.Comp.EntityPrototype?.ID == "CMTricordrazineAutoInjector")
                    spawnedStock.Add(entity.Owner);
            }

            entMan.EntityInitialized += OnEntityInitialized;
            try
            {
                // Map deletion reaches the same deployable shutdown -> vendor destruction path
                // as round cleanup, with the vendor's coordinate parent already terminating.
                entMan.DeleteEntity(deletingMap ? map.MapUid : deployable);
                Assert.That(spawnedStock, Has.Count.EqualTo(deletingMap ? 0 : 2));
                if (!deletingMap)
                {
                    foreach (var item in spawnedStock)
                        Assert.That(entMan.GetComponent<TransformComponent>(item).MapUid, Is.EqualTo(map.MapUid));
                }
            }
            finally
            {
                entMan.EntityInitialized -= OnEntityInitialized;
            }
        });

        await pair.CleanReturnAsync();
    }
}
