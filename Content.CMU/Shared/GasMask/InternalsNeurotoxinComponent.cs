using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.GasMask;

/// <summary>
/// Tracks how long a mob on internals has been standing in neurotoxin. Internals only keep it out for a while
/// per exposure; leaving the gas resets the timer. Server-side only: neurotoxin is applied by the server.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(SharedGasMaskSystem))]
public sealed partial class InternalsNeurotoxinComponent : Component
{
    /// <summary>When the current continuous exposure began.</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan ExposureStart;

    /// <summary>The last time neurotoxin reached the mob.</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan LastExposure;
}
