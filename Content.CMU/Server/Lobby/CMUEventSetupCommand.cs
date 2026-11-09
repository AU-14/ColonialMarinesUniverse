using Content.Server.Administration;
using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.CMU14.Lobby;

[AdminCommand(AdminFlags.Admin)]
public sealed partial class CMUEventSetupCommand : LocalizedEntityCommands
{
    [Dependency] private EuiManager _euis = default!;

    public override string Command => "eventsetup";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }
        _euis.OpenEui(new CMUEventSetupEui(), player);
    }
}
