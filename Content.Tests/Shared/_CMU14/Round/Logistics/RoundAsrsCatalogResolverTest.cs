#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Content.Shared.CMU.Round;
using NUnit.Framework;

namespace Content.Tests.Shared._CMU14.Round.Logistics;

[TestFixture]
public sealed class RoundAsrsCatalogResolverTest
{
    private static readonly RoundForceId Force = new("USCM");
    private static readonly RoundAsrsCategoryId Pouches = new("Pouches");
    private static readonly RoundAsrsCategoryId VehicleAmmo = new("VehicleAmmo");
    private static readonly RoundAsrsOfferId NormalPouch = new("LargeMagazinePouches");
    private static readonly RoundAsrsOfferId Cannon = new("LtbCannonMixed");
    private static readonly RoundAsrsOfferId Autocannon = new("AceAutocannon");
    private static readonly RoundAsrsOfferId Flamer = new("DragonFlamer");
    private static readonly RoundAsrsOfferId SmokeLauncher = new("SmokeLauncher");

    [Test]
    public void EmptyDeltaPreservesOrderAndStockPolicy()
    {
        var source = CreateCatalog();

        var resolved = RoundAsrsCatalogResolver.Resolve(
            source,
            new RoundAsrsForceDelta(Force));

        Assert.Multiple(() =>
        {
            Assert.That(resolved.Force, Is.EqualTo(Force));
            Assert.That(
                resolved.Categories.Select(category => category.Id),
                Is.EqualTo(new[] { Pouches, VehicleAmmo }));
            Assert.That(
                resolved.Categories[1].Offers.Select(offer => offer.Id),
                Is.EqualTo(new[] { Cannon, Autocannon, Flamer }));
            Assert.That(
                resolved.Categories[1].Offers.Select(offer => offer.Stock),
                Is.All.EqualTo(new RoundAsrsStockPolicy(2, TimeSpan.FromSeconds(300))));
        });
    }

    [Test]
    public void SparseDeltaComposesWithoutMutatingTheCommonCatalog()
    {
        var source = CreateCatalog();
        var delta = new RoundAsrsForceDelta(
            Force,
            additions:
            [
                new RoundAsrsOfferAddition(
                    Pouches,
                    new RoundAsrsOfferDefinition(
                        NormalPouch,
                        "RMCCrateClothingMagazinePouchesLargePMC",
                        150)),
                new RoundAsrsOfferAddition(
                    VehicleAmmo,
                    new RoundAsrsOfferDefinition(
                        SmokeLauncher,
                        "RMCCrateVehicleAmmoSmokeLauncher",
                        700,
                        new RoundAsrsStockPolicy(2, TimeSpan.FromSeconds(300))),
                    Flamer),
            ],
            exclusions: [NormalPouch],
            overrides:
            [
                new RoundAsrsOfferTermsOverride(
                    Cannon,
                    1950),
                new RoundAsrsOfferTermsOverride(
                    Autocannon,
                    Stock: RoundAsrsStockOverride.Clear),
                new RoundAsrsOfferTermsOverride(
                    Flamer,
                    Stock: RoundAsrsStockOverride.ReplaceWith(
                        new RoundAsrsStockPolicy(3, TimeSpan.FromSeconds(240)))),
            ]);

        var resolved = RoundAsrsCatalogResolver.Resolve(source, delta);
        var pouches = resolved.Categories.Single(category => category.Id == Pouches);
        var vehicleAmmo = resolved.Categories.Single(category => category.Id == VehicleAmmo);
        var foundPouch = resolved.TryGetOffer(NormalPouch, out var resolvedPouch);
        var foundMissing = resolved.TryGetOffer(new RoundAsrsOfferId("Missing"), out var missing);

        Assert.Multiple(() =>
        {
            Assert.That(
                pouches.Offers.Select(offer => offer.Id),
                Is.EqualTo(new[] { NormalPouch }));
            Assert.That(foundPouch, Is.True);
            Assert.That(foundMissing, Is.False);
            Assert.That(missing, Is.Null);
            Assert.That(
                resolvedPouch!.Crate.Id,
                Is.EqualTo("RMCCrateClothingMagazinePouchesLargePMC"));
            Assert.That(
                vehicleAmmo.Offers.Select(offer => offer.Id),
                Is.EqualTo(new[] { Cannon, Autocannon, SmokeLauncher, Flamer }));
            Assert.That(vehicleAmmo.Offers[0].Cost, Is.EqualTo(1950));
            Assert.That(
                vehicleAmmo.Offers[0].Stock,
                Is.EqualTo(new RoundAsrsStockPolicy(2, TimeSpan.FromSeconds(300))));
            Assert.That(vehicleAmmo.Offers[1].Stock, Is.Null);
            Assert.That(vehicleAmmo.Offers[1].Cost, Is.EqualTo(1800));
            Assert.That(
                vehicleAmmo.Offers[3].Stock,
                Is.EqualTo(new RoundAsrsStockPolicy(3, TimeSpan.FromSeconds(240))));
            Assert.That(vehicleAmmo.Offers[3].Cost, Is.EqualTo(1700));
            Assert.That(
                source.Categories[0].Offers.Select(offer => offer.Id),
                Is.EqualTo(new[] { NormalPouch }));
            Assert.That(
                source.Categories[1].Offers.Select(offer => offer.Id),
                Is.EqualTo(new[] { Cannon, Autocannon, Flamer }));
            Assert.That(source.Categories[1].Offers[0].Cost, Is.EqualTo(1900));
        });
    }

