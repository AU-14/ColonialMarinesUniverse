using Content.Shared._CMU14.Round.Objectives;
using Robust.Client.UserInterface;

namespace Content.Client._CMU14.Round.Objectives;

public sealed class ObjectivesConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private ObjectivesConsoleWindow? _window;
    private ObjectiveIntelWindow? _intelWindow;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ObjectivesConsoleWindow>();
        _window.RequestIntelCallback = RequestIntel;

        if (State is ObjectivesConsoleBoundUserInterfaceState cast)
            _window.UpdateObjectives(cast.Objectives, cast.CurrentWinPoints, cast.RequiredWinPoints);
    }

    public void RequestIntel(string objectiveId)
    {
        SendMessage(new ObjectivesConsoleRequestIntelMessage(objectiveId));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is ObjectivesConsoleBoundUserInterfaceState cast)
        {
            if (_window == null || _window.Disposed)
            {
                _window = this.CreateWindow<ObjectivesConsoleWindow>();
                _window.RequestIntelCallback = RequestIntel;
            }

            _window.UpdateObjectives(cast.Objectives, cast.CurrentWinPoints, cast.RequiredWinPoints);
        }
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);
        if (message is ObjectiveIntelBoundUserInterfaceMessage intelState)
            ShowIntelWindow(intelState);
    }

    private void ShowIntelWindow(ObjectiveIntelBoundUserInterfaceMessage intelState)
    {
        if (_intelWindow == null || _intelWindow.Disposed)
        {
            _intelWindow = new ObjectiveIntelWindow();
            _intelWindow.OnClose += OnIntelWindowClosed;
            _intelWindow.OpenCentered();
        }
        else if (!_intelWindow.IsOpen)
        {
            _intelWindow.OpenCentered();
        }

        _intelWindow.Populate(
            intelState.ObjectiveId,
            intelState.ObjectiveDefaultTitle,
            intelState.Tiers,
            intelState.UnlockedTier,
            intelState.FactionPoints,
            idx => SendMessage(new ObjectivesConsoleUnlockIntelMessage(intelState.ObjectiveId, idx)));
    }

    private void OnIntelWindowClosed()
    {
        if (_intelWindow != null)
            _intelWindow.OnClose -= OnIntelWindowClosed;

        _intelWindow = null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _intelWindow != null)
        {
            _intelWindow.OnClose -= OnIntelWindowClosed;
            _intelWindow.Close();
            _intelWindow.Orphan();
            _intelWindow = null;
        }

        _window = null;
        base.Dispose(disposing);
    }
}
