using Content.Client.Eui;
using Content.Shared._AU14.Insurgency.Editor;
using Content.Shared.Eui;

namespace Content.Client._AU14.Insurgency.Editor;

/// <summary>
///     Client side of the Default-faction editor. Shares its type name with the server EUI so the
///     EUI manager pairs them. Turns window actions into bound messages and pushes server state into
///     the window.
/// </summary>
public sealed class InsurgencyFactionEditorEui : BaseEui
{
    private readonly InsurgencyFactionEditorWindow _window;

    public InsurgencyFactionEditorEui()
    {
        _window = new InsurgencyFactionEditorWindow(
            onSave: (id, isDefault, def) => SendMessage(new InsurgencyFactionSaveMessage(id, isDefault, def)),
            onDelete: id => SendMessage(new InsurgencyFactionDeleteMessage(id)),
            onSelect: id => SendMessage(new InsurgencyFactionSelectMessage(id)));

        _window.OnClose += () => SendMessage(new CloseEuiMessage());
    }

    public override void Opened()
    {
        _window.OpenCentered();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is InsurgencyFactionEditorEuiState s)
            _window.SetState(s);
    }
}
