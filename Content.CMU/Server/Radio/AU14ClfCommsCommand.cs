using Content.Server.Administration;
using Content.Server.Chat.Managers;
using Content.Shared._AU14.CCVar;
using Content.Shared.Administration;
using Robust.Shared.Configuration;
using Robust.Shared.Console;

namespace Content.Server._AU14.Radio;

/// <summary>
///     Turns the comms overhaul off over the CLF/INSFOR nets and back on, without touching
///     GOVFOR or OPFOR. Exists because the cvar behind it is host-only, and the admin who
///     needs this is whoever is watching a cell that cannot talk to itself.
/// </summary>
[AdminCommand(AdminFlags.Fun)]
public sealed partial class AU14ClfCommsCommand : IConsoleCommand
{
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IChatManager _chat = default!;

    public string Command => "clfcomms";

    public string Description =>
        "Toggles the AU14 comms system over the CLF/INSFOR nets. Off means the cell's channels " +
        "work like stock radio: no coverage requirement, no static, no callsigns.";

    public string Help => "Usage: clfcomms [on|off]. With no argument, reports the current state.";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 1)
        {
            shell.WriteError(Help);
            return;
        }

        var current = _config.GetCVar(AU14CCVars.NewCommsSystemClf);

        if (args.Length == 0)
        {
            shell.WriteLine(current
                ? "CLF comms: ON. The cell's nets are anchor-gated and run under the full comms system."
                : "CLF comms: OFF. The cell's nets are running as stock radio.");

            if (!_config.GetCVar(AU14CCVars.NewCommsSystem))
                shell.WriteLine("Note: the master switch (au14.new_comms_system) is off, so this does nothing right now.");

            return;
        }

        if (!TryParseState(args[0], out var wanted))
        {
            shell.WriteError($"Could not read '{args[0]}'. Use on or off.");
            return;
        }

        if (wanted == current)
        {
            shell.WriteLine($"CLF comms are already {(current ? "on" : "off")}.");
            return;
        }

        _config.SetCVar(AU14CCVars.NewCommsSystemClf, wanted);

        // the rest of the admin team should not have to work out why the insurgents
        // suddenly hear each other across the whole map
        var who = shell.Player?.Name ?? "The server";

        _chat.SendAdminAnnouncement(wanted
            ? $"{who} turned the comms system back on for CLF/INSFOR."
            : $"{who} turned the comms system off for CLF/INSFOR - their nets are stock radio now.");

        shell.WriteLine(wanted
            ? "CLF comms ON. The cell is back under coverage rules, static and callsigns."
            : "CLF comms OFF. The cell's nets now reach anywhere, unmasked and unjammed.");
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length == 1
            ? CompletionResult.FromHintOptions(["on", "off"], "on|off")
            : CompletionResult.Empty;
    }

    private static bool TryParseState(string arg, out bool state)
    {
        switch (arg.ToLowerInvariant())
        {
            case "on":
            case "true":
            case "1":
            case "enable":
            case "enabled":
                state = true;
                return true;

            case "off":
            case "false":
            case "0":
            case "disable":
            case "disabled":
                state = false;
                return true;

            default:
                state = false;
                return false;
        }
    }
}
