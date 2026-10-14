using Content.Shared._RMC14.Marines.Squads;
using Content.Shared.CMU14.Squads;
using Content.Shared.Bed.Sleep;
using Content.Shared.Database;
using Content.Shared.Mobs.Systems;
using Content.Shared.SSDIndicator;
using Content.Shared.Roles;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Tracker.SquadLeader;

// CMU14: automatic fireteam assignment. Lives in a partial because fireteam membership and
// leadership components are only writable by SquadLeaderTrackerSystem.
public sealed partial class SquadLeaderTrackerSystem
{
    [Dependency] private MobStateSystem _cmuMobState = default!;

    /// <summary>
    /// A new fireteam is opened for every this many people in the squad, up to the fireteam limit.
    /// </summary>
    private const int CMUPeoplePerFireteam = 10;

    private static readonly HashSet<ProtoId<JobPrototype>> CMUFireteamLeaderJobs = new()
    {
        "CMFireteamLeader",
        "AU14JobGOVFORRadioTelephoneOperator",
        "AU14JobOPFORRadioTelephoneOperator",
        "AU14JobGOVFORRadioTelephoneOperatorRMC",
        "AU14JobGOVFORRadioTelephoneOperatorUPP",
        "AU14JobGOVFORRadioTelephoneOperatorWYPMC",
    };

    /// <summary>
    /// Puts a squad member who is not in a fireteam into the emptiest open fireteam, opening a new
    /// fireteam for every <see cref="CMUPeoplePerFireteam"/> people in the squad. Fireteam leader jobs
    /// lead their fireteam; a fireteam without a leader is led by its highest ranking member.
    /// </summary>
    public void CMUAutoAssignFireteam(EntityUid member)
    {
        if (_net.IsClient)
            return;

        if (!_squadMemberQuery.TryComp(member, out var squadMember) ||
            squadMember.Squad is not { } squadId ||
            !TryComp(squadId, out SquadTeamComponent? squad) ||
            !squad.Members.Contains(member) ||
            HasComp<SquadLeaderComponent>(member) ||
            _fireteamMemberQuery.HasComp(member))
        {
            return;
        }

        // Auxiliary squads have no fireteams. Someone who led a fireteam in their old squad loses the
        // fireteam leader icon here too.
        if (HasComp<CMUAuxiliarySquadComponent>(squadId))
        {
            RemComp<FireteamLeaderComponent>(member);
            return;
        }

        var maxFireteams = squad.Fireteams.Fireteams.Length;
        var openFireteams = Math.Clamp(
            (squad.Members.Count + CMUPeoplePerFireteam - 1) / CMUPeoplePerFireteam,
            1,
            maxFireteams);

        var sizes = new int[maxFireteams];
        var leaders = new EntityUid?[maxFireteams];
        foreach (var other in squad.Members)
        {
            if (other == member ||
                !_fireteamMemberQuery.TryComp(other, out var otherFireteam) ||
                otherFireteam.Fireteam < 0 ||
                otherFireteam.Fireteam >= maxFireteams)
            {
                continue;
            }

            sizes[otherFireteam.Fireteam]++;
            if (_fireteamLeaderQuery.HasComp(other))
                leaders[otherFireteam.Fireteam] = other;
        }

        // Fireteam leaders take command of a fireteam that is not already led by one, if there is
        // one, replacing whoever is leading it. Fireteams the squad leader filled by hand beyond the
        // open count are considered too.
        var isFireteamLeader = CMUIsFireteamLeaderJob(member);
        var chosen = -1;
        if (isFireteamLeader)
        {
            for (var i = 0; i < maxFireteams; i++)
            {
                if (i >= openFireteams && sizes[i] == 0)
                    continue;

                if (leaders[i] is { } existing && CMUIsFireteamLeaderJob(existing))
                    continue;

                if (chosen == -1 || sizes[i] < sizes[chosen])
                    chosen = i;
            }
        }

        if (chosen == -1)
        {
            for (var i = 0; i < openFireteams; i++)
            {
                if (chosen == -1 || sizes[i] < sizes[chosen])
                    chosen = i;
            }
        }

        var fireteamMember = EnsureComp<FireteamMemberComponent>(member);
        fireteamMember.Fireteam = chosen;
        Dirty(member, fireteamMember);

        var currentLeader = leaders[chosen];
        if (isFireteamLeader &&
            CMUCanAutoLead(member) &&
            (currentLeader == null || !CMUIsFireteamLeaderJob(currentLeader.Value)))
        {
            if (currentLeader != null)
            {
                RemComp<FireteamLeaderComponent>(currentLeader.Value);
                _adminLog.Add(LogType.RMCFireteam, $"{ToPrettyString(member)} automatically replaced {ToPrettyString(currentLeader.Value)} as leader of fireteam {chosen}");
            }

            EnsureComp<FireteamLeaderComponent>(member);
        }
        else if (currentLeader == null && CMUPickFireteamLeader(squad, chosen, member) is { } pickedLeader)
        {
            EnsureComp<FireteamLeaderComponent>(pickedLeader);
        }

        var updatedEv = new FireteamMemberUpdatedEvent(member);
        RaiseLocalEvent(member, ref updatedEv, true);

        _adminLog.Add(LogType.RMCFireteam, $"{ToPrettyString(member)} was automatically assigned to fireteam {chosen}");

        SyncFireteams((squadId, squad));
        CMUPointTrackersAtFireteamLeaders(squad);
    }

