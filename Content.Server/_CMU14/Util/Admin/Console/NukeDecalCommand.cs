using System.Collections.Generic;
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

    public override string Command => "nuke:decals";
    public override string Description => "Deletes decals from every loaded grid.";
    public override string Help =>
        "nuke:decals [all (true/false, default: false)] [decalId...] - Deletes decals from every loaded grid.\n" +
        " By default this will only delete cleanable decals (like blood/dirt etc.) to spare map details.\n" +
        " To delete all decals (including mapper placed details), you should pass 'true' as the first argument.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var all = false;
        var idArgs = args.AsEnumerable();
        if (args.Length > 0 && bool.TryParse(args[0], out var parsedBool))
        {
            all = parsedBool;
            idArgs = args.Skip(1);
        }

        var idArray = idArgs.ToArray();
        var idFilter = idArray.Length > 0 ? new HashSet<string>(idArray) : null;
        int totalRemoved = 0, totalSkipped = 0, gridCount = 0;
        var query = EntityManager.EntityQueryEnumerator<DecalGridComponent>();
        while (query.MoveNext(out var gridUid, out var decalGrid))
        {
            var (removed, skipped) = _decalSys.RemoveDecals(gridUid, idFilter, !all, decalGrid);
            totalRemoved += removed;
            totalSkipped += skipped;
            gridCount++;
        }

        var filterMsg = idFilter != null ? $" matching {idFilter.Count} ids" : "";
        var cleanMsg = all ? " (all decals)" : " (cleanable only)";
        shell.WriteLine($"Removed {totalRemoved} decals{filterMsg}{cleanMsg} from {gridCount} grids.");

        if (totalSkipped > 0)
        {
            shell.WriteLine($"[nuke:decals] {totalSkipped} matching decals were found but skipped because they have disabled defaultCleanable (janitor clean).");
            shell.WriteLine($"To delete them, run the command again starting with 'true' ('nuke:decals true {string.Join(" ", idArray)}').");
        }
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        var alreadyTyped = new HashSet<string>(args);
        var decalOptions = _protoMan
            .EnumeratePrototypes<DecalPrototype>()
            .Select(p => p.ID)
            .Where(id => !alreadyTyped.Contains(id));

        if (args.Length == 1)
        {
            var options = new List<string> { "true", "false" };
            options.AddRange(decalOptions);
            return CompletionResult.FromHintOptions(options, "[all (default: false)] or [decalId]");
        }

        return CompletionResult.FromHintOptions(decalOptions, "[decalId...]");
    }
}
