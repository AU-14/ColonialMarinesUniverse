using System.Numerics;

namespace Content.Client.CMU14.ThreeD;

/// <summary>Uses the same north/forward and east/right basis as the existing mover.</summary>
public static class CMU3DFirstPersonCamera
{
    public const float EyeHeight = 1.65f;
    public const float PitchLimit = 1.35f;

    public static CMU3DCameraFrame Frame(Vector2 position, float movementRotation, float pitch, Vector2 viewport,
        float verticalFovDegrees = CMU3DViewSettings.DefaultFov, float groundHeight = 0)
    {
        pitch = Math.Clamp(pitch, -PitchLimit, PitchLimit);
        var horizontal = new Vector3(-MathF.Sin(movementRotation), MathF.Cos(movementRotation), 0);
        var right = new Vector3(MathF.Cos(movementRotation), MathF.Sin(movementRotation), 0);
        var forward = horizontal * MathF.Cos(pitch) + Vector3.UnitZ * MathF.Sin(pitch);
        return new CMU3DCameraFrame(new Vector3(position, EyeHeight + groundHeight), forward, right,
            Vector3.Cross(right, forward),
            Math.Max(1, viewport.Y) * 0.5f / MathF.Tan(CMU3DViewSettings.Clamp(verticalFovDegrees, 40, 110, 75) * MathF.PI / 360),
            viewport * 0.5f);
    }
}