    [Test]
    public void SameIdReplacementKeepsItsCommonCatalogPosition()
    {
        var source = CreateCatalog();
        var replacement = new RoundAsrsOfferDefinition(
            Autocannon,
            "ForceSpecificAutocannonCrate",
            1850,
            new RoundAsrsStockPolicy(2, TimeSpan.FromSeconds(300)));

        var resolved = RoundAsrsCatalogResolver.Resolve(
            source,
            new RoundAsrsForceDelta(
                Force,
                additions: [new RoundAsrsOfferAddition(VehicleAmmo, replacement)],
                exclusions: [Autocannon]));
        var vehicleAmmo = resolved.Categories.Single(category => category.Id == VehicleAmmo);

        Assert.Multiple(() =>
        {
            Assert.That(
                vehicleAmmo.Offers.Select(offer => offer.Id),
                Is.EqualTo(new[] { Cannon, Autocannon, Flamer }));
            Assert.That(vehicleAmmo.Offers[1].Crate.Id, Is.EqualTo("ForceSpecificAutocannonCrate"));
            Assert.That(vehicleAmmo.Offers[1].Cost, Is.EqualTo(1850));
        });
    }

    [Test]
    public void AuthoringCollectionsAreCopiedAndRejectNullElements()
    {
        var offers = new List<RoundAsrsOfferDefinition>
        {
            new(NormalPouch, "SomeCrate", 10),
        };
        var category = new RoundAsrsCategoryDefinition(Pouches, "Pouches", offers);
        var categories = new List<RoundAsrsCategoryDefinition> { category };
        var source = new RoundAsrsCatalogDefinition(categories);
        offers.Clear();
        categories.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(source.Categories.Length, Is.EqualTo(1));
            Assert.That(source.Categories[0].Offers.Length, Is.EqualTo(1));
            Assert.That(
                () => new RoundAsrsCategoryDefinition(
                    Pouches,
                    "Pouches",
                    new RoundAsrsOfferDefinition[] { null! }),
                Throws.ArgumentException);
            Assert.That(
                () => new RoundAsrsCatalogDefinition(
                    new RoundAsrsCategoryDefinition[] { null! }),
                Throws.ArgumentException);
            Assert.That(
                () => new RoundAsrsForceDelta(
                    Force,
                    additions: new RoundAsrsOfferAddition[] { null! }),
                Throws.ArgumentException);
        });
    }

    [TestCaseSource(nameof(InvalidDefinitions))]
    public void InvalidDefinitionsReturnStableCodes(
        RoundAsrsCatalogDefinition source,
        RoundAsrsForceDelta delta,
        RoundAsrsCatalogError expected)
    {
        var exception = Assert.Throws<RoundAsrsCatalogResolutionException>(
            () => RoundAsrsCatalogResolver.Resolve(source, delta));

        Assert.That(exception!.Code, Is.EqualTo(expected));
    }

    private static IEnumerable<TestCaseData> InvalidDefinitions()
    {
        yield return InvalidCase(
            CreateCatalog(),
            new RoundAsrsForceDelta(default),
            RoundAsrsCatalogError.InvalidForceId);
        yield return InvalidCase(
            new RoundAsrsCatalogDefinition(
            [
                new RoundAsrsCategoryDefinition(default, "Broken", []),
            ]),
            new RoundAsrsForceDelta(Force),
            RoundAsrsCatalogError.InvalidCategoryId);
        yield return InvalidCase(
            new RoundAsrsCatalogDefinition(
            [
                new RoundAsrsCategoryDefinition(Pouches, "", []),
            ]),
            new RoundAsrsForceDelta(Force),
            RoundAsrsCatalogError.InvalidCategoryName);
        yield return InvalidCase(
            new RoundAsrsCatalogDefinition(
            [
                new RoundAsrsCategoryDefinition(Pouches, "Pouches", []),
                new RoundAsrsCategoryDefinition(Pouches, "Again", []),
            ]),
            new RoundAsrsForceDelta(Force),
            RoundAsrsCatalogError.DuplicateCategoryId);
        yield return InvalidCase(
            new RoundAsrsCatalogDefinition(
            [
                new RoundAsrsCategoryDefinition(
                    Pouches,
                    "Pouches",
                    [new RoundAsrsOfferDefinition(default, "SomeCrate", 10)]),
            ]),
            new RoundAsrsForceDelta(Force),
            RoundAsrsCatalogError.InvalidOfferId);
        yield return InvalidCase(
            new RoundAsrsCatalogDefinition(
            [
                new RoundAsrsCategoryDefinition(
                    Pouches,
                    "Pouches",
                    [new RoundAsrsOfferDefinition(NormalPouch, default, 10)]),
            ]),
            new RoundAsrsForceDelta(Force),
            RoundAsrsCatalogError.MissingCrateId);
        yield return InvalidCase(
            new RoundAsrsCatalogDefinition(
            [
                new RoundAsrsCategoryDefinition(
                    Pouches,
                    "Pouches",
                    [new RoundAsrsOfferDefinition(NormalPouch, "SomeCrate", -1)]),
            ]),
            new RoundAsrsForceDelta(Force),
            RoundAsrsCatalogError.NegativeCost);
        yield return InvalidCase(
            new RoundAsrsCatalogDefinition(
            [
                new RoundAsrsCategoryDefinition(
                    Pouches,
                    "Pouches",
                    [
                        new RoundAsrsOfferDefinition(
                            NormalPouch,
                            "SomeCrate",
                            10,
                            new RoundAsrsStockPolicy(0, TimeSpan.FromSeconds(30))),
                    ]),
            ]),
            new RoundAsrsForceDelta(Force),
            RoundAsrsCatalogError.InvalidStockMaximum);
        yield return InvalidCase(
            new RoundAsrsCatalogDefinition(
            [
                new RoundAsrsCategoryDefinition(
                    Pouches,
                    "Pouches",
                    [
                        new RoundAsrsOfferDefinition(
                            NormalPouch,
                            "SomeCrate",
                            10,
                            new RoundAsrsStockPolicy(2, TimeSpan.Zero)),
                    ]),
            ]),
            new RoundAsrsForceDelta(Force),
            RoundAsrsCatalogError.InvalidStockReplenishDelay);
        yield return InvalidCase(
            new RoundAsrsCatalogDefinition(
            [
                new RoundAsrsCategoryDefinition(
                    Pouches,
                    "Pouches",
                    [
                        new RoundAsrsOfferDefinition(
                            NormalPouch,
                            "SomeCrate",
                            10,
                            new RoundAsrsStockPolicy(2, TimeSpan.FromSeconds(30), 3)),
                    ]),
            ]),
            new RoundAsrsForceDelta(Force),
            RoundAsrsCatalogError.InvalidStartingStock);
        yield return InvalidCase(
            new RoundAsrsCatalogDefinition(
            [
                new RoundAsrsCategoryDefinition(
                    Pouches,
                    "Pouches",
                    [
                        new RoundAsrsOfferDefinition(
                            NormalPouch,
                            "SomeCrate",
                            10,
                            new RoundAsrsStockPolicy(2, TimeSpan.FromSeconds(30), ReplenishAmount: 0)),
                    ]),
            ]),
            new RoundAsrsForceDelta(Force),
            RoundAsrsCatalogError.InvalidReplenishAmount);
        yield return InvalidCase(
            new RoundAsrsCatalogDefinition(
            [
                new RoundAsrsCategoryDefinition(
                    Pouches,
                    "Pouches",
                    [
                        new RoundAsrsOfferDefinition(NormalPouch, "SameCrate", 10),
                        new RoundAsrsOfferDefinition(new RoundAsrsOfferId("Other"), "SameCrate", 20),
                    ]),
            ]),
            new RoundAsrsForceDelta(Force),
            RoundAsrsCatalogError.DuplicateCrateInCategory);
        yield return InvalidCase(
            CreateCatalog(),
            new RoundAsrsForceDelta(Force, exclusions: [new RoundAsrsOfferId("Missing")]),
            RoundAsrsCatalogError.UnknownExclusionTarget);
        yield return InvalidCase(
            CreateCatalog(),
            new RoundAsrsForceDelta(Force, exclusions: [Cannon, Cannon]),
            RoundAsrsCatalogError.ConflictingDeltaOperations);
        yield return InvalidCase(
            CreateCatalog(),
            new RoundAsrsForceDelta(
                Force,
                additions:
                [
                    new RoundAsrsOfferAddition(
                        new RoundAsrsCategoryId("Missing"),
                        new RoundAsrsOfferDefinition(new RoundAsrsOfferId("Added"), "SomeCrate", 10)),
                ]),
            RoundAsrsCatalogError.UnknownAdditionCategory);
        yield return InvalidCase(
            CreateCatalog(),
            new RoundAsrsForceDelta(
                Force,
                additions:
                [
                    new RoundAsrsOfferAddition(
                        Pouches,
                        new RoundAsrsOfferDefinition(new RoundAsrsOfferId("Added"), "SomeCrate", 10),
                        new RoundAsrsOfferId("Missing")),
                ]),
            RoundAsrsCatalogError.UnknownInsertionAnchor);
        yield return InvalidCase(
            CreateCatalog(),
            new RoundAsrsForceDelta(
                Force,
                additions:
                [
                    new RoundAsrsOfferAddition(
                        Pouches,
                        new RoundAsrsOfferDefinition(new RoundAsrsOfferId("Added"), "SomeCrate", 10),
                        Cannon),
                ]),
            RoundAsrsCatalogError.CrossCategoryInsertionAnchor);
        yield return InvalidCase(
            CreateCatalog(),
            new RoundAsrsForceDelta(
                Force,
                overrides:
                [
                    new RoundAsrsOfferTermsOverride(new RoundAsrsOfferId("Missing"), Cost: 10),
                ]),
            RoundAsrsCatalogError.UnknownOverrideTarget);
        yield return InvalidCase(
            CreateCatalog(),
            new RoundAsrsForceDelta(
                Force,
                overrides: [new RoundAsrsOfferTermsOverride(Cannon)]),
            RoundAsrsCatalogError.EmptyTermsOverride);
        yield return InvalidCase(
            new RoundAsrsCatalogDefinition(
            [
                new RoundAsrsCategoryDefinition(
                    Pouches,
                    "Pouches",
                    [new RoundAsrsOfferDefinition(NormalPouch, "SomeCrate", 10)]),
                new RoundAsrsCategoryDefinition(
                    VehicleAmmo,
                    "Vehicle Ammo",
                    [new RoundAsrsOfferDefinition(NormalPouch, "OtherCrate", 20)]),
            ]),
            new RoundAsrsForceDelta(Force),
            RoundAsrsCatalogError.DuplicateOfferId);
    }

    private static TestCaseData InvalidCase(
        RoundAsrsCatalogDefinition source,
        RoundAsrsForceDelta delta,
        RoundAsrsCatalogError expected)
    {
        return new TestCaseData(source, delta, expected).SetName($"Invalid_{expected}");
    }

    private static RoundAsrsCatalogDefinition CreateCatalog()
    {
        var stock = new RoundAsrsStockPolicy(2, TimeSpan.FromSeconds(300));
        return new RoundAsrsCatalogDefinition(
        [
            new RoundAsrsCategoryDefinition(
                Pouches,
                "Pouches",
                [
                    new RoundAsrsOfferDefinition(
                        NormalPouch,
                        "RMCCrateClothingMagazinePouchesLarge",
                        150),
                ]),
            new RoundAsrsCategoryDefinition(
                VehicleAmmo,
                "Vehicle Ammo",
                [
                    new RoundAsrsOfferDefinition(
                        Cannon,
                        "CMUCrateVehicleAmmoLTBCannonMixed",
                        1900,
                        stock),
                    new RoundAsrsOfferDefinition(
                        Autocannon,
                        "RMCCrateVehicleAmmoAceAutocannon",
                        1800,
                        stock),
                    new RoundAsrsOfferDefinition(
                        Flamer,
                        "RMCCrateVehicleAmmoDragonFlamer",
                        1700,
                        stock),
                ]),
        ]);
    }
}
