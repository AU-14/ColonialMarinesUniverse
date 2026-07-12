using System.Linq;
using Content.Server.Access.Systems;
using Content.Server.AU14.Round;
using Content.Server.AU14.Scenario;
using Content.Server.AU14.VendorMarker;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.IdentityManagement;
using Content.Server.Preferences.Managers;
using Content.Shared._CMU14.Threats;
using Content.Shared._RMC14.CrashLand;
using Content.Shared._RMC14.Dropship;
using Content.Shared.Access.Components;
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
using ParachuteMarkerComponent = Content.Shared._CMU14.Threats.ParachuteMarkerComponent;

namespace Content.Server._CMU14.Ops.ThirdParty;

public sealed partial class ThirdPartySystem : EntitySystem
{
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
    private static readonly ProtoId<JobPrototype> ThirdPartyLeaderJobId = new("AU14JobThirdPartyLeader");
    private static readonly ProtoId<JobPrototype> ThirdPartyMemberJobId = new("AU14JobThirdPartyMember");
    private static readonly ThreatMarkerType[] ThreatMarkerTypes = Enum.GetValues<ThreatMarkerType>();
    private const string ThirdPartyFaction = "thirdparty";
    private readonly ISawmill _sawmill = Logger.GetSawmill("thirdparty");

    // --- State for round third party spawning ---
    private ThreatPrototype? _currentThreat;
    private int _nextThirdPartyIndex;

    // --- Signal modifier applied by Ambassador / AI Core consoles ---
    private float _signalIntervalMultiplier = 1f;
    private bool _spawningActive;
    private TimeSpan _spawnInterval = TimeSpan.FromMinutes(5);
    private float _spawnTimer;
    private List<ThirdPartyPrototype>? _thirdPartyList;

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
        ThirdPartyPrototype party = _thirdPartyList[_nextThirdPartyIndex];
        if (party.RoundStart)
        {
            _nextThirdPartyIndex++;
            return;
        }

        TimeSpan interval = TimeSpan.FromTicks((long)(_spawnInterval.Ticks * _signalIntervalMultiplier));
        if (_spawnTimer < interval.TotalSeconds)
            return;

