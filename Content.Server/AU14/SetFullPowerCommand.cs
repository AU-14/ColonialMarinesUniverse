using Content.Server.Administration;
using Content.Server.AU14;
using Content.Shared._RMC14.Power;
using Content.Shared.Administration;
using Content.Shared.Light.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Console;

namespace Content.Server.AU14;

[AdminCommand(AdminFlags.Debug)]
public sealed class SetFullPowerCommand : IConsoleCommand
{
    public string Command => "setfullpower";
    public string Description => "Sets all entities with an RMCPowerReceiverComponent to full power.";
    public string Help => "Usage: setfullpower";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
       //need to finish this - eg
    }
}
