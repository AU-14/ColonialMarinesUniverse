using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.AU14.util;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server.AU14.Round.Commands
{
    [AdminCommand(AdminFlags.Admin)]
    public sealed class SetOpforCommand : IConsoleCommand
    {
        public string Command => "setopfor";
        public string Description => "Sets the Opfor (opposing force) platoon for the round.";
        public string Help => "Usage: setopfor <platoonPrototypeId>";

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            if (args.Length != 1)
            {
                shell.WriteError("Usage: setopfor <platoonPrototypeId>");
                return;
            }
            var sysMan = IoCManager.Resolve<IEntitySystemManager>();
            var protoMan = IoCManager.Resolve<IPrototypeManager>();
            var platoonSys = sysMan.GetEntitySystem<PlatoonSpawnRuleSystem>();
            if (!protoMan.TryIndex<PlatoonPrototype>(args[0], out var platoon))
            {
                shell.WriteError($"Platoon prototype not found: {args[0]}");
                return;
            }
            platoonSys.SelectedOpforPlatoon = platoon;
            shell.WriteLine($"Opfor platoon set to: {platoon.Name.ToString()} ({platoon.ID.ToString()})");
        }
    }

    [AdminCommand(AdminFlags.Admin)]
    public sealed class SetGovforCommand : IConsoleCommand
    {
        public string Command => "setgovfor";
        public string Description => "Sets the Govfor (government force) platoon for the round.";
        public string Help => "Usage: setgovfor <platoonPrototypeId>";

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            if (args.Length != 1)
            {
                shell.WriteError("Usage: setgovfor <platoonPrototypeId>");
                return;
            }
            var sysMan = IoCManager.Resolve<IEntitySystemManager>();
            var protoMan = IoCManager.Resolve<IPrototypeManager>();
            var platoonSys = sysMan.GetEntitySystem<PlatoonSpawnRuleSystem>();
            if (!protoMan.TryIndex<PlatoonPrototype>(args[0], out var platoon))
            {
                shell.WriteError($"Platoon prototype not found: {args[0]}");
                return;
            }
            platoonSys.SelectedGovforPlatoon = platoon;
            shell.WriteLine($"Govfor platoon set to: {platoon.Name.ToString()} ({platoon.ID.ToString()})");
        }
    }

    [AdminCommand(AdminFlags.Admin)]
    public sealed class SetPlanetCommand : IConsoleCommand
    {
        public string Command => "setplanet";
        public string Description => "Sets the planet for the round by prototype ID.";
        public string Help => "Usage: setplanet <planetPrototypeId>";

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            if (args.Length != 1)
            {
                shell.WriteError("Usage: setplanet <planetPrototypeId>");
                return;
            }
            var roundSystem = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<AuRoundSystem>();
            if (roundSystem.SetPlanet(args[0]))
                shell.WriteLine($"Planet set to: {args[0]}");
            else
                shell.WriteError($"Planet prototype not found: {args[0]}");
        }
    }
}
