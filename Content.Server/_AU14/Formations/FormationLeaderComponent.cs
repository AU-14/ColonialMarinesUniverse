using Content.Shared._AU14.Formations;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Server._AU14.Formations;

[RegisterComponent]
public sealed partial class AU14FormationLeaderComponent : Component
{
    /// <summary>Formation dot entities currently waiting for soldiers to slot into them.</summary>
    public List<EntityUid> PlacedDots = new();

    /// <summary>Entities currently slotted into the active formation.</summary>
    public List<EntityUid> ActiveFollowers = new();

    /// <summary>Dots staged during planning mode — not yet spawned.</summary>
    public List<FormationPendingDot> PendingDots = new();

    /// <summary>Randomized color assigned on component init. Vivid hue unique to this leader.</summary>
    [DataField]
    public Color FormationColor = Color.White;

    /// <summary>When true, leader movement does not propagate to slotted followers.</summary>
    [DataField]
    public bool FormationFrozen = true;

    /// <summary>How aggressively followers reposition toward their formation slot.</summary>
    [DataField]
    public FormationFollowMode FollowMode = FormationFollowMode.Chase;

    /// <summary>When false, FormationMemberComponent is removed from all members so they collide normally.</summary>
    [DataField]
    public bool CollisionsDisabled = true;

    /// <summary>When true, newly spawned dots last 15 minutes instead of 2. Intended only for prolonged static operations.</summary>
    [DataField]
    public bool ExtendedDotLifetime;

    /// <summary>True while the leader is in the dot-placement phase before confirming.</summary>
    public bool IsInPlanningMode;

    /// <summary>Whether the currently staged dot type is a leader dot.</summary>
    public bool IsPlacingLeaderDot;

    /// <summary>Tile position tracked for per-tile movement detection.</summary>
    public Vector2i LastTilePos;

    /// <summary>Facing tracked to detect turns between movement ticks.</summary>
    public Direction LastFacing = Direction.South;

    /// <summary>When true, persistent indicator dots are rendered at each follower's computed target position.</summary>
    public bool DebugShowSlots;

    /// <summary>Maps active-follower UID → the debug indicator dot entity for that follower.</summary>
    public Dictionary<EntityUid, EntityUid> DebugDots = new();

    /// <summary>The entity UID of the granted formation action (so it can be removed on component removal).</summary>
    public EntityUid? ActionUid;

    /// <summary>Entity ID of the formation action prototype to grant.</summary>
    [DataField]
    public EntProtoId ActionPrototype = "AU14FormationMenuAction";
}

public sealed class FormationPendingDot
{
    public Vector2i TilePos;
    public Direction Facing;
    public bool IsLeaderDot;
}
