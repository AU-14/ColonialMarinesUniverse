using System;

namespace Content.Shared._CMU14.Dropship.TacticalLand;

/// <summary>
/// Pure fixed-step and sweep-sampling policy for server-authoritative gunship flight.
/// </summary>
public static class GunshipFlightSimulation
{
    public const float StepSeconds = 1f / 30f;
    public const int MaxCatchUpSteps = 4;
    public const float MaximumSweepSpacing = 0.5f;

    public static int ConsumeSteps(ref float accumulator, float frameTime)
    {
        var elapsed = float.IsFinite(frameTime) ? MathF.Max(0f, frameTime) : 0f;
        var maximumAccumulatedTime = StepSeconds * MaxCatchUpSteps;
        accumulator = Math.Clamp(accumulator + elapsed, 0f, maximumAccumulatedTime);

        var steps = Math.Min(
            MaxCatchUpSteps,
            (int) MathF.Floor((accumulator + 0.000001f) / StepSeconds));
        accumulator = MathF.Max(0f, accumulator - steps * StepSeconds);
        return steps;
    }

    public static int GetLinearSweepSteps(float distance, float maximumSpacing = MaximumSweepSpacing)
    {
        return GetSweepSteps(MathF.Abs(distance), maximumSpacing);
    }

    public static int GetAngularSweepSteps(
        float angleRadians,
        float hullRadius,
        float maximumSpacing = MaximumSweepSpacing)
    {
        var cornerTravel = MathF.Abs(angleRadians) * MathF.Max(0f, hullRadius);
        return GetSweepSteps(cornerTravel, maximumSpacing);
    }

    /// <summary>
    /// Returns a conservative sample count for simultaneous translation and rotation.
    /// </summary>
    public static int GetCombinedSweepSteps(
        float linearDistance,
        float angleRadians,
        float hullRadius,
        float maximumSpacing = MaximumSweepSpacing)
    {
        var linearTravel = MathF.Abs(linearDistance);
        var cornerTravel = MathF.Abs(angleRadians) * MathF.Max(0f, hullRadius);
        return GetSweepSteps(linearTravel + cornerTravel, maximumSpacing);
    }

    private static int GetSweepSteps(float travel, float maximumSpacing)
    {
        if (!float.IsFinite(travel) || !float.IsFinite(maximumSpacing) || maximumSpacing <= 0f)
            return 1;

        return Math.Max(1, (int) MathF.Ceiling(travel / maximumSpacing));
    }
}
