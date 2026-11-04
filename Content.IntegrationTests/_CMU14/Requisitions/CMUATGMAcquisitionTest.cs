using System.Linq;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared._RMC14.Vendors;
using Content.Shared.CMU14.Requisitions;
using Content.Shared.Storage;
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
    private const int MissileCrateCost = 3500;
    private const int MissileClfPoints = 15;
    private const int MissilesPerBag = 7;

    private static readonly EntProtoId AtgmCase = "CMUATGMCase";
    private static readonly EntProtoId AtgmCrate = "CMUCrateATGM";
    private static readonly EntProtoId AtgmMissile = "CMUATGMMissile";
    private static readonly EntProtoId MissileBagFilled = "CMUATGMMissileBagFilled";
    private static readonly EntProtoId MissileCrate = "CMUCrateATGMMissiles";
    private static readonly EntProtoId ClfVendor = "AU14CLFObjectiveWeaponsVendor";
    private static readonly EntProtoId MortarKitCrate = "RMCCrateMortarKit";
    private static readonly ProtoId<CMURequisitionsRoundStartFreeCratePrototype> GovforFreeCrates =
        "CMURequisitionsRoundStartFreeCrateGovfor";

    private static readonly ProtoId<CMURequisitionsRoundStartFreeCratePrototype> OpforFreeCrates =
        "CMURequisitionsRoundStartFreeCrateOpfor";

    // BEAR inherits UPPCargoCatalog, so it receives the ATGM through UPP.
    // FLOW-12 (e): WeYu PMC is fully included, so WYPMCCargoCatalog moved here from the excluded list.
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
        "WYPMCCargoCatalog",
    ];

    // The colony ASRS and the WeYu corporate (science/facility) ASRS stay excluded.
    private static readonly EntProtoId[] CatalogsWithoutAtgm =
    [
        "CMASRSConsole",
        "CMASRSConsoleColony",
        "AU14CorporateASRSConsole",
    ];

    // GOVFOR's free kit comes only from the round-start ASRS crate (Distress Signal and Force on Force), so the
    // requisitions vendors must not hand out a second one. In Insurgency GOVFOR buys it from the ASRS.
    private static readonly EntProtoId[] RequisitionVendorsWithoutFreeKit =
    [
        "AU14USCMRequisitionVendor",
        "AU14LACNRequisitionVendor",
        "AU14WYPMCRequisitionVendor",
    ];

    [Test]
    public async Task AtgmCratesAreSoldByPlatoonCatalogsOnly()
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

                    var missiles = category.Entries.FirstOrDefault(e => e.Crate == MissileCrate);
                    Assert.That(missiles, Is.Not.Null, $"{catalogId} {AntiArmorCategory} does not sell {MissileCrate}");
                    Assert.That(missiles?.Cost, Is.EqualTo(MissileCrateCost), $"{catalogId} {MissileCrate} cost");
                }

                foreach (var catalogId in CatalogsWithoutAtgm)
                {
                    var req = GetRequisitions(prototypes, factory, catalogId);
                    var sold = req.Categories.SelectMany(c => c.Entries)
                        .Any(e => e.Crate == AtgmCrate || e.Crate == MissileCrate);
                    Assert.That(sold, Is.False, $"{catalogId} must not sell {AtgmCrate} or {MissileCrate}");
                }
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AtgmKitIsNotFreeFromRequisitionVendorsButSoldToClf()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            Assert.Multiple(() =>
            {
                foreach (var vendorId in RequisitionVendorsWithoutFreeKit)
                {
                    var vendor = GetVendor(prototypes, factory, vendorId);
                    Assert.That(vendor.Sections.SelectMany(s => s.Entries).Any(e => e.Id == AtgmCase), Is.False,
                        $"{vendorId} must not give a free {AtgmCase}; the round-start ASRS crate already does");
                }

                var clf = GetVendor(prototypes, factory, ClfVendor);
                var clfEntry = clf.Sections.SelectMany(s => s.Entries).FirstOrDefault(e => e.Id == AtgmCase);
                Assert.That(clfEntry, Is.Not.Null, $"{ClfVendor} does not vend {AtgmCase}");
                Assert.That(clfEntry?.Amount, Is.EqualTo(1), $"{ClfVendor} {AtgmCase} amount");
                Assert.That(clfEntry?.Points, Is.EqualTo(AtgmClfPoints), $"{ClfVendor} {AtgmCase} points");

                var clfMissile = clf.Sections.SelectMany(s => s.Entries).FirstOrDefault(e => e.Id == AtgmMissile);
                Assert.That(clfMissile, Is.Not.Null, $"{ClfVendor} does not vend {AtgmMissile}");
                Assert.That(clfMissile?.Points, Is.EqualTo(MissileClfPoints), $"{ClfVendor} {AtgmMissile} points");
                Assert.That(clfMissile?.Amount, Is.GreaterThan(0), $"{ClfVendor} {AtgmMissile} amount");
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

    [Test]
    public async Task MissileCrateHoldsOneFilledBag()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            Assert.That(prototypes.TryIndex<EntityPrototype>(MissileCrate, out var crate), Is.True,
                $"{MissileCrate} prototype does not exist");
            Assert.That(crate!.TryComp<StorageFillComponent>(out var storage, factory), Is.True,
                $"{MissileCrate} has no StorageFill component");

            // The itemized ASRS view reads the crate's own fill, so it lists one filled bag.
            var entries = storage!.Contents;
            Assert.That(entries, Has.Count.EqualTo(1), $"{MissileCrate} should have a single fill entry");
            Assert.That(entries[0].PrototypeId, Is.EqualTo((EntProtoId?) MissileBagFilled), $"{MissileCrate} fill");
            Assert.That(entries[0].Amount, Is.EqualTo(1), $"{MissileCrate} {MissileBagFilled} amount");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FilledMissileBagSpawnsWithSevenMissiles()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var bag = entMan.SpawnEntity(MissileBagFilled, map.GridCoords);
            var storage = entMan.GetComponent<StorageComponent>(bag);
            var contents = storage.Container.ContainedEntities
                .Select(e => entMan.GetComponent<MetaDataComponent>(e).EntityPrototype?.ID)
                .ToList();

            Assert.That(contents, Has.Count.EqualTo(MissilesPerBag), $"{MissileBagFilled} contents");
            Assert.That(contents, Is.All.EqualTo(AtgmMissile.Id), $"{MissileBagFilled} holds only missiles");

            entMan.DeleteEntity(bag);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GovforRoundStartFreeCratesIncludeAtgmKitOnlyInDistressAndForceOnForce()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var govfor = prototypes.Index(GovforFreeCrates);

            Assert.Multiple(() =>
            {
                // FLOW-13: GOVFOR's one free kit ships with the mortar, in Distress Signal and Force on Force only.
                Assert.That(govfor.Faction, Is.EqualTo("govfor"));
                Assert.That(govfor.Crates, Does.Contain(MortarKitCrate));
                Assert.That(govfor.Crates.Count(c => c == AtgmCrate), Is.EqualTo(1));
                Assert.That(govfor.Gamemodes, Is.EquivalentTo(new[] { "DistressSignal", "ForceOnForce" }));

                // No other GOVFOR entry may add a second kit, and none ships it in Insurgency.
                var govforKits = prototypes.EnumeratePrototypes<CMURequisitionsRoundStartFreeCratePrototype>()
                    .Where(b => string.Equals(b.Faction, "govfor", StringComparison.OrdinalIgnoreCase) && b.Crates.Contains(AtgmCrate))
                    .ToList();
                Assert.That(govforKits.Select(b => b.ID), Is.EquivalentTo(new[] { GovforFreeCrates.Id }),
                    "Only one free FOGO-ATGM kit per GOVFOR side.");
                Assert.That(govforKits.SelectMany(b => b.Gamemodes), Does.Not.Contain("Insurgency").IgnoreCase,
                    "The free FOGO-ATGM kit must not ship in Insurgency.");

                // Force-on-Force OPFOR gets one free kit too.
                var opfor = prototypes.Index(OpforFreeCrates);
                Assert.That(opfor.Faction, Is.EqualTo("opfor"));
                Assert.That(opfor.Gamemodes, Does.Contain("ForceOnForce"));
                Assert.That(opfor.Crates.Count(c => c == AtgmCrate), Is.EqualTo(1));

                // The colony and corporate elevators never receive it.
                foreach (var bonus in prototypes.EnumeratePrototypes<CMURequisitionsRoundStartFreeCratePrototype>())
                {
                    if (bonus.Faction is "colony" or "corporate")
                        Assert.That(bonus.Crates, Does.Not.Contain(AtgmCrate), $"{bonus.ID} must not give {AtgmCrate}");
                }
            });
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
