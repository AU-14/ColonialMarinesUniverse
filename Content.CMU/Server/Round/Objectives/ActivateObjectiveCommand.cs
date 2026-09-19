using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;

namespace Content.Server.CMU14.Round.Objectives;

[AdminCommand(AdminFlags.Fun)]
public sealed class ActivateObjectiveCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entManager = default!;

    public string Command => "activateobjective";
    public string Description => "Activates inactive planet objectives mid-round.";
    public string Help =>
        "Usage: activateobjective [prototypeId]\n" +
        "Without an argument, activates every inactive hotspot objective on the planet. " +
        "With an entity prototype id, activates every inactive objective spawned from that prototype.";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 1)
        {
            shell.WriteError(Help);
            return;
        }

        var activated = _entManager.System<ObjectiveControlSystem>()
            .ActivateInactiveObjectives(args.Length == 1 ? args[0] : null);

        shell.WriteLine(activated == 0
            ? "No inactive objectives matched."
            : $"Activated {activated} objective(s).");
    }
}
