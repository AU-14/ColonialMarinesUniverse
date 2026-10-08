using System.Numerics;
using System.Linq;
using Content.Shared.CMU14.Elevators;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Gibbing;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Server.CMU14.ZLevels.Core;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Elevators;

public sealed class CMUElevatorSystem : EntitySystem
{
    private const int MinimumRailTiles = 4;
    private const int MaximumFootprintTiles = 1024;
    private const float EntityLookupRadius = 0.75f;
    private const float ControlInteractionDistance = 2f;

    [Dependency] private DialogSystem _dialog = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private GibbingSystem _gibbing = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private CMUZLevelsSystem _zLevels = default!;

    private readonly HashSet<EntityUid> _nearby = new();
    private readonly HashSet<EntityUid> _platformEntities = new();
    private readonly HashSet<EntityUid> _crushTargets = new();
    private readonly HashSet<Vector2i> _railTiles = new();
    private readonly HashSet<Vector2i> _footprintTiles = new();
    private readonly HashSet<Vector2i> _outsideTiles = new();
    private readonly Queue<Vector2i> _floodQueue = new();
    private readonly List<ElevatorTile> _tilesToMove = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUElevatorComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<CMUElevatorComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<CMUElevatorComponent, CMUElevatorMoveConfirmedEvent>(OnMoveConfirmed);
        SubscribeLocalEvent<CMUElevatorComponent, CMUElevatorDisableConfirmedEvent>(OnDisableConfirmed);
    }

    private void OnActivate(Entity<CMUElevatorComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (ent.Comp.Disabled)
        {
            Popup(args.User, "cmu-elevator-disabled");
            return;
        }

        if (!TryBuildFootprint(ent, out _, out _))
        {
            Popup(args.User, "cmu-elevator-invalid-rail-outline");
            return;
        }

        var direction = ent.Comp.TravelDirection > 0
            ? Loc.GetString("cmu-elevator-direction-up")
            : Loc.GetString("cmu-elevator-direction-down");
        var message = Loc.GetString("cmu-elevator-move-confirmation", ("direction", direction));
        _dialog.OpenConfirmation(
            ent.Owner,
            args.User,
            Loc.GetString("cmu-elevator-title"),
            message,
            new CMUElevatorMoveConfirmedEvent(GetNetEntity(args.User)));
    }

    private void OnGetVerbs(Entity<CMUElevatorComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess ||
            !args.CanInteract ||
            !_skills.HasSkill(args.User, ent.Comp.DisableSkill, ent.Comp.DisableSkillLevel))
        {
            return;
        }

        var user = args.User;
        var disable = !ent.Comp.Disabled;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(disable ? "cmu-elevator-disable-verb" : "cmu-elevator-enable-verb"),
            Priority = 2,
            Act = () => OpenDisableConfirmation(ent, user, disable),
        });
    }

    private void OpenDisableConfirmation(Entity<CMUElevatorComponent> ent, EntityUid user, bool disabled)
    {
        if (!CanManage(ent, user))
            return;

        _dialog.OpenConfirmation(
            ent.Owner,
            user,
            Loc.GetString("cmu-elevator-title"),
            Loc.GetString(disabled ? "cmu-elevator-disable-confirmation" : "cmu-elevator-enable-confirmation"),
            new CMUElevatorDisableConfirmedEvent(GetNetEntity(user), disabled));
    }

    private void OnDisableConfirmed(Entity<CMUElevatorComponent> ent, ref CMUElevatorDisableConfirmedEvent args)
    {
        var user = GetEntity(args.User);
        if (Deleted(user) ||
            !CanManage(ent, user) ||
            !_skills.HasSkill(user, ent.Comp.DisableSkill, ent.Comp.DisableSkillLevel))
        {
            return;
        }

        ent.Comp.Disabled = args.Disabled;
        Dirty(ent);
        Popup(user, args.Disabled ? "cmu-elevator-disabled-success" : "cmu-elevator-enabled-success");
    }

    private void OnMoveConfirmed(Entity<CMUElevatorComponent> ent, ref CMUElevatorMoveConfirmedEvent args)
    {
        var user = GetEntity(args.User);
        if (Deleted(user) || !IsNearControl(ent, user))
            return;

        if (ent.Comp.Disabled)
        {
            Popup(user, "cmu-elevator-disabled");
            return;
        }

        if (!TryMovePlatform(ent, out var failure))
        {
            Popup(user, failure);
            return;
        }

        ent.Comp.TravelDirection = ent.Comp.TravelDirection > 0 ? -1 : 1;
        Dirty(ent);
    }

    private bool TryMovePlatform(Entity<CMUElevatorComponent> ent, out string failure)
    {
        failure = "cmu-elevator-invalid-rail-outline";
        if (!TryBuildFootprint(ent, out var source, out var footprint))
            return false;

        var direction = ent.Comp.TravelDirection > 0 ? 1 : -1;
        if (!_zLevels.TryMapOffset((source.MapUid, null), direction, out var targetMap, out var targetMapComp))
        {
            failure = "cmu-elevator-no-destination";
            return false;
        }

        _crushTargets.Clear();
        _tilesToMove.Clear();
        foreach (var sourceTile in footprint)
        {
            var sourceLocal = _map.GridTileToLocal(source.GridUid, source.Grid, sourceTile);
            var sourceCoordinates = _transform.ToMapCoordinates(sourceLocal);
            var targetCoordinates = new MapCoordinates(sourceCoordinates.Position, targetMapComp.MapId);
            if (!_map.TryFindGridAt(targetCoordinates, out var targetGridUid, out var targetGrid))
            {
                targetGridUid = targetMap.Value.Owner;
                if (!TryComp<MapGridComponent>(targetGridUid, out targetGrid))
                {
                    failure = "cmu-elevator-no-destination";
                    return false;
                }
            }

            var targetTile = _map.TileIndicesFor(targetGridUid, targetGrid, targetCoordinates);
            if (_map.TryGetTileRef(targetGridUid, targetGrid, targetTile, out var targetTileRef) &&
                !targetTileRef.Tile.IsEmpty)
            {
                failure = "cmu-elevator-destination-blocked";
                return false;
            }

            if (HasBlockingEntity(targetGridUid, targetGrid, targetTile, targetCoordinates))
            {
                failure = "cmu-elevator-destination-blocked";
                return false;
            }

            var sourceTileData = _map.TryGetTileRef(source.GridUid, source.Grid, sourceTile, out var sourceTileRef)
                ? sourceTileRef.Tile
                : Tile.Empty;
            _tilesToMove.Add(new ElevatorTile(
                sourceTile,
                sourceTileData,
                targetGridUid,
                targetGrid,
                targetTile));
        }

        if (!TryCollectPlatformEntities(source, footprint))
        {
            failure = "cmu-elevator-platform-grid-blocked";
            return false;
        }

        if (TryComp<CMUElevatorWeightLimitComponent>(ent, out var weightLimit) &&
            CountPlatformLoad() > weightLimit.MaxEntities)
        {
            ent.Comp.Disabled = true;
            Dirty(ent);
            failure = "cmu-elevator-overloaded";
            return false;
        }

        foreach (var target in _crushTargets)
        {
            if (!TerminatingOrDeleted(target))
                _gibbing.Gib(target);
        }

        foreach (var tile in _tilesToMove)
        {
            if (!tile.Tile.IsEmpty)
                _map.SetTile(tile.TargetGridUid, tile.TargetGrid, tile.TargetTile, tile.Tile);
        }

        foreach (var entity in _platformEntities)
        {
            if (TerminatingOrDeleted(entity))
                continue;

            var coordinates = _transform.GetMapCoordinates(entity);
            _transform.SetMapCoordinates(entity, new MapCoordinates(coordinates.Position, targetMapComp.MapId));
        }

        foreach (var tile in _tilesToMove)
            _map.SetTile(source.GridUid, source.Grid, tile.SourceTile, Tile.Empty);

        failure = string.Empty;
        return true;
    }

    private bool TryBuildFootprint(
        Entity<CMUElevatorComponent> ent,
        out ElevatorSource source,
        out HashSet<Vector2i> footprint)
    {
        source = default;
        footprint = _footprintTiles;
        _railTiles.Clear();
        _footprintTiles.Clear();
        _outsideTiles.Clear();
        _floodQueue.Clear();

        var controlXform = Transform(ent);
        if (string.IsNullOrWhiteSpace(ent.Comp.ElevatorId) ||
            controlXform.MapUid is not { } mapUid ||
            controlXform.GridUid is not { } gridUid ||
            !TryComp<MapGridComponent>(gridUid, out var grid))
        {
            return false;
        }

        var rails = EntityQueryEnumerator<CMUElevatorRailComponent, TransformComponent>();
        while (rails.MoveNext(out _, out var rail, out var railXform))
        {
            if (string.IsNullOrWhiteSpace(rail.ElevatorId) ||
                rail.ElevatorId != ent.Comp.ElevatorId ||
                railXform.MapUid != mapUid ||
                railXform.GridUid != gridUid)
            {
                continue;
            }

            _railTiles.Add(_map.TileIndicesFor(gridUid, grid, railXform.Coordinates));
        }

        if (_railTiles.Count < MinimumRailTiles || !AreRailsConnected())
        {
            return false;
        }

        var minX = _railTiles.Min(tile => tile.X);
        var minY = _railTiles.Min(tile => tile.Y);
        var maxX = _railTiles.Max(tile => tile.X);
        var maxY = _railTiles.Max(tile => tile.Y);
        var width = (long) maxX - minX + 1;
        var height = (long) maxY - minY + 1;
        if (width * height > MaximumFootprintTiles)
            return false;

        var outsideMin = new Vector2i(minX - 1, minY - 1);
        var outsideMax = new Vector2i(maxX + 1, maxY + 1);
        _outsideTiles.Add(outsideMin);
        _floodQueue.Enqueue(outsideMin);

        while (_floodQueue.TryDequeue(out var tile))
        {
            AddOutside(tile + new Vector2i(1, 0));
            AddOutside(tile + new Vector2i(-1, 0));
            AddOutside(tile + new Vector2i(0, 1));
            AddOutside(tile + new Vector2i(0, -1));
        }

        for (var x = minX; x <= maxX; x++)
        {
            for (var y = minY; y <= maxY; y++)
            {
                var tile = new Vector2i(x, y);
                if (_railTiles.Contains(tile) || !_outsideTiles.Contains(tile))
                    _footprintTiles.Add(tile);
            }
        }

        if (_footprintTiles.Count <= _railTiles.Count)
            return false;

        if (!_footprintTiles.Contains(_map.TileIndicesFor(gridUid, grid, controlXform.Coordinates)))
            return false;

        source = new ElevatorSource(mapUid, gridUid, grid);
        return true;

        void AddOutside(Vector2i tile)
        {
            if (tile.X < outsideMin.X || tile.X > outsideMax.X ||
                tile.Y < outsideMin.Y || tile.Y > outsideMax.Y ||
                _railTiles.Contains(tile) ||
                !_outsideTiles.Add(tile))
            {
                return;
            }

            _floodQueue.Enqueue(tile);
        }
    }

    private bool AreRailsConnected()
    {
        var start = _railTiles.First();
        var connected = new HashSet<Vector2i> { start };
        var queue = new Queue<Vector2i>();
        queue.Enqueue(start);

        while (queue.TryDequeue(out var tile))
        {
            AddRail(tile + new Vector2i(1, 0));
            AddRail(tile + new Vector2i(-1, 0));
            AddRail(tile + new Vector2i(0, 1));
            AddRail(tile + new Vector2i(0, -1));
        }

        return connected.Count == _railTiles.Count;

        void AddRail(Vector2i tile)
        {
            if (_railTiles.Contains(tile) && connected.Add(tile))
                queue.Enqueue(tile);
        }
    }

    private bool TryCollectPlatformEntities(ElevatorSource source, HashSet<Vector2i> footprint)
    {
        _platformEntities.Clear();
        foreach (var tile in footprint)
        {
            var localCoordinates = _map.GridTileToLocal(source.GridUid, source.Grid, tile);
            var mapCoordinates = _transform.ToMapCoordinates(localCoordinates);
            _nearby.Clear();
            _lookup.GetEntitiesInRange(mapCoordinates.MapId, mapCoordinates.Position, EntityLookupRadius, _nearby, LookupFlags.All);

            foreach (var entity in _nearby)
            {
                if (entity == source.MapUid || entity == source.GridUid || TerminatingOrDeleted(entity))
                    continue;

                var xform = Transform(entity);
                if (xform.MapUid != source.MapUid || xform.GridUid != source.GridUid)
                    continue;

                var entityTile = _map.TileIndicesFor(source.GridUid, source.Grid, xform.Coordinates);
                if (!footprint.Contains(entityTile))
                    continue;

                if (HasComp<MapGridComponent>(entity))
                    return false;

                _platformEntities.Add(entity);
            }
        }

        return true;
    }

    private int CountPlatformLoad()
    {
        var count = 0;
        foreach (var entity in _platformEntities)
        {
            if (HasComp<CMUElevatorComponent>(entity) ||
                HasComp<CMUElevatorRailComponent>(entity))
            {
                continue;
            }

            count++;
        }

        return count;
    }

    private bool HasBlockingEntity(EntityUid gridUid, MapGridComponent grid, Vector2i tile, MapCoordinates coordinates)
    {
        _nearby.Clear();
        _lookup.GetEntitiesInRange(coordinates.MapId, coordinates.Position, EntityLookupRadius, _nearby, LookupFlags.Uncontained);
        foreach (var entity in _nearby)
        {
            if (HasComp<MapComponent>(entity) ||
                HasComp<MapGridComponent>(entity) ||
                TerminatingOrDeleted(entity))
                continue;

            var xform = Transform(entity);
            if (xform.MapID != coordinates.MapId)
                continue;

            var entityCoordinates = _transform.GetMapCoordinates(entity);
            if (_map.TileIndicesFor(gridUid, grid, entityCoordinates) != tile)
                continue;

            if (HasComp<MobStateComponent>(entity))
            {
                _crushTargets.Add(entity);
                continue;
            }

            return true;
        }

        return false;
    }

    private bool CanManage(Entity<CMUElevatorComponent> ent, EntityUid user)
    {
        return !Deleted(user) &&
               IsNearControl(ent, user) &&
               _skills.HasSkill(user, ent.Comp.DisableSkill, ent.Comp.DisableSkillLevel);
    }

    private bool IsNearControl(Entity<CMUElevatorComponent> ent, EntityUid user)
    {
        var controlCoordinates = _transform.GetMapCoordinates(ent.Owner);
        var userCoordinates = _transform.GetMapCoordinates(user);
        return controlCoordinates.MapId == userCoordinates.MapId &&
               Vector2.DistanceSquared(controlCoordinates.Position, userCoordinates.Position) <=
               ControlInteractionDistance * ControlInteractionDistance;
    }

    private void Popup(EntityUid user, string message)
    {
        _popup.PopupEntity(Loc.GetString(message), user, user, PopupType.SmallCaution);
    }

    private readonly record struct ElevatorSource(EntityUid MapUid, EntityUid GridUid, MapGridComponent Grid);

    private readonly record struct ElevatorTile(
        Vector2i SourceTile,
        Tile Tile,
        EntityUid TargetGridUid,
        MapGridComponent TargetGrid,
        Vector2i TargetTile);
}
