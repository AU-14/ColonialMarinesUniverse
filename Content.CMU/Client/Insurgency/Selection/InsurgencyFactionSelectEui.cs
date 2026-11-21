using Content.Client.Eui;
using Content.Shared.CMU14.Insurgency.Selection;
using Content.Shared.Eui;

namespace Content.Client.CMU14.Insurgency.Selection;

/// <summary>
///     Sends the player's faction vote and exclusions to the server.
/// </summary>
public sealed class InsurgencyFactionSelectEui : BaseEui
{
    private readonly InsurgencyFactionSelectWindow _window;

    public InsurgencyFactionSelectEui()
    {
        _window = new InsurgencyFactionSelectWindow();
        _window.OnExclusionsChanged += ids => SendMessage(new CMUInsurgencyExclusionsMessage(ids));
        _window.OnSelectDefault += id => SendMessage(new InsurgencyFactionSelectDefaultMessage(id));
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
    }

    public override void Opened()
    {
        _window.OpenCentered();
        SendMessage(new CMUInsurgencyExclusionsMessage(_window.Exclusions));
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is InsurgencyFactionSelectEuiState s)
            _window.SetState(s);
    }

    public override void Closed()
    {
        base.Closed();
        _window.Close();
    }
}
