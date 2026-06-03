using System.Collections.Generic;
using System.Linq;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.Storage.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Medical;

[TestFixture]
public sealed class CMUMedicalAsrsCatalogTest
{
    private static readonly EntProtoId FieldTreatmentCrate = "CMUCrateMedicalFieldTreatments";

    private static readonly EntProtoId[] BaseAsrsConsoles =
    [
        "CMASRSConsole",
        "CMASRSConsoleColony",
    ];

    private static readonly EntProtoId[] PlatoonAsrsCatalogs =
    [
        "USCMCargoCatalog",
        "RMCCargoCatalog",
        "UPPCargoCatalog",
        "WEYUCargoCatalog",
        "VAIPOCargoCatalog",
        "ProdigyCargoCatalog",
        "LACNCargoCatalog",
        "HAZOPSCargoCatalog",
        "CMBCIUCargoCatalog",
    ];

    private static readonly Dictionary<EntProtoId, int> ExpectedFieldTreatmentContents = new()
    {
        ["CMUPlainGauze10"] = 1,
        ["CMUPlainTraumaDressing10"] = 1,
        ["CMUCoagulantPowder"] = 1,
        ["CMUAntiseptic"] = 1,
        ["CMUBurnGel"] = 1,
        ["CMUTissueSealant"] = 1,
        ["CMUTraumaFoam"] = 1,
        ["CMUHemostaticGauze6"] = 1,
        ["CMUAntisepticGauze6"] = 1,
        ["CMUBurnGelGauze6"] = 1,
        ["CMUSealingGauze6"] = 1,
        ["CMUCompressionGauze6"] = 1,
        ["CMUHemostaticTraumaDressing4"] = 1,
        ["CMUAntisepticTraumaDressing4"] = 1,
        ["CMUBurnTraumaDressing4"] = 1,
        ["CMUSealingTraumaDressing4"] = 1,
        ["CMUCompressionTraumaDressing4"] = 1,
        ["CMBloodPackFull"] = 1,
        ["CMBloodPack"] = 1,
    };

    [Test]
    public async Task FieldTreatmentCrateIsSoldByAsrsMedicalCatalogs()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            Assert.That(prototypes.TryIndex<EntityPrototype>(FieldTreatmentCrate, out var crate), Is.True);
            Assert.That(crate!.TryGetComponent<StorageFillComponent>(out var storage, factory), Is.True);

            var actualContents = storage!.Contents
                .Where(entry => entry.PrototypeId != null)
                .ToDictionary(entry => entry.PrototypeId!.Value, entry => entry.Amount);

            Assert.Multiple(() =>
            {
                foreach (var (id, amount) in ExpectedFieldTreatmentContents)
                {
                    Assert.That(actualContents.TryGetValue(id, out var actualAmount), Is.True,
                        $"{FieldTreatmentCrate} is missing {id}");
                    Assert.That(actualAmount, Is.EqualTo(amount),
                        $"{FieldTreatmentCrate} should contain {amount}x {id}");
                }

                foreach (var consoleId in BaseAsrsConsoles)
                {
                    Assert.That(prototypes.TryIndex<EntityPrototype>(consoleId, out var console), Is.True,
                        $"{consoleId} prototype does not exist");
                    Assert.That(console!.TryGetComponent<RequisitionsComputerComponent>(out var req, factory), Is.True,
                        $"{consoleId} has no RequisitionsComputer component");

                    var medical = req!.Categories.FirstOrDefault(category => category.Name == "Medical");
                    Assert.That(medical, Is.Not.Null, $"{consoleId} has no Medical category");
                    Assert.That(medical!.Entries.Any(entry => entry.Crate == FieldTreatmentCrate), Is.True,
                        $"{consoleId} Medical category does not sell {FieldTreatmentCrate}");
                }

                foreach (var catalogId in PlatoonAsrsCatalogs)
                {
                    Assert.That(prototypes.TryIndex<EntityPrototype>(catalogId, out var catalog), Is.True,
                        $"{catalogId} prototype does not exist");
                    Assert.That(catalog!.TryGetComponent<RequisitionsComputerComponent>(out var req, factory), Is.True,
                        $"{catalogId} has no RequisitionsComputer component");

                    var medical = req!.Categories.FirstOrDefault(category => category.Name == "Medical");
                    Assert.That(medical, Is.Not.Null, $"{catalogId} has no Medical category");
                    Assert.That(medical!.Entries.Any(entry => entry.Crate == FieldTreatmentCrate), Is.True,
                        $"{catalogId} Medical category does not sell {FieldTreatmentCrate}");
                }
            });
        });

        await pair.CleanReturnAsync();
    }
}
