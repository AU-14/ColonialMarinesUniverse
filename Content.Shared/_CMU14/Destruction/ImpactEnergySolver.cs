using System;
using System.Numerics;
using Robust.Shared.Maths;

namespace Content.Shared._CMU14.Destruction;

/// <summary>
/// Shared squared-speed energy accounting for vehicle and dropship impacts.
/// </summary>
public static class ImpactEnergySolver
{
    public readonly record struct BatchAllocation(
        bool CanClearAll,
        float AppliedFraction,
        float RemainingSpeed);

    public static float GetRemainingSpeed(float availableSpeed, float requiredSpeed)
    {
        var available = MathF.Abs(availableSpeed);
        var required = MathF.Max(0f, requiredSpeed);
        return MathF.Sqrt(MathF.Max(0f, available * available - required * required));
    }

    /// <summary>
    /// Returns a stable geometric key for resolving swept contacts from the
    /// start of a motion toward its target. Stationary probes use radial
    /// distance so rotation-only checks still resolve the nearest contact first.
    /// </summary>
    public static float GetContactOrder(Vector2 start, Vector2 target, Vector2 contact)
    {
        var delta = target - start;
        if (delta == Vector2.Zero)
            return Vector2.DistanceSquared(start, contact);

        return Vector2.Dot(contact - start, Vector2.Normalize(delta));
    }

    /// <summary>
    /// Computes normalized entry time for a moving axis-aligned proxy against
    /// a static obstacle. A value in [0, 1] is a contact on this sweep;
    /// <see cref="float.PositiveInfinity"/> means the sweep does not contact it.
    /// </summary>
    public static float GetSweptAabbContactTime(
        Vector2 start,
        Vector2 target,
        Vector2 movingHalfExtents,
        Box2 obstacle)
    {
        var extents = Vector2.Abs(movingHalfExtents);
        var expanded = new Box2(
            obstacle.Left - extents.X,
            obstacle.Bottom - extents.Y,
            obstacle.Right + extents.X,
            obstacle.Top + extents.Y);
        var delta = target - start;
        var enter = 0f;
        var exit = 1f;

        if (!ClipSweepAxis(start.X, delta.X, expanded.Left, expanded.Right, ref enter, ref exit) ||
            !ClipSweepAxis(start.Y, delta.Y, expanded.Bottom, expanded.Top, ref enter, ref exit))
        {
            return float.PositiveInfinity;
        }

        return Math.Clamp(enter, 0f, 1f);
    }

    private static bool ClipSweepAxis(
        float start,
        float delta,
        float minimum,
        float maximum,
        ref float enter,
        ref float exit)
    {
        if (MathF.Abs(delta) <= 0.000001f)
            return start >= minimum && start <= maximum;

        var first = (minimum - start) / delta;
        var second = (maximum - start) / delta;
        if (first > second)
            (first, second) = (second, first);

        enter = MathF.Max(enter, first);
        exit = MathF.Min(exit, second);
        return enter <= exit && exit >= 0f && enter <= 1f;
    }

    /// <summary>
    /// Allocates one impact's energy across simultaneous contacts. When there
    /// is not enough energy to clear the whole batch, every contact receives
    /// the same proportion of its required destruction energy.
    /// </summary>
    public static BatchAllocation AllocateBatch(float availableSpeed, ReadOnlySpan<float> requiredSpeeds)
    {
        var availableEnergy = MathF.Abs(availableSpeed);
        availableEnergy *= availableEnergy;

        var requiredEnergy = 0f;
        foreach (var requiredSpeed in requiredSpeeds)
        {
            var required = MathF.Max(0f, requiredSpeed);
            requiredEnergy += required * required;
        }

        if (requiredEnergy <= 0f)
            return new BatchAllocation(true, 1f, MathF.Sqrt(availableEnergy));

        var appliedFraction = Math.Clamp(availableEnergy / requiredEnergy, 0f, 1f);
        var canClearAll = appliedFraction >= 1f;
        var remainingEnergy = canClearAll ? MathF.Max(0f, availableEnergy - requiredEnergy) : 0f;
        return new BatchAllocation(canClearAll, appliedFraction, MathF.Sqrt(remainingEnergy));
    }
}
