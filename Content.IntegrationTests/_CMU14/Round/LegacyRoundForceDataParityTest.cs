#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Content.Shared.AU14.util;
using Content.Shared._RMC14.Vendors;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Round;

[TestFixture]
public sealed class LegacyRoundForceDataParityTest
{
    private static readonly HashSet<PlatoonMarkerClass> AutomatedVendorSlots =
    [
        PlatoonMarkerClass.Arifleman,
        PlatoonMarkerClass.Clothing,
        PlatoonMarkerClass.combattech,
        PlatoonMarkerClass.Corpsman,
        PlatoonMarkerClass.Dcc,
        PlatoonMarkerClass.JuniorOfficer,
        PlatoonMarkerClass.MilitaryDoctor,
        PlatoonMarkerClass.MilitaryPolice,
        PlatoonMarkerClass.OperationsOfficer,
        PlatoonMarkerClass.Pilot,
        PlatoonMarkerClass.ReqVend,
        PlatoonMarkerClass.Rifleman,
        PlatoonMarkerClass.Rto,
        PlatoonMarkerClass.SectionSergeant,
        PlatoonMarkerClass.ShipsideUniform,
        PlatoonMarkerClass.SquadSergeant,
        PlatoonMarkerClass.SWeapons,
        PlatoonMarkerClass.VehicleCrew,
        PlatoonMarkerClass.Weapons,
    ];

    private static readonly IReadOnlyDictionary<string, int> ExpectedVendorSlotCounts =
        new Dictionary<string, int>
        {
            ["USCM"] = 19,
            ["LACN"] = 19,
            ["UPP"] = 19,
            ["WEYU"] = 19,
            ["CMBCIU"] = 19,
            ["HAZOPS"] = 19,
            ["ProdigySF"] = 21,
            ["VAIPO"] = 21,
            ["RMC"] = 21,
        };

    private static readonly IReadOnlyDictionary<string, EntProtoId> ExpectedWeaponsVendors =
        new Dictionary<string, EntProtoId>
        {
            ["USCM"] = "AU14USCMWeaponsVendor",
            ["LACN"] = "AU14LACNWeaponsVendor",
            ["UPP"] = "AU14UPPWeaponsVendor",
            ["WEYU"] = "AU14WYWeaponsVendor",
            ["CMBCIU"] = "AU14CMBCIUWeaponsVendor",
            ["HAZOPS"] = "AU14HAZOPSWeaponsVendor",
            ["ProdigySF"] = "AU14prodigyWeaponsVendor",
            ["VAIPO"] = "AU14VAIPOWeaponsVendor",
            ["RMC"] = "AU14RMCWeaponsVendor",
        };

    private static readonly IReadOnlyDictionary<string, string> ExpectedVendorResolutionDigests =
        new Dictionary<string, string>
        {
            ["USCM"] = "EB6D194B6AFAD223FE2C354EAF7F71A8B6F3E7F7D720DACDEAE975062B2D58EE",
            ["LACN"] = "C35DB4ED87A53B780636204B33782B9BD2BEA672F421003B1030BAE04C69FFB4",
            ["UPP"] = "BC4F1D8DB2B1AE01345DDF7F468CF896C29F963E4712F2D41BD7200C7B3EDE91",
            ["WEYU"] = "CD1A39C4B599DBD780D4F267592595AF51B20C241FEA3C3595D86DFD55A4DD58",
            ["CMBCIU"] = "795B2B6ED8F9543B56E839BF16F762EAF42D1536BE664062A872F9E658D4D927",
            ["HAZOPS"] = "DA08B87682C3A3505B3BB5F6872DBFAD7D94C08B482CA23D70FDF3FB9E7C1C86",
            ["ProdigySF"] = "43CADEB4D8846379EA727F3DD0B454D17F3E2FEEF285083B167F6ED0107F09F1",
            ["VAIPO"] = "6612A6E29D47E99EB1B59CEAFD5194199223706592EF1A8F56F8864624E63233",
            ["RMC"] = "0C15E9A829D0B7BDFC25E63EF86D4B326F5020B017C61099C30DD7DCB5AEC579",
        };

    [Test]
    public async Task EveryLegacyForceResolvesRequiredVendorSlotsAndMainShip()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;
            var actualForceIds = prototypes.EnumeratePrototypes<PlatoonPrototype>()
                .Select(platoon => platoon.ID)
                .ToArray();
            var mismatches = new List<string>();

            Assert.That(actualForceIds, Is.EquivalentTo(ExpectedWeaponsVendors.Keys));

            foreach (var (forceId, expectedVendorId) in ExpectedWeaponsVendors)
            {
                Assert.That(prototypes.TryIndex<PlatoonPrototype>(forceId, out var platoon), Is.True,
                    $"Missing legacy force {forceId}");
                Assert.That(platoon!.PossibleShips, Is.EqualTo(new[] { "USSBushRedux" }),
                    $"{forceId} changed its intended main-ship pairing");
                Assert.That(platoon.VendorSet, Is.EqualTo((ProtoId<PlatoonVendorSetPrototype>?) forceId),
                    $"{forceId} must retain its vendor-set compatibility mapping during migration");

                var vendorSet = prototypes.Index(platoon.VendorSet!.Value);
                Assert.That(
                    vendorSet.Vendors,
                    Has.Count.EqualTo(ExpectedVendorSlotCounts[forceId]),
                    $"{forceId} changed its resolved legacy vendor-slot count");
                foreach (var slot in AutomatedVendorSlots)
                {
                    Assert.That(
                        vendorSet.Vendors.ContainsKey(slot),
                        Is.True,
                        $"{forceId} does not resolve the semantic {slot} slot");
                }

                var resolvedSlots = new List<string>(vendorSet.Vendors.Count);
                foreach (var (slot, resolvedVendorId) in vendorSet.Vendors)
                {
                    Assert.That(
                        prototypes.TryIndex<EntityPrototype>(resolvedVendorId, out var resolvedVendor),
                        Is.True,
                        $"{forceId} references missing {slot} vendor {resolvedVendorId}");
                    if (AutomatedVendorSlots.Contains(slot))
                    {
                        Assert.That(
                            resolvedVendor!.TryGetComponent<CMAutomatedVendorComponent>(out _, factory),
                            Is.True,
                            $"{forceId} {slot} endpoint {resolvedVendorId} is not an automated vendor");
                    }

                    resolvedSlots.Add($"{slot}\t{resolvedVendorId}");
                }

                var digest = Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(
                        '\n',
                        resolvedSlots.Order(StringComparer.Ordinal)))));
                if (digest != ExpectedVendorResolutionDigests[forceId])
                    mismatches.Add($"{forceId}: {digest}");

                Assert.That(
                    vendorSet.Vendors.TryGetValue(PlatoonMarkerClass.Weapons, out var actualVendorId),
                    Is.True,
                    $"{forceId} does not resolve the semantic Weapons slot");
                Assert.That(actualVendorId, Is.EqualTo(expectedVendorId),
                    $"{forceId} resolved the wrong Weapons vendor");

                Assert.That(prototypes.TryIndex<EntityPrototype>(actualVendorId, out var vendor), Is.True,
                    $"{forceId} references missing vendor {actualVendorId}");
                Assert.That(vendor!.TryGetComponent<CMAutomatedVendorComponent>(out _, factory), Is.True,
                    $"{actualVendorId} is not an automated vendor");
            }

            Assert.That(
                mismatches,
                Is.Empty,
                $"Resolved vendor-slot parity changed:{Environment.NewLine}" +
                string.Join(Environment.NewLine, mismatches));
        });

        await pair.CleanReturnAsync();
    }
}
