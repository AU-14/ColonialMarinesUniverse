using Content.Server.GameTicking;
using Content.Server.Spawners.Components;
using Content.Server.Station.Systems;
using Content.Shared._CMU14.Round.Roles;
using Content.Shared.AU14;
using Content.Shared.GameTicking;
using Content.Shared.Roles;
using Robust.Shared.Profiling;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._CMU14.Spawners;

/// <summary>
/// Compiles spawn-point and faction-ship lookup data once for the synchronous round-start spawn batch.
/// </summary>
public sealed partial class CMURoundSpawnPointSnapshotSystem : EntitySystem
{
    [Dependency] private EntityQuery<MetaDataComponent> _metaQuery = default!;
    [Dependency] private ProfManager _prof = default!;
    [Dependency] private EntityQuery<SpawnPointComponent> _spawnPointQuery = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private EntityQuery<TransformComponent> _xformQuery = default!;

    private readonly HashSet<EntityUid> _allShipGrids = [];
    private readonly HashSet<EntityUid> _allShipStations = [];
    private readonly HashSet<EntityUid> _govforShipGrids = [];
    private readonly HashSet<EntityUid> _govforShipStations = [];
    private readonly HashSet<EntityUid> _opforShipGrids = [];
    private readonly HashSet<EntityUid> _opforShipStations = [];
    private readonly CMURoundSpawnPointSnapshot _snapshot = new();

    /// <summary>
    /// Whether the synchronous round-start body-spawn batch can consume this snapshot.
    /// </summary>
    public bool Active { get; private set; }

    internal IReadOnlyList<CMURoundSpawnPointEntry> Entries => _snapshot.Entries;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundStartPlayerSpawnBatchEvent>(OnRoundStartPlayerSpawnBatch);
        SubscribeLocalEvent<RoundStartPlayerSpawnBatchFinishedEvent>(OnRoundStartPlayerSpawnBatchFinished);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    internal CMUGenericSpawnSelection PickGeneric(
        EntityUid? station,
        ProtoId<JobPrototype>? job,
        bool inRound,
        IRobustRandom random,
        out EntityUid uid)
    {
        if (!Active)
            return NoGenericSelection(out uid);

        var selection = _snapshot.PickGeneric(station, job, inRound, random, out uid);
        if (selection != CMUGenericSpawnSelection.None && !ValidateCachedEntry(uid))
        {
            return NoGenericSelection(out uid);
        }

        return selection;
    }

    internal bool TryPickFactionShip(
        RoundJobSide side,
        ProtoId<JobPrototype>? job,
        IRobustRandom random,
        out EntityUid uid)
    {
        if (!Active || !_snapshot.TryPickFactionShip(side, job, random, out uid))
        {
            uid = default;
            return false;
        }

        if (ValidateCachedEntry(uid))
            return true;

        uid = default;
        return false;
    }

    internal bool TryPickFactionPlanet(
        RoundJobSide side,
        ProtoId<JobPrototype>? job,
        IRobustRandom random,
        out EntityUid uid)
    {
        if (!Active || !_snapshot.TryPickFactionPlanet(side, job, random, out uid))
        {
            uid = default;
            return false;
        }

        if (ValidateCachedEntry(uid))
            return true;

        uid = default;
        return false;
    }

    internal void Prepare()
    {
        if (Active)
            return;

        using var profile = _prof.Group("CMU Round Spawn Point Snapshot");
        Clear();
        CollectFactionShips();

        var points = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
        while (points.MoveNext(out var uid, out var spawnPoint, out var transform))
        {
            var station = _station.GetOwningStation(uid, transform);
            var grid = transform.GridUid;
            var shipStation = grid is { } gridUid
                ? _station.GetOwningStation(gridUid)
                : null;

            _snapshot.Add(new CMURoundSpawnPointEntry(
                uid,
                station,
                spawnPoint.Job,
                spawnPoint.SpawnType,
                IsOnShip(grid, shipStation, _allShipGrids, _allShipStations),
                IsOnShip(grid, shipStation, _govforShipGrids, _govforShipStations),
                IsOnShip(grid, shipStation, _opforShipGrids, _opforShipStations)));
        }

        Active = true;
        if (_prof.IsEnabled)
        {
            _prof.WriteValue("CMU Round Spawn Point Snapshot Entries", _snapshot.Entries.Count);
            _prof.WriteValue("CMU Round Spawn Point Snapshot Ships", _allShipGrids.Count);
        }
    }

