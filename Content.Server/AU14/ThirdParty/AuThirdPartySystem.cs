using System.Linq;
using Content.Server.AU14.Round;
using Content.Shared.AU14.Threats;
using Robust.Shared.Map;
using Content.Shared.Roles;
using Content.Shared.Mind;
using Content.Server.GameTicking;
using Content.Shared._RMC14.Dropship;
using Content.Shared.AU14.util;
using Content.Shared.Players;
using Robust.Shared.Random;
using Robust.Server.Player;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Content.Server.AU14.VendorMarker;
using Content.Shared.Ghost;
using Robust.Shared.Console;

namespace Content.Server.AU14.ThirdParty;

public sealed class AuThirdPartySystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    private readonly ISawmill _sawmill = Logger.GetSawmill("thirdparty");
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly AuRoundSystem _auRoundSystem = default!;
    [Dependency] private readonly IConsoleHost _consoleHost = default!;

    // --- State for round third party spawning ---
    private ThreatPrototype? _currentThreat;
    private List<AuThirdPartyPrototype>? _thirdPartyList;
    private int _nextThirdPartyIndex = 0;
    private float _spawnTimer = 0f;
    private TimeSpan _spawnInterval = TimeSpan.FromMinutes(5);
    private bool _spawningActive = false;

    public void SpawnThirdParty(AuThirdPartyPrototype party, PartySpawnPrototype spawnProto, bool roundStart, Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)>? assignedJobs = null, bool? overrideDropship = null)
    {
        _sawmill.Debug($"[AuThirdPartySystem] Spawning third party: {party.ID}");
        bool useDropship = overrideDropship ?? party.Enterbyshuttle;
        _sawmill.Debug($"[AuThirdPartySystem] Dropship override: {overrideDropship}, using dropship: {useDropship}");
        List<EntityUid> markerEntities = new();
        EntityUid mainGridUid = EntityUid.Invalid;
        if (useDropship)
        {
            // Dropship step
            var foundDestination = false;
            EntityUid? chosenDestination = null;
            var destQuery = _entityManager.EntityQueryEnumerator<DropshipDestinationComponent, TransformComponent>();
            while (destQuery.MoveNext(out var destUid, out var destComp, out var destXform))
            {
                if (destComp.Ship == null && string.IsNullOrEmpty(destComp.FactionController))
                {
                    foundDestination = true;
                    chosenDestination = destUid;
                    break;
                }
            }
            if (!foundDestination)
            {
                _sawmill.Error("[AuThirdPartySystem] No valid dropship destination found (not landed, not controlled). Aborting third party spawn.");
                return;
            }
            _sawmill.Debug($"[AuThirdPartySystem] Found valid dropship destination: {chosenDestination}");
            // Dropship grid load
            if (!_mapLoader.TryLoadMap(party.dropshippath, out var dropshipMap, out var grids))
            {
                _sawmill.Error($"[AuThirdPartySystem] Failed to load dropship map: {party.dropshippath}");
                return;
            }
            mainGridUid = grids.FirstOrDefault();
            if (mainGridUid == EntityUid.Invalid)
            {
                _sawmill.Error($"[AuThirdPartySystem] No grids found in dropship map: {party.dropshippath}");
                return;
            }
            _sawmill.Debug($"[AuThirdPartySystem] Dropship grid initialized: {mainGridUid}");
            // Collect markers on dropship grid
            var query = _entityManager.EntityQueryEnumerator<AuInsertMarkerComponent>();
            while (query.MoveNext(out var uid, out _))
            {
                var gridUid = _entityManager.GetComponent<TransformComponent>(uid).GridUid;
                if (gridUid != null && gridUid.Value == mainGridUid)
                    markerEntities.Add(uid);
            }
            _sawmill.Debug($"[AuThirdPartySystem] Dropship markers collected: {markerEntities.Count}");
            // Spawn consoles
            var vmarkerQuery = _entityManager.EntityQueryEnumerator<VendorMarkerComponent>();
            int consoleCount = 0;
            while (vmarkerQuery.MoveNext(out var vmarkerUid, out var vmarkerComp))
            {
                var markerXform = _entityManager.GetComponent<TransformComponent>(vmarkerUid);
                if (markerXform.GridUid != mainGridUid)
                    continue;
                switch (vmarkerComp.Class)
                {
                    case PlatoonMarkerClass.DSPilot:
                        _entityManager.SpawnEntity("CMComputerDropshipNavigationThirdParty", markerXform.Coordinates);
                        consoleCount++;
                        break;
                    case PlatoonMarkerClass.DSWeapons:
                        _entityManager.SpawnEntity("CMComputerDropshipWeapons", markerXform.Coordinates);
                        consoleCount++;
                        break;
                }
            }
            _sawmill.Debug($"[AuThirdPartySystem] Dropship consoles spawned: {consoleCount}");
        }
        else
        {
            // No dropship: collect all markers on main map
            var query = _entityManager.EntityQueryEnumerator<AuInsertMarkerComponent>();
            while (query.MoveNext(out var uid, out _))
            {
                markerEntities.Add(uid);
            }
            _sawmill.Debug($"[AuThirdPartySystem] Main map markers collected: {markerEntities.Count}");
        }

        var newpartySpawn = spawnProto;
        MapId? mapId = null;
        if (party.Enterbyshuttle)
        {
            if (markerEntities.Count > 0)
            {
                mapId = _entityManager.GetComponent<TransformComponent>(markerEntities[0]).MapID;
            }
        }
        else if (markerEntities.Count > 0)
        {
            mapId = _entityManager.GetComponent<TransformComponent>(markerEntities[0]).MapID;
        }
        List<EntityUid> GetMarkers(Content.Shared.AU14.Threats.ThreatMarkerType markerType)
        {
            var markerId = newpartySpawn != null && newpartySpawn.Markers.TryGetValue(markerType, out var id) ? id : "";
            var markers = new List<EntityUid>();
            var query = _entityManager.EntityQueryEnumerator<Content.Shared.AU14.Threats.ThreatSpawnMarkerComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                if (comp.ThreatMarkerType == markerType && (comp.ID == markerId || (comp.ID == "" && markerId == "")))
                {
                    if (mapId == null || _entityManager.GetComponent<TransformComponent>(uid).MapID == mapId)
                        markers.Add(uid);
                }
            }
            _sawmill.Debug($"[AuThirdPartySystem] GetMarkers({markerType}): Found {markers.Count} markers with markerId '{markerId}' on map {mapId}");
            return markers;
        }
        // --- Spawn Together logic ---
        bool spawnTogether = newpartySpawn?.SpawnTogether == true;
        Dictionary<Content.Shared.AU14.Threats.ThreatMarkerType, List<EntityUid>> markerCache = new();
        EntityUid? centerMarker = null;
        if (spawnTogether)
        {
            // Gather all markers of all types
            var allMarkers = new List<EntityUid>();
            foreach (Content.Shared.AU14.Threats.ThreatMarkerType type in System.Enum.GetValues(typeof(Content.Shared.AU14.Threats.ThreatMarkerType)))
            {
                allMarkers.AddRange(GetMarkers(type));
            }
            if (allMarkers.Count > 0)
            {
                centerMarker = allMarkers[_random.Next(allMarkers.Count)];
                var centerCoords = _entityManager.GetComponent<TransformComponent>(centerMarker.Value).Coordinates;
                foreach (Content.Shared.AU14.Threats.ThreatMarkerType type in System.Enum.GetValues(typeof(Content.Shared.AU14.Threats.ThreatMarkerType)))
                {
                    var markers = GetMarkers(type);
                    var filtered = markers.Where(m =>
                    {
                        var coords = _entityManager.GetComponent<TransformComponent>(m).Coordinates;
                        return coords.InRange(_entityManager, centerCoords, 50f);
                    }).ToList();
                    // Fallback to all markers if none are in range
                    markerCache[type] = filtered.Count > 0 ? filtered : markers;
                }
            }
        }
        List<EntityUid> GetSpawnMarkers(Content.Shared.AU14.Threats.ThreatMarkerType type)
        {
            if (spawnTogether && markerCache.TryGetValue(type, out var cached))
                return cached;
            return GetMarkers(type);
        }

        var spawnedLeaders = new List<EntityUid>();
        var spawnedGrunts = new List<EntityUid>();
        var SpawnedEnts = new List<EntityUid>();
        // Before spawning leaders
        var leaderMarkers = GetSpawnMarkers(Content.Shared.AU14.Threats.ThreatMarkerType.Leader);
        if (leaderMarkers.Count == 0)
        {
            _sawmill.Warning($"[AuThirdPartySystem] No leader markers found for {party.ID}, skipping leader spawns.");
        }
        else
        {
            _sawmill.Debug($"[AuThirdPartySystem] Spawning leaders...");
            foreach (var (protoId, count) in spawnProto.LeadersToSpawn)
            {
                for (int i = 0; i < count; i++)
                {
                    var marker = leaderMarkers[_random.Next(leaderMarkers.Count)];
                    var coords = _entityManager.GetComponent<TransformComponent>(marker).Coordinates;
                    var ent = _entityManager.SpawnEntity(protoId, coords);
                    spawnedLeaders.Add(ent);
                    _sawmill.Debug($"[AuThirdPartySystem] Spawned leader {protoId} at {coords} (entity {ent})");
                }
            }
        }
        // Before spawning grunts
        var gruntMarkers = GetSpawnMarkers(Content.Shared.AU14.Threats.ThreatMarkerType.Member);
        if (gruntMarkers.Count == 0)
        {
            _sawmill.Warning($"[AuThirdPartySystem] No grunt/member markers found for {party.ID}, ski   `pping grunt/member spawns.");
        }
        else
        {
            _sawmill.Debug($"[AuThirdPartySystem] Spawning grunts...");
            foreach (var (protoId, count) in spawnProto.GruntsToSpawn)
            {
                for (int i = 0; i < count; i++)
                {
                    var marker = gruntMarkers[_random.Next(gruntMarkers.Count)];
                    var coords = _entityManager.GetComponent<TransformComponent>(marker).Coordinates;
                    var ent = _entityManager.SpawnEntity(protoId, coords);
                    spawnedGrunts.Add(ent);
                    _sawmill.Debug($"[AuThirdPartySystem] Spawned grunt {protoId} at {coords} (entity {ent})");
                }
            }
        }
        // Before spawning ents
        var entityMarkers = GetSpawnMarkers(Content.Shared.AU14.Threats.ThreatMarkerType.Entity);
        if (entityMarkers.Count == 0)
        {
            _sawmill.Warning($"[AuThirdPartySystem] No entity markers found for {party.ID}, skipping entity spawns.");
        }
        else
        {
            _sawmill.Debug($"[AuThirdPartySystem] Spawning ents...");
            foreach (var (protoId, count) in spawnProto.entitiestospawn)
            {
                for (int i = 0; i < count; i++)
                {
                    var marker = entityMarkers[_random.Next(entityMarkers.Count)];
                    var coords = _entityManager.GetComponent<TransformComponent>(marker).Coordinates;
                    var ent = _entityManager.SpawnEntity(protoId, coords);
                    SpawnedEnts.Add(ent);
                    _sawmill.Debug($"[AuThirdPartySystem] Spawned ent {protoId} at {coords} (entity {ent})");
                }
            }
        }

        if (roundStart && assignedJobs != null)
        {
            _sawmill.Debug($"[AuThirdPartySystem] Assigning minds to third party entities (roundstart)");
            var leaderJobId = new ProtoId<JobPrototype>("AU14JobThreatLeader");
            var memberJobId = new ProtoId<JobPrototype>("AU14JobThreatMember");
            var leaderPlayers = assignedJobs.Where(x => x.Value.Item1 == leaderJobId).Select(x => x.Key).ToList();
            var memberPlayers = assignedJobs.Where(x => x.Value.Item1 == memberJobId).Select(x => x.Key).ToList();
            var mindSystem = _entityManager.System<SharedMindSystem>();
            var roleSystem = _entityManager.System<SharedRoleSystem>();
            for (int i = 0; i < leaderPlayers.Count && i < spawnedLeaders.Count; i++)
            {
                var playerNetId = leaderPlayers[i];
                var entity = spawnedLeaders[i];
                if (!_playerManager.TryGetSessionById(playerNetId, out var session))
                    continue;
                var ticker = _entityManager.System<GameTicker>();
                ticker.PlayerJoinGame(session, silent: true);
                var data = session.ContentData();
                var mind = mindSystem.GetMind(playerNetId) ?? mindSystem.CreateMind(playerNetId, data?.Name ?? "Third Party Player");
                mindSystem.SetUserId(mind, playerNetId);
                mindSystem.TransferTo(mind, entity);
                roleSystem.MindAddJobRole(mind, silent: true, jobPrototype: "AU14JobThreatLeader");
            }
            for (int i = 0; i < memberPlayers.Count && i < spawnedGrunts.Count; i++)
            {
                var playerNetId = memberPlayers[i];
                var entity = spawnedGrunts[i];
                if (!_playerManager.TryGetSessionById(playerNetId, out var session))
                    continue;
                var ticker = _entityManager.System<GameTicker>();
                ticker.PlayerJoinGame(session, silent: true);
                var data = session.ContentData();
                var mind = mindSystem.GetMind(playerNetId) ?? mindSystem.CreateMind(playerNetId, data?.Name ?? "Third Party Player");
                mindSystem.SetUserId(mind, playerNetId);
                mindSystem.TransferTo(mind, entity);
                roleSystem.MindAddJobRole(mind, silent: true, jobPrototype: "AU14JobThreatMember");
            }
        }

    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!_spawningActive || _thirdPartyList == null || _currentThreat == null)
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
        {
            return;
        }
        var interval = TimeSpan.FromTicks((long)(_spawnInterval.Ticks / Math.Max(1, party.weight)));
        if (_spawnTimer < interval.TotalSeconds)
            return;
        _spawnTimer = 0f;
        int roll = _random.Next(1, 101);
        int chance = Math.Clamp(party.weight * 10, 5, 100); // Example: weight 1 = 10%, weight 10 = 100%
        if (roll <= chance)
        {
            if (_prototypeManager.TryIndex(party.PartySpawn, out var spawnProto))
            {
                SpawnThirdParty(party, spawnProto, false);
                _sawmill.Debug($"[AuThirdPartySystem] Spawned third party {party.ID} (roll {roll} <= {chance})");
            }
            else
            {
                _sawmill.Error($"[AuThirdPartySystem] No spawn proto for third party {party.ID} (PartySpawn={party.PartySpawn})");
            }
            _nextThirdPartyIndex++;
        }
        else
        {
            _sawmill.Debug($"[AuThirdPartySystem] Did not spawn {party.ID} (roll {roll} > {chance})");
        }
    }


    public void StartThirdPartySpawning(ThreatPrototype threat)
    {
        _currentThreat = threat;
        _thirdPartyList = _auRoundSystem.SelectedThirdParties.ToList();
        _nextThirdPartyIndex = 0;
        _spawnTimer = 0f;
        _spawningActive = true;
        if (_thirdPartyList == null)
            return;
        // Spawn all roundstart third parties immediately
        foreach (var party in _thirdPartyList)
        {
            if (!party.RoundStart)
                break;
            if (_prototypeManager.TryIndex<PartySpawnPrototype>(party.PartySpawn, out var spawnProto))
            {
                SpawnThirdParty(party, spawnProto, true);
                _sawmill.Debug($"[AuThirdPartySystem] Spawned roundstart third party {party.ID}");
            }
            else
            {
                _sawmill.Error($"[AuThirdPartySystem] No spawn proto for roundstart third party {party.ID} (PartySpawn={party.PartySpawn})");
            }
            _nextThirdPartyIndex++;
        }
    }


}

