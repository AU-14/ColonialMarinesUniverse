using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Explosion;

/// <summary>
/// Spawns an entity where this explosive goes off, like the phosphorus cloud from a white phosphorus mortar shell.
/// </summary>
[RegisterComponent]
public sealed partial class CMUSpawnOnExplosiveTriggerComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Spawn;
}
