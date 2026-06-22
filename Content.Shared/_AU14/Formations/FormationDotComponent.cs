using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;

namespace Content.Shared._AU14.Formations;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FormationDotComponent : Component
{
    [AutoNetworkedField, DataField]
    public Color DotColor = Color.White;

    [AutoNetworkedField, DataField]
    public Direction FacingDirection = Direction.South;

    [AutoNetworkedField, DataField]
    public bool IsLeaderDot;

    /// <summary>When this dot expires (server mapped time).</summary>
    [AutoNetworkedField]
    public TimeSpan DeathTime;

    /// <summary>Total lifetime used to compute client-side alpha fade.</summary>
    [AutoNetworkedField]
    public TimeSpan MaxLifetime = TimeSpan.FromSeconds(10);

    // Server-side only — entity currently occupying this slot.
    public EntityUid? SlottedEntity;

    // Server-side only — leader who placed this dot.
    public EntityUid OwnerLeader;

    // Server-side only — when true, Update() repositions this dot every tick to track the
    // leader's formation slot (the "moving car" return dot after a follower gets blocked).
    public bool IsDynamicSlot;

    // Server-side only — the leader-local offset that defines this slot's position.
    public Vector2 SlotLocalOffset;
}
