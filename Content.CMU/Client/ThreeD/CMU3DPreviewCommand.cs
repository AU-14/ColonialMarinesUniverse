using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Client.CMU14.ThreeD;

/// <summary>Local asset workbench. Does not request map information or change gameplay entities.</summary>
[AnyCommand]
public sealed class CMU3DPreviewCommand : LocalizedEntityCommands
{
    public override string Command => "cmu_3d";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 1)
        {
            shell.WriteError(Help);
            return;
        }
        var preview = EntityManager.System<CMU3DPreviewSystem>();
        if (args.Length == 1 && args[0] == "off")
        {
            preview.Close();
            return;
        }
        if (!preview.Open(args.Length == 1 ? args[0] : null))
            shell.WriteError(Loc.GetString("cmu-3d-unknown", ("id", args[0])));
    }
}
