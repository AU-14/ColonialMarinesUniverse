using System.Numerics;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.PowerLoader;

/// <summary>
/// CMU's additions to the power loader: hydraulics when it turns, a beacon and a seated pilot while piloted,
/// clamps that force doors and podlocks, and damage that shows and slows it until it's welded back.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(CMUPowerLoaderSystem))]
public sealed partial class CMUPowerLoaderComponent : Component
{
    #region Turning

    /// <summary>
    /// Played when the loader turns to face a new direction.
    /// </summary>
    [DataField]
    public SoundSpecifier? TurnSound;

    /// <summary>
    /// Shortest time between two turn sounds, so quick wiggles don't spam it.
    /// </summary>
    [DataField]
    public TimeSpan TurnSoundCooldown = TimeSpan.FromSeconds(0.4);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextTurnSound;

    #endregion

    #region Visuals, applied on the client

    /// <summary>
    /// Draw depth of the loader while nobody pilots it, for the direction it's drawn facing,
    /// so someone standing on it shows in front of or behind it. Other directions keep the usual depth.
    /// </summary>
    [DataField]
    public Dictionary<Direction, DrawDepth.DrawDepth> UnpilotedDirectionDepths = new();

    /// <summary>
    /// Shift of the pilot's sprite in tiles on screen, whichever way the loader and the camera face.
    /// </summary>
    [DataField]
    public Vector2 PilotOffset;

    /// <summary>
    /// Extra shift of the pilot's sprite for the direction the loader is drawn facing, added to <see cref="PilotOffset"/>.
    /// </summary>
    [DataField]
    public Dictionary<Direction, Vector2> PilotDirectionOffsets = new();

    /// <summary>
    /// The point light is turned on while piloted, and kept on the beacon drawn on the sprite
    /// at this offset in tiles from the center for each direction the loader is drawn facing.
    /// </summary>
    [DataField]
    public Dictionary<Direction, Vector2> BeaconOffsets = new();

    #endregion

    #region Prying

    /// <summary>
    /// Multiplies the time to force a podlock, powered or not. Other doors take their usual time.
    /// </summary>
    [DataField]
    public float PodlockPryMultiplier = 12f;

    /// <summary>
    /// Played on the door while it's being forced, and cut off if the loader stops.
    /// </summary>
    [DataField]
    public SoundSpecifier? PrySound = new SoundPathSpecifier("/Audio/Machines/airlock_creaking.ogg");

    #endregion

    #region Damage

    /// <summary>
    /// The damage states, from least to most damaged.
    /// </summary>
    [DataField]
    public List<CMUPowerLoaderDamageState> DamageStates = new();

    /// <summary>
    /// Index into <see cref="DamageStates"/>, or -1 while undamaged.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int DamageState = -1;

    #endregion
}

[DataDefinition]
public sealed partial class CMUPowerLoaderDamageState
{
    /// <summary>
    /// Total damage at which this state starts.
    /// </summary>
    [DataField(required: true)]
    public FixedPoint2 Damage;

    [DataField]
    public float SpeedModifier = 1f;

    /// <summary>
    /// Spawned once on the loader when it gets this damaged, like a spark.
    /// </summary>
    [DataField]
    public EntProtoId? Effect;

    /// <summary>
    /// Played once with <see cref="Effect"/>.
    /// </summary>
    [DataField]
    public SoundSpecifier? Sound;

    /// <summary>
    /// Shown to the pilot when the loader gets this damaged.
    /// </summary>
    [DataField]
    public LocId? Popup;

    [DataField]
    public LocId? Examine;
}
