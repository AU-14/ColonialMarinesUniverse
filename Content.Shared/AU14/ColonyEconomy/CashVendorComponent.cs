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

    /// <summary>When true, the UI shows a Scan ID button allowing purchases via department budget.</summary>
    [DataField("allowDepartmentBudget")]
    public bool AllowDepartmentBudget = false;

    /// <summary>The department console linked by a successful ID scan (server-side only).</summary>
    public EntityUid? ScannedDepartmentConsole = null;

    /// <summary>
    ///     Fraction of the item base price returned to the colony budget on a cash purchase (0–1).
    ///     Department budget purchases are never counted — only physical cash.
    ///     Set to 0 for dept vendors so the money is fully consumed; set to e.g. 0.6 for
    ///     public vendors so 60 % of each sale circulates back into the colony economy.
    /// </summary>
    [DataField]
    public float PercentToColony = 0f;
}

