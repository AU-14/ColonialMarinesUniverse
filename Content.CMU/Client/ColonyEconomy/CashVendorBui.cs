using Content.Shared.AU14.ColonyEconomy;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.AU14.ColonyEconomy;

public sealed class AU14CashVendorBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private AU14CashVendorWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<AU14CashVendorWindow>();
        _window.OnBuyPressed += idx => SendPredictedMessage(new AU14CashVendorBuyBuiMsg(idx));
        _window.ReturnChangeBtn.OnPressed += _ => SendPredictedMessage(new AU14CashVendorReturnChangeBuiMsg());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not AU14CashVendorBuiState s)
            return;

        _window.UpdateState(s);
    }
}