        int ghostCount = _playerManager.Sessions.Count(s => s.AttachedEntity == null
            || _entityManager.HasComponent<GhostComponent>(s.AttachedEntity));
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
            _sawmill.Debug($"[ThirdPartySystem] Did not spawn ({party.ID}) (roll {roll} > {chance})");
            return;
        }

        if (!_prototypeManager.TryIndex(party.PartySpawn, out PartySpawnPrototype? spawnProto))
        {
            _sawmill.Error($"[ThirdPartySystem] No spawn proto for ({party.ID}) (PartySpawn={party.PartySpawn})");
            _nextThirdPartyIndex++;
            return;
        }

        try
        {
            if (SpawnThirdParty(party, spawnProto, false))
                _sawmill.Debug($"[ThirdPartySystem] Spawned ({party.ID}) (roll {roll} <= {chance})");
            else
                _sawmill.Warning($"[ThirdPartySystem] Spawn of ({party.ID}) failed; skipping.");
        }
        catch (Exception ex) { _sawmill.Error($"[ThirdPartySystem] Exception spawning ({party.ID}): {ex}"); }

        _nextThirdPartyIndex++;
    }

    private static ThirdPartyAssignmentCounts CountThirdPartyAssignments(
        Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)>? assignedJobs)
    {
        if (assignedJobs == null)
            return default(ThirdPartyAssignmentCounts);

        var leaders = 0;
        var members = 0;
        foreach ((NetUserId _, (ProtoId<JobPrototype>? job, EntityUid _)) in assignedJobs)
        {
            if (job == ThirdPartyLeaderJobId)
                leaders++;
            else if (job == ThirdPartyMemberJobId)
                members++;
        }

        return new(leaders, members);
    }

    private static string FormatSpawnBodies(IReadOnlyDictionary<string, int> bodies)
    {
        return bodies.Count == 0
            ? "none"
            : string.Join(", ", bodies.Select(pair => $"{pair.Key}={pair.Value}"));
    }

    /// <summary>
    ///     Returns the list of queued third parties that have not yet spawned.
    /// </summary>
    public List<ThirdPartyPrototype> GetQueuedThirdParties()
    {
        if (_thirdPartyList == null || _nextThirdPartyIndex >= _thirdPartyList.Count)
            return new();

        return _thirdPartyList.GetRange(_nextThirdPartyIndex, _thirdPartyList.Count - _nextThirdPartyIndex);
    }

    /// <summary>
    ///     Sets the signal interval multiplier. Below 1 = signal boost, above 1 = signal jam.
    /// </summary>
    public void SetSignalIntervalMultiplier(float multiplier)
    {
        _signalIntervalMultiplier = Math.Max(0.1f, multiplier);
    }

    public float GetSignalIntervalMultiplier() => _signalIntervalMultiplier;

    public bool SpawnThirdParty(ThirdPartyPrototype party, PartySpawnPrototype spawnProto, bool roundStart,
        Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)>? assignedJobs = null, bool? overrideDropship = null)
    {
        const float SpawnTogetherRadius = 8f;
        _sawmill.Debug($"[ThirdPartySystem] Spawning third party: ({party.ID})");
        if (spawnProto == null)
        {
            _sawmill.Error($"[ThirdPartySystem] Spawn called with null spawnProto for party ({party.ID})!");
            return false;
        }

        // Determine entry method. If overrideDropship is provided, it takes precedence (true => shuttle, false =>
        // ground).
        string? entryMethod = overrideDropship.HasValue
            ? overrideDropship.Value ? "shuttle" : "ground"
            : string.IsNullOrWhiteSpace(party.EntryMethod) ? "ground" : party.EntryMethod;
        _sawmill.Debug($"[ThirdPartySystem] Entry method: {entryMethod} (overrideDropship={overrideDropship})");
        string presetId = _auRoundSystem.SelectedPreset?.ID ?? string.Empty;
        bool coveredScenarioForce = _scenarioPlan.HasMappedThirdPartyRoundGroup(presetId, party.ID);
        if (_sawmill.Level <= LogLevel.Debug)
        {
            ThirdPartyAssignmentCounts assignmentCounts = ThirdPartySystem.CountThirdPartyAssignments(assignedJobs);
            _sawmill.Debug(
                $"[ThirdPartySystem] Spawn context: party={party.ID}, spawnProto={spawnProto.ID}, roundStart={roundStart
                }, preset={presetId}, threat={_currentThreat?.ID ?? "null"}, entryMethod={entryMethod
                }, coveredScenarioForce={coveredScenarioForce}, assignedJobs={assignedJobs?.Count ?? 0
                }, assignedThirdPartyLeaders={assignmentCounts.Leaders}, assignedThirdPartyMembers={
                    assignmentCounts.Members}.");
        }

        List<EntityUid> markerEntities = new();
        var markerEntitySet = new HashSet<EntityUid>();
        EntityUid mainGridUid = EntityUid.Invalid;
        var parachuteMode = false;

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
            EntityQueryEnumerator<DropshipDestinationComponent, TransformComponent> destQuery = _entityManager
                .EntityQueryEnumerator<DropshipDestinationComponent, TransformComponent>();
            while (destQuery.MoveNext(out EntityUid destUid, out DropshipDestinationComponent? destComp,
                out TransformComponent? destXform))
            {
                if (IsAvailableThirdPartyDropshipDestination(destUid, destComp))
                {
                    chosenDestination = destUid;
                    break;
                }
            }

            if (chosenDestination == null)
            {
                _sawmill.Error(
                    "[ThirdPartySystem] No valid third-party dropship landing destination found. Aborting third party spawn.");
                return false;
            }

            EntityUid destination = chosenDestination.Value;
            _sawmill.Debug($"[ThirdPartySystem] Found valid dropship destination: {destination}");

            DeserializationOptions deserializationOpts = DeserializationOptions.Default with { InitializeMaps = true };
            if (!TryLoadDropshipGrid(party.dropshippath, deserializationOpts, out mainGridUid))
                return false;

            _sawmill.Debug($"[ThirdPartySystem] Dropship grid initialized: {mainGridUid}");

            var dropshipMapCoordinates = _transform.ToMapCoordinates(_entityManager
                .GetComponent<TransformComponent>(mainGridUid).Coordinates);
            EntityUid returnDestination;
            try
            {
                returnDestination = _entityManager.SpawnEntity(
                    "CMDropshipDestinationThirdPartyReturn",
                    dropshipMapCoordinates);
            }
            catch (Exception ex)
            {
                _sawmill.Error($"[ThirdPartySystem] Failed to spawn return destination entity at {dropshipMapCoordinates
                }: {ex}");
                return false;
            }

            var returnDestinationComp = EnsureComp<ThirdPartyDropshipReturnDestinationComponent>(returnDestination);
            returnDestinationComp.Shuttle = mainGridUid;

            EnsureComp<DropshipDestinationComponent>(returnDestination);
            _sharedDropshipSystem.SetDestinationShip(returnDestination, mainGridUid);
            _sharedDropshipSystem.SetDestinationHome(returnDestination, true);

            EnsureComp<DropshipComponent>(mainGridUid);
            _sharedDropshipSystem.SetDropshipDestination(mainGridUid, returnDestination);

            _sawmill.Debug($"[ThirdPartySystem] Third-party dropship {mainGridUid} loaded and waiting for manual launch to destination {
                destination}.");

            // Collect markers on dropship grid
            EntityQueryEnumerator<AuInsertMarkerComponent> query = _entityManager
                .EntityQueryEnumerator<AuInsertMarkerComponent>();
            while (query.MoveNext(out EntityUid uid, out _))
            {
                if (_entityManager.TryGetComponent(uid, out TransformComponent? transform) &&
                    ThirdPartySystem.IsOnGrid(transform, mainGridUid))
                    AddMarkerEntity(uid);
            }

            EntityQueryEnumerator<ThreatSpawnMarkerComponent, TransformComponent> legacyMarkerQuery = _entityManager
                .EntityQueryEnumerator<ThreatSpawnMarkerComponent, TransformComponent>();
            while (legacyMarkerQuery.MoveNext(out EntityUid uid, out ThreatSpawnMarkerComponent? marker,
                out TransformComponent? transform))
            {
                if (!marker.ThirdParty ||
                    !ThirdPartySystem.IsOnGrid(transform, mainGridUid))
                    continue;

                AddMarkerEntity(uid);
            }

            EntityQueryEnumerator<ScenarioSpawnMarkerComponent, TransformComponent> scenarioMarkerQuery = _entityManager
                .EntityQueryEnumerator<ScenarioSpawnMarkerComponent, TransformComponent>();
            while (scenarioMarkerQuery.MoveNext(out EntityUid uid, out _, out TransformComponent? transform))
            {
                if (!HasStandaloneThirdPartyMarker(uid) ||
                    !ThirdPartySystem.IsOnGrid(transform, mainGridUid))
                    continue;

                AddMarkerEntity(uid);
            }

            _sawmill.Debug($"[ThirdPartySystem] Dropship markers collected: {markerEntities.Count}");

            // Spawn consoles
            EntityQueryEnumerator<VendorMarkerComponent> vmarkerQuery = _entityManager
                .EntityQueryEnumerator<VendorMarkerComponent>();
            var consoleCount = 0;
            while (vmarkerQuery.MoveNext(out EntityUid vmarkerUid, out VendorMarkerComponent? vmarkerComp))
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
                                _entityManager.SpawnEntity("CMComputerDropshipNavigationThirdParty",
                                    markerXform.Coordinates);
                                consoleCount++;
                            }
                            catch (Exception ex)
                            {
                                _sawmill.Error($"[ThirdPartySystem] Failed to spawn Dropship Navigation console: {ex
                                }");
                            }

                            break;
                        case PlatoonMarkerClass.DSWeapons:
                            try
                            {
                                _entityManager.SpawnEntity("CMComputerDropshipWeapons", markerXform.Coordinates);
                                consoleCount++;
                            }
                            catch (Exception ex)
                            {
                                _sawmill.Error($"[ThirdPartySystem] Failed to spawn Dropship Weapons console: {ex}");
                            }

                            break;
                    }
                }
                catch (Exception ex)
                {
                    _sawmill.Debug($"[ThirdPartySystem] Skipping vendor marker {vmarkerUid} (class={vmarkerComp.Class
                    }) due to component error: {ex}");
                }
            }

            _sawmill.Debug($"[ThirdPartySystem] Dropship consoles spawned: {consoleCount}");
        }
        else if (entryMethod.Equals("parachute", StringComparison.OrdinalIgnoreCase))
        {
            // Parachute mode: collect parachute markers on the main map
            parachuteMode = true;
            EntityQueryEnumerator<ParachuteMarkerComponent, TransformComponent> pQuery = _entityManager
                .EntityQueryEnumerator<ParachuteMarkerComponent, TransformComponent>();
            while (pQuery.MoveNext(out EntityUid uid, out ParachuteMarkerComponent? pComp,
                out TransformComponent? pxform))
            {
                // Parachute markers are reusable and do not need to be marked as used; include all of them.
                AddMarkerEntity(uid);
            }

            _sawmill.Debug($"[ThirdPartySystem] Parachute markers collected: {markerEntities.Count}");
        }
        else
        {
            // Ground spawn: collect all markers on main map (existing behavior)
            EntityQueryEnumerator<AuInsertMarkerComponent> query = _entityManager
                .EntityQueryEnumerator<AuInsertMarkerComponent>();
            while (query.MoveNext(out EntityUid uid, out _))
            {
                AddMarkerEntity(uid);
            }

            EntityQueryEnumerator<ThreatSpawnMarkerComponent> legacyMarkerQuery = _entityManager
                .EntityQueryEnumerator<ThreatSpawnMarkerComponent>();
            while (legacyMarkerQuery.MoveNext(out EntityUid uid, out ThreatSpawnMarkerComponent? marker))
            {
                if (!marker.ThirdParty)
                    continue;

                AddMarkerEntity(uid);
            }

            EntityQueryEnumerator<ScenarioSpawnMarkerComponent> scenarioMarkerQuery
                = _entityManager.EntityQueryEnumerator<ScenarioSpawnMarkerComponent>();
            while (scenarioMarkerQuery.MoveNext(out EntityUid uid, out _))
            {
                if (!HasStandaloneThirdPartyMarker(uid))
                    continue;

                AddMarkerEntity(uid);
            }

            _sawmill.Debug($"[ThirdPartySystem] Main map third-party markers collected: {markerEntities.Count}");
        }

        var candidateMapIds = new List<MapId>();
        var candidateMapIdSet = new HashSet<MapId>();
        foreach (EntityUid marker in markerEntities)
        {
            if (!_entityManager.TryGetComponent(marker, out TransformComponent? markerTransform) ||
                !candidateMapIdSet.Add(markerTransform.MapID))
                continue;

            candidateMapIds.Add(markerTransform.MapID);
        }

        MapId? mapId = candidateMapIds.Count > 0
            ? candidateMapIds[0]
            : null;
        if (_sawmill.Level <= LogLevel.Debug)
        {
            _sawmill.Debug(
                $"[ThirdPartySystem] Candidate marker maps for third party ({party.ID}): count={candidateMapIds.Count
                }, maps=[{string.Join(", ", candidateMapIds)}], initialMap={mapId?.ToString() ?? "null"}.");
        }

        ResolvedThirdPartySpawnMarkerSet? scenarioMarkers = null;
        foreach (MapId spawnMapId in candidateMapIds)
        {
            if (!TryResolveScenarioPlanSpawnMarkers(
                party,
                spawnMapId,
                assignedJobs,
                out scenarioMarkers,
                false,
                coveredScenarioForce))
                continue;

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
                $"[ThirdPartySystem] Covered Round Group for third party '{party.ID
                }' resolved without live Spawn Markers; aborting authoritative Scenario Plan third-party spawn instead of using legacy marker lookup.");
            return false;
        }

        var markerCache = new Dictionary<ThreatMarkerType, List<EntityUid>>();

        List<EntityUid> GetMarkers(ThreatMarkerType markerType)
        {
            if (markerCache.TryGetValue(markerType, out List<EntityUid>? cachedMarkers))
                return cachedMarkers;

            List<EntityUid> markers = ResolveMarkers(markerType);
            markerCache[markerType] = markers;
            return markers;
        }

        List<EntityUid> ResolveMarkers(ThreatMarkerType markerType)
        {
            TimeSpan time = _timing.CurTime;
            if (scenarioMarkers != null &&
                scenarioMarkers.TryGetMarkers(markerType.ToString(), out IReadOnlyList<EntityUid> plannedMarkers))
            {
                EntityUid? gridUid = useDropship && mainGridUid != EntityUid.Invalid
                    ? mainGridUid
                    : null;
                List<EntityUid> filteredScenarioMarkers = FilterScenarioMarkers(markerType, plannedMarkers, time, mapId,
                    gridUid);
                _sawmill.Debug($"[ThirdPartySystem] GetMarkers({markerType}): Using {filteredScenarioMarkers.Count
                } Scenario Plan marker(s) on map {mapId}");
                if (filteredScenarioMarkers.Count > 0 || !useDropship)
                    return filteredScenarioMarkers;

                if (coveredScenarioForce)
                    return filteredScenarioMarkers;

                _sawmill.Warning($"[ThirdPartySystem] Scenario Plan resolved no dropship grid markers for {markerType
                } on grid {mainGridUid}; falling back to legacy marker lookup.");
            }

            string markerId = spawnProto.Markers.TryGetValue(markerType, out string? id) ? id : string.Empty;
            var legacyMarkers = new List<EntityUid>();
            EntityQueryEnumerator<ThreatSpawnMarkerComponent> query = _entityManager
                .EntityQueryEnumerator<ThreatSpawnMarkerComponent>();
            while (query.MoveNext(out EntityUid uid, out ThreatSpawnMarkerComponent? comp))
            {
                // Only include markers that are of the requested type, match the optional marker ID,
                // are explicitly marked as ThirdParty, and are unused - and aren't on a Cooldown
                if (comp.ThreatMarkerType != markerType
                    || !(comp.ID == markerId || (comp.ID == string.Empty && markerId == string.Empty))
                    || !comp.ThirdParty
                    || comp.NextAvailableAt > time)
                    continue;

                if (useDropship && mainGridUid != EntityUid.Invalid)
                {
                    if (!_entityManager.TryGetComponent(uid, out TransformComponent? tcomp)
                        || !tcomp.GridUid.HasValue || tcomp.GridUid.Value != mainGridUid)
                        continue;
                }
                else
                {
                    // Otherwise, ensure we are on the same map (if mapId set).
                    if (mapId != null && _entityManager.GetComponent<TransformComponent>(uid).MapID != mapId)
                        continue;
                }

                // Only include markers that are not already used
                // if (!comp.Used) // <- now handled by Cooldowns
                legacyMarkers.Add(uid);
            }

            _sawmill.Debug($"[ThirdPartySystem] GetMarkers({markerType}): Found {legacyMarkers.Count
            } unused markers with markerId '{markerId}' on map {mapId}");
            return legacyMarkers;
        }

        bool spawnTogether = spawnProto.SpawnTogether;
        Dictionary<ThreatMarkerType, List<EntityUid>> spawnTogetherMarkers = new();
        if (spawnTogether)
        {
            var allMarkers = new List<EntityUid>();
            foreach (ThreatMarkerType type in ThreatMarkerTypes)
            {
                allMarkers.AddRange(GetMarkers(type));
            }

            if (allMarkers.Count > 0)
            {
                EntityUid centerMarker = allMarkers[_random.Next(allMarkers.Count)];
                EntityCoordinates centerCoords = _entityManager.GetComponent<TransformComponent>(centerMarker)
                    .Coordinates;
                foreach (ThreatMarkerType type in ThreatMarkerTypes)
                {
                    List<EntityUid> markers = GetMarkers(type);
                    var filtered = new List<EntityUid>();
                    foreach (EntityUid marker in markers)
                    {
                        EntityCoordinates coords = _entityManager.GetComponent<TransformComponent>(marker).Coordinates;
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
            if (spawnTogether && spawnTogetherMarkers.TryGetValue(type, out List<EntityUid>? cached))
                return cached;
            return GetMarkers(type);
        }

        var spawnedLeaders = new List<EntityUid>();
        var spawnedGrunts = new List<EntityUid>();
        var spawnedEnts = new List<EntityUid>();

        // Track the last marker we used during this spawn operation
        EntityUid? lastUsedMarker = null;
        IReadOnlyDictionary<string, int> leaderBodies = ThirdPartySystem.GetSpawnBodies(
            scenarioMarkers?.Force.SpawnPlan,
            ThreatMarkerType.Leader,
            spawnProto.LeadersToSpawn);
        IReadOnlyDictionary<string, int> gruntBodies = ThirdPartySystem.GetSpawnBodies(
            scenarioMarkers?.Force.SpawnPlan,
            ThreatMarkerType.Member,
            spawnProto.GruntsToSpawn);
        IReadOnlyDictionary<string, int> entityBodies = ThirdPartySystem.GetSpawnBodies(
            scenarioMarkers?.Force.SpawnPlan,
            ThreatMarkerType.Entity,
            spawnProto.EntitiesToSpawn);
        int leaderReq = leaderBodies.Values.Sum();
        int gruntReq = gruntBodies.Values.Sum();
        int entityReq = entityBodies.Values.Sum();

        List<EntityUid> leaderMarkers = GetSpawnMarkers(ThreatMarkerType.Leader);
        List<EntityUid> gruntMarkers = GetSpawnMarkers(ThreatMarkerType.Member);
        List<EntityUid> entityMarkers = GetSpawnMarkers(ThreatMarkerType.Entity);
        if (_sawmill.Level <= LogLevel.Debug)
        {
            _sawmill.Debug(
                $"[ThirdPartySystem] Spawn plan for third party ({party.ID}): force={
                    scenarioMarkers?.Force.ForceId ?? "legacy"}, leaders[{
                        ThirdPartySystem.FormatSpawnBodies(leaderBodies)}] requested={leaderReq} markers={
                            leaderMarkers.Count}, grunts[{ThirdPartySystem.FormatSpawnBodies(gruntBodies)}] requested={
                                gruntReq} markers={gruntMarkers.Count}, entities[{
                                    ThirdPartySystem.FormatSpawnBodies(entityBodies)}] requested={entityReq} markers={
                                        entityMarkers.Count}.");
        }

        if (leaderReq > 0 && leaderMarkers.Count == 0)
        {
            _sawmill.Warning($"[ThirdPartySystem] Third party ({party.ID}) requested {leaderReq
            } leader body/bodies but found no leader markers.");
        }

        if (gruntReq > 0 && gruntMarkers.Count == 0)
        {
            _sawmill.Warning($"[ThirdPartySystem] Third party ({party.ID}) requested {gruntReq
            } grunt body/bodies but found no member markers.");
        }

        if (entityReq > 0 && entityMarkers.Count == 0)
        {
            _sawmill.Warning($"[ThirdPartySystem] Third party ({party.ID}) requested {entityReq
            } entity spawn(s) but found no entity markers.");
        }

        List<EntityUid> FilterByType(ThreatMarkerType type)
        {
            var filtered = new List<EntityUid>();
            foreach (EntityUid marker in markerEntities)
            {
                if (_entityManager.TryGetComponent(marker,
                        out ThreatSpawnMarkerComponent? comp) &&
                    comp.ThirdParty &&
                    comp.ThreatMarkerType == type)
                    filtered.Add(marker);
            }

            return filtered;
        }

        // If parachute mode, use the parachute marker pool for all types; make local mutable copies so we can pick
        // without replacement during this spawn
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
            List<EntityUid> safeLeaderMarkers = FilterSafeMarkers(leaderMarkers);
            List<EntityUid> safeGruntMarkers = FilterSafeMarkers(gruntMarkers);
            List<EntityUid> safeEntityMarkers = FilterSafeMarkers(entityMarkers);

            if (safeLeaderMarkers.Count < leaderReq || safeGruntMarkers.Count < gruntReq
                || safeEntityMarkers.Count < entityReq)
            {
                _sawmill.Warning($"[ThirdPartySystem] Not enough safe markers to spawn third party ({party.ID
                }): leaders needed {leaderReq}, safe available {safeLeaderMarkers.Count}; grunts needed {gruntReq
                }, safe available {safeGruntMarkers.Count}; entities needed {entityReq}, safe available {
                    safeEntityMarkers.Count}. Aborting spawn.");
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
                _sawmill.Warning($"[ThirdPartySystem] Not enough unused dropship markers to spawn third party ({party.ID
                }): leaders needed {leaderReq}, available {leaderMarkers.Count}; grunts needed {gruntReq}, available {
                    gruntMarkers.Count}; entities needed {entityReq}, available {entityMarkers.Count
                    }. Aborting spawn.");
                return false;
            }
        }

        _sawmill.Debug(
            $"[ThirdPartySystem] Final marker pools for third party ({party.ID}): leaders={leaderMarkers.Count
            }, grunts={gruntMarkers.Count}, entities={entityMarkers.Count}, useDropship={useDropship}, parachuteMode={
                parachuteMode}.");

        List<EntityUid> FilterSafeMarkers(List<EntityUid> markers)
        {
            var safeMarkers = new List<EntityUid>(markers.Count);
            foreach (EntityUid marker in markers)
            {
                if (!IsMarkerBlockedByPlayers(marker))
                    safeMarkers.Add(marker);
            }

            return safeMarkers;
        }

        // Spawn leaders
        _sawmill.Debug("[ThirdPartySystem] Spawning leaders...");
        foreach ((string protoId, int count) in leaderBodies)
        {
            for (var i = 0; i < count; i++)
            {
                if (!TrySpawnAtMarker(protoId, leaderMarkers, spawnedLeaders, parachuteMode, useDropship, "leader",
                    ref lastUsedMarker))
                    _sawmill.Warning($"[ThirdPartySystem] Failed to spawn leader {protoId}");
            }
        }

        // Spawn grunts
        _sawmill.Debug("[ThirdPartySystem] Spawning grunts...");
        foreach ((string protoId, int count) in gruntBodies)
        {
            for (var i = 0; i < count; i++)
            {
                if (!TrySpawnAtMarker(protoId, gruntMarkers, spawnedGrunts, parachuteMode, useDropship, "grunt",
                    ref lastUsedMarker))
                    _sawmill.Warning($"[ThirdPartySystem] Failed to spawn grunt {protoId}");
            }
        }

        // Spawn ents
        _sawmill.Debug("[ThirdPartySystem] Spawning ents...");
        foreach ((string protoId, int count) in entityBodies)
        {
            for (var i = 0; i < count; i++)
            {
                if (!TrySpawnAtMarker(protoId, entityMarkers, spawnedEnts, parachuteMode, useDropship, "ent",
                    ref lastUsedMarker))
                    _sawmill.Warning($"[ThirdPartySystem] Failed to spawn entity {protoId}");
            }
        }

        _sawmill.Info(
            $"[ThirdPartySystem] Third-party spawn result for ({party.ID}): spawnedLeaders={spawnedLeaders.Count}/{
                leaderReq}, spawnedGrunts={spawnedGrunts.Count}/{gruntReq}, spawnedEntities={spawnedEnts.Count}/{
                    entityReq}.");

        // After all spawns: if spawnTogether is true, mark nearby unused markers around the last used marker.
        void MarkNeighborsIfNeeded()
        {
            if (!spawnTogether || lastUsedMarker == null)
                return;

            EntityUid centerMarkerUid = lastUsedMarker.Value;
            if (!_entityManager.HasComponent<ThreatSpawnMarkerComponent>(centerMarkerUid) &&
                !_entityManager.HasComponent<ScenarioSpawnMarkerCooldownComponent>(centerMarkerUid))
                return;

            var centerXform = _entityManager.GetComponent<TransformComponent>(centerMarkerUid);
            EntityCoordinates centerCoords = centerXform.Coordinates;
            MapId centerMap = centerXform.MapID;

            EntityQueryEnumerator<ThreatSpawnMarkerComponent> query = _entityManager
                .EntityQueryEnumerator<ThreatSpawnMarkerComponent>();
            while (query.MoveNext(out EntityUid otherUid, out _))
            {
                if (otherUid == centerMarkerUid)
                    continue;

                var otherXform = _entityManager.GetComponent<TransformComponent>(otherUid);
                if (otherXform.MapID != centerMap)
                    continue;

                if (_transform.InRange(otherXform.Coordinates, centerCoords, SpawnTogetherRadius))
                {
                    if (_entityManager.TryGetComponent(otherUid,
                        out ThreatSpawnMarkerComponent? otherComp))
                    {
                        otherComp.NextAvailableAt = _timing.CurTime + otherComp.Cooldown;
                        Dirty(otherUid, otherComp);
                    }
                }
            }

            EntityQueryEnumerator<ScenarioSpawnMarkerCooldownComponent, TransformComponent> scenarioQuery
                = _entityManager.EntityQueryEnumerator<ScenarioSpawnMarkerCooldownComponent, TransformComponent>();
            while (scenarioQuery.MoveNext(out EntityUid otherUid, out _, out TransformComponent? otherXform))
            {
                if (otherUid == centerMarkerUid ||
                    otherXform.MapID != centerMap ||
                    !HasStandaloneThirdPartyMarker(otherUid))
                    continue;

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
            foreach ((NetUserId player, (ProtoId<JobPrototype>? job, EntityUid _)) in assignedJobs)
            {
                if (job == ThirdPartyLeaderJobId)
                    leaderPlayers.Add(player);
                else if (job == ThirdPartyMemberJobId)
                    memberPlayers.Add(player);
            }

            _sawmill.Debug("[ThirdPartySystem] Assigning minds to third party entities (roundstart)");
            AssignMinds(leaderPlayers, spawnedLeaders, ThirdPartyLeaderJobId.Id, "leader");
            AssignMinds(memberPlayers, spawnedGrunts, ThirdPartyMemberJobId.Id, "member");
        }

        if (!string.IsNullOrWhiteSpace(party.AnnounceArrival))
        {
            _chat.DispatchGlobalAnnouncement(party.AnnounceArrival, string.Empty, false,
                colorOverride: Color.DarkOrange);
            _sawmill.Info($"[ThirdPartySystem] Announced arrival for third party ({party.ID}): {party.AnnounceArrival
            }");
        }

        return true;
    }

    private static IReadOnlyDictionary<string, int> GetSpawnBodies(ResolvedSpawnPlan? spawnPlan,
        ThreatMarkerType markerType,
        IReadOnlyDictionary<string, int> legacyBodies)
    {
        SpawnBodyBucket? bucket = spawnPlan?.BodyBuckets.FirstOrDefault(bodyBucket =>
            bodyBucket.Bucket.Equals(markerType.ToString(), StringComparison.OrdinalIgnoreCase));
        if (bucket != null &&
            (bucket.Bodies.Count > 0 || bucket.Count == 0))
            return bucket.Bodies;

        return legacyBodies
            .ToDictionary(
                body => body.Key,
                body => Math.Max(0, body.Value),
                StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsOnGrid(TransformComponent transform, EntityUid gridUid)
        => (transform.GridUid.HasValue && transform.GridUid.Value == gridUid) ||
            transform.ParentUid == gridUid;

    private bool IsAvailableThirdPartyDropshipDestination(
        EntityUid destination,
        DropshipDestinationComponent destinationComponent)
    {
        return destinationComponent.Ship == null &&
               !destinationComponent.Home &&
               !HasComp<ThirdPartyDropshipReturnDestinationComponent>(destination) &&
               string.Equals(destinationComponent.FactionController, ThirdPartyFaction, StringComparison.OrdinalIgnoreCase);
    }

    private List<EntityUid> FilterScenarioMarkers(ThreatMarkerType markerType,
        IReadOnlyList<EntityUid> candidateMarkers,
        TimeSpan time,
        MapId? mapId,
        EntityUid? gridUid)
    {
        var filteredMarkers = new List<EntityUid>();
        foreach (EntityUid uid in candidateMarkers)
        {
            if (!_entityManager.TryGetComponent(uid, out TransformComponent? transform))
                continue;

            if (mapId != null && transform.MapID != mapId) continue;

            if (gridUid != null &&
                !ThirdPartySystem.IsOnGrid(transform, gridUid.Value))
                continue;

            if (_entityManager.TryGetComponent(uid, out ThreatSpawnMarkerComponent? marker))
            {
                if (marker.ThreatMarkerType != markerType ||
                    !marker.ThirdParty ||
                    marker.NextAvailableAt > time)
                    continue;

                filteredMarkers.Add(uid);
                continue;
            }

            if (HasStandaloneThirdPartyMarker(uid, markerType) &&
                IsScenarioMarkerAvailable(uid, time))
                filteredMarkers.Add(uid);
        }

        return filteredMarkers;
    }

    private bool HasStandaloneThirdPartyMarker(EntityUid uid, ThreatMarkerType? markerType = null)
    {
        if (!_entityManager.TryGetComponent(uid, out ScenarioSpawnMarkerComponent? marker) ||
            marker.Kind != SpawnMarkerKind.ThirdPartyMarker ||
            !marker.Tags.Contains(ScenarioMarkerTags.ForceThirdParty, StringComparer.OrdinalIgnoreCase))
            return false;

        if (markerType != null &&
            !marker.Tags.Contains(ScenarioMarkerTags.Bucket(markerType.Value.ToString()),
                StringComparer.OrdinalIgnoreCase))
            return false;

        return !_entityManager.HasComponent<ThreatSpawnMarkerComponent>(uid);
    }

    private bool TryLoadDropshipGrid(ResPath path, DeserializationOptions options, out EntityUid gridUid)
    {
        gridUid = EntityUid.Invalid;
        var loadOptions = new MapLoadOptions
        {
            DeserializationOptions = options with { LogOrphanedGrids = false }
        };

        if (!_mapLoader.TryLoadGeneric(path, out LoadResult? result, loadOptions))
        {
            _sawmill.Error($"[ThirdPartySystem] Failed to load dropship map or grid: {path}");
            return false;
        }

        gridUid = result.Grids.FirstOrDefault();
        if (gridUid != EntityUid.Invalid)
            return true;

        _mapLoader.Delete(result);
        _sawmill.Error($"[ThirdPartySystem] No grids found in dropship map or grid: {path}");
        return false;
    }

    private bool IsScenarioMarkerAvailable(EntityUid uid, TimeSpan time)
        => !_entityManager.TryGetComponent(uid, out ScenarioSpawnMarkerCooldownComponent? cooldown) ||
            cooldown.NextAvailableAt <= time;

    private void ApplyScenarioMarkerCooldown(EntityUid uid)
    {
        if (!_entityManager.TryGetComponent(uid, out ScenarioSpawnMarkerCooldownComponent? cooldown))
            return;

        cooldown.NextAvailableAt = _timing.CurTime + cooldown.Cooldown;
        Dirty(uid, cooldown);
    }

    private bool TryResolveScenarioPlanSpawnMarkers(ThirdPartyPrototype party,
        MapId mapId,
        Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)>? assignedJobs,
        out ResolvedThirdPartySpawnMarkerSet? markers,
        bool logFailure = true,
        bool coveredScenarioForce = false)
    {
        markers = null;

        try
        {
            ScenarioPlanValidationRequest request = BuildThirdPartySpawnScenarioPlanRequest(assignedJobs);
            if (_scenarioPlan.TryResolveThirdPartySpawnMarkers(request, party.ID, mapId, out markers,
                out string diagnostic))
            {
                _sawmill.Debug($"[ThirdPartySystem] Using Scenario Plan Spawn Markers for third party '{party.ID
                }' on map {mapId}.");
                return true;
            }

            if (logFailure)
            {
                string backupDiagnostic = coveredScenarioForce
                    ? "covered Round Groups do not use legacy marker lookup"
                    : "falling back to legacy marker lookup";
                _sawmill.Warning($"[ThirdPartySystem] Could not resolve Scenario Plan Spawn Markers for third party '{
                    party.ID}' on map {mapId}; {backupDiagnostic}. {diagnostic}");
            }
        }
        catch (Exception ex)
        {
            if (logFailure)
            {
                string backupDiagnostic = coveredScenarioForce
                    ? "covered Round Groups do not use legacy marker lookup"
                    : "falling back to legacy marker lookup";
                _sawmill.Error($"[ThirdPartySystem] Scenario Plan Spawn Marker resolution threw for third party '{
                    party.ID}' on map {mapId}; {backupDiagnostic}. {ex}");
            }
        }

        markers = null;
        return false;
    }

    private ScenarioPlanValidationRequest BuildThirdPartySpawnScenarioPlanRequest(
        Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)>? assignedJobs)
    {
        int playerCount = Math.Max(_playerManager.PlayerCount, assignedJobs?.Count ?? 0);

        return new(
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
            TryComp(mind.Value, out MindComponent? mindComp) &&
            !string.IsNullOrWhiteSpace(mindComp.CharacterName))
            return mindComp.CharacterName;

        if (_preferences.GetPreferencesOrNull(player.UserId)?.SelectedCharacter is HumanoidCharacterProfile profile &&
            !string.IsNullOrWhiteSpace(profile.Name))
            return profile.Name;

        return fallback;
    }

    private void ApplyPlayerCharacterName(EntityUid mob, string characterName)
    {
        if (!HasComp<HumanoidAppearanceComponent>(mob))
            return;

        if (string.IsNullOrWhiteSpace(characterName))
            return;

        _metaData.SetEntityName(mob, characterName);

        if (_idCard.TryFindIdCard(mob, out Entity<IdCardComponent> idCard))
            _idCard.TryChangeFullName(idCard.Owner, characterName, idCard.Comp);

        _identity.QueueIdentityUpdate(mob);
    }

    public void StartThirdPartySpawning(ThreatPrototype threat,
        Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)>? assignedJobs = null)
    {
        _currentThreat = threat;
        _thirdPartyList = _auRoundSystem.SelectedThirdParties.ToList();
        _nextThirdPartyIndex = 0;
        _spawnTimer = 0f;
        _spawnInterval = TimeSpan.FromSeconds(Math.Max(1, threat.ThirdPartyInterval));

        var roundstartCount = 0;
        foreach (ThirdPartyPrototype party in _thirdPartyList)
        {
            if (party.RoundStart)
                roundstartCount++;
        }

        ThirdPartyAssignmentCounts assignmentCounts = ThirdPartySystem.CountThirdPartyAssignments(assignedJobs);
        _sawmill.Info(
            $"[ThirdPartySystem] Starting third-party queue: threat={threat.ID}, selected={_thirdPartyList.Count
            }, roundstart={roundstartCount}, interval={_spawnInterval}, assignedJobs={assignedJobs?.Count ?? 0
            }, assignedThirdPartyLeaders={assignmentCounts.Leaders}, assignedThirdPartyMembers={assignmentCounts.Members
            }.");

        if (_thirdPartyList.Count == 0)
        {
            _sawmill.Debug(
                "[ThirdPartySystem] No third parties selected for this planet; skipping third-party spawning.");
            _spawningActive = false;
            return;
        }

        _spawningActive = true;

        // Spawn all roundstart third parties immediately (called after jobs assigned)
        foreach (ThirdPartyPrototype party in _thirdPartyList)
        {
            if (!party.RoundStart)
                break;

            _sawmill.Debug($"[ThirdPartySystem] Attempting roundstart third-party ({party.ID}) with PartySpawn={
                party.PartySpawn}.");
            if (_prototypeManager.TryIndex(party.PartySpawn, out PartySpawnPrototype? spawnProto))
            {
                if (SpawnThirdParty(party, spawnProto, true, assignedJobs))
                    _sawmill.Debug($"[ThirdPartySystem] Spawned roundstart third party ({party.ID})");
                else
                {
                    _sawmill.Warning(
                        $"[ThirdPartySystem] Roundstart spawn attempt for third party ({party.ID}) failed.");
                }
            }
            else
            {
                _sawmill.Error($"[ThirdPartySystem] No spawn proto for roundstart third party ({party.ID}) PartySpawn={
                    party.PartySpawn}");
            }

            _nextThirdPartyIndex++;
        }
    }

    private bool TrySpawnAtMarker(string protoId, List<EntityUid> markerPool, List<EntityUid> spawnedList,
        bool parachuteMode, bool useDropship, string label, ref EntityUid? lastUsedMarker)
    {
        if (markerPool.Count == 0)
        {
            _sawmill.Warning($"[ThirdPartySystem] Cannot spawn {label} ({protoId}): marker pool is empty.");
            return false;
        }

        // Non-dropship pools were pre-filtered for player safety before spawning.
        EntityUid marker = PickRandomMarker(markerPool, parachuteMode && !useDropship);

        if (marker == EntityUid.Invalid)
        {
            _sawmill.Warning($"[ThirdPartySystem] Cannot spawn {label} ({protoId}): selected marker is invalid.");
            return false;
        }

        EntityCoordinates coords = _entityManager.GetComponent<TransformComponent>(marker).Coordinates;
        try
        {
            EntityUid ent = _entityManager.SpawnEntity(protoId, coords);

            // If parachute mode, hand off to the shared paradrop system so the entity falls from the sky.
            if (parachuteMode)
            {
                // Ensure the entity is paradroppable; SharedParaDropSystem will fall back to crash-land if missing.
                var paraComp = EnsureComp<ParaDroppableComponent>(ent);
                Dirty(ent, paraComp);

                // Raise AttemptCrashLandEvent on the grid entity that the parachute marker resides on so the para-drop
                // handler will run.
                var markerXform = _entityManager.GetComponent<TransformComponent>(marker);
                if (markerXform.GridUid.HasValue)
                {
                    var attemptEvent = new AttemptCrashLandEvent(ent);
                    RaiseLocalEvent(markerXform.GridUid.Value, ref attemptEvent);
                }
            }

            spawnedList.Add(ent);

            // Put marker on a cooldown
            if (!parachuteMode
                && _entityManager.TryGetComponent(marker,
                    out ThreatSpawnMarkerComponent? markerComp))
            {
                markerComp.NextAvailableAt = _timing.CurTime + markerComp.Cooldown;
                Dirty(marker, markerComp);
            }
            else if (!parachuteMode) ApplyScenarioMarkerCooldown(marker);

            // Parachute markers are intentionally NOT marked as used so they may be reused.
            lastUsedMarker = marker;
            if (!parachuteMode)
                markerPool.Remove(marker); // prevent stacking

            _sawmill.Debug($"[ThirdPartySystem] Spawned {label} {protoId} at {coords} (entity {ent})");
            return true;
        }
        catch (Exception ex)
        {
            _sawmill.Error($"[ThirdPartySystem] Failed to spawn {label} ({protoId}) at {coords}! {ex.Message}");
            return false;
        }
    }

    private bool IsMarkerBlockedByPlayers(EntityUid marker)
    {
        const float PlayerAvoidRadius = 8f;

        // Only check main-map/groundside markers; dropship spawns handled elsewhere via useDropship
        EntityCoordinates markerCoords = _entityManager.GetComponent<TransformComponent>(marker).Coordinates;
        foreach (ICommonSession session in _playerManager.Sessions)
        {
            if (!session.AttachedEntity.HasValue)
                continue;

            EntityUid attached = session.AttachedEntity.Value;

            // Skip ghosts
            if (_entityManager.HasComponent<GhostComponent>(attached))
                continue;

            if (!_entityManager.TryGetComponent(attached, out TransformComponent? playerXform))
                continue;

            if (_transform.InRange(playerXform.Coordinates, markerCoords, PlayerAvoidRadius))
            {
                _sawmill.Debug($"[ThirdPartySystem] Marker {marker} is blocked by player {attached} within radius {
                    PlayerAvoidRadius}");
                return true;
            }
        }

        return false;
    }

    private EntityUid PickRandomMarker(List<EntityUid> candidates, bool remove)
    {
        if (candidates.Count == 0)
            return EntityUid.Invalid;

        int index = _random.Next(candidates.Count);
        EntityUid marker = candidates[index];
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

        for (var i = 0; i < playerIds.Count && i < spawnedList.Count; i++)
        {
            NetUserId playerNetId = playerIds[i];
            EntityUid entity = spawnedList[i];
            try
            {
                if (!_playerManager.TryGetSessionById(playerNetId, out ICommonSession? session))
                    continue;

                ticker.PlayerJoinGame(session, true);

                ContentPlayerData? data = session.ContentData();
                EntityUid? mind = mindSystem.GetMind(playerNetId);
                string characterName = GetPlayerCharacterName(session, mind, data?.Name ?? "Third Party Player");
                ApplyPlayerCharacterName(entity, characterName);

                mind ??= mindSystem.CreateMind(playerNetId, characterName);
                mindSystem.SetUserId(mind.Value, playerNetId);
                mindSystem.TransferTo(mind.Value, entity);
                roleSystem.MindAddJobRole(mind.Value, silent: true, jobPrototype: jobProto);
                assigned++;
            }
            catch (Exception ex)
            {
                _sawmill.Error($"[ThirdPartySystem] Failed to assign {roleLabel} mind (player {playerNetId}, entity {
                    entity}): {ex}");
            }
        }

        _sawmill.Info(
            $"[ThirdPartySystem] Third-party {roleLabel} mind assignment result: players={playerIds.Count}, bodies={
                spawnedList.Count}, assigned={assigned}, job={jobProto}.");
        if (playerIds.Count > spawnedList.Count)
        {
            _sawmill.Warning($"[ThirdPartySystem] Third-party {roleLabel} assignment had {playerIds.Count
            } player(s) but only {spawnedList.Count} body/bodies.");
        }
    }

    private readonly record struct ThirdPartyAssignmentCounts(int Leaders, int Members);
}
