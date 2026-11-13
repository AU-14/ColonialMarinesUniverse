using System.Linq;
using Content.Server.CMU14.Diagnostics.Performance;
using Content.Server.CMU14.Round;
using Content.Server.CMU14.Ship;
using Content.Server.CMU14.Round.Objectives;
using Content.Server.GameTicking;
using Content.Server.Chat.Managers;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Preferences.Managers;
using Content.Shared.CMU14.Threats;
using Content.Shared.CMU14.Yautja;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared._RMC14.Xenonids.Construction.Nest;
using Content.Shared.CMU14.Threats.Mobs.Biomorph;
using Content.Shared.CMU14.Threats.Mobs.Wendigo.Pack;
using Content.Shared.CMU14.util;
using Content.Shared.Ghost.Components;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Players;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Content.Shared.SSDIndicator;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using BiomorphComponent = Content.Shared.CMU14.Threats.Mobs.Biomorph.BiomorphComponent;
using ApeComponent = Content.Shared.CMU14.Threats.Mobs.Ape.ApeComponent;
using TribalComponent = Content.Shared.CMU14.Threats.Mobs.Tribal.TribalComponent;

namespace Content.Server.CMU14.Threats;

public sealed partial class ThreatSystem : EntitySystem
{
    [Dependency] private CMUAdriftSystem _adrift = default!;
    [Dependency] private AuRoundSystem _auRound = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private ForceInterestSystem _forceInterest = default!;
    [Dependency] private GhostRoleSystem _ghostRole = default!;
    [Dependency] private SharedMindSystem _mindSystem = default!;
    [Dependency] private NpcFactionSystem _npcFaction = default!;
    [Dependency] private ICMUServerPerformanceDiagnostics _performance = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private PlayTimeTrackingSystem _playTime = default!;
    [Dependency] private IServerPreferencesManager _prefs = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedRoleSystem _roles = default!;
    [Dependency] private ThreatVoteSystem _threatVote = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    private static readonly ProtoId<JobPrototype> ThreatLeaderJobId = new("AU14JobThreatLeader");
    private static readonly ProtoId<JobPrototype> ThreatMemberJobId = new("AU14JobThreatMember");
    private static readonly EntProtoId ThreatMindRoleId = new("MindRoleThreat");
    private static readonly ProtoId<NpcFactionPrototype> ThreatNpcFaction = new("THREAT");

    private static readonly IReadOnlyDictionary<string, JobScaleEntry> EmptyScaling
        = new Dictionary<string, JobScaleEntry>();

    private static readonly ThreatMarkerType[] ThreatMarkerTypes = Enum.GetValues<ThreatMarkerType>();

    private readonly List<PendingThreatForceSpawn> _pendingSpawns = new();

    private readonly ISawmill _sawmill = Logger.GetSawmill("au14.threat");
    public readonly ProtoId<NpcFactionPrototype> threatNPCFaction = "THREAT";

