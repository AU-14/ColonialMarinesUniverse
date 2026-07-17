using System.Numerics;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared.Ghost;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._CMU14.ZLevels.Core;

public sealed partial class CMUZLevelStairsSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private CMUZLevelsSystem _zLevels = default!;

    private EntityQuery<CMUZLevelStairsComponent> _stairsQuery;
    private EntityQuery<MapGridComponent> _gridQuery;
    private readonly HashSet<EntityUid> _moving = new();

    public override void Initialize()
    {
        base.Initialize();

        _stairsQuery = GetEntityQuery<CMUZLevelStairsComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _transform.OnGlobalMoveEvent += OnGlobalMove;

        SubscribeLocalEvent<CMUZLevelStairsComponent, ActivateInWorldEvent>(OnActivate);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _transform.OnGlobalMoveEvent -= OnGlobalMove;
    }

    private void OnGlobalMove(ref MoveEvent ev)
    {
        if (_moving.Contains(ev.Sender) ||
            !_gridQuery.TryGetComponent(ev.OldPosition.EntityId, out var oldGrid) ||
            !_gridQuery.TryGetComponent(ev.NewPosition.EntityId, out var newGrid) ||
            ev.OldPosition.EntityId != ev.NewPosition.EntityId)
        {
            return;
        }

        if (!HasComp<MobStateComponent>(ev.Sender) &&
            !HasComp<GhostComponent>(ev.Sender))
            return;

        var oldTile = _map.LocalToTile(ev.OldPosition.EntityId, oldGrid, ev.OldPosition);
        var newTile = _map.LocalToTile(ev.NewPosition.EntityId, newGrid, ev.NewPosition);
        var delta = newTile - oldTile;
        if (delta == Vector2i.Zero)
            return;

        var oldUserCoordinates = _transform.ToMapCoordinates(ev.OldPosition);
        if (TryMoveAcrossZFromTile(ev.OldPosition.EntityId, oldGrid, oldTile, delta, ev.Sender, oldUserCoordinates))
            return;

        TryMoveAcrossZFromTile(ev.NewPosition.EntityId, newGrid, newTile, delta, ev.Sender, oldUserCoordinates);
    }

    private bool TryMoveAcrossZFromTile(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile,
        Vector2i delta,
        EntityUid user,
        MapCoordinates oldUserCoordinates)
    {
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var anchoredUid))
        {
            if (!_stairsQuery.TryComp(anchoredUid, out var stairs))
                continue;

            var requiredDelta = RequiredDelta(stairs);
            if (delta != requiredDelta)
                continue;

            var stairPosition = _transform.GetWorldPosition(anchoredUid.Value);
            MoveAcrossZ((anchoredUid.Value, stairs), user, stairPosition + new Vector2(requiredDelta.X, requiredDelta.Y), oldUserCoordinates);
            return true;
        }

        return false;
    }

    private void OnActivate(Entity<CMUZLevelStairsComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !HasComp<GhostComponent>(args.User))
            return;

        args.Handled = true;

        var delta = RequiredDelta(ent.Comp);
        var landingWorldPosition = _transform.GetWorldPosition(ent) + new Vector2(delta.X, delta.Y);
        MoveAcrossZ(ent, args.User, landingWorldPosition, null);
    }

    private void MoveAcrossZ(
        Entity<CMUZLevelStairsComponent> stairs,
        EntityUid user,
        Vector2 landingWorldPosition,
        MapCoordinates? oldUserCoordinatesOverride)
    {
        var map = Transform(stairs).MapUid;
        if (map is null ||
            !_zLevels.TryProjectToZMap(map.Value, stairs.Comp.Offset, landingWorldPosition, out var targetCoordinates, out _))
        {
            _popup.PopupClient(Loc.GetString("cmu-zlevel-stairs-no-level"), stairs, user, PopupType.SmallCaution);
            return;
        }

        var oldUserCoordinates = oldUserCoordinatesOverride ?? _transform.GetMapCoordinates(user);
        var hasPulled = _zLevels.TryPreparePulledEntityZMove(user, oldUserCoordinates, out var pulledMove);
        _moving.Add(user);
        try
        {
            _transform.SetMapCoordinates(user, targetCoordinates);
        }
        finally
        {
            _moving.Remove(user);
        }

        if (hasPulled)
            _zLevels.MovePulledEntityAcrossZ(pulledMove, targetCoordinates, stairs.Comp.LandingLocalPosition);

        if (TryComp<CMUZPhysicsComponent>(user, out var zPhysics))
        {
            _zLevels.SetZVelocity((user, zPhysics), 0f);
            _zLevels.SetZLocalPosition((user, zPhysics), stairs.Comp.LandingLocalPosition);
        }
    }

    private static Vector2i RequiredDelta(CMUZLevelStairsComponent stairs)
    {
        var delta = DirectionDelta(stairs.Direction);
        return stairs.Offset >= 0 ? delta : -delta;
    }

    private static Vector2i DirectionDelta(Direction direction)
    {
        return direction switch
        {
            Direction.North => new Vector2i(0, 1),
            Direction.South => new Vector2i(0, -1),
            Direction.East => new Vector2i(1, 0),
            Direction.West => new Vector2i(-1, 0),
            Direction.NorthEast => new Vector2i(1, 1),
            Direction.NorthWest => new Vector2i(-1, 1),
            Direction.SouthEast => new Vector2i(1, -1),
            Direction.SouthWest => new Vector2i(-1, -1),
            _ => Vector2i.Zero,
        };
    }
}
