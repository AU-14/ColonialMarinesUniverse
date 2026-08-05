using Content.Server.Administration;
using Content.Shared._RMC14.CCVar;
using Content.Shared.Administration;
using Robust.Shared.Configuration;
using Robust.Shared.Console;

namespace Content.Server.GameTicking.Commands;

[AdminCommand(AdminFlags.Round)]
public sealed partial class EndRoundCommand : LocalizedEntityCommands
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private GameTicker _gameTicker = default!;

    public override string Command => "endround";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_gameTicker.RunLevel != GameRunLevel.InRound)
        {
            shell.WriteLine(Loc.GetString("shell-can-only-run-while-round-is-active"));
            return;
        }

        // RMC14: Explicit admin round ends must release an active round-end hold.
        _cfg.SetCVar(RMCCVars.RMCDelayRoundEnd, false);
        _gameTicker.EndRound();
    }
}
