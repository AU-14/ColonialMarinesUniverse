using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.AU14.ColonyEconomy;

/// <summary>
///     One item sold by a cash-operated vending machine.
/// </summary>
[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class AU14CashVendorEntry
{
    /// <summary>Entity prototype to spawn when purchased.</summary>
    [DataField("id", required: true)]
    public EntProtoId ItemId = default!;

    /// <summary>Display name override; if null, uses the entity prototype name.</summary>
    [DataField("name")]
    public string? Name;

    /// <summary>Base price in cash before sales tax.</summary>
    [DataField("price", required: true)]
    public int BasePrice = 10;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class AU14CashVendorComponent : Component
{
    /// <summary>Items this machine sells.</summary>
    [DataField("items")]
    public List<AU14CashVendorEntry> Items = new();

    /// <summary>Cash currently inserted by the user (not networked — managed server-side).</summary>
    public float InsertedCash = 0f;
}

