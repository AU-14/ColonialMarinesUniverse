using Content.Client.Eui;
using Content.Shared._AU14.Insurgency.Selection;
using Content.Shared.Eui;

namespace Content.Client._AU14.Insurgency.Selection;

/// <summary>
///     Client side of the CLF-leader faction selection popup. Shares its type name with the server EUI
///     so the EUI manager pairs them. Turns the window's picks into bound messages; the server applies
///     and validates.
/// </summary>
public sealed class InsurgencyFactionSelectEui : BaseEui
{
    private readonly InsurgencyFactionSelectWindow _window;

    public InsurgencyFactionSelectEui()
    {
        _window = new InsurgencyFactionSelectWindow();
        _window.OnSelectDefault += id => SendMessage(new InsurgencyFactionSelectDefaultMessage(id));
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
    }

    public override void Opened()
    {
        _window.OpenCentered();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is InsurgencyFactionSelectEuiState s)
            _window.SetState(s);
    }

    // The server closes this EUI the moment a faction is picked (or is re-validated and applied). Without
    // this the window would linger open on the client, hiding the reveal popup that opens right behind it.
    public override void Closed()
    {
        base.Closed();
        _window.Close();
    }
}
