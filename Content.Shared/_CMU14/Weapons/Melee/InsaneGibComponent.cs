using Content.Shared.Destructible.Thresholds;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Weapons.Melee;

/// <summary>
/// Gibs hit bodies on melee impact and sprays extra gore around them. Debug weapon support.
/// </summary>
[RegisterComponent]
public sealed partial class InsaneGibComponent : Component
{
    /// <summary>
    /// Extra gore spawned around each gibbed body, per prototype.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<EntProtoId, MinMax> ExtraGibs = new();

    /// <summary>
    /// Launch impulse multiplier for the body's real parts and organs.
    /// </summary>
    [DataField]
    public float SplatModifier = 5f;

    /// <summary>
    /// Launch impulse for the extra spawned gore.
    /// </summary>
    [DataField]
    public float ExtraGibImpulse = 20f;

    /// <summary>
    /// Max distance extra gore spawns from the body.
    /// </summary>
    [DataField]
    public float SpawnOffset = 0.5f;
}
