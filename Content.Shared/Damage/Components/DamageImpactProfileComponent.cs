using Robust.Shared.GameStates;

namespace Content.Shared.Damage.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class DamageImpactProfileComponent : Component
{
    [DataField]
    public DamageImpactProfile Melee = new();

    [DataField]
    public DamageImpactProfile HeavyMelee = new();

    [DataField]
    public DamageImpactProfile Thrown = new();

    public DamageImpact GetMeleeImpact(DamageImpact fallback, bool heavy)
    {
        if (heavy && HeavyMelee.IsSpecified)
            return HeavyMelee.ApplyTo(fallback, DamageImpactDelivery.Melee);

        return Melee.ApplyTo(fallback, DamageImpactDelivery.Melee);
    }

    public DamageImpact GetThrownImpact(DamageImpact fallback)
        => Thrown.ApplyTo(fallback, DamageImpactDelivery.Thrown);
}
