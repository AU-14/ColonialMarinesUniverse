using Content.Shared.AU14.Objectives;
using Robust.Client.UserInterface;
using Robust.Shared.Log;

namespace Content.Client.AU14.Objectives;

public sealed class ObjectivesConsoleBoundUserInterface : BoundUserInterface
{
    private ObjectivesConsoleWindow? _window;
    private ObjectiveIntelWindow? _intelWindow;

    public ObjectivesConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ObjectivesConsoleWindow>();
        _window.OnClose += Close;
        _window.RequestIntelCallback = id => RequestIntel(id);
        _window.OpenCentered();

        // If we already have a state for this BUI (server sent it before Open was called), apply it now
        if (State is ObjectivesConsoleBoundUserInterfaceState cast)
        {
            _window.UpdateObjectives(cast.Objectives, cast.CurrentWinPoints, cast.RequiredWinPoints);
        }

        // If the server already sent an intel state (possible before open), open/populate the intel window
        if (State is ObjectiveIntelBoundUserInterfaceState intelState)
        {
            // Create a separate popup window without registering it as the primary BUI control
            _intelWindow = new ObjectiveIntelWindow();
            _intelWindow.OpenCentered();
            _intelWindow.Populate(
                intelState.ObjectiveId,
                intelState.ObjectiveDefaultTitle,
                intelState.Tiers ?? new List<ObjectiveIntelTierEntry>(),
                intelState.UnlockedTier,
                intelState.FactionPoints,
                idx => SendMessage(new ObjectivesConsoleUnlockIntelMessage(intelState.ObjectiveId, idx))
            );
            _intelWindow.OnClose += () => _intelWindow = null;
        }
    }

    public void RequestIntel(string objectiveId)
    {
        // Send request to server and wait for it to respond with the intel state.
        Logger.Info($"[ObjectivesConsoleBUI] Sending RequestIntel for objective={objectiveId} owner={Owner}");
        SendMessage(new ObjectivesConsoleRequestIntelMessage(objectiveId));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is ObjectivesConsoleBoundUserInterfaceState cast)
        {
            Logger.Info($"[ObjectivesConsoleBUI] Received Objectives state: objectives={cast.Objectives.Count} win={cast.CurrentWinPoints}/{cast.RequiredWinPoints}");
            if (_window == null)
            {
                _window = this.CreateWindow<ObjectivesConsoleWindow>();
                _window.OnClose += Close;
                _window.RequestIntelCallback = id => RequestIntel(id);
                _window.OpenCentered();
            }
            _window.UpdateObjectives(cast.Objectives, cast.CurrentWinPoints, cast.RequiredWinPoints);
            return;
        }

        if (state is ObjectiveIntelBoundUserInterfaceState intelState)
        {

                Logger.Info("[ObjectivesConsoleBUI] Creating intel window and populating");
                _intelWindow = new ObjectiveIntelWindow();
                _intelWindow.OpenCentered();
                Logger.Info($"[ObjectivesConsoleBUI] After OpenCentered: IsOpen={_intelWindow.IsOpen} Disposed={_intelWindow.Disposed} Parent={( _intelWindow.Parent == null ? "null" : _intelWindow.Parent.GetType().FullName)}");
                _intelWindow.Populate(
                    intelState.ObjectiveId,
                    intelState.ObjectiveDefaultTitle,
                    intelState.Tiers ?? new List<ObjectiveIntelTierEntry>(),
                    intelState.UnlockedTier,
                    intelState.FactionPoints,
                    idx => {
                        Logger.Info($"[ObjectivesConsoleBUI] Sending Unlock request objective={intelState.ObjectiveId} tier={idx}");
                        SendMessage(new ObjectivesConsoleUnlockIntelMessage(intelState.ObjectiveId, idx));
                    }
                );
                Logger.Info($"[ObjectivesConsoleBUI] After Populate: IsOpen={_intelWindow.IsOpen} Disposed={_intelWindow.Disposed} Parent={( _intelWindow.Parent == null ? "null" : _intelWindow.Parent.GetType().FullName)}");
                _intelWindow.OnClose += () => _intelWindow = null;

        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (_window != null)
        {
            try
            {
                if (!_window.Disposed)
                    _window.Orphan(); // Remove from UI tree instead of Dispose
            }
            catch
            {
                // Swallow: if orphaning fails because control is already disposed, just clear reference
            }
            _window = null;
        }

        if (_intelWindow != null)
        {
            try
            {
                if (!_intelWindow.Disposed)
                    _intelWindow.Orphan();
            }
            catch
            {
                // If already disposed, nothing to do
            }
            _intelWindow = null;
        }
    }
}
