using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._RMC14.Vendors;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Vending;

public sealed class BattalionCommanderVendorTest : InteractionTest
{
    [TestCase("CMUBattalionCommanderVendor")]
    [TestCase("CMUBattalionCommanderCombatVendorDistressFOF")]
    [TestCase("CMUBattalionCommanderCombatVendorInsurgency")]
    public async Task VendorIsCommandingOfficerLockedAndStocksResolvableItems(string vendorPrototype)
    {
        await SpawnTarget(vendorPrototype);

        var vendor = SEntMan.GetComponent<CMAutomatedVendorComponent>(STarget!.Value);
        var protoMan = Server.ResolveDependency<IPrototypeManager>();
        var compFactory = Server.ResolveDependency<IComponentFactory>();

        // Every platoon commander job a player can spawn as, GOVFOR and OPFOR, including the faction variants.
        foreach (var job in new[]
                 {
                     "AU14JobGOVFORPlatCo", "AU14JobOPFORPlatCo", "AU14JobGOVFORPlatCoCMBCIU",
                     "AU14JobGOVFORPlatCoRMC", "AU14JobGOVFORPlatCoUPP", "AU14JobGOVFORPlatCoWYPMC",
                 })
        {
            Assert.That(vendor.Jobs, Does.Contain((ProtoId<JobPrototype>) job),
                $"Battalion Commander vendor must be usable by {job}.");
        }

        Assert.That(vendor.Jobs, Does.Not.Contain((ProtoId<JobPrototype>) "CMCommandingOfficer"),
            "Battalion Commander vendor is for platoon commanders, not the ship CO.");
        Assert.That(vendor.Sections, Is.Not.Empty, "Vendor has no sections.");

        foreach (var section in vendor.Sections)
        {
            foreach (var entry in section.Entries)
            {
                Assert.That(protoMan.TryIndex<EntityPrototype>(entry.Id, out var proto), Is.True,
                    $"Section '{section.Name}' references unknown prototype '{entry.Id}'.");

                if (proto == null || !proto.TryGetComponent(out CMVendorBundleComponent? bundle, compFactory))
                    continue;

                foreach (var bundled in bundle.Bundle)
                {
                    Assert.That(protoMan.HasIndex<EntityPrototype>(bundled), Is.True,
                        $"Bundle '{entry.Id}' references unknown prototype '{bundled}'.");
                }
            }
        }
    }

    [TestCase("CMUBattalionCommanderCombatVendorDistressFOF")]
    [TestCase("CMUBattalionCommanderCombatVendorInsurgency")]
    public async Task CombatVendorStockIsRestricted(string vendorPrototype)
    {
        await SpawnTarget(vendorPrototype);

        var vendor = SEntMan.GetComponent<CMAutomatedVendorComponent>(STarget!.Value);
        var primary = vendor.Sections.Single(s => s.Name == "Primary Weapon");

        Assert.That(primary.Choices, Is.Not.Null.And.Property("Amount").EqualTo(1),
            "Primary weapon section must allow exactly one pick.");
        var armor = vendor.Sections.Single(s => s.Name == "Armor");
        Assert.That(armor.Entries.Select(e => e.Id.Id), Does.Not.Contain("RMCArmorM3General"),
            "General officer armor must not be offered.");
        Assert.That(vendor.Sections.Select(s => s.Name), Does.Contain("Belt").And.Contain("Pouches"));

        Assert.That(primary.Entries.Select(e => e.Id.Id),
            Is.EquivalentTo(new[] { "CMUBattalionCommanderBundleM59A", "CMUBattalionCommanderBundleM56C" }));
    }
}
