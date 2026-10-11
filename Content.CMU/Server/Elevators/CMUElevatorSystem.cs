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
using Content.Shared.CMU14.ZLevels.Core.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Elevators;

public sealed class CMUElevatorSystem : EntitySystem
{
    private const int MaximumFootprintTiles = 1024;
    private const float EntityLookupRadius = 0.75f;
    private const float ControlInteractionDistance = 2f;
    private const string ZLevelEyePrototype = "CMUZLevelEye";

    private static readonly Vector2i[] CardinalOffsets =
    {
        new(1, 0),
        new(-1, 0),
        new(0, 1),
        new(0, -1),
    };

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
    private readonly HashSet<(Vector2i, Vector2i)> _blockedEdges = new();
    private readonly HashSet<Vector2i> _footprintTiles = new();
    private readonly HashSet<Vector2i> _solidTiles = new();
    private readonly Queue<Vector2i> _floodQueue = new();
    private readonly List<ElevatorTile> _tilesToMove = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUElevatorComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<CMUElevatorComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<CMUElevatorComponent, CMUElevatorFloorSelectedEvent>(OnFloorSelected);
        SubscribeLocalEvent<CMUElevatorComponent, CMUElevatorDisableConfirmedEvent>(OnDisableConfirmed);
        SubscribeLocalEvent<CMUElevatorCallButtonComponent, ActivateInWorldEvent>(OnCallButtonActivate);
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

        if (!TryBuildFootprint(ent, out var source, out _, out var outlineFailure))
        {
            Popup(args.User, outlineFailure);
            return;
        }

        if (!_zLevels.TryGetZNetwork(source.MapUid, out var network))
        {
            Popup(args.User, "cmu-elevator-no-destination");
            return;
        }

        var options = new List<DialogOption>();
        var currentFloor = 0;
        var floors = _zLevels.GetOrderedNetworkMaps(network.Value);
        for (var i = 0; i < floors.Count; i++)
        {
            var map = floors[i].Map;
            if (map == source.MapUid)
            {
                currentFloor = i + 1;
                continue;
            }

            options.Add(new DialogOption(
                Loc.GetString("cmu-elevator-floor", ("floor", i + 1)),
                new CMUElevatorFloorSelectedEvent(GetNetEntity(args.User), GetNetEntity(map))));
        }

        if (options.Count == 0)
        {
            Popup(args.User, "cmu-elevator-no-destination");
            return;
        }

