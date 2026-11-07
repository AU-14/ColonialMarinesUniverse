using Content.Client.UserInterface.Screens;
using Robust.Client.UserInterface;
using Robust.Shared.Console;

namespace Content.Client.CMU14.SceneAnimation;

/// <summary>
/// Toggles a clean cinematic viewport while keeping the debug console available.
/// Deliberately does not use the engine's existing "scene" command.
/// </summary>
public sealed class CMUFreecamCommand : IConsoleCommand
{
    public string Command => "freecam";
    public string Description => "Toggle the cinematic HUD-free view.";
    public string Help => "freecam [on|off|toggle|status]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var ui = IoCManager.Resolve<IUserInterfaceManager>();
        if (ui.ActiveScreen is not InGameScreen screen)
        {
            shell.WriteError("The freecam cinematic view is only available in game.");
            return;
        }

        if (args.Length > 1)
        {
            shell.WriteLine(Help);
            return;
        }

        var operation = args.Length == 0 ? "toggle" : args[0].ToLowerInvariant();
        switch (operation)
        {
            case "on":
                screen.SetCinematicMode(true);
                break;
            case "off":
                screen.SetCinematicMode(false);
                break;
            case "toggle":
                screen.SetCinematicMode(!screen.CinematicMode);
                break;
            case "status":
                shell.WriteLine($"Freecam cinematic mode is {(screen.CinematicMode ? "on" : "off")}.");
                return;
            default:
                shell.WriteLine(Help);
                return;
        }

        shell.WriteLine($"Freecam cinematic mode {(screen.CinematicMode ? "enabled" : "disabled")}.");
    }
}
