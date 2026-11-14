using System.Numerics;

namespace Content.Shared.CMU14.Automation;

/// <summary>Folds the original two-link arm between the adjacent input and output tiles.</summary>
public static class CMUInserterAnimation
{
    // Joint centers in factory_roboticarm2's west-facing frame, measured in source pixels.
    public static readonly Vector2 ShoulderPixels = new(11.5f, 44.5f);
    public static readonly Vector2 ElbowPixels = new(24.5f, 33.5f);
    public static readonly Vector2 ClawPixels = new(49f, 33.5f);

    public static Vector2 PixelOffset(Vector2 pixel, Vector2 pivot) =>
        new Vector2(pixel.X - pivot.X, pivot.Y - pixel.Y) / 32f;

    public static CMUInserterPose GetPose(CMUInserterPhase phase, float progress)
    {
        progress = Math.Clamp(progress, 0f, 1f);
        var eased = progress * progress * (3f - 2f * progress);
        var turn = phase switch
        {
            CMUInserterPhase.Swinging => eased,
            CMUInserterPhase.Waiting => 1f,
            CMUInserterPhase.Returning => 1f - eased,
            _ => 0f,
        };
        var upper = PixelOffset(ElbowPixels, ShoulderPixels);
        var forearm = PixelOffset(ClawPixels, ElbowPixels);
        var upperLength = upper.Length();
        var forearmLength = forearm.Length();
        var across = 2f * turn - 1f;
        // At the middle the links fold back against each other. Changing the elbow's bend there
        // keeps both joints continuous and carries the item close to the base, rather than in a semicircle.
        var grip = new Vector2(across, -(forearmLength - upperLength) * (1f - across * across));
        var distance = grip.Length();
        var bend = MathF.Acos(Math.Clamp(
            (upperLength * upperLength + distance * distance - forearmLength * forearmLength)
            / (2f * upperLength * distance), -1f, 1f));
        var shoulder = new Angle(grip) + (turn < 0.5f ? -bend : bend);
        var elbow = shoulder.RotateVec(Vector2.UnitX) * upperLength;
        var direction = Angle.FromDegrees(-90);
        return new CMUInserterPose(
            direction + shoulder - new Angle(upper),
            direction + new Angle(grip - elbow) - new Angle(forearm),
            direction.RotateVec(elbow),
            direction.RotateVec(grip));
    }
}

public readonly record struct CMUInserterPose(Angle Shoulder, Angle Forearm, Vector2 Elbow, Vector2 Item);
