using Content.Shared.Administration;
using Robust.Shared.Console;
using Content.Shared.CMU14.Input;
using Robust.Client.Input;

namespace Content.Client.CMU14.ThreeD.Scene;

[AnyCommand]
public sealed partial class CMU3DMouseCaptureCommand : LocalizedEntityCommands
{
    [Dependency] private IInputManager _input = default!;
    public override string Command => "cmu_3d_capture";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 1 || args.Length == 1 && args[0] != "off")
        {
            shell.WriteError(Help);
            return;
        }

        var capture = args.Length == 0;
        if (!EntityManager.System<CMU3DLiveSceneSystem>().RequestMouseCapture(capture))
            shell.WriteError(Loc.GetString("cmu-3d-capture-unavailable"));
        else
            shell.WriteLine(Loc.GetString(capture ? "cmu-3d-capture-requested" : "cmu-3d-capture-released",
                ("key", _input.GetKeyFunctionButtonString(CMUKeyFunctions.CMUToggleFirstPersonMouse))));
    }
}
