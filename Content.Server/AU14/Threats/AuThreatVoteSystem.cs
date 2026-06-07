using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.AU14.Round;
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

    public bool IsRoundJoinBlocked(NetUserId playerId)
    {
        return _roundJoinBlockedPlayers.Contains(playerId);
    }

    public void ClearRoundJoinBlocks()
    {
        _roundJoinBlockedPlayers.Clear();
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
            return false;
        }

        var candidates = BuildCandidates(planet, presetId, profiles.Count);
        if (candidates.Count == 0)
        {
            _jobSelection.ForcedJobAssignments.Clear();
            Logger.GetSawmill("au14.threat").Warning(
                $"[AuThreatVoteSystem] No valid threat vote candidates for preset {presetId} on planet {planet.MapId}.");
            return false;
        }

        var heldBodyCount = candidates
            .OrderBy(candidate => candidate.BodyCount.Total)
            .Select(candidate => candidate.BodyCount)
            .First();
        var candidateIds = candidates
            .Select(candidate => new ProtoId<ThreatPrototype>(candidate.Threat.ID))
            .ToList();
        var heldPlayers = _jobSelection.AssignThreatVotePoolJobs(
            profiles,
            candidateIds,
            heldBodyCount,
            presetId);
        BlockRoundJoinsForHeldPlayers(heldPlayers);

        _prepared = new PreparedThreatVote
        {
            PresetId = presetId,
            MapId = mapId,
            Candidates = candidates,
            HeldPlayers = heldPlayers,
        };

        Logger.GetSawmill("au14.threat").Debug(
            $"[AuThreatVoteSystem] Prepared {candidates.Count} candidate(s), held {heldPlayers.Count} player(s), held body count {heldBodyCount.Total}.");
        return true;
    }

    public bool StartPreparedThreatVote(Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assignedJobs)
    {
        if (_prepared == null)
        {
            ClearRoundJoinBlocks();
            return false;
        }

        var prepared = _prepared;
        _prepared = null;
        BlockRoundJoinsForHeldPlayers(prepared.HeldPlayers);

        var voteOptions = new VoteOptions
        {
            Title = Loc.GetString(VoteTitleLocId),
            Options = prepared.Candidates
                .Select(candidate => (GetLocalizedThreatDisplayName(candidate.Threat.ID), (object) candidate.Threat))
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
            var selected = ResolveThreatWinner(args.Winner, args.Winners, prepared.Candidates);
            if (selected == null)
            {
                ClearRoundJoinBlocks();
                return;
            }

            args.ResolveWinner(selected);
            FinishThreatVote(prepared, selected, assignedJobs);
        };

        Logger.GetSawmill("au14.threat").Debug(
            $"[AuThreatVoteSystem] Started threat vote with {prepared.Candidates.Count} candidate(s) and {prepared.HeldPlayers.Count} voter(s).");
        return true;
    }

    private List<ThreatVoteCandidate> BuildCandidates(
        RMCPlanetMapPrototypeComponent planet,
        string presetId,
        int readyPlayerCount)
    {
        var platoonSpawnRuleSystem = EntityManager.EntitySysManager.GetEntitySystem<PlatoonSpawnRuleSystem>();
        var govforId = platoonSpawnRuleSystem.SelectedGovforPlatoon?.ID;
        var opforId = platoonSpawnRuleSystem.SelectedOpforPlatoon?.ID;
        var playerCount = Math.Max(_player.PlayerCount, readyPlayerCount);
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
        _auRound.SetSelectedThreat(selected);
        _auRound.PreselectThirdPartiesForSelectedThreat();
        MoveHeldPlayersToObservers(prepared.HeldPlayers, selected);

        try
        {
            _threat.SpawnThreatFromVote(selected, prepared.MapId, assignedJobs, prepared.HeldPlayers);
        }
        catch (Exception threatEx)
        {
            Logger.GetSawmill("au14.threat").Error($"[AuThreatVoteSystem] SpawnThreatFromVote threw: {threatEx}");
            AuThreatSystem.RemoveThreatJobAssignments(assignedJobs);
        }

        try
        {
            _thirdParty.StartThirdPartySpawning(selected, assignedJobs);
        }
        catch (Exception thirdPartyEx)
        {
            Logger.GetSawmill("au14.threat").Error($"[AuThreatVoteSystem] StartThirdPartySpawning threw: {thirdPartyEx}");
        }
    }

    private void MoveHeldPlayersToObservers(IReadOnlyCollection<NetUserId> heldPlayers, ThreatPrototype selected)
    {
        var ticker = EntityManager.EntitySysManager.GetEntitySystem<GameTicker>();
        var isColonyFall = string.Equals(_auRound.SelectedPreset?.ID, "ColonyFall", StringComparison.OrdinalIgnoreCase);
        var minMinutes = Math.Max(1, (int) Math.Round(selected.SpawnDelayMin / 60.0));
        var maxMinutes = Math.Max(minMinutes, (int) Math.Round(selected.SpawnDelayMax / 60.0));

        foreach (var playerId in heldPlayers)
        {
            if (!_player.TryGetSessionById(playerId, out var session) ||
                session.Status == SessionStatus.Disconnected)
            {
                continue;
            }

            ticker.JoinAsObserver(session);
            if (isColonyFall)
            {
                _chat.DispatchServerMessage(session,
                    Loc.GetString("au14-threat-vote-colony-fall-observer-warning",
                        ("min", minMinutes),
                        ("max", maxMinutes)));
            }
        }
    }

    private static string BuildCarryoverKey(PreparedThreatVote prepared)
    {
        var candidateIds = prepared.Candidates
            .Select(candidate => candidate.Threat.ID)
            .Order(StringComparer.OrdinalIgnoreCase);

        return $"au14-threat:{prepared.PresetId}:{string.Join(",", candidateIds)}";
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
