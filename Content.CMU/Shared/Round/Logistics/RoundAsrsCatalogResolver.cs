#nullable enable

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Content.Shared.CMU.Round;

/// <summary>
/// Compiles common ASRS data and sparse force changes into a detached immutable catalog.
/// </summary>
public static class RoundAsrsCatalogResolver
{
    /// <summary>
    /// Applies one force's sparse changes to common data and returns a detached immutable catalog.
    /// </summary>
    public static ResolvedRoundAsrsCatalog Resolve(
        RoundAsrsCatalogDefinition common,
        RoundAsrsForceDelta delta)
    {
        if (common == null)
            throw new ArgumentException("The common ASRS catalog cannot be null.", nameof(common));
        if (delta == null)
            throw new ArgumentException("The ASRS force delta cannot be null.", nameof(delta));

        if (!delta.Force.IsValid)
            Throw(RoundAsrsCatalogError.InvalidForceId, "The ASRS force identifier is missing.");

        var categories = new List<MutableCategory>(common.Categories.Length);
        var categoriesById = new Dictionary<RoundAsrsCategoryId, MutableCategory>();
        var offersById = new Dictionary<RoundAsrsOfferId, ResolvedRoundAsrsOffer>();
        var offerCategories = new Dictionary<RoundAsrsOfferId, MutableCategory>();
        var declaredOfferIds = new HashSet<RoundAsrsOfferId>();
        var originalPositions = new Dictionary<RoundAsrsOfferId, OriginalOfferPosition>();

        foreach (var sourceCategory in common.Categories)
        {
            ValidateCategory(sourceCategory);
            if (categoriesById.ContainsKey(sourceCategory.Id))
            {
                Throw(
                    RoundAsrsCatalogError.DuplicateCategoryId,
                    $"ASRS category '{sourceCategory.Id}' is declared more than once.");
            }

            var category = new MutableCategory(sourceCategory.Id, sourceCategory.Name);
            categories.Add(category);
            categoriesById.Add(category.Id, category);

            foreach (var sourceOffer in sourceCategory.Offers)
            {
                ValidateOffer(sourceOffer);
                if (!declaredOfferIds.Add(sourceOffer.Id))
                {
                    Throw(
                        RoundAsrsCatalogError.DuplicateOfferId,
                        $"ASRS offer '{sourceOffer.Id}' is declared more than once.");
                }

                var offer = ResolveOffer(sourceOffer);
                originalPositions.Add(offer.Id, new OriginalOfferPosition(category, category.Offers.Count));
                category.Offers.Add(offer);
                offersById.Add(offer.Id, offer);
                offerCategories.Add(offer.Id, category);
            }
        }

        var excluded = ApplyExclusions(
            delta.Exclusions,
            declaredOfferIds,
            offersById,
            offerCategories);
        ApplyAdditions(
            delta.Additions,
            categoriesById,
            declaredOfferIds,
            excluded,
            originalPositions,
            offersById,
            offerCategories);
        ApplyOverrides(
            delta.Overrides,
            excluded,
            offersById,
            offerCategories);
        ValidateResolvedCrates(categories);

        var resolvedCategories = ImmutableArray.CreateBuilder<ResolvedRoundAsrsCategory>(categories.Count);
        foreach (var category in categories)
        {
            resolvedCategories.Add(new ResolvedRoundAsrsCategory(
                category.Id,
                category.Name,
                category.Offers.ToImmutableArray()));
        }

        return new ResolvedRoundAsrsCatalog(
            delta.Force,
            resolvedCategories.MoveToImmutable(),
            offersById.ToFrozenDictionary());
    }

    private static HashSet<RoundAsrsOfferId> ApplyExclusions(
        ImmutableArray<RoundAsrsOfferId> exclusions,
        ISet<RoundAsrsOfferId> declaredOfferIds,
        Dictionary<RoundAsrsOfferId, ResolvedRoundAsrsOffer> offersById,
        Dictionary<RoundAsrsOfferId, MutableCategory> offerCategories)
    {
        var excluded = new HashSet<RoundAsrsOfferId>();
        foreach (var offerId in exclusions)
        {
            ValidateOfferId(offerId);
            if (!excluded.Add(offerId))
            {
                Throw(
                    RoundAsrsCatalogError.ConflictingDeltaOperations,
                    $"ASRS offer '{offerId}' is excluded more than once.");
            }

            if (!offerCategories.Remove(offerId, out var category) ||
                !offersById.Remove(offerId))
            {
                Throw(
                    RoundAsrsCatalogError.UnknownExclusionTarget,
                    $"ASRS exclusion target '{offerId}' does not exist.");
            }

            category.Offers.RemoveAll(offer => offer.Id == offerId);
            declaredOfferIds.Remove(offerId);
        }

        return excluded;
    }