    internal IReadOnlyList<PendingThreatSpawnDebugView> PendingThreatSpawnsForDebug =>
        _pendingSpawns
            .Select(pending => new PendingThreatSpawnDebugView(pending.Threat.ID,
                pending.FireAt))
            .ToList();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
        SubscribeLocalEvent<ThreatComponent, NewXenoEvolvedEvent>(OnThreatEvolved);
    }

    private void OnThreatEvolved(Entity<ThreatComponent> ent, ref NewXenoEvolvedEvent args)
    {
        if (ent.Comp.ObjectiveJob is { } job)
            AddThreatFaction(args.NewXeno, job);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        while (_pendingSpawns.Count > 0 && _timing.CurTime >= _pendingSpawns[0].FireAt)
        {
            PendingThreatForceSpawn pending = _pendingSpawns[0];
            _pendingSpawns.RemoveAt(0);

            _forceInterest.SetReady(pending.InterestId);
        }
    }

    internal static bool IsThreatJob(ProtoId<JobPrototype>? job)
        => job == ThreatLeaderJobId || job == ThreatMemberJobId;

    private static ThreatAssignmentCounts CountThreatAssignments(
        Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assignedJobs)
    {
        var leaders = 0;
        var members = 0;
        foreach ((NetUserId _, (ProtoId<JobPrototype>? job, EntityUid _)) in assignedJobs)
        {
            if (job == ThreatLeaderJobId)
                leaders++;
            else if (job == ThreatMemberJobId)
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

    internal static int RemoveThreatJobAssignments(
        Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assignedJobs,
        IReadOnlySet<NetUserId>? keepPlayers = null)
    {
        var removed = 0;
        foreach ((NetUserId player, (ProtoId<JobPrototype>? job, EntityUid _)) in assignedJobs.ToArray())
        {
            if (!ThreatSystem.IsThreatJob(job))
                continue;

            if (keepPlayers != null && keepPlayers.Contains(player))
                continue;

            assignedJobs.Remove(player);
            removed++;
        }

        return removed;
    }

    private void ReleaseVoteHeldPlayers(IReadOnlyCollection<NetUserId>? voteHeldPlayers,
        string threatId,
        string reason,
        bool respawn)
    {
        if (voteHeldPlayers == null || voteHeldPlayers.Count == 0)
            return;

        _threatVote.UnblockRoundJoinsForPlayers(voteHeldPlayers);

        if (!respawn)
        {
            _sawmill.Debug($"[ThreatSystem] Released {voteHeldPlayers.Count} held threat vote player(s) after {reason
            } for '{
                threatId}'.");

            return;
        }

        foreach (NetUserId playerId in voteHeldPlayers)
        {
            if (!_playerManager.TryGetSessionById(playerId, out ICommonSession? session)
                || session.Status == SessionStatus.Disconnected)
                continue;

            _sawmill.Info($"[ThreatSystem] Releasing held threat vote player {session.Name} ({playerId}) for '{threatId
            }' because {reason}; returning them to lobby.");
            _ticker.Respawn(session);
        }
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        if (ev.New != GameRunLevel.InRound)
            _pendingSpawns.Clear();
    }

    /// <summary>
    ///     Queues the threat for player interest, with an additional random arrival delay in Colony Fall.
    ///     Win conditions begin when the force actually deploys.
    /// </summary>
    public void SpawnThreatAtRoundStart(ThreatPrototype threat,
        MapId mapId,
        Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assignedJobs)
    {
        if (threat == null)
        {
            _sawmill.Debug("[ThreatSystem] No threat selected for round start, skipping threat spawn.");

            return;
        }

        if (_sawmill.Level <= LogLevel.Debug)
        {
            ThreatAssignmentCounts assignmentCounts = ThreatSystem.CountThreatAssignments(assignedJobs);
            _sawmill.Debug($"[ThreatSystem] Roundstart threat spawn requested: threat={threat.ID}, map={mapId
            }, assignedJobs={
                assignedJobs.Count}, threatLeaders={assignmentCounts.Leaders}, threatMembers={
                    assignmentCounts.Members}, roundStartSpawn={threat.RoundStartSpawn}.");
        }

        bool isColonyFall = string.Equals(_auRound.SelectedPreset?.ID, "ColonyFall",
            StringComparison.OrdinalIgnoreCase);
        int playerCount = assignedJobs.Count;
        int playerBudget = ThreatVoteSelection.CalculateThreatPlayerBudget(playerCount, threat.ThreatRatio);

        if (isColonyFall)
        {
            double delaySeconds = _random.NextDouble() * (threat.SpawnDelayMax - threat.SpawnDelayMin)
                + threat.SpawnDelayMin;
            _sawmill.Debug($"[ThreatSystem] Colony Fall threat '{threat.ID}' will spawn in {delaySeconds:F1}s.");
            SchedulePendingThreatSpawn(threat,
                mapId,
                assignedJobs,
                TimeSpan.FromSeconds(delaySeconds),
                playerCount: playerCount,
                playerBudget: playerBudget);
        }
        else
        {
            QueueThreat(threat, mapId, assignedJobs, playerCount: playerCount, playerBudget: playerBudget);
        }
    }

    public void SpawnThreatFromVote(ThreatPrototype threat,
        MapId mapId,
        Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assignedJobs,
        IReadOnlyList<NetUserId> heldPlayers,
        int playerCount)
    {
        if (threat == null)
        {
            _sawmill.Debug("[ThreatSystem] No threat selected from vote, skipping threat spawn.");
            ReleaseVoteHeldPlayers(heldPlayers, "null", "no threat was selected", true);

            return;
        }

        if (_sawmill.Level <= LogLevel.Debug)
        {
            ThreatAssignmentCounts assignmentCounts = ThreatSystem.CountThreatAssignments(assignedJobs);
            _sawmill.Debug($"[ThreatSystem] Voted threat spawn requested: threat={threat.ID}, map={mapId
            }, assignedJobs={
                assignedJobs.Count}, heldPlayers={heldPlayers.Count}, threatLeaders={assignmentCounts.Leaders
                }, threatMembers={assignmentCounts.Members}, roundStartSpawn={threat.RoundStartSpawn}.");
        }

        bool isColonyFall = string.Equals(_auRound.SelectedPreset?.ID, "ColonyFall",
            StringComparison.OrdinalIgnoreCase);
        int playerBudget = ThreatVoteSelection.CalculateThreatPlayerBudget(playerCount, threat.ThreatRatio);

        if (isColonyFall)
        {
            double delaySeconds = _random.NextDouble() * (threat.SpawnDelayMax - threat.SpawnDelayMin)
                + threat.SpawnDelayMin;
            _sawmill.Debug($"[ThreatSystem] Colony Fall voted threat '{threat.ID}' will spawn in {delaySeconds:F1}s.");
            SchedulePendingThreatSpawn(threat,
                mapId,
                assignedJobs,
                TimeSpan.FromSeconds(delaySeconds),
                heldPlayers,
                playerCount,
                playerBudget);
        }
        else
        {
            QueueThreat(threat, mapId, assignedJobs, heldPlayers, playerCount: playerCount, playerBudget: playerBudget);
        }
    }

    public bool ForceSpawnThreat(ProtoId<ThreatPrototype> threatId, EntityUid fallbackMarker,
        bool startWinConditions = true, bool skipInterest = false)
    {
        if (!_prototypeManager.TryIndex(threatId, out ThreatPrototype? threat) ||
            !_entityManager.TryGetComponent(fallbackMarker, out TransformComponent? transform))
        {
            return false;
        }

        var id = QueueThreat(threat, transform.MapID, new(), startWinConditions: startWinConditions,
            forced: true);
        if (id == null)
            return false;

        // testing and events: do not wait for players to show interest, the bodies become ghost roles
        if (skipInterest)
            _forceInterest.DeployNow(id.Value);

        return true;
    }

    internal void SchedulePendingThreatSpawn(ThreatPrototype threat,
        MapId mapId,
        Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assignedJobs,
        TimeSpan delay,
        IReadOnlyList<NetUserId>? voteHeldPlayers = null,
        int? playerCount = null,
        int? playerBudget = null)
    {
        var id = QueueThreat(threat, mapId, assignedJobs, voteHeldPlayers, false, playerCount, playerBudget);
        if (id == null)
            return;

        EnqueuePendingThreatSpawn(new PendingThreatForceSpawn
        {
            Threat = threat,
            FireAt = _timing.CurTime + delay,
            InterestId = id.Value,
        });
    }

    private uint? QueueThreat(ThreatPrototype threat, MapId mapId,
        Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assignedJobs,
        IReadOnlyList<NetUserId>? voteHeldPlayers = null, bool ready = true,
        int? playerCount = null, int? playerBudget = null, bool startWinConditions = true, bool forced = false)
    {
        if (!_prototypeManager.TryIndex(threat.RoundStartSpawn, out var spawn))
        {
            RemoveThreatJobAssignments(assignedJobs);
            ReleaseVoteHeldPlayers(voteHeldPlayers, threat.ID, "missing spawn prototype", true);
            return null;
        }

        var population = playerCount ?? _playerManager.PlayerCount;
        var leaders = GetSpawnBodies(ThreatMarkerType.Leader, spawn.LeadersToSpawn, spawn.Scaling, population);
        var members = GetSpawnBodies(ThreatMarkerType.Member, spawn.GruntsToSpawn, spawn.Scaling, population);
        if (playerBudget is { } budget)
            ThreatVoteSelection.LimitBodies(leaders, members, budget);
        var bodies = _forceInterest.GetPlayableBodies(leaders.Concat(members).GroupBy(pair => pair.Key)
            .ToDictionary(group => group.Key, group => group.Sum(pair => pair.Value)), true);
        foreach (var (body, count) in _forceInterest.GetPlayableBodies(spawn.EntitiesToSpawn))
            bodies[body] = bodies.GetValueOrDefault(body) + count;

        var fallbackJobs = leaders.Where(pair => pair.Value > 0).ToDictionary(pair => pair.Key, _ => ThreatLeaderJobId);
        foreach (var (body, count) in members)
        {
            if (count > 0)
                fallbackJobs[body] = ThreatMemberJobId;
        }

        var assignments = assignedJobs.Where(pair => IsThreatJob(pair.Value.Item1)).ToDictionary();
        var volunteers = voteHeldPlayers?.ToList() ?? assignments.Keys.ToList();
        var name = Loc.GetString(ThreatVoteSelection.GetThreatDisplayNameLocId(threat.ID), ("threat", threat.ID));
        var id = _forceInterest.QueueDeployment(name, bodies,
            interested => DeployThreat(interested).GetEnumerator(), ready, volunteers, fallbackJobs);

        IEnumerable<ForceDeploymentStep> DeployThreat(IReadOnlySet<NetUserId> interested)
        {
            // A vote is consent, but only while that player remains interested and available.
            var currentAssignments = assignments.Where(pair => interested.Contains(pair.Key)).ToDictionary();
            var currentVoters = voteHeldPlayers?.Where(interested.Contains).ToList();
            var stagedBodies = new List<EntityUid>();
            try
            {
                foreach (var step in ExecuteSpawnSteps(threat, mapId, currentAssignments, stagedBodies, currentVoters,
                             scalingPlayerCount: population, playerBudget: playerBudget,
                             interestedPlayers: interested))
                {
                    if (step == ForceDeploymentStep.Completed && startWinConditions)
                    {
                        StartThreatWinConditions(threat);
                        if (forced)
                        {
                            _auRound.SetSelectedThreat(threat);
                            _auRound.PreselectThirdPartiesForSelectedThreat();
                        }
                    }

                    yield return step;
                }
            }
            finally
            {
                // Bodies cannot be claimed between spawn steps and their reserved mind assignments.
                // Also release partial deployments if the scheduler cancels or a later step fails.
                foreach (var body in stagedBodies)
                {
                    if (TerminatingOrDeleted(body))
                        continue;

                    _ghostRole.ReleaseForceRole(body);
                    _forceInterest.TrackRole(body);
                }
            }
        }

        // Shelved forces must not indefinitely prevent volunteers from joining another role.
        ReleaseVoteHeldPlayers(voteHeldPlayers, threat.ID, "force queued for interest", false);
        return id;
    }

    private void EnqueuePendingThreatSpawn(PendingThreatForceSpawn pending)
    {
        int index = _pendingSpawns.FindIndex(existing => existing.FireAt > pending.FireAt);
        if (index < 0)
        {
            _pendingSpawns.Add(pending);

            return;
        }

        _pendingSpawns.Insert(index, pending);
    }

    private IEnumerable<ForceDeploymentStep> ExecuteSpawnSteps(ThreatPrototype threat,
        MapId mapId,
        Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assignedJobs,
        List<EntityUid> stagedBodies,
        IReadOnlyList<NetUserId>? voteHeldPlayers = null,
        bool requireObserverForVotePlayers = false,
        int? scalingPlayerCount = null,
        int? playerBudget = null,
        IReadOnlySet<NetUserId>? interestedPlayers = null)
    {
        string threatId = threat.ID;
        if (threat.RoundStartSpawn is not { } partySpawn || string.IsNullOrWhiteSpace(partySpawn.Id))
        {
            _sawmill.Debug($"[DEBUG] Threat '{threat.ID}' has no RoundStartSpawn configured, skipping spawn.");
            int removed = ThreatSystem.RemoveThreatJobAssignments(assignedJobs);
            if (removed > 0)
            {
                _sawmill.Warning($"[ThreatSystem] Removed {removed} threat assignment(s) for threat '{threat.ID
                }' with no roundstart spawn so normal overflow assignment can handle them.");
            }

            ReleaseVoteHeldPlayers(voteHeldPlayers, threat.ID, "no roundstart spawn was configured", true);

            yield break;
        }

        PartySpawnPrototype? newPartySpawn = _prototypeManager.TryIndex(partySpawn, out PartySpawnPrototype? spawn)
            ? spawn
            : null;
        if (newPartySpawn == null)
        {
            _sawmill.Error($"[ERROR] Could not find RoundStartSpawn prototype '{partySpawn}' for threat '{threat.ID
            }'. Skipping threat spawn.");
            int removed = ThreatSystem.RemoveThreatJobAssignments(assignedJobs);
            if (removed > 0)
            {
                _sawmill.Warning($"[ThreatSystem] Removed {removed} threat assignment(s) for threat '{threat.ID
                }' with missing roundstart spawn '{partySpawn}' so normal overflow assignment can handle them.");
            }

            ReleaseVoteHeldPlayers(voteHeldPlayers, threat.ID, $"roundstart spawn '{partySpawn}' was missing", true);

            yield break;
        }

        var markerCache = new Dictionary<ThreatMarkerType, List<EntityUid>>();

        // Helper to get marker entity Uids by marker type
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
            string markerId = newPartySpawn != null && newPartySpawn.Markers.TryGetValue(markerType, out string? id)
                ? id
                : string.Empty;
            var legacyMarkers = new List<EntityUid>();
            EntityQueryEnumerator<ThreatSpawnMarkerComponent> query = _entityManager
                .EntityQueryEnumerator<ThreatSpawnMarkerComponent>();
            while (query.MoveNext(out EntityUid uid, out ThreatSpawnMarkerComponent? comp))
            {
                if (comp.ThreatMarkerType == markerType
                    && !comp.ThirdParty
                    && (comp.ID == markerId || (comp.ID == string.Empty && markerId == string.Empty)))
                {
                    if (_entityManager.GetComponent<TransformComponent>(uid).MapID == mapId)
                        legacyMarkers.Add(uid);
                }
            }

            _sawmill.Debug($"[DEBUG] GetMarkers({markerType}): Found {legacyMarkers.Count} markers with markerId '{
                markerId
            }' on map {mapId}");

            return legacyMarkers;
        }

        // --- Spawn entities and collect them for mind assignment ---
        var spawnedLeaders = new List<EntityUid>();
        var spawnedMembers = new List<EntityUid>();
        _sawmill.Debug($"[DEBUG] Begin spawning threat entities for threat: {threat?.ID ?? "null"}");

        // --- Spawn Together logic ---
        bool spawnTogether = newPartySpawn?.SpawnTogether == true;
        Dictionary<ThreatMarkerType, List<EntityUid>> spawnTogetherMarkers = new();
        if (spawnTogether)
        {
            // Gather all markers of all types
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

        // Spawn leaders
        if (newPartySpawn != null)
        {
            int playerCount = scalingPlayerCount ?? _playerManager.PlayerCount;

            Dictionary<string, int> leaderBodies = ThreatSystem.GetSpawnBodies(
                ThreatMarkerType.Leader,
                newPartySpawn.LeadersToSpawn,
                newPartySpawn.Scaling,
                playerCount);
            Dictionary<string, int> memberBodies = ThreatSystem.GetSpawnBodies(
                ThreatMarkerType.Member,
                newPartySpawn.GruntsToSpawn,
                newPartySpawn.Scaling,
                playerCount);
            IReadOnlyDictionary<string, int> entityBodies = ThreatSystem.GetSpawnBodies(
                ThreatMarkerType.Entity,
                newPartySpawn.EntitiesToSpawn,
                EmptyScaling,
                playerCount);
            if (playerBudget is { } maxPlayerBodies)
                ThreatVoteSelection.LimitBodies(leaderBodies, memberBodies, maxPlayerBodies);

            int leaderReq = leaderBodies.Values.Sum();
            int memberReq = memberBodies.Values.Sum();
            int entityReq = entityBodies.Values.Sum();
            List<EntityUid> leaderMarkers = GetSpawnMarkers(ThreatMarkerType.Leader);
            List<EntityUid> memberMarkers = GetSpawnMarkers(ThreatMarkerType.Member);
            List<EntityUid> entityMarkers = GetSpawnMarkers(ThreatMarkerType.Entity);
            List<EntityUid> originalLeaderMarkers = leaderMarkers.ToList();
            List<EntityUid> originalMemberMarkers = memberMarkers.ToList();
            if (_sawmill.Level <= LogLevel.Debug)
            {
                ThreatAssignmentCounts assignmentCounts = ThreatSystem.CountThreatAssignments(assignedJobs);
                _sawmill.Debug($"[ThreatSystem] Threat spawn plan for '{threatId}': partySpawn={
                    newPartySpawn!.ID}, leaders[{ThreatSystem.FormatSpawnBodies(leaderBodies)
                }] requested={leaderReq} markers={leaderMarkers.Count}, members[{
                    ThreatSystem.FormatSpawnBodies(memberBodies)}] requested={memberReq} markers={
                        memberMarkers.Count}, entities[{ThreatSystem.FormatSpawnBodies(entityBodies)}] requested={
                            entityReq} markers={entityMarkers.Count}, assignedThreatLeaders={
                                assignmentCounts.Leaders}, assignedThreatMembers={assignmentCounts.Members}.");
            }

            if (leaderReq > 0 && leaderMarkers.Count == 0)
            {
                _sawmill.Warning($"[ThreatSystem] Threat '{threatId}' requested {leaderReq
                } leader body/bodies but found no leader markers on map {mapId}.");
            }

            if (memberReq > 0 && memberMarkers.Count == 0)
            {
                _sawmill.Warning($"[ThreatSystem] Threat '{threatId}' requested {memberReq
                } member body/bodies but found no member markers on map {mapId}.");
            }

            if (entityReq > 0 && entityMarkers.Count == 0)
            {
                _sawmill.Warning($"[ThreatSystem] Threat '{threatId}' requested {entityReq
                } entity spawn(s) but found no entity markers on map {mapId}.");
            }

            AnnounceMarkerShortfall(threatId,
                (ThreatMarkerType.Leader, leaderReq, leaderMarkers.Count),
                (ThreatMarkerType.Member, memberReq, memberMarkers.Count),
                (ThreatMarkerType.Entity, entityReq, entityMarkers.Count));

            IEnumerable<ForceDeploymentStep> SpawnBodies(string protoId, int count, List<EntityUid> markers,
                List<EntityUid> spawnedList, string label, ProtoId<JobPrototype>? job = null)
            {
                _sawmill.Debug($"[DEBUG] Spawning {count} {label}(s) of protoId {protoId} at {markers.Count} markers");

                if (count > markers.Count)
                {
                    _sawmill.Warning($"[ThreatSystem] Threat '{threatId}' requested {count} {label} body/bodies for {
                        protoId
                    } but only {markers.Count
                    } marker(s) are available; remaining bodies will not spawn instead of stacking on reused markers.");
                }

                for (var i = 0; i < count && markers.Count > 0; i++)
                {
                    int markerIndex = _random.Next(markers.Count);
                    EntityUid marker = markers[markerIndex];
                    markers.RemoveAt(markerIndex);
                    if (TerminatingOrDeleted(marker) || !TryComp(marker, out TransformComponent? markerTransform))
                        continue;
                    var coords = markerTransform.Coordinates;

                    try
                    {
                        using var cost = _performance.MeasureOperation("threat-body-spawn", protoId);
                        EntityUid ent = _entityManager.SpawnEntity(protoId, coords);
                        stagedBodies.Add(ent);
                        _ghostRole.HoldForceRole(ent);
                        spawnedList.Add(ent);
                        if (job is { } bodyJob)
                            AddThreatFaction(ent, bodyJob);
                        _sawmill.Debug($"[DEBUG] Spawned {label} entity {ent} at marker {marker}");
                    }
                    catch (Exception ex)
                    {
                        _sawmill.Error($"[ThreatSystem] Failed to spawn {label} ({protoId}) for threat '{threatId
                        }' at marker {marker}: {ex}");
                    }

                    yield return ForceDeploymentStep.Spawned;
                }
            }

            string? GetExtraMemberPrototype()
            {
                foreach ((string protoId, int count) in memberBodies)
                {
                    if (count > 0)
                        return protoId;
                }

                return null;
            }

            IEnumerable<ForceDeploymentStep> SpawnExtraVoteMemberBodies(int count)
            {
                if (count <= 0)
                    yield break;

                string? protoId = GetExtraMemberPrototype();
                if (protoId == null)
                {
                    _sawmill.Warning($"[ThreatSystem] Threat '{threatId}' needs {count} extra voted member body/bodies but has no member body prototype configured.");

                    yield break;
                }

                List<EntityUid> markers = originalMemberMarkers.Count > 0
                    ? originalMemberMarkers
                    : originalLeaderMarkers;
                if (markers.Count == 0)
                {
                    _sawmill.Warning($"[ThreatSystem] Threat '{threatId}' needs {count} extra voted member body/bodies but has no member or leader markers to reuse.");

                    yield break;
                }

                for (var i = 0; i < count; i++)
                {
                    EntityUid marker = markers[_random.Next(markers.Count)];
                    if (TerminatingOrDeleted(marker) || !TryComp(marker, out TransformComponent? markerTransform))
                        continue;
                    var coords = markerTransform.Coordinates;

                    try
                    {
                        using var cost = _performance.MeasureOperation("threat-extra-body-spawn", protoId);
                        EntityUid ent = _entityManager.SpawnEntity(protoId, coords);
                        stagedBodies.Add(ent);
                        _ghostRole.HoldForceRole(ent);
                        spawnedMembers.Add(ent);
                        AddThreatFaction(ent, ThreatMemberJobId);
                    }
                    catch (Exception ex)
                    {
                        _sawmill.Error($"[ThreatSystem] Failed to spawn extra voted member ({protoId}) for threat '{threatId}' at marker {marker}: {ex}");
                    }

                    yield return ForceDeploymentStep.Spawned;
                }
            }

            // short on markers? share them out per body type, otherwise the first type listed eats
            // all of them and you get a tribe of spearmen with no bows or shaman
            int leaderMarkerBudget = leaderMarkers.Count;
            if (leaderReq > leaderMarkerBudget)
                ThreatVoteSelection.DistributeBodies(leaderBodies, ref leaderMarkerBudget);
            int memberMarkerBudget = memberMarkers.Count;
            if (memberReq > memberMarkerBudget)
                ThreatVoteSelection.DistributeBodies(memberBodies, ref memberMarkerBudget);

            yield return ForceDeploymentStep.Prepared;

            // Spawn leaders — each entity proto gets its own scaled count
            foreach ((string protoId, int count) in leaderBodies)
            {
                foreach (var step in SpawnBodies(protoId, count, leaderMarkers, spawnedLeaders, "leader", ThreatLeaderJobId))
                    yield return step;
            }

            // Spawn grunts/members — each entity proto gets its own scaled count
            foreach ((string protoId, int count) in memberBodies)
            {
                foreach (var step in SpawnBodies(protoId, count, memberMarkers, spawnedMembers, "member", ThreatMemberJobId))
                    yield return step;
            }

            _sawmill.Debug($"[DEBUG] Spawned {spawnedMembers.Count} threat members.");

            // Spawn other entities
            var spawnedEntities = new List<EntityUid>();
            foreach ((string protoId, int count) in entityBodies)
            {
                foreach (var step in SpawnBodies(protoId, count, entityMarkers, spawnedEntities, "entity"))
                    yield return step;
            }

            _sawmill.Debug($"[DEBUG] Spawned {spawnedEntities.Count} other threat entities.");
            _sawmill.Info($"[ThreatSystem] Threat spawn result for '{threatId}': spawnedLeaders={spawnedLeaders.Count}/{
                leaderReq
            }, spawnedMembers={spawnedMembers.Count}/{memberReq}, spawnedEntities={spawnedEntities.Count}/{entityReq}.");

            // Earlier bodies may have been deleted while the deployment yielded.
            spawnedLeaders.RemoveAll(uid => TerminatingOrDeleted(uid));
            spawnedMembers.RemoveAll(uid => TerminatingOrDeleted(uid));
            var spawnedThreatPlayers = new HashSet<NetUserId>();

            if (voteHeldPlayers != null)
            {
                List<NetUserId> eligibleHeldPlayers = GetEligibleVoteHeldPlayers(voteHeldPlayers,
                    requireObserverForVotePlayers);
                int availableVoteSeats = playerBudget is { } votePlayerBudget
                    ? Math.Max(0, votePlayerBudget - spawnedLeaders.Count - spawnedMembers.Count)
                    : int.MaxValue;
                int missingVoteBodies = threat?.SpawnExtraVoteMembers == true
                    ? Math.Min(eligibleHeldPlayers.Count - spawnedLeaders.Count - spawnedMembers.Count,
                        availableVoteSeats)
                    : 0;
                var previousMembers = spawnedMembers.Count;
                foreach (var step in SpawnExtraVoteMemberBodies(missingVoteBodies))
                    yield return step;
                var extraMembers = spawnedMembers.Count - previousMembers;
                if (extraMembers > 0)
                {
                    _sawmill.Info($"[ThreatSystem] Spawned {extraMembers} extra voted threat member body/bodies for '{threatId}' so held voters can enter the round.");
                }

                spawnedLeaders.RemoveAll(uid => TerminatingOrDeleted(uid));
                spawnedMembers.RemoveAll(uid => TerminatingOrDeleted(uid));
                eligibleHeldPlayers = GetEligibleVoteHeldPlayers(voteHeldPlayers, requireObserverForVotePlayers);
                _random.Shuffle(eligibleHeldPlayers);
                // Voters who explicitly picked this threat are seated before "any threat" voters.
                string? preferencePresetId = _auRound.SelectedPreset?.ID;
                eligibleHeldPlayers = ThreatVoteSelection.OrderByThreatPreference(eligibleHeldPlayers,
                    player => _prefs.GetPreferencesOrNull(player)?.SelectedCharacter is HumanoidCharacterProfile profile
                        && ThreatVoteSelection.ExplicitlyPrefersThreat(profile, preferencePresetId, threatId));
                var heldAssignments = new List<ThreatVoteAssignment>(eligibleHeldPlayers.Count);
                foreach (NetUserId player in eligibleHeldPlayers)
                {
                    ProtoId<JobPrototype> job = assignedJobs.TryGetValue(player,
                            out (ProtoId<JobPrototype>?, EntityUid) assigned)
                        && assigned.Item1 == ThreatLeaderJobId
                            ? ThreatLeaderJobId
                            : ThreatMemberJobId;

                    heldAssignments.Add(new(player, job));
                }

                List<ThreatVoteAssignment> voteAssignments = ThreatVoteSelection.BuildSpawnAssignments(heldAssignments,
                    spawnedLeaders.Count,
                    spawnedMembers.Count);
                var leaderAssignments = new List<ThreatVoteAssignment>(spawnedLeaders.Count);
                var memberAssignments = new List<ThreatVoteAssignment>(spawnedMembers.Count);
                foreach (ThreatVoteAssignment assignment in voteAssignments)
                {
                    if (assignment.Job == ThreatLeaderJobId)
                        leaderAssignments.Add(assignment);
                    else if (assignment.Job == ThreatMemberJobId)
                        memberAssignments.Add(assignment);
                }

                int assignedLeaders = AssignThreatMinds(leaderAssignments,
                    spawnedLeaders,
                    spawnedThreatPlayers);
                int assignedMembers = AssignThreatMinds(memberAssignments,
                    spawnedMembers,
                    spawnedThreatPlayers);
                _sawmill.Info($"[ThreatSystem] Voted threat assignment result for '{threatId}': eligibleHeldPlayers={
                    eligibleHeldPlayers.Count}, assignedLeaders={assignedLeaders}/{spawnedLeaders.Count
                    }, assignedMembers={assignedMembers}/{spawnedMembers.Count}.");

                bool reserveLeftoverVoters = threat?.ReserveLeftoverVoters == true;
                var leftoverVoters = new List<NetUserId>();
                foreach (NetUserId playerId in eligibleHeldPlayers)
                {
                    if (spawnedThreatPlayers.Contains(playerId))
                        continue;

                    if (reserveLeftoverVoters)
                    {
                        leftoverVoters.Add(playerId);
                        continue;
                    }

                    if (!_playerManager.TryGetSessionById(playerId, out ICommonSession? session))
                        continue;

                    _sawmill.Info($"[ThreatSystem] Player {session.Name} ({playerId
                    }) returning to lobby as there was no threat mob available for them.");
                    _ticker.Respawn(session);
                }

                AddGhostRolesForUnassigned(spawnedLeaders, assignedLeaders, ThreatLeaderJobId);
                AddGhostRolesForUnassigned(spawnedMembers, assignedMembers, ThreatMemberJobId);

                _sawmill.Debug($"[DEBUG] Voted threat assigned {assignedLeaders} leader(s), {assignedMembers
                } member(s), exposed {
                    spawnedLeaders.Count - assignedLeaders + spawnedMembers.Count - assignedMembers
                } ghost role body/bodies.");

                int removedVoteAssignments
                    = ThreatSystem.RemoveThreatJobAssignments(assignedJobs, spawnedThreatPlayers);
                if (removedVoteAssignments > 0)
                {
                    _sawmill.Warning($"[ThreatSystem] Removed {removedVoteAssignments
                    } unspawned voted threat assignment(s).");
                }

                ReleaseVoteHeldPlayers(voteHeldPlayers, threatId, "voted threat spawn completed", false);

                if (leftoverVoters.Count > 0)
                {
                    _sawmill.Info($"[ThreatSystem] Reserving {leftoverVoters.Count} leftover voter(s) for '{threatId}' instead of returning them to lobby.");
                    var leftoverEvent = new ThreatLeftoverVotersEvent(threatId, leftoverVoters);
                    RaiseLocalEvent(ref leftoverEvent);
                }

                yield return ForceDeploymentStep.Completed;
                yield break;
            }

            var roundstartLeaderAssignments = new List<ThreatVoteAssignment>();
            var roundstartMemberAssignments = new List<ThreatVoteAssignment>();
            foreach ((NetUserId player, (ProtoId<JobPrototype>? job, EntityUid _)) in assignedJobs)
            {
                if (job == ThreatLeaderJobId)
                    roundstartLeaderAssignments.Add(new(player, ThreatLeaderJobId));
                else if (job == ThreatMemberJobId)
                    roundstartMemberAssignments.Add(new(player, ThreatMemberJobId));
            }

            int assignedRoundstartLeaders = AssignThreatMinds(roundstartLeaderAssignments, spawnedLeaders,
                spawnedThreatPlayers);
            _sawmill.Debug($"[DEBUG] Assigned {assignedRoundstartLeaders} leader minds");
            int assignedRoundstartMembers = AssignThreatMinds(roundstartMemberAssignments, spawnedMembers,
                spawnedThreatPlayers);
            _sawmill.Debug($"[DEBUG] Assigned {assignedRoundstartMembers} member minds");
            _sawmill.Info($"[ThreatSystem] Roundstart threat assignment result for '{threatId}': leaderPlayers={
                roundstartLeaderAssignments.Count}, assignedLeaders={assignedRoundstartLeaders}/{
                    spawnedLeaders.Count}, memberPlayers={roundstartMemberAssignments.Count}, assignedMembers={
                        assignedRoundstartMembers}/{spawnedMembers.Count}.");
            if (roundstartLeaderAssignments.Count > spawnedLeaders.Count)
            {
                _sawmill.Warning($"[ThreatSystem] Threat '{threatId}' had {roundstartLeaderAssignments.Count
                } assigned leader player(s) but only {spawnedLeaders.Count} leader body/bodies spawned.");
            }

            if (roundstartMemberAssignments.Count > spawnedMembers.Count)
            {
                _sawmill.Warning($"[ThreatSystem] Threat '{threatId}' had {roundstartMemberAssignments.Count
                } assigned member player(s) but only {spawnedMembers.Count} member body/bodies spawned.");
            }

            // Forced and admin spawns reserve no jobs, so volunteers who registered interest are seated here.
            if (interestedPlayers != null)
            {
                List<NetUserId> volunteers = GetEligibleVoteHeldPlayers(
                    interestedPlayers.Where(player => !spawnedThreatPlayers.Contains(player)).ToList(),
                    false);
                _random.Shuffle(volunteers);
                assignedRoundstartLeaders += AssignThreatMinds(
                    volunteers.Select(player => new ThreatVoteAssignment(player, ThreatLeaderJobId)).ToList(),
                    spawnedLeaders.Skip(assignedRoundstartLeaders).ToList(),
                    spawnedThreatPlayers);
                assignedRoundstartMembers += AssignThreatMinds(
                    volunteers.Where(player => !spawnedThreatPlayers.Contains(player))
                        .Select(player => new ThreatVoteAssignment(player, ThreatMemberJobId))
                        .ToList(),
                    spawnedMembers.Skip(assignedRoundstartMembers).ToList(),
                    spawnedThreatPlayers);
            }

            // Must run before the deployment's finally block releases the bodies, or they stay unplayable.
            AddGhostRolesForUnassigned(spawnedLeaders, assignedRoundstartLeaders, ThreatLeaderJobId);
            AddGhostRolesForUnassigned(spawnedMembers, assignedRoundstartMembers, ThreatMemberJobId);

            int removed = ThreatSystem.RemoveThreatJobAssignments(assignedJobs, spawnedThreatPlayers);
            if (removed > 0)
            {
                _sawmill.Warning($"[ThreatSystem] Removed {removed
                } unspawned threat assignment(s) so normal overflow assignment can handle them.");
            }
        }

        yield return ForceDeploymentStep.Completed;
    }

    private static Dictionary<string, int> GetSpawnBodies(
        ThreatMarkerType markerType,
        IReadOnlyDictionary<string, int> legacyBodies,
        IReadOnlyDictionary<string, JobScaleEntry> legacyScaling,
        int playerCount)
    {
        var bodies = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach ((string bodyId, int staticCount) in legacyBodies)
        {
            bodies[bodyId] = legacyScaling.TryGetValue(bodyId, out JobScaleEntry entry)
                ? JobScaling.CalculateScaledSlots(playerCount, staticCount, entry)
                : Math.Max(0, staticCount);
        }

        return bodies;
    }

    private void AnnounceMarkerShortfall(string source,
        params (ThreatMarkerType Bucket, int Required, int Markers)[] pools)
    {
        var shortfalls = new List<string>();
        foreach ((ThreatMarkerType bucket, int required, int markers) in pools)
        {
            if (required > markers)
                shortfalls.Add($"{bucket}s {markers}/{required}");
        }

        if (shortfalls.Count == 0)
            return;

        _chat.DispatchServerAnnouncement(
              Loc.GetString("cmu-threat-marker-shortfall-announcement",
                  ("kind", "THREAT"),
                  ("source", source),
                  ("planet", _auRound.GetSelectedPlanetId() ?? "<unknown>"),
                ("short", string.Join(", ", shortfalls))),
            Color.Red);
    }

    private void StartThreatWinConditions(ThreatPrototype threat)
        => _auRound.StartThreatWinConditions(threat);

    private List<NetUserId> GetEligibleVoteHeldPlayers(IReadOnlyList<NetUserId> heldPlayers,
        bool requireObserver)
    {
        var eligible = new List<NetUserId>(heldPlayers.Count);
        foreach (NetUserId playerId in heldPlayers)
        {
            if (!_playerManager.TryGetSessionById(playerId, out ICommonSession? session)
                || session.Status is SessionStatus.Disconnected or SessionStatus.Zombie
                || session.AttachedEntity is { } attached && !HasComp<GhostComponent>(attached))
                continue;

            if (requireObserver && !_entityManager.TryGetComponent(session.AttachedEntity, out GhostComponent? _))
                continue;

            eligible.Add(playerId);
        }

        return eligible;
    }

    private int AssignThreatMinds(IReadOnlyList<ThreatVoteAssignment> assignments,
        IReadOnlyList<EntityUid> entities,
        HashSet<NetUserId> spawnedThreatPlayers)
    {
        var assigned = 0;
        for (var i = 0; i < assignments.Count; i++)
        {
            if (assigned >= entities.Count)
                break;

            ThreatVoteAssignment assignment = assignments[i];

            if (!TryAssignThreatMind(assignment.Player, entities[assigned], assignment.Job))
                continue;

            spawnedThreatPlayers.Add(assignment.Player);
            assigned++;
        }

        return assigned;
    }

    private bool TryAssignThreatMind(NetUserId playerNetId,
        EntityUid entity,
        ProtoId<JobPrototype> jobId)
    {
        if (!_playerManager.TryGetSessionById(playerNetId, out ICommonSession? session))
        {
            _sawmill.Error($"[THREAT SPAWN] Could not find session for player {playerNetId}");

            return false;
        }

        // The player can join another role while earlier bodies are being spawned.
        if (TerminatingOrDeleted(entity)
            || MetaData(entity).EntityPrototype is not { } prototype
            || !_ghostRole.CanJoinForceRole(session, prototype.ID, jobId))
            return false;

        _ticker.PlayerJoinGame(session, true);

        GhostRoleComponent? ghostRole = null;
        if (TryComp(entity, out ghostRole) && ghostRole.MakeSentient)
            _mindSystem.MakeSentient(entity, ghostRole.AllowMovement, ghostRole.AllowSpeech);

        ContentPlayerData? data = session.ContentData();
        EntityUid? mind = _mindSystem.GetMind(playerNetId);
        if (mind == null)
        {
            mind = _mindSystem.CreateMind(playerNetId, data?.Name ?? "Threat Player");
            _mindSystem.SetUserId(mind.Value, playerNetId);
            _sawmill.Debug($"[DEBUG] Created mind for threat player {playerNetId}");
        }

        _mindSystem.TransferTo(mind.Value, entity);
        _sawmill.Debug($"[DEBUG] Assigned threat mind {mind.Value} to entity {entity} for player {playerNetId} as {
            jobId.Id}");

        ProtoId<JobPrototype> entityJob = ghostRole?.JobProto ?? jobId;
        _roles.MindAddJobRole(mind.Value, silent: true, jobPrototype: entityJob);
        AddStartingMindRole(entity, mind.Value);
        _roles.MindAddRole(mind.Value, ThreatMindRoleId, silent: true);

        AddThreatFaction(entity, jobId);

        if (ghostRole != null) _ghostRole.UnregisterGhostRole((entity, ghostRole));

        return true;
    }

    private void AddStartingMindRole(EntityUid entity, EntityUid mind)
    {
        if (TryComp(entity, out StartingMindRoleComponent? starting))
            _roles.MindAddRole(mind, starting.MindRole, silent: starting.Silent);
    }

    private void AddGhostRolesForUnassigned(IReadOnlyList<EntityUid> entities,
        int assignedCount,
        ProtoId<JobPrototype> jobId)
    {
        for (int i = Math.Max(0, assignedCount); i < entities.Count; i++)
        {
            MakeThreatGhostRole(entities[i], jobId);
        }
    }

    private void MakeThreatGhostRole(EntityUid entity, ProtoId<JobPrototype> jobId)
    {
        AddThreatFaction(entity, jobId);

        var ghostRole = EnsureComp<GhostRoleComponent>(entity);
        ghostRole.RoleName = jobId == ThreatLeaderJobId
            ? "au14-threat-leader-ghost-role-name"
            : "au14-threat-ghost-role-name";
        ghostRole.RoleDescription = "au14-threat-ghost-role-description";
        ghostRole.RoleRules = "au14-threat-ghost-role-rules";
        ghostRole.JobProto = jobId;
        ghostRole.MindRoles = new List<EntProtoId> { ThreatMindRoleId };

        EnsureComp<GhostTakeoverAvailableComponent>(entity);
        _forceInterest.TrackRole(entity);
    }

    /// <summary>
    ///     Makes <paramref name="body"/> count as the given threat job: objective job, THREAT faction and the
    ///     objective re-scan. If the body has a mind, its job role is swapped to match.
    /// </summary>
    internal void ApplyThreatRole(EntityUid body, ProtoId<JobPrototype> job)
    {
        AddThreatFaction(body, job);

        if (!_mindSystem.TryGetMind(body, out EntityUid mindId, out MindComponent? mind))
            return;

        if (_roles.MindHasRole<JobRoleComponent>((mindId, mind), out var current)
            && current.Value.Comp1.JobPrototype == job)
            return;

        _roles.MindAddJobRole(mindId, mind, silent: true, jobPrototype: job);
        if (mind.UserId is { } userId && _playerManager.TryGetSessionById(userId, out ICommonSession? session))
            _playTime.PlayerRolesChanged(session);
    }

    private void AddThreatFaction(EntityUid entity, ProtoId<JobPrototype> jobId)
    {
        EnsureComp<ThreatComponent>(entity).ObjectiveJob = jobId;
        EnsureComp<NpcFactionMemberComponent>(entity);
        _npcFaction.AddFaction((entity, CompOrNull<NpcFactionMemberComponent>(entity)), threatNPCFaction);
        RaiseLocalEvent(new ObjectiveWatchedEntityStartupEvent(entity));
    }

    internal bool IsExcludedFromVictory(EntityUid uid, MobStateComponent mobState)
    {
        // Exclude all threats, which use their own Victory Condition logic
        if (HasComp<XenoComponent>(uid)
            || HasComp<YautjaComponent>(uid)
            || HasComp<ApeComponent>(uid)
            || HasComp<TribalComponent>(uid)
            || HasComp<BiomorphComponent>(uid)
            || HasComp<BiomorphMimicComponent>(uid)
            || HasComp<CMUWendigoPackComponent>(uid))
            return true;

        if (HasComp<SynthComponent>(uid))
            return true;

        // Don't exclude the dead (ghosts), we tally them as eliminated instead
        if (mobState.CurrentState == MobState.Dead)
            return false;

        // Alive and nested or SSD, or adrift in space outside the ship
        return HasComp<XenoNestedComponent>(uid)
            || (TryComp(uid, out SSDIndicatorComponent? ssd) && ssd.IsSSD)
            || _adrift.IsAdrift(uid);
    }

    internal sealed record PendingThreatSpawnDebugView(
        string ThreatId,
        TimeSpan FireAt
    );

    private readonly record struct ThreatAssignmentCounts(int Leaders, int Members);

    private sealed class PendingThreatForceSpawn
    {
        public required TimeSpan FireAt;
        public required ThreatPrototype Threat;
        public required uint InterestId;
    }
}
