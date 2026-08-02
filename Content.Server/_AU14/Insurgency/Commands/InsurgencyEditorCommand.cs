using Content.Server.Administration;
using Content.Server.AU14.Round;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Server._AU14.Insurgency.Database;
using Content.Server._AU14.Insurgency.Editor;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._AU14.Insurgency.Commands;

/// <summary>
///     Opens the Default-faction editor for the calling player. This console command is the
///     permanent, always-available entry point: the Improved Construction Menu's "Tools" section may
///     additionally host the editor on servers that ship that menu, but nothing here depends on it,
///     so servers without it lose nothing.
/// </summary>
// AnyCommand + an explicit IsAuthorized check: admins pass as before, and players job-whitelisted
// for InsurgencyAuthorization.EditorWhitelistJob (jobwhitelistadd <player> InsforEditor) also pass.
// An [AdminCommand] gate would lock the whitelisted non-admins out before our check ran.
[AnyCommand]
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

        var adminCheck = IoCManager.Resolve<IAdminManager>();
        if (!InsurgencyAuthorization.IsAuthorized(adminCheck, player))
        {
            shell.WriteError("You are not whitelisted for the INSFOR editor.");
            return;
        }

        var eui = IoCManager.Resolve<EuiManager>();
        var admin = IoCManager.Resolve<IAdminManager>();
        var entMan = IoCManager.Resolve<IEntityManager>();

        var editor = new InsurgencyFactionEditorEui(
            admin,
            entMan.System<InsurgencyFactionDbSystem>(),
            entMan.System<InsurgencyFactionApplySystem>(),
            entMan.System<PlatoonSpawnRuleSystem>(),
            IoCManager.Resolve<IPrototypeManager>());

        eui.OpenEui(editor, player);
    }
}
