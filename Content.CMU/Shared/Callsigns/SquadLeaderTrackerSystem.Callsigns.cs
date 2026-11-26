using Content.Shared._RMC14.Marines.Squads;
using Content.Shared.CMU14.Callsigns;

namespace Content.Shared._RMC14.Tracker.SquadLeader;

// CMU14 callsigns in the squad menu. callsigns are server-only data, so the shared tracker
// asks for them through an event and sends them in the BUI state (never a networked
// component, which would hand the enemy roster to anyone reading their client state)
public sealed partial class SquadLeaderTrackerSystem
{
    // CMU14 method
    private void InitializeCMUCallsigns()
    {
        SubscribeLocalEvent<AU14CallsignChangedEvent>(CMUOnCallsignChanged);
    }

    // CMU14 method: objectives plus the viewer-faction callsigns of the squad
    private SquadLeaderTrackerBoundUserInterfaceState CMUBuildUiState(EntityUid viewer, SquadTeamComponent? team)
    {
        var objectives = team == null
            ? new Dictionary<SquadObjectiveType, string>()
            : new Dictionary<SquadObjectiveType, string>(team.Objectives);

        IEnumerable<EntityUid> members = team == null
            ? Array.Empty<EntityUid>()
            : team.Members;

        // only the server callsign system answers
        var ev = new AU14GetSquadCallsignsEvent(members, new Dictionary<NetEntity, string>());
        RaiseLocalEvent(viewer, ref ev);

        return new SquadLeaderTrackerBoundUserInterfaceState(objectives, ev.Callsigns);
    }

    // CMU14 method: a callsign changed, re-send state to open squad menus in that squad
    private void CMUOnCallsignChanged(ref AU14CallsignChangedEvent ev)
    {
        if (_net.IsClient)
            return;

        if (!_squadMemberQuery.TryComp(ev.Member, out var squadMember) ||
            !TryComp(squadMember.Squad, out SquadTeamComponent? team))
        {
            return;
        }

        foreach (var member in team.Members)
        {
            if (!_squadLeaderTrackerQuery.HasComp(member) ||
                !_ui.IsUiOpen(member, SquadLeaderTrackerUI.Key))
            {
                continue;
            }

            _ui.SetUiState(member, SquadLeaderTrackerUI.Key, CMUBuildUiState(member, team));
        }
    }
}
