using Content.Shared.CMU14.Xenomorphs.Swarm;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Xenomorphs.Swarm;

/// <summary>Orders belong to the unit, never to an ovi UI or its temporary selection.</summary>
[RegisterComponent]
public sealed partial class CMUSwarmAgentComponent : Component
{
    public int Squad;
    public int Number;
    public CMUSwarmAggression Aggression = CMUSwarmAggression.Aggressive;
    public CMUSwarmOrder? Order;
    public EntityCoordinates? Home;
    public EntityCoordinates? Destination;
    public int Waypoint;
    public int FormationSlot;
    public TimeSpan PauseUntil;
    public TimeSpan RestAt;
    public TimeSpan WakeAt;
    public TimeSpan MoveUntil;
    public TimeSpan NextWork;
    public TimeSpan NextSense;
    public bool Slow;
}

public sealed class CMUSwarmOrder
{
    public CMUSwarmOrderKind Kind;
    public CMUSwarmStructure Structure;
    public EntityUid? Follow;
    public readonly List<EntityUid> Flags = new();
    public EntityUid? Worker;
    public bool Complete;
    public int NextSlot;
}

[RegisterComponent]
public sealed partial class CMUSwarmCommanderComponent : Component
{
    public readonly List<EntityUid> Actions = new();
    public readonly List<EntityUid> Selection = new();
    public CMUSwarmOrderKind? Pending;
    public CMUSwarmStructure Structure;
    public bool Append;
    public TimeSpan NextCommand;
    public TimeSpan NextUi;
}
