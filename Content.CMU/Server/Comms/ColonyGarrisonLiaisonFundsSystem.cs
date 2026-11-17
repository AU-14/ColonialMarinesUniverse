using Content.Server.CMU14.ColonyEconomy;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.CMU14;
using Content.Shared.CMU14.ForceOnForce;
using Content.Shared.Popups;
using Robust.Server.GameObjects;

namespace Content.Server.CMU14.Comms;

/// <summary>
///     The Colony Garrison Liaison Office's money: the liaison can pay the colony's budget out of their faction's
///     requisitions account, and pay the garrison back out of the colony's budget.
/// </summary>
public sealed class ColonyGarrisonLiaisonFundsSystem : EntitySystem
{
    [Dependency] private BudgetConsoleSystem _budgetConsole = default!;
    [Dependency] private ColonyBudgetSystem _colonyBudget = default!;
    [Dependency] private ForceOnForceSystem _factions = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedRequisitionsSystem _requisitions = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ColonyCommsConsoleComponent, ColonyGarrisonLiaisonTransferBuiMsg>(OnTransfer);
    }


    private void OnTransfer(Entity<ColonyCommsConsoleComponent> ent, ref ColonyGarrisonLiaisonTransferBuiMsg args)
    {
        var user = args.Actor;
        var amount = args.Amount;
        if (amount <= 0)
            return;

        if (GetGarrisonAccount(user) is not { } account)
        {
            _popup.PopupEntity(Loc.GetString("colony-garrison-liaison-no-garrison"), ent, user);
            return;
        }

        if (args.ToColony)
        {
            if (account.Comp.Balance < amount)
            {
                _popup.PopupEntity(Loc.GetString("colony-garrison-liaison-not-enough-garrison"), ent, user);
                return;
            }

            _requisitions.ChangeBudget(-amount, account.Comp.Faction);
            _colonyBudget.AddToBudget(amount);
        }
        else
        {
            if (_colonyBudget.GetBudget() < amount)
            {
                _popup.PopupEntity(Loc.GetString("colony-garrison-liaison-not-enough-colony"), ent, user);
                return;
            }

            _colonyBudget.AddToBudget(-amount);
            _requisitions.ChangeBudget(amount, account.Comp.Faction);
        }

        _budgetConsole.UpdateAllUi();
        RefreshAll();
    }

    private Entity<RequisitionsAccountComponent>? GetGarrisonAccount(EntityUid user)
    {
        if (_factions.GetFaction(user) is not { } faction)
            return null;

        var query = EntityQueryEnumerator<RequisitionsAccountComponent>();
        while (query.MoveNext(out var uid, out var account))
        {
            if (string.Equals(account.Faction, faction, StringComparison.OrdinalIgnoreCase))
                return (uid, account);
        }

        return null;
    }

    public void SendFunds(EntityUid console, EntityUid user)
    {
        var account = GetGarrisonAccount(user);
        _ui.ServerSendUiMessage(console, ColonyGarrisonLiaisonUi.Key,
            new ColonyGarrisonLiaisonFundsBuiMsg(account != null, account?.Comp.Balance ?? 0, (int) _colonyBudget.GetBudget()),
            user);
    }

    /// <summary>Everyone using an office sees the new balances, each their own garrison's.</summary>
    private void RefreshAll()
    {
        var query = EntityQueryEnumerator<ColonyCommsConsoleComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            foreach (var actor in _ui.GetActors(uid, ColonyGarrisonLiaisonUi.Key))
            {
                SendFunds(uid, actor);
            }
        }
    }
}
