using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.CMU14.Administration;

/// <summary>
/// Toggles in-game admin mode: the admin keeps answering AHelps but loses every other admin tool, so they can
/// play the round. Run it again to get the full permissions back.
/// An AnyCommand with its own check, because an in-game admin is inactive and could not run an AdminCommand.
/// </summary>
[AnyCommand]
public sealed partial class CMUIngameAdminCommand : LocalizedCommands
{
    [Dependency] private IAdminManager _adminManager = default!;

    public override string Command => "ingameadmin";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }

        if (_adminManager.GetAdminData(player, includeDeAdmin: true) == null)
        {
            shell.WriteError(Loc.GetString("cmu-ingameadmin-not-admin"));
            return;
        }

        var manager = (AdminManager) _adminManager;
        var enable = !manager.CMUIsIngameAdmin(player);
        if (!manager.CMUSetIngameAdmin(player, enable))
            shell.WriteError(Loc.GetString("cmu-ingameadmin-deadminned"));
    }
}
