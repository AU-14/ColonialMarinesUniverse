using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Client.CMU14.ThreeD.Scene;

[AnyCommand]
public sealed class CMU3DCommand : LocalizedEntityCommands
{
    public override string Command => "cmu3d";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 0)
        {
            shell.WriteError(Help);
            return;
        }

        if (!EntityManager.System<CMU3DLiveSceneSystem>().ToggleFirstPerson())
            shell.WriteError(Loc.GetString("cmu-3d-firstperson-unavailable"));
    }
}
