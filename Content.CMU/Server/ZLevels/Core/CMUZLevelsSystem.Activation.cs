using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared.Ghost;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;

namespace Content.Server._CMU14.ZLevels.Core;

public sealed partial class CMUZLevelsSystem
{
    [Dependency] private EntityLookupSystem _entityLookup = default!;

    private readonly HashSet<EntityUid> _zFallWakeBuffer = new();

    private void InitializeActivation()
    {
        SubscribeLocalEvent<CMUZPhysicsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMUZPhysicsComponent, AnchorStateChangedEvent>(OnAnchorStateChange);
        SubscribeLocalEvent<CMUZPhysicsComponent, PhysicsBodyTypeChangedEvent>(OnPhysicsBodyTypeChange);
        SubscribeLocalEvent<CMUZPhysicsComponent, MoveEvent>(OnZPhysicsMove);
    }

    private void OnAnchorStateChange(Entity<CMUZPhysicsComponent> ent, ref AnchorStateChangedEvent args)
    {
        CheckActivation(ent);
    }

    private void OnMapInit(Entity<CMUZPhysicsComponent> ent, ref MapInitEvent args)
    {
        CheckActivation(ent);
    }

    private void OnPhysicsBodyTypeChange(Entity<CMUZPhysicsComponent> ent, ref PhysicsBodyTypeChangedEvent args)
    {
        CheckActivation(ent);
    }

    private void OnZPhysicsMove(Entity<CMUZPhysicsComponent> ent, ref MoveEvent args)
    {
        OnZPhysicsMoveGroundSnap(ent, ref args);

        if (!TryGetFallCheckTile(ent, out var map, out var tile))
            return;

        if (ent.Comp.LastFallCheckMap == map &&
            ent.Comp.LastFallCheckTile == tile)
        {
            if (!HasHighGroundAtTile(map, tile))
                return;
        }

        ent.Comp.LastFallCheckMap = map;
        ent.Comp.LastFallCheckTile = tile;
        CheckActivation(ent);
    }

    private void OnZPhysicsTileChanged(ref TileChangedEvent args)
    {
        if (!_zLevelsEnabled)
            return;

        for (var i = 0; i < args.Changes.Length; i++)
        {
            ref readonly var change = ref args.Changes[i];
            if (!change.EmptyChanged)
                continue;

            WakeZPhysicsAtTile(args.Entity, change.GridIndices);
        }
    }

    private void CheckActivation(Entity<CMUZPhysicsComponent> ent)
    {
        if (!CanUseZPhysics(ent))
        {
            SetActiveStatus(ent, false);
            return;
        }

        SetActiveStatus(ent, true);
    }

    private bool CanUseZPhysics(Entity<CMUZPhysicsComponent> ent)
    {
        if (!_zLevelsEnabled ||
            TerminatingOrDeleted(ent))
        {
            return false;
        }

        if (HasComp<GhostComponent>(ent))
            return false;

        var xform = Transform(ent);
        if (xform.MapUid is not { } map ||
            !HasComp<CMUZLevelMapComponent>(map) ||
            xform.Anchored)
        {
            return false;
        }

        if (TryComp<PhysicsComponent>(ent, out var physics))
        {
            if (physics.BodyType == BodyType.Static)
                return false;
        }

        return true;
    }

    private void SetActiveStatus(EntityUid ent, bool active)
    {
        if (active)
            WakeZPhysics(ent);
        else
        {
            RemCompDeferred<CMUZFallingComponent>(ent);
        }
    }

    private bool TryGetFallCheckTile(Entity<CMUZPhysicsComponent> ent, out EntityUid map, out Vector2i tile)
    {
        map = default;
        tile = default;

        var xform = Transform(ent);
        if (xform.MapUid is not { } mapUid ||
            !TryComp<MapGridComponent>(mapUid, out var grid))
        {
            return false;
        }

        map = mapUid;
        tile = _map.TileIndicesFor(mapUid, grid, new MapCoordinates(_transform.GetWorldPosition(ent), xform.MapID));
        return true;
    }

    private bool HasHighGroundAtTile(EntityUid mapUid, Vector2i tile)
    {
        if (!TryComp<MapGridComponent>(mapUid, out var grid))
            return false;

        var query = _map.GetAnchoredEntitiesEnumerator(mapUid, grid, tile);
        while (query.MoveNext(out var uid))
        {
            if (HasComp<CMUZLevelHighGroundComponent>(uid))
                return true;
        }

        return false;
    }

    private void WakeZPhysicsAtTile(Entity<MapGridComponent> grid, Vector2i tile)
    {
        var coordinates = _map.GridTileToWorld(grid.Owner, grid.Comp, tile);

        _zFallWakeBuffer.Clear();
        _entityLookup.GetEntitiesInRange(
            coordinates.MapId,
            coordinates.Position,
            0.75f,
            _zFallWakeBuffer,
            LookupFlags.Dynamic | LookupFlags.Sundries | LookupFlags.Uncontained);

        foreach (var uid in _zFallWakeBuffer)
        {
            if (!TryComp<CMUZPhysicsComponent>(uid, out var zPhys))
                continue;

            WakeZPhysics((uid, zPhys));
        }

        _zFallWakeBuffer.Clear();
    }
}
