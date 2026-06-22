using System.Numerics;

namespace Content.Server._AU14.Formations;

[RegisterComponent]
public sealed partial class FormationSlottedComponent : Component
{
    /// <summary>The formation commander whose movement we echo.</summary>
    public EntityUid LeaderUid;

    /// <summary>
    /// Offset stored in leader-local space.
    /// Forward (+Y) = direction the leader faces. Right (+X) = leader's right hand.
    /// Computed once on slot: localOffset = Rotate(dotWorldPos - leaderWorldPos, -leaderFacing).
    /// </summary>
    public Vector2 LocalOffset;

    /// <summary>True if this entity occupies the leader slot (commander's own position marker).</summary>
    public bool IsLeaderDot;

    /// <summary>Set to true during forced movement to prevent MoveEvent from triggering a voluntary-unslot check.</summary>
    public bool IsBeingForceMoved;

    /// <summary>Queued tile steps to walk around a wall obstacle. Consumed one step per leader movement tick.</summary>
    public Queue<Vector2i> PathQueue = new();

    /// <summary>True while the 1-second join stun is active (prevents premature unslot on join teleport).</summary>
    public bool JoinStunActive = true;

    /// <summary>Target tile for smooth formation movement. Null when the follower is stationary.</summary>
    public Vector2i? SmoothTargetTile;

    /// <summary>Facing direction to apply when the smooth move completes.</summary>
    public Direction SmoothTargetFacing = Direction.South;
}
