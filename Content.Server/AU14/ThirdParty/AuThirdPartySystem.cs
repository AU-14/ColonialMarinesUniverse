using System.Linq;
using Content.Server.Access.Systems;
using Content.Server.AU14.Round;
using Content.Server.AU14.Scenario;
using Content.Server.AU14.VendorMarker;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.IdentityManagement;
using Content.Server.Preferences.Managers;
using Content.Shared._RMC14.CrashLand;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Rules;
using Content.Shared.AU14.Threats;
using Content.Shared.AU14.Scenario;
using Content.Shared.AU14.util;
using Content.Shared.Ghost;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.ParaDrop;
using Content.Shared.Players;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.AU14.ThirdParty;

public sealed partial class AuThirdPartySystem : EntitySystem
{
    private static readonly ProtoId<JobPrototype> ThirdPartyLeaderJobId = new("AU14JobThirdPartyLeader");
    private static readonly ProtoId<JobPrototype> ThirdPartyMemberJobId = new("AU14JobThirdPartyMember");
    private static readonly ThreatMarkerType[] ThreatMarkerTypes = Enum.GetValues<ThreatMarkerType>();

    private readonly record struct ThirdPartyAssignmentCounts(int Leaders, int Members);

    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private PlatoonSpawnRuleSystem _platoonSpawnRule = default!;
    [Dependency] private ScenarioPlanSystem _scenarioPlan = default!;
    [Dependency] private AuRoundSystem _auRoundSystem = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedDropshipSystem _sharedDropshipSystem = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IServerPreferencesManager _preferences = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private IdCardSystem _idCard = default!;
    [Dependency] private IdentitySystem _identity = default!;
    [Dependency] private IGameTiming _timing = default!;
    private readonly ISawmill _sawmill = Logger.GetSawmill("thirdparty");

    // --- State for round third party spawning ---
    private ThreatPrototype? _currentThreat;
    private List<AuThirdPartyPrototype>? _thirdPartyList;
    private int _nextThirdPartyIndex = 0;
    private float _spawnTimer = 0f;
    private TimeSpan _spawnInterval = TimeSpan.FromMinutes(5);
    private bool _spawningActive = false;

    // --- Signal modifier applied by Ambassador / AI Core consoles ---
    private float _signalIntervalMultiplier = 1f;

