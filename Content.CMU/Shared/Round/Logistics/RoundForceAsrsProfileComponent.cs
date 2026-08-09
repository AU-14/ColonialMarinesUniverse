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
    public List<RoundForceAsrsCategoryPrototypeDefinition> Categories { get; private set; } = new();

    [DataField]
    public List<RoundForceAsrsOfferAdditionPrototypeDefinition> Additions { get; private set; } = new();

    [DataField]
    public List<string> Exclusions { get; private set; } = new();
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

        return RoundAsrsCatalogResolver.Resolve(
            new RoundAsrsCatalogDefinition(categories),
            new RoundAsrsForceDelta(
                new RoundForceId(profile.ForceId ?? string.Empty),
                additions,
                exclusions));
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
                : new RoundAsrsStockPolicy(
                    offer.Stock.Maximum,
                    TimeSpan.FromSeconds(offer.Stock.ReplenishDelay),
                    offer.Stock.StartingStock,
                    offer.Stock.ReplenishAmount));
    }
}
