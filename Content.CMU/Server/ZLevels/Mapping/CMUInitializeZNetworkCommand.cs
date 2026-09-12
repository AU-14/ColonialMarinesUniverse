using Content.Server.Administration;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Administration;
using Robust.Server.GameObjects;
using Robust.Shared.Console;
using Robust.Shared.Map.Components;

namespace Content.Server.CMU14.ZLevels.Mapping;

[AdminCommand(AdminFlags.Server | AdminFlags.Mapping)]
public sealed partial class CMUInitializeZNetworkCommand : LocalizedEntityCommands
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private MapSystem _map = default!;

    public override string Command => "znetwork-initialize";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        var options = new List<CompletionOption>();
        var query = _entities.EntityQueryEnumerator<CMUZLevelsNetworkComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out _, out var meta))
        {
            options.Add(new CompletionOption(_entities.GetNetEntity(uid).ToString(), meta.EntityName));
        }

        return CompletionResult.FromHintOptions(options, Loc.GetString("cmu-cmd-znetwork-entity-hint"));
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        EntityUid? target;
        if (!NetEntity.TryParse(args[0], out var targetNet) ||
            !_entities.TryGetEntity(targetNet, out target))
        {
            shell.WriteError(Loc.GetString("cmu-cmd-znetwork-entity-missing", ("entity", args[0])));
            return;
        }

        if (!_entities.TryGetComponent<CMUZLevelsNetworkComponent>(target, out var levelComp))
        {
            shell.WriteError(Loc.GetString("cmu-cmd-znetwork-component-missing", ("entity", args[0])));
            return;
        }

        foreach (var (_, mapUid) in levelComp.ZLevels)
        {
            if (!_entities.TryGetComponent<MapComponent>(mapUid, out var mapComp))
            {
                shell.WriteError(Loc.GetString("cmu-cmd-znetwork-initialize-map-component-missing",
                    ("map", mapUid.ToString())));
                continue;
            }

            if (!_map.MapExists(mapComp.MapId))
            {
                shell.WriteError(Loc.GetString("cmu-cmd-znetwork-initialize-map-missing",
                    ("mapId", mapComp.MapId.ToString())));
                continue;
            }

            if (_map.IsInitialized(mapComp.MapId))
            {
                shell.WriteLine(Loc.GetString("cmu-cmd-znetwork-initialize-already",
                    ("mapId", mapComp.MapId.ToString())));
                continue;
            }

            _map.InitializeMap(mapComp.MapId);
            shell.WriteLine(Loc.GetString("cmu-cmd-znetwork-initialize-success",
                ("mapId", mapComp.MapId.ToString())));
        }
    }
}
