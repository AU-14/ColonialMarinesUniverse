using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server._CMU14.Ops.ThirdParty;
using Content.Server._CMU14.Threats;
using Content.Server.AU14.Round;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.GameTicking;

public sealed partial class GameTicker
{
    [Dependency] private AuJobSelectionSystem _cmuJobSelection = default!;
    [Dependency] private ThirdPartySystem _cmuThirdParty = default!;
    [Dependency] private ThreatSystem _cmuThreat = default!;
    [Dependency] private ThreatVoteSystem _cmuThreatVote = default!;

    private CmuThreatRoundStartState PrepareCmuThreatRoundStart(
        Dictionary<NetUserId, HumanoidCharacterProfile> profiles)
    {
        _cmuThreatVote.ClearRoundJoinBlocks();

        var usesPostRoundstartVote = _auRoundSystem.UsesPostRoundstartThreatVote();
        if (!usesPostRoundstartVote)
        {
            _cmuJobSelection.AssignThreatAndThirdPartyJobs(profiles);
            return new(false, false);
        }

        try
        {
            var prepared = _cmuThreatVote.TryPrepareThreatVote(profiles, DefaultMap);
            return new(true, prepared);
        }
        catch (Exception ex)
        {
            Log.Error($"TryPrepareThreatVote threw - round will continue without a threat vote. {ex}");
            _cmuThreatVote.ClearRoundJoinBlocks();
            _cmuJobSelection.ForcedJobAssignments.Clear();
            return new(true, false);
        }
    }

    private void SpawnImmediateCmuThreat(
        CmuThreatRoundStartState state,
        Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assignedJobs)
    {
        if (state.UsesPostRoundstartVote || _auRoundSystem.SelectedThreat is not { } selectedThreat)
            return;

        try
        {
            _cmuThreat.SpawnThreatAtRoundStart(selectedThreat, DefaultMap, assignedJobs);
        }
        catch (Exception ex)
        {
            Log.Error($"SpawnThreatAtRoundStart threw - round will continue without threat spawn. {ex}");
            var removed = ThreatSystem.RemoveThreatJobAssignments(assignedJobs);
            if (removed > 0)
            {
                Log.Warning($"Removed {removed} threat assignment(s) after threat spawning failed so overflow assignment can handle those players.");
            }
        }
    }

    private void CompleteCmuThreatRoundStart(
        CmuThreatRoundStartState state,
        Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assignedJobs)
    {
        if (state.VotePrepared)
        {
            try
            {
                _cmuThreatVote.StartPreparedThreatVote(assignedJobs);
            }
            catch (Exception ex)
            {
                Log.Error($"StartPreparedThreatVote threw - round will continue without a threat vote. {ex}");
                _cmuThreatVote.ClearRoundJoinBlocks();
                var removed = ThreatSystem.RemoveThreatJobAssignments(assignedJobs);
                if (removed > 0)
                    Log.Warning($"Removed {removed} held threat assignment(s) after threat vote start failed.");
            }

            return;
        }

        if (_auRoundSystem.SelectedThreat is not { } selectedThreat)
            return;

        try
        {
            _cmuThirdParty.StartThirdPartySpawning(selectedThreat, assignedJobs);
        }
        catch (Exception ex)
        {
            Log.Error($"StartThirdPartySpawning threw - round will continue without third-party spawn. {ex}");
        }
    }

    private ICommonSession[] GetCmuRuleJobsAssignedPlayers(
        CmuThreatRoundStartState state,
        Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)> assignedJobs)
    {
        return assignedJobs
            .Where(assignment => !state.VotePrepared || !ThreatSystem.IsThreatJob(assignment.Value.Item1))
            .Select(assignment => _playerManager.GetSessionById(assignment.Key))
            .ToArray();
    }

    private bool IsCmuThreatVoteRoundJoinBlocked(ICommonSession player)
    {
        if (!_cmuThreatVote.IsRoundJoinBlocked(player.UserId))
            return false;

        _chatManager.DispatchServerMessage(player, Loc.GetString("au14-threat-vote-round-join-blocked"));
        return true;
    }

    private static bool IsCmuThreatJob(ProtoId<JobPrototype>? job)
        => ThreatSystem.IsThreatJob(job);

    private readonly record struct CmuThreatRoundStartState(
        bool UsesPostRoundstartVote,
        bool VotePrepared);
}
