using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.AU14.Round;
using Content.Server.AU14.Scenario;
using Content.Server.AU14.ThirdParty;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.Voting;
using Content.Server.Voting.Managers;
using Content.Shared._RMC14.Rules;
using Content.Shared.AU14.Threats;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.AU14.Threats;

public sealed partial class AuThreatVoteSystem : EntitySystem
{
    private const string VoteTitleLocId = "au14-threat-vote-title";
    private static readonly TimeSpan VoteDuration = TimeSpan.FromSeconds(30);

    [Dependency] private AuRoundSystem _auRound = default!;
    [Dependency] private AuJobSelectionSystem _jobSelection = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private PlatoonSpawnRuleSystem _platoonSpawnRule = default!;
    [Dependency] private ScenarioPlanSystem _scenarioPlan = default!;
    [Dependency] private AuThreatSystem _threat = default!;
    [Dependency] private AuThirdPartySystem _thirdParty = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IVoteManager _voteManager = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IRobustRandom _random = default!;

    private sealed record ThreatVoteCandidate(
        ThreatPrototype Threat,
        ThreatVoteBodyCount BodyCount);

    private sealed class PreparedThreatVote
    {
        public required string PresetId;
        public required MapId MapId;
        public required List<ThreatVoteCandidate> Candidates;
        public required List<NetUserId> HeldPlayers;
    }

