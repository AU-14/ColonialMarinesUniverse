using System.Numerics;

namespace Content.Shared.CMU14.Weapons.ATGM;

/// <summary>
///     Pure steering math for wire-guided missiles, kept free of entity state so it can be unit tested.
///     All angles are radians in world vector space.
/// </summary>
public static class CMUGuidedMissileMath
{
    private const double FullTurn = Math.PI * 2;

    /// <summary>
    ///     Wraps an angle into [-pi, pi].
    /// </summary>
    public static double Wrap(double radians)
    {
        return radians - FullTurn * Math.Round(radians / FullTurn);
    }

    /// <summary>
    ///     Turns <paramref name="current"/> toward <paramref name="desired"/> by at most
    ///     <paramref name="maxTurn"/>, keeping the result within <paramref name="maxOffBoresight"/> of
    ///     <paramref name="launch"/>.
    /// </summary>
    /// <remarks>
    ///     The desired heading is clamped into the allowed cone before choosing a turn direction, so a
    ///     target behind the missile swings it across the cone instead of pinning it to the near edge.
    ///     A cone of pi or more is unlimited.
    /// </remarks>
    public static double StepHeading(double current, double desired, double launch, double maxTurn, double maxOffBoresight)
    {
        if (!double.IsFinite(current) || !double.IsFinite(launch))
            return launch;

        if (!double.IsFinite(desired))
            return Wrap(current);

        maxTurn = Math.Max(0, maxTurn);
        if (maxOffBoresight >= Math.PI)
        {
            var delta = Wrap(desired - current);
            return Wrap(current + Math.Clamp(delta, -maxTurn, maxTurn));
        }

        maxOffBoresight = Math.Max(0, maxOffBoresight);
        var currentOff = Math.Clamp(Wrap(current - launch), -maxOffBoresight, maxOffBoresight);
        var desiredOff = Math.Clamp(Wrap(desired - launch), -maxOffBoresight, maxOffBoresight);
        var nextOff = currentOff + Math.Clamp(desiredOff - currentOff, -maxTurn, maxTurn);
        return Wrap(launch + nextOff);
    }

    /// <summary>
    ///     Shortens <paramref name="vector"/> to at most <paramref name="maxLength"/>. Non-finite vectors and
    ///     non-positive limits give zero.
    /// </summary>
    public static Vector2 ClampLength(Vector2 vector, float maxLength)
    {
        if (!(maxLength > 0) || !float.IsFinite(vector.X) || !float.IsFinite(vector.Y))
            return Vector2.Zero;

        var length = vector.Length();
        return length > maxLength ? vector * (maxLength / length) : vector;
    }

    /// <summary>
    ///     The point a locked-on missile steers toward. It homes on <paramref name="target"/>, and the spotter's
    ///     <paramref name="cursor"/> may pull the aim point away from it to bend the flight path around cover.
    /// </summary>
    /// <remarks>
    ///     The pull is limited to <c>R = min(maxSteerOffset, steerPerDistance * |missile - target|)</c>, so the
    ///     spotter has most authority far from the target and none at impact. Inside
    ///     <paramref name="terminalDistance"/> the pull is gone entirely, fading in linearly over the following
    ///     <paramref name="fadeDistance"/>, so the missile always converges on the target.
    /// </remarks>
    /// <param name="missile">The missile's world position.</param>
    /// <param name="target">The (lead-adjusted) target world position.</param>
    /// <param name="cursor">The spotter's latest fresh cursor, or null when there is none or it is stale.</param>
    public static Vector2 ComputeAimPoint(
        Vector2 missile,
        Vector2 target,
        Vector2? cursor,
        float maxSteerOffset,
        float steerPerDistance,
        float terminalDistance,
        float fadeDistance)
    {
        if (cursor is not { } c || !float.IsFinite(c.X) || !float.IsFinite(c.Y))
            return target;

        var distance = Vector2.Distance(missile, target);
        if (!float.IsFinite(distance))
            return target;

        var radius = MathF.Min(maxSteerOffset, steerPerDistance * distance);
        float fade;
        if (fadeDistance > 0)
            fade = Math.Clamp((distance - terminalDistance) / fadeDistance, 0f, 1f);
        else
            fade = distance > terminalDistance ? 1f : 0f;

        return target + ClampLength(c - target, radius) * fade;
    }

    /// <summary>
    ///     Simple lead for a moving target: where it will be after the missile's time to go, capped at
    ///     <paramref name="maxLead"/> tiles so a jittery velocity estimate cannot throw the missile off.
    /// </summary>
    public static Vector2 ComputeLead(Vector2 targetVelocity, float distance, float missileSpeed, float maxLead)
    {
        if (!(missileSpeed > 0) || !float.IsFinite(distance) || distance <= 0)
            return Vector2.Zero;

        return ClampLength(targetVelocity * (distance / missileSpeed), maxLead);
    }
}
