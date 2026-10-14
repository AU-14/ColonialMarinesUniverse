using System.Linq;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared._RMC14.Vendors;
using Content.Shared.Storage.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Requisitions;

[TestFixture]
public sealed class CMUATGMAcquisitionTest
{
    private const string AntiArmorCategory = "Anti-Armor";
    private const int AtgmCrateCost = 4000;
    private const int AtgmClfPoints = 35;

    private static readonly EntProtoId AtgmCase = "CMUATGMCase";
    private static readonly EntProtoId AtgmCrate = "CMUCrateATGM";
    private static readonly EntProtoId ClfVendor = "AU14CLFObjectiveWeaponsVendor";

    // BEAR inherits UPPCargoCatalog, so it receives the ATGM through UPP.
    private static readonly EntProtoId[] CatalogsWithAtgm =
    [
        "USCMCargoCatalog",
        "RMCCargoCatalog",
        "UPPCargoCatalog",
        "BEARCargoCatalog",
        "VAIPOCargoCatalog",
        "ProdigyCargoCatalog",
        "LACNCargoCatalog",
        "HAZOPSCargoCatalog",
        "CMBCIUCargoCatalog",
    ];

    private static readonly EntProtoId[] CatalogsWithoutAtgm =
    [
        "WYPMCCargoCatalog",
        "CMASRSConsole",
        "CMASRSConsoleColony",
        "AU14CorporateASRSConsole",
    ];

    private static readonly EntProtoId[] VendorsWithAtgm =
    [
        "AU14USCMRequisitionVendor",
        "AU14LACNRequisitionVendor",
    ];

    private static readonly EntProtoId[] VendorsWithoutAtgm =
    [
        "AU14WYPMCRequisitionVendor",
    ];

    [Test]
    public async Task AtgmCrateIsSoldByNonWeYuPlatoonCatalogsOnly()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            Assert.Multiple(() =>
            {
                foreach (var catalogId in CatalogsWithAtgm)
                {
                    var req = GetRequisitions(prototypes, factory, catalogId);
                    var category = req.Categories.FirstOrDefault(c => c.Name == AntiArmorCategory);
                    Assert.That(category, Is.Not.Null, $"{catalogId} has no {AntiArmorCategory} category");
                    if (category == null)
                        continue;

                    var entry = category.Entries.FirstOrDefault(e => e.Crate == AtgmCrate);
                    Assert.That(entry, Is.Not.Null, $"{catalogId} {AntiArmorCategory} does not sell {AtgmCrate}");
                    if (entry == null)
                        continue;

                    Assert.That(entry.Cost, Is.EqualTo(AtgmCrateCost), $"{catalogId} {AtgmCrate} cost");
                    Assert.That(entry.MaxStock, Is.EqualTo(1), $"{catalogId} {AtgmCrate} maxStock");
                }

                foreach (var catalogId in CatalogsWithoutAtgm)
                {
                    var req = GetRequisitions(prototypes, factory, catalogId);
                    var sold = req.Categories.SelectMany(c => c.Entries).Any(e => e.Crate == AtgmCrate);
                    Assert.That(sold, Is.False, $"{catalogId} must not sell {AtgmCrate}");
                }
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AtgmCaseIsInFreeAndClfVendors()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            Assert.Multiple(() =>
            {
                foreach (var vendorId in VendorsWithAtgm)
                {
                    var vendor = GetVendor(prototypes, factory, vendorId);
                    var entry = vendor.Sections.SelectMany(s => s.Entries).FirstOrDefault(e => e.Id == AtgmCase);
                    Assert.That(entry, Is.Not.Null, $"{vendorId} does not vend {AtgmCase}");
                    Assert.That(entry?.Amount, Is.EqualTo(1), $"{vendorId} {AtgmCase} amount");
                }

                foreach (var vendorId in VendorsWithoutAtgm)
                {
                    var vendor = GetVendor(prototypes, factory, vendorId);
                    var sold = vendor.Sections.SelectMany(s => s.Entries).Any(e => e.Id == AtgmCase);
                    Assert.That(sold, Is.False, $"{vendorId} must not vend {AtgmCase}");
                }

                var clf = GetVendor(prototypes, factory, ClfVendor);
                var clfEntry = clf.Sections.SelectMany(s => s.Entries).FirstOrDefault(e => e.Id == AtgmCase);
                Assert.That(clfEntry, Is.Not.Null, $"{ClfVendor} does not vend {AtgmCase}");
                Assert.That(clfEntry?.Amount, Is.EqualTo(1), $"{ClfVendor} {AtgmCase} amount");
                Assert.That(clfEntry?.Points, Is.EqualTo(AtgmClfPoints), $"{ClfVendor} {AtgmCase} points");
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AtgmCrateContainsKitCase()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            Assert.That(prototypes.TryIndex<EntityPrototype>(AtgmCase, out _), Is.True,
                $"{AtgmCase} prototype does not exist");
            Assert.That(prototypes.TryIndex<EntityPrototype>(AtgmCrate, out var crate), Is.True,
                $"{AtgmCrate} prototype does not exist");
            Assert.That(crate!.TryComp<StorageFillComponent>(out var storage, factory), Is.True,
                $"{AtgmCrate} has no StorageFill component");

            var entries = storage!.Contents.Where(e => e.PrototypeId == AtgmCase).ToList();
            Assert.That(entries, Has.Count.EqualTo(1), $"{AtgmCrate} should contain one {AtgmCase} entry");
            Assert.That(entries.Sum(e => e.Amount), Is.EqualTo(1), $"{AtgmCrate} {AtgmCase} amount");
        });

        await pair.CleanReturnAsync();
    }

    private static RequisitionsComputerComponent GetRequisitions(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        EntProtoId id)
    {
        Assert.That(prototypes.TryIndex<EntityPrototype>(id, out var proto), Is.True,
            $"{id} prototype does not exist");
        Assert.That(proto!.TryComp<RequisitionsComputerComponent>(out var req, factory), Is.True,
            $"{id} has no RequisitionsComputer component");
        return req!;
    }

    private static CMAutomatedVendorComponent GetVendor(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        EntProtoId id)
    {
        Assert.That(prototypes.TryIndex<EntityPrototype>(id, out var proto), Is.True,
            $"{id} prototype does not exist");
        Assert.That(proto!.TryComp<CMAutomatedVendorComponent>(out var vendor, factory), Is.True,
            $"{id} has no CMAutomatedVendor component");
        return vendor!;
    }
}
