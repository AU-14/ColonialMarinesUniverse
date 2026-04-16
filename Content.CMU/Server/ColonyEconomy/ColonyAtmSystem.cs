using Content.Server.Stack;
using Content.Shared.Access.Components;
using Content.Shared.AU14.ColonyEconomy;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Server.GameObjects;

namespace Content.Server.AU14.ColonyEconomy;

public sealed class ColonyAtmSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly AdminConsoleSystem _adminConsole = default!;
    [Dependency] private readonly ColonyBudgetSystem _colonyBudget = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ColonyAtmComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<ColonyAtmComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<ColonyAtmComponent, BoundUIClosedEvent>(OnUiClosed);
        SubscribeLocalEvent<ColonyAtmComponent, ColonyAtmWithdrawBuiMsg>(OnWithdraw);
    }

    /// <summary>
    ///     When a player uses an ID card on the ATM, store it and open the UI.
    /// </summary>
    private void OnInteractUsing(EntityUid uid, ColonyAtmComponent comp, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<IdCardComponent>(args.Used, out _))
            return;

        args.Handled = true;

        comp.SwipedCard = args.Used;
        _ui.TryOpenUi(uid, ColonyAtmUi.Key, args.User);
    }

    private void OnUiOpened(EntityUid uid, ColonyAtmComponent comp, BoundUIOpenedEvent args)
    {
        var taxPct = _adminConsole.GetIncomeTax() * 100f;
        if (comp.SwipedCard == null || !TryComp<IdCardComponent>(comp.SwipedCard.Value, out var idCard))
        {
            var state = new ColonyAtmBuiState(0, "No ID card swiped", taxPct);
            _ui.SetUiState(uid, ColonyAtmUi.Key, state);
            return;
        }

        var uiState = new ColonyAtmBuiState(idCard.AccountBalance, idCard.FullName ?? "Unknown", taxPct);
        _ui.SetUiState(uid, ColonyAtmUi.Key, uiState);
    }

    /// <summary>
    ///     Clear the swiped card when the UI is closed.
    /// </summary>
    private void OnUiClosed(EntityUid uid, ColonyAtmComponent comp, BoundUIClosedEvent args)
    {
        comp.SwipedCard = null;
    }

    private void OnWithdraw(EntityUid uid, ColonyAtmComponent comp, ColonyAtmWithdrawBuiMsg msg)
    {
        if (comp.SwipedCard == null || !TryComp<IdCardComponent>(comp.SwipedCard.Value, out var idCard))
            return;

        var balance = idCard.AccountBalance;

        if (msg.Amount <= 0 || msg.Amount > balance)
            return;

        idCard.AccountBalance -= msg.Amount;
        Dirty(comp.SwipedCard.Value, idCard);

        // Apply income tax — tax goes to colony budget, remainder dispensed as cash
        var incomeTaxRate = _adminConsole.GetIncomeTax();
        var taxAmount = (int) Math.Floor(msg.Amount * incomeTaxRate);
        var netAmount = msg.Amount - taxAmount;

        if (netAmount > 0)
            _stack.SpawnMultiple("RMCSpaceCash", netAmount, uid);
        if (taxAmount > 0)
            _colonyBudget.AddToBudget(taxAmount);

        var taxPct = incomeTaxRate * 100f;
        var state = new ColonyAtmBuiState(idCard.AccountBalance, idCard.FullName ?? "Unknown", taxPct);
        _ui.SetUiState(uid, ColonyAtmUi.Key, state);
    }
}


