using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.CMU14.Yautja;

// sent by the lobby's Ready for the Hunt toggle, the server re-checks the whitelist itself
[AnyCommand]
public sealed class YautjaHuntReadyCommand : LocalizedEntityCommands
{
    [Dependency] private YautjaPredatorRoundSystem _round = default!;

    public override string Command => "yautjahuntready";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }

        if (args.Length != 1 || !bool.TryParse(args[0], out var ready))
        {
            shell.WriteError(Loc.GetString("cmd-yautjahuntready-usage"));
            return;
        }

        if (_round.TrySetHuntReady(player, ready, out var message))
            shell.WriteLine(message);
        else
            shell.WriteError(message);
    }
}
