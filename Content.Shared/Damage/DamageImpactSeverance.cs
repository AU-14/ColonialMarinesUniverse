using Content.Shared.FixedPoint;

namespace Content.Shared.Damage;

/// <summary>
/// Calculates structural severance accumulation independently from normal health damage.
/// </summary>
public static class DamageImpactSeverance
{
    private const float BlastBruteWeight = 0.85f;
    private const float BluntWeight = 0.25f;
    private const float PiercingWeight = 0.65f;
    private const float SlashWeight = 1f;

    public static FixedPoint2 Calculate(DamageSpecifier damage, DamageImpact impact)
    {
        var weightedBrute = impact.Delivery == DamageImpactDelivery.Explosion
            ? GetPositive(damage, "Blunt") + GetPositive(damage, "Piercing") + GetPositive(damage, "Slash")
            : GetPositive(damage, "Blunt") * BluntWeight +
              GetPositive(damage, "Piercing") * PiercingWeight +
              GetPositive(damage, "Slash") * SlashWeight;

        if (weightedBrute <= 0f)
            return FixedPoint2.Zero;

        if (impact.Delivery == DamageImpactDelivery.Explosion)
            weightedBrute *= BlastBruteWeight;

        var multiplier = GetEnergyMultiplier(impact.Energy) *
                         GetContactMultiplier(impact.Contact) *
                         GetDeliveryMultiplier(impact.Delivery);

        if ((impact.Context & DamageImpactContext.PointBlank) != 0)
            multiplier *= 2f;

        return FixedPoint2.New(weightedBrute * multiplier);
    }

    private static float GetEnergyMultiplier(DamageImpactEnergy energy) => energy switch
    {
        DamageImpactEnergy.Low => 0.25f,
        DamageImpactEnergy.Medium => 0.45f,
        DamageImpactEnergy.High => 0.85f,
        DamageImpactEnergy.Severe => 1.2f,
        _ => 1f,
    };

    private static float GetContactMultiplier(DamageImpactContact contact) => contact switch
    {
        DamageImpactContact.Slash => 1.5f,
        DamageImpactContact.Fragment => 1.25f,
        DamageImpactContact.Stab => 0.6f,
        DamageImpactContact.Crush => 0.5f,
        _ => 1f,
    };

    private static float GetDeliveryMultiplier(DamageImpactDelivery delivery) => delivery switch
    {
        DamageImpactDelivery.Projectile or DamageImpactDelivery.Melee => 1f,
        DamageImpactDelivery.Thrown => 0.15f,
        DamageImpactDelivery.Explosion => 1.5f,
        DamageImpactDelivery.Contact or DamageImpactDelivery.Environment => 0.1f,
        _ => 0.25f,
    };

    private static float GetPositive(DamageSpecifier damage, string type)
        => damage.DamageDict.TryGetValue(type, out var value) && value > FixedPoint2.Zero
            ? value.Float()
            : 0f;
}
