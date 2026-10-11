using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Client.CMU14.ThreeD.Scene;

[AnyCommand]
public sealed class CMU3DFirstPersonCommand : LocalizedEntityCommands
{
    public override string Command => "cmu_3d_firstperson";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var system = EntityManager.System<CMU3DLiveSceneSystem>();
        if (args.Length == 1 && args[0] == "off")
        {
            system.Close();
            return;
        }
        if (args.Length != 0)
        {
            shell.WriteError(Help);
            return;
        }
        if (!system.OpenFirstPerson())
            shell.WriteError(Loc.GetString("cmu-3d-firstperson-unavailable"));
    }
}
