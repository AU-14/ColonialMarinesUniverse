using System.Linq;
using Content.Server.AU14.Round;
using Content.Server.AU14.Scenario;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.Voting;
using Content.Server.Voting.Managers;
using Content.Shared._CMU14.Threats;
using Content.Shared._RMC14.Rules;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using ThirdPartySystem = Content.Server._CMU14.Ops.ThirdParty.ThirdPartySystem;

namespace Content.Server._CMU14.Threats;

public sealed partial class ThreatVoteSystem : EntitySystem
{
    [Dependency] private AuRoundSystem _auRound = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private AuJobSelectionSystem _jobSelection = default!;
    [Dependency] private PlatoonSpawnRuleSystem _platoonSpawnRule = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ScenarioPlanSystem _scenarioPlan = default!;
    [Dependency] private ThirdPartySystem _thirdParty = default!;
    [Dependency] private ThreatSystem _threat = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IVoteManager _voteManager = default!;
    private static readonly TimeSpan VoteDuration = TimeSpan.FromSeconds(30);
    private const string VoteTitleLocId = "au14-threat-vote-title";
    private readonly HashSet<NetUserId> _roundJoinBlockedPlayers = new();

    private PreparedThreatVote? _prepared;
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

    public bool IsRoundJoinBlocked(NetUserId playerId) => _roundJoinBlockedPlayers.Contains(playerId);

    public void ClearRoundJoinBlocks() { _roundJoinBlockedPlayers.Clear(); }

    internal void UnblockRoundJoinsForPlayers(IEnumerable<NetUserId> players)
    {
        foreach (NetUserId player in players)
        {
            _roundJoinBlockedPlayers.Remove(player);
        }
    }

    internal void BlockRoundJoinsForHeldPlayers(IEnumerable<NetUserId> heldPlayers)
    {
        _roundJoinBlockedPlayers.Clear();
        _roundJoinBlockedPlayers.UnionWith(heldPlayers);
    }

    public bool TryPrepareThreatVote(Dictionary<NetUserId, HumanoidCharacterProfile> profiles,
        MapId mapId)
    {
        _prepared = null;
        ClearRoundJoinBlocks();

        if (!_auRound.UsesPostRoundstartThreatVote())
            return false;

        string? presetId = _auRound.SelectedPreset?.ID;
        RMCPlanetMapPrototypeComponent? planet = _auRound.GetSelectedPlanet();
        if (presetId == null || planet == null)
        {
            _jobSelection.ForcedJobAssignments.Clear();
            Sawmill.Warning($"[ThreatVoteSystem] Cannot prepare threat vote: preset={presetId ?? "null"}, planet={
                planet?.MapId ?? "null"}.");

            return false;
        }

        int playerCount = Math.Max(_player.PlayerCount, profiles.Count);
        Sawmill.Debug($"[ThreatVoteSystem] Preparing threat vote: preset={presetId}, planet={planet.MapId}, profiles={
            profiles.Count}, playerCount={playerCount}, selectedThreat={_auRound.SelectedThreat?.ID ?? "null"}.");
        var candidates = new List<ThreatVoteCandidate>();
        ThreatVoteBodyCount heldBodyCount;
        if (!TryBuildCandidatesFromScenarioPlan(planet, presetId, playerCount, out candidates, out heldBodyCount,
            out string diagnostic))
        {
            if (HasCoveredScenarioThreatCandidate(planet, presetId))
            {
                _jobSelection.ForcedJobAssignments.Clear();
                Sawmill.Error(
                    $"[ThreatVoteSystem] Could not resolve deferred threat vote from Scenario Plan for covered Round Groups; vote will not start instead of falling back to legacy body-count calculation. {
                        diagnostic}");

                return false;
            }

            Sawmill.Warning(
                $"[ThreatVoteSystem] Could not resolve deferred threat vote from Scenario Plan; falling back to legacy body-count calculation. {
                    diagnostic}");

            candidates = BuildLegacyCandidates(planet, presetId, playerCount);
            heldBodyCount = ThreatVoteSystem.GetMaxRequiredBodyCount(candidates);
        }

        if (candidates.Count == 0)
        {
            _jobSelection.ForcedJobAssignments.Clear();
            Sawmill.Warning($"[ThreatVoteSystem] No valid threat vote candidates for preset {presetId} on planet {
                planet.MapId}.");

            return false;
        }

        if (Sawmill.Level <= LogLevel.Debug)
        {
            Sawmill.Debug($"[ThreatVoteSystem] Threat vote candidates: {string.Join(", ",
                candidates.Select(candidate => $"{candidate.Threat.ID}(leaders={candidate.BodyCount.Leaders
                }, members={candidate.BodyCount.Members})"))}; heldBodyCount leaders={heldBodyCount.Leaders
            }, members={heldBodyCount.Members}.");
        }

