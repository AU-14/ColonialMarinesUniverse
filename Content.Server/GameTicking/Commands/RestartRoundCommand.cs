using Content.Server.Administration;
using Content.Server.RoundEnd;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.GameTicking.Commands;

[AdminCommand(AdminFlags.Round)]
public sealed partial class RestartRoundCommand : LocalizedEntityCommands
{
    [AdminCommand(AdminFlags.Round)]
    public sealed partial class RestartRoundCommand : IConsoleCommand
    {
        [Dependency] private IEntityManager _e = default!;

        public string Command => "restartround";
        public string Description => "Ends the current round and starts the countdown for the next lobby.";
        public string Help => string.Empty;

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            shell.WriteLine(Loc.GetString("shell-can-only-run-while-round-is-active"));
            return;
        }

    [AdminCommand(AdminFlags.Round)]
    public sealed partial class RestartRoundNowCommand : IConsoleCommand
    {
        [Dependency] private IEntityManager _e = default!;

    public override string Command => "restartroundnow";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        _gameTicker.RestartRound();
    }
}
