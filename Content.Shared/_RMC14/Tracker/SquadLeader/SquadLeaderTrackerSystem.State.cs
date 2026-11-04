using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Tracker.SquadLeader;

public sealed partial class SquadLeaderTrackerSystem
{
    [SubscribeLocalEvent]
    private void OnSquadLeaderTrackerComponentGetState(Entity<SquadLeaderTrackerComponent> ent, ref ComponentGetState args)
    {
        // References can outlive their entities. Build a safe snapshot without mutating gameplay state.
        TryGetNetEntity(ent.Comp.Target, out var netTarget);
        TryGetNetEntity(ent.Comp.BattleBuddy, out var netBattleBuddy);
        args.State = new SquadLeaderTrackerComponentState
        {
            UpdateEvery = ent.Comp.UpdateEvery,
            Fireteams = ent.Comp.Fireteams,
            Mode = ent.Comp.Mode,
            ManualMode = ent.Comp.ManualMode,
            Target = netTarget,
            BattleBuddy = netBattleBuddy,
            TrackerModes = new(ent.Comp.TrackerModes),
        };
    }

    [SubscribeLocalEvent]
    private void OnSquadLeaderTrackerComponentHandleState(Entity<SquadLeaderTrackerComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not SquadLeaderTrackerComponentState state)
            return;

        ent.Comp.UpdateEvery = state.UpdateEvery;
        ent.Comp.Fireteams = state.Fireteams;
        ent.Comp.Mode = state.Mode;
        ent.Comp.ManualMode = state.ManualMode;
        ent.Comp.Target = EnsureEntity<SquadLeaderTrackerComponent>(state.Target, ent);
        ent.Comp.BattleBuddy = EnsureEntity<SquadLeaderTrackerComponent>(state.BattleBuddy, ent);
        ent.Comp.TrackerModes = new(state.TrackerModes);

        var ev = new SquadLeaderTrackerStateChangedEvent();
        RaiseLocalEvent(ent, ref ev);
    }
}

[ByRefEvent]
public readonly record struct SquadLeaderTrackerStateChangedEvent;

[Serializable, NetSerializable]
public sealed class SquadLeaderTrackerComponentState : ComponentState
{
    public TimeSpan UpdateEvery { get; init; }
    public FireteamData Fireteams { get; init; } = new();
    public ProtoId<TrackerModePrototype>? Mode { get; init; }
    public bool ManualMode { get; init; }
    public NetEntity? Target { get; init; }
    public NetEntity? BattleBuddy { get; init; }
    public HashSet<ProtoId<TrackerModePrototype>> TrackerModes { get; init; } = new();
}
