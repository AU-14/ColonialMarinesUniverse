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
    public List<RequisitionsStockInfo> Stock;

    public RequisitionsBuiState(
        RequisitionsElevatorMode? platformLowered,
        bool busy,
        int balance,
        bool full,
        List<RequisitionsStockInfo> stock)
    {
        PlatformLowered = platformLowered;
        Busy = busy;
        Balance = balance;
        Full = full;
        Stock = stock;
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
