using Content.Shared._AU14.Radio;
using Robust.Client.UserInterface;

namespace Content.Client._AU14.Radio;

public sealed class AU14NetSpliceBui : BoundUserInterface
{
    [ViewVariables]
    private AU14NetSpliceWindow? _window;

    public AU14NetSpliceBui(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<AU14NetSpliceWindow>();

        _window.OnProbe += position => SendMessage(new AU14NetSpliceProbeMsg(position));
        _window.OnLock += position => SendMessage(new AU14NetSpliceLockMsg(position));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not AU14NetSpliceBuiState spliceState)
            return;

        _window?.UpdateState(spliceState);
    }
}
