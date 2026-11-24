namespace Content.Shared.CMU14.CorporateWork;

/// <summary>
///     Supplier Negotiation terminal: haggle a supplier down from their list price. They have a
///     walk-away price they won't go below and only so much patience. A fair offer gets a counter
///     that moves toward the walk-away price; a lowball offer insults them and costs extra patience.
///     Meet their current ask to close. Pays by how much of the possible saving was won.
/// </summary>
[RegisterComponent]
public sealed partial class CMUNegotiationTerminalComponent : Component
{
    [DataField(required: true)]
    public List<CMUNegotiationTier> Tiers = new();

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(60);

    /// <summary>
    ///     Offers below this fraction of the walk-away price insult the supplier.
    /// </summary>
    [DataField]
    public float InsultFraction = 0.85f;

    [ViewVariables]
    public CMUNegotiationCase? Case;

    [ViewVariables]
    public TimeSpan NextCaseAt;

    [ViewVariables]
    public CMUNegotiationResult? LastResult;
}

[DataDefinition]
public sealed partial class CMUNegotiationTier
{
    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    ///     Offers the supplier will hear before walking away.
    /// </summary>
    [DataField(required: true)]
    public int Patience;

    /// <summary>
    ///     The lowest and highest walk-away price, as fractions of the list price.
    /// </summary>
    [DataField(required: true)]
    public float MinFloor;

    [DataField(required: true)]
    public float MaxFloor;

    /// <summary>
    ///     How far the supplier moves toward a fair offer each round, as a fraction of the gap.
    /// </summary>
    [DataField(required: true)]
    public float Concession;

    /// <summary>
    ///     Payout for closing at the walk-away price, before repeat reductions.
    /// </summary>
    [DataField(required: true)]
    public int Reward;
}

public sealed class CMUNegotiationCase
{
    public required int Tier;
    public required string Supplier;
    public required string Goods;
    public required int ListPrice;

    /// <summary>
    ///     The lowest price the supplier will take. Never sent to clients.
    /// </summary>
    public required int Floor;

    public required int Ask;
    public required int Patience;
    public readonly List<CMUNegotiationExchange> Log = new();
}
