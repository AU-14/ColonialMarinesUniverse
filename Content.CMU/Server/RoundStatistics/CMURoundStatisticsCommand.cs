using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._CMU14.RoundStatistics;

[AnyCommand]
public sealed partial class CMURoundStatisticsCommand : LocalizedCommands
{
    [Dependency] private EuiManager _eui = default!;

    public override string Command => "cmuroundstats";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var player = shell.Player;
        if (player == null)
        {
            shell.WriteLine("shell-only-players-can-run-this-command");
            return;
        }

        _eui.OpenEui(new CMURoundStatisticsEui(), player);
    }
}
