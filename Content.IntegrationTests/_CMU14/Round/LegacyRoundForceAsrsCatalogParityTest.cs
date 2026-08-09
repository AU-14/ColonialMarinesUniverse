#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Content.Shared.AU14.util;
using Content.Shared.CMU.Round;
using Content.Shared.Storage.Components;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.UnitTesting.Pool;
using YamlDotNet.RepresentationModel;

namespace Content.IntegrationTests._CMU14.Round;

[TestFixture]
public sealed class LegacyRoundForceAsrsCatalogParityTest
{
    private const string CommonPouchCrate = "RMCCrateClothingMagazinePouchesLarge";
    private const string WeyuPouchCrate = "RMCCrateClothingMagazinePouchesLargePMC";
    private static readonly ResPath SharedCatalogSource =
        new("/Prototypes/_CMU14/Entities/Structures/Machines/corporate_asrs.yml");

    private static readonly ExpectedCatalog[] ExpectedCatalogs =
    [
        new("USCM", "USCMCargoCatalog", "uscm_requisitions_catalog.yml", 18, 162, "0D66108DC346DF36F40370669ACA2C6BBC285FD10A3F40FF37F65281B6CACAE0"),
        new("LACN", "LACNCargoCatalog", "lacn_requisitions_catalog.yml", 18, 161, "3ECE5637CF002596D71E904AE8355C2DCF6F2651C16EDE0A60E1CBA053D6A5AF"),
        new("UPP", "UPPCargoCatalog", "upp_requisitions_catalog.yml", 18, 163, "899372798402AD8858699D4AB68D46B006CB454E7A3BD92A2BB5B7E453A95150"),
        new("WEYU", "WEYUCargoCatalog", "weyu_requisitions_catalog.yml", 18, 159, "E6A4CC2CB0A2D261DE81B4CEE3C4831AB2C4BF73ABDC4DDEEDE4305421E5661D"),
        new("CMBCIU", "CMBCIUCargoCatalog", "cmbciu_requisitions_catalog.yml", 18, 158, "329171CABBE4B21057B9434D0566E6E690F74B41FC5B6C5F118B79019C65E927"),
        new("HAZOPS", "HAZOPSCargoCatalog", "hazops_requisitions_catalog.yml", 18, 158, "3773DEB3F8A087C406B0F4D5B6871DC3D2B220B8576343B98F052854FD2F651E"),
        new("ProdigySF", "ProdigyCargoCatalog", "prodigy_requisitions_catalog.yml", 18, 158, "1D2CB39C41F5B0B1E8A7C0A7743FDF27341529E6CB1CDEED0D44EC75EF9F862A"),
        new("VAIPO", "VAIPOCargoCatalog", "vaipo_requisitions_catalog.yml", 18, 159, "BC337693C906CE4444D87AF062D7299CA7E30F8C3748C170BA7EA8DB8857EBE7"),
        new("RMC", "RMCCargoCatalog", "rmc_requisitions_catalog.yml", 18, 160, "9A8090FBFC97334D08863705A09DD05B87411C2C97AABBE4F3607A462DE6CE6E"),
    ];

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> ExpectedPouchCrates =
        new Dictionary<string, IReadOnlyDictionary<string, int>>
        {
            [CommonPouchCrate] = new Dictionary<string, int>
            {
                ["RMCPouchMagazineLarge"] = 2,
                ["RMCPouchMagazinePistolLarge"] = 2,
            },
            [WeyuPouchCrate] = new Dictionary<string, int>
            {
                ["RMCPouchMagazineLargePMC"] = 2,
                ["RMCPouchMagazinePistolLarge"] = 2,
            },
        };

    [Test]
    public async Task EveryLegacyProductionForceProfileMatchesOrderedSource()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;
            var resources = server.ResolveDependency<IResourceManager>();
            var mismatches = new List<string>();
            var sharedRoot = LoadRoot(resources, SharedCatalogSource);

            // Global test prototypes omit the legacy vendor-set boundary used by every production force.
            Assert.That(
                ExpectedCatalogs.Select(expected => expected.ForceId),
                Is.EquivalentTo(prototypes.EnumeratePrototypes<PlatoonPrototype>()
                    .Where(platoon => platoon.VendorSet != null)
                    .Select(platoon => platoon.ID)),
                "Ordered ASRS parity sources must cover every legacy production force");

