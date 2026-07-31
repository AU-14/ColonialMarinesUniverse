using Content.Shared._CMU14.Yautja;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._CMU14.Yautja;

[UsedImplicitly]
public sealed class YautjaTranslatorBui : BoundUserInterface
{
    private YautjaTranslatorWindow? _window;

    public YautjaTranslatorBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<YautjaTranslatorWindow>();
        _window.OnSend += message => SendMessage(new YautjaTranslatorSendMessageMsg(message));

        if (State is YautjaTranslatorBuiState state)
            _window.UpdateState(state);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is YautjaTranslatorBuiState translatorState)
            _window?.UpdateState(translatorState);
    }
}