        List<ProtoId<ThreatPrototype>> candidateIds = candidates
            .Select(candidate => new ProtoId<ThreatPrototype>(candidate.Threat.ID))
            .ToList();
        List<NetUserId> heldPlayers = _jobSelection.AssignThreatVotePoolJobs(profiles,
            candidateIds,
            heldBodyCount,
            presetId);
        if (heldPlayers.Count == 0)
        {
            _jobSelection.ForcedJobAssignments.Clear();
            ClearRoundJoinBlocks();
            Sawmill.Warning($"[ThreatVoteSystem] Threat vote for preset {presetId} on planet {planet.MapId
            } had no held voters; vote will not start.");

            return false;
        }

        BlockRoundJoinsForHeldPlayers(heldPlayers);

        _prepared = new()
        {
            PresetId = presetId,
            MapId = mapId,
            Candidates = candidates,
            HeldPlayers = heldPlayers
        };

        Sawmill.Debug($"[ThreatVoteSystem] Prepared {candidates.Count} candidate(s), held {heldPlayers.Count
        } player(s), held body count {heldBodyCount.Total}.");

        return true;
    }

    private static ThreatVoteBodyCount GetMaxRequiredBodyCount(IReadOnlyList<ThreatVoteCandidate> candidates)
    {
        var leaders = 0;
        var members = 0;
        foreach (ThreatVoteCandidate candidate in candidates)
        {
            leaders = Math.Max(leaders, candidate.BodyCount.Leaders);
            members = Math.Max(members, candidate.BodyCount.Members);
        }

        return new(leaders, members);
    }

    public bool StartPreparedThreatVote(Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assignedJobs)
    {
        if (_prepared == null)
        {
            Sawmill.Warning("[ThreatVoteSystem] StartPreparedThreatVote called with no prepared vote.");
            ClearRoundJoinBlocks();

            return false;
        }

        PreparedThreatVote? prepared = _prepared;
        _prepared = null;
        BlockRoundJoinsForHeldPlayers(prepared.HeldPlayers);

        if (prepared.Candidates.Count == 1)
        {
            if (_ticker.RunLevel != GameRunLevel.InRound)
            {
                ClearRoundJoinBlocks();

                return false;
            }

            ThreatPrototype selected = prepared.Candidates[0].Threat;
            Sawmill.Info($"[ThreatVoteSystem] Only one threat candidate '{selected.ID}' prepared for preset {
                prepared.PresetId
            }; auto-selecting without starting a vote.");
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
            CarryoverKey = ThreatVoteSystem.BuildCarryoverKey(prepared)
        };
        voteOptions.SetInitiatorOrServer(null);

        IVoteHandle handle = _voteManager.CreateVote(voteOptions);
        handle.OnCancelled += _ => ClearRoundJoinBlocks();
        handle.OnFinished += (_, args) =>
        {
            Sawmill.Debug($"[ThreatVoteSystem] Threat vote finished: winner={args.Winner}, tiedWinners={
                args.Winners.Length
            }, heldPlayers={prepared.HeldPlayers.Count}.");
            if (_ticker.RunLevel != GameRunLevel.InRound)
            {
                ClearRoundJoinBlocks();

                return;
            }

            ThreatPrototype? selected = ResolveThreatWinner(args.Winner, args.Winners, prepared.Candidates);
            if (selected == null)
            {
                Sawmill.Warning("[ThreatVoteSystem] Threat vote finished without a resolvable selected threat.");
                ClearRoundJoinBlocks();

                return;
            }

            args.ResolveWinner(selected);
            FinishThreatVote(prepared, selected, assignedJobs);
        };

        Sawmill.Debug($"[ThreatVoteSystem] Started threat vote with {prepared.Candidates.Count} candidate(s) and {
            prepared.HeldPlayers.Count} voter(s).");

        return true;
    }

    private bool TryBuildCandidatesFromScenarioPlan(RMCPlanetMapPrototypeComponent planet,
        string presetId,
        int playerCount,
        out List<ThreatVoteCandidate> candidates,
        out ThreatVoteBodyCount heldBodyCount,
        out string diagnostic)
    {
        candidates = new();
        heldBodyCount = default(ThreatVoteBodyCount);

        var request = new ScenarioPlanValidationRequest(presetId,
            playerCount,
            GetSelectedGovforPlatoonId(),
            GetSelectedOpforPlatoonId(),
            _auRound.GetSelectedPlanetId(),
            planet.MapId,
            null,
            _auRound.GetSelectedGovforShip(),
            _auRound.GetSelectedOpforShip());

        if (!_scenarioPlan.TryResolveDeferredThreatVote(request, out ResolvedDeferredThreatChoice? deferredChoice,
                out diagnostic)
            || deferredChoice == null)
            return false;

        foreach (ResolvedThreatForcePlan resolved in deferredChoice.Candidates)
        {
            if (!_prototype.TryIndex(resolved.ThreatId, out ThreatPrototype? threat))
            {
                diagnostic = $"Resolved deferred threat candidate '{resolved.ThreatId}' could not be indexed.";
                candidates.Clear();

                return false;
            }

            candidates.Add(new(threat,
                new(resolved.LeaderBodies, resolved.MemberBodies)));
        }

        heldBodyCount = new(deferredChoice.ReservationPolicy.ReservedLeaderBodies,
            deferredChoice.ReservationPolicy.ReservedMemberBodies);
        if (candidates.Count == 0 || heldBodyCount.Total <= 0)
        {
            diagnostic = $"Resolved deferred threat choice '{deferredChoice.ChoiceId
            }' did not produce reservable bodies.";
            candidates.Clear();
            heldBodyCount = default(ThreatVoteBodyCount);

            return false;
        }

        diagnostic = string.Empty;

        return true;
    }

    private bool HasCoveredScenarioThreatCandidate(RMCPlanetMapPrototypeComponent planet, string presetId)
    {
        foreach (ProtoId<ThreatPrototype> threatId in planet.AllowedThreats)
        {
            if (_scenarioPlan.HasMappedHostileRoundGroup(presetId, threatId.Id))
                return true;
        }

        return false;
    }

    private List<ThreatVoteCandidate> BuildLegacyCandidates(RMCPlanetMapPrototypeComponent planet,
        string presetId,
        int playerCount)
    {
        string? govforId = _platoonSpawnRule.SelectedGovforPlatoon?.ID;
        string? opforId = _platoonSpawnRule.SelectedOpforPlatoon?.ID;
        var candidates = new List<ThreatVoteCandidate>();

        foreach (ProtoId<ThreatPrototype> threatId in planet.AllowedThreats)
        {
            if (!_prototype.TryIndex(threatId, out ThreatPrototype? threatProto)
                || !ThreatVoteSelection.IsThreatAllowed(threatProto, presetId, govforId, opforId, playerCount)
                || !_prototype.TryIndex(threatProto.RoundStartSpawn, out PartySpawnPrototype? spawn))
                continue;

            ThreatVoteBodyCount bodyCount = ThreatVoteSelection.CalculateBodyCount(spawn, playerCount);

            if (bodyCount.Total <= 0)
                continue;

            candidates.Add(new(threatProto, bodyCount));
        }

        return candidates;
    }

    private ThreatPrototype? ResolveThreatWinner(object? winner,
        IReadOnlyCollection<object> tiedWinners,
        IReadOnlyCollection<ThreatVoteCandidate> candidates)
    {
        if (winner is ThreatPrototype threat)
            return threat;

        List<ThreatPrototype> tiedThreats = tiedWinners
            .OfType<ThreatPrototype>()
            .ToList();

        if (tiedThreats.Count > 0)
            return _random.Pick(tiedThreats);

        return candidates.Count > 0
            ? _random.Pick(candidates).Threat
            : null;
    }

    private void FinishThreatVote(PreparedThreatVote prepared,
        ThreatPrototype selected,
        Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assignedJobs)
    {
        Sawmill.Info($"[ThreatVoteSystem] Finishing threat vote: selected={selected.ID}, preset={prepared.PresetId
        }, map={
            prepared.MapId}, heldPlayers={prepared.HeldPlayers.Count}, assignedJobs={assignedJobs.Count}.");
        _auRound.SetSelectedThreat(selected);
        _auRound.PreselectThirdPartiesForSelectedThreat();
        try
        {
            _scenarioPlan.GenerateShadowPlan(new(prepared.PresetId,
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
            Sawmill.Error($"[ThreatVoteSystem] GenerateShadowPlan threw after threat vote: {scenarioEx}");
        }

        MoveHeldPlayersToObservers(prepared.HeldPlayers, selected);

        try
        {
            Sawmill.Debug($"[ThreatVoteSystem] Spawning voted threat '{selected.ID}'.");
            _threat.SpawnThreatFromVote(selected, prepared.MapId, assignedJobs, prepared.HeldPlayers);
        }
        catch (Exception threatEx)
        {
            Sawmill.Error($"[ThreatVoteSystem] SpawnThreatFromVote threw: {threatEx}");
            ThreatSystem.RemoveThreatJobAssignments(assignedJobs);
            ReleaseHeldPlayersToLobby(prepared.HeldPlayers, selected.ID, "threat spawn threw");

            return;
        }

        try
        {
            Sawmill.Debug($"[ThreatVoteSystem] Starting third-party spawning after threat vote; selectedThirdParties={
                _auRound.SelectedThirdParties.Count}.");
            _thirdParty.StartThirdPartySpawning(selected, assignedJobs);
        }
        catch (Exception thirdPartyEx)
        {
            Sawmill.Error($"[ThreatVoteSystem] StartThirdPartySpawning threw: {thirdPartyEx}");
        }
    }

    private void MoveHeldPlayersToObservers(IReadOnlyCollection<NetUserId> heldPlayers, ThreatPrototype selected)
    {
        bool isColonyFall = string.Equals(_auRound.SelectedPreset?.ID, "ColonyFall",
            StringComparison.OrdinalIgnoreCase);
        int minMinutes = Math.Max(1, (int)Math.Round(selected.SpawnDelayMin / 60.0));
        int maxMinutes = Math.Max(minMinutes, (int)Math.Round(selected.SpawnDelayMax / 60.0));

        foreach (NetUserId playerId in heldPlayers)
        {
            if (!_player.TryGetSessionById(playerId, out ICommonSession? session)
                || session.Status == SessionStatus.Disconnected)
                continue;

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

    private void ReleaseHeldPlayersToLobby(IReadOnlyCollection<NetUserId> heldPlayers,
        string threatId,
        string reason)
    {
        UnblockRoundJoinsForPlayers(heldPlayers);

        foreach (NetUserId playerId in heldPlayers)
        {
            if (!_player.TryGetSessionById(playerId, out ICommonSession? session)
                || session.Status == SessionStatus.Disconnected)
                continue;

            Sawmill.Info($"[ThreatVoteSystem] Releasing held threat vote player {session.Name} ({playerId}) for '{
                threatId
            }' because {reason}; returning them to lobby.");
            _ticker.Respawn(session);
        }
    }

    private static string BuildCarryoverKey(PreparedThreatVote prepared)
    {
        IOrderedEnumerable<string> candidateIds = prepared.Candidates
            .Select(candidate => candidate.Threat.ID)
            .Order(StringComparer.OrdinalIgnoreCase);

        return $"au14-threat:{prepared.PresetId}:{string.Join(",", candidateIds)}";
    }

    private string? GetSelectedGovforPlatoonId() => _platoonSpawnRule.SelectedGovforPlatoon?.ID;

    private string? GetSelectedOpforPlatoonId() => _platoonSpawnRule.SelectedOpforPlatoon?.ID;

    private string GetLocalizedThreatDisplayName(string threatId)
    {
        string locId = ThreatVoteSelection.GetThreatDisplayNameLocId(threatId);
        if (locId == ThreatVoteSelection.GenericThreatDisplayNameLocId)
        {
            return Loc.GetString(locId,
                ("threat", ThreatVoteSelection.GetThreatDisplayName(threatId)));
        }

        return Loc.GetString(locId);
    }

    private sealed record ThreatVoteCandidate(
        ThreatPrototype Threat,
        ThreatVoteBodyCount BodyCount
    );

    private sealed class PreparedThreatVote
    {
        public required List<ThreatVoteCandidate> Candidates;
        public required List<NetUserId> HeldPlayers;
        public required MapId MapId;
        public required string PresetId;
    }
}