            foreach (var expected in ExpectedCatalogs)
            {
                var source = new ResPath(
                    $"/Prototypes/_CMU14/Economy/Catalog/Cargo/{expected.SourceFile}");
                var sourceRoot = LoadRoot(resources, source);
                var legacy = LoadCategories(sourceRoot, expected.CatalogId)
                    .Concat(LoadInheritedCategories(
                        sharedRoot,
                        LoadParents(sourceRoot, expected.CatalogId)))
                    .ToArray();
                var legacyLines = BuildParityLines(legacy);
                var legacyDigest = ComputeAuditDigest(legacy);

                var profileId = $"CMURoundForceAsrs{expected.ForceId}";
                if (!prototypes.TryIndex<EntityPrototype>(profileId, out var profileEntity) ||
                    !profileEntity.TryComp<RoundForceAsrsProfileComponent>(out var profile, factory))
                {
                    mismatches.Add($"{expected.ForceId}: missing profile {profileId}");
                    continue;
                }

                var compiled = ToSnapshot(RoundForceAsrsProfileCompiler.Compile(profile));
                var compiledLines = BuildParityLines(compiled);
                var compiledDigest = ComputeAuditDigest(compiled);
                var legacyOffers = legacy.Sum(category => category.Offers.Length);
                var compiledOffers = compiled.Sum(category => category.Offers.Length);

                if (legacy.Length == expected.CategoryCount &&
                    legacyOffers == expected.OfferCount &&
                    legacyDigest == expected.Digest &&
                    compiledLines.SequenceEqual(legacyLines, StringComparer.Ordinal))
                {
                    continue;
                }

                mismatches.Add(
                    $"{expected.ForceId}: legacy={legacy.Length}/{legacyOffers}/{legacyDigest}, " +
                    $"compiled={compiled.Length}/{compiledOffers}/{compiledDigest}; " +
                    FindFirstDifference(legacyLines, compiledLines));
            }

