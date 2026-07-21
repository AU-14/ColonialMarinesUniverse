using Content.Server.Administration;
using Content.Shared._AU14.Chemistry.Research;
using Content.Shared._CMU14.Chemistry.Reagent;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Server._AU14.Chemistry.Research;

[AdminCommand(Shared.Administration.AdminFlags.Debug)]
internal sealed partial class PickReagentCommand : LocalizedEntityCommands
{
    public override string Command => "research:pick";

    [Dependency] private ServerResearchDataTerminalSystem _research = default!;

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError("research:pick uses only one argument.");
            return;
        }

        if (_research.Selectable.Count > 0)
        {
            if (int.TryParse(args[0], out int result))
            {
                shell.WriteLine($"{_research.Selectable[result].ID} was picked.");
                _research.PickChem(_research.Selectable[result].ID);
                return;
            }
            foreach (var chem in _research.Selectable)
            {
                if (args[0] == chem.ID)
                {
                    _research.PickChem(args[0]);
                    return;
                }
            }
            shell.WriteError("No match for '" + args[0] + "'.");
        }
        else
        {
            shell.WriteError("There are no selectable chemicals.");
        }
    }
}
[AdminCommand(Shared.Administration.AdminFlags.Admin)]
internal sealed partial class GetResearchTimeCommand : LocalizedEntityCommands
{
    public override string Command => "research:gettime";

    [Dependency] private ServerResearchDataTerminalSystem _dat = default!;
    [Dependency] private IGameTiming _time = default!;

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        TimeSpan time = _time.CurTime - _dat.NextReroll;
        string minutes = time.Minutes.ToString();
        string seconds = time.Seconds.ToString();
        shell.WriteLine(minutes + " minute(s) and " + seconds + " second(s) from now.");
    }
}

[AdminCommand(Shared.Administration.AdminFlags.Admin)]
internal sealed partial class GetTerminalChemicals : LocalizedEntityCommands
{
    public override string Command => "research:getterminalchems";

    [Dependency] private ServerResearchDataTerminalSystem _dat = default!;
    [Dependency] private IPrototypeManager _protoman = default!;

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var instances = _protoman.GetInstances<ReagentPropertyPrototype>();
        foreach (var line in _dat.Selectable)
        {
            string props = "";
            foreach (var prop in line.Effects)
            {
                props += (" " + instances[prop.Key].Code + prop.Value);
            }
            shell.WriteLine(line.ID + " | " + line.GenTier + " |" + props);
        }
    }
}

[AdminCommand(Shared.Administration.AdminFlags.Admin)]
internal sealed partial class GetTerminalPoints : LocalizedEntityCommands
{
    public override string Command =>  "research:getpoints";
    [Dependency] private ServerResearchDataTerminalSystem _dat = default!;
    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        string clearance = (_dat.Clearance == 6) ? "X" : _dat.Clearance.ToString();
        shell.WriteLine(_dat.Credits.ToString() + " points at " + clearance + " clearance.");
    }
}

[AdminCommand(Shared.Administration.AdminFlags.Admin)]
internal sealed partial class SetTerminalPoints : LocalizedEntityCommands
{
    [Dependency] private SharedResearchDataTerminalSystem _term = default!;
    public override string Command =>  "research:setpointsclearance";
    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1 || args.Length > 2)
            shell.WriteError("research:setpointsclearance takes in a minimum of one argument and a maximum of two arguments.");
        int clearance = -1;
        if (args.Length == 2)
        {
            clearance = Convert.ToInt32(args[1]);
            if (args[1] == "X")
                clearance = 6;
        }
        int points = Convert.ToInt32(args[0]);
        _term.UpdateClearance(points, clearance);
    }
}
