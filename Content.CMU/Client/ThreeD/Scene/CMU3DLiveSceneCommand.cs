using Robust.Shared.Console;

namespace Content.Client.CMU14.ThreeD.Scene;

/// <summary>The system rechecks permission throughout the window lifetime, not just at command dispatch.</summary>
public sealed class CMU3DLiveSceneCommand : LocalizedEntityCommands
{
    public override string Command => "cmu_3d_live";

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
        if (!system.Open())
            shell.WriteError(Loc.GetString("cmu-3d-live-unavailable"));
    }
}
