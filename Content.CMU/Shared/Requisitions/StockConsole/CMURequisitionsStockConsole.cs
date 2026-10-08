using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Requisitions.StockConsole;

/// <summary>
/// Marks a faction's automated requisitions vendor so stock consoles on the same map can list what it has left.
/// </summary>
[RegisterComponent]
public sealed partial class CMURequisitionsVendorComponent : Component;

/// <summary>
/// A console that shows the stock of every automated requisitions vendor on its map.
/// </summary>
[RegisterComponent]
public sealed partial class CMURequisitionsStockConsoleComponent : Component
{
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

[Serializable, NetSerializable]
public sealed class CMURequisitionsStockConsoleRefreshBuiMsg : BoundUserInterfaceMessage;
