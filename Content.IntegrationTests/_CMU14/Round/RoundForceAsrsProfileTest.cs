#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Content.Shared.CMU.Round;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests._CMU14.Round;

[TestFixture]
public sealed class RoundForceAsrsProfileTest
{
    private const string CommonProfileId = "CMURoundForceAsrsCommon";
    private const string CostOverrideProfileId = "CMURoundForceAsrsCostOverrideTest";
    private const string StockClearProfileId = "CMURoundForceAsrsStockClearTest";
    private const string StockReplacementProfileId = "CMURoundForceAsrsStockReplacementTest";

    private const string CostOverrideProfile = """
        - type: entity
          parent: CMURoundForceAsrsCommon
          id: CMURoundForceAsrsCostOverrideTest
          categories: [ HideSpawnMenu ]
          components:
          - type: RoundForceAsrsProfile
            forceId: CostOverrideTest
            overrides:
            - offer: LtbCannonMixed
              cost: 1234
        """;

    private const string StockReplacementProfile = """
        - type: entity
          parent: CMURoundForceAsrsCommon
          id: CMURoundForceAsrsStockReplacementTest
          categories: [ HideSpawnMenu ]
          components:
          - type: RoundForceAsrsProfile
            forceId: StockReplacementTest
            overrides:
            - offer: LtbCannonMixed
              stock:
                kind: Replace
                policy:
                  maximum: 5
                  replenishDelay: 45
                  startingStock: 3
                  replenishAmount: 2
        """;

    private const string StockClearProfile = """
        - type: entity
          parent: CMURoundForceAsrsCommon
          id: CMURoundForceAsrsStockClearTest
          categories: [ HideSpawnMenu ]
          components:
          - type: RoundForceAsrsProfile
            forceId: StockClearTest
            overrides:
            - offer: LtbCannonMixed
              stock:
                kind: Clear
        """;

    private const string InvalidOverrideProfiles = """
        - type: entity
          parent: CMURoundForceAsrsCommon
          id: CMURoundForceAsrsEmptyOverrideTest
          categories: [ HideSpawnMenu ]
          components:
          - type: RoundForceAsrsProfile
            forceId: EmptyOverrideTest
            overrides:
            - offer: LtbCannonMixed

        - type: entity
          parent: CMURoundForceAsrsCommon
          id: CMURoundForceAsrsUnchangedPolicyTest
          categories: [ HideSpawnMenu ]
          components:
          - type: RoundForceAsrsProfile
            forceId: UnchangedPolicyTest
            overrides:
            - offer: LtbCannonMixed
              stock:
                kind: Unchanged
                policy:
                  maximum: 2
                  replenishDelay: 300

        - type: entity
          parent: CMURoundForceAsrsCommon
          id: CMURoundForceAsrsMissingReplacementPolicyTest
          categories: [ HideSpawnMenu ]
          components:
          - type: RoundForceAsrsProfile
            forceId: MissingReplacementPolicyTest
            overrides:
            - offer: LtbCannonMixed
              stock:
                kind: Replace

        - type: entity
          parent: CMURoundForceAsrsCommon
          id: CMURoundForceAsrsClearPolicyTest
          categories: [ HideSpawnMenu ]
          components:
          - type: RoundForceAsrsProfile
            forceId: ClearPolicyTest
            overrides:
            - offer: LtbCannonMixed
              stock:
                kind: Clear
                policy:
                  maximum: 2
                  replenishDelay: 300
        """;

    private static readonly string[] ExpectedForceIds =
    [
        "CMBCIU",
        "HAZOPS",
        "LACN",
        "ProdigySF",
        "RMC",
        "UPP",
        "USCM",
        "VAIPO",
        "WEYU",
    ];

