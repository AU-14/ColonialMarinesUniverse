#nullable enable

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU.Round;

/// <summary>
/// Stable identity of an ASRS category, independent from its display name.
/// </summary>
public readonly record struct RoundAsrsCategoryId
{
    public RoundAsrsCategoryId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string? Value { get; }

    public bool IsValid => !string.IsNullOrWhiteSpace(Value);

    public override string ToString()
    {
        return Value ?? string.Empty;
    }
}

/// <summary>
/// Stable identity of an ASRS offer across force-specific catalog changes.
/// </summary>
public readonly record struct RoundAsrsOfferId
{
    public RoundAsrsOfferId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string? Value { get; }

    public bool IsValid => !string.IsNullOrWhiteSpace(Value);

    public override string ToString()
    {
        return Value ?? string.Empty;
    }
}

/// <summary>
/// Stock limits and replenishment terms for one resolved offer.
/// A starting stock of -1 means that the offer starts full.
/// </summary>
public readonly record struct RoundAsrsStockPolicy(
    int Maximum,
    TimeSpan ReplenishDelay,
    int StartingStock = -1,
    int ReplenishAmount = 1);

/// <summary>
/// One common or force-specific ASRS offer before catalog resolution.
/// </summary>
public sealed class RoundAsrsOfferDefinition
{
    public RoundAsrsOfferId Id { get; }
    public EntProtoId Crate { get; }
    public int Cost { get; }
    public RoundAsrsStockPolicy? Stock { get; }

    public RoundAsrsOfferDefinition(
        RoundAsrsOfferId id,
        EntProtoId crate,
        int cost,
        RoundAsrsStockPolicy? stock = null)
    {
        Id = id;
        Crate = crate;
        Cost = cost;
        Stock = stock;
    }
}

/// <summary>
/// One ordered category in the common ASRS catalog.
/// </summary>
public sealed class RoundAsrsCategoryDefinition
{
    public RoundAsrsCategoryId Id { get; }
    public string Name { get; }
    public ImmutableArray<RoundAsrsOfferDefinition> Offers { get; }

    public RoundAsrsCategoryDefinition(
        RoundAsrsCategoryId id,
        string name,
        IEnumerable<RoundAsrsOfferDefinition> offers)
    {
        ArgumentNullException.ThrowIfNull(offers);

        Id = id;
        Name = name;
        Offers = offers.ToImmutableArray();
        if (Offers.Any(offer => offer == null))
            throw new ArgumentException("An ASRS category cannot contain a null offer.", nameof(offers));
    }
}

/// <summary>
/// Ordered common ASRS data to which a force delta is applied.
/// </summary>
public sealed class RoundAsrsCatalogDefinition
{
    public ImmutableArray<RoundAsrsCategoryDefinition> Categories { get; }

    public RoundAsrsCatalogDefinition(IEnumerable<RoundAsrsCategoryDefinition> categories)
    {
        ArgumentNullException.ThrowIfNull(categories);
        Categories = categories.ToImmutableArray();
        if (Categories.Any(category => category == null))
            throw new ArgumentException("An ASRS catalog cannot contain a null category.", nameof(categories));
    }
}

/// <summary>
/// Adds an offer to a category, optionally immediately before a surviving common offer
/// or an offer declared by an earlier addition.
/// </summary>
public sealed class RoundAsrsOfferAddition
{
    public RoundAsrsCategoryId Category { get; }
    public RoundAsrsOfferDefinition Offer { get; }
    public RoundAsrsOfferId? InsertBefore { get; }

    public RoundAsrsOfferAddition(
        RoundAsrsCategoryId category,
        RoundAsrsOfferDefinition offer,
        RoundAsrsOfferId? insertBefore = null)
    {
        ArgumentNullException.ThrowIfNull(offer);

        Category = category;
        Offer = offer;
        InsertBefore = insertBefore;
    }
}

public enum RoundAsrsStockOverrideKind : byte
{
    Unchanged,
    Replace,
    Clear,
}

/// <summary>
/// Explicitly keeps, replaces, or clears an offer's common stock policy.
/// </summary>
public readonly record struct RoundAsrsStockOverride
{
    public RoundAsrsStockOverrideKind Kind { get; }
    public RoundAsrsStockPolicy? Policy { get; }

    private RoundAsrsStockOverride(
        RoundAsrsStockOverrideKind kind,
        RoundAsrsStockPolicy? policy)
    {
        Kind = kind;
        Policy = policy;
    }

    public static RoundAsrsStockOverride ReplaceWith(RoundAsrsStockPolicy policy)
    {
        return new RoundAsrsStockOverride(RoundAsrsStockOverrideKind.Replace, policy);
    }

    public static RoundAsrsStockOverride Clear =>
        new(RoundAsrsStockOverrideKind.Clear, null);
}

/// <summary>
/// Replaces the price and stock terms of an existing common offer.
/// </summary>
public readonly record struct RoundAsrsOfferTermsOverride(
    RoundAsrsOfferId Offer,
    int? Cost = null,
    RoundAsrsStockOverride Stock = default);

