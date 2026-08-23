using System.Globalization;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server.AU14.Hospital;

[AdminCommand(AdminFlags.Admin)]
public sealed class HospitalIncidentTimerCommand : IConsoleCommand
{
    public string Command => "hospitalincidenttimer";
    public string Description => "Sets the seconds until the next hospital evacuation shuttle incident.";
    public string Help => "Usage: hospitalincidenttimer <seconds>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1 ||
            !double.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) ||
            seconds < 0)
        {
            shell.WriteError(Help);
            return;
        }

        var hospital = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<HospitalEmergencySystem>();
        var updated = hospital.SetNextIncidentDelay(TimeSpan.FromSeconds(seconds));
        if (updated == 0)
        {
            shell.WriteError("No idle hospital emergency computers are waiting for a shuttle incident.");
            return;
        }

        shell.WriteLine($"Updated {updated} hospital emergency computer(s). Next alert in {seconds.ToString("0.##", CultureInfo.InvariantCulture)} seconds.");
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length == 1
            ? CompletionResult.FromHintOptions(new[] { "0", "60", "180", "600", "720" }, "<seconds>")
            : CompletionResult.Empty;
    }
}
