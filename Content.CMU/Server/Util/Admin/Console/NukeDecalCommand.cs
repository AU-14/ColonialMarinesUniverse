using System.Linq;
using Content.Server.Administration;
using Content.Server.Decals;
using Content.Shared.Administration;
using Content.Shared.Decals;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Util.Admin.Console;

[AdminCommand(AdminFlags.Fun)]
public sealed partial class NukeDecalsCommand : LocalizedEntityCommands
{
    [Dependency] private DecalSystem _decalSys = default!;
    [Dependency] private IPrototypeManager _protoMan = default!;

    public override string Command => "nukedecals";
    public override string Help => "nukedecals [decalId...] - Deletes decals from every loaded grid." +
        " With no arguments, deletes all decals. With decal prototype ids given, only those are deleted.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var idFilter = args.Length > 0 ? new HashSet<string>(args) : null;
        var totalRemoved = 0;
        var gridCount = 0;

        var query = EntityManager.AllEntityQuery<DecalGridComponent>();
        while (query.MoveNext(out var gridUid, out var decalGrid))
        {
            totalRemoved += _decalSys.RemoveDecals(gridUid, idFilter, decalGrid);
            gridCount++;
        }

        shell.WriteLine(idFilter != null
            ? $"Removed {totalRemoved} decals matching {idFilter.Count} ids from {gridCount} grids."
            : $"Removed {totalRemoved} decals from {gridCount} grids.");
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        var alreadyTyped = new HashSet<string>(args);
        var options = _protoMan.EnumeratePrototypes<DecalPrototype>().Select(p => p.ID).Where(id => !alreadyTyped.Contains(id));
        return CompletionResult.FromHintOptions(options, "[decalId...]");
    }
}
