#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Content.Shared._RMC14.Requisitions.Components;
using Robust.Shared.Prototypes;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests._CMU14.Round;

[TestFixture]
public sealed class LegacyRoundForceAsrsCatalogParityTest
{
    private static readonly ExpectedCatalog[] ExpectedCatalogs =
    [
        new("USCM", "USCMCargoCatalog", 18, 162, "34582B81F690A54DE8151FB428857CB8DD005D9FB9081DC4DFEAD7FAE1767D71"),
        new("LACN", "LACNCargoCatalog", 18, 161, "94C59F450FD86ACCC06A4030FE5BB5A0518B3FDC85D075E364EEB07B80AAF9C7"),
        new("UPP", "UPPCargoCatalog", 18, 163, "5F8CED501A996BBCE857271CBD754805A03ADFFF867D119CBBEC553F2AD76CF1"),
        new("WEYU", "WEYUCargoCatalog", 18, 159, "BE54A35490E11B2E318DC9C35DE765DE7F05015D0F6CFACFCE96258D352F3F2F"),
        new("CMBCIU", "CMBCIUCargoCatalog", 18, 158, "3748D02ACF405EEBCCF2920C68BA03D35776FBB89294CD5429821DFCA496B689"),
        new("HAZOPS", "HAZOPSCargoCatalog", 18, 158, "BB784CD3D0F0A375AEF650A346AEED7C1A71C432ED9463D69F1ACCE39ACD4000"),
        new("ProdigySF", "ProdigyCargoCatalog", 18, 158, "1C85E5E161F36EA1C8B892D1B53D91D797B135DF33665B5D277714E6A42158DC"),
        new("VAIPO", "VAIPOCargoCatalog", 18, 159, "EAC629256AA34AAE45CDC9A535DD8A0488AFE343A168803D7D18F9FFB2117342"),
        new("RMC", "RMCCargoCatalog", 18, 160, "3D809151C0158CAEDDCC4ACEF966E2CDD6B7CCACD00F5BF43B084D69AD40DAB6"),
    ];

    [Test]
    public async Task EverySelectableForceKeepsItsIntendedAsrsOffersAndPrices()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;
            var mismatches = new List<string>();

            foreach (var expected in ExpectedCatalogs)
            {
                Assert.That(
                    prototypes.TryIndex<EntityPrototype>(expected.CatalogId, out var catalog),
                    Is.True,
                    $"{expected.ForceId} catalog {expected.CatalogId} does not exist");
                Assert.That(
                    catalog!.TryGetComponent<RequisitionsComputerComponent>(out var requisitions, factory),
                    Is.True,
                    $"{expected.ForceId} catalog {expected.CatalogId} has no RequisitionsComputer component");

                var offers = requisitions!.Categories
                    .SelectMany(category => category.Entries.Select(entry =>
                        $"{category.Name}\t{entry.Crate}\t{entry.Cost}"))
                    .Order(StringComparer.Ordinal)
                    .ToArray();
                var digest = Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', offers))));

                if (requisitions.Categories.Count == expected.CategoryCount &&
                    offers.Length == expected.OfferCount &&
                    digest == expected.Digest)
                {
                    continue;
                }

                mismatches.Add(
                    $"{expected.ForceId}/{expected.CatalogId}: categories={requisitions.Categories.Count}, " +
                    $"offers={offers.Length}, digest={digest}");
            }

            Assert.That(
                mismatches,
                Is.Empty,
                $"Resolved ASRS catalog parity changed:{Environment.NewLine}{string.Join(Environment.NewLine, mismatches)}");
        });

        await pair.CleanReturnAsync();
    }

    private sealed record ExpectedCatalog(
        string ForceId,
        EntProtoId CatalogId,
        int CategoryCount,
        int OfferCount,
        string Digest);
}
