using Content.Shared.AU14.Objectives;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client.AU14.Objectives;

public sealed class ObjectivesConsoleBoundUserInterface : BoundUserInterface
{
    private ObjectivesConsoleWindow? _window;

    public ObjectivesConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = new ObjectivesConsoleWindow();
        _window.OnClose += Close;
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not ObjectivesConsoleBoundUserInterfaceState cast)
            return;
        _window?.UpdateObjectives(cast.Objectives, cast.CurrentWinPoints, cast.RequiredWinPoints);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (_window != null)
        {
            _window.Orphan(); // Remove from UI tree instead of Dispose
            _window = null;
        }
    }
}
