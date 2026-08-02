using Content.Server.Administration;
using Content.Server.Administration.Commands;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Console;

namespace Content.Server._CMU14.TileMovement;

[AdminCommand(AdminFlags.Server)]
public sealed partial class CMUForceTileMovementCommand : LocalizedCommands
{
    [Dependency] private IConfigurationManager _cfg = default!;

    public override string Command => "cmuforcetilemovement";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var toggle = PanicBunkerCommand.Toggle(CCVars.CMUTileMovementForcedForAll, shell, args, _cfg, LocalizationManager);
        if (toggle == null)
            return;

        shell.WriteLine(Loc.GetString(toggle.Value
            ? "cmuforcetilemovement-command-enabled"
            : "cmuforcetilemovement-command-disabled"
        ));
    }
}
