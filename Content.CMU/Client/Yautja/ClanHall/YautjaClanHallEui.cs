using Content.Client.Eui;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Eui;

namespace Content.Client.CMU14.Yautja.ClanHall;

public sealed class YautjaClanHallEui : BaseEui
{
    private YautjaClanHallWindow? _window;

    public override void Opened()
    {
        base.Opened();
        _window = new YautjaClanHallWindow();
        _window.Send += SendMessage;
        _window.OnClose += OnWindowClosed;
        _window.OpenCentered();
    }

    public override void Closed()
    {
        base.Closed();
        if (_window == null)
            return;

        _window.Send -= SendMessage;
        _window.OnClose -= OnWindowClosed;
        _window.Close();
        _window = null;
    }

    public override void HandleState(EuiStateBase state)
    {
        base.HandleState(state);
        if (state is YautjaClanHallEuiState hall)
            _window?.UpdateState(hall);
    }

    private void OnWindowClosed()
    {
        SendMessage(new CloseEuiMessage());
    }
}
