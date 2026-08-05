using Content.Shared.Damage.Prototypes;
using Robust.Shared.GameStates;

namespace Content.Shared.Damage.Components;

/// <summary>
/// Applies runtime damage modifier sets to an entity.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DamageProtectionBuffComponent : Component
{
    /// <summary>
    /// Damage modifier prototypes applied while the component is present.
    /// </summary>
    [DataField]
    public Dictionary<string, DamageModifierSetPrototype> Modifiers = new();

    /// <summary>
    /// Direct explosion damage multiplier. This is not relayed through worn inventory items.
    /// </summary>
    public float? ExplosionCoefficient;
}
