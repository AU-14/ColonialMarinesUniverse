using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Organs.Heart;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedHeartSystem))]
public sealed partial class HeartComponent : Component
{
    [DataField, AutoNetworkedField]
    public int BeatsPerMinute = 70;

    [DataField, AutoNetworkedField]
    public bool Stopped;

    [DataField]
    public int MaxBpm = 200;

    /// <summary>
    ///     Below this floor the grace period starts; if the heart is still below
    ///     for the full <see cref="StopGracePeriod"/> it transitions to
    ///     <see cref="Stopped"/>.
    /// </summary>
    [DataField]
    public int MinBpmBeforeStop = 30;

    [DataField]
    public TimeSpan StopGracePeriod = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     When did BPM first dip below <see cref="MinBpmBeforeStop"/>? Null while
    ///     above the floor.
    /// </summary>
    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan? BelowThresholdSince;

    /// <summary>
    ///     When did circulation fully stop? Used for collapse timing.
    /// </summary>
    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan? NoPulseSince;

    [DataField, AutoPausedField]
    public TimeSpan NextPulseUpdate;

    [DataField]
    public TimeSpan PulseUpdateInterval = TimeSpan.FromSeconds(5);

    [DataField, AutoPausedField]
    public TimeSpan NextCardiacArrestTick;

    [DataField]
    public FixedPoint2 CardiacArrestAsphyxPerSecond = FixedPoint2.New(6);

    [DataField]
    public TimeSpan CardiacArrestUnconsciousDelay = TimeSpan.FromSeconds(5);
}
