using System.Numerics;

namespace Content.Shared.CMU14.Automation;

/// <summary>
/// CMU14 (ECS-04): the cosmetic reach of a tray arm, using the inserter's two links (see <see cref="CMUInserterAnimation"/>).
/// Everything here is in the arm's work frame: tiles, Y up, the shoulder at the origin and +X toward the side the arm works.
/// The client mirrors the work frame onto the screen for an arm whose work side is drawn on the left.
/// </summary>
public static class CMUTrayArmAnimation
{
    /// <summary>The reach out ends here (as a fraction of the reach duration); the claw then holds at the tray.</summary>
    public const float OutEnd = 0.4f;

    /// <summary>The claw leaves the tray here and returns to rest by the end.</summary>
    public const float ReturnStart = 0.6f;

    /// <summary>Beyond this fraction of the links' natural reach, the forearm telescopes instead of straightening further.</summary>
    public const float NaturalReachFraction = 0.95f;

    /// <summary>Where the claw rests: folded, pointing toward the work side, elbow up.</summary>
    public static readonly Vector2 RestClaw = new(0.7f, 0.15f);

    /// <summary>The upper arm, shoulder to elbow, in the West frame's natural pose (tiles).</summary>
    public static Vector2 Upper => CMUInserterAnimation.PixelOffset(CMUInserterAnimation.ElbowPixels, CMUInserterAnimation.ShoulderPixels);

    /// <summary>The forearm, elbow to claw, in the West frame's natural pose (tiles).</summary>
    public static Vector2 Forearm => CMUInserterAnimation.PixelOffset(CMUInserterAnimation.ClawPixels, CMUInserterAnimation.ElbowPixels);

    /// <summary>How far the links reach without telescoping.</summary>
    public static float NaturalReach => NaturalReachFraction * (Upper.Length() + Forearm.Length());

    /// <summary>
    /// Maps between the work frame and the drawn frame: a horizontal flip about the shoulder when the work side is drawn on
    /// the left (it is its own inverse).
    /// </summary>
    public static Vector2 Mirror(Vector2 vector, bool mirrored)
    {
        return mirrored ? new Vector2(-vector.X, vector.Y) : vector;
    }

    /// <summary>
    /// How far along the path from rest to the tray the claw is: 0 at rest, 1 at the tray. Eased out over
    /// [0, <see cref="OutEnd"/>), held until <see cref="ReturnStart"/>, eased back by 1. Anything outside [0, 1) is rest.
    /// </summary>
    public static float GetExtension(float progress)
    {
        if (!(progress > 0f) || progress >= 1f)
            return 0f;
        if (progress < OutEnd)
            return Smooth(progress / OutEnd);
        if (progress <= ReturnStart)
            return 1f;
        return 1f - Smooth((progress - ReturnStart) / (1f - ReturnStart));
    }

    /// <summary>Smoothstep, as <see cref="CMUInserterAnimation.GetPose"/> eases its swing.</summary>
    private static float Smooth(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    /// <summary>
    /// Whether the claw shows what it carries: on the way out and at the tray when it carries it out (planting), at the
    /// tray and on the way back when it brings it back (harvesting).
    /// </summary>
    public static bool ShowsCarried(float progress, bool carriesOut)
    {
        if (!(progress >= 0f) || progress >= 1f)
            return false;
        return carriesOut ? progress < ReturnStart : progress >= OutEnd;
    }

    /// <summary>The pose at <paramref name="progress"/> through a reach toward <paramref name="target"/> (work frame, tiles).</summary>
    public static CMUTrayArmPose GetPose(Vector2 target, float progress)
    {
        return Solve(Vector2.Lerp(RestClaw, target, GetExtension(progress)));
    }

    /// <summary>
    /// Two-link inverse kinematics putting the claw on <paramref name="claw"/>, elbow up (counter-clockwise of the line
    /// from the shoulder to the claw). Past <see cref="NaturalReach"/> the forearm telescopes by the extra distance, so
    /// the claw still ends on the target; closer than the links can fold, the claw stops at the folded distance.
    /// </summary>
    public static CMUTrayArmPose Solve(Vector2 claw)
    {
        var upper = Upper;
        var forearm = Forearm;
        var upperLength = upper.Length();
        var forearmLength = forearm.Length();

        var distance = claw.Length();
        var folded = MathF.Abs(forearmLength - upperLength) + 0.001f;
        if (!(distance >= folded))
        {
            claw = distance > 0.0001f ? claw / distance * folded : new Vector2(folded, 0f);
            distance = folded;
        }

        var telescope = MathF.Max(0f, distance - NaturalReach);
        var reach = forearmLength + telescope;
        var bend = MathF.Acos(Math.Clamp(
            (upperLength * upperLength + distance * distance - reach * reach) / (2f * upperLength * distance), -1f, 1f));
        var shoulder = new Angle(claw) + bend;
        var elbow = shoulder.RotateVec(Vector2.UnitX) * upperLength;
        var forearmAngle = new Angle(claw - elbow);
        var direction = forearmAngle.RotateVec(Vector2.UnitX);
        return new CMUTrayArmPose(
            shoulder - new Angle(upper),
            forearmAngle - new Angle(forearm),
            elbow,
            elbow + direction * telescope,
            telescope,
            elbow + direction * reach);
    }
}

/// <param name="Shoulder">The upper arm's rotation from its drawn (West frame) pose.</param>
/// <param name="Forearm">The forearm's rotation from its drawn (West frame) pose.</param>
/// <param name="Elbow">The elbow joint, relative to the shoulder.</param>
/// <param name="ForearmPivot">Where the forearm part's elbow pivot is drawn: the elbow, moved out by <paramref name="Telescope"/>.</param>
/// <param name="Telescope">How far the forearm is pushed out along itself (tiles); the rod spans this gap.</param>
/// <param name="Claw">Where the claw grips, relative to the shoulder.</param>
public readonly record struct CMUTrayArmPose(Angle Shoulder, Angle Forearm, Vector2 Elbow, Vector2 ForearmPivot, float Telescope, Vector2 Claw);