/// <summary>
/// Sparse changes that turn the common catalog into one force's catalog.
/// Exclusions are applied first, then ordered additions, then terms overrides.
/// An exclusion followed by an addition with the same offer ID is a replacement.
/// </summary>
public sealed class RoundAsrsForceDelta
{
    public RoundForceId Force { get; }
    public ImmutableArray<RoundAsrsOfferAddition> Additions { get; }
    public ImmutableArray<RoundAsrsOfferId> Exclusions { get; }
    public ImmutableArray<RoundAsrsOfferTermsOverride> Overrides { get; }

    public RoundAsrsForceDelta(
        RoundForceId force,
        IEnumerable<RoundAsrsOfferAddition>? additions = null,
        IEnumerable<RoundAsrsOfferId>? exclusions = null,
        IEnumerable<RoundAsrsOfferTermsOverride>? overrides = null)
    {
        Force = force;
        Additions = additions?.ToImmutableArray() ?? ImmutableArray<RoundAsrsOfferAddition>.Empty;
        if (Additions.Any(addition => addition == null))
            throw new ArgumentException("An ASRS force delta cannot contain a null addition.", nameof(additions));
        Exclusions = exclusions?.ToImmutableArray() ?? ImmutableArray<RoundAsrsOfferId>.Empty;
        Overrides = overrides?.ToImmutableArray() ?? ImmutableArray<RoundAsrsOfferTermsOverride>.Empty;
    }
}

/// <summary>
/// One immutable offer detached from authoring data and runtime requisitions DTOs.
/// </summary>
public sealed class ResolvedRoundAsrsOffer
{
    public RoundAsrsOfferId Id { get; }
    public EntProtoId Crate { get; }
    public int Cost { get; }
    public RoundAsrsStockPolicy? Stock { get; }

    internal ResolvedRoundAsrsOffer(
        RoundAsrsOfferId id,
        EntProtoId crate,
        int cost,
        RoundAsrsStockPolicy? stock)
    {
        Id = id;
        Crate = crate;
        Cost = cost;
        Stock = stock;
    }
}

/// <summary>
/// One immutable, ordered category in a resolved force catalog.
/// </summary>
public sealed class ResolvedRoundAsrsCategory
{
    public RoundAsrsCategoryId Id { get; }
    public string Name { get; }
    public ImmutableArray<ResolvedRoundAsrsOffer> Offers { get; }

    internal ResolvedRoundAsrsCategory(
        RoundAsrsCategoryId id,
        string name,
        ImmutableArray<ResolvedRoundAsrsOffer> offers)
    {
        Id = id;
        Name = name;
        Offers = offers;
    }
}

/// <summary>
/// Immutable ASRS catalog resolved for one force before the round plan is committed.
/// </summary>
public sealed class ResolvedRoundAsrsCatalog
{
    private readonly FrozenDictionary<RoundAsrsOfferId, ResolvedRoundAsrsOffer> _offersById;

    public RoundForceId Force { get; }
    public ImmutableArray<ResolvedRoundAsrsCategory> Categories { get; }

    internal ResolvedRoundAsrsCatalog(
        RoundForceId force,
        ImmutableArray<ResolvedRoundAsrsCategory> categories,
        FrozenDictionary<RoundAsrsOfferId, ResolvedRoundAsrsOffer> offersById)
    {
        Force = force;
        Categories = categories;
        _offersById = offersById;
    }

    /// <summary>
    /// Resolves an offer by its stable identity without consulting prototype data.
    /// </summary>
    public bool TryGetOffer(
        RoundAsrsOfferId id,
        [MaybeNullWhen(false)]
        out ResolvedRoundAsrsOffer offer)
    {
        return _offersById.TryGetValue(id, out offer!);
    }
}

/// <summary>
/// Stable reason why ASRS authoring data could not be resolved.
/// </summary>
public enum RoundAsrsCatalogError : byte
{
    InvalidForceId,
    InvalidCategoryId,
    InvalidCategoryName,
    DuplicateCategoryId,
    InvalidOfferId,
    DuplicateOfferId,
    MissingCrateId,
    NegativeCost,
    InvalidStockMaximum,
    InvalidStockReplenishDelay,
    InvalidStartingStock,
    InvalidReplenishAmount,
    EmptyTermsOverride,
    DuplicateCrateInCategory,
    UnknownExclusionTarget,
    UnknownAdditionCategory,
    UnknownOverrideTarget,
    UnknownInsertionAnchor,
    CrossCategoryInsertionAnchor,
    ConflictingDeltaOperations,
}

public sealed class RoundAsrsCatalogResolutionException : Exception
{
    public RoundAsrsCatalogError Code { get; }

    internal RoundAsrsCatalogResolutionException(
        RoundAsrsCatalogError code,
        string message)
        : base(message)
    {
        Code = code;
    }
}
