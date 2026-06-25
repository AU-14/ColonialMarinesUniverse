using System.Linq;
using Content.Shared._RMC14.Requisitions.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Requisitions;

[TestFixture]
public sealed class CMUAsrsVehicleAmmoCatalogTest
{
    private const string VehicleAmmoCategory = "Vehicle Ammo";
    private static readonly TimeSpan VehicleAmmoReplenishDelay = TimeSpan.FromMinutes(5);

    private static readonly EntProtoId[] VehicleAmmoCrates =
    [
        "RMCCrateVehicleAmmoLTBCannon",
        "RMCCrateVehicleAmmoLTAAAP",
        "RMCCrateVehicleAmmoAceAutocannon",
        "RMCCrateVehicleAmmoDragonFlamer",
        "RMCCrateVehicleAmmoBoyarsDualCannon",
        "RMCCrateVehicleAmmoGrenadeLauncher",
        "RMCCrateVehicleAmmoSmokeLauncher",
        "RMCCrateVehicleAmmoTowLauncher",
        "RMCCrateVehicleAmmoCupola",
        "RMCCrateVehicleAmmoLZRNFlamer",
        "RMCCrateVehicleAmmoFrontalCannon",
        "RMCCrateVehicleAmmoFlareLauncher",
        "RMCCrateVehicleAmmoRotaryCannon",
    ];

    private static readonly EntProtoId[] BaseAsrsConsoles =
    [
        "CMASRSConsole",
        "CMASRSConsoleColony",
    ];

    private static readonly EntProtoId[] PlatoonAsrsCatalogs =
    [
        "USCMCargoCatalog",
        "RMCCargoCatalog",
        "UPPCargoCatalog",
        "WEYUCargoCatalog",
        "VAIPOCargoCatalog",
        "ProdigyCargoCatalog",
        "LACNCargoCatalog",
        "HAZOPSCargoCatalog",
        "CMBCIUCargoCatalog",
    ];

    [Test]
    public async Task VehicleAmmoCratesAreSoldByAsrsCatalogsWithLimitedStock()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            Assert.Multiple(() =>
            {
                foreach (var crateId in VehicleAmmoCrates)
                {
                    Assert.That(prototypes.TryIndex<EntityPrototype>(crateId, out _), Is.True,
                        $"{crateId} prototype does not exist");
                }

                foreach (var consoleId in BaseAsrsConsoles)
                    AssertCatalogHasVehicleAmmo(prototypes, factory, consoleId);

                foreach (var catalogId in PlatoonAsrsCatalogs)
                    AssertCatalogHasVehicleAmmo(prototypes, factory, catalogId);
            });
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertCatalogHasVehicleAmmo(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        EntProtoId catalogId)
    {
        Assert.That(prototypes.TryIndex<EntityPrototype>(catalogId, out var catalog), Is.True,
            $"{catalogId} prototype does not exist");
        Assert.That(catalog!.TryGetComponent<RequisitionsComputerComponent>(out var req, factory), Is.True,
            $"{catalogId} has no RequisitionsComputer component");

        var vehicleAmmo = req!.Categories.FirstOrDefault(category => category.Name == VehicleAmmoCategory);
        Assert.That(vehicleAmmo, Is.Not.Null, $"{catalogId} has no {VehicleAmmoCategory} category");

        var entries = vehicleAmmo!.Entries.ToDictionary(entry => entry.Crate);
        Assert.That(entries.Keys, Is.EquivalentTo(VehicleAmmoCrates),
            $"{catalogId} {VehicleAmmoCategory} category does not contain the expected vehicle ammo crates");

        foreach (var crateId in VehicleAmmoCrates)
        {
            var entry = entries[crateId];
            Assert.That(entry.MaxStock, Is.EqualTo(2),
                $"{catalogId} {crateId} should have a stock limit of 2");
            Assert.That(entry.StockReplenishDelay, Is.EqualTo(VehicleAmmoReplenishDelay),
                $"{catalogId} {crateId} should replenish every 5 minutes");
        }
    }
}
