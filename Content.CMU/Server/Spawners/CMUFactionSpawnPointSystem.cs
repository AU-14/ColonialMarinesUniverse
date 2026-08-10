using Content.Server._RMC14.Rules.DistressSignal;
using Content.Server.AU14.Roles;
using Content.Server.AU14.Round;
using Content.Server.Spawners.Components;
using Content.Server.Spawners.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared._CMU14.Round.Roles;
using Content.Shared.AU14;
using Content.Shared.Roles;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._CMU14.Spawners;

/// <summary>
/// Routes GOVFOR and OPFOR jobs to their configured planet or faction ship before the generic spawner runs.
/// </summary>
public sealed partial class CMUFactionSpawnPointSystem : EntitySystem
{
    [Dependency] private CMURoundDirectorSystem _roundDirector = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private RoundJobProfileSystem _roundJobProfiles = default!;
    [Dependency] private CMURoundSpawnPointSnapshotSystem _roundSpawnPoints = default!;
    [Dependency] private StationSpawningSystem _stationSpawning = default!;
    [Dependency] private StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawningEvent>(
            OnPlayerSpawning,
            before: [typeof(SpawnPointSystem)],
            after: [typeof(CMDistressSignalRuleSystem)]);
    }

    private void OnPlayerSpawning(PlayerSpawningEvent args)
    {
        if (args.SpawnResult != null)
            return;

        var jobId = args.Job?.ToString();
        JobPrototype? job = null;
        if (args.Job is { } jobPrototype)
            _prototypes.TryIndex(jobPrototype, out job);

        var side = _roundJobProfiles.GetRoundSide(job, jobId);
        if (side is not (RoundJobSide.Govfor or RoundJobSide.Opfor))
            return;

        var govfor = side == RoundJobSide.Govfor;
        var faction = govfor ? "govfor" : "opfor";
        _roundDirector.TryGetCommittedLegacyPlanet(out var planet);
        var factionInShip = govfor
            ? planet?.GovforInShip ?? false
            : planet?.OpforInShip ?? false;

        if (_roundSpawnPoints.Active)
        {
            var spawned = factionInShip
                ? TrySpawnOnCachedShip(args, side)
                : TrySpawnOnCachedPlanet(args, side);
            if (_roundSpawnPoints.Active)
            {
                if (factionInShip && !spawned)
                    Log.Error($"No spawn points found on the selected {faction} ship for job '{jobId}'.");

                return;
            }
        }

        GetFactionShips(faction, out var factionShipGrids, out var allShipGrids,
            out var factionShipStations, out var allShipStations);

        if (factionInShip)
        {
            if (!TrySpawnOnShip(args, factionShipGrids, factionShipStations))
            {
                Log.Error($"No spawn points found on the selected {faction} ship for job '{jobId}'.");
            }

            return;
        }

        TrySpawnOnPlanet(args, side, allShipGrids, allShipStations);
    }

    private bool TrySpawnOnCachedShip(PlayerSpawningEvent args, RoundJobSide side)
    {
        return _roundSpawnPoints.TryPickFactionShip(side, args.Job, _random, out var spawnPoint) &&
               TrySpawnAtCachedPoint(spawnPoint, args);
    }

    private bool TrySpawnOnCachedPlanet(PlayerSpawningEvent args, RoundJobSide side)
    {
        return _roundSpawnPoints.TryPickFactionPlanet(side, args.Job, _random, out var spawnPoint) &&
               TrySpawnAtCachedPoint(spawnPoint, args);
    }

    private bool TrySpawnAtCachedPoint(EntityUid spawnPoint, PlayerSpawningEvent args)
    {
        if (!TryComp(spawnPoint, out TransformComponent? transform))
            return false;

        args.SpawnResult = SpawnPlayer(transform.Coordinates, args);
        return true;
    }

    private void GetFactionShips(
        string faction,
        out HashSet<EntityUid> factionShipGrids,
        out HashSet<EntityUid> allShipGrids,
        out HashSet<EntityUid> factionShipStations,
        out HashSet<EntityUid> allShipStations)
    {
        factionShipGrids = [];
        allShipGrids = [];
        factionShipStations = [];
        allShipStations = [];

        var query = EntityQueryEnumerator<ShipFactionComponent>();
        while (query.MoveNext(out var shipUid, out var shipFaction))
        {
            if (string.IsNullOrWhiteSpace(shipFaction.Faction))
                continue;

            allShipGrids.Add(shipUid);
            var shipStation = _station.GetOwningStation(shipUid);
            if (shipStation is { } station)
                allShipStations.Add(station);

            if (!shipFaction.Faction.Equals(faction, StringComparison.OrdinalIgnoreCase))
                continue;

            factionShipGrids.Add(shipUid);
            if (shipStation is { } factionStation)
                factionShipStations.Add(factionStation);
        }
    }

    private bool TrySpawnOnShip(
        PlayerSpawningEvent args,
        IReadOnlySet<EntityUid> factionShipGrids,
        IReadOnlySet<EntityUid> factionShipStations)
    {
        var preferred = new List<EntityCoordinates>();
        var fallback = new List<EntityCoordinates>();
        var points = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();

        while (points.MoveNext(out _, out var spawnPoint, out var transform))
        {
            if (!IsOnShip(transform, factionShipGrids, factionShipStations) ||
                spawnPoint.SpawnType == SpawnPointType.Observer)
            {
                continue;
            }

            if (spawnPoint.Job != null && spawnPoint.Job == args.Job)
                preferred.Add(transform.Coordinates);
            else
                fallback.Add(transform.Coordinates);
        }

        var positions = preferred.Count > 0 ? preferred : fallback;
        if (positions.Count == 0)
            return false;

        args.SpawnResult = SpawnPlayer(_random.Pick(positions), args);
        return true;
    }

    private bool TrySpawnOnPlanet(
        PlayerSpawningEvent args,
        RoundJobSide side,
        IReadOnlySet<EntityUid> allShipGrids,
        IReadOnlySet<EntityUid> allShipStations)
    {
        var preferred = new List<EntityCoordinates>();
        var fallback = new List<EntityCoordinates>();
        var points = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();

        while (points.MoveNext(out _, out var spawnPoint, out var transform))
        {
            if (IsOnShip(transform, allShipGrids, allShipStations))
                continue;

            if ((spawnPoint.SpawnType is SpawnPointType.Job or SpawnPointType.Unset) &&
                (args.Job == null || spawnPoint.Job == args.Job))
            {
                preferred.Add(transform.Coordinates);
            }
            else if ((side == RoundJobSide.Opfor && spawnPoint.SpawnType == SpawnPointType.LateJoinOpfor) ||
                     (side == RoundJobSide.Govfor && spawnPoint.SpawnType == SpawnPointType.LateJoinGovfor))
            {
                fallback.Add(transform.Coordinates);
            }
        }

        var positions = preferred.Count > 0 ? preferred : fallback;
        if (positions.Count == 0)
            return false;

        args.SpawnResult = SpawnPlayer(_random.Pick(positions), args);
        return true;
    }

    private EntityUid SpawnPlayer(EntityCoordinates coordinates, PlayerSpawningEvent args)
    {
        return _stationSpawning.SpawnPlayerMob(
            coordinates,
            args.Job,
            args.HumanoidCharacterProfile,
            args.Station);
    }

    private bool IsOnShip(
        TransformComponent transform,
        IReadOnlySet<EntityUid> shipGrids,
        IReadOnlySet<EntityUid> shipStations)
    {
        if (transform.GridUid is not { } grid)
            return false;

        if (shipGrids.Contains(grid))
            return true;

        return _station.GetOwningStation(grid) is { } station && shipStations.Contains(station);
    }
}
