using System.Numerics;

namespace Content.Server.CMU14.Xenomorphs.Swarm;

/// <summary>A local stair approach followed by a physical crossing into the next floor.</summary>
[RegisterComponent]
public sealed partial class CMUSwarmTraversalComponent : Component
{
    public CMUSwarmStairRoute? Route;
    public bool Crossing;

    /// <summary>
    /// Within <c>StairShield</c> of a stair or ramp shaft on its floor. Swarm separation is suppressed here so
    /// a crowd at the landing cannot push the member back down the stair it just climbed.
    /// </summary>
    public bool NearStair;
    public TimeSpan ProgressDeadline;
    public Vector2 LastPosition;
    public readonly Dictionary<EntityUid, TimeSpan> FailedStairs = new();
}

public readonly record struct CMUSwarmStairRoute(EntityUid Stair, EntityUid FromMap, EntityUid ToMap,
    Vector2 Approach, Vector2 Exit);
