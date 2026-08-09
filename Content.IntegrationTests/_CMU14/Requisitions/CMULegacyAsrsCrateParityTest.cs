#nullable enable

using System.Linq;
using Content.Shared.Storage.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Requisitions;

[TestFixture]
public sealed class CMULegacyAsrsCrateParityTest
{
    private static readonly ExpectedCrate[] ExpectedCrates =
    [
        new(
            "AU14CrateMagazineSpecSniperM96S",
            "RMCCrateAmmo",
            [new("CMMagazineSniperM96S", 6)]),
        new(
            "AU14CrateMagazineSpecSniperM96SIncendiary",
            "RMCCrateAmmo",
            [new("CMMagazineSniperM96SIncendiary", 6)]),
        new(
            "AU14CrateMagazineSpecSniperM96SFlak",
            "RMCCrateAmmo",
            [new("CMMagazineSniperM96SFlak", 6)]),
        new(
            "AU14CrateMagazineSpecSniperM96SMixed",
            "RMCCrateAmmo",
            [
                new("CMMagazineSniperM96S", 2),
                new("CMMagazineSniperM96SIncendiary", 2),
                new("CMMagazineSniperM96SFlak", 2),
            ]),
        new(
            "AU14CrateMagazineSpecAMRXM43E1",
            "RMCCrateAmmo",
            [new("RMCMagazineSniperXM43E1AntiMateriel", 6)]),
        new(
            "AU14CrateMagazineSpecSharpHE",
            "RMCCrateAmmo",
            [new("RMCMagazineRifleSharpExplosive", 6)]),
        new(
            "AU14CrateMagazineSpecSharpFLC",
            "RMCCrateAmmo",
            [new("RMCMagazineRifleSharpFlechette", 6)]),
        new(
            "AU14CrateMagazineSpecSharpINCEN",
            "RMCCrateAmmo",
            [new("RMCMagazineRifleSharpIncendiary", 6)]),
        new(
            "AU14CrateMagazineSpecSharpMixed",
            "RMCCrateAmmo",
            [
                new("RMCMagazineRifleSharpExplosive", 2),
                new("RMCMagazineRifleSharpFlechette", 2),
                new("RMCMagazineRifleSharpIncendiary", 2),
            ]),
        new(
            "AU14CrateMagazineSpecScoutStandard",
            "RMCCrateAmmo",
            [new("RMCMagazineRifleM4SPRA19", 6)]),
        new(
            "AU14CrateMagazineSpecScoutINCEN",
            "RMCCrateAmmo",
            [new("RMCMagazineRifleM4SPRA19Incendiary", 6)]),
        new(
            "AU14CrateMagazineSpecScoutFLAK",
            "RMCCrateAmmo",
            [new("RMCMagazineRifleM4SPRA19Impact", 6)]),
        new(
            "AU14CrateMagazineSpecScoutMixed",
            "RMCCrateAmmo",
            [
                new("RMCMagazineRifleM4SPRA19", 2),
                new("RMCMagazineRifleM4SPRA19Incendiary", 2),
                new("RMCMagazineRifleM4SPRA19Impact", 2),
            ]),
        new(
            "AU14CrateMortarShellHEATMP",
            "RMCCrateMortarAmmo",
            [new("AU14MortarShellHEATMP", 12)]),
        new(
            "AU14CrateMortarShellMixed",
            "RMCCrateMortarAmmo",
            [
                new("RMCMortarShellHE", 3),
                new("RMCMortarShellIncendiary", 3),
                new("RMCMortarShellFlare", 3),
                new("AU14MortarShellHEATMP", 3),
            ]),
        new(
            "RMCCratePyroTankMixedUPP",
            "RMCCrateAmmo",
            [
                new("RMCTankFlamerXLargeUPP", 1),
                new("RMCTankFlamerBLargeUPP", 1),
                new("RMCTankFlamerLargeUPP", 1),
            ]),
        new(
            "RMCCratePyroTankMixedWY",
            "RMCCrateAmmo",
            [
                new("RMCTankFlamerFL3", 1),
                new("RMCTankFlamerFL3B", 1),
                new("RMCTankFlamerFL3X", 1),
            ]),
        new(
            "RMCCratePyroTankMixedHAZOPS",
            "RMCCrateAmmo",
            [
                new("AU14TankFlamerM240A2HAZOPS", 1),
                new("AU14TankFlamerM240A2HAZOPSNapalmB", 1),
                new("AU14TankFlamerM240A2HAZOPSNapalmX", 1),
            ]),
    ];

    [Test]
    public async Task RestoredCratesMatchTheirHistoricalDefinitions()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            Assert.That(ExpectedCrates, Has.Length.EqualTo(18));
            Assert.Multiple(() =>
            {
                foreach (var expected in ExpectedCrates)
                {
                    var found = prototypes.TryIndex<EntityPrototype>(expected.Id, out var crate);
                    Assert.That(found, Is.True, $"Missing historical ASRS crate {expected.Id}");
                    if (!found)
                        continue;

                    Assert.That(
                        crate!.Parents,
                        Is.EqualTo(new[] { expected.ParentId }),
                        $"{expected.Id} changed its historical crate parent");

                    var hasFill = crate.TryComp<StorageFillComponent>(out var storage, factory);
                    Assert.That(hasFill, Is.True, $"{expected.Id} has no StorageFill component");
                    if (!hasFill)
                        continue;

                    var actualContents = storage!.Contents
                        .Select(entry => new ExpectedContent(
                            entry.PrototypeId?.Id ?? "<missing prototype>",
                            entry.Amount,
                            entry.SpawnProbability,
                            entry.MaxAmount,
                            entry.GroupId))
                        .ToArray();
                    Assert.That(
                        actualContents,
                        Is.EquivalentTo(expected.Contents),
                        $"{expected.Id} changed its historical StorageFill contents");
                }
            });
        });

        await pair.CleanReturnAsync();
    }

    private sealed record ExpectedCrate(
        string Id,
        string ParentId,
        ExpectedContent[] Contents);

    private sealed record ExpectedContent(
        string Id,
        int Amount,
        float SpawnProbability = 1,
        int MaxAmount = 1,
        string? GroupId = null);
}
