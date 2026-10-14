using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.CMU14.Construction.Concrete;

/// <summary>
/// Freshly poured concrete that turns into <see cref="Hardened"/> once <see cref="Delay"/> has passed.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(CMUConcreteHardeningSystem))]
public sealed partial class CMUConcreteHardeningComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Hardened;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromMinutes(10);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan HardenAt;
}
