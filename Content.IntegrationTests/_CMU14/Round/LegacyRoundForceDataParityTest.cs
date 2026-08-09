#nullable enable

using System.Collections.Generic;
using Content.Shared.AU14.util;
using Content.Shared._RMC14.Vendors;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Round;

[TestFixture]
public sealed class LegacyRoundForceDataParityTest
{
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

    [Test]
    public async Task EveryLegacyForceResolvesAWeaponsVendorAndMainShip()
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
        });

        await pair.CleanReturnAsync();
    }
}
