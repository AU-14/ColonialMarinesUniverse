using System.Linq;
using Content.Server.AU14.Round;
using Content.Server.AU14.Scenario;
using Content.Server.GameTicking;
using Content.Server.Spawners.Components;
using Content.Server.Spawners.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared._CMU14.Threats.Mobs.CLF;
using Content.Shared.AU14;
using Content.Shared.AU14.Scenario;
using Content.Shared.GameTicking;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._CMU14.Threats.Mobs.CLF;

/// <summary>
///     Handles CLF spawning at round start (at a chosen safehouse) and additional entity spawning.
///     Command roles (Cell Leader, Physician, Surgeon) always spawn at the safehouse.
///     Guerilla roles have a 66% chance to spawn at colony civilian spawn points and 34% at the safehouse.
/// </summary>
public sealed partial class ClfSpawnSystem : EntitySystem
{
    [Dependency] private AuRoundSystem _auRound = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ScenarioPlanSystem _scenarioPlan = default!;
    [Dependency] private StationSpawningSystem _stationSpawning = default!;
    [Dependency] private GameTicker _ticker = default!;

    private static readonly ProtoId<CLFSpawnConfigPrototype> ClfSpawnConfig = "CLFSpawnConfig";

    /// <summary>
    ///     CLF command job IDs that always spawn at the safehouse.
    /// </summary>
    private static readonly HashSet<string> CommandJobIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "AU14JobCLFCellLeader",
        "AU14JobCLFPhysician",
        "AU14JobCLFSurgeon"
    };

    private static readonly string[] ClfSurgeonRoundstartEquipment =
    {
        "CMPortableSurgicalBedSpawnFolded",
        "RMCSurgicalTray"
    };

    private const string ClfSafehouseBucket = "CLFSafehouse";
    private const string ClfCivilianBackupBucket = "CLFCivilianBackup";

    /// <summary>
    ///     The colony civilian job whose spawn points guerillas may use.
    /// </summary>
    private const string ColonyCivilianJobId = "AU14JobCivilianColonist";

    private const string ClfSurgeonJobId = "AU14JobCLFSurgeon";

    /// <summary>
    ///     Chance (0-1) for a guerilla to spawn at a colony civilian spawn point instead of the safehouse.
    /// </summary>
    private const float GuerillaCivilianSpawnChance = 0.66f;

    private EntityCoordinates? _chosenSafehouseLocation;
    private bool _hasSpawnedAdditionalEntities;
    private ResolvedClfSpawnMarkerSet? _resolvedClfMarkers;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<PlayerSpawningEvent>(OnPlayerSpawning, new[] { typeof(SpawnPointSystem) });
        SubscribeLocalEvent<RulePlayerSpawningEvent>(OnRulePlayerSpawning);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _chosenSafehouseLocation = null;
        _hasSpawnedAdditionalEntities = false;
        _resolvedClfMarkers = null;
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
        var jobId = args.Job?.ToString();

        if (string.IsNullOrEmpty(jobId) || !jobId.Contains("CLF", StringComparison.OrdinalIgnoreCase))
            return;

        // Already handled by another system
        if (args.SpawnResult != null)
            return;

        // Choose safehouse location if not already chosen
        if (_chosenSafehouseLocation == null)
        {
            List<EntityUid> safehouseMarkers = GetSafehouseMarkers();

            if (safehouseMarkers.Count > 0)
            {
                EntityUid chosenMarker = _random.Pick(safehouseMarkers);
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
            EntityCoordinates? civilianSpawnLocation = GetRandomColonyCivilianSpawnPoint();
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
            Log.Warning(
                "CLF Spawn System: No colony civilian spawn points found, falling back to safehouse for guerilla.");
        }

        // Command roles always spawn here; guerillas that rolled safehouse (34%) or had no civilian points also land
        // here
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

        EntityCoordinates coordinates = Transform(mob).Coordinates;
        foreach (string protoId in ClfSurgeonRoundstartEquipment)
        {
            _entityManager.SpawnEntity(protoId, coordinates);
        }
    }

    /// <summary>
    ///     Finds a random spawn point entity whose SpawnPointComponent.Job matches the colony civilian job ID.
    /// </summary>
    private EntityCoordinates? GetRandomColonyCivilianSpawnPoint()
    {
        if (_resolvedClfMarkers != null &&
            _resolvedClfMarkers.TryGetMarkers(ClfCivilianBackupBucket,
                out IReadOnlyList<EntityUid> scenarioMarkers))
        {
            List<EntityUid> scenarioCandidates = FilterCivilianSpawnMarkers(scenarioMarkers);

            if (scenarioCandidates.Count > 0)
                return Transform(_random.Pick(scenarioCandidates)).Coordinates;

            Log.Warning(
                "CLF Spawn System: Scenario Plan CLF civilian markers resolved but no live civilian spawn markers remained.");
        }

        var candidates = new List<EntityCoordinates>();
        EntityQueryEnumerator<SpawnPointComponent, TransformComponent> spawnPoints
            = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
        while (spawnPoints.MoveNext(out _, out SpawnPointComponent? sp, out TransformComponent? xform))
        {
            if (sp.Job != null &&
                string.Equals(sp.Job.ToString(), ColonyCivilianJobId,
                    StringComparison.OrdinalIgnoreCase))
                candidates.Add(xform.Coordinates);
        }

        return candidates.Count > 0 ? _random.Pick(candidates) : null;
    }

    private List<EntityUid> GetSafehouseMarkers()
    {
        if (TryResolveScenarioPlanSpawnMarkers(out ResolvedClfSpawnMarkerSet? markerSet) &&
            markerSet != null)
        {
            if (markerSet.TryGetMarkers(ClfSafehouseBucket,
                out IReadOnlyList<EntityUid> scenarioMarkers))
            {
                List<EntityUid> markers = FilterSafehouseMarkers(scenarioMarkers);
                if (markers.Count > 0)
                {
                    _resolvedClfMarkers = markerSet;
                    Log.Debug($"CLF Spawn System: Using {markers.Count} Scenario Plan safehouse marker(s).");

                    return markers;
                }

                Log.Warning(
                    "CLF Spawn System: Scenario Plan safehouse markers resolved but no live safehouse markers remained.");
            }
            else
                Log.Warning("CLF Spawn System: Scenario Plan CLF markers resolved without a safehouse bucket.");
        }

        _resolvedClfMarkers = null;

        return GetLegacySafehouseMarkers();
    }

    private List<EntityUid> GetLegacySafehouseMarkers()
    {
        var markers = new List<EntityUid>();
        EntityQueryEnumerator<SafehouseMarkerComponent> query = EntityQueryEnumerator<SafehouseMarkerComponent>();
        while (query.MoveNext(out EntityUid uid, out _))
        {
            markers.Add(uid);
        }

        if (markers.Count > 0)
            Log.Debug($"CLF Spawn System: Falling back to {markers.Count} legacy safehouse marker(s).");

        return markers;
    }

    private bool TryResolveScenarioPlanSpawnMarkers(out ResolvedClfSpawnMarkerSet? markers)
    {
        markers = null;

        try
        {
            ScenarioPlanValidationRequest request = BuildClfScenarioPlanRequest();

            if (_scenarioPlan.TryResolveClfSpawnMarkers(request, _ticker.DefaultMap, out markers,
                out string diagnostic))
                return true;

            Log.Warning($"CLF Spawn System: Could not resolve Scenario Plan CLF markers. {diagnostic}");
        }
        catch (Exception ex)
        {
            Log.Error($"CLF Spawn System: Scenario Plan CLF marker resolution threw. {ex}");
        }

        markers = null;

        return false;
    }

    private ScenarioPlanValidationRequest BuildClfScenarioPlanRequest() => new(
        _ticker.CurrentPreset?.ID ?? _ticker.Preset?.ID ?? _auRound.SelectedPreset?.ID ?? string.Empty,
        0,
        PlanetId: _auRound.GetSelectedPlanetId(),
        MapId: _auRound.GetSelectedPlanet()?.MapId,
        GovforShipId: _auRound.GetSelectedGovforShip(),
        OpforShipId: _auRound.GetSelectedOpforShip());

    private List<EntityUid> FilterSafehouseMarkers(IReadOnlyList<EntityUid> candidates)
    {
        var markers = new List<EntityUid>();
        foreach (EntityUid uid in candidates)
        {
            if (TryComp<SafehouseMarkerComponent>(uid, out _))
            {
                markers.Add(uid);

                continue;
            }

            if (HasStandaloneScenarioMarker(uid, SpawnMarkerKind.ClfSafehouse, ScenarioMarkerTags.ForceClfSafehouse))
                markers.Add(uid);
        }

        return markers;
    }

    private List<EntityUid> FilterCivilianSpawnMarkers(IReadOnlyList<EntityUid> candidates)
    {
        var markers = new List<EntityUid>();
        foreach (EntityUid uid in candidates)
        {
            if (TryComp(uid, out SpawnPointComponent? spawnPoint) &&
                spawnPoint.Job != null &&
                spawnPoint.Job.Value.Id.Equals(ColonyCivilianJobId, StringComparison.OrdinalIgnoreCase))
            {
                markers.Add(uid);

                continue;
            }

            if (HasStandaloneScenarioMarker(
                uid,
                SpawnMarkerKind.ClfCivilianSpawn,
                ScenarioMarkerTags.ClfCivilianSpawn(ColonyCivilianJobId)))
                markers.Add(uid);
        }

        return markers;
    }

    private bool HasStandaloneScenarioMarker(EntityUid uid,
        SpawnMarkerKind kind,
        string requiredTag)
    {
        if (!TryComp(uid, out ScenarioSpawnMarkerComponent? marker) ||
            marker.Kind != kind ||
            !marker.Tags.Contains(requiredTag, StringComparer.OrdinalIgnoreCase))
            return false;

        return !HasComp<SafehouseMarkerComponent>(uid) &&
            !HasComp<SpawnPointComponent>(uid);
    }

    private void SpawnAdditionalEntities()
    {
        if (_chosenSafehouseLocation == null)
            return;

        // Get CLF spawn config
        if (!_prototypeManager.TryIndex(ClfSpawnConfig, out CLFSpawnConfigPrototype? config))
        {
            Log.Info("CLF Spawn System: No CLFSpawnConfig found, skipping additional entity spawning");

            return;
        }

        // Spawn each configured entity at the chosen safehouse
        foreach (string protoId in config.additionalItems)
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
    ///     Gets the chosen safehouse location for the current round (if any)
    /// </summary>
    public EntityCoordinates? GetChosenSafehouse() => _chosenSafehouseLocation;
}
