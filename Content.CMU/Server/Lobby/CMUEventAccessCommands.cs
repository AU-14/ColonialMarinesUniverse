using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.CMU14.Lobby;

[AnyCommand]
public sealed partial class CMUEventCodeCommand : LocalizedEntityCommands
{
    [Dependency] private CMUEventAccessSystem _access = default!;

    public override string Command => "eventcode";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }

        if (args.Length != 1)
        {
            shell.WriteError(Help);
            return;
        }

        if (_access.TryRedeem(player, args[0], out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed partial class CMUEventAccessCommand : LocalizedEntityCommands
{
    [Dependency] private CMUEventAccessSystem _access = default!;
    [Dependency] private CMUEventJobTargetsSystem _targets = default!;

    public override string Command => "eventaccess";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 0)
        {
            shell.ExecuteCommand("eventsetup");
            return;
        }
        if (args is ["enable"] or ["disable"])
        {
            _access.SetEnabled(args[0] == "enable");
            shell.WriteLine(Loc.GetString(_access.Enabled ? "cmu-event-enabled" : "cmu-event-disabled"));
            return;
        }
        if (args is ["list"])
        {
            foreach (var (name, jobCount, players) in _access.DescribeGroups())
                shell.WriteLine(Loc.GetString("cmu-event-access-group",
                    ("group", name), ("jobs", jobCount), ("players", players)));
            return;
        }

        if (args is ["remove", var group])
        {
            shell.WriteLine(Loc.GetString(_access.RemoveGroup(group)
                ? "cmu-event-access-removed" : "cmu-event-access-not-found", ("group", group)));
            return;
        }

        if (args.Length < 5 || args[0] != "set")
        {
            shell.WriteError(Help);
            return;
        }

        if (_access.TryConfigureGroup(args[1], args[2], args[3], args.Skip(4).ToArray(), out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args) => args.Length switch
    {
        1 => CompletionResult.FromOptions(["enable", "disable", "set", "remove", "list"]),
        2 when args[0] is "set" or "remove" => CompletionResult.FromHintOptions(_access.GroupNames, "<group>"),
        3 when args[0] == "set" => CompletionResult.FromHint(Loc.GetString("cmu-event-code-placeholder")),
        4 when args[0] == "set" => CompletionResult.FromOptions(_targets.SideOptions()),
        >= 5 when args[0] == "set" => CompletionResult.FromOptions(_targets.RoleOptions(args[3]).Prepend("all")),
        _ => CompletionResult.Empty,
    };
}
