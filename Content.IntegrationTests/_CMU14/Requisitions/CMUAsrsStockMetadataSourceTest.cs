#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using YamlDotNet.RepresentationModel;

namespace Content.IntegrationTests._CMU14.Requisitions;

[TestFixture]
public sealed class CMUAsrsStockMetadataSourceTest
{
    private static readonly (string CatalogId, ResPath Source)[] ForceCatalogs =
    [
        ("CMBCIUCargoCatalog", new ResPath("/Prototypes/_CMU14/Economy/Catalog/Cargo/cmbciu_requisitions_catalog.yml")),
        ("HAZOPSCargoCatalog", new ResPath("/Prototypes/_CMU14/Economy/Catalog/Cargo/hazops_requisitions_catalog.yml")),
        ("LACNCargoCatalog", new ResPath("/Prototypes/_CMU14/Economy/Catalog/Cargo/lacn_requisitions_catalog.yml")),
        ("ProdigyCargoCatalog", new ResPath("/Prototypes/_CMU14/Economy/Catalog/Cargo/prodigy_requisitions_catalog.yml")),
        ("RMCCargoCatalog", new ResPath("/Prototypes/_CMU14/Economy/Catalog/Cargo/rmc_requisitions_catalog.yml")),
        ("UPPCargoCatalog", new ResPath("/Prototypes/_CMU14/Economy/Catalog/Cargo/upp_requisitions_catalog.yml")),
        ("USCMCargoCatalog", new ResPath("/Prototypes/_CMU14/Economy/Catalog/Cargo/uscm_requisitions_catalog.yml")),
        ("VAIPOCargoCatalog", new ResPath("/Prototypes/_CMU14/Economy/Catalog/Cargo/vaipo_requisitions_catalog.yml")),
        ("WEYUCargoCatalog", new ResPath("/Prototypes/_CMU14/Economy/Catalog/Cargo/weyu_requisitions_catalog.yml")),
    ];

    private static readonly ResPath SharedCatalogSource =
        new("/Prototypes/_CMU14/Entities/Structures/Machines/corporate_asrs.yml");

    private static readonly StockOffer[] ExpectedForceStockOffers =
    [
        new("Medical", "AU14CrateBoxDefibrillator", 600, 4, 600),
        new("Medical", "CMUCrateMedicalFieldTreatments", 850, 2, 480),
        new("Vehicle Ammo", "CMUCrateVehicleAmmoCupolaMixed", 1025, 2, 300),
        new("Vehicle Ammo", "CMUCrateVehicleAmmoCupolaTracer", 1050, 2, 300),
        new("Vehicle Ammo", "CMUCrateVehicleAmmoGrenadeLauncherMixed", 900, 2, 300),
        new("Vehicle Ammo", "CMUCrateVehicleAmmoLTBCannonMixed", 1900, 2, 300),
        new("Vehicle Ammo", "RMCCrateVehicleAmmoAceAutocannon", 1800, 2, 300),
        new("Vehicle Ammo", "RMCCrateVehicleAmmoBoyarsDualCannon", 1800, 2, 300),
        new("Vehicle Ammo", "RMCCrateVehicleAmmoDragonFlamer", 1700, 2, 300),
        new("Vehicle Ammo", "RMCCrateVehicleAmmoFlareLauncher", 700, 2, 300),
        new("Vehicle Ammo", "RMCCrateVehicleAmmoFrontalCannon", 1000, 2, 300),
        new("Vehicle Ammo", "RMCCrateVehicleAmmoLTAAAP", 1800, 2, 300),
        new("Vehicle Ammo", "RMCCrateVehicleAmmoLZRNFlamer", 1200, 2, 300),
        new("Vehicle Ammo", "RMCCrateVehicleAmmoRotaryCannon", 1800, 2, 300),
        new("Vehicle Ammo", "RMCCrateVehicleAmmoSmokeLauncher", 700, 2, 300),
        new("Vehicle Ammo", "RMCCrateVehicleAmmoTowLauncher", 2200, 2, 300),
    ];

    private static readonly StockOffer[] ExpectedSharedStockOffers =
    [
        new("Research", "CMUExoticCubeCrate", 8000, 1, 1800),
        new("Research", "CMUMonkeyCubeCrate", 4000, 1, 1800),
    ];

    [Test]
    public async Task EveryForceRetainsTheCommonStockPolicy()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var resources = server.ResolveDependency<IResourceManager>();
            foreach (var (catalogId, source) in ForceCatalogs)
            {
                Assert.That(
                    LoadStockOffers(resources, source, catalogId),
                    Is.EqualTo(ExpectedForceStockOffers),
                    catalogId);
            }

            Assert.That(
                LoadStockOffers(resources, SharedCatalogSource, "CMUASRSResearchGoodies"),
                Is.EqualTo(ExpectedSharedStockOffers),
                "shared Research stock policy");
        });

        await pair.CleanReturnAsync();
    }

    private static StockOffer[] LoadStockOffers(
        IResourceManager resources,
        ResPath source,
        string catalogId)
    {
        var root = LoadRoot(resources, source);
        var catalog = root.Children
            .Cast<YamlMappingNode>()
            .Single(node =>
                node.GetNode("type").AsString() == "entity" &&
                node.GetNode("id").AsString() == catalogId);
        var requisitions = ((YamlSequenceNode) catalog.GetNode("components"))
            .Children
            .Cast<YamlMappingNode>()
            .Single(node => node.GetNode("type").AsString() == "RequisitionsComputer");
        var offers = new List<StockOffer>();

        foreach (var category in ((YamlSequenceNode) requisitions.GetNode("categories"))
                     .Children
                     .Cast<YamlMappingNode>())
        {
            var categoryName = category.GetNode("name").AsString();
            foreach (var entry in ((YamlSequenceNode) category.GetNode("entries"))
                         .Children
                         .Cast<YamlMappingNode>())
            {
                var hasMaximum = entry.TryGetNode<YamlScalarNode>("maxStock", out var maximum);
                var hasDelay = entry.TryGetNode<YamlScalarNode>("stockReplenishDelay", out var delay);
                Assert.That(
                    hasMaximum,
                    Is.EqualTo(hasDelay),
                    $"{catalogId} {categoryName}/{entry.GetNode("crate").AsString()} must pair maxStock and stockReplenishDelay");
                if (!hasMaximum)
                    continue;

                offers.Add(new StockOffer(
                    categoryName,
                    entry.GetNode("crate").AsString(),
                    entry.GetNode("cost").AsInt(),
                    maximum!.AsInt(),
                    delay!.AsInt()));
            }
        }

        return offers
            .OrderBy(offer => offer.Category, StringComparer.Ordinal)
            .ThenBy(offer => offer.Crate, StringComparer.Ordinal)
            .ToArray();
    }

    private static YamlSequenceNode LoadRoot(IResourceManager resources, ResPath source)
    {
        using var stream = resources.ContentFileRead(source);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var yaml = new YamlStream();
        yaml.Load(reader);

        Assert.That(yaml.Documents, Has.Count.EqualTo(1), source.ToString());
        return (YamlSequenceNode) yaml.Documents[0].RootNode;
    }

    private readonly record struct StockOffer(
        string Category,
        string Crate,
        int Cost,
        int Maximum,
        int ReplenishDelaySeconds);
}
