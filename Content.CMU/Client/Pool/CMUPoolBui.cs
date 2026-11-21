using Content.Shared.CMU14.Pool;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Pool;

public sealed class CMUPoolBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private CMUPoolWindow? _window;

    protected override void Open()
    {
        base.Open();
        var visuals = EntMan.System<CMUPoolVisualSystem>();
        var table = new CMUPoolTableControl(() => visuals.GetView(Owner), message => SendMessage(message));
        _window = new CMUPoolWindow(table);
        _window.OnClose += Close;
        _window.OpenCentered();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _window != null)
        {
            _window.OnClose -= Close;
            _window.Close();
            _window = null;
        }

        base.Dispose(disposing);
    }
}
