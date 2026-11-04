using Content.Client.Chat.TypingIndicator;
using Content.Shared.CMU14.Yautja;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Yautja;

[UsedImplicitly]
public sealed class YautjaTranslatorBui : BoundUserInterface
{
    private YautjaTranslatorWindow? _window;
    private bool _typingShown;

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
        _typingShown = true;
        _window.OnTyping += typing.ClientChangedChatText;
        _window.OnClose += ClearTyping;
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

    protected override void Dispose(bool disposing)
    {
        // OnClose only fires for window.Close(). a server-side close (sending, bracer off, out of range)
        // just disposes the window, and the bubble stayed up forever
        if (disposing)
            ClearTyping();

        base.Dispose(disposing);
    }

    private void ClearTyping()
    {
        if (!_typingShown)
            return;

        _typingShown = false;
        var typing = EntMan.System<TypingIndicatorSystem>();
        typing.ClientSubmittedChatText();
        typing.ClientChangedChatFocus(false);
    }
}
