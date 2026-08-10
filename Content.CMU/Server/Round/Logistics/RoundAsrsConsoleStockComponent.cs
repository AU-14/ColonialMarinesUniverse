using Content.Server._RMC14.Requisitions;
using Content.Shared.CMU.Round;

namespace Content.Server.CMU.Round;

/// <summary>
/// Holds mutable per-console stock counts keyed by stable committed offer identity.
/// </summary>
[RegisterComponent]
[Access(typeof(RequisitionsSystem))]
public sealed partial class RoundAsrsConsoleStockComponent : Component
{
    internal Dictionary<RoundAsrsOfferId, RoundAsrsOfferStockState> Offers { get; } = [];
}

internal readonly record struct RoundAsrsOfferStockState(int Current, TimeSpan NextReplenish);