    /// <summary>
    /// Points the tracker of every squad member who has not picked a tracking mode by hand at their
    /// fireteam leader. <see cref="SyncFireteams"/> only does this for members synced after their
    /// leader, so it depends on iteration order.
    /// </summary>
    private void CMUPointTrackersAtFireteamLeaders(SquadTeamComponent squad)
    {
        var maxFireteams = squad.Fireteams.Fireteams.Length;
        var leaders = new EntityUid?[maxFireteams];
        foreach (var other in squad.Members)
        {
            if (_fireteamLeaderQuery.HasComp(other) &&
                _fireteamMemberQuery.TryComp(other, out var leaderFireteam) &&
                leaderFireteam.Fireteam >= 0 &&
                leaderFireteam.Fireteam < maxFireteams)
            {
                leaders[leaderFireteam.Fireteam] = other;
            }
        }

        foreach (var other in squad.Members)
        {
            if (!_squadLeaderTrackerQuery.TryComp(other, out var tracker) ||
                (tracker.ManualMode && tracker.Mode != FireteamLeader) ||
                !_fireteamMemberQuery.TryComp(other, out var otherFireteam) ||
                otherFireteam.Fireteam < 0 ||
                otherFireteam.Fireteam >= maxFireteams)
            {
                continue;
            }

            var leader = leaders[otherFireteam.Fireteam];
            if (leader == null || leader == other)
            {
                // A fireteam leader's own tracker goes back to the squad leader.
                if (tracker.Mode == FireteamLeader)
                    SetMode((other, tracker), SquadLeaderMode);

                continue;
            }

            SetTarget((other, tracker), leader);
            SetMode((other, tracker), FireteamLeader);
        }
    }

    private EntityUid? CMUPickFireteamLeader(SquadTeamComponent squad, int fireteam, EntityUid newMember)
    {
        EntityUid? best = null;
        var bestIsLeaderJob = false;
        var bestScore = int.MinValue;
        if (CMUCanAutoLead(newMember))
        {
            best = newMember;
            bestIsLeaderJob = CMUIsFireteamLeaderJob(newMember);
            bestScore = CMUPaygradeScore(newMember);
        }

        foreach (var other in squad.Members)
        {
            if (other == newMember ||
                HasComp<SquadLeaderComponent>(other) ||
                !CMUCanAutoLead(other) ||
                !_fireteamMemberQuery.TryComp(other, out var otherFireteam) ||
                otherFireteam.Fireteam != fireteam)
            {
                continue;
            }

            var isLeaderJob = CMUIsFireteamLeaderJob(other);
            var score = CMUPaygradeScore(other);
            if (best == null ||
                (isLeaderJob && !bestIsLeaderJob) ||
                (isLeaderJob == bestIsLeaderJob && score > bestScore))
            {
                best = other;
                bestIsLeaderJob = isLeaderJob;
                bestScore = score;
            }
        }

        return best;
    }

