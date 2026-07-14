using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._CMU14.BalanceRating;

[AnyCommand]
public sealed partial class CMUBalanceRatingCommand : LocalizedCommands
{
    [Dependency] private EuiManager _eui = default!;

    public override string Command => "ratingstats";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }

        _eui.OpenEui(new CMUBalanceRatingEui(), player);
    }
}
