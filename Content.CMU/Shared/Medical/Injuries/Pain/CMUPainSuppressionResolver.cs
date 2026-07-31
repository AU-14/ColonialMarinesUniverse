using System;

namespace Content.Shared._CMU14.Medical.Injuries.Pain;

public readonly record struct CMUPainSuppressionResult(
    float AccumulationSuppression,
    int TierSuppression,
    float DecayBonus,
    bool Changed);

public static class CMUPainSuppressionResolver
{
    private const float EffectEpsilon = 0.001f;

    public static CMUPainSuppressionResult ResolveAndPrune(
        PainSuppressionComponent suppression,
        float painFraction,
        TimeSpan now)
    {
        var removed = suppression.ActiveProfiles.RemoveAll(entry => entry.ExpiresAt <= now) > 0;

        var bestAccumulation = 0f;
        var bestTier = 0;
        var bestDecay = 0f;
        var additiveAccumulation = 0f;
        var additiveTier = 0;
        var additiveDecay = 0f;
        foreach (var entry in suppression.ActiveProfiles)
        {
            var effectiveness = GetEffectiveness(entry, painFraction);
            var accumulation = entry.AccumulationSuppression * effectiveness;
            var tier = (int) MathF.Floor(entry.TierSuppression * effectiveness + EffectEpsilon);
            var decay = entry.DecayBonus * effectiveness;

            if (entry.Additive)
            {
                additiveAccumulation += accumulation;
                additiveTier += tier;
                additiveDecay += decay;
                continue;
            }

            if (IsProfileStronger(accumulation, tier, decay, bestAccumulation, bestTier, bestDecay))
            {
                bestAccumulation = accumulation;
                bestTier = tier;
                bestDecay = decay;
            }
        }

        bestAccumulation = Math.Clamp(bestAccumulation + additiveAccumulation, 0f, 1f);
        bestTier = Math.Max(0, bestTier + additiveTier);
        bestDecay = Math.Max(0f, bestDecay + additiveDecay);

        var changed = removed
            || MathF.Abs(suppression.AccumulationSuppression - bestAccumulation) > EffectEpsilon
            || suppression.TierSuppression != bestTier
            || MathF.Abs(suppression.DecayBonus - bestDecay) > EffectEpsilon;

        return new CMUPainSuppressionResult(bestAccumulation, bestTier, bestDecay, changed);
    }

    public static bool SuppressionImproved(
        PainSuppressionComponent suppression,
        float oldAccumulation,
        int oldTier,
        float oldDecay)
    {
        return suppression.TierSuppression > oldTier
            || suppression.AccumulationSuppression > oldAccumulation + EffectEpsilon
            || suppression.DecayBonus > oldDecay + EffectEpsilon;
    }

    private static float GetEffectiveness(PainSuppressionEntry entry, float painFraction)
    {
        if (entry.ReductionDecreaseRate <= 0f || painFraction <= 0f)
            return 1f;

        return Math.Clamp(1f - painFraction * entry.ReductionDecreaseRate, 0f, 1f);
    }

    private static bool IsProfileStronger(
        float accumulation,
        int tier,
        float decay,
        float bestAccumulation,
        int bestTier,
        float bestDecay)
    {
        if (tier != bestTier)
            return tier > bestTier;
        if (MathF.Abs(accumulation - bestAccumulation) > EffectEpsilon)
            return accumulation > bestAccumulation;

        return decay > bestDecay;
    }
}
