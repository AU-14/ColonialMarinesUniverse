using Content.Shared._RMC14.Requisitions.Components;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Requisitions;

[Serializable, NetSerializable]
public enum RequisitionsUIKey
{
    Key
}

[Serializable, NetSerializable]
public sealed class RequisitionsBuiState : BoundUserInterfaceState
{
    public RequisitionsElevatorMode? PlatformLowered;
    public bool Busy;
    public int Balance;
    public bool Full;
    public int AvailableSlots;
    public List<RequisitionsStockInfo> Stock;
    public List<RequisitionsItemStockInfo> ItemStock;

    public RequisitionsBuiState(
        RequisitionsElevatorMode? platformLowered,
        bool busy,
        int balance,
        bool full,
        int availableSlots,
        List<RequisitionsStockInfo> stock,
        List<RequisitionsItemStockInfo> itemStock)
    {
        PlatformLowered = platformLowered;
        Busy = busy;
        Balance = balance;
        Full = full;
        AvailableSlots = availableSlots;
        Stock = stock;
        ItemStock = itemStock;
    }
}

[Serializable, NetSerializable]
public sealed class RequisitionsStockInfo
{
    public int Category;
    public int Order;
    public int Current;
    public int Max;
    public int SecondsUntilNextReplenish;

    public RequisitionsStockInfo(
        int category,
        int order,
        int current,
        int max,
        int secondsUntilNextReplenish)
    {
        Category = category;
        Order = order;
        Current = current;
        Max = max;
        SecondsUntilNextReplenish = secondsUntilNextReplenish;
    }
}

[Serializable, NetSerializable]
public sealed class RequisitionsBuyMsg(int category, int order) : BoundUserInterfaceMessage
{
    public int Category = category;
    public int Order = order;
}

[Serializable, NetSerializable]
public sealed class RequisitionsPlatformMsg(bool raise) : BoundUserInterfaceMessage
{
    public bool Raise = raise;
}
