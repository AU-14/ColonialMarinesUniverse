using System.Linq;
using Content.Server.Administration;
using Content.Shared._RMC14.Intel;
using Content.Shared.Administration;
using Content.Shared.AU14.Objectives;
using Robust.Shared.Toolshed;

namespace Content.Server._RMC14.Intel;

[ToolshedCommand, AdminCommand(AdminFlags.VarEdit)]
public sealed class IntelCommand : ToolshedCommand
{
    [CommandImplementation("addpoints")]
    public async void AddPoints([CommandArgument] int points)
    {
    }

    [CommandImplementation("removepoints")]
    public async void RemovePoints([CommandArgument] int points)
    {
    }

    [CommandImplementation("spawnintel")]
    public async void SpawnIntel()
    {
        Sys<IntelSystem>().RunSpawners();
    }
}
