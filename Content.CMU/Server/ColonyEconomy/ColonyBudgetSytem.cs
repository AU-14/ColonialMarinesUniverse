// Content.Server/AU14/ColonyEconomy/ColonyBudgetSystem.cs
using System.Collections.Generic;
using System.Linq;

namespace Content.Server.AU14.ColonyEconomy;

public sealed class ColonyBudgetSystem : EntitySystem
{
    private float _budget = 0f;
    private List<string> WithdrawHistory = new List<string>();


    public void AddToBudget(float amount, EntityUid? by = null)
    {
        _budget += amount;
        // Optionally: Raise event, sync to clients, etc.
    }

    public float GetBudget() => _budget;

    public void AddWithdraw(string withdrawer)
    {
        WithdrawHistory.Add(withdrawer);
    }

    public string givewithdrawers() {

        return string.Join(",",WithdrawHistory);
    }
}
