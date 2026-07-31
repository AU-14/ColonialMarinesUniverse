// Content.Shared.AU14.ColonyEconomy.BudgetConsoleUi.cs
using Robust.Shared.Serialization;

namespace Content.Shared.AU14.ColonyEconomy;

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
    public List<BudgetConsoleDepartmentInfo> Departments { get; }

    public BudgetConsoleBuiState(float budget, List<BudgetConsoleDepartmentInfo> departments)
    {
        Budget = budget;
        Departments = departments;
    }
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
public sealed class BudgetConsoleDispenseSalariesBuiMsg : BoundUserInterfaceMessage { }
