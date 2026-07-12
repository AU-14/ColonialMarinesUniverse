using Content.Shared._CMU14.Medical.Injuries.Pain;

namespace Content.Shared._CMU14.Medical.Injuries.Pain.Penalties;

public static class CMUPainTierPenaltyMultipliers
{
    public static float GetMovementMultiplier(PainTier tier) => tier switch
    {
        PainTier.None => 1.00f,
        PainTier.Mild => 0.97f,
        PainTier.Moderate => 0.92f,
        PainTier.Severe => 0.85f,
        PainTier.Shock => 0.70f,
        _ => 1f,
    };

    public static float GetAimSwayMultiplier(PainTier tier) => tier switch
    {
        PainTier.None => 1.00f,
        PainTier.Mild => 1.01f,
        PainTier.Moderate => 1.03f,
        PainTier.Severe => 1.08f,
        PainTier.Shock => 1.15f,
        _ => 1f,
    };

    public static float GetActionSpeedMultiplier(PainTier tier) => tier switch
    {
        PainTier.None => 1.00f,
        PainTier.Mild => 1.05f,
        PainTier.Moderate => 1.15f,
        PainTier.Severe => 1.30f,
        PainTier.Shock => 1.50f,
        _ => 1f,
    };
}
