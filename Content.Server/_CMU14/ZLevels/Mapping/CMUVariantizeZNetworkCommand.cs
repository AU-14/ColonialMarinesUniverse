using Content.Server.Administration;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared.Administration;
using Content.Shared.Maps;
using Robust.Server.GameObjects;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._CMU14.ZLevels.Mapping;

[AdminCommand(AdminFlags.Server | AdminFlags.Mapping)]
public sealed partial class CMUVariantizeZNetworkCommand : LocalizedEntityCommands
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private TileSystem _tile = default!;
    [Dependency] private TurfSystem _turf = default!;

    public override string Command => "znetwork-variantize";
    public override string Description => "Random tile variations over all zNetwork maps";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        var options = new List<CompletionOption>();
        var query = _entities.EntityQueryEnumerator<CMUZLevelsNetworkComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out _, out var meta))
        {
            options.Add(new CompletionOption(_entities.GetNetEntity(uid).ToString(), meta.EntityName));
        }

        return CompletionResult.FromHintOptions(options, "zNetwork net entity");
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        // get the target
        EntityUid? target;

        if (!NetEntity.TryParse(args[0], out var targetNet) ||
            !_entities.TryGetEntity(targetNet, out target))
        {
            shell.WriteError($"Unable to find entity {args[0]}");
            return;
        }

        if (!_entities.TryGetComponent<CMUZLevelsNetworkComponent>(target, out var levelComp))
        {
            shell.WriteError($"Target entity doesnt have CMUZLevelsNetworkComponent {args[0]}");
            return;
        }

        foreach (var (_, mapUid) in levelComp.ZLevels)
        {
            if (!_entities.TryGetComponent<MapGridComponent>(mapUid, out var gridComp))
            {
                shell.WriteError($"Euid '{mapUid}' does not exist or is not a grid.");
                continue;
            }

            foreach (var tile in _map.GetAllTiles(mapUid.Value, gridComp))
            {
                var def = _turf.GetContentTileDefinition(tile);
                var newTile = new Tile(tile.Tile.TypeId, tile.Tile.Flags, _tile.PickVariant(def), tile.Tile.RotationMirroring);
                _map.SetTile(mapUid.Value, gridComp, tile.GridIndices, newTile);
            }
        }
    }
}
