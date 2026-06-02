using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.AU14.ColonyEconomy;

[Serializable, NetSerializable]
public enum AU14CashVendorUi
{
    Key,
}

/// <summary>One item entry sent to the cash vendor client UI.</summary>
[Serializable, NetSerializable]
public sealed class AU14CashVendorItemState
{
    public int Index { get; }
    public string Name { get; }
    public int EffectivePrice { get; }
    public EntProtoId ItemProtoId { get; }

    public AU14CashVendorItemState(int index, string name, int effectivePrice, EntProtoId itemProtoId)
    {
        Index = index;
        Name = name;
        EffectivePrice = effectivePrice;
        ItemProtoId = itemProtoId;
    }
}

[Serializable, NetSerializable]
public sealed class AU14CashVendorBuiState : BoundUserInterfaceState
{
    public float InsertedCash { get; }
    public List<AU14CashVendorItemState> Items { get; }
    public float SalesTaxPercent { get; }
    public bool AllowDepartmentBudget { get; }
    public bool HasDepartmentMode { get; }
    public float DepartmentBudget { get; }
    public string DepartmentName { get; }

    public AU14CashVendorBuiState(float insertedCash, List<AU14CashVendorItemState> items, float salesTaxPercent = 0f,
        bool allowDepartmentBudget = false, bool hasDepartmentMode = false, float departmentBudget = 0f, string departmentName = "")
    {
        InsertedCash = insertedCash;
        Items = items;
        SalesTaxPercent = salesTaxPercent;
        AllowDepartmentBudget = allowDepartmentBudget;
        HasDepartmentMode = hasDepartmentMode;
        DepartmentBudget = departmentBudget;
        DepartmentName = departmentName;
    }
}

[Serializable, NetSerializable]
public sealed class AU14CashVendorBuyBuiMsg : BoundUserInterfaceMessage
{
    public int ItemIndex { get; }
    public AU14CashVendorBuyBuiMsg(int itemIndex) { ItemIndex = itemIndex; }
}

[Serializable, NetSerializable]
public sealed class AU14CashVendorReturnChangeBuiMsg : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public sealed class AU14CashVendorScanIDBuiMsg : BoundUserInterfaceMessage { }

