using Content.Server.Administration;
using Content.Shared._RMC14.CCVar;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Console;

namespace Content.Server.GameTicking.Commands;

[AdminCommand(AdminFlags.Round)]
public sealed partial class EndRoundCommand : LocalizedEntityCommands
{
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    public override string Command => "endround";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_gameTicker.RunLevel != GameRunLevel.InRound)
        {
            shell.WriteLine(Loc.GetString("shell-can-only-run-while-round-is-active"));
            return;
        }

        // RMC14: an explicit admin round end must bypass a previously enabled delay.
        _cfg.SetCVar(RMCCVars.RMCDelayRoundEnd, false);
        // CMU14: endroundhold documents that endround releases the hold; without this the
        // swallowed end leaves the round running with no visible way to finish it.
        _cfg.SetCVar(CCVars.HoldRoundEnd, false);
        _gameTicker.EndRound();
    }
}
