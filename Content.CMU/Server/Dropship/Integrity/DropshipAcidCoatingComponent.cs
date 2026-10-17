using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.CMU14.Dropship.Integrity;

/// <summary>A single corrosion effect shared by the entire hull; repeated hits refresh it.</summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class DropshipAcidCoatingComponent : Component
{
    [DataField]
    public float DamagePerSecond;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan ExpiresAt;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextDamageAt;
}
