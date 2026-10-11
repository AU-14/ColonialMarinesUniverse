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
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not CMURequisitionsStockConsoleBuiState stockState)
            return;

        _window.UpdateState(stockState);
    }
}
