using Content.Shared.AU14.Objectives;
using Robust.Client.UserInterface;

namespace Content.Client.AU14.Objectives;

public sealed class ObjectivesConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private ObjectivesConsoleWindow? _window;
    private ObjectiveIntelWindow? _intelWindow;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ObjectivesConsoleWindow>();
        _window.RequestIntelCallback = id => RequestIntel(id);

        // If we already have a state for this BUI (server sent it before Open was called), apply it now
        if (State is ObjectivesConsoleBoundUserInterfaceState cast)
            _window.UpdateObjectives(cast.Objectives, cast.CurrentWinPoints, cast.RequiredWinPoints);

        // If the server already sent an intel state (possible before open), open/populate the intel window
        if (State is ObjectiveIntelBoundUserInterfaceState intelState)
            ShowIntelWindow(intelState);
    }

    public void RequestIntel(string objectiveId)
    {
        // Send request to server and wait for it to respond with the intel state.
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
                _window.RequestIntelCallback = id => RequestIntel(id);
            }

            _window.UpdateObjectives(cast.Objectives, cast.CurrentWinPoints, cast.RequiredWinPoints);
            return;
        }

        if (state is ObjectiveIntelBoundUserInterfaceState intelState)
            ShowIntelWindow(intelState);
    }

    private void ShowIntelWindow(ObjectiveIntelBoundUserInterfaceState intelState)
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
            intelState.Tiers ?? new List<ObjectiveIntelTierEntry>(),
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