    private static readonly ExpectedOffer[] ExpectedVehicleAmmo =
    [
        new("LtbCannonMixed", "CMUCrateVehicleAmmoLTBCannonMixed", 1900),
        new("LtaaAp", "RMCCrateVehicleAmmoLTAAAP", 1800),
        new("AceAutocannon", "RMCCrateVehicleAmmoAceAutocannon", 1800),
        new("DragonFlamer", "RMCCrateVehicleAmmoDragonFlamer", 1700),
        new("BoyarsDualCannon", "RMCCrateVehicleAmmoBoyarsDualCannon", 1800),
        new("GrenadeLauncherMixed", "CMUCrateVehicleAmmoGrenadeLauncherMixed", 900),
        new("SmokeLauncher", "RMCCrateVehicleAmmoSmokeLauncher", 700),
        new("TowLauncher", "RMCCrateVehicleAmmoTowLauncher", 2200),
        new("CupolaMixed", "CMUCrateVehicleAmmoCupolaMixed", 1025),
        new("CupolaTracer", "CMUCrateVehicleAmmoCupolaTracer", 1050),
        new("LzrnFlamer", "RMCCrateVehicleAmmoLZRNFlamer", 1200),
        new("FrontalCannon", "RMCCrateVehicleAmmoFrontalCannon", 1000),
        new("FlareLauncher", "RMCCrateVehicleAmmoFlareLauncher", 700),
        new("RotaryCannon", "RMCCrateVehicleAmmoRotaryCannon", 1800),
    ];

    [Test]
    public async Task CommonPouchesAndVehicleAmmoResolveFirstSliceParity()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;
            var profiles = new List<(EntityPrototype Entity, RoundForceAsrsProfileComponent Profile)>();
            foreach (var entity in prototypes.EnumeratePrototypes<EntityPrototype>())
            {
                if (!entity.TryGetComponent<RoundForceAsrsProfileComponent>(out var profile, factory) ||
                    string.IsNullOrWhiteSpace(profile.ForceId))
                {
                    continue;
                }

                profiles.Add((entity, profile));
            }

            profiles.Sort((left, right) =>
                StringComparer.Ordinal.Compare(left.Profile.ForceId, right.Profile.ForceId));

            Assert.That(
                profiles.Select(profile => profile.Profile.ForceId),
                Is.EqualTo(ExpectedForceIds));

            foreach (var (entity, profile) in profiles)
            {
                var forceId = profile.ForceId!;
                Assert.That(entity.HideSpawnMenu, Is.True, $"{entity.ID} must remain hidden from the entity list");
                Assert.That(
                    entity.Parents,
                    Does.Contain(CommonProfileId),
                    $"{forceId} must inherit the common ASRS data");
                Assert.That(
                    profile.Additions.Count,
                    Is.EqualTo(forceId == "WEYU" ? 1 : 0),
                    $"{forceId} additions must remain sparse");
                Assert.That(
                    profile.Exclusions.Count,
                    Is.EqualTo(forceId == "WEYU" ? 1 : 0),
                    $"{forceId} exclusions must remain sparse");

                var resolved = RoundForceAsrsProfileCompiler.Compile(profile);
                Assert.That(resolved.Force.Value, Is.EqualTo(forceId));
                Assert.That(
                    resolved.Categories.Select(category => category.Id.Value),
                    Is.EqualTo(new[] { "Pouches", "VehicleAmmo" }));
                Assert.That(
                    resolved.Categories.Select(category => category.Name),
                    Is.EqualTo(new[] { "Pouches", "Vehicle Ammo" }));

                var pouches = resolved.Categories[0].Offers;
                var expectedPouchCrate = forceId == "WEYU"
                    ? "RMCCrateClothingMagazinePouchesLargePMC"
                    : "RMCCrateClothingMagazinePouchesLarge";
                Assert.Multiple(() =>
                {
                    Assert.That(pouches.Length, Is.EqualTo(1));
                    Assert.That(pouches[0].Id.Value, Is.EqualTo("LargeMagazinePouches"));
                    Assert.That(pouches[0].Crate.Id, Is.EqualTo(expectedPouchCrate));
                    Assert.That(pouches[0].Cost, Is.EqualTo(150));
                    Assert.That(pouches[0].Stock, Is.Null);
                });

                var vehicleAmmo = resolved.Categories[1].Offers;
                Assert.That(vehicleAmmo.Length, Is.EqualTo(ExpectedVehicleAmmo.Length));
                for (var index = 0; index < ExpectedVehicleAmmo.Length; index++)
                {
                    var expected = ExpectedVehicleAmmo[index];
                    var actual = vehicleAmmo[index];
                    Assert.Multiple(() =>
                    {
                        Assert.That(actual.Id.Value, Is.EqualTo(expected.Id), $"{forceId} offer {index}");
                        Assert.That(actual.Crate.Id, Is.EqualTo(expected.Crate), $"{forceId} offer {index}");
                        Assert.That(actual.Cost, Is.EqualTo(expected.Cost), $"{forceId} offer {index}");
                        Assert.That(
                            actual.Stock,
                            Is.EqualTo(new RoundAsrsStockPolicy(2, TimeSpan.FromSeconds(300))),
                            $"{forceId} offer {index}");
                    });
                }

                foreach (var category in resolved.Categories)
                {
                    foreach (var offer in category.Offers)
                    {
                        Assert.That(
                            prototypes.TryIndex<EntityPrototype>(offer.Crate, out _),
                            Is.True,
                            $"{forceId} references missing crate {offer.Crate}");
                    }
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CostOverrideChangesOnlyResolvedCost()
    {
        await AssertCompiledProfile(CostOverrideProfile, CostOverrideProfileId, resolved =>
        {
            Assert.That(
                resolved.TryGetOffer(new RoundAsrsOfferId("LtbCannonMixed"), out var offer),
                Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(offer!.Cost, Is.EqualTo(1234));
                Assert.That(
                    offer.Stock,
                    Is.EqualTo(new RoundAsrsStockPolicy(2, TimeSpan.FromSeconds(300))));
            });
        });
    }

    [Test]
    public async Task StockReplacementChangesOnlyResolvedStockPolicy()
    {
        await AssertCompiledProfile(StockReplacementProfile, StockReplacementProfileId, resolved =>
        {
            Assert.That(
                resolved.TryGetOffer(new RoundAsrsOfferId("LtbCannonMixed"), out var offer),
                Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(offer!.Cost, Is.EqualTo(1900));
                Assert.That(
                    offer.Stock,
                    Is.EqualTo(new RoundAsrsStockPolicy(
                        5,
                        TimeSpan.FromSeconds(45),
                        3,
                        2)));
            });
        });
    }