    private static ThirdPartyAssignmentCounts CountThirdPartyAssignments(
        Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)>? assignedJobs)
    {
        if (assignedJobs == null)
            return default;

        var leaders = 0;
        var members = 0;
        foreach (var (_, (job, _)) in assignedJobs)
        {
            if (job == ThirdPartyLeaderJobId)
                leaders++;
            else if (job == ThirdPartyMemberJobId)
                members++;
        }

        return new ThirdPartyAssignmentCounts(leaders, members);
    }

    private static string FormatSpawnBodies(IReadOnlyDictionary<string, int> bodies)
    {
        return bodies.Count == 0
            ? "none"
            : string.Join(", ", bodies.Select(pair => $"{pair.Key}={pair.Value}"));
    }

    /// <summary>
    /// Returns the list of queued third parties that have not yet spawned.
    /// </summary>
    public List<AuThirdPartyPrototype> GetQueuedThirdParties()
    {
        if (_thirdPartyList == null || _nextThirdPartyIndex >= _thirdPartyList.Count)
            return new List<AuThirdPartyPrototype>();

        return _thirdPartyList.GetRange(_nextThirdPartyIndex, _thirdPartyList.Count - _nextThirdPartyIndex);
    }

    /// <summary>
    /// Sets the signal interval multiplier. Below 1 = signal boost, above 1 = signal jam.
    /// </summary>
    public void SetSignalIntervalMultiplier(float multiplier)
    {
        _signalIntervalMultiplier = Math.Max(0.1f, multiplier);
    }

    public float GetSignalIntervalMultiplier() => _signalIntervalMultiplier;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!_spawningActive || _thirdPartyList == null)
            return;
        if (_nextThirdPartyIndex >= _thirdPartyList.Count)
        {
            _spawningActive = false;
            return;
        }

        _spawnTimer += frameTime;
        var party = _thirdPartyList[_nextThirdPartyIndex];
        if (party.RoundStart)
        {
            _nextThirdPartyIndex++;
            return;
        }

        var interval = TimeSpan.FromTicks((long)(_spawnInterval.Ticks * _signalIntervalMultiplier));
        if (_spawnTimer < interval.TotalSeconds)
            return;

        int ghostCount = _playerManager.Sessions.Count(s => s.AttachedEntity == null || _entityManager.HasComponent<GhostComponent>(s.AttachedEntity));
        if (ghostCount < party.GhostsNeeded)
        {
            _spawnTimer = 0f;
            return;
        }

        _spawnTimer = 0f;
        int roll = _random.Next(1, 101);
        int chance = Math.Clamp(party.weight * 10, 5, 100); // Example: weight 1 = 10%, weight 10 = 100%

        if (roll > chance)
        {
            _sawmill.Debug($"[AuThirdPartySystem] Did not spawn ({party.ID}) (roll {roll} > {chance})");
            return;
        }

        if (!_prototypeManager.TryIndex(party.PartySpawn, out var spawnProto))
        {
            _sawmill.Error($"[AuThirdPartySystem] No spawn proto for ({party.ID}) (PartySpawn={party.PartySpawn})");
            _nextThirdPartyIndex++;
            return;
        }

        try
        {
            if (SpawnThirdParty(party, spawnProto, false))
                _sawmill.Debug($"[AuThirdPartySystem] Spawned ({party.ID}) (roll {roll} <= {chance})");
            else
                _sawmill.Warning($"[AuThirdPartySystem] Spawn of ({party.ID}) failed; skipping.");
        }
        catch (Exception ex) { _sawmill.Error($"[AuThirdPartySystem] Exception spawning ({party.ID}): {ex}"); }
        _nextThirdPartyIndex++;
    }

    public bool SpawnThirdParty(AuThirdPartyPrototype party, PartySpawnPrototype spawnProto, bool roundStart, Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)>? assignedJobs = null, bool? overrideDropship = null)
    {
        const float SpawnTogetherRadius = 8f;
        _sawmill.Debug($"[AuThirdPartySystem] Spawning third party: ({party.ID})");
        if (spawnProto == null)
        {
            _sawmill.Error($"[AuThirdPartySystem] Spawn called with null spawnProto for party ({party.ID})!");
            return false;
        }

        // Determine entry method. If overrideDropship is provided, it takes precedence (true => shuttle, false => ground).
        var entryMethod = overrideDropship.HasValue
            ? (overrideDropship.Value ? "shuttle" : "ground")
            : (string.IsNullOrWhiteSpace(party.EntryMethod) ? "ground" : party.EntryMethod);
        _sawmill.Debug($"[AuThirdPartySystem] Entry method: {entryMethod} (overrideDropship={overrideDropship})");
        var presetId = _auRoundSystem.SelectedPreset?.ID ?? string.Empty;
        var coveredScenarioForce = _scenarioPlan.HasMappedThirdPartyRoundGroup(presetId, party.ID);
        if (_sawmill.Level <= Robust.Shared.Log.LogLevel.Debug)
        {
            var assignmentCounts = CountThirdPartyAssignments(assignedJobs);
            _sawmill.Debug(
                $"[AuThirdPartySystem] Spawn context: party={party.ID}, spawnProto={spawnProto.ID}, roundStart={roundStart}, preset={presetId}, threat={_currentThreat?.ID ?? "null"}, entryMethod={entryMethod}, coveredScenarioForce={coveredScenarioForce}, assignedJobs={assignedJobs?.Count ?? 0}, assignedThirdPartyLeaders={assignmentCounts.Leaders}, assignedThirdPartyMembers={assignmentCounts.Members}.");
        }

        List<EntityUid> markerEntities = new();
        var markerEntitySet = new HashSet<EntityUid>();
        EntityUid mainGridUid = EntityUid.Invalid;
        bool parachuteMode = false;

        void AddMarkerEntity(EntityUid uid)
        {
            if (markerEntitySet.Add(uid))
                markerEntities.Add(uid);
        }

        // Maintain compatibility with existing code that uses these locals.
        bool useDropship = entryMethod.Equals("shuttle", StringComparison.OrdinalIgnoreCase);
        if (useDropship)
        {
            // Dropship step (existing behavior)
            EntityUid? chosenDestination = null;
            var destQuery = _entityManager.EntityQueryEnumerator<DropshipDestinationComponent, TransformComponent>();
            while (destQuery.MoveNext(out var destUid, out var destComp, out var destXform))
            {
                if (destComp.Ship == null && string.IsNullOrEmpty(destComp.FactionController))
                {
                    chosenDestination = destUid;
                    break;
                }
            }

            if (chosenDestination == null)
            {
                _sawmill.Error("[AuThirdPartySystem] No valid dropship destination found (not landed, not controlled). Aborting third party spawn.");
                return false;
            }

            var destination = chosenDestination.Value;
            _sawmill.Debug($"[AuThirdPartySystem] Found valid dropship destination: {destination}");

            var deserializationOpts = DeserializationOptions.Default with { InitializeMaps = true };
            if (!TryLoadDropshipGrid(party.dropshippath, deserializationOpts, out mainGridUid))
                return false;

            _sawmill.Debug($"[AuThirdPartySystem] Dropship grid initialized: {mainGridUid}");

            var dropshipMapCoordinates = _transform.ToMapCoordinates(_entityManager.GetComponent<TransformComponent>(mainGridUid).Coordinates);
            EntityUid returnDestination;
            try
            {
                returnDestination = _entityManager.SpawnEntity(
                    "CMDropshipDestinationThirdPartyReturn",
                    dropshipMapCoordinates);
            }
            catch (Exception ex)
            {
                _sawmill.Error($"[AuThirdPartySystem] Failed to spawn return destination entity at {dropshipMapCoordinates}: {ex}");
                return false;
            }
            var returnDestinationComp = EnsureComp<ThirdPartyDropshipReturnDestinationComponent>(returnDestination);
            returnDestinationComp.Shuttle = mainGridUid;

            EnsureComp<DropshipDestinationComponent>(returnDestination);
            _sharedDropshipSystem.SetDestinationShip(returnDestination, mainGridUid);
            _sharedDropshipSystem.SetDestinationHome(returnDestination, true);

            EnsureComp<DropshipComponent>(mainGridUid);
            _sharedDropshipSystem.SetDropshipDestination(mainGridUid, returnDestination);

            var navQuery = _entityManager.EntityQueryEnumerator<DropshipNavigationComputerComponent, TransformComponent>();
            EntityUid? navUid = null;
            DropshipNavigationComputerComponent? navComp = null;
            while (navQuery.MoveNext(out var uid, out var comp, out var xform))
            {
                if (xform.ParentUid == mainGridUid)
                {
                    navUid = uid;
                    navComp = comp;
                    break;
                }
            }

            if (navUid != null && navComp != null)
            {
                var navEntity = new Entity<DropshipNavigationComputerComponent>(navUid.Value, navComp);
                _sharedDropshipSystem.FlyTo(navEntity, destination, null);
                _sawmill.Debug($"[AuThirdPartySystem] Commanded dropship nav computer {navUid} to fly to destination {destination}");
            }
            else
                _sawmill.Warning($"[AuThirdPartySystem] Could not find navigation computer on dropship grid {mainGridUid}; the dropship may not be able to travel.");

            // Collect markers on dropship grid
            var query = _entityManager.EntityQueryEnumerator<AuInsertMarkerComponent>();
            while (query.MoveNext(out var uid, out _))
            {
                if (_entityManager.TryGetComponent(uid, out TransformComponent? transform) &&
                    IsOnGrid(transform, mainGridUid))
                {
                    AddMarkerEntity(uid);
                }
            }

            var legacyMarkerQuery = _entityManager.EntityQueryEnumerator<ThreatSpawnMarkerComponent, TransformComponent>();
            while (legacyMarkerQuery.MoveNext(out var uid, out var marker, out var transform))
            {
                if (!marker.ThirdParty ||
                    !IsOnGrid(transform, mainGridUid))
                {
                    continue;
                }

                AddMarkerEntity(uid);
            }

            var scenarioMarkerQuery = _entityManager.EntityQueryEnumerator<ScenarioSpawnMarkerComponent, TransformComponent>();
            while (scenarioMarkerQuery.MoveNext(out var uid, out _, out var transform))
            {
                if (!HasStandaloneThirdPartyMarker(uid) ||
                    !IsOnGrid(transform, mainGridUid))
                {
                    continue;
                }

                AddMarkerEntity(uid);
            }

            _sawmill.Debug($"[AuThirdPartySystem] Dropship markers collected: {markerEntities.Count}");

            // Spawn consoles
            var vmarkerQuery = _entityManager.EntityQueryEnumerator<VendorMarkerComponent>();
            int consoleCount = 0;
            while (vmarkerQuery.MoveNext(out var vmarkerUid, out var vmarkerComp))
            {
                try
                {
                    var markerXform = _entityManager.GetComponent<TransformComponent>(vmarkerUid);
                    if (markerXform.GridUid != mainGridUid)
                        continue;

                    switch (vmarkerComp.Class)
                    {
                        case PlatoonMarkerClass.DSPilot:
                            try
                            {
                                _entityManager.SpawnEntity("CMComputerDropshipNavigationThirdParty", markerXform.Coordinates);
                                consoleCount++;
                            }
                            catch (Exception ex) { _sawmill.Error($"[AuThirdPartySystem] Failed to spawn Dropship Navigation console: {ex}"); }
                            break;
                        case PlatoonMarkerClass.DSWeapons:
                            try
                            {
                                _entityManager.SpawnEntity("CMComputerDropshipWeapons", markerXform.Coordinates);
                                consoleCount++;
                            }
                            catch (Exception ex) { _sawmill.Error($"[AuThirdPartySystem] Failed to spawn Dropship Weapons console: {ex}"); }
                            break;
                    }
                }
                catch (Exception ex) { _sawmill.Debug($"[AuThirdPartySystem] Skipping vendor marker {vmarkerUid} (class={vmarkerComp.Class}) due to component error: {ex}"); }
            }
            _sawmill.Debug($"[AuThirdPartySystem] Dropship consoles spawned: {consoleCount}");
        }
        else if (entryMethod.Equals("parachute", StringComparison.OrdinalIgnoreCase))
        {
            // Parachute mode: collect parachute markers on the main map
            parachuteMode = true;
            var pQuery = _entityManager.EntityQueryEnumerator<ParachuteMarkerComponent, TransformComponent>();
            while (pQuery.MoveNext(out var uid, out var pComp, out var pxform))
            {
                // Parachute markers are reusable and do not need to be marked as used; include all of them.
                AddMarkerEntity(uid);
            }
            _sawmill.Debug($"[AuThirdPartySystem] Parachute markers collected: {markerEntities.Count}");
        }
        else
        {
            // Ground spawn: collect all markers on main map (existing behavior)
            var query = _entityManager.EntityQueryEnumerator<AuInsertMarkerComponent>();
            while (query.MoveNext(out var uid, out _))
            {
                AddMarkerEntity(uid);
            }

            var legacyMarkerQuery = _entityManager.EntityQueryEnumerator<ThreatSpawnMarkerComponent>();
            while (legacyMarkerQuery.MoveNext(out var uid, out var marker))
            {
                if (!marker.ThirdParty)
                    continue;

                AddMarkerEntity(uid);
            }

            var scenarioMarkerQuery = _entityManager.EntityQueryEnumerator<ScenarioSpawnMarkerComponent>();
            while (scenarioMarkerQuery.MoveNext(out var uid, out _))
            {
                if (!HasStandaloneThirdPartyMarker(uid))
                    continue;

                AddMarkerEntity(uid);
            }

            _sawmill.Debug($"[AuThirdPartySystem] Main map third-party markers collected: {markerEntities.Count}");
        }

        var candidateMapIds = new List<MapId>();
        var candidateMapIdSet = new HashSet<MapId>();
        foreach (var marker in markerEntities)
        {
            if (!_entityManager.TryGetComponent(marker, out TransformComponent? markerTransform) ||
                !candidateMapIdSet.Add(markerTransform.MapID))
            {
                continue;
            }

            candidateMapIds.Add(markerTransform.MapID);
        }

        MapId? mapId = candidateMapIds.Count > 0
            ? candidateMapIds[0]
            : null;
        if (_sawmill.Level <= Robust.Shared.Log.LogLevel.Debug)
        {
            _sawmill.Debug(
                $"[AuThirdPartySystem] Candidate marker maps for third party ({party.ID}): count={candidateMapIds.Count}, maps=[{string.Join(", ", candidateMapIds)}], initialMap={mapId?.ToString() ?? "null"}.");
        }
        ResolvedThirdPartySpawnMarkerSet? scenarioMarkers = null;
        foreach (var spawnMapId in candidateMapIds)
        {
            if (!TryResolveScenarioPlanSpawnMarkers(
                party,
                spawnMapId,
                assignedJobs,
                out scenarioMarkers,
                logFailure: false,
                coveredScenarioForce))
            {
                continue;
            }

            mapId = spawnMapId;
            break;
        }

        if (scenarioMarkers == null &&
            mapId is { } fallbackMapId)
        {
            TryResolveScenarioPlanSpawnMarkers(
                party,
                fallbackMapId,
                assignedJobs,
                out scenarioMarkers,
                coveredScenarioForce: coveredScenarioForce);
        }

        if (scenarioMarkers == null && coveredScenarioForce)
        {
            _sawmill.Error(
                $"[AuThirdPartySystem] Covered Round Group for third party '{party.ID}' resolved without live Spawn Markers; aborting authoritative Scenario Plan third-party spawn instead of using legacy marker lookup.");
            return false;
        }

        var markerCache = new Dictionary<ThreatMarkerType, List<EntityUid>>();

        List<EntityUid> GetMarkers(ThreatMarkerType markerType)
        {
            if (markerCache.TryGetValue(markerType, out var cachedMarkers))
                return cachedMarkers;

            var markers = ResolveMarkers(markerType);
            markerCache[markerType] = markers;
            return markers;
        }

        List<EntityUid> ResolveMarkers(ThreatMarkerType markerType)
        {
            var time = _timing.CurTime;
            if (scenarioMarkers != null &&
                scenarioMarkers.TryGetMarkers(markerType.ToString(), out var plannedMarkers))
            {
                var gridUid = useDropship && mainGridUid != EntityUid.Invalid
                    ? mainGridUid
                    : (EntityUid?) null;
                var filteredScenarioMarkers = FilterScenarioMarkers(markerType, plannedMarkers, time, mapId, gridUid);
                _sawmill.Debug($"[AuThirdPartySystem] GetMarkers({markerType}): Using {filteredScenarioMarkers.Count} Scenario Plan marker(s) on map {mapId}");
                if (filteredScenarioMarkers.Count > 0 || !useDropship)
                    return filteredScenarioMarkers;

                if (coveredScenarioForce)
                    return filteredScenarioMarkers;

                _sawmill.Warning($"[AuThirdPartySystem] Scenario Plan resolved no dropship grid markers for {markerType} on grid {mainGridUid}; falling back to legacy marker lookup.");
            }

            var markerId = spawnProto.Markers.TryGetValue(markerType, out var id) ? id : "";
            var legacyMarkers = new List<EntityUid>();
            var query = _entityManager.EntityQueryEnumerator<ThreatSpawnMarkerComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                // Only include markers that are of the requested type, match the optional marker ID,
                // are explicitly marked as ThirdParty, and are unused - and aren't on a Cooldown
                if (comp.ThreatMarkerType != markerType
                        || !(comp.ID == markerId || (comp.ID == "" && markerId == ""))
                        || !comp.ThirdParty
                        || comp.NextAvailableAt > time)
                    continue;

                if (useDropship && mainGridUid != EntityUid.Invalid)
                {
                    if (!_entityManager.TryGetComponent<TransformComponent>(uid, out var tcomp) || !tcomp.GridUid.HasValue || tcomp.GridUid.Value != mainGridUid)
                        continue;
                }
                else
                {
                    // Otherwise, ensure we are on the same map (if mapId set).
                    if (mapId != null && _entityManager.GetComponent<TransformComponent>(uid).MapID != mapId)
                        continue;
                }

                // Only include markers that are not already used
                //if (!comp.Used) // <- now handled by Cooldowns
                legacyMarkers.Add(uid);
            }

            _sawmill.Debug($"[AuThirdPartySystem] GetMarkers({markerType}): Found {legacyMarkers.Count} unused markers with markerId '{markerId}' on map {mapId}");
            return legacyMarkers;
        }

        bool spawnTogether = spawnProto.SpawnTogether == true;
        Dictionary<ThreatMarkerType, List<EntityUid>> spawnTogetherMarkers = new();
        if (spawnTogether)
        {
            var allMarkers = new List<EntityUid>();
            foreach (var type in ThreatMarkerTypes)
            {
                allMarkers.AddRange(GetMarkers(type));
            }
            if (allMarkers.Count > 0)
            {
                var centerMarker = allMarkers[_random.Next(allMarkers.Count)];
                var centerCoords = _entityManager.GetComponent<TransformComponent>(centerMarker).Coordinates;
                foreach (var type in ThreatMarkerTypes)
                {
                    var markers = GetMarkers(type);
                    var filtered = new List<EntityUid>();
                    foreach (var marker in markers)
                    {
                        var coords = _entityManager.GetComponent<TransformComponent>(marker).Coordinates;
                        if (_transform.InRange(coords, centerCoords, 50f))
                            filtered.Add(marker);
                    }

                    // Fallback to all markers if none are in range
                    spawnTogetherMarkers[type] = filtered.Count > 0 ? filtered : markers;
                }
            }
        }

        List<EntityUid> GetSpawnMarkers(ThreatMarkerType type)
        {
            if (spawnTogether && spawnTogetherMarkers.TryGetValue(type, out var cached))
                return cached;
            return GetMarkers(type);
        }

        var spawnedLeaders = new List<EntityUid>();
        var spawnedGrunts = new List<EntityUid>();
        var spawnedEnts = new List<EntityUid>();
        // Track the last marker we used during this spawn operation
        EntityUid? lastUsedMarker = null;
        var leaderBodies = GetSpawnBodies(
            scenarioMarkers?.Force.SpawnPlan,
            ThreatMarkerType.Leader,
            spawnProto.LeadersToSpawn);
        var gruntBodies = GetSpawnBodies(
            scenarioMarkers?.Force.SpawnPlan,
            ThreatMarkerType.Member,
            spawnProto.GruntsToSpawn);
        var entityBodies = GetSpawnBodies(
            scenarioMarkers?.Force.SpawnPlan,
            ThreatMarkerType.Entity,
            spawnProto.EntitiesToSpawn);
        var leaderReq = leaderBodies.Values.Sum();
        var gruntReq = gruntBodies.Values.Sum();
        var entityReq = entityBodies.Values.Sum();

        var leaderMarkers = GetSpawnMarkers(ThreatMarkerType.Leader);
        var gruntMarkers = GetSpawnMarkers(ThreatMarkerType.Member);
        var entityMarkers = GetSpawnMarkers(ThreatMarkerType.Entity);
        if (_sawmill.Level <= Robust.Shared.Log.LogLevel.Debug)
        {
            _sawmill.Debug(
                $"[AuThirdPartySystem] Spawn plan for third party ({party.ID}): force={scenarioMarkers?.Force.ForceId ?? "legacy"}, leaders[{FormatSpawnBodies(leaderBodies)}] requested={leaderReq} markers={leaderMarkers.Count}, grunts[{FormatSpawnBodies(gruntBodies)}] requested={gruntReq} markers={gruntMarkers.Count}, entities[{FormatSpawnBodies(entityBodies)}] requested={entityReq} markers={entityMarkers.Count}.");
        }

        if (leaderReq > 0 && leaderMarkers.Count == 0)
            _sawmill.Warning($"[AuThirdPartySystem] Third party ({party.ID}) requested {leaderReq} leader body/bodies but found no leader markers.");
        if (gruntReq > 0 && gruntMarkers.Count == 0)
            _sawmill.Warning($"[AuThirdPartySystem] Third party ({party.ID}) requested {gruntReq} grunt body/bodies but found no member markers.");
        if (entityReq > 0 && entityMarkers.Count == 0)
            _sawmill.Warning($"[AuThirdPartySystem] Third party ({party.ID}) requested {entityReq} entity spawn(s) but found no entity markers.");

        List<EntityUid> FilterByType(ThreatMarkerType type)
        {
            var filtered = new List<EntityUid>();
            foreach (var marker in markerEntities)
            {
                if (_entityManager.TryGetComponent<ThreatSpawnMarkerComponent>(marker, out var comp) &&
                    comp.ThirdParty &&
                    comp.ThreatMarkerType == type)
                {
                    filtered.Add(marker);
                }
            }

            return filtered;
        }

        // If parachute mode, use the parachute marker pool for all types; make local mutable copies so we can pick without replacement during this spawn
        if (parachuteMode && scenarioMarkers == null)
        {
            // Parachute markers must still have a ThreatSpawnMarkerComponent with ThirdParty==true
            leaderMarkers = FilterByType(ThreatMarkerType.Leader);
            gruntMarkers = FilterByType(ThreatMarkerType.Member);
            entityMarkers = FilterByType(ThreatMarkerType.Entity);
        }

        // If this is a groundside spawn, ensure there are enough *safe* markers (unused and not near alive players).
        if (!useDropship)
        {
            var safeLeaderMarkers = FilterSafeMarkers(leaderMarkers);
            var safeGruntMarkers = FilterSafeMarkers(gruntMarkers);
            var safeEntityMarkers = FilterSafeMarkers(entityMarkers);

            if (safeLeaderMarkers.Count < leaderReq || safeGruntMarkers.Count < gruntReq || safeEntityMarkers.Count < entityReq)
            {
                _sawmill.Warning($"[AuThirdPartySystem] Not enough safe markers to spawn third party ({party.ID}): leaders needed {leaderReq}, safe available {safeLeaderMarkers.Count}; grunts needed {gruntReq}, safe available {safeGruntMarkers.Count}; entities needed {entityReq}, safe available {safeEntityMarkers.Count}. Aborting spawn.");
                return false;
            }

            // Replace marker pools with safe lists so subsequent selection never picks an unsafe marker.
            leaderMarkers = safeLeaderMarkers;
            gruntMarkers = safeGruntMarkers;
            entityMarkers = safeEntityMarkers;
        }
        else
        {
            // For dropship spawns we still require unused markers, as before
            if (leaderMarkers.Count < leaderReq || gruntMarkers.Count < gruntReq || entityMarkers.Count < entityReq)
            {
                _sawmill.Warning($"[AuThirdPartySystem] Not enough unused dropship markers to spawn third party ({party.ID}): leaders needed {leaderReq}, available {leaderMarkers.Count}; grunts needed {gruntReq}, available {gruntMarkers.Count}; entities needed {entityReq}, available {entityMarkers.Count}. Aborting spawn.");
                return false;
            }
        }
        _sawmill.Debug(
            $"[AuThirdPartySystem] Final marker pools for third party ({party.ID}): leaders={leaderMarkers.Count}, grunts={gruntMarkers.Count}, entities={entityMarkers.Count}, useDropship={useDropship}, parachuteMode={parachuteMode}.");

        List<EntityUid> FilterSafeMarkers(List<EntityUid> markers)
        {
            var safeMarkers = new List<EntityUid>(markers.Count);
            foreach (var marker in markers)
            {
                if (!IsMarkerBlockedByPlayers(marker))
                    safeMarkers.Add(marker);
            }

            return safeMarkers;
        }

        // Spawn leaders
        _sawmill.Debug($"[AuThirdPartySystem] Spawning leaders...");
        foreach (var (protoId, count) in leaderBodies)
            for (int i = 0; i < count; i++)
                if (!TrySpawnAtMarker(protoId, leaderMarkers, spawnedLeaders, parachuteMode, useDropship, "leader", ref lastUsedMarker))
                    _sawmill.Warning($"[AuThirdPartySystem] Failed to spawn leader {protoId}");

        // Spawn grunts
        _sawmill.Debug($"[AuThirdPartySystem] Spawning grunts...");
        foreach (var (protoId, count) in gruntBodies)
            for (int i = 0; i < count; i++)
                if (!TrySpawnAtMarker(protoId, gruntMarkers, spawnedGrunts, parachuteMode, useDropship, "grunt", ref lastUsedMarker))
                    _sawmill.Warning($"[AuThirdPartySystem] Failed to spawn grunt {protoId}");

        // Spawn ents
        _sawmill.Debug($"[AuThirdPartySystem] Spawning ents...");
        foreach (var (protoId, count) in entityBodies)
            for (int i = 0; i < count; i++)
                if (!TrySpawnAtMarker(protoId, entityMarkers, spawnedEnts, parachuteMode, useDropship, "ent", ref lastUsedMarker))
                    _sawmill.Warning($"[AuThirdPartySystem] Failed to spawn entity {protoId}");

        _sawmill.Info(
            $"[AuThirdPartySystem] Third-party spawn result for ({party.ID}): spawnedLeaders={spawnedLeaders.Count}/{leaderReq}, spawnedGrunts={spawnedGrunts.Count}/{gruntReq}, spawnedEntities={spawnedEnts.Count}/{entityReq}.");

        // After all spawns: if spawnTogether is true, mark nearby unused markers around the last used marker.
        void MarkNeighborsIfNeeded()
        {
            if (!spawnTogether || lastUsedMarker == null)
                return;

            var centerMarkerUid = lastUsedMarker.Value;
            if (!_entityManager.HasComponent<ThreatSpawnMarkerComponent>(centerMarkerUid) &&
                !_entityManager.HasComponent<ScenarioSpawnMarkerCooldownComponent>(centerMarkerUid))
            {
                return;
            }

            var centerXform = _entityManager.GetComponent<TransformComponent>(centerMarkerUid);
            var centerCoords = centerXform.Coordinates;
            var centerMap = centerXform.MapID;

            var query = _entityManager.EntityQueryEnumerator<ThreatSpawnMarkerComponent>();
            while (query.MoveNext(out var otherUid, out var _))
            {
                if (otherUid == centerMarkerUid)
                    continue;

                var otherXform = _entityManager.GetComponent<TransformComponent>(otherUid);
                if (otherXform.MapID != centerMap)
                    continue;

                if (_transform.InRange(otherXform.Coordinates, centerCoords, SpawnTogetherRadius))
                {
                    if (_entityManager.TryGetComponent<ThreatSpawnMarkerComponent>(otherUid, out var otherComp))
                    {
                        otherComp.NextAvailableAt = _timing.CurTime + otherComp.Cooldown;
                        Dirty(otherUid, otherComp);
                    }
                }
            }

            var scenarioQuery = _entityManager.EntityQueryEnumerator<ScenarioSpawnMarkerCooldownComponent, TransformComponent>();
            while (scenarioQuery.MoveNext(out var otherUid, out _, out var otherXform))
            {
                if (otherUid == centerMarkerUid ||
                    otherXform.MapID != centerMap ||
                    !HasStandaloneThirdPartyMarker(otherUid))
                {
                    continue;
                }

                if (_transform.InRange(otherXform.Coordinates, centerCoords, SpawnTogetherRadius))
                    ApplyScenarioMarkerCooldown(otherUid);
            }
        }

        // Run neighbor-marking now (only once per spawn operation, using the last used marker)
        MarkNeighborsIfNeeded();

        if (roundStart && assignedJobs != null)
        {
            var leaderPlayers = new List<NetUserId>();
            var memberPlayers = new List<NetUserId>();
            foreach (var (player, (job, _)) in assignedJobs)
            {
                if (job == ThirdPartyLeaderJobId)
                    leaderPlayers.Add(player);
                else if (job == ThirdPartyMemberJobId)
                    memberPlayers.Add(player);
            }

            _sawmill.Debug($"[AuThirdPartySystem] Assigning minds to third party entities (roundstart)");
            AssignMinds(leaderPlayers, spawnedLeaders, ThirdPartyLeaderJobId.Id, "leader");
            AssignMinds(memberPlayers, spawnedGrunts, ThirdPartyMemberJobId.Id, "member");
        }

        if (!string.IsNullOrWhiteSpace(party.AnnounceArrival))
        {
            _chat.DispatchGlobalAnnouncement(party.AnnounceArrival, "", playSound: false, colorOverride: Color.DarkOrange);
            _sawmill.Info($"[AuThirdPartySystem] Announced arrival for third party ({party.ID}): {party.AnnounceArrival}");
        }

        return true;
    }

    private static IReadOnlyDictionary<string, int> GetSpawnBodies(
        ResolvedSpawnPlan? spawnPlan,
        ThreatMarkerType markerType,
        IReadOnlyDictionary<string, int> legacyBodies)
    {
        var bucket = spawnPlan?.BodyBuckets.FirstOrDefault(bodyBucket =>
            bodyBucket.Bucket.Equals(markerType.ToString(), StringComparison.OrdinalIgnoreCase));
        if (bucket != null &&
            (bucket.Bodies.Count > 0 || bucket.Count == 0))
        {
            return bucket.Bodies;
        }

        return legacyBodies
            .ToDictionary(
                body => body.Key,
                body => Math.Max(0, body.Value),
                StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsOnGrid(TransformComponent transform, EntityUid gridUid)
    {
        return (transform.GridUid.HasValue && transform.GridUid.Value == gridUid) ||
               transform.ParentUid == gridUid;
    }

    private List<EntityUid> FilterScenarioMarkers(
        ThreatMarkerType markerType,
        IReadOnlyList<EntityUid> candidateMarkers,
        TimeSpan time,
        MapId? mapId,
        EntityUid? gridUid)
    {
        var filteredMarkers = new List<EntityUid>();
        foreach (var uid in candidateMarkers)
        {
            if (!_entityManager.TryGetComponent(uid, out TransformComponent? transform))
                continue;

            if (mapId != null && transform.MapID != mapId)
            {
                continue;
            }

            if (gridUid != null &&
                !IsOnGrid(transform, gridUid.Value))
            {
                continue;
            }

            if (_entityManager.TryGetComponent(uid, out ThreatSpawnMarkerComponent? marker))
            {
                if (marker.ThreatMarkerType != markerType ||
                    !marker.ThirdParty ||
                    marker.NextAvailableAt > time)
                {
                    continue;
                }

                filteredMarkers.Add(uid);
                continue;
            }

            if (HasStandaloneThirdPartyMarker(uid, markerType) &&
                IsScenarioMarkerAvailable(uid, time))
            {
                filteredMarkers.Add(uid);
            }
        }

        return filteredMarkers;
    }

    private bool HasStandaloneThirdPartyMarker(EntityUid uid, ThreatMarkerType? markerType = null)
    {
        if (!_entityManager.TryGetComponent(uid, out ScenarioSpawnMarkerComponent? marker) ||
            marker.Kind != SpawnMarkerKind.ThirdPartyMarker ||
            !marker.Tags.Contains(ScenarioMarkerTags.ForceThirdParty, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        if (markerType != null &&
            !marker.Tags.Contains(ScenarioMarkerTags.Bucket(markerType.Value.ToString()), StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return !_entityManager.HasComponent<ThreatSpawnMarkerComponent>(uid);
    }

    private bool TryLoadDropshipGrid(ResPath path, DeserializationOptions options, out EntityUid gridUid)
    {
        gridUid = EntityUid.Invalid;
        var loadOptions = new MapLoadOptions
        {
            DeserializationOptions = options with { LogOrphanedGrids = false },
        };

        if (!_mapLoader.TryLoadGeneric(path, out var result, loadOptions))
        {
            _sawmill.Error($"[AuThirdPartySystem] Failed to load dropship map or grid: {path}");
            return false;
        }

        gridUid = result.Grids.FirstOrDefault();
        if (gridUid != EntityUid.Invalid)
            return true;

        _mapLoader.Delete(result);
        _sawmill.Error($"[AuThirdPartySystem] No grids found in dropship map or grid: {path}");
        return false;
    }

    private bool IsScenarioMarkerAvailable(EntityUid uid, TimeSpan time)
    {
        return !_entityManager.TryGetComponent(uid, out ScenarioSpawnMarkerCooldownComponent? cooldown) ||
               cooldown.NextAvailableAt <= time;
    }

    private void ApplyScenarioMarkerCooldown(EntityUid uid)
    {
        if (!_entityManager.TryGetComponent(uid, out ScenarioSpawnMarkerCooldownComponent? cooldown))
            return;

        cooldown.NextAvailableAt = _timing.CurTime + cooldown.Cooldown;
        Dirty(uid, cooldown);
    }

    private bool TryResolveScenarioPlanSpawnMarkers(
        AuThirdPartyPrototype party,
        MapId mapId,
        Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)>? assignedJobs,
        out ResolvedThirdPartySpawnMarkerSet? markers,
        bool logFailure = true,
        bool coveredScenarioForce = false)
    {
        markers = null;

        try
        {
            var request = BuildThirdPartySpawnScenarioPlanRequest(assignedJobs);
            if (_scenarioPlan.TryResolveThirdPartySpawnMarkers(request, party.ID, mapId, out markers, out var diagnostic))
            {
                _sawmill.Debug($"[AuThirdPartySystem] Using Scenario Plan Spawn Markers for third party '{party.ID}' on map {mapId}.");
                return true;
            }

            if (logFailure)
            {
                var backupDiagnostic = coveredScenarioForce
                    ? "covered Round Groups do not use legacy marker lookup"
                    : "falling back to legacy marker lookup";
                _sawmill.Warning($"[AuThirdPartySystem] Could not resolve Scenario Plan Spawn Markers for third party '{party.ID}' on map {mapId}; {backupDiagnostic}. {diagnostic}");
            }
        }
        catch (Exception ex)
        {
            if (logFailure)
            {
                var backupDiagnostic = coveredScenarioForce
                    ? "covered Round Groups do not use legacy marker lookup"
                    : "falling back to legacy marker lookup";
                _sawmill.Error($"[AuThirdPartySystem] Scenario Plan Spawn Marker resolution threw for third party '{party.ID}' on map {mapId}; {backupDiagnostic}. {ex}");
            }
        }

        markers = null;
        return false;
    }

    private ScenarioPlanValidationRequest BuildThirdPartySpawnScenarioPlanRequest(
        Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)>? assignedJobs)
    {
        var playerCount = Math.Max(_playerManager.PlayerCount, assignedJobs?.Count ?? 0);

        return new ScenarioPlanValidationRequest(
            _auRoundSystem.SelectedPreset?.ID ?? string.Empty,
            playerCount,
            _platoonSpawnRule.SelectedGovforPlatoon?.ID,
            _platoonSpawnRule.SelectedOpforPlatoon?.ID,
            _auRoundSystem.GetSelectedPlanetId(),
            _auRoundSystem.GetSelectedPlanet()?.MapId,
            _currentThreat?.ID,
            _auRoundSystem.GetSelectedGovforShip(),
            _auRoundSystem.GetSelectedOpforShip());
    }

    private string GetPlayerCharacterName(ICommonSession player, EntityUid? mind, string fallback)
    {
        if (mind != null &&
            TryComp<MindComponent>(mind.Value, out var mindComp) &&
            !string.IsNullOrWhiteSpace(mindComp.CharacterName))
        {
            return mindComp.CharacterName;
        }

        if (_preferences.GetPreferencesOrNull(player.UserId)?.SelectedCharacter is HumanoidCharacterProfile profile &&
            !string.IsNullOrWhiteSpace(profile.Name))
        {
            return profile.Name;
        }

        return fallback;
    }

    private void ApplyPlayerCharacterName(EntityUid mob, string characterName)
    {
        if (!HasComp<HumanoidAppearanceComponent>(mob))
            return;

        if (string.IsNullOrWhiteSpace(characterName))
            return;

        _metaData.SetEntityName(mob, characterName);

        if (_idCard.TryFindIdCard(mob, out var idCard))
            _idCard.TryChangeFullName(idCard.Owner, characterName, idCard.Comp);

        _identity.QueueIdentityUpdate(mob);
    }

    public void StartThirdPartySpawning(ThreatPrototype threat, Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)>? assignedJobs = null)
    {
        _currentThreat = threat;
        _thirdPartyList = _auRoundSystem.SelectedThirdParties.ToList();
        _nextThirdPartyIndex = 0;
        _spawnTimer = 0f;
        _spawnInterval = TimeSpan.FromSeconds(Math.Max(1, threat.ThirdPartyInterval));

        var roundstartCount = 0;
        foreach (var party in _thirdPartyList)
        {
            if (party.RoundStart)
                roundstartCount++;
        }

        var assignmentCounts = CountThirdPartyAssignments(assignedJobs);
        _sawmill.Info(
            $"[AuThirdPartySystem] Starting third-party queue: threat={threat.ID}, selected={_thirdPartyList.Count}, roundstart={roundstartCount}, interval={_spawnInterval}, assignedJobs={assignedJobs?.Count ?? 0}, assignedThirdPartyLeaders={assignmentCounts.Leaders}, assignedThirdPartyMembers={assignmentCounts.Members}.");

        if (_thirdPartyList.Count == 0)
        {
            _sawmill.Debug("[AuThirdPartySystem] No third parties selected for this planet; skipping third-party spawning.");
            _spawningActive = false;
            return;
        }

        _spawningActive = true;
        // Spawn all roundstart third parties immediately (called after jobs assigned)
        foreach (var party in _thirdPartyList)
        {
            if (!party.RoundStart)
                break;

            _sawmill.Debug($"[AuThirdPartySystem] Attempting roundstart third-party ({party.ID}) with PartySpawn={party.PartySpawn}.");
            if (_prototypeManager.TryIndex(party.PartySpawn, out var spawnProto))
            {
                if (SpawnThirdParty(party, spawnProto, true, assignedJobs))
                    _sawmill.Debug($"[AuThirdPartySystem] Spawned roundstart third party ({party.ID})");
                else
                    _sawmill.Warning($"[AuThirdPartySystem] Roundstart spawn attempt for third party ({party.ID}) failed.");
            }
            else
                _sawmill.Error($"[AuThirdPartySystem] No spawn proto for roundstart third party ({party.ID}) PartySpawn={party.PartySpawn}");

            _nextThirdPartyIndex++;
        }
    }

    private bool TrySpawnAtMarker(string protoId, List<EntityUid> markerPool, List<EntityUid> spawnedList,
    bool parachuteMode, bool useDropship, string label, ref EntityUid? lastUsedMarker)
    {
        if (markerPool.Count == 0)
        {
            _sawmill.Warning($"[AuThirdPartySystem] Cannot spawn {label} ({protoId}): marker pool is empty.");
            return false;
        }

        // Non-dropship pools were pre-filtered for player safety before spawning.
        var marker = PickRandomMarker(markerPool, parachuteMode && !useDropship);

        if (marker == EntityUid.Invalid)
        {
            _sawmill.Warning($"[AuThirdPartySystem] Cannot spawn {label} ({protoId}): selected marker is invalid.");
            return false;
        }

        var coords = _entityManager.GetComponent<TransformComponent>(marker).Coordinates;
        try
        {
            EntityUid ent = _entityManager.SpawnEntity(protoId, coords);
            // If parachute mode, hand off to the shared paradrop system so the entity falls from the sky.
            if (parachuteMode)
            {
                // Ensure the entity is paradroppable; SharedParaDropSystem will fall back to crash-land if missing.
                var paraComp = EnsureComp<ParaDroppableComponent>(ent);
                Dirty(ent, paraComp);

                // Raise AttemptCrashLandEvent on the grid entity that the parachute marker resides on so the para-drop handler will run.
                var markerXform = _entityManager.GetComponent<TransformComponent>(marker);
                if (markerXform.GridUid.HasValue)
                {
                    var attemptEvent = new AttemptCrashLandEvent(ent);
                    RaiseLocalEvent(markerXform.GridUid.Value, ref attemptEvent);
                }
            }
            spawnedList.Add(ent);

            // Put marker on a cooldown
            if (!parachuteMode && _entityManager.TryGetComponent<ThreatSpawnMarkerComponent>(marker, out var markerComp))
            {
                markerComp.NextAvailableAt = _timing.CurTime + markerComp.Cooldown;
                Dirty(marker, markerComp);
            }
            else if (!parachuteMode)
            {
                ApplyScenarioMarkerCooldown(marker);
            }

            // Parachute markers are intentionally NOT marked as used so they may be reused.
            lastUsedMarker = marker;
            if (!parachuteMode)
                markerPool.Remove(marker); // prevent stacking

            _sawmill.Debug($"[AuThirdPartySystem] Spawned {label} {protoId} at {coords} (entity {ent})");
            return true;
        }
        catch (Exception ex)
        {
            _sawmill.Error($"[AuThirdPartySystem] Failed to spawn {label} ({protoId}) at {coords}! {ex.Message}");
            return false;
        }
    }

    private bool IsMarkerBlockedByPlayers(EntityUid marker)
    {
        const float PlayerAvoidRadius = 8f;

        // Only check main-map/groundside markers; dropship spawns handled elsewhere via useDropship
        var markerCoords = _entityManager.GetComponent<TransformComponent>(marker).Coordinates;
        foreach (var session in _playerManager.Sessions)
        {
            if (!session.AttachedEntity.HasValue)
                continue;

            var attached = session.AttachedEntity.Value;

            // Skip ghosts
            if (_entityManager.HasComponent<GhostComponent>(attached))
                continue;

            if (!_entityManager.TryGetComponent<TransformComponent>(attached, out var playerXform))
                continue;

            if (_transform.InRange(playerXform.Coordinates, markerCoords, PlayerAvoidRadius))
            {
                _sawmill.Debug($"[AuThirdPartySystem] Marker {marker} is blocked by player {attached} within radius {PlayerAvoidRadius}");
                return true;
            }
        }

        return false;
    }

    private EntityUid PickRandomMarker(List<EntityUid> candidates, bool remove)
    {
        if (candidates.Count == 0)
            return EntityUid.Invalid;

        var index = _random.Next(candidates.Count);
        var marker = candidates[index];
        if (remove)
            candidates.RemoveAt(index);

        return marker;
    }

    private void AssignMinds(List<NetUserId> playerIds, List<EntityUid> spawnedList, string jobProto, string roleLabel)
    {
        var mindSystem = _entityManager.System<SharedMindSystem>();
        var roleSystem = _entityManager.System<SharedRoleSystem>();
        var ticker = _entityManager.System<GameTicker>();
        var assigned = 0;

        for (int i = 0; i < playerIds.Count && i < spawnedList.Count; i++)
        {
            var playerNetId = playerIds[i];
            var entity = spawnedList[i];
            try
            {
                if (!_playerManager.TryGetSessionById(playerNetId, out var session))
                    continue;

                ticker.PlayerJoinGame(session, silent: true);

                var data = session.ContentData();
                var mind = mindSystem.GetMind(playerNetId);
                var characterName = GetPlayerCharacterName(session, mind, data?.Name ?? "Third Party Player");
                ApplyPlayerCharacterName(entity, characterName);

                mind ??= mindSystem.CreateMind(playerNetId, characterName);
                mindSystem.SetUserId(mind.Value, playerNetId);
                mindSystem.TransferTo(mind.Value, entity);
                roleSystem.MindAddJobRole(mind.Value, silent: true, jobPrototype: jobProto);
                assigned++;
            }
            catch (Exception ex)
            {
                _sawmill.Error($"[AuThirdPartySystem] Failed to assign {roleLabel} mind (player {playerNetId}, entity {entity}): {ex}");
            }
        }

        _sawmill.Info(
            $"[AuThirdPartySystem] Third-party {roleLabel} mind assignment result: players={playerIds.Count}, bodies={spawnedList.Count}, assigned={assigned}, job={jobProto}.");
        if (playerIds.Count > spawnedList.Count)
            _sawmill.Warning($"[AuThirdPartySystem] Third-party {roleLabel} assignment had {playerIds.Count} player(s) but only {spawnedList.Count} body/bodies.");
    }
}
