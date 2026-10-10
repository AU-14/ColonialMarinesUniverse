using System.Globalization;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server.CMU14.Hospital;

[AdminCommand(AdminFlags.Admin)]
public sealed class HospitalIncidentTimerCommand : IConsoleCommand
{
    public string Command => "hospitalincidenttimer";
    public string Description => Loc.GetString("hospital-emergency-command-description");
    public string Help => Loc.GetString("hospital-emergency-command-help");

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1 ||
            !double.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) ||
            !double.IsFinite(seconds) || seconds < 0 || seconds > TimeSpan.FromDays(365).TotalSeconds)
        {
            shell.WriteError(Help);
            return;
        }

        var hospital = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<HospitalEmergencySystem>();
        var updated = hospital.SetNextIncidentDelay(TimeSpan.FromSeconds(seconds));
        if (updated == 0)
        {
            shell.WriteError(Loc.GetString("hospital-emergency-command-no-idle-computers"));
            return;
        }

        shell.WriteLine(Loc.GetString("hospital-emergency-command-updated",
            ("count", updated),
            ("seconds", seconds.ToString("0.##", CultureInfo.InvariantCulture))));
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length == 1
            ? CompletionResult.FromHintOptions(
                new[] { "0", "60", "180", "600", "720" },
                Loc.GetString("hospital-emergency-command-seconds-hint"))
            : CompletionResult.Empty;
    }
}
