using System.Globalization;
using Robust.Shared.Console;
using Robust.Shared.ContentPack;

namespace Content.Client.CMU14.Diagnostics.Performance;

public sealed partial class CMUClientPerformanceCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IResourceManager _resources = default!;

    public string Command => "cmu_client_perf";
    public string Description => "Captures detailed client FPS, allocation, rendering and prediction diagnostics to a local log file.";
    public string Help => "Usage: cmu_client_perf [start [seconds=120] [spike_ms=33.333]] | stop | report | status | open | help\n" +
                          "Run with no arguments, close the console and reproduce the FPS drop. Capture stops automatically.\n" +
                          "Use stop to finish early, report for a detailed checkpoint, and open to find the .log file to share.\n" +
                          "Duration: 5–1800s. Spike threshold: 1–10000ms (decimal point). Profiling adds overhead while enabled.";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!TryParse(args, out var action, out var seconds, out var spikeMs))
        {
            shell.WriteError(Help);
            return;
        }
        try
        {
            if (action == "help")
            {
                shell.WriteLine(Help);
                return;
            }
            // File retrieval should still work after disconnecting, when client entity systems have shut down.
            if (action == "open")
            {
                _resources.UserData.CreateDir(CMUClientPerformanceSystem.OutputDirectory);
                _resources.UserData.OpenOsWindow(CMUClientPerformanceSystem.OutputDirectory);
                return;
            }
            if (!_entities.EntitySysManager.TryGetEntitySystem(typeof(CMUClientPerformanceSystem), out var system))
            {
                shell.WriteError("Join a server before capturing client performance. Use cmu_client_perf open to find earlier captures.");
                return;
            }
            var diagnostics = (CMUClientPerformanceSystem) system;
            switch (action)
            {
                case "start":
                    shell.WriteLine(diagnostics.StartCapture(seconds, spikeMs));
                    break;
                case "stop":
                    shell.WriteLine(diagnostics.StopCapture());
                    break;
                case "report":
                    shell.WriteLine(diagnostics.ManualReport());
                    break;
                case "status":
                    shell.WriteLine(diagnostics.Status());
                    break;
            }
        }
        catch (Exception e)
        {
            shell.WriteError($"Client performance diagnostics failed: {e.Message}");
        }
    }

    internal static bool TryParse(string[] args, out string action, out int seconds, out double spikeMs)
    {
        action = args.Length == 0 ? "start" : args[0].ToLowerInvariant();
        seconds = 120;
        spikeMs = 1000d / 30;
        if (action != "start")
            return args.Length == 1 && action is "stop" or "report" or "status" or "open" or "help";
        return args.Length <= 3 &&
               (args.Length < 2 || int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds)) &&
               (args.Length < 3 || double.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out spikeMs)) &&
               seconds is >= 5 and <= 1800 && double.IsFinite(spikeMs) && spikeMs is >= 1 and <= 10000;
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length == 1
            ? CompletionResult.FromOptions(["start", "stop", "report", "status", "open", "help"])
            : CompletionResult.Empty;
    }
}
