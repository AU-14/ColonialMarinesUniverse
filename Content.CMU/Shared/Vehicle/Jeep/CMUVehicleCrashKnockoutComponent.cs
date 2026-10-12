using Robust.Shared.Audio;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Vehicle.Jeep;

/// <summary>
/// A hard head on crash can knock the riders of an open vehicle out. From <see cref="MinSpeed"/> of
/// top speed each rider is knocked out with <see cref="MinChance"/>, rising to certain at top speed,
/// for a random time between <see cref="MinDuration"/> and <see cref="MaxDuration"/>. A driver
/// knocked out slumps on the wheel and <see cref="HornSound"/> blares until they come to.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CMUVehicleCrashKnockoutComponent : Component
{
    /// <summary>
    /// The slowest crash that can knock anyone out, as a share of the vehicle's top speed.
    /// </summary>
    [DataField]
    public float MinSpeed = 0.7f;

    /// <summary>
    /// Each rider's chance of being knocked out by a crash at <see cref="MinSpeed"/>.
    /// </summary>
    [DataField]
    public float MinChance = 0.2f;

    [DataField]
    public TimeSpan MinDuration = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan MaxDuration = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The horn held down by a driver out on the wheel; stopped as they come to.
    /// </summary>
    [DataField]
    public SoundSpecifier? HornSound;

    /// <summary>
    /// The blaring horn's audio entity, if it is playing.
    /// </summary>
    [DataField]
    public EntityUid? Horn;

    /// <summary>
    /// The driver slumped on the wheel.
    /// </summary>
    [DataField]
    public EntityUid? Driver;

    /// <summary>
    /// When the driver's knockout ends, and the horn with it at the latest.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan HornUntil;
}
