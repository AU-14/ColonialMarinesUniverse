using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Tracker.SquadLeader;
using Robust.Shared.Network;

namespace Content.Shared.CMU14.Squads;

/// <summary>
/// Automatically places people into a fireteam when they join a squad. Only runs on joining, so
/// a squad leader's manual changes are kept until the person leaves and rejoins the squad.
/// </summary>
public sealed partial class CMUAutoFireteamSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private SquadLeaderTrackerSystem _squadLeaderTracker = default!;

    // Assignment waits a tick: spawning assigns the squad before promoting the squad leader,
    // and the squad leader must not be put in a fireteam.
    private readonly HashSet<EntityUid> _pending = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<SquadMemberAddedEvent>(OnSquadMemberAdded);
        SubscribeLocalEvent<SquadMemberRemovedEvent>(OnSquadMemberRemoved);
    }

    private void OnSquadMemberAdded(ref SquadMemberAddedEvent ev)
    {
        if (_net.IsClient)
            return;

        _pending.Add(ev.Member);
    }

    private void OnSquadMemberRemoved(ref SquadMemberRemovedEvent ev)
    {
        _pending.Remove(ev.Member);
    }

    public override void Update(float frameTime)
    {
        if (_pending.Count == 0)
            return;

        foreach (var member in _pending)
        {
            if (!TerminatingOrDeleted(member))
                _squadLeaderTracker.CMUAutoAssignFireteam(member);
        }

        _pending.Clear();
    }
}
