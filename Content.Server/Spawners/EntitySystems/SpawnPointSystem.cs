using Content.Server.CMU14.Round;
using Content.Server.GameTicking;
using Content.Server.CMU14.Roles;
using Content.Server.Spawners.Components;
using Content.Server.Station.Systems;
using Content.Shared._RMC14.Map; // CMU14
using Content.Shared.Maps; // CMU14
using Content.Shared.CMU14.Round.Roles;
using Content.Shared.CMU14;
using Content.Shared.Roles;
using Robust.Shared.Map;
using Robust.Shared.Map.Components; // CMU14
using Robust.Shared.Maths; // CMU14
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Spawners.EntitySystems;

public sealed partial class SpawnPointSystem : EntitySystem
{
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private RoundJobProfileSystem _roundJobProfiles = default!;
    [Dependency] private StationSystem _stationSystem = default!;
    [Dependency] private StationSpawningSystem _stationSpawning = default!;
    [Dependency] private AuRoundSystem _auRoundSystem = default!;
    [Dependency] private RMCMapSystem _rmcMap = default!; // CMU14

    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerSpawningEvent>(OnPlayerSpawning);
    }

    private void OnPlayerSpawning(PlayerSpawningEvent args)
    {
        if (args.SpawnResult != null)
            return;

        bool isLateJoin = _gameTicker.RunLevel == GameRunLevel.InRound;
        string? jobId = args.Job?.ToString();
        JobPrototype? job = null;
        if (args.Job is { } jobProto)
            _prototype.TryIndex(jobProto, out job);

        var side = _roundJobProfiles.GetRoundSide(job, jobId);
        bool isOpfor = side == RoundJobSide.Opfor;
        bool isGovfor = side == RoundJobSide.Govfor;
        var spawnPointJob = job?.SpawnPointJob;

        // --- AU14: Faction spawn routing ---
        // If the player is govfor or opfor we decide where they spawn based solely on
        // whether their faction is configured as ship-side.  If they are ship-side they
        // MUST spawn on their ship and can NEVER fall through to the generic planet logic.
        if (isGovfor || isOpfor)
        {
            var planet = _auRoundSystem.GetSelectedPlanet();
            bool factionInShip = isGovfor ? (planet?.GovforInShip ?? false)
                                           : (planet?.OpforInShip ?? false);

            // Build ship-grid sets.
            // ShipFactionComponent sits on the grid entity itself, so its UID == the GridUid
            // of any entity that lives on that ship.
            var factionShipGrids = new HashSet<EntityUid>(); // grids belonging to THIS faction's ships
            var allShipGrids     = new HashSet<EntityUid>(); // grids belonging to ANY faction ship
            var factionShipStations = new HashSet<EntityUid>();
            var allShipStations = new HashSet<EntityUid>();
            var shipQuery = EntityQueryEnumerator<ShipFactionComponent>();
            while (shipQuery.MoveNext(out var shipUid, out var shipFaction))
            {
                if (string.IsNullOrEmpty(shipFaction.Faction)) continue;
                allShipGrids.Add(shipUid);
                var shipStation = _stationSystem.GetOwningStation(shipUid);
                if (shipStation is { } resolvedShipStation)
                    allShipStations.Add(resolvedShipStation);

                bool isFactionMatch = isGovfor
                    ? shipFaction.Faction.Equals("govfor", StringComparison.OrdinalIgnoreCase)
                    : shipFaction.Faction.Equals("opfor",  StringComparison.OrdinalIgnoreCase);
                if (isFactionMatch)
                {
                    factionShipGrids.Add(shipUid);
                    if (shipStation is { } resolvedFactionShipStation)
                        factionShipStations.Add(resolvedFactionShipStation);
                }
            }

            if (factionInShip)
            {
                // SHIP-SIDE SPAWN
                // Use this job's marker (or its explicit legacy alias), then a designated
                // late-join marker. Never guess by picking another role's spawn point.
                // We NEVER fall through to the general logic below.
                var preferred = new List<EntityCoordinates>();
                var legacyPreferred = new List<EntityCoordinates>();
                var fallback  = new List<EntityCoordinates>();

                var pts = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
                while (pts.MoveNext(out var _, out var sp, out var xform))
                {
                    if (!IsOnShip(xform, factionShipGrids, factionShipStations))
                        continue;
                    if (sp.SpawnType == SpawnPointType.Observer)
                        continue;

                    if (IsJobSpawnPoint(sp, args.Job))
                        preferred.Add(xform.Coordinates);
                    else if (IsLegacyJobSpawnPoint(sp, spawnPointJob))
                        legacyPreferred.Add(xform.Coordinates);
                    else if (sp.SpawnType == SpawnPointType.LateJoin ||
                             (isGovfor && sp.SpawnType == SpawnPointType.LateJoinGovfor) ||
                             (isOpfor && sp.SpawnType == SpawnPointType.LateJoinOpfor))
                        fallback.Add(xform.Coordinates);
                }

                if (preferred.Count == 0 && legacyPreferred.Count == 0 && fallback.Count == 0)
                {
                    Log.Error($"[SpawnPointSystem] No spawn points found on ship for faction " +
                              $"{(isGovfor ? "govfor" : "opfor")} — player cannot spawn!");
                    return;
                }

                var loc = preferred.Count > 0 ? _random.Pick(preferred) :
                    legacyPreferred.Count > 0 ? _random.Pick(legacyPreferred) :
                    _random.Pick(fallback);
                args.SpawnResult = _stationSpawning.SpawnPlayerMob(
                    loc, args.Job, args.HumanoidCharacterProfile, args.Station);
                return; // hard return — never touches planet logic
            }
            else
            {
                // PLANET-SIDE SPAWN
                // Only use spawn points that are NOT on any faction ship grid.
                var preferred = new List<EntityCoordinates>();
                var legacyPreferred = new List<EntityCoordinates>();
                var fallback  = new List<EntityCoordinates>();

                var pts = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
                while (pts.MoveNext(out var _, out var sp, out var xform))
                {
                    // Exclude anything on a faction ship
                    if (IsOnShip(xform, allShipGrids, allShipStations))
                        continue;

                    if ((sp.SpawnType == SpawnPointType.Job || sp.SpawnType == SpawnPointType.Unset) &&
                        args.Job == null)
                    {
                        preferred.Add(xform.Coordinates);
                    }
                    else if ((sp.SpawnType == SpawnPointType.Job || sp.SpawnType == SpawnPointType.Unset) &&
                             IsJobSpawnPoint(sp, args.Job))
                    {
                        preferred.Add(xform.Coordinates);
                    }
                    else if ((sp.SpawnType == SpawnPointType.Job || sp.SpawnType == SpawnPointType.Unset) &&
                             IsLegacyJobSpawnPoint(sp, spawnPointJob))
                    {
                        legacyPreferred.Add(xform.Coordinates);
                    }
                    else if ((isOpfor  && sp.SpawnType == SpawnPointType.LateJoinOpfor) ||
                             (isGovfor && sp.SpawnType == SpawnPointType.LateJoinGovfor))
                    {
                        fallback.Add(xform.Coordinates);
                    }
                }

                if (preferred.Count > 0 || legacyPreferred.Count > 0 || fallback.Count > 0)
                {
                    var loc = preferred.Count > 0 ? _random.Pick(preferred) :
                        legacyPreferred.Count > 0 ? _random.Pick(legacyPreferred) :
                        _random.Pick(fallback);
                    args.SpawnResult = _stationSpawning.SpawnPlayerMob(
                        loc, args.Job, args.HumanoidCharacterProfile, args.Station);
                    return;
                }
                // If nothing found planet-side, fall through to generic logic below.
            }
        }

        // --- Generic (non-faction / planet fallback) spawn logic ---
        var possiblePositions  = new List<EntityCoordinates>();
        var preferredPositions = new List<EntityCoordinates>();

        var points = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
        while (points.MoveNext(out var uid, out var spawnPoint, out var xform))
        {
            if (args.Station != null && _stationSystem.GetOwningStation(uid, xform) != args.Station)
                continue;

            if (isLateJoin && spawnPoint.SpawnType == SpawnPointType.LateJoin)
            {
                possiblePositions.Add(xform.Coordinates);
            }
            else if (spawnPoint.SpawnType == SpawnPointType.Job &&
                     (args.Job == null || spawnPoint.Job == null || spawnPoint.Job == args.Job))
            {
                if (isLateJoin)
                    preferredPositions.Add(xform.Coordinates);
                else
                    possiblePositions.Add(xform.Coordinates);
            }
        }

        // CMU14 Begin: the station filter above hides job markers on other maps, so colony jobs
        // handed the warship station ended in the random-spawner backup below. Match the exact
        // job marker anywhere before that fallback.
        if (preferredPositions.Count == 0 && possiblePositions.Count == 0)
        {
            var jobPoints = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
            while (jobPoints.MoveNext(out _, out var jobPoint, out var jobXform))
            {
                if (jobPoint.SpawnType == SpawnPointType.Job
                    && (args.Job == null || jobPoint.Job == args.Job))
                    possiblePositions.Add(jobXform.Coordinates);
            }
        }
        // CMU14 End

        // Last resort: any spawn point.
        if (preferredPositions.Count == 0 && possiblePositions.Count == 0)
        {
            // Ok we've still not returned, but we need to put them /somewhere/.
            // TODO: Refactor gameticker spawning code so we don't have to do this!
            var points2 = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();

            if (points2.MoveNext(out _, out var xform))
            {
                Log.Error($"Unable to pick a valid spawn point, picking random spawner as a backup.\nRunLevel: {_gameTicker.RunLevel} Station: {ToPrettyString(args.Station)} Job: {args.Job}");
                possiblePositions.Add(xform.Coordinates);
            }
            else
            {
                Log.Error($"No spawn points were available!\nRunLevel: {_gameTicker.RunLevel} Station: {ToPrettyString(args.Station)} Job: {args.Job}");

                // CMU14: a map with no spawn points at all must not lock valid roles out of the
                // round. Fall back to the first unblocked tile near the largest grid's middle.
                // return; // CMU14
                if (!TryGetFallbackSpawn(args.Station, out var fallback))
                    return;

                possiblePositions.Add(fallback);
            }
        }

        var spawnLoc = preferredPositions.Count > 0
            ? _random.Pick(preferredPositions)
            : _random.Pick(possiblePositions);

        args.SpawnResult = _stationSpawning.SpawnPlayerMob(
            spawnLoc, args.Job, args.HumanoidCharacterProfile, args.Station);
    }

    // CMU14 method: last ditch spawn for stations with no spawn points. Walks outward from
    // the middle of the largest grid's bounding box to the first tile nothing blocks.
    private bool TryGetFallbackSpawn(EntityUid? station, out EntityCoordinates coords)
    {
        coords = default;

        if (station is not { } stationUid
            || _stationSystem.GetLargestGrid(stationUid) is not { } gridUid
            || !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var aabb = grid.LocalAABB;
        var center = new Vector2i((int) ((aabb.Left + aabb.Right) / 2f), (int) ((aabb.Bottom + aabb.Top) / 2f));
        var reach = (int) MathF.Max(aabb.Width, aabb.Height) / 2 + 1;

        for (var radius = 0; radius <= reach; radius++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dy = -radius; dy <= radius; dy++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius)
                        continue;

                    var tile = new EntityCoordinates(gridUid, center + new Vector2i(dx, dy));
                    // CMU14: IsTileBlocked ignores empty space, the ring can walk off the hull
                    if (_rmcMap.TryGetTileDef(tile, out var def)
                        && def.ID != ContentTileDefinition.SpaceID
                        && !_rmcMap.IsTileBlocked(tile))
                    {
                        coords = tile;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private bool IsOnShip(
        TransformComponent xform,
        IReadOnlySet<EntityUid> shipGrids,
        IReadOnlySet<EntityUid> shipStations)
    {
        if (xform.GridUid is not { } grid)
            return false;

        if (shipGrids.Contains(grid))
            return true;

        return _stationSystem.GetOwningStation(grid) is { } station && shipStations.Contains(station);
    }

    private static bool IsJobSpawnPoint(
        SpawnPointComponent spawnPoint,
        ProtoId<JobPrototype>? job)
    {
        return job != null && spawnPoint.Job == job;
    }

    private static bool IsLegacyJobSpawnPoint(
        SpawnPointComponent spawnPoint,
        ProtoId<JobPrototype>? spawnPointJob)
    {
        return spawnPointJob != null && spawnPoint.Job == spawnPointJob;
    }
}
