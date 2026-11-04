using Content.Client.Eui;
using Content.Shared.CMU14.CharacterDescription;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client.CMU14.CharacterDescription.UI;

[UsedImplicitly]
public sealed class EditCharacterDescriptionEui : BaseEui
{
    private readonly EditCharacterDescriptionWindow _window;

    public EditCharacterDescriptionEui()
    {
        _window = new EditCharacterDescriptionWindow();
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
        _window.OnSave += (shortExamine, fullDescription) =>
            SendMessage(new EditCharacterDescriptionSaveMessage(shortExamine, fullDescription));
    }

    public override void Opened()
    {
        _window.OpenCentered();
    }

    public override void Closed()
    {
        base.Closed();
        _window.Close();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is EditCharacterDescriptionEuiState editState)
            _window.UpdateState(editState);
    }
}
