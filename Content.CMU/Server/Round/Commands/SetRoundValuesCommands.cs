using System.Linq;
using Content.Server.Administration;
using Content.Shared._RMC14.Rules;
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
            shell.WriteLine($"Opfor platoon set to: {platoon.Name} ({platoon.ID})");
        }

        public CompletionResult GetCompletion(IConsoleShell _, string[] args)
            => RoundCommandCompletion.PlatoonCompletion(args);
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
            shell.WriteLine($"Govfor platoon set to: {platoon.Name} ({platoon.ID})");
        }

        public CompletionResult GetCompletion(IConsoleShell _, string[] args)
            => RoundCommandCompletion.PlatoonCompletion(args);
    }

    [AdminCommand(AdminFlags.Admin)]
    public sealed class SetOpforShipCommand : IConsoleCommand
    {
        public string Command => "setopforship";
        public string Description => "Sets the Opfor ship for the round.";
        public string Help => "Usage: setopforship <shipId>";

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            if (args.Length != 1)
            {
                shell.WriteError("Usage: setopforship <shipId>");
                return;
            }
            var roundSystem = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<AuRoundSystem>();
            roundSystem.SetOpforShip(args[0]);
            shell.WriteLine($"Opfor ship set to: {args[0]}");
        }

        public CompletionResult GetCompletion(IConsoleShell _, string[] args)
            => RoundCommandCompletion.ShipCompletion(args);
    }

    [AdminCommand(AdminFlags.Admin)]
    public sealed class SetGovforShipCommand : IConsoleCommand
    {
        public string Command => "setgovforship";
        public string Description => "Sets the Govfor ship for the round.";
        public string Help => "Usage: setgovforship <shipId>";

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            if (args.Length != 1)
            {
                shell.WriteError("Usage: setgovforship <shipId>");
                return;
            }
            var roundSystem = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<AuRoundSystem>();
            roundSystem.SetGovforShip(args[0]);
            shell.WriteLine($"Govfor ship set to: {args[0]}");
        }

        public CompletionResult GetCompletion(IConsoleShell _, string[] args)
            => RoundCommandCompletion.ShipCompletion(args);
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

        public CompletionResult GetCompletion(IConsoleShell _, string[] args)
            => RoundCommandCompletion.PlanetCompletion(args);
    }

    internal static class RoundCommandCompletion
    {
        internal static CompletionResult PlatoonCompletion(string[] args)
        {
            if (args.Length != 1) return CompletionResult.Empty;

            var protoMan = IoCManager.Resolve<IPrototypeManager>();
            var options = protoMan.EnumeratePrototypes<PlatoonPrototype>()
                .OrderBy(p => p.ID)
                .Select(p => p.ID)
                .ToList();

            return CompletionResult.FromHintOptions(options, "<platoonPrototypeId>");
        }

        internal static CompletionResult PlanetCompletion(string[] args)
        {
            if (args.Length != 1) return CompletionResult.Empty;

            var protoMan = IoCManager.Resolve<IPrototypeManager>();
            var factory = IoCManager.Resolve<IComponentFactory>();
            var options = protoMan.EnumeratePrototypes<EntityPrototype>()
                .Where(p => p.TryGetComponent(out RMCPlanetMapPrototypeComponent? _, factory))
                .OrderBy(p => p.ID)
                .Select(p => p.ID)
                .ToList();

            return CompletionResult.FromHintOptions(options, "<planetPrototypeId>");
        }

        internal static CompletionResult ShipCompletion(string[] args)
        {
            if (args.Length != 1) return CompletionResult.Empty;

            var protoMan = IoCManager.Resolve<IPrototypeManager>();
            var options = protoMan.EnumeratePrototypes<PlatoonPrototype>()
                .SelectMany(p => p.PossibleShips)
                .Distinct()
                .OrderBy(id => id)
                .ToList();

            return CompletionResult.FromHintOptions(options, "<shipId>");
        }
    }
}
