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
    private static readonly PlatoonMarkerClass[] RequiredVendorSlots =
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
            ["ProdigySF"] = "F39B1504D17822B9717CB95D63AB4D2C51B6F921166EE0925DC834AFC89BA740",
            ["VAIPO"] = "A7385699186E31A8AE6A060128FD824C023379C517E046727772E773C9F9CAC3",
            ["RMC"] = "779007959EB35BFA3EBDE02DFF60687059B58A6F6C7E79DB79DA41244AA2E01A",
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
                var resolvedSlots = new List<string>();
                foreach (var slot in RequiredVendorSlots)
                {
                    Assert.That(
                        vendorSet.Vendors.TryGetValue(slot, out var resolvedVendorId),
                        Is.True,
                        $"{forceId} does not resolve the semantic {slot} slot");
                    Assert.That(
                        prototypes.TryIndex<EntityPrototype>(resolvedVendorId, out var resolvedVendor),
                        Is.True,
                        $"{forceId} references missing {slot} vendor {resolvedVendorId}");
                    Assert.That(
                        resolvedVendor!.TryGetComponent<CMAutomatedVendorComponent>(out _, factory),
                        Is.True,
                        $"{forceId} {slot} endpoint {resolvedVendorId} is not an automated vendor");

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