            Assert.That(
                mismatches,
                Is.Empty,
                $"Compiled ASRS profiles differ from their ordered legacy sources:{Environment.NewLine}" +
                string.Join(Environment.NewLine, mismatches));
        });

        await pair.CleanReturnAsync();
    }

    private static CatalogCategory[] LoadCategories(YamlSequenceNode root, string catalogId)
    {
        var catalog = FindEntity(root, catalogId);
        var requisitions = ((YamlSequenceNode) catalog.GetNode("components"))
            .Children
            .Cast<YamlMappingNode>()
            .Single(node => node.GetNode("type").AsString() == "RequisitionsComputer");

        return ((YamlSequenceNode) requisitions.GetNode("categories"))
            .Children
            .Cast<YamlMappingNode>()
            .Select(category =>
            {
                var name = category.GetNode("name").AsString();
                var offers = ((YamlSequenceNode) category.GetNode("entries"))
                    .Children
                    .Cast<YamlMappingNode>()
                    .Select(entry =>
                    {
                        var hasMaximum = entry.TryGetNode<YamlScalarNode>("maxStock", out var maximum);
                        var hasDelay = entry.TryGetNode<YamlScalarNode>("stockReplenishDelay", out var delay);
                        Assert.That(
                            hasMaximum,
                            Is.EqualTo(hasDelay),
                            $"{catalogId} {name}/{entry.GetNode("crate").AsString()} has incomplete stock metadata");

                        var stock = hasMaximum
                            ? new CatalogStock(maximum!.AsInt(), delay!.AsInt(), -1, 1)
                            : null;
                        return new CatalogOffer(
                            entry.GetNode("crate").AsString(),
                            entry.GetNode("cost").AsInt(),
                            stock);
                    })
                    .ToArray();
                return new CatalogCategory(name.Replace(" ", string.Empty), name, offers);
            })
            .ToArray();
    }

    private static string[] LoadParents(YamlSequenceNode root, string catalogId)
    {
        var entity = FindEntity(root, catalogId);
        if (!entity.TryGetNode<YamlNode>("parent", out var parentNode))
            return [];

        return parentNode switch
        {
            YamlScalarNode scalar => [scalar.AsString()],
            YamlSequenceNode sequence => sequence.Children
                .Cast<YamlScalarNode>()
                .Select(parent => parent.AsString())
                .ToArray(),
            var node => throw new InvalidDataException(
                $"Catalog {catalogId} has unsupported parent node {node.NodeType}."),
        };
    }

    private static CatalogCategory[] LoadInheritedCategories(
        YamlSequenceNode root,
        IEnumerable<string> parentIds)
    {
        var categories = new List<CatalogCategory>();
        foreach (var parentId in parentIds)
        {
            Assert.That(
                LoadParents(root, parentId),
                Is.Empty,
                $"Legacy ASRS parent {parentId} gained another inheritance level; expand the parity loader");
            categories.AddRange(LoadCategories(root, parentId));
        }

        return categories.ToArray();
    }

    private static YamlMappingNode FindEntity(YamlSequenceNode root, string prototypeId)
    {
        return root.Children
            .Cast<YamlMappingNode>()
            .Single(node =>
                node.GetNode("type").AsString() == "entity" &&
                node.GetNode("id").AsString() == prototypeId);
    }

    private static CatalogCategory[] ToSnapshot(ResolvedRoundAsrsCatalog catalog)
    {
        return catalog.Categories
            .Select(category => new CatalogCategory(
                category.Id.Value!,
                category.Name,
                category.Offers
                    .Select(offer => new CatalogOffer(
                        offer.Crate.Id,
                        offer.Cost,
                        offer.Stock is { } stock
                            ? new CatalogStock(
                                stock.Maximum,
                                (int) stock.ReplenishDelay.TotalSeconds,
                                stock.StartingStock,
                                stock.ReplenishAmount)
                            : null))
                    .ToArray()))
            .ToArray();
    }

    private static string[] BuildParityLines(CatalogCategory[] categories)
    {
        var lines = new List<string>();
        for (var categoryIndex = 0; categoryIndex < categories.Length; categoryIndex++)
        {
            var category = categories[categoryIndex];
            lines.Add($"C|{categoryIndex}|{category.Id}|{category.Name}|{category.Offers.Length}");
            for (var offerIndex = 0; offerIndex < category.Offers.Length; offerIndex++)
            {
                var offer = category.Offers[offerIndex];
                var stock = offer.Stock;
                lines.Add(
                    $"O|{categoryIndex}|{offerIndex}|{offer.Crate}|{offer.Cost}|" +
                    $"{stock?.Maximum.ToString() ?? "-"}|" +
                    $"{stock?.ReplenishDelaySeconds.ToString() ?? "-"}|" +
                    $"{stock?.StartingStock.ToString() ?? "-"}|" +
                    $"{stock?.ReplenishAmount.ToString() ?? "-"}");
            }
        }

        return lines.ToArray();
    }

    private static string ComputeAuditDigest(CatalogCategory[] categories)
    {
        var lines = new List<string>();
        for (var categoryIndex = 0; categoryIndex < categories.Length; categoryIndex++)
        {
            var category = categories[categoryIndex];
            for (var offerIndex = 0; offerIndex < category.Offers.Length; offerIndex++)
            {
                var offer = category.Offers[offerIndex];
                lines.Add(
                    $"{categoryIndex}|{category.Name}|{offerIndex}|{offer.Crate}|{offer.Cost}|" +
                    $"{offer.Stock?.Maximum.ToString() ?? "-"}|" +
                    $"{offer.Stock?.ReplenishDelaySeconds.ToString() ?? "-"}");
            }
        }

        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', lines))));
    }

    private static string FindFirstDifference(string[] expected, string[] actual)
    {
        var sharedLength = Math.Min(expected.Length, actual.Length);
        for (var index = 0; index < sharedLength; index++)
        {
            if (expected[index] != actual[index])
                return $"line {index}: expected '{expected[index]}', actual '{actual[index]}'";
        }

        return $"line count differs: expected {expected.Length}, actual {actual.Length}";
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

    [Test]
    public async Task IntendedPouchOffersKeepDistinctCratesAndContents()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            Assert.Multiple(() =>
            {
                foreach (var (crateId, expectedContents) in ExpectedPouchCrates)
                {
                    Assert.That(
                        prototypes.TryIndex<EntityPrototype>(crateId, out var crate),
                        Is.True,
                        $"Missing intended ASRS pouch crate {crateId}");
                    Assert.That(
                        crate!.TryComp<StorageFillComponent>(out var storage, factory),
                        Is.True,
                        $"{crateId} has no StorageFill component");

                    var actualContents = storage!.Contents
                        .Where(entry => entry.PrototypeId != null)
                        .ToDictionary(entry => entry.PrototypeId!.Value, entry => entry.Amount);
                    Assert.That(actualContents, Has.Count.EqualTo(expectedContents.Count),
                        $"{crateId} changed its intended item count");
                    foreach (var (itemId, amount) in expectedContents)
                    {
                        Assert.That(actualContents.TryGetValue(itemId, out var actualAmount), Is.True,
                            $"{crateId} is missing {itemId}");
                        Assert.That(actualAmount, Is.EqualTo(amount),
                            $"{crateId} should contain {amount}x {itemId}");
                    }

                    foreach (var entry in storage.Contents)
                    {
                        Assert.That(entry.SpawnProbability, Is.EqualTo(1),
                            $"{crateId}/{entry.PrototypeId} probability");
                        Assert.That(entry.MaxAmount, Is.EqualTo(1),
                            $"{crateId}/{entry.PrototypeId} maximum");
                        Assert.That(entry.GroupId, Is.Null,
                            $"{crateId}/{entry.PrototypeId} group");
                    }
                }
            });
        });

        await pair.CleanReturnAsync();
    }

    private sealed record ExpectedCatalog(
        string ForceId,
        EntProtoId CatalogId,
        string SourceFile,
        int CategoryCount,
        int OfferCount,
        string Digest);

    private sealed record CatalogCategory(
        string Id,
        string Name,
        CatalogOffer[] Offers);

    private sealed record CatalogOffer(
        string Crate,
        int Cost,
        CatalogStock? Stock);

    private sealed record CatalogStock(
        int Maximum,
        int ReplenishDelaySeconds,
        int StartingStock,
        int ReplenishAmount);
}