        _dialog.OpenOptions(
            ent.Owner,
            args.User,
            Loc.GetString("cmu-elevator-title"),
            options,
            Loc.GetString("cmu-elevator-select-floor", ("floor", currentFloor)));
    }

    private void OnCallButtonActivate(Entity<CMUElevatorCallButtonComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (Transform(ent).MapUid is not { } floor ||
            !TryFindElevator(ent.Comp.ElevatorId, floor, out var elevator))
        {
            Popup(args.User, "cmu-elevator-call-no-elevator");
            return;
        }

        if (Transform(elevator).MapUid == floor)
        {
            Popup(args.User, "cmu-elevator-call-already-here");
            return;
        }

        if (elevator.Comp.Disabled)
        {
            Popup(args.User, "cmu-elevator-disabled");
            return;
        }

        if (!TryMovePlatform(elevator, floor, out var failure))
        {
            Popup(args.User, failure);
            return;
        }

        _popup.PopupEntity(Loc.GetString("cmu-elevator-call-arrived"), ent, args.User);
    }

    private bool TryFindElevator(string elevatorId, EntityUid floor, out Entity<CMUElevatorComponent> elevator)
    {
        elevator = default;
        if (string.IsNullOrWhiteSpace(elevatorId))
            return false;

        var query = EntityQueryEnumerator<CMUElevatorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (comp.ElevatorId != elevatorId || !_zLevels.IsSameZNetwork(xform.MapUid, floor))
                continue;

            elevator = (uid, comp);
            return true;
        }

        return false;
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

    private void OnFloorSelected(Entity<CMUElevatorComponent> ent, ref CMUElevatorFloorSelectedEvent args)
    {
        var user = GetEntity(args.User);
        if (Deleted(user) || !IsNearControl(ent, user))
            return;

        if (ent.Comp.Disabled)
        {
            Popup(user, "cmu-elevator-disabled");
            return;
        }

        if (!TryGetEntity(args.TargetMap, out var targetMap))
        {
            Popup(user, "cmu-elevator-no-destination");
            return;
        }

        if (!TryMovePlatform(ent, targetMap.Value, out var failure))
            Popup(user, failure);
    }

    private bool TryMovePlatform(Entity<CMUElevatorComponent> ent, EntityUid targetMap, out string failure)
    {
        if (!TryBuildFootprint(ent, out var source, out var footprint, out failure))
            return false;

        if (targetMap == source.MapUid)
        {
            failure = "cmu-elevator-call-already-here";
            return false;
        }

        if (!_zLevels.TryGetZNetwork(source.MapUid, out var network) ||
            !TryComp<CMUZLevelMapComponent>(source.MapUid, out var sourceLevel) ||
            !TryComp<CMUZLevelMapComponent>(targetMap, out var targetLevel) ||
            !_zLevels.IsMapInNetwork(network.Value, targetMap) ||
            !TryComp<MapComponent>(targetMap, out var targetMapComp))
        {
            failure = "cmu-elevator-no-destination";
            return false;
        }

        _crushTargets.Clear();
        _tilesToMove.Clear();

        // The platform passes through every level between here and the destination, so the whole shaft must be open.
        var minDepth = Math.Min(sourceLevel.Depth, targetLevel.Depth);
        var maxDepth = Math.Max(sourceLevel.Depth, targetLevel.Depth);
        foreach (var (depth, map) in _zLevels.GetOrderedNetworkMaps(network.Value))
        {
            if (depth <= minDepth || depth >= maxDepth || !TryComp<MapComponent>(map, out var shaftMap))
                continue;

            foreach (var sourceTile in footprint)
            {
                if (!TryCheckShaftTile(source, sourceTile, map, shaftMap, false, out failure))
                    return false;
            }
        }

        foreach (var sourceTile in footprint)
        {
            if (!TryCheckShaftTile(source, sourceTile, targetMap, targetMapComp, true, out failure))
                return false;
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

            var xform = Transform(entity);
            var wasAnchored = xform.Anchored;
            var coordinates = _transform.GetMapCoordinates(entity);
            _transform.SetMapCoordinates(entity, new MapCoordinates(coordinates.Position, targetMapComp.MapId));
            if (wasAnchored && !_transform.AnchorEntity((entity, xform)))
                Log.Error($"Failed to re-anchor elevator structure {ToPrettyString(entity)} at its destination.");
        }

        foreach (var tile in _tilesToMove)
            _map.SetTile(source.GridUid, source.Grid, tile.SourceTile, Tile.Empty);

        failure = string.Empty;
        return true;
    }

    /// <summary>
    /// Checks that the level has open space under/over one platform tile. Mobs in the way are crushed.
    /// The destination must have a grid to land on; intermediate levels may be open space.
    /// </summary>
    private bool TryCheckShaftTile(
        ElevatorSource source,
        Vector2i sourceTile,
        EntityUid levelMap,
        MapComponent levelMapComp,
        bool destination,
        out string failure)
    {
        var sourceLocal = _map.GridTileToLocal(source.GridUid, source.Grid, sourceTile);
        var sourceCoordinates = _transform.ToMapCoordinates(sourceLocal);
        var levelCoordinates = new MapCoordinates(sourceCoordinates.Position, levelMapComp.MapId);
        if (!_map.TryFindGridAt(levelCoordinates, out var gridUid, out var grid))
        {
            gridUid = levelMap;
            if (!TryComp(gridUid, out grid))
            {
                failure = destination ? "cmu-elevator-no-destination" : string.Empty;
                return !destination;
            }
        }

        var tile = _map.TileIndicesFor(gridUid, grid, levelCoordinates);
        var blocked = destination ? "cmu-elevator-destination-blocked" : "cmu-elevator-shaft-blocked";
        if (_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) && !tileRef.Tile.IsEmpty ||
            HasBlockingEntity(gridUid, grid, tile, levelCoordinates))
        {
            failure = blocked;
            return false;
        }

        failure = string.Empty;
        if (!destination)
            return true;

        var sourceTileData = _map.TryGetTileRef(source.GridUid, source.Grid, sourceTile, out var sourceTileRef)
            ? sourceTileRef.Tile
            : Tile.Empty;
        _tilesToMove.Add(new ElevatorTile(sourceTile, sourceTileData, gridUid, grid, tile));
        return true;
    }

    private bool TryBuildFootprint(
        Entity<CMUElevatorComponent> ent,
        out ElevatorSource source,
        out HashSet<Vector2i> footprint,
        out string failure)
    {
        source = default;
        footprint = _footprintTiles;
        failure = "cmu-elevator-invalid-rail-outline";
        _railTiles.Clear();
        _solidTiles.Clear();
        _blockedEdges.Clear();

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

            var railTile = _map.TileIndicesFor(gridUid, grid, railXform.Coordinates);
            _railTiles.Add(railTile);

            if (rail.FullTile)
            {
                _solidTiles.Add(railTile);
                continue;
            }

            // Railings fence only the side of their tile that they face.
            var fence = railXform.LocalRotation.GetCardinalDir().ToIntVec();
            _blockedEdges.Add(EdgeKey(railTile, railTile + fence));
        }

        if (_railTiles.Count == 0)
        {
            failure = "cmu-elevator-no-rails";
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

        var bounds = new Box2i(minX, minY, maxX, maxY);
        var controlTile = _map.TileIndicesFor(gridUid, grid, controlXform.Coordinates);

        // Older maps outline the platform with a solid ring of railings, so retry with every piece as a full tile.
        if (!TryFloodFootprint(controlTile, bounds, legacyRing: false, out failure) &&
            !TryFloodFootprint(controlTile, bounds, legacyRing: true, out _))
        {
            return false;
        }

        source = new ElevatorSource(mapUid, gridUid, grid);
        return true;
    }

    /// <summary>
    /// Floods outward from the control. Railings block the edge they face; full-tile pieces such as walls
    /// and doors join the platform but the flood only continues from them into other full-tile pieces.
    /// The outline is closed when the flood never leaves the pieces' bounding box.
    /// </summary>
    private bool TryFloodFootprint(Vector2i start, Box2i bounds, bool legacyRing, out string failure)
    {
        _footprintTiles.Clear();
        _floodQueue.Clear();
        failure = "cmu-elevator-outline-open";

        if (!Contains(bounds, start))
            return false;

        _footprintTiles.Add(start);
        _floodQueue.Enqueue(start);
        while (_floodQueue.TryDequeue(out var tile))
        {
            var fromSolid = tile != start && IsSolid(tile);
            foreach (var offset in CardinalOffsets)
            {
                var next = tile + offset;
                if (!legacyRing && _blockedEdges.Contains(EdgeKey(tile, next)))
                    continue;

                if (fromSolid && !IsSolid(next))
                    continue;

                if (!Contains(bounds, next))
                {
                    _footprintTiles.Clear();
                    return false;
                }

                if (_footprintTiles.Add(next))
                    _floodQueue.Enqueue(next);
            }
        }

        // Pieces outside the platform would be left behind on the old level.
        if (!_railTiles.IsSubsetOf(_footprintTiles))
        {
            failure = "cmu-elevator-rail-outside";
            _footprintTiles.Clear();
            return false;
        }

        return true;

        bool IsSolid(Vector2i tile)
        {
            return legacyRing ? _railTiles.Contains(tile) : _solidTiles.Contains(tile);
        }
    }

    private static bool Contains(Box2i bounds, Vector2i tile)
    {
        return tile.X >= bounds.Left && tile.X <= bounds.Right &&
               tile.Y >= bounds.Bottom && tile.Y <= bounds.Top;
    }

    private static (Vector2i, Vector2i) EdgeKey(Vector2i a, Vector2i b)
    {
        return a.X < b.X || (a.X == b.X && a.Y <= b.Y) ? (a, b) : (b, a);
    }

    private bool TryCollectPlatformEntities(ElevatorSource source, HashSet<Vector2i> footprint)
    {
        _platformEntities.Clear();
        foreach (var tile in footprint)
        {
            var localCoordinates = _map.GridTileToLocal(source.GridUid, source.Grid, tile);
            var mapCoordinates = _transform.ToMapCoordinates(localCoordinates);
            _nearby.Clear();
            _lookup.GetEntitiesInRange(mapCoordinates.MapId, mapCoordinates.Position, EntityLookupRadius, _nearby, LookupFlags.Uncontained);

            foreach (var entity in _nearby)
            {
                if (entity == source.MapUid || entity == source.GridUid ||
                    TerminatingOrDeleted(entity) || IsViewProbe(entity))
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
                TerminatingOrDeleted(entity) ||
                IsViewProbe(entity))
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

    private bool IsViewProbe(EntityUid entity)
    {
        return MetaData(entity).EntityPrototype?.ID == ZLevelEyePrototype;
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
