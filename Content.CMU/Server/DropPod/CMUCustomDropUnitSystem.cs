using System.Numerics;
using Content.Shared._RMC14.CameraShake;
using Content.Shared.CMU14.DropPod;
using Content.Shared.Damage.Systems;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Ghost.Components;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.DropPod;

/// <summary>
/// Fires drop units built from ordinary walls and doors around a <see cref="CMUCustomDropUnitComponent"/> core.
/// Like an elevator car, the room is found by flooding out from the core, held with its doors bolted for the flight,
/// and then cut out of its grid and set down on the planet with everything inside it.
/// </summary>
public sealed partial class CMUCustomDropUnitSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private RMCCameraShakeSystem _cameraShake = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoorSystem _doors = default!;
    [Dependency] private CMUDropPodLandingSystem _landing = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private ITileDefinitionManager _tileDefinitions = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    private const float LookupRadius = 0.45f;

    private static readonly Vector2i[] Cardinals = [new(1, 0), new(-1, 0), new(0, 1), new(0, -1)];

    private static readonly Vector2i[] Neighbours =
    [
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
        new(1, 1), new(1, -1), new(-1, 1), new(-1, -1),
    ];

    private readonly HashSet<Vector2i> _interior = new();
    private readonly HashSet<Vector2i> _boundary = new();
    private readonly Queue<Vector2i> _queue = new();
    private readonly HashSet<EntityUid> _nearby = new();
    private readonly HashSet<EntityUid> _cargo = new();
    private readonly List<EntityUid> _riders = new();
    private readonly List<EntityUid> _doorList = new();
    private readonly List<UnitTile> _tiles = new();
    private readonly List<Entity<CMUCustomDropUnitComponent, CMUCustomDropUnitFlightComponent>> _arrivals = new();

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        _arrivals.Clear();

        var query = EntityQueryEnumerator<CMUCustomDropUnitComponent, CMUCustomDropUnitFlightComponent>();
        while (query.MoveNext(out var uid, out var unit, out var flight))
        {
            // Doors still swinging shut are bolted on a later tick, and anyone opening one mid-flight is shut out again.
            foreach (var door in flight.Doors)
                SecureDoor(door);

            if (now < flight.NextPhaseAt)
                continue;

            flight.Phase++;
            if (flight.Phase >= unit.Phases.Count)
            {
                _arrivals.Add((uid, unit, flight));
                continue;
            }

            AdvancePhase((uid, unit), flight, now);
        }

        foreach (var arrival in _arrivals)
            Arrive(arrival);
    }

    public bool TryLaunch(EntityUid uid, EntityUid planetMap, Vector2i target, out string? reason)
    {
        reason = null;
        if (!TryComp(uid, out CMUCustomDropUnitComponent? unit))
            return false;

        Entity<CMUCustomDropUnitComponent> ent = (uid, unit);
        if (unit.State != CMUDropPodState.Ready || HasComp<CMUCustomDropUnitFlightComponent>(uid))
        {
            reason = Loc.GetString("cmu-drop-pod-not-ready", ("pod", uid));
            return false;
        }

        if (!TryGetUnit(ent, out var unitData, out reason))
            return false;

        if (!TryFindLanding(ent, unitData, planetMap, target, unit.ScatterRadius, out _))
        {
            reason = Loc.GetString("cmu-drop-pod-no-landing", ("pod", uid));
            return false;
        }

        if (!CollectCargo(unitData))
        {
            reason = Loc.GetString("cmu-drop-unit-grid-aboard");
            return false;
        }

        var flight = EnsureComp<CMUCustomDropUnitFlightComponent>(uid);
        flight.PlanetMap = planetMap;
        flight.Target = target;
        flight.Phase = -1;
        flight.NextPhaseAt = _timing.CurTime;
        flight.BrokenApart = false;
        flight.Doors = new List<EntityUid>(_doorList);

        unit.State = CMUDropPodState.InFlight;
        Dirty(ent);

        foreach (var door in flight.Doors)
            SecureDoor(door);

        _audio.PlayPvs(unit.LaunchSound, uid);
        return true;
    }

    private void AdvancePhase(Entity<CMUCustomDropUnitComponent> ent, CMUCustomDropUnitFlightComponent flight, TimeSpan now)
    {
        var phase = ent.Comp.Phases[flight.Phase];
        flight.NextPhaseAt = now + phase.Duration;

        if (!TryGetUnit(ent, out var unitData, out _))
            return;

        CollectCargo(unitData);
        var breakup = phase.Atmospheric && !flight.BrokenApart && _random.Prob(ent.Comp.AtmosphereBreakupChance);
        if (breakup)
            flight.BrokenApart = true;

        foreach (var rider in _riders)
        {
            if (phase.Message is { } message)
                _popup.PopupEntity(Loc.GetString(message), rider, rider, PopupType.Medium);

            if (phase.Sound != null)
                _audio.PlayGlobal(phase.Sound, rider);

            if (phase.Shakes > 0)
                _cameraShake.ShakeCamera(rider, phase.Shakes, phase.ShakeStrength);

            if (breakup)
                _popup.PopupEntity(Loc.GetString("cmu-drop-unit-breakup-atmosphere"), rider, rider, PopupType.LargeCaution);
        }
    }

    private void Arrive(Entity<CMUCustomDropUnitComponent, CMUCustomDropUnitFlightComponent> ent)
    {
        var (uid, unit, flight) = ent;
        var brokenApart = flight.BrokenApart;
        var planetMap = flight.PlanetMap;
        var target = flight.Target;
        var doors = flight.Doors;
        RemCompDeferred<CMUCustomDropUnitFlightComponent>(uid);

        var radius = unit.ScatterRadius + (brokenApart ? unit.BreakupExtraScatter : 0);
        if (!TryGetUnit((uid, unit), out var unitData, out var reason)
            || !TryFindLanding((uid, unit), unitData, planetMap, target, radius, out var landing)
            || !CollectCargo(unitData))
        {
            Abort((uid, unit), doors, reason ?? Loc.GetString("cmu-drop-pod-no-landing", ("pod", uid)));
            return;
        }

        CrushLandingZone(uid, unit, landing);

        foreach (var tile in _tiles)
        {
            if (!tile.Tile.IsEmpty)
                _map.SetTile(tile.Grid, tile.Grid, tile.TargetIndices, tile.Tile);
        }

        var offset = landing.CoreWorld - unitData.CoreWorld;
        foreach (var cargo in _cargo)
        {
            if (TerminatingOrDeleted(cargo))
                continue;

            var xform = Transform(cargo);
            var wasAnchored = xform.Anchored;
            var position = _transform.GetMapCoordinates(cargo).Position + offset;
            _transform.SetMapCoordinates(cargo, new MapCoordinates(position, landing.MapId));
            if (wasAnchored && !_transform.AnchorEntity((cargo, xform)))
                Log.Warning($"Failed to re-anchor drop unit piece {ToPrettyString(cargo)} after landing.");
        }

        var leftBehind = unit.LeftBehindTile is { } leftId && _tileDefinitions.TryGetDefinition(leftId, out var leftDef)
            ? new Tile(leftDef.TileId)
            : Tile.Empty;

        foreach (var tile in _tiles)
            _map.SetTile(unitData.Grid, unitData.Grid, tile.SourceIndices, leftBehind);

        unit.State = CMUDropPodState.Landed;
        Dirty(uid, unit);
        _audio.PlayPvs(unit.LandingSound, uid);

        foreach (var door in _doorList)
            ReleaseDoor(door);

        foreach (var rider in _riders)
        {
            if (unit.LandingShakes > 0)
                _cameraShake.ShakeCamera(rider, unit.LandingShakes, unit.LandingShakeStrength);
        }

        if (brokenApart || _random.Prob(unit.ImpactBreakupChance))
        {
            BreakApart(uid, unit);
            return;
        }

        foreach (var rider in _riders)
        {
            if (unit.InjuryDamage is not { } injury || !_random.Prob(unit.InjuryChance))
                continue;

            _damageable.TryChangeDamage(rider, injury, true, false, origin: uid);
            if (unit.InjuryStun > TimeSpan.Zero)
                _stun.TryStun(rider, unit.InjuryStun, true);

            _popup.PopupEntity(Loc.GetString("cmu-drop-pod-injured"), rider, rider, PopupType.MediumCaution);
        }
    }

    private void Abort(Entity<CMUCustomDropUnitComponent> ent, List<EntityUid> doors, string reason)
    {
        ent.Comp.State = CMUDropPodState.Ready;
        Dirty(ent);

        foreach (var door in doors)
            ReleaseDoor(door);

        _popup.PopupEntity(Loc.GetString("cmu-drop-unit-aborted", ("reason", reason)), ent, PopupType.MediumCaution);
    }

    private void CrushLandingZone(EntityUid uid, CMUCustomDropUnitComponent unit, Landing landing)
    {
        if (unit.CrushDamage is not { } crush)
            return;

        var crushed = new HashSet<EntityUid>();
        foreach (var tile in _tiles)
        {
            _nearby.Clear();
            _lookup.GetEntitiesInRange(landing.MapId, tile.TargetWorld, LookupRadius, _nearby, LookupFlags.Uncontained);
            foreach (var victim in _nearby)
            {
                if (!_cargo.Contains(victim) && HasComp<MobStateComponent>(victim) && crushed.Add(victim))
                    _damageable.TryChangeDamage(victim, crush, true, false, origin: uid);
            }
        }
    }

    private void BreakApart(EntityUid uid, CMUCustomDropUnitComponent unit)
    {
        _audio.PlayPvs(unit.BreakupSound, uid);
        foreach (var rider in _riders)
        {
            _popup.PopupEntity(Loc.GetString("cmu-drop-unit-breakup"), rider, rider, PopupType.LargeCaution);
            if (unit.BreakupDamage is { } damage)
                _damageable.TryChangeDamage(rider, damage, true, false, origin: uid);

            if (unit.BreakupStun > TimeSpan.Zero)
                _stun.TryStun(rider, unit.BreakupStun, true);
        }

        foreach (var piece in _cargo)
        {
            if (TerminatingOrDeleted(piece) || GetPieceKind(unit, piece) == PieceKind.None)
                continue;

            if (unit.BreakupDebris is { } debris)
                Spawn(debris, Transform(piece).Coordinates);

            QueueDel(piece);
        }
    }

    /// <summary>
    /// Floods out from the core. The fill is stopped only by wall and door pieces, which join the unit along with every
    /// piece touching the room, corners included. Holes in the floor and rooms that run past the size limit are rejected.
    /// </summary>
    private bool TryGetUnit(Entity<CMUCustomDropUnitComponent> ent, out UnitData unitData, out string? reason)
    {
        unitData = default;
        reason = Loc.GetString("cmu-drop-unit-invalid-grid");
        _interior.Clear();
        _boundary.Clear();
        _queue.Clear();

        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid || !TryComp(gridUid, out MapGridComponent? grid))
            return false;

        Entity<MapGridComponent> gridEnt = (gridUid, grid);
        var core = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        _interior.Add(core);
        _queue.Enqueue(core);
        var doors = 0;

        while (_queue.TryDequeue(out var tile))
        {
            if (IsHole(gridEnt, tile))
            {
                reason = Loc.GetString("cmu-drop-unit-breach");
                return false;
            }

            foreach (var dir in Neighbours)
            {
                var next = tile + dir;
                if (_interior.Contains(next) || _boundary.Contains(next))
                    continue;

                var kind = GetTileKind(ent.Comp, gridEnt, next);
                if (kind != PieceKind.None)
                {
                    _boundary.Add(next);
                    if (kind == PieceKind.Door)
                        doors++;

                    continue;
                }

                // Diagonals only pick up corner pieces; the room itself spreads orthogonally.
                if (dir.X != 0 && dir.Y != 0)
                    continue;

                _interior.Add(next);
                _queue.Enqueue(next);
                if (_interior.Count + _boundary.Count > ent.Comp.MaxTiles)
                {
                    reason = Loc.GetString("cmu-drop-unit-too-large", ("max", ent.Comp.MaxTiles));
                    return false;
                }
            }
        }

        if (_interior.Count + _boundary.Count > ent.Comp.MaxTiles)
        {
            reason = Loc.GetString("cmu-drop-unit-too-large", ("max", ent.Comp.MaxTiles));
            return false;
        }

        if (ent.Comp.RequireDoor && doors == 0)
        {
            reason = Loc.GetString("cmu-drop-unit-no-door");
            return false;
        }

        var coreWorld = _transform.ToMapCoordinates(_map.GridTileToLocal(gridUid, grid, core)).Position;
        unitData = new UnitData(gridEnt, xform.MapID, coreWorld);
        reason = null;
        return true;
    }

    private bool IsHole(Entity<MapGridComponent> grid, Vector2i tile)
    {
        return !_map.TryGetTileRef(grid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty || _turf.IsSpace(tileRef);
    }

    private PieceKind GetTileKind(CMUCustomDropUnitComponent unit, Entity<MapGridComponent> grid, Vector2i indices)
    {
        var result = PieceKind.None;
        var anchored = _map.GetAnchoredEntities(grid, indices);
        while (anchored.MoveNext(out var piece))
        {
            var kind = GetPieceKind(unit, piece.Value);
            if (kind == PieceKind.Door)
                return kind;

            if (kind == PieceKind.Wall)
                result = kind;
        }

        return result;
    }

    private PieceKind GetPieceKind(CMUCustomDropUnitComponent unit, EntityUid piece)
    {
        if (unit.DoorWhitelist != null && _whitelist.IsValid(unit.DoorWhitelist, piece))
            return PieceKind.Door;

        if (unit.WallWhitelist != null && _whitelist.IsValid(unit.WallWhitelist, piece))
            return PieceKind.Wall;

        return PieceKind.None;
    }

    /// <summary>
    /// Tries landing points around the target until the whole unit fits. The core picks the column, including climbing
    /// onto indestructible walls, and every other tile must be clear on the level the core lands on.
    /// Fills <see cref="_tiles"/> with the source and destination of every tile in the unit.
    /// </summary>
    private bool TryFindLanding(
        Entity<CMUCustomDropUnitComponent> ent,
        UnitData unitData,
        EntityUid planetMap,
        Vector2i target,
        float radius,
        out Landing landing)
    {
        landing = default;
        var unit = ent.Comp;
        for (var attempt = 0; attempt < Math.Max(1, unit.LandingAttempts); attempt++)
        {
            if (!_landing.TryFindLanding(planetMap,
                    target,
                    radius,
                    1,
                    unit.TargetDepth,
                    unit.LandingBlockers,
                    unit.BlockersMustBeIndestructible,
                    unit.RespectParadropAreas,
                    out var coreLanding))
            {
                continue;
            }

            var coreMap = _transform.ToMapCoordinates(coreLanding);
            landing = new Landing(_map.GetMap(coreMap.MapId), coreMap.MapId, coreMap.Position);
            if (TryFitUnit(unit, unitData, landing))
                return true;
        }

        _tiles.Clear();
        return false;
    }

    private bool TryFitUnit(CMUCustomDropUnitComponent unit, UnitData unitData, Landing landing)
    {
        _tiles.Clear();
        var offset = landing.CoreWorld - unitData.CoreWorld;
        foreach (var source in AllTiles())
        {
            var sourceWorld = _transform.ToMapCoordinates(_map.GridTileToLocal(unitData.Grid, unitData.Grid, source)).Position;
            var targetWorld = sourceWorld + offset;
            if (!_landing.TryGetClearTile(landing.MapUid,
                    targetWorld,
                    unit.LandingBlockers,
                    unit.BlockersMustBeIndestructible,
                    out var targetGrid,
                    out var targetIndices))
            {
                return false;
            }

            // The unit cannot land on its own ship.
            if (targetGrid.Owner == unitData.Grid.Owner)
                return false;

            var tile = _map.GetTileRef(unitData.Grid, unitData.Grid, source).Tile;
            _tiles.Add(new UnitTile(source, tile, targetGrid, targetIndices, targetWorld));
        }

        return true;
    }

    private IEnumerable<Vector2i> AllTiles()
    {
        foreach (var tile in _interior)
            yield return tile;

        foreach (var tile in _boundary)
            yield return tile;
    }

    /// <summary>
    /// Gathers everything standing in the unit into <see cref="_cargo"/>, its mobs into <see cref="_riders"/>, and its doors.
    /// Returns false if another grid is aboard, since grids cannot be carried.
    /// </summary>
    private bool CollectCargo(UnitData unitData)
    {
        _cargo.Clear();
        _riders.Clear();
        _doorList.Clear();

        var gridUid = unitData.Grid.Owner;
        foreach (var tile in AllTiles())
        {
            var world = _transform.ToMapCoordinates(_map.GridTileToLocal(gridUid, unitData.Grid, tile)).Position;
            _nearby.Clear();
            _lookup.GetEntitiesInRange(unitData.MapId, world, LookupRadius, _nearby, LookupFlags.Uncontained);
            foreach (var entity in _nearby)
            {
                if (entity == gridUid || HasComp<MapComponent>(entity) || TerminatingOrDeleted(entity))
                    continue;

                // Ghosts and physics-less helpers such as z-level view probes are never carried.
                if (HasComp<GhostComponent>(entity) || !HasComp<PhysicsComponent>(entity))
                    continue;

                var xform = Transform(entity);
                if (xform.GridUid != gridUid)
                    continue;

                var indices = _map.TileIndicesFor(gridUid, unitData.Grid, xform.Coordinates);
                if (!_interior.Contains(indices) && !_boundary.Contains(indices))
                    continue;

                if (HasComp<MapGridComponent>(entity))
                    return false;

                if (!_cargo.Add(entity))
                    continue;

                if (HasComp<MobStateComponent>(entity))
                    _riders.Add(entity);

                if (HasComp<DoorComponent>(entity))
                    _doorList.Add(entity);
            }
        }

        return true;
    }

    private void SecureDoor(EntityUid uid)
    {
        if (!TryComp(uid, out DoorComponent? door))
            return;

        if (door.State == DoorState.Closed)
        {
            if (TryComp(uid, out DoorBoltComponent? bolts) && !bolts.BoltsDown)
                _doors.TrySetBoltDown((uid, bolts), true);

            return;
        }

        if (door.State is DoorState.Open or DoorState.Opening)
            _doors.TryClose(uid, door);
    }

    private void ReleaseDoor(EntityUid uid)
    {
        if (TryComp(uid, out DoorBoltComponent? bolts) && bolts.BoltsDown)
            _doors.TrySetBoltDown((uid, bolts), false);
    }

    private enum PieceKind : byte
    {
        None,
        Wall,
        Door,
    }

    private readonly record struct UnitData(Entity<MapGridComponent> Grid, MapId MapId, Vector2 CoreWorld);

    private readonly record struct Landing(EntityUid MapUid, MapId MapId, Vector2 CoreWorld);

    private readonly record struct UnitTile(
        Vector2i SourceIndices,
        Tile Tile,
        Entity<MapGridComponent> Grid,
        Vector2i TargetIndices,
        Vector2 TargetWorld);
}
