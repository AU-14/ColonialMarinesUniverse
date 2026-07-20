using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.Mind.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed partial class MakeSentientCommand : LocalizedEntityCommands
{
    [AdminCommand(AdminFlags.Admin)]
    public sealed partial class MakeSentientCommand : IConsoleCommand
    {
        [Dependency] private IEntityManager _entManager = default!;

        public string Command => "makesentient";
        public string Description => "Makes an entity sentient (able to be controlled by a player)";
        public string Help => "makesentient <entity id>";

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            shell.WriteLine(Loc.GetString("shell-need-exactly-one-argument"));
            return;
        }

        if (!NetEntity.TryParse(args[0], out var entNet) || !EntityManager.TryGetEntity(entNet, out var entId) || !EntityManager.EntityExists(entId))
        {
            shell.WriteLine(Loc.GetString("shell-could-not-find-entity-with-uid", ("uid", args[0])));
            return;
        }

        _mindSystem.MakeSentient(entId.Value);
    }
}
