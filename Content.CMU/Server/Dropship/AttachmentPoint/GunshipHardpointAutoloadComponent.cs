using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.CMU14.Dropship.AttachmentPoint;

/// <summary>
/// Reloads this mounted hardpoint weapon from a gunship ammunition rack on the same grid once it runs dry.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(GunshipHardpointAutoloadSystem))]
public sealed partial class GunshipHardpointAutoloadComponent : Component
{
    /// <summary>How long the weapon sits empty before the rack reloads it.</summary>
    [DataField]
    public TimeSpan ReloadDelay = TimeSpan.FromSeconds(3);

    /// <summary>When the pending reload happens, or null while none is pending.</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? ReloadAt;
}