    private void OnRoundStartPlayerSpawnBatch(ref RoundStartPlayerSpawnBatchEvent ev)
    {
        Prepare();
    }

    private void OnRoundStartPlayerSpawnBatchFinished(ref RoundStartPlayerSpawnBatchFinishedEvent ev)
    {
        Clear();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        Clear();
    }

    private void CollectFactionShips()
    {
        var query = EntityQueryEnumerator<ShipFactionComponent>();
        while (query.MoveNext(out var shipUid, out var shipFaction))
        {
            if (string.IsNullOrWhiteSpace(shipFaction.Faction))
                continue;

            var shipStation = _station.GetOwningStation(shipUid);
            AddShip(shipUid, shipStation, _allShipGrids, _allShipStations);

            if (shipFaction.Faction.Equals("govfor", StringComparison.OrdinalIgnoreCase))
            {
                AddShip(shipUid, shipStation, _govforShipGrids, _govforShipStations);
            }
            else if (shipFaction.Faction.Equals("opfor", StringComparison.OrdinalIgnoreCase))
            {
                AddShip(shipUid, shipStation, _opforShipGrids, _opforShipStations);
            }
        }
    }

    private static void AddShip(
        EntityUid grid,
        EntityUid? station,
        HashSet<EntityUid> grids,
        HashSet<EntityUid> stations)
    {
        grids.Add(grid);
        if (station is { } stationUid)
            stations.Add(stationUid);
    }

    private static bool IsOnShip(
        EntityUid? grid,
        EntityUid? station,
        IReadOnlySet<EntityUid> shipGrids,
        IReadOnlySet<EntityUid> shipStations)
    {
        return grid is { } gridUid &&
               (shipGrids.Contains(gridUid) ||
                station is { } stationUid && shipStations.Contains(stationUid));
    }

    private static CMUGenericSpawnSelection NoGenericSelection(out EntityUid uid)
    {
        uid = default;
        return CMUGenericSpawnSelection.None;
    }

    internal bool ValidateCachedEntry(EntityUid uid)
    {
        if (Active && ValidateEntry(uid))
            return true;

        Clear();
        return false;
    }

    private bool ValidateEntry(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid) ||
            !_snapshot.TryGetEntry(uid, out var cached) ||
            !_metaQuery.TryGetComponent(uid, out var meta) ||
            meta.EntityPaused ||
            !_spawnPointQuery.TryGetComponent(uid, out var spawnPoint) ||
            !_xformQuery.TryGetComponent(uid, out var transform) ||
            spawnPoint.Job != cached.Job ||
            spawnPoint.SpawnType != cached.SpawnType)
        {
            return false;
        }

        var station = _station.GetOwningStation(uid, transform);
        var grid = transform.GridUid;
        var shipStation = grid is { } gridUid
            ? _station.GetOwningStation(gridUid)
            : null;

        return station == cached.Station &&
               IsOnShip(grid, shipStation, _allShipGrids, _allShipStations) == cached.OnAnyShip &&
               IsOnShip(grid, shipStation, _govforShipGrids, _govforShipStations) == cached.OnGovforShip &&
               IsOnShip(grid, shipStation, _opforShipGrids, _opforShipStations) == cached.OnOpforShip;
    }

    private void Clear()
    {
        Active = false;
        _snapshot.Clear();
        _allShipGrids.Clear();
        _allShipStations.Clear();
        _govforShipGrids.Clear();
        _govforShipStations.Clear();
        _opforShipGrids.Clear();
        _opforShipStations.Clear();
    }
}
