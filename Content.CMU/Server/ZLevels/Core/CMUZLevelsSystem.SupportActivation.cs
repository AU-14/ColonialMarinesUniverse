using Content.Server.CMU14.ZLevelBuilding;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Robust.Shared.Map.Components;

namespace Content.Server.CMU14.ZLevels.Core;

public sealed partial class CMUZLevelsSystem
{
    private readonly HashSet<(EntityUid Map, Box2 Bounds)> _pendingSupportWakeRegions = new();
    private readonly List<(EntityUid Map, Box2 Bounds)> _supportWakeRegions = new();
    private readonly HashSet<EntityUid> _supportWakeEntities = new();

    private void InitializeSupportActivation()
    {
        SubscribeLocalEvent<ZLevelSupportChangedEvent>(OnWallSupportChanged);
        SubscribeLocalEvent<CMUZLevelHighGroundComponent, ComponentShutdown>(OnHighGroundShutdown);
        SubscribeLocalEvent<CMUZLevelHighGroundComponent, AnchorStateChangedEvent>(OnHighGroundAnchorChanged);
        SubscribeLocalEvent<Content.Shared.GameTicking.RoundRestartCleanupEvent>(_ =>
        {
            _pendingSupportWakeRegions.Clear();
            _supportWakeRegions.Clear();
            _supportWakeEntities.Clear();
        });
    }

    private void OnWallSupportChanged(ref ZLevelSupportChangedEvent args)
        => QueueSupportWake(args.Support);

    private void OnHighGroundShutdown(Entity<CMUZLevelHighGroundComponent> ent, ref ComponentShutdown args)
    {
        if (Transform(ent).Anchored)
            QueueSupportWake(ent.Owner);
    }

    private void OnHighGroundAnchorChanged(Entity<CMUZLevelHighGroundComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored)
            QueueSupportWake(ent.Owner);
    }

    private void QueueSupportWake(EntityUid support)
    {
        if (!_zLevelsEnabled ||
            !TryComp(support, out TransformComponent? xform) ||
            xform.MapUid is not { } map ||
            xform.GridUid is not { } gridUid ||
            !TryComp<MapGridComponent>(gridUid, out var grid) ||
            !TryComp<CMUZLevelMapComponent>(map, out var level))
        {
            return;
        }

        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var localBounds = new Box2(tile * grid.TileSize, (tile + Vector2i.One) * grid.TileSize);
        var bounds = _transform.GetWorldMatrix(gridUid).TransformBox(localBounds.Enlarged(HighGroundEdgeSupport));
        _pendingSupportWakeRegions.Add((map, bounds));
        if (level.MapAbove is { } above)
            _pendingSupportWakeRegions.Add((above, bounds));
    }

    private void UpdateSupportActivation()
    {
        if (_pendingSupportWakeRegions.Count == 0)
            return;

        // Lifecycle handlers only capture locations. Query and wake after component/anchor teardown has
        // completed, so DistanceToGround cannot put the body back to sleep on the disappearing support.
        _supportWakeRegions.Clear();
        _supportWakeRegions.AddRange(_pendingSupportWakeRegions);
        _pendingSupportWakeRegions.Clear();
        _supportWakeEntities.Clear();
        foreach (var (map, bounds) in _supportWakeRegions)
        {
            if (TerminatingOrDeleted(map))
                continue;

            // Query the map broadphase by fixture intersection, including sleeping dynamic bodies. A large
            // vehicle whose edge lost support is relevant even when its origin is outside the changed tile.
            _entityLookup.GetEntitiesIntersecting(map, bounds, _supportWakeEntities,
                LookupFlags.Dynamic | LookupFlags.Sundries | LookupFlags.Uncontained);
        }

        foreach (var uid in _supportWakeEntities)
        {
            if (!TerminatingOrDeleted(uid) && TryComp<CMUZPhysicsComponent>(uid, out var physics))
                WakeZPhysics((uid, physics));
        }

        _supportWakeEntities.Clear();
        _supportWakeRegions.Clear();
    }
}
