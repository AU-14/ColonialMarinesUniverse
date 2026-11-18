using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.CMU14.ColonyEconomy;

namespace Content.Shared._RMC14.Requisitions;

public abstract partial class SharedRequisitionsSystem
{
    protected void CMUNotifyBalanceChanged(RequisitionsAccountComponent account)
    {
        if (account.Faction != "colony")
            return;

        var ev = new CMUColonyAsrsBalanceChangedEvent();
        RaiseLocalEvent(ref ev);
    }
}
