using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Explosion;

/// <summary>
/// Spawns an entity where this explosive goes off, like the phosphorus cloud from a white phosphorus mortar shell, and
/// optionally sets that one tile on fire.
/// </summary>
[RegisterComponent]
public sealed partial class CMUSpawnOnExplosiveTriggerComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Spawn;

    /// <summary>Tile fire to start on the one tile the explosive went off on.</summary>
    [DataField]
    public EntProtoId? Fire;
}
