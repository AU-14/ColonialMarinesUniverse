using Content.Shared.CMU14.Yautja;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Yautja;

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
        _window.OnMark += (record, revision, kind, reason) =>
            SendMessage(new YautjaMarkPanelMarkMsg(record, revision, kind, reason));
        _window.OnUnmark += (record, revision, kind) =>
            SendMessage(new YautjaMarkPanelUnmarkMsg(record, revision, kind));
        _window.OnChange += (record, revision, oldKind, newKind, reason) =>
            SendMessage(new YautjaMarkPanelChangeMsg(record, revision, oldKind, newKind, reason));
        _window.OnRefresh += (history, page) => SendMessage(new YautjaMarkPanelRefreshMsg(history, page));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is YautjaMarkPanelState markState)
            _window?.UpdateState(markState);
    }
}
