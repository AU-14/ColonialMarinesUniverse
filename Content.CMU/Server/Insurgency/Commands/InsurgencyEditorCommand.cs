using Content.Server.Administration;
using Content.Server.AU14.Round;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Server._AU14.Insurgency.Database;
using Content.Server._AU14.Insurgency.Editor;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._AU14.Insurgency.Commands;

/// <summary>
///     Opens the Default-faction editor for the calling player. Groundwork: the plan later hosts
///     this same editor under the Improved Construction Menu "Tools" section, but a console command
///     is the launch point for now.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class InsurgencyEditorCommand : IConsoleCommand
{
    public string Command => "insforeditor";
    public string Description => "Opens the INSFOR Default-faction editor.";
    public string Help => "Usage: insforeditor";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var player = shell.Player;
        if (player == null)
        {
            shell.WriteError("This command can only be run by a player.");
            return;
        }

        var eui = IoCManager.Resolve<EuiManager>();
        var admin = IoCManager.Resolve<IAdminManager>();
        var entMan = IoCManager.Resolve<IEntityManager>();

        var editor = new InsurgencyFactionEditorEui(
            admin,
            entMan.System<InsurgencyFactionDbSystem>(),
            entMan.System<InsurgencyFactionApplySystem>(),
            entMan.System<PlatoonSpawnRuleSystem>());

        eui.OpenEui(editor, player);
    }
}
