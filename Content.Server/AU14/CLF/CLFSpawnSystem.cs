using Content.Server.GameTicking;
using Content.Server.Spawners.Components;
using Content.Server.Spawners.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared.AU14;
using Content.Shared.GameTicking;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.AU14.CLF;

/// <summary>
/// Handles CLF spawning at round start (at a chosen safehouse) and additional entity spawning.
/// Command roles (Cell Leader, Physician, Surgeon) always spawn at the safehouse.
/// Guerilla roles have a 66% chance to spawn at colony civilian spawn points and 34% at the safehouse.
/// </summary>
public sealed class ClfSpawnSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawning = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;

    private EntityCoordinates? _chosenSafehouseLocation;
    private bool _hasSpawnedAdditionalEntities;

    /// <summary>
    /// CLF command job IDs that always spawn at the safehouse.
    /// </summary>
    private static readonly HashSet<string> CommandJobIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "AU14JobCLFCellLeader",
        "AU14JobCLFPhysician",
        "AU14JobCLFSurgeon",
    };

    /// <summary>
    /// The colony civilian job whose spawn points guerillas may use.
    /// </summary>
    private const string ColonyCivilianJobId = "AU14JobCivilianColonist";
    private const string ClfSurgeonJobId = "AU14JobCLFSurgeon";

    private static readonly string[] ClfSurgeonRoundstartEquipment =
    {
        "CMPortableSurgicalBedSpawnFolded",
        "RMCSurgicalTray",
    };

    /// <summary>
    /// Chance (0-1) for a guerilla to spawn at a colony civilian spawn point instead of the safehouse.
    /// </summary>
    private const float GuerillaCivilianSpawnChance = 0.66f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<PlayerSpawningEvent>(OnPlayerSpawning, before: new[] { typeof(SpawnPointSystem) });
        SubscribeLocalEvent<RulePlayerSpawningEvent>(OnRulePlayerSpawning);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _chosenSafehouseLocation = null;
        _hasSpawnedAdditionalEntities = false;
    }

    private void OnRulePlayerSpawning(RulePlayerSpawningEvent ev)
    {
        // After all players have been assigned jobs, spawn additional entities if CLF players exist
        if (_chosenSafehouseLocation != null && !_hasSpawnedAdditionalEntities)
        {
            SpawnAdditionalEntities();
            _hasSpawnedAdditionalEntities = true;
        }
    }

    private void OnPlayerSpawning(PlayerSpawningEvent args)
    {
        // Only handle CLF jobs
        string? jobId = args.Job?.ToString();
        if (string.IsNullOrEmpty(jobId) || !jobId.Contains("CLF", StringComparison.OrdinalIgnoreCase))
            return;

        // Already handled by another system
        if (args.SpawnResult != null)
            return;

        // Choose safehouse location if not already chosen
        if (_chosenSafehouseLocation == null)
        {
            var safehouseMarkers = new List<EntityUid>();
            var query = EntityQueryEnumerator<SafehouseMarkerComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var _, out var _))
            {
                safehouseMarkers.Add(uid);
            }

            if (safehouseMarkers.Count > 0)
            {
                var chosenMarker = _random.Pick(safehouseMarkers);
                _chosenSafehouseLocation = Transform(chosenMarker).Coordinates;
                Log.Info($"CLF Spawn System: Chose safehouse marker {chosenMarker} at {_chosenSafehouseLocation}");

                // Spawn additional entities now that we have chosen a location
                SpawnAdditionalEntities();
                _hasSpawnedAdditionalEntities = true;
            }
            else
            {
                Log.Warning("CLF Spawn System: No SafehouseMarker found for CLF spawning!");
                return;
            }
        }

        // Determine spawn location based on role type
        bool isCommand = CommandJobIds.Contains(jobId);

        if (!isCommand && _random.Prob(GuerillaCivilianSpawnChance))
        {
            // Guerilla: try to spawn at a colony civilian spawn point
            var civilianSpawnLocation = GetRandomColonyCivilianSpawnPoint();
            if (civilianSpawnLocation != null)
            {
                args.SpawnResult = _stationSpawning.SpawnPlayerMob(
                    civilianSpawnLocation.Value,
                    args.Job,
                    args.HumanoidCharacterProfile,
                    args.Station);
                SpawnJobEquipment(jobId, args.SpawnResult.Value);
                Log.Info($"CLF Spawn System: Spawned guerilla {jobId} at colony civilian spawn point");
                return;
            }

            // Fall back to safehouse if no civilian spawn points found
            Log.Warning("CLF Spawn System: No colony civilian spawn points found, falling back to safehouse for guerilla.");
        }

        // Command roles always spawn here; guerillas that rolled safehouse (34%) or had no civilian points also land here
        args.SpawnResult = _stationSpawning.SpawnPlayerMob(
            _chosenSafehouseLocation.Value,
            args.Job,
            args.HumanoidCharacterProfile,
            args.Station);
        SpawnJobEquipment(jobId, args.SpawnResult.Value);
        Log.Info($"CLF Spawn System: Spawned {(isCommand ? "command" : "guerilla")} {jobId} at safehouse");
    }

    private void SpawnJobEquipment(string jobId, EntityUid mob)
    {
        if (!string.Equals(jobId, ClfSurgeonJobId, StringComparison.OrdinalIgnoreCase))
            return;

        var coordinates = Transform(mob).Coordinates;
        foreach (var protoId in ClfSurgeonRoundstartEquipment)
        {
            _entityManager.SpawnEntity(protoId, coordinates);
        }
    }

    /// <summary>
    /// Finds a random spawn point entity whose SpawnPointComponent.Job matches the colony civilian job ID.
    /// </summary>
    private EntityCoordinates? GetRandomColonyCivilianSpawnPoint()
    {
        var candidates = new List<EntityCoordinates>();
        var spawnPoints = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
        while (spawnPoints.MoveNext(out var _, out var sp, out var xform))
        {
            if (sp.Job != null &&
                string.Equals(sp.Job.ToString(), ColonyCivilianJobId, StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(xform.Coordinates);
            }
        }

        return candidates.Count > 0 ? _random.Pick(candidates) : null;
    }

    private void SpawnAdditionalEntities()
    {
        if (_chosenSafehouseLocation == null)
            return;

        // Get CLF spawn config
        if (!_prototypeManager.TryIndex<CLFSpawnConfigPrototype>("CLFSpawnConfig", out var config))
        {
            Log.Info("CLF Spawn System: No CLFSpawnConfig found, skipping additional entity spawning");
            return;
        }

        // Spawn each configured entity at the chosen safehouse
        foreach (var protoId in config.additionalItems)
        {
            try
            {
                _entityManager.SpawnEntity(protoId, _chosenSafehouseLocation.Value);
                Log.Info($"CLF Spawn System: Spawned additional entity {protoId} at safehouse");
            }
            catch (Exception ex)
            {
                Log.Error($"CLF Spawn System: Failed to spawn entity {protoId}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Gets the chosen safehouse location for the current round (if any)
    /// </summary>
    public EntityCoordinates? GetChosenSafehouse() => _chosenSafehouseLocation;
}



