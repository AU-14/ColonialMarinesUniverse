using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.CMU14.BugHelp;

[AnyCommand]
public sealed class BugHelpCommand : IConsoleCommand
{
    public string Command => "bughelp";
    public string Description => Loc.GetString("cmu-cmd-bughelp-desc");
    public string Help => Loc.GetString("cmu-cmd-bughelp-help");

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("cmu-cmd-bughelp-player-only"));
            return;
        }

        var message = string.Join(' ', args).Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            shell.WriteLine(Loc.GetString("cmu-cmd-bughelp-empty"));
            return;
        }

        var system = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<BugHelpSystem>();
        switch (system.TrySubmit(player, message, out var reportId))
        {
            case BugHelpOutcome.Sent:
                shell.WriteLine(Loc.GetString("cmu-cmd-bughelp-sent", ("id", reportId!)));
                break;
            case BugHelpOutcome.Disabled:
                shell.WriteError(Loc.GetString("cmu-cmd-bughelp-disabled"));
                break;
            case BugHelpOutcome.RateLimited:
                shell.WriteError(Loc.GetString("cmu-cmd-bughelp-rate-limited"));
                break;
            case BugHelpOutcome.NoEntity:
                shell.WriteError(Loc.GetString("cmu-cmd-bughelp-no-entity"));
                break;
        }
    }
}
