using System.Collections.Generic;
using System.Linq;
using Content.Server.Administration;
using Content.Server.Decals;
using Content.Shared.Administration;
using Content.Shared.Decals;
using Robust.Shared.Console;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Util.Admin.Console;

[AdminCommand(AdminFlags.Fun)]
public sealed partial class NukeDecalsCommand : LocalizedEntityCommands
{
    [Dependency] private DecalSystem _decalSys = default!;
    [Dependency] private IPrototypeManager _protoMan = default!;

    public override string Command => "nukedecals";
    public override string Help => "nukedecals [cleanableOnly (true/false, default: true)] [decalId...] - Deletes decals from every loaded grid.\n" +
        " By default this will only delete cleanable decals (like blood/dirt etc.) to spare map details.\n" +
        " To delete all decals (including mapper placed details), you should pass 'false' as the first argument.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var cleanableOnly = true;
        var idArgs = args.AsEnumerable();
        if (args.Length > 0 && bool.TryParse(args[0], out var parsedBool))
        {
            cleanableOnly = parsedBool;
            idArgs = args.Skip(1);
        }

        var idArray = idArgs.ToArray();
        var idFilter = idArray.Length > 0 ? new HashSet<string>(idArray) : null;
        int totalRemoved = 0, totalSkipped = 0;
        var grids = new HashSet<EntityUid>();
        var query = EntityManager.EntityQueryEnumerator<ChunkEntityComponent, DecalChunkComponent>();
        while (query.MoveNext(out _, out var chunk, out var decals))
        {
            grids.Add(chunk.Root);
            foreach (var (id, decal) in decals.Decals.ToArray())
            {
                if (idFilter != null && !idFilter.Contains(decal.Id))
                    continue;

                if (cleanableOnly &&
                    (!_protoMan.TryIndex<DecalPrototype>(decal.Id, out var prototype) || !prototype.DefaultCleanable))
                {
                    totalSkipped++;
                    continue;
                }

                if (_decalSys.RemoveDecal(chunk.Root, new DecalIndex(chunk.Chunk, id)))
                    totalRemoved++;
            }
        }

        var filterMsg = idFilter != null ? $" matching {idFilter.Count} ids" : "";
        var cleanMsg = cleanableOnly ? " (cleanable only)" : " (all decals)";
        shell.WriteLine($"Removed {totalRemoved} decals{filterMsg}{cleanMsg} from {grids.Count} grids.");

        if (totalSkipped > 0)
        {
            shell.WriteLine($"[nukedecals] {totalSkipped} matching decals were found but skipped because they have disabled defaultCleanable (janitor clean).");
            shell.WriteLine($"To delete them, run the command again starting with 'false' ('nukedecals false {string.Join(" ", idArray)}').");
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
            return CompletionResult.FromHintOptions(options, "[cleanableOnly (default: true)] or [decalId]");
        }

        return CompletionResult.FromHintOptions(decalOptions, "[decalId...]");
    }
}
