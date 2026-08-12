using Content.Server.AU14.Roles;
using Content.Shared._CMU14.Round.Roles;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server.Station.Systems;

public sealed partial class StationSpawningSystem
{
    [Dependency] private SquadSystem _cmuSquads = default!;

    private static readonly EntProtoId[] GovforSquads =
    [
        "SquadGovfor",
        "SquadGovforBravo",
        "SquadGovforCharlie",
    ];

    private static readonly EntProtoId[] OpforSquads =
    [
        "SquadOpfor",
        "SquadOpforBravo",
        "SquadOpforCharlie",
    ];

    private static readonly HashSet<string> NoSquadRoundRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Advisor",
        "DropshipCrewChief",
        "DropshipPilot",
        "MilitaryDoctor",
        "MilitaryPolice",
        "PlatoonCommander",
        "ExecutiveOfficer",
        "CMO",
        "ChiefMP",
        "LogisticsOfficer",
        "EngineeringOfficer",
    };

    private static readonly HashSet<string> AuxiliarySquadRoundRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "AuxSupportSynth",
        "AuxTech",
        "CombatCorrespondent",
        "DroneOperator",
        "EngineeringTech",
        "IntelOfficer",
        "JuniorOfficer",
        "Nurse",
        "WorkingJoe",
        "VehicleCommander",
        "VehicleCrewman",
    };

    private static readonly HashSet<string> NoSquadJobIdFragments = new(StringComparer.OrdinalIgnoreCase)
    {
        "dcc",
        "pilot",
        "platco",
        "policeman",
        "militarydoctor",
    };

    private static readonly HashSet<string> AuxiliarySquadJobIdFragments = new(StringComparer.OrdinalIgnoreCase)
    {
        "synth",
        "platop",
    };

    private int _govforNextSquadIndex;
    private int _opforNextSquadIndex;

    private void AssignRoundStartSquad(
        EntityUid entity,
        ProtoId<JobPrototype>? jobId,
        JobPrototype? job)
    {
        var side = _roundJobProfiles.GetRoundSide(job, jobId?.Id);
        if (side is not (RoundJobSide.Govfor or RoundJobSide.Opfor) ||
            !ShouldAssignToSquad(job, jobId?.Id))
        {
            return;
        }

        var govfor = side == RoundJobSide.Govfor;
        EntProtoId squadId;

        if (IsAuxiliarySquadRole(job, jobId?.Id))
        {
            squadId = govfor ? "SquadGovforIntel" : "SquadOpforIntel";
        }
        else
        {
            var candidates = govfor ? GovforSquads : OpforSquads;
            squadId = PickCombatSquad(candidates, govfor, jobId, job);
        }

        if (!_cmuSquads.TryEnsureSquad(squadId, out var squad))
        {
            Log.Error($"Could not create round-start squad '{squadId}' for job '{jobId}'.");
            return;
        }

        _cmuSquads.AssignSquad(entity, (squad.Owner, (SquadTeamComponent?) squad.Comp), jobId);

        if (!IsSquadLeaderRole(job, jobId?.Id))
            return;

        var member = EnsureComp<SquadMemberComponent>(entity);
        _cmuSquads.PromoteSquadLeader((entity, member), entity, squad.Comp.LeaderIcon);
    }

    private EntProtoId PickCombatSquad(
        IReadOnlyList<EntProtoId> candidates,
        bool govfor,
        ProtoId<JobPrototype>? jobId,
        JobPrototype? job)
    {
        if (IsSquadLeaderRole(job, jobId?.Id))
        {
            foreach (var candidate in candidates)
            {
                if (_cmuSquads.TryEnsureSquad(candidate, out var squad) &&
                    !_cmuSquads.TryGetSquadLeader(squad, out _))
                {
                    return candidate;
                }
            }
        }
        if (govfor)
        {
            var result = candidates[_govforNextSquadIndex % candidates.Count];
            _govforNextSquadIndex = (_govforNextSquadIndex + 1) % candidates.Count;
            return result;
        }

        var opforResult = candidates[_opforNextSquadIndex % candidates.Count];
        _opforNextSquadIndex = (_opforNextSquadIndex + 1) % candidates.Count;
        return opforResult;
    }

    private static bool ShouldAssignToSquad(JobPrototype? job, string? fallbackJobId)
    {
        if (job?.RoundRole is { } roundRole)
            return !NoSquadRoundRoles.Contains(roundRole);

        if (string.IsNullOrEmpty(fallbackJobId))
            return false;

        foreach (var fragment in NoSquadJobIdFragments)
        {
            if (fallbackJobId.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private static bool IsAuxiliarySquadRole(JobPrototype? job, string? fallbackJobId)
    {
        if (job?.RoundRole is { } roundRole)
            return AuxiliarySquadRoundRoles.Contains(roundRole);

        if (string.IsNullOrEmpty(fallbackJobId))
            return false;

        foreach (var fragment in AuxiliarySquadJobIdFragments)
        {
            if (fallbackJobId.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsSquadLeaderRole(JobPrototype? job, string? fallbackJobId)
    {
        return IsRoundRole(job, "SectionSergeant") ||
               IsRoundRole(job, "SquadSergeant") ||
               fallbackJobId?.Contains("sergeant", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsRoundRole(JobPrototype? job, string expectedRoundRole)
    {
        return job?.RoundRole?.Equals(expectedRoundRole, StringComparison.OrdinalIgnoreCase) == true;
    }
}
