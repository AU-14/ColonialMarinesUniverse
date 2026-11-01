using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Roles;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server.Station.Systems;

// CMU14: round start squad distribution for GOVFOR/OPFOR.
public sealed partial class StationSpawningSystem
{
    /// <summary>
    /// Picks the squad a new GOVFOR/OPFOR member joins:
    /// <list type="bullet">
    /// <item>Squad leaders go to a squad not already led by a squad leader, so each squad gets one.</item>
    /// <item>Riflemen go to the squad with the fewest people, filling out the smaller squads.</item>
    /// <item>Every other role goes to the squad with the fewest of that role, so each squad gets one
    /// before any squad gets a second.</item>
    /// </list>
    /// Ties go to the squad with the fewest people, then to the first squad in order.
    /// </summary>
    private string CMUPickSquad(
        string[] candidates,
        ProtoId<JobPrototype>? job,
        JobPrototype? originalPrototype,
        string? originalJobId,
        bool isSquadLeader)
    {
        var isRifleman = IsRoundRole(originalPrototype, "SquadRifleman") ||
                         originalJobId?.EndsWith("SquadRifleman", StringComparison.OrdinalIgnoreCase) == true;

        string? best = null;
        var bestPrimary = int.MaxValue;
        var bestMembers = int.MaxValue;
        foreach (var candidate in candidates)
        {
            if (!_squadSystem.TryEnsureSquad(candidate, out var squad))
                continue;

            int primary;
            if (isSquadLeader)
                primary = CMUIsLedBySquadLeaderRole(squad) ? 1 : 0;
            else if (isRifleman || job == null)
                primary = 0;
            else
                primary = squad.Comp.Roles.GetValueOrDefault(job.Value);

            var members = squad.Comp.Members.Count;
            if (primary < bestPrimary || (primary == bestPrimary && members < bestMembers))
            {
                best = candidate;
                bestPrimary = primary;
                bestMembers = members;
            }
        }

        return best ?? candidates[0];
    }

    /// <summary>
    /// Whether the squad's current leader holds a squad leader job, as opposed to having no leader or
    /// someone promoted to lead it in the field.
    /// </summary>
    private bool CMUIsLedBySquadLeaderRole(Entity<SquadTeamComponent> squad)
    {
        if (!_squadSystem.TryGetSquadLeader(squad, out var leader) ||
            CompOrNull<OriginalRoleComponent>(leader)?.Job is not { } leaderJob)
        {
            return false;
        }

        _prototypeManager.TryIndex(leaderJob, out var leaderPrototype);
        return IsSquadLeaderRole(leaderPrototype, leaderJob.Id);
    }
}
