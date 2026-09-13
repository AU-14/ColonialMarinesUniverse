using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.CMU14.Diagnostics.Performance;

[AdminCommand(AdminFlags.Debug)]
public sealed partial class CMUServerPerformanceCommand : IConsoleCommand
{
    [Dependency] private ICMUServerPerformanceDiagnostics _diagnostics = default!;

    public string Command => "cmuperf";
    public string Description => Loc.GetString("cmu-cmd-server-perf-desc");
    public string Help => Loc.GetString("cmu-cmd-server-perf-help");

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
                    shell.WriteLine(Loc.GetString("cmu-cmd-server-perf-report-written"));
                else
                    shell.WriteError(Loc.GetString("cmu-cmd-server-perf-report-unavailable"));
                break;
            case "reset":
                if (_diagnostics.ResetBaselines())
                    shell.WriteLine(Loc.GetString("cmu-cmd-server-perf-reset"));
                else
                    shell.WriteError(Loc.GetString("cmu-cmd-server-perf-reset-unavailable"));
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
