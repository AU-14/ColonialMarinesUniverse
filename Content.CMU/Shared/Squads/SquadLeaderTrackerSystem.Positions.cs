using Content.Shared._RMC14.Marines.Squads;
using Robust.Shared.Map;

namespace Content.Shared._RMC14.Tracker.SquadLeader;

public sealed partial class SquadLeaderTrackerSystem
{
    private void RefreshCMULeaderPositions()
    {
        _squadLeaders.Clear();
        var squadLeaders = EntityQueryEnumerator<SquadLeaderComponent, SquadMemberComponent, RMCTrackableComponent>();
        while (squadLeaders.MoveNext(out var uid, out _, out var member, out _))
        {
            if (member.Squad is not { } squad)
                continue;

            _squadLeaders.TryAdd(squad, _transform.GetMapCoordinates(uid));
        }

        foreach (var leaders in _fireteamLeaders)
        {
            leaders?.Clear();
        }
        var fireteamLeaders = EntityQueryEnumerator<FireteamLeaderComponent, FireteamMemberComponent, SquadMemberComponent>();
        while (fireteamLeaders.MoveNext(out var uid, out _, out var fireteamMember, out var squadMember))
        {
            if (squadMember.Squad is not { } squad)
                continue;

            if (fireteamMember.Fireteam < 0 || fireteamMember.Fireteam >= _fireteamLeaders.Length)
                continue;

            ref var leaders = ref _fireteamLeaders[fireteamMember.Fireteam];
            leaders ??= new Dictionary<EntityUid, MapCoordinates>();
            leaders[squad] = _transform.GetMapCoordinates(uid);
        }
    }
}
