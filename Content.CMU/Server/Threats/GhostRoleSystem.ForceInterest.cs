using Content.Server.Ghost.Roles.Components;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Ghost.Roles;

public sealed partial class GhostRoleSystem
{
    // the force only takes people who pass this later, so the threat vote has to hold the same people
    public bool CanPlayForceJob(ICommonSession player, ProtoId<JobPrototype> job)
    {
        List<ProtoId<JobPrototype>> jobs = [job];
        if (_ban.GetRoleBans(player.UserId) == null || _ban.IsRoleBanned(player, jobs))
            return false;

        return IsRoleAllowed(player, jobs, null, null);
    }

    /// <summary>Checks the same session, ban and playtime rules before a force body exists.</summary>
    public bool CanJoinForceRole(ICommonSession player, EntProtoId body, ProtoId<JobPrototype>? fallbackJob)
    {
        if (!CanRequestGhostRole(player) || !ProtoMan.TryIndex(body, out var prototype))
            return false;

        var jobs = new List<ProtoId<JobPrototype>>();
        var antags = new List<ProtoId<AntagPrototype>>();
        prototype.TryComp<GhostRoleComponent>(out var ghostRole, Factory);
        if (fallbackJob is { } fallback)
            jobs.Add(fallback);
        else if (ghostRole?.JobProto is { } job)
            jobs.Add(job);

        if (fallbackJob == null && ghostRole != null)
        {
            foreach (var id in ghostRole.MindRoles)
            {
                if (!ProtoMan.TryIndex(id, out var mindRole) ||
                    !mindRole.TryComp<MindRoleComponent>(out var role, Factory))
                    continue;

                if (role.JobPrototype is { } mindJob)
                    jobs.Add(mindJob);
                if (role.AntagPrototype is { } antag)
                    antags.Add(antag);
            }
        }

        if ((jobs.Count > 0 || antags.Count > 0) && _ban.GetRoleBans(player.UserId) == null)
            return false;

        return !_ban.IsRoleBanned(player, jobs) && !_ban.IsRoleBanned(player, antags) &&
            IsRoleAllowed(player, jobs, antags, ghostRole?.Requirements);
    }
}
