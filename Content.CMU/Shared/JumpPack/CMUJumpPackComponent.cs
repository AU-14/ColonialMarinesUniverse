using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.JumpPack;

/// <summary>
/// A gas-fuelled pack worn in the back slot. Grants a directional boost, a z-level jump,
/// and a timed hover one z-level up. Fuel is the pack's <c>GasTank</c> air.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(CMUJumpPackSystem))]
public sealed partial class CMUJumpPackComponent : Component
{
    [DataField]
    public EntProtoId BoostAction = "CMUActionJumpPackBoost";

    [DataField, AutoNetworkedField]
    public EntityUid? BoostActionEntity;

    [DataField]
    public EntProtoId DirectionAction = "CMUActionJumpPackDirection";

    [DataField, AutoNetworkedField]
    public EntityUid? DirectionActionEntity;

    [DataField]
    public EntProtoId JumpAction = "CMUActionJumpPackJump";

    [DataField, AutoNetworkedField]
    public EntityUid? JumpActionEntity;

    [DataField]
    public EntProtoId HoverAction = "CMUActionJumpPackHover";

    [DataField, AutoNetworkedField]
    public EntityUid? HoverActionEntity;

    /// <summary>
    /// World direction the boost pushes the wearer.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Direction BoostDirection = Direction.North;

    /// <summary>
    /// When true, the direction action cycles all eight compass directions instead of the four cardinals.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool EightDirections;

    /// <summary>
    /// Shared by boost and jump. Boosting ignores it while hovering.
    /// </summary>
    [DataField]
    public TimeSpan BoostCooldown = TimeSpan.FromSeconds(7);

    [DataField]
    public float BoostDistance = 6f;

    [DataField]
    public float BoostSpeed = 12f;

    /// <summary>
    /// Upward z-velocity applied by the jump. Clears one level from <see cref="JumpTakeoffLocalPosition"/>.
    /// </summary>
    [DataField]
    public float JumpZVelocity = 5.5f;

    [DataField]
    public float JumpTakeoffLocalPosition = 0.25f;

    /// <summary>
    /// Horizontal push in the selected direction so the jump can carry the wearer onto a ledge.
    /// </summary>
    [DataField]
    public float JumpDashDistance = 1.5f;

    [DataField]
    public float JumpDashSpeed = 5f;

    [DataField]
    public TimeSpan HoverDuration = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Measured from hover start, applied once the hover ends so the action stays usable to end it early.
    /// </summary>
    [DataField]
    public TimeSpan HoverCooldown = TimeSpan.FromSeconds(35);

    [DataField]
    public float HoverAscendSpeed = 2.5f;

    [DataField]
    public float HoverDescendSpeed = 1.5f;

    // A full 5 L tank (2.051379 mol) lasts 300 seconds of hovering, or a boost/jump every cooldown for 300 seconds.
    [DataField]
    public float HoverFuelPerSecond = 0.0068379f;

    [DataField]
    public float FuelPerBoost = 0.0478655f;

    [DataField]
    public float FuelPerJump = 0.0478655f;

    [DataField, AutoNetworkedField]
    public EntityUid? HoverUser;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan HoverStart;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan HoverEnd;

    /// <summary>
    /// The wearer is still gliding down after a hover ended.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? DescendingUser;

    /// <summary>
    /// Server-only throttle for networking the gas drained while hovering.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextFuelSync;

    [DataField]
    public SoundSpecifier BoostSound = new SoundPathSpecifier("/Audio/Effects/spray2.ogg");

    [DataField]
    public SoundSpecifier HoverSound = new SoundPathSpecifier("/Audio/Effects/shuttle_thruster.ogg");
}
