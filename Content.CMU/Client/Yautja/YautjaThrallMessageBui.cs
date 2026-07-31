using Content.Shared._CMU14.Yautja;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._CMU14.Yautja;

[UsedImplicitly]
public sealed class YautjaThrallMessageBui : BoundUserInterface
{
    private YautjaThrallMessageWindow? _window;

    public YautjaThrallMessageBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<YautjaThrallMessageWindow>();
        _window.OnSend += message => SendMessage(new YautjaThrallSendMessageMsg(message));
    }
}
