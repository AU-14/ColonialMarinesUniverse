using System.Collections.Generic;
using System.Linq;
using Content.Server.Administration;
using Content.Server.Decals;
using Content.Shared.Administration;
using Content.Shared.Decals;
using Robust.Shared.Console;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Util.Admin.Console;

[AdminCommand(AdminFlags.Fun)]
public sealed partial class NukeDecalsCommand : LocalizedEntityCommands
{
    [Dependency] private DecalSystem _decalSys = default!;
    [Dependency] private IPrototypeManager _protoMan = default!;

    public override string Command => "nuke:decals";
    public override string Description => Loc.GetString("cmu-cmd-nuke-decals-desc");
    public override string Help => Loc.GetString("cmu-cmd-nuke-decals-help");

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
        var query = EntityManager.EntityQueryEnumerator<MapGridComponent>();
        while (query.MoveNext(out var gridUid, out _))
        {
            var (removed, skipped) = _decalSys.RemoveDecals(gridUid, idFilter, !all);
            totalRemoved += removed;
            totalSkipped += skipped;
            gridCount++;
        }

        var scope = Loc.GetString(all
            ? "cmu-cmd-nuke-decals-scope-all"
            : "cmu-cmd-nuke-decals-scope-cleanable");
        shell.WriteLine(idFilter != null
            ? Loc.GetString("cmu-cmd-nuke-decals-summary-filtered",
                ("removed", totalRemoved),
                ("filterCount", idFilter.Count),
                ("scope", scope),
                ("grids", gridCount))
            : Loc.GetString("cmu-cmd-nuke-decals-summary",
                ("removed", totalRemoved),
                ("scope", scope),
                ("grids", gridCount)));

        if (totalSkipped > 0)
        {
            shell.WriteLine(Loc.GetString("cmu-cmd-nuke-decals-skipped", ("count", totalSkipped)));
            shell.WriteLine(Loc.GetString("cmu-cmd-nuke-decals-retry", ("ids", string.Join(" ", idArray))));
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
            return CompletionResult.FromHintOptions(options, Loc.GetString("cmu-cmd-nuke-decals-hint-first"));
        }

        return CompletionResult.FromHintOptions(decalOptions, Loc.GetString("cmu-cmd-nuke-decals-hint-id"));
    }
}
