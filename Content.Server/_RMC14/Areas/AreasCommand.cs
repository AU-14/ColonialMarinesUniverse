using Content.Server.Administration;
using Content.Shared._RMC14.Areas;
using Content.Shared.Administration;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;

namespace Content.Server._RMC14.Areas;

[ToolshedCommand, AdminCommand(AdminFlags.Host)]
public sealed partial class AreasCommand : ToolshedCommand
{
    [Dependency] private IComponentFactory _compFactory = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private MapSystem? _map;

    [CommandImplementation("save")]
    public void Save([CommandInvocationContext] IInvocationContext ctx)
    {
        _map = GetSys<MapSystem>();

        var gridQuery = GetEntityQuery<MapGridComponent>();

        var query = EntityManager.AllEntityQueryEnumerator<AreaComponent, MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var metaData, out var xform))
        {
            if (xform.GridUid is not { } gridId ||
                !gridQuery.TryComp(gridId, out var grid))
            {
                continue;
            }

            var areaGrid = EnsureComp<AreaGridComponent>(gridId);
            if (metaData.EntityPrototype is not { } prototype)
            {
                ctx.WriteLine($"{EntityManager.ToPrettyString(uid)} did not have a prototype.");
                continue;
            }

            var indices = _map.TileIndicesFor(gridId, grid, xform.Coordinates);
            var areas = areaGrid.Areas;
            areas[indices] = prototype.ID;
            QDel(uid);
        }
    }

    [CommandImplementation("load")]
    public void Load()
    {
        Load(_ => true);
    }

    [CommandImplementation("loadmortar")]
    public void LoadMortar()
    {
        Load(a => a.MortarFire);
    }

    [CommandImplementation("cleanup")]
    public void Cleanup([CommandInvocationContext] IInvocationContext ctx)
    {
        _map = GetSys<MapSystem>();

        var areaSystem = GetSys<AreaSystem>();
        var gridQuery = GetEntityQuery<MapGridComponent>();
        var areaQuery = GetEntityQuery<AreaComponent>();
        var emptyAreaTiles = new List<(EntityUid GridId,
            MapGridComponent Grid,
            TransformComponent GridXform,
            HashSet<Vector2i> Indices)>();
        var removedSavedAreas = 0;

        var areaGridQuery = EntityManager.AllEntityQueryEnumerator<AreaGridComponent, MapGridComponent, TransformComponent>();
        while (areaGridQuery.MoveNext(out var gridId, out var areaGrid, out var grid, out var gridXform))
        {
            var emptyTiles = new HashSet<Vector2i>();
            foreach (var indices in new List<Vector2i>(areaGrid.Areas.Keys))
            {
                if (!_map.GetTileRef(gridId, grid, indices).Tile.IsEmpty)
                    continue;

                emptyTiles.Add(indices);
                areaSystem.RemoveArea(areaGrid, indices);
                removedSavedAreas++;
            }

            if (emptyTiles.Count == 0)
                continue;

            emptyAreaTiles.Add((gridId, grid, gridXform, emptyTiles));
            EntityManager.Dirty(gridId, areaGrid);
        }

        var transformSystem = GetSys<SharedTransformSystem>();
        var toDelete = new List<EntityUid>();
        var removedAreaEntities = 0;

        var entityQuery = EntityManager.AllEntityQueryEnumerator<TransformComponent>();
        while (entityQuery.MoveNext(out var uid, out var xform))
        {
            if (uid == xform.MapUid || uid == xform.GridUid)
                continue;

            var overEmptyTile = false;
            if (xform.GridUid is { } gridId)
            {
                if (!gridQuery.TryComp(gridId, out var grid) ||
                    !_map.GetTileRef(gridId, grid, xform.Coordinates).Tile.IsEmpty)
                {
                    continue;
                }

                overEmptyTile = true;
            }
            else if (emptyAreaTiles.Count > 0)
            {
                var mapCoordinates = transformSystem.ToMapCoordinates(xform.Coordinates);
                foreach (var emptyAreaGrid in emptyAreaTiles)
                {
                    if (mapCoordinates.MapId != emptyAreaGrid.GridXform.MapID ||
                        !emptyAreaGrid.Indices.Contains(_map.WorldToTile(emptyAreaGrid.GridId,
                            emptyAreaGrid.Grid,
                            mapCoordinates.Position)))
                    {
                        continue;
                    }

                    overEmptyTile = true;
                    break;
                }
            }

            if (!overEmptyTile)
                continue;

            toDelete.Add(uid);
            if (areaQuery.HasComp(uid))
                removedAreaEntities++;
        }

        foreach (var uid in toDelete)
        {
            QDel(uid);
        }

        ctx.WriteLine($"Removed {toDelete.Count} entities ({removedAreaEntities} areas) and " +
                      $"{removedSavedAreas} saved areas from empty tiles.");
    }

    private void Load(Predicate<AreaComponent> predicate)
    {
        _map = GetSys<MapSystem>();

        var query = EntityManager.AllEntityQueryEnumerator<AreaGridComponent, MapGridComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var areas, out var mapGrid, out var xform))
        {
            foreach (var (position, protoId) in areas.Areas)
            {
                if (!_prototypes.TryIndex(protoId, out var proto))
                    continue;

                if (!proto.TryComp(out AreaComponent? areaComp, _compFactory))
                    continue;

                if (!predicate(areaComp))
                    continue;

                var coordinates = _map.ToCoordinates(uid, position, mapGrid);
                Spawn(protoId, coordinates);
            }
        }
    }
}
