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
   public string  lastWithdrawals { get; }

    public BudgetConsoleBuiState(float budget, string LastWithdrawals)
    {
        Budget = budget;
        lastWithdrawals = LastWithdrawals;
    }
}
