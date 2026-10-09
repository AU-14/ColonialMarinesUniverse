using System.Text.RegularExpressions;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared.Database;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Tracker.SquadLeader;

// CMU14: automatic fireteam assignment. Lives in a partial because fireteam membership and
// leadership components are only writable by SquadLeaderTrackerSystem.
public sealed partial class SquadLeaderTrackerSystem
{
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

    // Paygrades look like "E4", "O-6", "W2" or "E9E" across every faction's rank set.
    private static readonly Regex CMUPaygradeRegex = new(@"^([EWO])-?(\d+)(\w*)$", RegexOptions.Compiled);

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
        if (isFireteamLeader && (currentLeader == null || !CMUIsFireteamLeaderJob(currentLeader.Value)))
        {
            if (currentLeader != null)
            {
                RemComp<FireteamLeaderComponent>(currentLeader.Value);
                _adminLog.Add(LogType.RMCFireteam, $"{ToPrettyString(member)} automatically replaced {ToPrettyString(currentLeader.Value)} as leader of fireteam {chosen}");
            }

            EnsureComp<FireteamLeaderComponent>(member);
        }
        else if (currentLeader == null)
        {
            EnsureComp<FireteamLeaderComponent>(CMUPickFireteamLeader(squad, chosen, member));
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

    private EntityUid CMUPickFireteamLeader(SquadTeamComponent squad, int fireteam, EntityUid newMember)
    {
        var best = newMember;
        var bestIsLeaderJob = CMUIsFireteamLeaderJob(newMember);
        var bestScore = CMUPaygradeScore(newMember);
        foreach (var other in squad.Members)
        {
            if (other == newMember ||
                HasComp<SquadLeaderComponent>(other) ||
                !_fireteamMemberQuery.TryComp(other, out var otherFireteam) ||
                otherFireteam.Fireteam != fireteam)
            {
                continue;
            }

            var isLeaderJob = CMUIsFireteamLeaderJob(other);
            var score = CMUPaygradeScore(other);
            if ((isLeaderJob && !bestIsLeaderJob) ||
                (isLeaderJob == bestIsLeaderJob && score > bestScore))
            {
                best = other;
                bestIsLeaderJob = isLeaderJob;
                bestScore = score;
            }
        }

        return best;
    }

    private bool CMUIsFireteamLeaderJob(EntityUid uid)
    {
        return _originalRoleQuery.CompOrNull(uid)?.Job is { } job && CMUFireteamLeaderJobs.Contains(job);
    }

    /// <summary>
    /// Orders ranks enlisted, then warrant, then officer, then by grade. Unranked people and
    /// unreadable paygrades sort lowest.
    /// </summary>
    private int CMUPaygradeScore(EntityUid uid)
    {
        if (_rank.GetRank(uid)?.Paygrade is not { } paygrade)
            return -1;

        var match = CMUPaygradeRegex.Match(paygrade);
        if (!match.Success)
            return -1;

        var tier = match.Groups[1].Value switch
        {
            "W" => 1,
            "O" => 2,
            _ => 0,
        };

        return tier * 1000 + int.Parse(match.Groups[2].Value) * 10 + (match.Groups[3].Length > 0 ? 1 : 0);
    }
}
