using Content.Server.Administration;
using Content.Server.Chat.Managers;
using Content.Shared.CMU14.CCVar;
using Content.Shared.Administration;
using Robust.Shared.Configuration;
using Robust.Shared.Console;

namespace Content.Server.CMU14.Radio;

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
    public string Description => Loc.GetString("cmu-cmd-clfcomms-desc");
    public string Help => Loc.GetString("cmu-cmd-clfcomms-help");

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
            shell.WriteLine(Loc.GetString(current
                ? "cmu-cmd-clfcomms-status-on"
                : "cmu-cmd-clfcomms-status-off"));

            if (!_config.GetCVar(AU14CCVars.NewCommsSystem))
                shell.WriteLine(Loc.GetString("cmu-cmd-clfcomms-master-off"));

            return;
        }

        if (!TryParseState(args[0], out var wanted))
        {
            shell.WriteError(Loc.GetString("cmu-cmd-clfcomms-invalid-state", ("value", args[0])));
            return;
        }

        if (wanted == current)
        {
            shell.WriteLine(Loc.GetString("cmu-cmd-clfcomms-already",
                ("state", Loc.GetString(current ? "cmu-cmd-clfcomms-state-on" : "cmu-cmd-clfcomms-state-off"))));
            return;
        }

        _config.SetCVar(AU14CCVars.NewCommsSystemClf, wanted);

        // The rest of the admin team should not have to work out why the insurgents
        // suddenly hear each other across the whole map.
        var who = shell.Player?.Name ?? Loc.GetString("cmu-cmd-clfcomms-server");

        _chat.SendAdminAnnouncement(Loc.GetString(wanted
                ? "cmu-cmd-clfcomms-admin-on"
                : "cmu-cmd-clfcomms-admin-off",
            ("user", who)));

        shell.WriteLine(Loc.GetString(wanted
            ? "cmu-cmd-clfcomms-result-on"
            : "cmu-cmd-clfcomms-result-off"));
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length == 1
            ? CompletionResult.FromHintOptions(["on", "off"], Loc.GetString("cmu-cmd-clfcomms-hint"))
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
