using Content.Server.AU14.Round;
using Content.Server.GameTicking;
using Content.Server.Spawners.Components;
using Content.Server.Station.Systems;
using Content.Shared.AU14;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.Spawners.EntitySystems;

public sealed partial class SpawnPointSystem : EntitySystem
{
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private StationSystem _stationSystem = default!;
    [Dependency] private StationSpawningSystem _stationSpawning = default!;
    [Dependency] private AuRoundSystem _auRoundSystem = default!;

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
        bool isOpfor  = !string.IsNullOrEmpty(jobId) && jobId.Contains("opfor",  StringComparison.OrdinalIgnoreCase);
        bool isGovfor = !string.IsNullOrEmpty(jobId) && jobId.Contains("govfor", StringComparison.OrdinalIgnoreCase);

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
            var shipQuery = EntityQueryEnumerator<ShipFactionComponent>();
            while (shipQuery.MoveNext(out var shipUid, out var shipFaction))
            {
                if (string.IsNullOrEmpty(shipFaction.Faction)) continue;
                allShipGrids.Add(shipUid);
                bool isFactionMatch = isGovfor
                    ? shipFaction.Faction.Equals("govfor", StringComparison.OrdinalIgnoreCase)
                    : shipFaction.Faction.Equals("opfor",  StringComparison.OrdinalIgnoreCase);
                if (isFactionMatch)
                    factionShipGrids.Add(shipUid);
            }

            if (factionInShip)
            {
                // SHIP-SIDE SPAWN
                // Any spawn point on the faction's ship is valid — type does not matter.
                // Prefer job-specific matches; fall back to everything else on the ship.
                // We NEVER fall through to the general logic below.
                var preferred = new List<EntityCoordinates>();
                var fallback  = new List<EntityCoordinates>();

                var pts = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
                while (pts.MoveNext(out var _, out var sp, out var xform))
                {
                    if (xform.GridUid == null || !factionShipGrids.Contains(xform.GridUid.Value))
                        continue;
                    if (sp.SpawnType == SpawnPointType.Observer)
                        continue;

                    if (sp.Job != null && sp.Job == args.Job)
                        preferred.Add(xform.Coordinates);
                    else
                        fallback.Add(xform.Coordinates);
                }

                if (preferred.Count == 0 && fallback.Count == 0)
                {
                    Log.Error($"[SpawnPointSystem] No spawn points found on ship for faction " +
                              $"{(isGovfor ? "govfor" : "opfor")} — player cannot spawn!");
                    return;
                }

                var loc = preferred.Count > 0 ? _random.Pick(preferred) : _random.Pick(fallback);
                args.SpawnResult = _stationSpawning.SpawnPlayerMob(
                    loc, args.Job, args.HumanoidCharacterProfile, args.Station);
                return; // hard return — never touches planet logic
            }
            else
            {
                // PLANET-SIDE SPAWN
                // Only use spawn points that are NOT on any faction ship grid.
                var preferred = new List<EntityCoordinates>();
                var fallback  = new List<EntityCoordinates>();

                var pts = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
                while (pts.MoveNext(out var _, out var sp, out var xform))
                {
                    // Exclude anything on a faction ship
                    if (xform.GridUid != null && allShipGrids.Contains(xform.GridUid.Value))
                        continue;

                    if ((sp.SpawnType == SpawnPointType.Job || sp.SpawnType == SpawnPointType.Unset) &&
                        (args.Job == null || sp.Job == args.Job))
                    {
                        preferred.Add(xform.Coordinates);
                    }
                    else if ((isOpfor  && sp.SpawnType == SpawnPointType.LateJoinOpfor) ||
                             (isGovfor && sp.SpawnType == SpawnPointType.LateJoinGovfor))
                    {
                        fallback.Add(xform.Coordinates);
                    }
                }

                if (preferred.Count > 0 || fallback.Count > 0)
                {
                    var loc = preferred.Count > 0 ? _random.Pick(preferred) : _random.Pick(fallback);
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
                     (args.Job == null || spawnPoint.Job == args.Job))
            {
                if (isLateJoin)
                    preferredPositions.Add(xform.Coordinates);
                else
                    possiblePositions.Add(xform.Coordinates);
            }
        }

        // Last resort: any spawn point.
        if (preferredPositions.Count == 0 && possiblePositions.Count == 0)
        {
            var pts2 = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
            if (pts2.MoveNext(out var _, out var _, out var xform2))
                possiblePositions.Add(xform2.Coordinates);
            else
            {
                Log.Error("No spawn points were available!");
                return;
            }
        }

        var spawnLoc = preferredPositions.Count > 0
            ? _random.Pick(preferredPositions)
            : _random.Pick(possiblePositions);

        args.SpawnResult = _stationSpawning.SpawnPlayerMob(
            spawnLoc, args.Job, args.HumanoidCharacterProfile, args.Station);
    }
}
