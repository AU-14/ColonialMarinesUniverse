using Content.Shared.FixedPoint;

namespace Content.Shared._CMU14.Medical.StatusEffects;

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

    public static readonly FixedPoint2[] UpwardThresholds =
    {
        (FixedPoint2)15,
        (FixedPoint2)35,
        (FixedPoint2)60,
        (FixedPoint2)85,
    };

    /// <summary>
    ///     Resolve the marine's new <see cref="PainTier"/> given their current
    ///     tier and current raw pain, applying downward hysteresis: the marine
    ///     stays at <paramref name="currentTier"/> until pain falls below the
    ///     boundary minus <paramref name="hysteresis"/>. Upward transitions
    ///     trigger immediately on the boundary.
    /// </summary>
    public static PainTier Get(PainTier currentTier, FixedPoint2 pain, float hysteresis = DefaultHysteresis)
    {
        var hyst = (FixedPoint2)hysteresis;

        var upTier = PainTier.None;
        for (var i = 0; i < UpwardThresholds.Length; i++)
        {
            if (pain >= UpwardThresholds[i])
                upTier = (PainTier)(i + 1);
            else
                break;
        }

        if (upTier > currentTier)
            return upTier;

        if (currentTier > PainTier.None)
        {
            var downBoundary = UpwardThresholds[(int)currentTier - 1] - hyst;
            if (pain >= downBoundary)
                return currentTier;
        }

        return upTier;
    }
}
