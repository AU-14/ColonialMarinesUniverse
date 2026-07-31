using Content.Shared._CMU14.Yautja;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._CMU14.Yautja;

[UsedImplicitly]
public sealed class YautjaBracerBui : BoundUserInterface
{
    private YautjaBracerWindow? _window;

    public YautjaBracerBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<YautjaBracerWindow>();
        _window.OnCommand += command => SendMessage(new YautjaBracerPanelCommandMsg(command));
        SendMessage(new YautjaBracerPanelRefreshMsg());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is YautjaBracerPanelState bracerState)
            _window?.UpdateState(bracerState);
    }
}
