#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU.Round;

/// <summary>
/// Force-owned ASRS authoring data carried by a hidden entity prototype and compiled into an immutable catalog.
/// </summary>
[RegisterComponent]
public sealed partial class RoundForceAsrsProfileComponent : Component
{
    /// <summary>
    /// Canonical force identifier. The shared abstract parent intentionally leaves this unset.
    /// </summary>
    [DataField]
    public string? ForceId { get; private set; }

    [DataField]
    [AlwaysPushInheritance]
    public List<RoundForceAsrsCategoryPrototypeDefinition> Categories { get; private set; } = new();

    [DataField]
    [AlwaysPushInheritance]
    public List<RoundForceAsrsOfferAdditionPrototypeDefinition> Additions { get; private set; } = new();

    [DataField]
    [AlwaysPushInheritance]
    public List<string> Exclusions { get; private set; } = new();

    /// <summary>
    /// Sparse price or stock changes keyed by stable offer identity.
    /// </summary>
    [DataField]
    [AlwaysPushInheritance]
    public List<RoundForceAsrsOfferOverridePrototypeDefinition> Overrides { get; private set; } = new();
}

[DataDefinition]
public sealed partial class RoundForceAsrsCategoryPrototypeDefinition
{
    [DataField(required: true)]
    public string Id { get; private set; } = string.Empty;

    [DataField(required: true)]
    public string Name { get; private set; } = string.Empty;

    [DataField(required: true)]
    public List<RoundForceAsrsOfferPrototypeDefinition> Offers { get; private set; } = new();
}

[DataDefinition]
public sealed partial class RoundForceAsrsOfferPrototypeDefinition
{
    [DataField(required: true)]
    public string Id { get; private set; } = string.Empty;

    [DataField(required: true)]
    public EntProtoId Crate { get; private set; }

    [DataField(required: true)]
    public int Cost { get; private set; }

    [DataField]
    public RoundForceAsrsStockPrototypeDefinition? Stock { get; private set; }
}

[DataDefinition]
public sealed partial class RoundForceAsrsStockPrototypeDefinition
{
    [DataField(required: true)]
    public int Maximum { get; private set; }

    /// <summary>
    /// Number of seconds between stock replenishments.
    /// </summary>
    [DataField(required: true)]
    public int ReplenishDelay { get; private set; }

    /// <summary>
    /// Initial stock, where -1 starts at the configured maximum.
    /// </summary>
    [DataField]
    public int StartingStock { get; private set; } = -1;

    [DataField]
    public int ReplenishAmount { get; private set; } = 1;
}

[DataDefinition]
public sealed partial class RoundForceAsrsOfferAdditionPrototypeDefinition
{
    [DataField(required: true)]
    public string Category { get; private set; } = string.Empty;

    [DataField(required: true)]
    public RoundForceAsrsOfferPrototypeDefinition Offer { get; private set; } = new();

    [DataField]
    public string? InsertBefore { get; private set; }
}

/// <summary>
/// Authoring data for sparse terms changes to one inherited ASRS offer.
/// </summary>
[DataDefinition]
public sealed partial class RoundForceAsrsOfferOverridePrototypeDefinition
{
    /// <summary>
    /// Stable identifier of the inherited offer whose terms are changed.
    /// </summary>
    [DataField(required: true)]
    public string Offer { get; private set; } = string.Empty;

    /// <summary>
    /// Replacement price, or omitted to preserve the inherited price.
    /// </summary>
    [DataField]
    public int? Cost { get; private set; }

    /// <summary>
    /// Stock-policy change, or omitted to preserve the inherited policy.
    /// </summary>
    [DataField]
    public RoundForceAsrsStockOverridePrototypeDefinition? Stock { get; private set; }
}