    /// <summary>
    /// Whether someone can be made a squad or fireteam leader automatically: they must be conscious,
    /// awake and controlled by a player who is not SSD or AFK.
    /// </summary>
    public bool CMUCanAutoLead(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid) ||
            !_cmuMobState.IsAlive(uid) ||
            HasComp<SleepingComponent>(uid) ||
            !HasComp<ActorComponent>(uid) ||
            (TryComp(uid, out SSDIndicatorComponent? ssd) && ssd.IsSSD))
        {
            return false;
        }

        var ev = new CMUCanAutoLeadEvent(uid);
        RaiseLocalEvent(ref ev);
        return !ev.Cancelled;
    }

    /// <summary>
    /// Whether this squad member is in an auxiliary squad, which has no fireteams.
    /// </summary>
    private bool CMUInAuxiliarySquad(EntityUid uid)
    {
        return _squadMemberQuery.TryComp(uid, out var member) &&
               member.Squad is { } squad &&
               HasComp<CMUAuxiliarySquadComponent>(squad);
    }

    private bool CMUIsFireteamLeaderJob(EntityUid uid)
    {
        return _originalRoleQuery.CompOrNull(uid)?.Job is { } job && CMUFireteamLeaderJobs.Contains(job);
    }

    private int CMUPaygradeScore(EntityUid uid)
    {
        return CMURankScore.FromPaygrade(_rank.GetRank(uid)?.Paygrade);
    }

    /// <summary>
    /// The leader and members of each fireteam in a squad, indexed by fireteam.
    /// </summary>
    public (EntityUid? Leader, List<EntityUid> Members)[] CMUGetFireteams(SquadTeamComponent squad)
    {
        var fireteams = new (EntityUid? Leader, List<EntityUid> Members)[squad.Fireteams.Fireteams.Length];
        for (var i = 0; i < fireteams.Length; i++)
        {
            fireteams[i] = (null, new List<EntityUid>());
        }

        foreach (var member in squad.Members)
        {
            if (!_fireteamMemberQuery.TryComp(member, out var fireteam) ||
                fireteam.Fireteam < 0 ||
                fireteam.Fireteam >= fireteams.Length)
            {
                continue;
            }

            fireteams[fireteam.Fireteam].Members.Add(member);
            if (_fireteamLeaderQuery.HasComp(member))
                fireteams[fireteam.Fireteam].Leader = member;
        }

        return fireteams;
    }

    /// <summary>
    /// The fireteam this squad member is in, if any.
    /// </summary>
    public int? CMUGetFireteam(EntityUid member)
    {
        return _fireteamMemberQuery.TryComp(member, out var fireteam) ? fireteam.Fireteam : null;
    }

    /// <summary>
    /// Makes a member of a fireteam its leader, replacing the current leader.
    /// </summary>
    public void CMUSetFireteamLeader(Entity<SquadTeamComponent> squad, int fireteam, EntityUid newLeader, string reason)
    {
        if (_net.IsClient ||
            !_fireteamMemberQuery.TryComp(newLeader, out var newLeaderFireteam) ||
            newLeaderFireteam.Fireteam != fireteam)
        {
            return;
        }

        foreach (var member in squad.Comp.Members)
        {
            if (member != newLeader &&
                _fireteamLeaderQuery.HasComp(member) &&
                _fireteamMemberQuery.TryComp(member, out var memberFireteam) &&
                memberFireteam.Fireteam == fireteam)
            {
                RemComp<FireteamLeaderComponent>(member);
            }
        }

        EnsureComp<FireteamLeaderComponent>(newLeader);
        _adminLog.Add(LogType.RMCFireteam, $"{ToPrettyString(newLeader)} became leader of fireteam {fireteam}: {reason}");

        SyncFireteams(squad.AsNullable());
        CMUPointTrackersAtFireteamLeaders(squad.Comp);
    }
}
