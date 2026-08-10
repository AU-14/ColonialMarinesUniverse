using System.Collections.Immutable;
using Content.Shared.CMU.Round;

namespace Content.Server.CMU.Round;

/// <summary>
/// Records the immutable force catalog identities and stock terms projected onto one side ASRS console.
/// Runtime requisitions DTOs remain separate mutable copies for UI and ordering compatibility.
/// </summary>
[RegisterComponent]
[Access(typeof(RoundAsrsConsoleCatalogSystem))]
public sealed partial class RoundAsrsConsoleCatalogComponent : Component
{
    /// <summary>
    /// Director generation whose committed catalog was projected onto this console.
    /// </summary>
    public int Generation { get; internal set; }

    /// <summary>
    /// Force whose committed catalog was projected onto this console.
    /// </summary>
    public RoundForceId Force { get; internal set; }

    /// <summary>
    /// Stable category identities in the same order as the runtime requisitions categories.
    /// </summary>
    public ImmutableArray<RoundAsrsCategoryId> CategoryIds { get; internal set; } = [];

    /// <summary>
    /// Stable offer identities for each runtime category and entry index.
    /// </summary>
    public ImmutableArray<ImmutableArray<RoundAsrsOfferId>> OfferIdsByCategory { get; internal set; } = [];

    /// <summary>
    /// Full stock terms for limited offers, keyed independently from mutable list indexes.
    /// </summary>
    public ImmutableDictionary<RoundAsrsOfferId, RoundAsrsStockPolicy> StockPolicies { get; internal set; } =
        ImmutableDictionary<RoundAsrsOfferId, RoundAsrsStockPolicy>.Empty;
}