/// <summary>
/// Explicit authoring choice to keep, replace, or clear inherited ASRS stock terms.
/// </summary>
[DataDefinition]
public sealed partial class RoundForceAsrsStockOverridePrototypeDefinition
{
    /// <summary>
    /// Operation applied to the inherited stock policy.
    /// </summary>
    [DataField(required: true)]
    public RoundAsrsStockOverrideKind Kind { get; private set; }

    /// <summary>
    /// Replacement policy. Required for Replace; forbidden for Clear and Unchanged.
    /// </summary>
    [DataField]
    public RoundForceAsrsStockPrototypeDefinition? Policy { get; private set; }
}

/// <summary>
/// Copies inherited entity-component profile data into the standalone ASRS resolver.
/// </summary>
public static class RoundForceAsrsProfileCompiler
{
    /// <summary>
    /// Validates and detaches a force profile from mutable prototype data.
    /// </summary>
    public static ResolvedRoundAsrsCatalog Compile(RoundForceAsrsProfileComponent profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var categories = profile.Categories.Select(category =>
            new RoundAsrsCategoryDefinition(
                new RoundAsrsCategoryId(category.Id),
                category.Name,
                category.Offers.Select(CompileOffer)));
        var additions = profile.Additions.Select(addition =>
            new RoundAsrsOfferAddition(
                new RoundAsrsCategoryId(addition.Category),
                CompileOffer(addition.Offer),
                string.IsNullOrWhiteSpace(addition.InsertBefore)
                    ? null
                    : new RoundAsrsOfferId(addition.InsertBefore)));
        var exclusions = profile.Exclusions.Select(id => new RoundAsrsOfferId(id));
        var overrides = profile.Overrides.Select(terms =>
            new RoundAsrsOfferTermsOverride(
                new RoundAsrsOfferId(terms.Offer),
                terms.Cost,
                CompileStockOverride(terms.Stock)));

        return RoundAsrsCatalogResolver.Resolve(
            new RoundAsrsCatalogDefinition(categories),
            new RoundAsrsForceDelta(
                new RoundForceId(profile.ForceId ?? string.Empty),
                additions,
                exclusions,
                overrides));
    }

    private static RoundAsrsOfferDefinition CompileOffer(
        RoundForceAsrsOfferPrototypeDefinition offer)
    {
        return new RoundAsrsOfferDefinition(
            new RoundAsrsOfferId(offer.Id),
            offer.Crate,
            offer.Cost,
            offer.Stock == null
                ? null
                : CompileStock(offer.Stock));
    }

    private static RoundAsrsStockOverride CompileStockOverride(
        RoundForceAsrsStockOverridePrototypeDefinition? stock)
    {
        return stock switch
        {
            null => default,
            { Kind: RoundAsrsStockOverrideKind.Unchanged, Policy: null } => default,
            { Kind: RoundAsrsStockOverrideKind.Unchanged } => throw new ArgumentException(
                "An unchanged ASRS stock override cannot declare a replacement policy.",
                nameof(stock)),
            { Kind: RoundAsrsStockOverrideKind.Replace, Policy: { } policy } =>
                RoundAsrsStockOverride.ReplaceWith(CompileStock(policy)),
            { Kind: RoundAsrsStockOverrideKind.Replace } => throw new ArgumentException(
                "A replacement ASRS stock override requires a policy.",
                nameof(stock)),
            { Kind: RoundAsrsStockOverrideKind.Clear, Policy: null } => RoundAsrsStockOverride.Clear,
            { Kind: RoundAsrsStockOverrideKind.Clear } => throw new ArgumentException(
                "A clearing ASRS stock override cannot declare a replacement policy.",
                nameof(stock)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(stock),
                stock.Kind,
                "Unsupported ASRS stock override kind."),
        };
    }

    private static RoundAsrsStockPolicy CompileStock(
        RoundForceAsrsStockPrototypeDefinition stock)
    {
        return new RoundAsrsStockPolicy(
            stock.Maximum,
            TimeSpan.FromSeconds(stock.ReplenishDelay),
            stock.StartingStock,
            stock.ReplenishAmount);
    }
}
