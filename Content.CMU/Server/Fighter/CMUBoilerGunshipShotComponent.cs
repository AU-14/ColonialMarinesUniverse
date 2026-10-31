using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.CMU14.Fighter;

/// <summary>A launched plasma shot resolves independently of its Boiler's subsequent actions.</summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CMUBoilerGunshipShotComponent : Component
{
    public EntityUid Target;
    public EntityUid Terrain;
    public float Damage;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan ImpactAt;
}
