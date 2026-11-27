// Content.Shared.CMU14.ColonyEconomy.BudgetConsoleUi.cs
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.ColonyEconomy;

[ByRefEvent]
public readonly record struct CMUColonyAsrsBalanceChangedEvent;

[Serializable, NetSerializable]
public enum BudgetConsoleUi
{
    Key,
}

[Serializable, NetSerializable]
public sealed class BudgetConsoleWithdrawBuiMsg : BoundUserInterfaceMessage
{
    public float Amount { get; }

    public BudgetConsoleWithdrawBuiMsg(float amount)
    {
        Amount = amount;
    }
}

[Serializable, NetSerializable]
public sealed class BudgetConsoleBuiState : BoundUserInterfaceState
{
    public float Budget { get; }
    public int ColonyAsrsBudget { get; }
    public List<BudgetConsoleDepartmentInfo> Departments { get; }
    public float WithdrawalRemaining { get; }
    public float WithdrawalLimit { get; }
    public int WithdrawalResetSeconds { get; }
    public List<BudgetConsoleGarrisonInfo> Garrisons { get; }

    public BudgetConsoleBuiState(float budget, int colonyAsrsBudget, List<BudgetConsoleDepartmentInfo> departments,
        float withdrawalRemaining, float withdrawalLimit, int withdrawalResetSeconds,
        List<BudgetConsoleGarrisonInfo>? garrisons = null)
    {
        Garrisons = garrisons ?? new List<BudgetConsoleGarrisonInfo>();
        Budget = budget;
        ColonyAsrsBudget = colonyAsrsBudget;
        Departments = departments;
        WithdrawalRemaining = withdrawalRemaining;
        WithdrawalLimit = withdrawalLimit;
        WithdrawalResetSeconds = withdrawalResetSeconds;
    }
}

/// <summary>A garrison's requisitions account, which the colony can choose to pay.</summary>
[Serializable, NetSerializable]
public sealed class BudgetConsoleGarrisonInfo(string faction, int balance)
{
    public readonly string Faction = faction;
    public readonly int Balance = balance;
}

/// <summary>
///     The colony paying a garrison's requisitions account out of its budget. Garrisons can pay the colony from the
///     Liaison Office but can never take it back themselves, so this is only honoured for someone not in a garrison.
/// </summary>
[Serializable, NetSerializable]
public sealed class BudgetConsoleTransferToGarrisonBuiMsg(string faction, int amount) : BoundUserInterfaceMessage
{
    public readonly string Faction = faction;
    public readonly int Amount = amount;
}

[Serializable, NetSerializable]
public sealed class BudgetConsoleDepartmentInfo
{
    public NetEntity Uid { get; }
    public string Name { get; }
    public float Budget { get; }

    public BudgetConsoleDepartmentInfo(NetEntity uid, string name, float budget)
    {
        Uid = uid;
        Name = name;
        Budget = budget;
    }
}

[Serializable, NetSerializable]
public sealed class BudgetConsoleTransferToDeptBuiMsg : BoundUserInterfaceMessage
{
    public NetEntity DeptConsoleUid { get; }
    public float Amount { get; }

    public BudgetConsoleTransferToDeptBuiMsg(NetEntity deptConsoleUid, float amount)
    {
        DeptConsoleUid = deptConsoleUid;
        Amount = amount;
    }
}

[Serializable, NetSerializable]
public sealed class BudgetConsoleTransferAsrsToDeptBuiMsg : BoundUserInterfaceMessage
{
    public NetEntity DeptConsoleUid { get; }
    public int Amount { get; }

    public BudgetConsoleTransferAsrsToDeptBuiMsg(NetEntity deptConsoleUid, int amount)
    {
        DeptConsoleUid = deptConsoleUid;
        Amount = amount;
    }
}

[Serializable, NetSerializable]
public sealed class BudgetConsoleDispenseSalariesBuiMsg : BoundUserInterfaceMessage { }