    private PreparedThreatVote? _prepared;
    private readonly HashSet<NetUserId> _roundJoinBlockedPlayers = new();
    private ISawmill? _sawmill;
    private ISawmill Sawmill => _sawmill ??= Logger.GetSawmill("au14.threat");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        if (ev.New != GameRunLevel.InRound)
        {
            _prepared = null;
            ClearRoundJoinBlocks();
        }
    }

    public bool IsRoundJoinBlocked(NetUserId playerId)
    {
        return _roundJoinBlockedPlayers.Contains(playerId);
    }

    public void ClearRoundJoinBlocks()
    {
        _roundJoinBlockedPlayers.Clear();
    }

    internal void UnblockRoundJoinsForPlayers(IEnumerable<NetUserId> players)
    {
        foreach (var player in players)
        {
            _roundJoinBlockedPlayers.Remove(player);
        }
    }

    internal void BlockRoundJoinsForHeldPlayers(IEnumerable<NetUserId> heldPlayers)
    {
        _roundJoinBlockedPlayers.Clear();
        _roundJoinBlockedPlayers.UnionWith(heldPlayers);
    }

    public bool TryPrepareThreatVote(
        Dictionary<NetUserId, HumanoidCharacterProfile> profiles,
        MapId mapId)
    {
        _prepared = null;
        ClearRoundJoinBlocks();

        if (!_auRound.UsesPostRoundstartThreatVote())
            return false;

        var presetId = _auRound.SelectedPreset?.ID;
        var planet = _auRound.GetSelectedPlanet();
        if (presetId == null || planet == null)
        {
            _jobSelection.ForcedJobAssignments.Clear();
            Sawmill.Warning(
                $"[AuThreatVoteSystem] Cannot prepare threat vote: preset={presetId ?? "null"}, planet={planet?.MapId ?? "null"}.");
            return false;
        }

        var playerCount = Math.Max(_player.PlayerCount, profiles.Count);
        Sawmill.Debug(
            $"[AuThreatVoteSystem] Preparing threat vote: preset={presetId}, planet={planet.MapId}, profiles={profiles.Count}, playerCount={playerCount}, selectedThreat={_auRound.SelectedThreat?.ID ?? "null"}.");
        var candidates = new List<ThreatVoteCandidate>();
        ThreatVoteBodyCount heldBodyCount;
        if (!TryBuildCandidatesFromScenarioPlan(planet, presetId, playerCount, out candidates, out heldBodyCount, out var diagnostic))
        {
            if (HasCoveredScenarioThreatCandidate(planet, presetId))
            {
                _jobSelection.ForcedJobAssignments.Clear();
                Sawmill.Error(
                    $"[AuThreatVoteSystem] Could not resolve deferred threat vote from Scenario Plan for covered Round Groups; vote will not start instead of falling back to legacy body-count calculation. {diagnostic}");
                return false;
            }

            Sawmill.Warning(
                $"[AuThreatVoteSystem] Could not resolve deferred threat vote from Scenario Plan; falling back to legacy body-count calculation. {diagnostic}");

            candidates = BuildLegacyCandidates(planet, presetId, playerCount);
            heldBodyCount = GetMaxRequiredBodyCount(candidates);
        }

        if (candidates.Count == 0)
        {
            _jobSelection.ForcedJobAssignments.Clear();
            Sawmill.Warning(
                $"[AuThreatVoteSystem] No valid threat vote candidates for preset {presetId} on planet {planet.MapId}.");
            return false;
        }

        if (Sawmill.Level <= Robust.Shared.Log.LogLevel.Debug)
        {
            Sawmill.Debug(
                $"[AuThreatVoteSystem] Threat vote candidates: {string.Join(", ", candidates.Select(candidate => $"{candidate.Threat.ID}(leaders={candidate.BodyCount.Leaders}, members={candidate.BodyCount.Members})"))}; heldBodyCount leaders={heldBodyCount.Leaders}, members={heldBodyCount.Members}.");
        }

        var candidateIds = candidates
            .Select(candidate => new ProtoId<ThreatPrototype>(candidate.Threat.ID))
            .ToList();
        var heldPlayers = _jobSelection.AssignThreatVotePoolJobs(
            profiles,
            candidateIds,
            heldBodyCount,
            presetId);
        if (heldPlayers.Count == 0)
        {
            _jobSelection.ForcedJobAssignments.Clear();
            ClearRoundJoinBlocks();
            Sawmill.Warning(
                $"[AuThreatVoteSystem] Threat vote for preset {presetId} on planet {planet.MapId} had no held voters; vote will not start.");
            return false;
        }

        BlockRoundJoinsForHeldPlayers(heldPlayers);

        _prepared = new PreparedThreatVote
        {
            PresetId = presetId,
            MapId = mapId,
            Candidates = candidates,
            HeldPlayers = heldPlayers,
        };

        Sawmill.Debug(
            $"[AuThreatVoteSystem] Prepared {candidates.Count} candidate(s), held {heldPlayers.Count} player(s), held body count {heldBodyCount.Total}.");
        return true;
    }

    private static ThreatVoteBodyCount GetMaxRequiredBodyCount(IReadOnlyList<ThreatVoteCandidate> candidates)
    {
        var leaders = 0;
        var members = 0;
        foreach (var candidate in candidates)
        {
            leaders = Math.Max(leaders, candidate.BodyCount.Leaders);
            members = Math.Max(members, candidate.BodyCount.Members);
        }

        return new ThreatVoteBodyCount(leaders, members);
    }

    public bool StartPreparedThreatVote(Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assignedJobs)
    {
        if (_prepared == null)
        {
            Sawmill.Warning("[AuThreatVoteSystem] StartPreparedThreatVote called with no prepared vote.");
            ClearRoundJoinBlocks();
            return false;
        }

        var prepared = _prepared;
        _prepared = null;
        BlockRoundJoinsForHeldPlayers(prepared.HeldPlayers);

        if (prepared.Candidates.Count == 1)
        {
            var selected = prepared.Candidates[0].Threat;
            Sawmill.Info(
                $"[AuThreatVoteSystem] Only one threat candidate '{selected.ID}' prepared for preset {prepared.PresetId}; auto-selecting without starting a vote.");
            // StartPreparedThreatVote is called while SpawnPlayers is still running, before the ticker
            // flips to InRound. A real vote finishes later; a single-candidate shortcut still needs to
            // complete the same spawn path instead of dropping the prepared vote.
            FinishThreatVote(prepared, selected, assignedJobs);
            return true;
        }

        var voteOptions = new VoteOptions
        {
            Title = Loc.GetString(VoteTitleLocId),
            Options = prepared.Candidates
                .Select(candidate => (GetLocalizedThreatDisplayName(candidate.Threat.ID), (object)candidate.Threat))
                .ToList(),
            Duration = VoteDuration,
            AllowedVoters = prepared.HeldPlayers.ToHashSet(),
            RandomizeMissingVotes = true,
            CarryoverEnabled = true,
            CarryoverKey = BuildCarryoverKey(prepared),
        };
        voteOptions.SetInitiatorOrServer(null);

        var handle = _voteManager.CreateVote(voteOptions);
        handle.OnCancelled += _ => ClearRoundJoinBlocks();
        handle.OnFinished += (_, args) =>
        {
            Sawmill.Debug(
                $"[AuThreatVoteSystem] Threat vote finished: winner={args.Winner}, tiedWinners={args.Winners.Length}, heldPlayers={prepared.HeldPlayers.Count}.");
            if (_ticker.RunLevel != GameRunLevel.InRound)
            {
                ClearRoundJoinBlocks();
                return;
            }

            var selected = ResolveThreatWinner(args.Winner, args.Winners, prepared.Candidates);
            if (selected == null)
            {
                Sawmill.Warning("[AuThreatVoteSystem] Threat vote finished without a resolvable selected threat.");
                ClearRoundJoinBlocks();
                return;
            }

            args.ResolveWinner(selected);
            FinishThreatVote(prepared, selected, assignedJobs);
        };

        Sawmill.Debug(
            $"[AuThreatVoteSystem] Started threat vote with {prepared.Candidates.Count} candidate(s) and {prepared.HeldPlayers.Count} voter(s).");
        return true;
    }

    private bool TryBuildCandidatesFromScenarioPlan(
        RMCPlanetMapPrototypeComponent planet,
        string presetId,
        int playerCount,
        out List<ThreatVoteCandidate> candidates,
        out ThreatVoteBodyCount heldBodyCount,
        out string diagnostic)
    {
        candidates = new List<ThreatVoteCandidate>();
        heldBodyCount = default;

        var request = new ScenarioPlanValidationRequest(
            presetId,
            playerCount,
            GetSelectedGovforPlatoonId(),
            GetSelectedOpforPlatoonId(),
            _auRound.GetSelectedPlanetId(),
            planet.MapId,
            null,
            _auRound.GetSelectedGovforShip(),
            _auRound.GetSelectedOpforShip());

        if (!_scenarioPlan.TryResolveDeferredThreatVote(request, out var deferredChoice, out diagnostic) ||
            deferredChoice == null)
        {
            return false;
        }

        foreach (var resolved in deferredChoice.Candidates)
        {
            if (!_prototype.TryIndex<ThreatPrototype>(resolved.ThreatId, out var threat))
            {
                diagnostic = $"Resolved deferred threat candidate '{resolved.ThreatId}' could not be indexed.";
                candidates.Clear();
                return false;
            }

            candidates.Add(new ThreatVoteCandidate(
                threat,
                new ThreatVoteBodyCount(resolved.LeaderBodies, resolved.MemberBodies)));
        }

        heldBodyCount = new ThreatVoteBodyCount(
            deferredChoice.ReservationPolicy.ReservedLeaderBodies,
            deferredChoice.ReservationPolicy.ReservedMemberBodies);
        if (candidates.Count == 0 || heldBodyCount.Total <= 0)
        {
            diagnostic = $"Resolved deferred threat choice '{deferredChoice.ChoiceId}' did not produce reservable bodies.";
            candidates.Clear();
            heldBodyCount = default;
            return false;
        }

        diagnostic = string.Empty;
        return true;
    }

    private bool HasCoveredScenarioThreatCandidate(RMCPlanetMapPrototypeComponent planet, string presetId)
    {
        foreach (var threatId in planet.AllowedThreats)
        {
            if (_scenarioPlan.HasMappedHostileRoundGroup(presetId, threatId.Id))
                return true;
        }

        return false;
    }

    private List<ThreatVoteCandidate> BuildLegacyCandidates(
        RMCPlanetMapPrototypeComponent planet,
        string presetId,
        int playerCount)
    {
        var govforId = _platoonSpawnRule.SelectedGovforPlatoon?.ID;
        var opforId = _platoonSpawnRule.SelectedOpforPlatoon?.ID;
        var candidates = new List<ThreatVoteCandidate>();

        foreach (var threatId in planet.AllowedThreats)
        {
            if (!_prototype.TryIndex(threatId, out ThreatPrototype? threatProto) ||
                !ThreatVoteSelection.IsThreatAllowed(threatProto, presetId, govforId, opforId, playerCount) ||
                !_prototype.TryIndex(threatProto.RoundStartSpawn, out PartySpawnPrototype? spawn))
            {
                continue;
            }

            var bodyCount = ThreatVoteSelection.CalculateBodyCount(spawn, playerCount);
            if (bodyCount.Total <= 0)
                continue;

            candidates.Add(new ThreatVoteCandidate(threatProto, bodyCount));
        }

        return candidates;
    }

    private ThreatPrototype? ResolveThreatWinner(
        object? winner,
        IReadOnlyCollection<object> tiedWinners,
        IReadOnlyCollection<ThreatVoteCandidate> candidates)
    {
        if (winner is ThreatPrototype threat)
            return threat;

        var tiedThreats = tiedWinners
            .OfType<ThreatPrototype>()
            .ToList();

        if (tiedThreats.Count > 0)
            return _random.Pick(tiedThreats);

        return candidates.Count > 0
            ? _random.Pick(candidates).Threat
            : null;
    }

    private void FinishThreatVote(
        PreparedThreatVote prepared,
        ThreatPrototype selected,
        Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assignedJobs)
    {
        Sawmill.Info(
            $"[AuThreatVoteSystem] Finishing threat vote: selected={selected.ID}, preset={prepared.PresetId}, map={prepared.MapId}, heldPlayers={prepared.HeldPlayers.Count}, assignedJobs={assignedJobs.Count}.");
        _auRound.SetSelectedThreat(selected);
        _auRound.PreselectThirdPartiesForSelectedThreat();
        try
        {
            _scenarioPlan.GenerateShadowPlan(
                new ScenarioPlanValidationRequest(
                    prepared.PresetId,
                    Math.Max(_player.PlayerCount, prepared.HeldPlayers.Count),
                    GetSelectedGovforPlatoonId(),
                    GetSelectedOpforPlatoonId(),
                    _auRound.GetSelectedPlanetId(),
                    _auRound.GetSelectedPlanet()?.MapId,
                    selected.ID,
                    _auRound.GetSelectedGovforShip(),
                    _auRound.GetSelectedOpforShip()),
                "PostRoundstartThreatVoteFinished");
        }
        catch (Exception scenarioEx)
        {
            Sawmill.Error(
                $"[AuThreatVoteSystem] GenerateShadowPlan threw after threat vote: {scenarioEx}");
        }

        MoveHeldPlayersToObservers(prepared.HeldPlayers, selected);

        try
        {
            Sawmill.Debug($"[AuThreatVoteSystem] Spawning voted threat '{selected.ID}'.");
            _threat.SpawnThreatFromVote(selected, prepared.MapId, assignedJobs, prepared.HeldPlayers);
        }
        catch (Exception threatEx)
        {
            Sawmill.Error($"[AuThreatVoteSystem] SpawnThreatFromVote threw: {threatEx}");
            AuThreatSystem.RemoveThreatJobAssignments(assignedJobs);
            ReleaseHeldPlayersToLobby(prepared.HeldPlayers, selected.ID, "threat spawn threw");
            return;
        }

        try
        {
            Sawmill.Debug(
                $"[AuThreatVoteSystem] Starting third-party spawning after threat vote; selectedThirdParties={_auRound.SelectedThirdParties.Count}.");
            _thirdParty.StartThirdPartySpawning(selected, assignedJobs);
        }
        catch (Exception thirdPartyEx)
        {
            Sawmill.Error($"[AuThreatVoteSystem] StartThirdPartySpawning threw: {thirdPartyEx}");
        }
    }

    private void MoveHeldPlayersToObservers(IReadOnlyCollection<NetUserId> heldPlayers, ThreatPrototype selected)
    {
        var isColonyFall = string.Equals(_auRound.SelectedPreset?.ID, "ColonyFall", StringComparison.OrdinalIgnoreCase);
        var minMinutes = Math.Max(1, (int)Math.Round(selected.SpawnDelayMin / 60.0));
        var maxMinutes = Math.Max(minMinutes, (int)Math.Round(selected.SpawnDelayMax / 60.0));

        foreach (var playerId in heldPlayers)
        {
            if (!_player.TryGetSessionById(playerId, out var session) ||
                session.Status == SessionStatus.Disconnected)
            {
                continue;
            }

            _ticker.JoinAsObserver(session);
            if (isColonyFall)
            {
                _chat.DispatchServerMessage(session,
                    Loc.GetString("au14-threat-vote-colony-fall-observer-warning",
                        ("min", minMinutes),
                        ("max", maxMinutes)));
            }
        }
    }

    private void ReleaseHeldPlayersToLobby(
        IReadOnlyCollection<NetUserId> heldPlayers,
        string threatId,
        string reason)
    {
        UnblockRoundJoinsForPlayers(heldPlayers);

        foreach (var playerId in heldPlayers)
        {
            if (!_player.TryGetSessionById(playerId, out var session) ||
                session.Status == SessionStatus.Disconnected)
            {
                continue;
            }

            Sawmill.Info(
                $"[AuThreatVoteSystem] Releasing held threat vote player {session.Name} ({playerId}) for '{threatId}' because {reason}; returning them to lobby.");
            _ticker.Respawn(session);
        }
    }

    private static string BuildCarryoverKey(PreparedThreatVote prepared)
    {
        var candidateIds = prepared.Candidates
            .Select(candidate => candidate.Threat.ID)
            .Order(StringComparer.OrdinalIgnoreCase);

        return $"au14-threat:{prepared.PresetId}:{string.Join(",", candidateIds)}";
    }

    private string? GetSelectedGovforPlatoonId()
    {
        return _platoonSpawnRule.SelectedGovforPlatoon?.ID;
    }

    private string? GetSelectedOpforPlatoonId()
    {
        return _platoonSpawnRule.SelectedOpforPlatoon?.ID;
    }

    private string GetLocalizedThreatDisplayName(string threatId)
    {
        var locId = ThreatVoteSelection.GetThreatDisplayNameLocId(threatId);
        if (locId == ThreatVoteSelection.GenericThreatDisplayNameLocId)
        {
            return Loc.GetString(locId,
                ("threat", ThreatVoteSelection.GetThreatDisplayName(threatId)));
        }

        return Loc.GetString(locId);
    }
}
