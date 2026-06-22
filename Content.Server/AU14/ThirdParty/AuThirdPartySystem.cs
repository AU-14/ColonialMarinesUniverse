using System.Linq;
using Content.Server.Access.Systems;
using Content.Server.AU14.Round;
using Content.Server.AU14.VendorMarker;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.IdentityManagement;
using Content.Server.Preferences.Managers;
using Content.Shared._RMC14.CrashLand;
using Content.Shared._RMC14.Dropship;
using Content.Shared.AU14.Threats;
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

namespace Content.Server.AU14.ThirdParty;

public sealed partial class AuThirdPartySystem : EntitySystem
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
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

        int ghostCount = _playerManager.Sessions.Count(s => s.AttachedEntity == null || _entityManager.HasComponent<GhostComponent>(s.AttachedEntity));
        if (ghostCount < party.GhostsNeeded)
            return;

        var interval = TimeSpan.FromTicks((long)(_spawnInterval.Ticks * _signalIntervalMultiplier));
        if (_spawnTimer < interval.TotalSeconds)
            return;

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
            : (party.EntryMethod?.ToLowerInvariant() ?? "ground");
        _sawmill.Debug($"[AuThirdPartySystem] Entry method: {entryMethod} (overrideDropship={overrideDropship})");

        List<EntityUid> markerEntities = new();
        EntityUid mainGridUid = EntityUid.Invalid;
        bool parachuteMode = false;

        // Maintain compatibility with existing code that uses these locals.
        bool useDropship = entryMethod == "shuttle";
        if (entryMethod == "shuttle")
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
            if (!_mapLoader.TryLoadMap(party.dropshippath, out var dropshipMap, out var grids, deserializationOpts))
            {
                _sawmill.Error($"[AuThirdPartySystem] Failed to load dropship map: {party.dropshippath}");
                return false;
            }

            mainGridUid = grids.FirstOrDefault();
            if (mainGridUid == EntityUid.Invalid)
            {
                _sawmill.Error($"[AuThirdPartySystem] No grids found in dropship map: {party.dropshippath}");
                return false;
            }
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
                try
                {
                    var gridUid = _entityManager.GetComponent<TransformComponent>(uid).GridUid;
                    if (gridUid != null && gridUid.Value == mainGridUid)
                        markerEntities.Add(uid);
                }
                catch (Exception ex) { _sawmill.Debug($"[AuThirdPartySystem] Skipping deleted marker {uid} during dropship collection: {ex}"); }
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
                catch (Exception ex) { _sawmill.Debug($"[AuThirdPartySystem] Skipping vendor marker {vmarkerUid} (class=markerComp.Class) due to component error: {ex}"); }
            }
            _sawmill.Debug($"[AuThirdPartySystem] Dropship consoles spawned: {consoleCount}");
        }
        else if (entryMethod == "parachute")
        {
            // Parachute mode: collect parachute markers on the main map
            parachuteMode = true;
            var pQuery = _entityManager.EntityQueryEnumerator<ParachuteMarkerComponent, TransformComponent>();
            while (pQuery.MoveNext(out var uid, out var pComp, out var pxform))
            {
                // Parachute markers are reusable and do not need to be marked as used; include all of them.
                try { markerEntities.Add(uid); }
                catch (Exception ex) { _sawmill.Debug($"[AuThirdPartySystem] Skipping deleted parachute marker {uid}: {ex}"); }
            }
            _sawmill.Debug($"[AuThirdPartySystem] Parachute markers collected: {markerEntities.Count}");
        }
        else
        {
            // Ground spawn: collect all markers on main map (existing behavior)
            var query = _entityManager.EntityQueryEnumerator<AuInsertMarkerComponent>();
            while (query.MoveNext(out var uid, out _))
            {
                try { markerEntities.Add(uid); }
                catch (Exception ex) { _sawmill.Debug($"[AuThirdPartySystem] Skipping deleted marker {uid} during ground collection: {ex}"); }
            }
            _sawmill.Debug($"[AuThirdPartySystem] Main map markers collected: {markerEntities.Count}");
        }

        MapId? mapId = null;
        if (markerEntities.Count > 0)
            mapId = _entityManager.GetComponent<TransformComponent>(markerEntities[0]).MapID;

        List<EntityUid> GetMarkers(ThreatMarkerType markerType)
        {
            var markerId = spawnProto.Markers.TryGetValue(markerType, out var id) ? id : "";
            var markers = new List<EntityUid>();
            var time = _timing.CurTime;
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
                markers.Add(uid);
            }

            _sawmill.Debug($"[AuThirdPartySystem] GetMarkers({markerType}): Found {markers.Count} unused markers with markerId '{markerId}' on map {mapId}");
            return markers;
        }

        bool spawnTogether = spawnProto.SpawnTogether == true;
        Dictionary<ThreatMarkerType, List<EntityUid>> markerCache = new();
        EntityUid? centerMarker = null;
        if (spawnTogether)
        {
            var allMarkers = new List<EntityUid>();
            foreach (ThreatMarkerType type in Enum.GetValues(typeof(ThreatMarkerType)))
            {
                allMarkers.AddRange(GetMarkers(type));
            }
            if (allMarkers.Count > 0)
            {
                centerMarker = allMarkers[_random.Next(allMarkers.Count)];
                var centerCoords = _entityManager.GetComponent<TransformComponent>(centerMarker.Value).Coordinates;
                foreach (ThreatMarkerType type in Enum.GetValues(typeof(ThreatMarkerType)))
                {
                    var markers = GetMarkers(type);
                    var filtered = markers.Where(m =>
                    {
                        var coords = _entityManager.GetComponent<TransformComponent>(m).Coordinates;
                        return _transform.InRange(coords, centerCoords, 50f);
                    }).ToList();
                    // Fallback to all markers if none are in range
                    markerCache[type] = filtered.Count > 0 ? filtered : markers;
                }
            }
        }

        List<EntityUid> GetSpawnMarkers(ThreatMarkerType type)
        {
            if (spawnTogether && markerCache.TryGetValue(type, out var cached))
                return cached;
            return GetMarkers(type);
        }

        var spawnedLeaders = new List<EntityUid>();
        var spawnedGrunts = new List<EntityUid>();
        var spawnedEnts = new List<EntityUid>();
        // Track the last marker we used during this spawn operation
        EntityUid? lastUsedMarker = null;
        // Before spawning, verify we have enough unused markers for each required type. If not, abort the spawn.
        var leaderReq = spawnProto.LeadersToSpawn.Values.Sum();
        var gruntReq = spawnProto.GruntsToSpawn.Values.Sum();
        var entityReq = spawnProto.EntitiesToSpawn.Values.Sum();

        var leaderMarkers = GetSpawnMarkers(ThreatMarkerType.Leader);
        var gruntMarkers = GetSpawnMarkers(ThreatMarkerType.Member);
        var entityMarkers = GetSpawnMarkers(ThreatMarkerType.Entity);

        List<EntityUid> FilterByType(ThreatMarkerType type) =>
            markerEntities.Where(m => _entityManager.TryGetComponent<ThreatSpawnMarkerComponent>(m, out var comp)
                && comp.ThirdParty
                && comp.ThreatMarkerType == type).ToList();

        // If parachute mode, use the parachute marker pool for all types; make local mutable copies so we can pick without replacement during this spawn
        if (parachuteMode)
        {
            // Parachute markers must still have a ThreatSpawnMarkerComponent with ThirdParty==true
            leaderMarkers = FilterByType(ThreatMarkerType.Leader);
            gruntMarkers = FilterByType(ThreatMarkerType.Member);
            entityMarkers = FilterByType(ThreatMarkerType.Entity);
        }

        // If this is a groundside spawn, ensure there are enough *safe* markers (unused and not near alive players).
        if (!useDropship)
        {
            var safeLeaderMarkers = leaderMarkers.Where(m => !IsMarkerBlockedByPlayers(m)).ToList();
            var safeGruntMarkers = gruntMarkers.Where(m => !IsMarkerBlockedByPlayers(m)).ToList();
            var safeEntityMarkers = entityMarkers.Where(m => !IsMarkerBlockedByPlayers(m)).ToList();

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

        // Spawn leaders
        _sawmill.Debug($"[AuThirdPartySystem] Spawning leaders...");
        foreach (var (protoId, count) in spawnProto.LeadersToSpawn)
            for (int i = 0; i < count; i++)
                if (!TrySpawnAtMarker(protoId, leaderMarkers, spawnedLeaders, parachuteMode, useDropship, "leader", ref lastUsedMarker))
                    _sawmill.Warning($"[AuThirdPartySystem] Failed to spawn leader {protoId}");

        // Spawn grunts
        _sawmill.Debug($"[AuThirdPartySystem] Spawning grunts...");
        foreach (var (protoId, count) in spawnProto.GruntsToSpawn)
            for (int i = 0; i < count; i++)
                if (!TrySpawnAtMarker(protoId, gruntMarkers, spawnedGrunts, parachuteMode, useDropship, "grunt", ref lastUsedMarker))
                    _sawmill.Warning($"[AuThirdPartySystem] Failed to spawn grunt {protoId}");

        // Spawn ents
        _sawmill.Debug($"[AuThirdPartySystem] Spawning ents...");
        foreach (var (protoId, count) in spawnProto.EntitiesToSpawn)
            for (int i = 0; i < count; i++)
                if (!TrySpawnAtMarker(protoId, entityMarkers, spawnedEnts, parachuteMode, useDropship, "ent", ref lastUsedMarker))
                    _sawmill.Warning($"[AuThirdPartySystem] Failed to spawn entity {protoId}");

        // After all spawns: if spawnTogether is true, mark nearby unused markers around the last used marker.
        void MarkNeighborsIfNeeded()
        {
            if (!spawnTogether || lastUsedMarker == null)
                return;

            var centerMarkerUid = lastUsedMarker.Value;
            if (!_entityManager.TryGetComponent<ThreatSpawnMarkerComponent>(centerMarkerUid, out var centerComp))
                return;

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
        }

        // Run neighbor-marking now (only once per spawn operation, using the last used marker)
        MarkNeighborsIfNeeded();

        if (roundStart && assignedJobs != null)
        {
            var leaderJobId = new ProtoId<JobPrototype>("AU14JobThirdPartyLeader");
            var memberJobId = new ProtoId<JobPrototype>("AU14JobThirdPartyMember");
            var leaderPlayers = assignedJobs.Where(x => x.Value.Item1 == leaderJobId).Select(x => x.Key).ToList();
            var memberPlayers = assignedJobs.Where(x => x.Value.Item1 == memberJobId).Select(x => x.Key).ToList();

            _sawmill.Debug($"[AuThirdPartySystem] Assigning minds to third party entities (roundstart)");
            AssignMinds(leaderPlayers, spawnedLeaders, "AU14JobThirdPartyLeader", "leader");
            AssignMinds(memberPlayers, spawnedGrunts, "AU14JobThirdPartyMember", "member");
        }

        if (!string.IsNullOrWhiteSpace(party.AnnounceArrival))
        {
            _chat.DispatchGlobalAnnouncement(party.AnnounceArrival, "", playSound: false, colorOverride: Color.DarkOrange);
            _sawmill.Info($"[AuThirdPartySystem] Announced arrival for third party ({party.ID}): {party.AnnounceArrival}");
        }

        return true;
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

        try { _spawnInterval = TimeSpan.FromSeconds(Math.Max(1, _currentThreat.ThirdPartyInterval)); }
        catch { _sawmill.Warning($"[AuThirdPartySystem] Invalid ThirdPartyInterval on threat {_currentThreat?.ID}; using default interval."); }

        if (_thirdPartyList == null || _thirdPartyList.Count == 0)
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
        // Select a groundside marker that is not too close to alive players (exclude freshly spawned entities)
        EntityUid marker;
        if (parachuteMode && !useDropship)
        {
            // pick a random safe marker from leaderMarkers and remove it so it's not reused this spawn
            var safe = markerPool.Where(m => !IsMarkerBlockedByPlayers(m)).ToList();
            marker = safe.Count > 0
                ? safe[_random.Next(safe.Count)]
                : PickSafeMarker(markerPool);

            markerPool.Remove(marker); // prevent stacking
        }
        else
            marker = useDropship
                ? markerPool[_random.Next(markerPool.Count)]
                : PickSafeMarker(markerPool);

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
        _sawmill.Debug($"[AuThirdPartySystem] Checking marker {marker} at coords {markerCoords}");

        foreach (var session in _playerManager.Sessions)
        {
            if (!session.AttachedEntity.HasValue)
            {
                _sawmill.Debug($"[AuThirdPartySystem] Session has no attached entity, skipping");
                continue;
            }

            var attached = session.AttachedEntity.Value;
            _sawmill.Debug($"[AuThirdPartySystem] Found attached entity {attached} for session");

            // Skip ghosts
            if (_entityManager.HasComponent<GhostComponent>(attached))
            {
                _sawmill.Debug($"[AuThirdPartySystem] Attached entity {attached} is a ghost, skipping");
                continue;
            }

            if (!_entityManager.TryGetComponent<TransformComponent>(attached, out var playerXform))
            {
                _sawmill.Debug($"[AuThirdPartySystem] Could not get TransformComponent for attached entity {attached}, skipping");
                continue;
            }

            // Log check steps for debugging
            _sawmill.Debug($"[AuThirdPartySystem] Checking player {attached} for proximity to marker {marker} (player coords={playerXform.Coordinates}, marker coords={markerCoords})");

            if (_transform.InRange(playerXform.Coordinates, markerCoords, PlayerAvoidRadius))
            {
                _sawmill.Debug($"[AuThirdPartySystem] Marker {marker} is blocked by player {attached} within radius {PlayerAvoidRadius}");
                return true;
            }
            else
            {
                _sawmill.Debug($"[AuThirdPartySystem] Player {attached} not within avoid radius of marker {marker}");
            }
        }

        return false;
    }

    private EntityUid PickSafeMarker(List<EntityUid> candidates)
    {
        if (candidates.Count == 0)
            return EntityUid.Invalid;

        // Shuffle candidates for fairness
        var shuffled = candidates.OrderBy(_ => _random.Next()).ToList();
        foreach (var m in shuffled)
        {
            if (!IsMarkerBlockedByPlayers(m))
                return m;
        }

        // Fallback: no safe marker found, return a random one
        return candidates[_random.Next(candidates.Count)];
    }

    private void AssignMinds(List<NetUserId> playerIds, List<EntityUid> spawnedList, string jobProto, string roleLabel)
    {
        var mindSystem = _entityManager.System<SharedMindSystem>();
        var roleSystem = _entityManager.System<SharedRoleSystem>();
        var ticker = _entityManager.System<GameTicker>();

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
            }
            catch (Exception ex)
            {
                _sawmill.Error($"[AuThirdPartySystem] Failed to assign {roleLabel} mind (player {playerNetId}, entity {entity}): {ex}");
            }
        }
    }
}
