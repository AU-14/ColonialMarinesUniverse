using Content.Shared.FixedPoint;

namespace Content.Shared._CMU14.Medical.Injuries.Pain;

public enum PainTier : byte
{
    None = 0,
    Mild = 1,
    Moderate = 2,
    Severe = 3,
    Shock = 4,
}

/// <summary>
///     Boundary table for <see cref="PainTier"/> with downward hysteresis.
/// </summary>
public static class PainTierThresholds
{
    /// <summary>
    ///     Default downward-cross offset (raw pain units). Matches the
    ///     <c>cmu.medical.pain.tier_hysteresis</c> CCVar default.
    /// </summary>
    public const float DefaultHysteresis = 3f;

    public static readonly FixedPoint2 MildThreshold = (FixedPoint2)15;
    public static readonly FixedPoint2 ModerateThreshold = (FixedPoint2)35;
    public static readonly FixedPoint2 SevereThreshold = (FixedPoint2)60;
    public static readonly FixedPoint2 ShockThreshold = (FixedPoint2)85;

    /// <summary>
    ///     Resolve the marine's new <see cref="PainTier"/> given their current
    ///     tier and current raw pain, applying downward hysteresis: the marine
    ///     stays at <paramref name="currentTier"/> until pain falls below the
    ///     boundary minus <paramref name="hysteresis"/>. Upward transitions
    ///     trigger immediately on the boundary.
    /// </summary>
    public static PainTier Get(PainTier currentTier, FixedPoint2 pain, float hysteresis = DefaultHysteresis)
        => Get(currentTier, pain, hysteresis, ShockThreshold);

    public static PainTier Get(
        PainTier currentTier,
        FixedPoint2 pain,
        float hysteresis,
        FixedPoint2 shockThreshold)
    {
        var hyst = (FixedPoint2)hysteresis;
        var upTier = PainTier.None;
        for (var i = (int)PainTier.Mild; i <= (int)PainTier.Shock; i++)
        {
            var tier = (PainTier)i;
            if (pain >= GetUpwardThreshold(tier, shockThreshold))
                upTier = tier;
            else
                break;
        }

        if (upTier > currentTier)
            return upTier;

        if (currentTier > PainTier.None)
        {
            var downBoundary = GetUpwardThreshold(currentTier, shockThreshold) - hyst;
            if (pain >= downBoundary)
                return currentTier;
        }

        return upTier;
    }

    public static FixedPoint2 GetUpwardThreshold(PainTier tier)
        => GetUpwardThreshold(tier, ShockThreshold);

    public static FixedPoint2 GetUpwardThreshold(PainTier tier, FixedPoint2 shockThreshold) => tier switch
    {
        PainTier.Mild => MildThreshold,
        PainTier.Moderate => ModerateThreshold,
        PainTier.Severe => SevereThreshold,
        PainTier.Shock => shockThreshold,
        _ => FixedPoint2.Zero,
    };
}
