using Content.Client.Eui;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Eui;

namespace Content.Client._CMU14.Yautja;

public sealed class YautjaPredatorAdminEditorEui : BaseEui
{
    private YautjaPredatorAdminEditorWindow? _window;

    public override void Opened()
    {
        base.Opened();

        _window = new YautjaPredatorAdminEditorWindow();
        _window.OnClose += OnWindowClosed;
        _window.OnInitialize += OnInitialize;
        _window.OnHunterSlotsChanged += OnHunterSlotsChanged;
        _window.OnRandomChanged += OnRandomChanged;
        _window.OnRefresh += OnRefresh;
        _window.OpenCentered();
    }

    public override void Closed()
    {
        base.Closed();

        if (_window == null)
            return;

        _window.OnClose -= OnWindowClosed;
        _window.OnInitialize -= OnInitialize;
        _window.OnHunterSlotsChanged -= OnHunterSlotsChanged;
        _window.OnRandomChanged -= OnRandomChanged;
        _window.OnRefresh -= OnRefresh;
        _window.Close();
        _window = null;
    }

    public override void HandleState(EuiStateBase state)
    {
        base.HandleState(state);

        if (state is YautjaPredatorAdminEditorEuiState editorState)
            _window?.UpdateState(editorState);
    }

    private void OnInitialize()
    {
        SendMessage(new YautjaPredatorAdminEditorInitializeMessage());
    }

    private void OnHunterSlotsChanged(int slots)
    {
        SendMessage(new YautjaPredatorAdminEditorSetHunterSlotsMessage(slots));
    }

    private void OnRandomChanged(bool enabled, int minimumRounds, int maximumRounds)
    {
        SendMessage(new YautjaPredatorAdminEditorSetRandomMessage(enabled, minimumRounds, maximumRounds));
    }

    private void OnRefresh()
    {
        SendMessage(new YautjaPredatorAdminEditorRefreshMessage());
    }

    private void OnWindowClosed()
    {
        SendMessage(new CloseEuiMessage());
    }
}
