using Content.Client.Chat.TypingIndicator;
using Content.Shared.CMU14.Yautja;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Yautja;

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

        // people around a hunter standing still at the translator should see the same bubble as chat typing
        var typing = EntMan.System<TypingIndicatorSystem>();
        typing.ClientChangedChatFocus(true);
        _window.OnTyping += typing.ClientChangedChatText;
        _window.OnClose += () => typing.ClientChangedChatFocus(false);
        _window.OnSend += message =>
        {
            SendMessage(new YautjaTranslatorSendMessageMsg(message));
            typing.ClientSubmittedChatText();
        };

        if (State is YautjaTranslatorBuiState state)
            _window.UpdateState(state);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is YautjaTranslatorBuiState translatorState)
            _window?.UpdateState(translatorState);
    }
}
