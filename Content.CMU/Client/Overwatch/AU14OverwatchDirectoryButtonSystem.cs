using Content.Client._RMC14.Overwatch;
using Content.Shared._AU14.Callsigns;
using Content.Shared._RMC14.Overwatch;

namespace Content.Client._AU14.Overwatch;

// injects the comms directory tab into open overwatch consoles from outside the
// upstream BUI, so no RMC file needs to wire the button in. only entities that
// carry the directory component qualify, stock RMC consoles are never touched
public sealed class AU14OverwatchDirectoryButtonSystem : EntitySystem
{
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<AU14CallsignConsoleComponent, UserInterfaceComponent>();

        while (query.MoveNext(out var uid, out _, out var ui))
        {
            if (_ui.TryGetOpenUi<OverwatchConsoleBui>((uid, ui), OverwatchConsoleUI.Key, out var bui))
                bui.AU14EnsureCommsDirectoryButtons();
        }
    }
}
