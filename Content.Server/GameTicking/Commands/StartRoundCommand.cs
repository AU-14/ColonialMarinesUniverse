using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.GameTicking.Commands;

[AdminCommand(AdminFlags.Round)]
public sealed partial class StartRoundCommand : LocalizedEntityCommands
{
    [AdminCommand(AdminFlags.RMCMaintainer)] //RMC14
    [AdminCommand(AdminFlags.Round)]
    sealed partial class StartRoundCommand : IConsoleCommand
    {
        [Dependency] private IEntityManager _e = default!;

        public string Command => "startround";
        public string Description => "Ends PreRoundLobby state and starts the round.";
        public string Help => String.Empty;

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            shell.WriteLine(Loc.GetString("shell-can-only-run-from-pre-round-lobby"));
            return;
        }

        _gameTicker.StartRound();
    }
}