    [Test]
    public async Task StockClearRemovesOnlyResolvedStockPolicy()
    {
        await AssertCompiledProfile(StockClearProfile, StockClearProfileId, resolved =>
        {
            Assert.That(
                resolved.TryGetOffer(new RoundAsrsOfferId("LtbCannonMixed"), out var offer),
                Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(offer!.Cost, Is.EqualTo(1900));
                Assert.That(offer.Stock, Is.Null);
            });
        });
    }

    [Test]
    public async Task InvalidStockOverrideMappingsFailDeterministically()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        var server = pair.Server;

        var changed = new Dictionary<Type, HashSet<string>>();
        server.ProtoMan.LoadString(InvalidOverrideProfiles, changed: changed);
        await server.WaitPost(() => server.ProtoMan.ReloadPrototypes(changed));

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            var emptyOverride = GetProfile(prototypes, factory, "CMURoundForceAsrsEmptyOverrideTest");
            var emptyException = Assert.Throws<RoundAsrsCatalogResolutionException>(() =>
                RoundForceAsrsProfileCompiler.Compile(emptyOverride));
            Assert.That(emptyException!.Code, Is.EqualTo(RoundAsrsCatalogError.EmptyTermsOverride));

            Assert.Throws<ArgumentException>(() => RoundForceAsrsProfileCompiler.Compile(
                GetProfile(prototypes, factory, "CMURoundForceAsrsUnchangedPolicyTest")));
            Assert.Throws<ArgumentException>(() => RoundForceAsrsProfileCompiler.Compile(
                GetProfile(prototypes, factory, "CMURoundForceAsrsMissingReplacementPolicyTest")));
            Assert.Throws<ArgumentException>(() => RoundForceAsrsProfileCompiler.Compile(
                GetProfile(prototypes, factory, "CMURoundForceAsrsClearPolicyTest")));
        });

        await pair.CleanReturnAsync();
    }

    private static async Task AssertCompiledProfile(
        string yaml,
        string profileId,
        Action<ResolvedRoundAsrsCatalog> assertion)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        var server = pair.Server;

        var changed = new Dictionary<Type, HashSet<string>>();
        server.ProtoMan.LoadString(yaml, changed: changed);
        await server.WaitPost(() => server.ProtoMan.ReloadPrototypes(changed));

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;
            var resolved = RoundForceAsrsProfileCompiler.Compile(
                GetProfile(prototypes, factory, profileId));
            assertion(resolved);
        });

        await pair.CleanReturnAsync();
    }

    private static RoundForceAsrsProfileComponent GetProfile(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        string profileId)
    {
        var entity = prototypes.Index<EntityPrototype>(profileId);
        Assert.That(
            entity.TryGetComponent<RoundForceAsrsProfileComponent>(out var profile, factory),
            Is.True);
        return profile!;
    }

    private sealed record ExpectedOffer(string Id, string Crate, int Cost);
}
