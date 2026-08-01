using Content.Client.Eui;
using Content.Shared._AU14.Insurgency.Selection;
using Content.Shared.Eui;

namespace Content.Client._AU14.Insurgency.Selection;

/// <summary>
///     Client side of the faction reveal popup. Shares its type name with the server EUI. Rebuilds the
///     window from state so the sprites resolve once the data has arrived.
/// </summary>
public sealed class InsurgencyFactionRevealEui : BaseEui
{
    private InsurgencyFactionRevealWindow? _window;

    public override void HandleState(EuiStateBase state)
    {
        if (state is not InsurgencyFactionRevealEuiState s)
            return;

        _window?.Close();
        _window = new InsurgencyFactionRevealWindow(s);
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
        _window.OpenCentered();
    }

    public override void Closed()
    {
        _window?.Close();
    }
}
