using System.Linq;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.Storage.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Requisitions;

/// <summary>
/// FLOW-30: every ASRS (colony, GOVFOR, OPFOR and corporate consoles, and each platoon catalog) sells the chemistry
/// hotplate and chiller flatpacks for $250 each, once.
/// </summary>
[TestFixture]
public sealed class CMUASRSChemistryFlatpackTest
{
    private const int Cost = 250;

    private static readonly (EntProtoId Crate, EntProtoId Flatpack)[] Crates =
    [
        ("CMUCrateChemistryHotplate", "CMUChemistryHotplateFlatpack"),
        ("CMUCrateChemistryChiller", "CMUChemistryChillerFlatpack"),
    ];

    private static readonly EntProtoId[] Catalogs =
    [
        "CMASRSConsoleColony",
        "CMASRSConsoleGovfor",
        "CMASRSConsoleOpfor",
        "AU14CorporateASRSConsole",
        "CMBCIUCargoCatalog",
        "HAZOPSCargoCatalog",
        "LACNCargoCatalog",
        "ProdigyCargoCatalog",
        "RMCCargoCatalog",
        "UPPCargoCatalog",
        "USCMCargoCatalog",
        "VAIPOCargoCatalog",
        "WYPMCCargoCatalog",
        "BEARCargoCatalog",
    ];

    [Test]
    public async Task EveryASRSSellsHotplateAndChillerFlatpacks()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            Assert.Multiple(() =>
            {
                foreach (var (crate, flatpack) in Crates)
                {
                    Assert.That(prototypes.Index<EntityPrototype>(crate).TryComp<StorageFillComponent>(out var fill, factory), Is.True, crate.Id);
                    Assert.That(fill!.Contents.Select(entry => entry.PrototypeId?.Id), Is.EqualTo(new[] { flatpack.Id }),
                        $"{crate.Id} must contain exactly its flatpack");
                }

                foreach (var catalogId in Catalogs)
                {
                    Assert.That(prototypes.Index<EntityPrototype>(catalogId).TryComp<RequisitionsComputerComponent>(out var requisitions, factory),
                        Is.True, catalogId.Id);
                    var entries = requisitions!.Categories.SelectMany(category => category.Entries).ToList();
                    foreach (var (crate, _) in Crates)
                    {
                        var matches = entries.Where(entry => entry.Crate.Id == crate.Id).ToList();
                        Assert.That(matches, Has.Count.EqualTo(1), $"{catalogId.Id} must list {crate.Id} exactly once");
                        if (matches.Count == 1)
                            Assert.That(matches[0].Cost, Is.EqualTo(Cost), $"{catalogId.Id} {crate.Id} price");
                    }
                }
            });
        });

        await pair.CleanReturnAsync();
    }
}
