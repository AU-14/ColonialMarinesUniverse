using System.Globalization;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.CMU14.Round.ColonyShift;

[AdminCommand(AdminFlags.Round)]
public sealed partial class ColonyRotationCallCommand : IConsoleCommand
{
    [Dependency] private IEntitySystemManager _systems = default!;

    public string Command => "rotationcall";
    public string Description => Loc.GetString("colony-rotation-command-call-description");
    public string Help => Loc.GetString("colony-rotation-command-call-help");

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        TimeSpan? eta = null;
        if (args.Length > 1)
        {
            shell.WriteError(Help);
            return;
        }

        if (args.Length == 1)
        {
            if (!double.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var minutes) ||
                !double.IsFinite(minutes) || minutes < 0 || minutes > 600)
            {
                shell.WriteError(Help);
                return;
            }

            eta = TimeSpan.FromMinutes(minutes);
        }

        if (!_systems.GetEntitySystem<ColonyRotationRuleSystem>().TryAdminCall(eta, out var reason))
        {
            shell.WriteError(reason ?? Help);
            return;
        }

        shell.WriteLine(Loc.GetString("colony-rotation-command-called"));
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length == 1
            ? CompletionResult.FromHintOptions(new[] { "1", "5", "10" },
                Loc.GetString("colony-rotation-command-minutes-hint"))
            : CompletionResult.Empty;
    }
}

[AdminCommand(AdminFlags.Round)]
public sealed partial class ColonyRotationRecallCommand : IConsoleCommand
{
    [Dependency] private IEntitySystemManager _systems = default!;

    public string Command => "rotationrecall";
    public string Description => Loc.GetString("colony-rotation-command-recall-description");
    public string Help => Loc.GetString("colony-rotation-command-recall-help");

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 0)
        {
            shell.WriteError(Help);
            return;
        }

        if (!_systems.GetEntitySystem<ColonyRotationRuleSystem>().TryAdminRecall(out var reason))
        {
            shell.WriteError(reason ?? Help);
            return;
        }

        shell.WriteLine(Loc.GetString("colony-rotation-command-recalled"));
    }
}
