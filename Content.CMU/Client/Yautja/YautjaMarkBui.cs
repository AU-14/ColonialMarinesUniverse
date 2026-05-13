using Content.Shared._CMU14.Yautja;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._CMU14.Yautja;

[UsedImplicitly]
public sealed class YautjaMarkBui : BoundUserInterface
{
    private YautjaMarkWindow? _window;

    public YautjaMarkBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<YautjaMarkWindow>();
        _window.OnRefresh += () => SendMessage(new YautjaMarkPanelRefreshMsg());
        _window.OnMark += (target, kind, reason) => SendMessage(new YautjaMarkPanelMarkMsg(target, kind, reason));
        _window.OnUnmark += (target, kind) => SendMessage(new YautjaMarkPanelUnmarkMsg(target, kind));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is YautjaMarkPanelState markState)
            _window?.UpdateState(markState);
    }
}
