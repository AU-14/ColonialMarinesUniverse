using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.CorporateWork;

[Serializable, NetSerializable]
public enum CMUNegotiationTerminalUi : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum CMUNegotiationReply : byte
{
    Counter,
    Insulted,
    Accepted,
    WalkedAway,
}

/// <summary>
///     One round: what was offered and how the supplier answered.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct CMUNegotiationExchange(int Offer, CMUNegotiationReply Reply, int Ask);

[Serializable, NetSerializable]
public readonly record struct CMUNegotiationTierInfo(string Name, int Patience, int Reward);

/// <param name="Price">What the deal closed at, or null if the supplier walked away.</param>
[Serializable, NetSerializable]
public readonly record struct CMUNegotiationResult(int? Price, int ListPrice, int Payout);

[Serializable, NetSerializable]
public sealed class CMUNegotiationTerminalBuiState(
    List<CMUNegotiationTierInfo> tiers,
    int? activeTier,
    string supplier,
    string goods,
    int listPrice,
    int ask,
    int patience,
    List<CMUNegotiationExchange> log,
    TimeSpan nextCaseAt,
    CMUNegotiationResult? lastResult) : BoundUserInterfaceState
{
    public readonly List<CMUNegotiationTierInfo> Tiers = tiers;
    public readonly int? ActiveTier = activeTier;
    public readonly string Supplier = supplier;
    public readonly string Goods = goods;
    public readonly int ListPrice = listPrice;
    public readonly int Ask = ask;
    public readonly int Patience = patience;
    public readonly List<CMUNegotiationExchange> Log = log;
    public readonly TimeSpan NextCaseAt = nextCaseAt;
    public readonly CMUNegotiationResult? LastResult = lastResult;
}

[Serializable, NetSerializable]
public sealed class CMUNegotiationStartBuiMsg(int tier) : BoundUserInterfaceMessage
{
    public readonly int Tier = tier;
}

[Serializable, NetSerializable]
public sealed class CMUNegotiationOfferBuiMsg(int offer) : BoundUserInterfaceMessage
{
    public readonly int Offer = offer;
}

/// <summary>
///     Takes the supplier's current ask.
/// </summary>
[Serializable, NetSerializable]
public sealed class CMUNegotiationAcceptBuiMsg : BoundUserInterfaceMessage;
