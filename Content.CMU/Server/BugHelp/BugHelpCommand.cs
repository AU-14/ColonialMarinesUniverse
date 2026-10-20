using Content.Server.CMU14.BugHelp;
using Content.Shared.Administration;
using Content.Shared.CMU14.BugHelp;
using Robust.Shared.Console;
using Robust.Shared.Network;

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
            // Bare bughelp opens the report window; the text is its fast path.
            IoCManager.Resolve<INetManager>()
                .ServerSendMessage(new BugHelpOpenMsg(), player.Channel);
            return;
        }

        var system = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<BugHelpSystem>();
        switch (system.TrySubmit(player, message, null, null, null, null, out var reportId))
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
