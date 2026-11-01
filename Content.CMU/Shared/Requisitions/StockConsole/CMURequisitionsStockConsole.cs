using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Requisitions.StockConsole;

/// <summary>
/// Marks a faction's automated requisitions vendor so stock consoles on the same map can list what it has left.
/// </summary>
[RegisterComponent]
public sealed partial class CMURequisitionsVendorComponent : Component;

/// <summary>
/// A console that shows the stock of every automated requisitions vendor on its map, and prints order slips for them.
/// </summary>
[RegisterComponent]
public sealed partial class CMURequisitionsStockConsoleComponent : Component
{
    /// <summary>Most items one order slip can ask for.</summary>
    [DataField]
    public int MaxSlipItems = 8;

    /// <summary>Squad leaders and command staff can ask for more on one slip.</summary>
    [DataField]
    public int CommandMaxSlipItems = 15;

    /// <summary>The jobs that count as command staff for <see cref="CommandMaxSlipItems"/>.</summary>
    [DataField]
    public EntProtoId CommandJobGroup = "CMUJobsMilitaryCommand";

    [DataField]
    public EntProtoId SlipPrototype = "CMUPaperRequisitionSlip";

    [DataField]
    public TimeSpan PrintCooldown = TimeSpan.FromSeconds(3);

    [DataField]
    public SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/_RMC14/Machines/print.ogg");

    [ViewVariables]
    public TimeSpan NextPrint;

    /// <summary>How often the open UI refreshes on its own.</summary>
    [DataField]
    public TimeSpan RefreshEvery = TimeSpan.FromSeconds(2);

    [ViewVariables]
    public TimeSpan NextRefresh;
}

[Serializable, NetSerializable]
public enum CMURequisitionsStockConsoleUiKey
{
    Key,
}

[Serializable, NetSerializable]
public sealed class CMURequisitionsStockItem
{
    /// <summary>The vended entity's prototype, so the console can draw its sprite.</summary>
    public string Id = string.Empty;

    public string Name = string.Empty;

    /// <summary>How many are left, or null when the vendor has an unlimited supply.</summary>
    public int? Amount;

    /// <summary>How many the vendor started the round with, if it tracks that.</summary>
    public int? Max;

    /// <summary>Whether it can go on an order slip: free stock, not bought with points.</summary>
    public bool Orderable;
}

[Serializable, NetSerializable]
public sealed class CMURequisitionsStockSection
{
    public string Name = string.Empty;
    public List<CMURequisitionsStockItem> Items = new();
}

[Serializable, NetSerializable]
public sealed class CMURequisitionsStockVendor
{
    public string Name = string.Empty;
    public List<CMURequisitionsStockSection> Sections = new();
}

[Serializable, NetSerializable]
public sealed class CMURequisitionsStockConsoleBuiState(List<CMURequisitionsStockVendor> vendors) : BoundUserInterfaceState
{
    public readonly List<CMURequisitionsStockVendor> Vendors = vendors;
}

/// <summary>
/// Tells one viewer how many items their slips can hold; command staff get more than everyone else.
/// </summary>
[Serializable, NetSerializable]
public sealed class CMURequisitionsStockConsoleSlipLimitBuiMsg(int maxSlipItems) : BoundUserInterfaceMessage
{
    public readonly int MaxSlipItems = maxSlipItems;
}

[Serializable, NetSerializable]
public sealed class CMURequisitionsStockConsoleRefreshBuiMsg : BoundUserInterfaceMessage;

/// <summary>
/// One line of a requisition order: an item from a requisitions vendor and how many of it.
/// </summary>
[Serializable, NetSerializable]
public sealed class CMURequisitionsOrderLine
{
    public string Id = string.Empty;
    public int Count;
}

/// <summary>
/// Asks the stock console to print an order slip for these items.
/// </summary>
[Serializable, NetSerializable]
public sealed class CMURequisitionsStockConsolePrintSlipBuiMsg(List<CMURequisitionsOrderLine> lines) : BoundUserInterfaceMessage
{
    public readonly List<CMURequisitionsOrderLine> Lines = lines;
}

/// <summary>
/// A requisition order slip printed by a stock console. Once stamped approved, using it on a requisitions vendor
/// has the vendor hand out whatever it has in stock from the order.
/// </summary>
[RegisterComponent]
public sealed partial class CMURequisitionSlipComponent : Component
{
    /// <summary>What's still owed, by vended prototype.</summary>
    [DataField]
    public Dictionary<string, int> Remaining = new();

    /// <summary>The whole order as printed, by item name, for the filled slip.</summary>
    [DataField]
    public Dictionary<string, int> Ordered = new();

    [DataField]
    public string Requester = string.Empty;

    /// <summary>Who printed it, by player, so each person's orders only pay out once.</summary>
    [DataField]
    public string RequesterKey = string.Empty;

    /// <summary>
    /// Components added once the whole order is handed out, so the slip pays a little when sent down the ASRS lift.
    /// </summary>
    [DataField]
    public EntProtoId PayoutTemplate = "CMURequisitionSlipPayoutTemplate";

    /// <summary>The vendor stamping the slip complete.</summary>
    [DataField]
    public SoundSpecifier CompletedSound = new SoundPathSpecifier("/Audio/Machines/short_print_and_rip.ogg");

    [DataField]
    public string RequiredStamp = "paper_stamp-approve";
}

/// <summary>
/// A requisitions table that order slips are filled onto: the vendor puts the order on whichever of these is closest to
/// the person handing in the slip.
/// </summary>
[RegisterComponent]
public sealed partial class CMURequisitionsDispenseTableComponent : Component;
