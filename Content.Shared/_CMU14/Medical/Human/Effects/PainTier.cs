using Content.Shared.FixedPoint;

namespace Content.Shared._CMU14.Medical.Human.Effects;

public enum PainTier : byte
{
    None = 0,
    Mild = 1,
    Discomforting = 2,
    Moderate = 3,
    Distressing = 4,
    Severe = 5,
    Horrible = 6,
    Shock = Horrible,
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
        (FixedPoint2)20,
        (FixedPoint2)30,
        (FixedPoint2)40,
        (FixedPoint2)60,
        (FixedPoint2)70,
        (FixedPoint2)80,
    };

    /// <summary>
    ///     Resolve the marine's new <see cref="PainTier"/> given their current
    ///     tier and current raw pain, applying downward hysteresis: the marine
    ///     stays at <paramref name="currentTier"/> until pain falls below the
    ///     boundary minus <paramref name="hysteresis"/>. Upward transitions
    ///     trigger immediately on the boundary.
    /// </summary>
    public static PainTier Get(PainTier currentTier, FixedPoint2 pain, float hysteresis = DefaultHysteresis)
        => Get(currentTier, pain, hysteresis, UpwardThresholds[(int) PainTier.Horrible - 1]);

    public static PainTier Get(
        PainTier currentTier,
        FixedPoint2 pain,
        float hysteresis,
        FixedPoint2 shockThreshold)
    {
        var hyst = (FixedPoint2)hysteresis;
        var upwardThresholds = new[]
        {
            UpwardThresholds[0],
            UpwardThresholds[1],
            UpwardThresholds[2],
            UpwardThresholds[3],
            UpwardThresholds[4],
            shockThreshold,
        };

        var upTier = PainTier.None;
        for (var i = 0; i < upwardThresholds.Length; i++)
        {
            if (pain >= upwardThresholds[i])
                upTier = (PainTier)(i + 1);
            else
                break;
        }

        if (upTier > currentTier)
            return upTier;

        if (currentTier > PainTier.None)
        {
            var downBoundary = upwardThresholds[(int)currentTier - 1] - hyst;
            if (pain >= downBoundary)
                return currentTier;
        }

        return upTier;
    }
}
