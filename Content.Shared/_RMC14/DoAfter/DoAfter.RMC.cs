using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.DoAfter;

public sealed partial class DoAfter
{
    /// <summary>
    /// Last time the server-authoritative RMC target effect was scheduled.
    /// </summary>
    [DataField("lastEffectSpawnTime", customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan? LastEffectSpawnTime;
}
