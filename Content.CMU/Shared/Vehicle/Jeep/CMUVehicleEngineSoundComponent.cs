using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Vehicle.Jeep;

/// <summary>
/// An open vehicle's engine: it starts when someone takes the wheel and can drive, revs as it
/// pulls away, runs rough while the engine is badly damaged, and sputters out when the tank runs
/// dry or the engine dies under the driver. While it runs, each client plays an idle loop and a
/// sped-up driving loop around it and fades between them with the vehicle's speed.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUVehicleEngineSoundComponent : Component
{
    [DataField]
    public SoundSpecifier? StartSound;

    [DataField]
    public SoundSpecifier? IdleSound;

    [DataField]
    public SoundSpecifier? DriveSound;

    /// <summary>
    /// Loops played instead of the idle and driving ones while the engine is below its smoke threshold.
    /// </summary>
    [DataField]
    public SoundSpecifier? RoughSound;

    [DataField]
    public SoundSpecifier? RoughDriveSound;

    [DataField]
    public SoundSpecifier? RevSound;

    [DataField]
    public SoundSpecifier? StallSound;

    /// <summary>
    /// From the start sound to the loops, which take over while the start fades out.
    /// </summary>
    [DataField]
    public TimeSpan IdleDelay = TimeSpan.FromSeconds(1.9);

    [DataField]
    public TimeSpan RevCooldown = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Speed above which the vehicle counts as moving, for the rev when it pulls away.
    /// </summary>
    [DataField]
    public float MovingSpeed = 0.1f;

    /// <summary>
    /// Whether the engine's loops are playing: it has started and still runs.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool EngineRunning;

    [ViewVariables, AutoNetworkedField]
    public bool Rough;

    [ViewVariables]
    public bool On;

    public TimeSpan? IdleAt;

    /// <summary>
    /// The start sound while it plays, so an engine switched off mid-crank cuts it.
    /// </summary>
    [ViewVariables]
    public EntityUid? StartStream;

    public TimeSpan NextRev;
    public bool WasMoving;
}
