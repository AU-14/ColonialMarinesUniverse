using System.Linq;
using Content.Shared.CMU.Round;
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
        "CMUCrateVehicleAmmoLTBCannonMixed",
        "CMUCrateVehicleAmmoLTBCannonAPFSDS",
        "CMUCrateVehicleAmmoLTBCannonHEAT",
        "CMUCrateVehicleAmmoLTBCannonHE",
        "CMUCrateVehicleAmmoLTBCannonCanister",
        "CMUCrateVehicleAmmoLTBCannonNapalm",
        "RMCCrateVehicleAmmoLTAAAP",
        "RMCCrateVehicleAmmoAceAutocannon",
        "RMCCrateVehicleAmmoDragonFlamer",
        "RMCCrateVehicleAmmoBoyarsDualCannon",
        "CMUCrateVehicleAmmoGrenadeLauncherMixed",
        "RMCCrateVehicleAmmoSmokeLauncher",
        "RMCCrateVehicleAmmoTowLauncher",
        "CMUCrateVehicleAmmoCupolaMixed",
        "CMUCrateVehicleAmmoCupolaTracer",
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

    private const string AsrsProfile = "CMURoundForceAsrsUSCM";

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
                {
                    Assert.That(prototypes.TryIndex<EntityPrototype>(consoleId, out _), Is.True,
                        $"{consoleId} prototype does not exist");
                }

                AssertProfileHasVehicleAmmo(prototypes, factory, AsrsProfile);
            });
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertProfileHasVehicleAmmo(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        string profileId)
    {
        Assert.That(prototypes.TryIndex<EntityPrototype>(profileId, out var profile), Is.True,
            $"{profileId} prototype does not exist");
        Assert.That(profile!.TryComp<RoundForceAsrsProfileComponent>(out var req, factory), Is.True,
            $"{profileId} has no RoundForceAsrsProfile component");

        var vehicleAmmo = req!.Categories.FirstOrDefault(category => category.Name == VehicleAmmoCategory);
        Assert.That(vehicleAmmo, Is.Not.Null, $"{profileId} has no {VehicleAmmoCategory} category");

        var offers = vehicleAmmo!.Offers.ToDictionary(offer => offer.Crate);
        Assert.That(offers.Keys, Is.EquivalentTo(VehicleAmmoCrates),
            $"{profileId} {VehicleAmmoCategory} category does not contain the expected vehicle ammo crates");

        foreach (var crateId in VehicleAmmoCrates)
        {
            var offer = offers[crateId];
            Assert.That(offer.Stock, Is.Not.Null,
                $"{profileId} {crateId} has no stock limit");
            Assert.That(offer.Stock!.Maximum, Is.EqualTo(2),
                $"{profileId} {crateId} should have a stock limit of 2");
            Assert.That(offer.Stock.ReplenishDelay, Is.EqualTo((int) VehicleAmmoReplenishDelay.TotalSeconds),
                $"{profileId} {crateId} should replenish every 5 minutes");
        }
    }
}
