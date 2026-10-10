using Content.Server.CMU14.ZLevels.Core;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Server.GameObjects;
using Robust.Shared.Console;
using Robust.Shared.Map;

namespace Content.Server.CMU14.ZLevels.Mapping;

[AdminCommand(AdminFlags.Server | AdminFlags.Mapping)]
public sealed partial class CMUCombineZNetworkCommand : LocalizedEntityCommands
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private CMUZLevelsSystem _zLevels = default!;
    [Dependency] private MetaDataSystem _meta = default!;

    public override string Command => "znetwork-combine";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return CompletionResult.FromHintOptions(
            CompletionHelper.MapIds(_entities),
            Loc.GetString("cmu-cmd-znetwork-combine-map-hint"));
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteError(Loc.GetString("cmu-cmd-znetwork-combine-not-enough"));
            return;
        }

        List<MapId> maps = new();
        foreach (var arg in args)
        {
            if (!int.TryParse(arg, out var mapIdInt))
            {
                shell.WriteError(Loc.GetString("cmu-cmd-znetwork-combine-parse-map", ("value", arg)));
                return;
            }

            var mapId = new MapId(mapIdInt);

            if (mapId == MapId.Nullspace)
            {
                shell.WriteError(Loc.GetString("cmu-cmd-znetwork-combine-nullspace"));
                return;
            }

            if (!_map.MapExists(mapId))
            {
                shell.WriteError(Loc.GetString("cmu-cmd-znetwork-combine-map-missing", ("mapId", mapId.ToString())));
                return;
            }

            if (maps.Contains(mapId))
            {
                shell.WriteError(Loc.GetString("cmu-cmd-znetwork-combine-duplicate", ("mapId", mapId.ToString())));
                return;
            }

            maps.Add(mapId);
        }

        var network = _zLevels.CreateZNetwork();
        _meta.SetEntityName(network, Loc.GetString("cmu-cmd-znetwork-combine-name", ("id", network.Owner.Id)));
        var counter = 0;
        Dictionary<EntityUid, int> dict = new();
        foreach (var findMap in maps)
        {
            dict.Add(_map.GetMap(findMap), counter);
            counter++;
        }

        var success = _zLevels.TryAddMapsIntoZNetwork(network, dict);

        if (success)
        {
            shell.WriteLine(Loc.GetString("cmu-cmd-znetwork-combine-success",
                ("network", network.ToString())));
        }
        else
        {
            shell.WriteLine(Loc.GetString("cmu-cmd-znetwork-combine-partial-failure",
                ("network", network.ToString())));
        }
    }
}
