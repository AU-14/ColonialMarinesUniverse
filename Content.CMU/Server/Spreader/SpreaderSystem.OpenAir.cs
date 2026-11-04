using System.Numerics;
using Content.Shared.Atmos;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Spreader;
using Robust.Shared.Collections;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Spreader;

public sealed partial class SpreaderSystem
{
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    // CMU14 method: generated open-air levels do not necessarily have a terrain grid.
    private void OnSpreaderMapInit(Entity<EdgeSpreaderComponent> ent, ref MapInitEvent args)
    {
        var xform = Transform(ent);
        if (ProtoMan.Resolve(ent.Comp.Id, out var proto) && CanSpreadOnOpenAir(xform, proto))
            EnsureComp<SpreaderGridComponent>(xform.MapUid!.Value);
    }

    // CMU14 methods
    private bool CanSpreadOnOpenAir(TransformComponent xform, EdgeSpreaderPrototype prototype)
        => prototype.SpreadOnOpenAir && xform.MapUid is { } map && _zLevels.TryGetZNetwork(map, out _);

    private EntityUid? GetSpreadingGrid(TransformComponent xform, ProtoId<EdgeSpreaderPrototype> prototype)
    {
        if (xform.GridUid is { } grid) return grid;
        if (ProtoMan.Resolve(prototype, out var proto) && CanSpreadOnOpenAir(xform, proto))
            return xform.MapUid;
        return null;
    }

    private ValueList<EntityCoordinates> GetAirNeighbors(TransformComponent xform)
    {
        ValueList<EntityCoordinates> result = [];
        if (xform.MapUid is not { } map) return result;
        var position = _transform.GetWorldPosition(xform);
        var center = new Vector2(MathF.Floor(position.X) + 0.5f, MathF.Floor(position.Y) + 0.5f);
        var blocked = GetAirBlockedDirections(map, center);
        for (var i = 0; i < 4; i++)
        {
            var direction = (AtmosDirection) (1 << i);
            var neighbor = center + (Vector2) Vector2i.Zero.Offset(direction);
            if ((blocked & direction) != 0 ||
                (GetAirBlockedDirections(map, neighbor) & i.ToOppositeDir()) != 0)
                continue;
            result.Add(new EntityCoordinates(map, neighbor));
        }
        return result;
    }

    private AtmosDirection GetAirBlockedDirections(EntityUid map, Vector2 position)
    {
        if (!_map.TryFindGridAt(map, position, out var gridUid, out var grid))
            return AtmosDirection.Invalid;
        var tile = _map.WorldToTile(gridUid, grid, position);
        var anchored = _map.GetAnchoredEntities(gridUid, grid, tile);
        var blocked = AtmosDirection.Invalid;
        while (anchored.MoveNext(out var uid))
        {
            if (_airtightQuery.TryComp(uid, out var airtight) && airtight.AirBlocked && !_tag.HasTag(uid.Value, IgnoredTag))
                blocked |= airtight.AirBlockedDirection;
        }
        return blocked;
    }

}