    private static void ApplyOverrides(
        ImmutableArray<RoundAsrsOfferTermsOverride> overrides,
        IReadOnlySet<RoundAsrsOfferId> excluded,
        Dictionary<RoundAsrsOfferId, ResolvedRoundAsrsOffer> offersById,
        Dictionary<RoundAsrsOfferId, MutableCategory> offerCategories)
    {
        var overridden = new HashSet<RoundAsrsOfferId>();
        foreach (var terms in overrides)
        {
            ValidateOfferId(terms.Offer);
            if (excluded.Contains(terms.Offer) || !overridden.Add(terms.Offer))
            {
                Throw(
                    RoundAsrsCatalogError.ConflictingDeltaOperations,
                    $"ASRS offer '{terms.Offer}' has conflicting force changes.");
            }

            if (!offersById.TryGetValue(terms.Offer, out var existing))
            {
                Throw(
                    RoundAsrsCatalogError.UnknownOverrideTarget,
                    $"ASRS override target '{terms.Offer}' does not exist.");
            }
            if (!offerCategories.TryGetValue(terms.Offer, out var category))
            {
                Throw(
                    RoundAsrsCatalogError.UnknownOverrideTarget,
                    $"ASRS override target '{terms.Offer}' has no category.");
            }

            if (terms.Cost == null && terms.Stock.Kind == RoundAsrsStockOverrideKind.Unchanged)
            {
                Throw(
                    RoundAsrsCatalogError.EmptyTermsOverride,
                    $"ASRS offer '{terms.Offer}' has an empty terms override.");
            }

            var cost = terms.Cost ?? existing.Cost;
            var stock = terms.Stock.Kind switch
            {
                RoundAsrsStockOverrideKind.Unchanged => existing.Stock,
                RoundAsrsStockOverrideKind.Replace when terms.Stock.Policy is { } stockPolicy => stockPolicy,
                RoundAsrsStockOverrideKind.Clear => null,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(terms.Stock),
                    terms.Stock.Kind,
                    "Unknown ASRS stock override kind."),
            };
            ValidateTerms(cost, stock, terms.Offer);
            var replacement = new ResolvedRoundAsrsOffer(
                existing.Id,
                existing.Crate,
                cost,
                stock);
            var index = category.Offers.FindIndex(offer => offer.Id == terms.Offer);
            category.Offers[index] = replacement;
            offersById[terms.Offer] = replacement;
        }
    }

    private static void ApplyAdditions(
        ImmutableArray<RoundAsrsOfferAddition> additions,
        Dictionary<RoundAsrsCategoryId, MutableCategory> categoriesById,
        ISet<RoundAsrsOfferId> declaredOfferIds,
        ISet<RoundAsrsOfferId> excluded,
        IReadOnlyDictionary<RoundAsrsOfferId, OriginalOfferPosition> originalPositions,
        Dictionary<RoundAsrsOfferId, ResolvedRoundAsrsOffer> offersById,
        Dictionary<RoundAsrsOfferId, MutableCategory> offerCategories)
    {
        foreach (var addition in additions)
        {
            if (!addition.Category.IsValid)
            {
                Throw(
                    RoundAsrsCatalogError.UnknownAdditionCategory,
                    $"ASRS addition category '{addition.Category}' does not exist.");
            }
            if (!categoriesById.TryGetValue(addition.Category, out var category))
            {
                Throw(
                    RoundAsrsCatalogError.UnknownAdditionCategory,
                    $"ASRS addition category '{addition.Category}' does not exist.");
            }

            ValidateOffer(addition.Offer);
            if (!declaredOfferIds.Add(addition.Offer.Id))
            {
                Throw(
                    RoundAsrsCatalogError.DuplicateOfferId,
                    $"ASRS addition '{addition.Offer.Id}' reuses an existing offer identifier.");
            }

            var insertAt = category.Offers.Count;
            if (addition.InsertBefore is { } anchor)
                insertAt = ResolveInsertionIndex(anchor, category, offerCategories);
            else if (excluded.Contains(addition.Offer.Id) &&
                     originalPositions.TryGetValue(addition.Offer.Id, out var original) &&
                     original.Category == category)
            {
                insertAt = ResolveReplacementIndex(category, original.Index, originalPositions);
            }

            var offer = ResolveOffer(addition.Offer);
            category.Offers.Insert(insertAt, offer);
            offersById.Add(offer.Id, offer);
            offerCategories.Add(offer.Id, category);
            excluded.Remove(offer.Id);
        }
    }

    private static int ResolveInsertionIndex(
        RoundAsrsOfferId anchor,
        MutableCategory category,
        Dictionary<RoundAsrsOfferId, MutableCategory> offerCategories)
    {
        ValidateOfferId(anchor);
        if (!offerCategories.TryGetValue(anchor, out var anchorCategory))
        {
            Throw(
                RoundAsrsCatalogError.UnknownInsertionAnchor,
                $"ASRS insertion anchor '{anchor}' does not exist.");
        }

        if (anchorCategory != category)
        {
            Throw(
                RoundAsrsCatalogError.CrossCategoryInsertionAnchor,
                $"ASRS insertion anchor '{anchor}' belongs to another category.");
        }

        var index = category.Offers.FindIndex(offer => offer.Id == anchor);
        if (index >= 0)
            return index;

        Throw(
            RoundAsrsCatalogError.UnknownInsertionAnchor,
            $"ASRS insertion anchor '{anchor}' is not available.");
        return default;
    }

    private static int ResolveReplacementIndex(
        MutableCategory category,
        int originalIndex,
        IReadOnlyDictionary<RoundAsrsOfferId, OriginalOfferPosition> originalPositions)
    {
        for (var index = 0; index < category.Offers.Count; index++)
        {
            if (!originalPositions.TryGetValue(category.Offers[index].Id, out var candidate) ||
                candidate.Category != category ||
                candidate.Index <= originalIndex)
            {
                continue;
            }

            return index;
        }

        return category.Offers.Count;
    }

    private static void ValidateCategory(RoundAsrsCategoryDefinition category)
    {
        if (!category.Id.IsValid)
            Throw(RoundAsrsCatalogError.InvalidCategoryId, "An ASRS category identifier is missing.");
        if (string.IsNullOrWhiteSpace(category.Name))
        {
            Throw(
                RoundAsrsCatalogError.InvalidCategoryName,
                $"ASRS category '{category.Id}' has no display name.");
        }
    }

    private static void ValidateOffer(RoundAsrsOfferDefinition offer)
    {
        ValidateOfferId(offer.Id);
        if (string.IsNullOrWhiteSpace(offer.Crate.Id))
        {
            Throw(
                RoundAsrsCatalogError.MissingCrateId,
                $"ASRS offer '{offer.Id}' has no crate prototype identifier.");
        }

        ValidateTerms(offer.Cost, offer.Stock, offer.Id);
    }

    private static void ValidateOfferId(RoundAsrsOfferId offerId)
    {
        if (!offerId.IsValid)
            Throw(RoundAsrsCatalogError.InvalidOfferId, "An ASRS offer identifier is missing.");
    }

    private static void ValidateTerms(
        int cost,
        RoundAsrsStockPolicy? stock,
        RoundAsrsOfferId offerId)
    {
        if (cost < 0)
        {
            Throw(
                RoundAsrsCatalogError.NegativeCost,
                $"ASRS offer '{offerId}' has a negative cost.");
        }

        if (stock is not { } policy)
            return;
        if (policy.Maximum <= 0)
        {
            Throw(
                RoundAsrsCatalogError.InvalidStockMaximum,
                $"ASRS offer '{offerId}' must have a positive stock maximum.");
        }

        if (policy.ReplenishDelay <= TimeSpan.Zero)
        {
            Throw(
                RoundAsrsCatalogError.InvalidStockReplenishDelay,
                $"ASRS offer '{offerId}' must have a positive replenishment delay.");
        }

        if (policy.StartingStock < -1 || policy.StartingStock > policy.Maximum)
        {
            Throw(
                RoundAsrsCatalogError.InvalidStartingStock,
                $"ASRS offer '{offerId}' has invalid starting stock.");
        }

        if (policy.ReplenishAmount <= 0)
        {
            Throw(
                RoundAsrsCatalogError.InvalidReplenishAmount,
                $"ASRS offer '{offerId}' must replenish a positive amount.");
        }
    }

    private static void ValidateResolvedCrates(IEnumerable<MutableCategory> categories)
    {
        foreach (var category in categories)
        {
            var crates = new HashSet<string>(StringComparer.Ordinal);
            foreach (var offer in category.Offers)
            {
                if (crates.Add(offer.Crate.Id))
                    continue;

                Throw(
                    RoundAsrsCatalogError.DuplicateCrateInCategory,
                    $"ASRS category '{category.Id}' contains crate '{offer.Crate}' more than once.");
            }
        }
    }

    private static ResolvedRoundAsrsOffer ResolveOffer(RoundAsrsOfferDefinition offer)
    {
        return new ResolvedRoundAsrsOffer(
            offer.Id,
            offer.Crate,
            offer.Cost,
            offer.Stock);
    }

    [DoesNotReturn]
    private static void Throw(RoundAsrsCatalogError code, string message)
    {
        throw new RoundAsrsCatalogResolutionException(code, message);
    }

    private sealed class MutableCategory
    {
        public RoundAsrsCategoryId Id { get; }
        public string Name { get; }
        public List<ResolvedRoundAsrsOffer> Offers { get; } = new();

        public MutableCategory(RoundAsrsCategoryId id, string name)
        {
            Id = id;
            Name = name;
        }
    }

    private readonly record struct OriginalOfferPosition(
        MutableCategory Category,
        int Index);
}
