#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Content.Shared.AU14.util;
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
    private static readonly RoundAsrsOfferId VehicleAmmoOfferId =
        new("VehicleAmmo_CMUCrateVehicleAmmoLTBCannonMixed");

    private const string MultiParentProfiles = """
        - type: entity
          abstract: true
          id: CMURoundForceAsrsInheritanceCommonTest
          components:
          - type: RoundForceAsrsProfile
            categories:
            - id: CommonCategory
              name: Common Category
              offers:
              - id: CommonCategoryOffer
                crate: RMCCrateClothingMagazinePouchesLarge
                cost: 100
            additions:
            - category: CommonCategory
              offer:
                id: CommonAddition
                crate: RMCCrateClothingMagazinePouchesLarge
                cost: 101
            exclusions:
            - CommonExclusion
            overrides:
            - offer: CommonOverride
              cost: 102

        - type: entity
          abstract: true
          id: CMURoundForceAsrsInheritanceFragmentTest
          components:
          - type: RoundForceAsrsProfile
            categories:
            - id: FragmentCategory
              name: Fragment Category
              offers:
              - id: FragmentCategoryOffer
                crate: RMCCrateClothingMagazinePouchesLarge
                cost: 200
            additions:
            - category: FragmentCategory
              offer:
                id: FragmentAddition
                crate: RMCCrateClothingMagazinePouchesLarge
                cost: 201
            exclusions:
            - FragmentExclusion
            overrides:
            - offer: FragmentOverride
              cost: 202

        - type: entity
          parent:
          - CMURoundForceAsrsInheritanceCommonTest
          - CMURoundForceAsrsInheritanceFragmentTest
          id: CMURoundForceAsrsInheritanceChildTest
          categories: [ HideSpawnMenu ]
          components:
          - type: RoundForceAsrsProfile
            forceId: InheritanceChildTest
            categories:
            - id: ChildCategory
              name: Child Category
              offers:
              - id: ChildCategoryOffer
                crate: RMCCrateClothingMagazinePouchesLarge
                cost: 300
            additions:
            - category: ChildCategory
              offer:
                id: ChildAddition
                crate: RMCCrateClothingMagazinePouchesLarge
                cost: 301
            exclusions:
            - ChildExclusion
            overrides:
            - offer: ChildOverride
              cost: 302
        """;

    private const string CostOverrideProfile = """
        - type: entity
          parent: CMURoundForceAsrsCommon
          id: CMURoundForceAsrsCostOverrideTest
          categories: [ HideSpawnMenu ]
          components:
          - type: RoundForceAsrsProfile
            forceId: CostOverrideTest
            overrides:
            - offer: VehicleAmmo_CMUCrateVehicleAmmoLTBCannonMixed
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
            - offer: VehicleAmmo_CMUCrateVehicleAmmoLTBCannonMixed
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
            - offer: VehicleAmmo_CMUCrateVehicleAmmoLTBCannonMixed
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
            - offer: VehicleAmmo_CMUCrateVehicleAmmoLTBCannonMixed

        - type: entity
          parent: CMURoundForceAsrsCommon
          id: CMURoundForceAsrsUnchangedPolicyTest
          categories: [ HideSpawnMenu ]
          components:
          - type: RoundForceAsrsProfile
            forceId: UnchangedPolicyTest
            overrides:
            - offer: VehicleAmmo_CMUCrateVehicleAmmoLTBCannonMixed
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
            - offer: VehicleAmmo_CMUCrateVehicleAmmoLTBCannonMixed
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
            - offer: VehicleAmmo_CMUCrateVehicleAmmoLTBCannonMixed
              stock:
                kind: Clear
                policy:
                  maximum: 2
                  replenishDelay: 300
        """;

    [Test]
    public async Task EveryLegacyProductionForceHasOneCompleteProfile()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;
            var productionForceIds = prototypes.EnumeratePrototypes<PlatoonPrototype>()
                .Where(platoon => platoon.VendorSet != null)
                .Select(platoon => platoon.ID)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var profiles = new List<(EntityPrototype Entity, RoundForceAsrsProfileComponent Profile)>();
            foreach (var entity in prototypes.EnumeratePrototypes<EntityPrototype>())
            {
                if (entity.Abstract ||
                    !entity.TryComp<RoundForceAsrsProfileComponent>(out var profile, factory))
                    continue;

                profiles.Add((entity, profile));
            }

            profiles.Sort((left, right) =>
                StringComparer.Ordinal.Compare(left.Profile.ForceId, right.Profile.ForceId));
            var profileForceIds = profiles
                .Select(profile => profile.Profile.ForceId ?? string.Empty)
                .ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(
                    profileForceIds,
                    Is.EqualTo(productionForceIds),
                    "Concrete ASRS profiles must exactly cover the legacy production force vocabulary selected by " +
                    "the current vendor-set compatibility boundary");
                Assert.That(
                    profileForceIds,
                    Is.Unique,
                    "Each legacy production force must have exactly one concrete ASRS profile");

                foreach (var (entity, profile) in profiles)
                {
                    Assert.That(
                        string.IsNullOrWhiteSpace(profile.ForceId),
                        Is.False,
                        $"Concrete profile {entity.ID} must declare a force ID");
                    Assert.That(
                        entity.HideSpawnMenu,
                        Is.True,
                        $"{entity.ID} must remain hidden from the entity list");
                    Assert.That(
                        entity.Parents,
                        Does.Contain(CommonProfileId),
                        $"{entity.ID} must directly declare the common ASRS profile parent");
                }
            });

            foreach (var (_, profile) in profiles)
            {
                var forceId = profile.ForceId!;
                var resolved = RoundForceAsrsProfileCompiler.Compile(profile);
                Assert.That(resolved.Force.Value, Is.EqualTo(forceId));
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
                resolved.TryGetOffer(VehicleAmmoOfferId, out var offer),
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
                resolved.TryGetOffer(VehicleAmmoOfferId, out var offer),
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
                resolved.TryGetOffer(VehicleAmmoOfferId, out var offer),
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

    [Test]
    public async Task MultiParentListsComposeChildThenDeclaredParents()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        var server = pair.Server;

        var changed = new Dictionary<Type, HashSet<string>>();
        server.ProtoMan.LoadString(MultiParentProfiles, changed: changed);
        await server.WaitPost(() => server.ProtoMan.ReloadPrototypes(changed));

        await server.WaitAssertion(() =>
        {
            var profile = GetProfile(
                server.ResolveDependency<IPrototypeManager>(),
                server.EntMan.ComponentFactory,
                "CMURoundForceAsrsInheritanceChildTest");

            Assert.Multiple(() =>
            {
                Assert.That(
                    profile.Categories.Select(category => category.Id),
                    Is.EqualTo(new[] { "ChildCategory", "CommonCategory", "FragmentCategory" }));
                Assert.That(
                    profile.Additions.Select(addition => addition.Offer.Id),
                    Is.EqualTo(new[] { "ChildAddition", "CommonAddition", "FragmentAddition" }));
                Assert.That(
                    profile.Exclusions,
                    Is.EqualTo(new[] { "ChildExclusion", "CommonExclusion", "FragmentExclusion" }));
                Assert.That(
                    profile.Overrides.Select(terms => terms.Offer),
                    Is.EqualTo(new[] { "ChildOverride", "CommonOverride", "FragmentOverride" }));
            });
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
            entity.TryComp<RoundForceAsrsProfileComponent>(out var profile, factory),
            Is.True);
        return profile!;
    }
}
