using Content.Shared._RMC14.Marines.Squads;
using Content.Shared.Database;

namespace Content.Shared._RMC14.Tracker.SquadLeader;

// CMU14: fireteam management from the overwatch console. Lives in a partial because fireteam
// membership and leadership components are only writable by SquadLeaderTrackerSystem.
public sealed partial class SquadLeaderTrackerSystem
{
    /// <summary>
    /// Moves a squad member into <paramref name="fireteam"/>, or out of every fireteam when it is
    /// null. A fireteam leader who changes fireteam stops leading.
    /// </summary>
    public void CMUMoveToFireteam(Entity<SquadTeamComponent> squad, EntityUid member, int? fireteam, EntityUid actor)
    {
        if (_net.IsClient ||
            !squad.Comp.Members.Contains(member) ||
            HasComp<SquadLeaderComponent>(member) ||
            (fireteam != null && CMUInAuxiliarySquad(member)) ||
            fireteam is < 0 ||
            fireteam >= squad.Comp.Fireteams.Fireteams.Length)
        {
            return;
        }

        var current = CMUGetFireteam(member);
        if (current == fireteam)
            return;

        RemComp<FireteamLeaderComponent>(member);

        if (fireteam is { } index)
        {
            var fireteamMember = EnsureComp<FireteamMemberComponent>(member);
            fireteamMember.Fireteam = index;
            Dirty(member, fireteamMember);
            _adminLog.Add(LogType.RMCFireteam, $"{ToPrettyString(actor)} moved {ToPrettyString(member)} to fireteam {index} from overwatch");
        }
        else
        {
            RemComp<FireteamMemberComponent>(member);
            _adminLog.Add(LogType.RMCFireteam, $"{ToPrettyString(actor)} removed {ToPrettyString(member)} from fireteam {current} from overwatch");
        }

        var updatedEv = new FireteamMemberUpdatedEvent(member);
        RaiseLocalEvent(member, ref updatedEv, true);

        SyncFireteams(squad.AsNullable());
        CMUPointTrackersAtFireteamLeaders(squad.Comp);
        Dirty(squad);
    }

    /// <summary>
    /// Makes a fireteam member its leader, or stands them down.
    /// </summary>
    public void CMUSetFireteamLeadership(Entity<SquadTeamComponent> squad, EntityUid member, bool lead, EntityUid actor)
    {
        if (_net.IsClient ||
            !squad.Comp.Members.Contains(member) ||
            CMUGetFireteam(member) is not { } fireteam)
        {
            return;
        }

        if (lead)
        {
            CMUSetFireteamLeader(squad, fireteam, member, $"set by {ToPrettyString(actor)} from overwatch");
        }
        else if (HasComp<FireteamLeaderComponent>(member))
        {
            RemComp<FireteamLeaderComponent>(member);
            _adminLog.Add(LogType.RMCFireteam, $"{ToPrettyString(actor)} stood {ToPrettyString(member)} down as leader of fireteam {fireteam} from overwatch");
            SyncFireteams(squad.AsNullable());
            CMUPointTrackersAtFireteamLeaders(squad.Comp);
        }

        Dirty(squad);
    }

    /// <summary>
    /// The fireteam nicknames, so a resync (which rebuilds the fireteams from their members) can put
    /// them back.
    /// </summary>
    private static string?[] CMUSaveFireteamNicknames(FireteamData data)
    {
        var nicknames = new string?[data.Fireteams.Length];
        for (var i = 0; i < nicknames.Length; i++)
        {
            nicknames[i] = data.Fireteams[i]?.Nickname;
        }

        return nicknames;
    }

    private static void CMURestoreFireteamNicknames(FireteamData data, string?[] nicknames)
    {
        for (var i = 0; i < nicknames.Length && i < data.Fireteams.Length; i++)
        {
            if (nicknames[i] == null)
                continue;

            data.Fireteams[i] ??= new SquadLeaderTrackerFireteam();
            data.Fireteams[i]!.Nickname = nicknames[i];
        }
    }
}
