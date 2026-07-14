using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._CMU14.Diagnostics.Performance;

[AdminCommand(AdminFlags.Debug)]
public sealed partial class CMUServerPerformanceCommand : IConsoleCommand
{
    [Dependency] private ICMUServerPerformanceDiagnostics _diagnostics = default!;

    public string Command => "cmuperf";
    public string Description => "Shows or manually captures CMU automatic server performance diagnostics.";
    public string Help => "Usage: cmuperf status | report | reset";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        string mode = args.Length == 0 ? "status" : args[0].ToLowerInvariant();
        switch (mode)
        {
            case "status":
                shell.WriteLine(_diagnostics.GetStatus());
                break;
            case "report":
                if (_diagnostics.CaptureManualReport())
                    shell.WriteLine("Detailed CMU performance report written to the cmu.server-performance sawmill.");
                else
                    shell.WriteError("Performance diagnostics are disabled or have not produced their first sample.");
                break;
            case "reset":
                if (_diagnostics.ResetBaselines())
                    shell.WriteLine("CMU performance rate windows and healthy baselines were reset.");
                else
                    shell.WriteError("Performance diagnostics are disabled or not initialized.");
                break;
            default:
                shell.WriteError(Help);
                break;
        }
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length == 1
            ? CompletionResult.FromOptions(["status", "report", "reset"])
            : CompletionResult.Empty;
    }
}
