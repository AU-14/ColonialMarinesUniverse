using Content.Shared.CMU14.Requisitions.StockConsole;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Requisitions.StockConsole;

public sealed class CMURequisitionsStockConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private CMURequisitionsStockConsoleWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CMURequisitionsStockConsoleWindow>();
        _window.OnRefresh += () => SendMessage(new CMURequisitionsStockConsoleRefreshBuiMsg());
        _window.OnPrintSlip += lines => SendMessage(new CMURequisitionsStockConsolePrintSlipBuiMsg(lines));
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);
        if (message is CMURequisitionsStockConsoleSlipLimitBuiMsg limit)
            _window?.SetMaxSlipItems(limit.MaxSlipItems);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not CMURequisitionsStockConsoleBuiState stockState)
            return;

        _window.UpdateState(stockState);
    }
}
