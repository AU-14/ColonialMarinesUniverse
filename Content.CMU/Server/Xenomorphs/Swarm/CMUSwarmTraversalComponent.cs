using System.Numerics;

namespace Content.Server.CMU14.Xenomorphs.Swarm;

/// <summary>A local stair approach followed by a physical crossing into the next floor.</summary>
[RegisterComponent]
public sealed partial class CMUSwarmTraversalComponent : Component
{
    public CMUSwarmStairRoute? Route;
    public bool Crossing;
    public TimeSpan ProgressDeadline;
    public Vector2 LastPosition;
    public readonly Dictionary<EntityUid, TimeSpan> FailedStairs = new();
}

public readonly record struct CMUSwarmStairRoute(EntityUid Stair, EntityUid FromMap, EntityUid ToMap,
    Vector2 Approach, Vector2 Exit);
