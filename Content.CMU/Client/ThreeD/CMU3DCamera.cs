using System.Numerics;

namespace Content.Client.CMU14.ThreeD;

/// <summary>
/// Z-up perspective orbit camera. The model bounding sphere stays beyond the near plane at every
/// zoom and orbit angle. Screen positions and picking rays use the same basis and pixel units.
/// </summary>
public sealed class CMU3DCamera
{
    public const float NearPlane = 0.01f;
    public const float VerticalFieldOfView = MathF.PI / 4;

    public Vector3 Target { get; private set; }
    public float Yaw { get; private set; } = -MathF.PI / 2;
    public float Elevation { get; private set; } = MathF.PI / 6;
    public float Distance { get; private set; } = 4;
    public float Radius { get; private set; } = 1;
    private float _fitDistance = 4;

    public void Fit(Vector3 min, Vector3 max, float aspect)
    {
        Target = (min + max) * 0.5f;
        Radius = Math.Max(0.05f, (max - min).Length() * 0.5f);
        var halfAngle = MathF.Atan(MathF.Tan(VerticalFieldOfView / 2) * Math.Min(1, Math.Max(0.05f, aspect)));
        _fitDistance = Radius / MathF.Sin(halfAngle) * 1.12f;
        Distance = _fitDistance;
    }

    public void SetAngles(float yaw, float elevation)
    {
        Yaw = yaw % (MathF.PI * 2);
        Elevation = Math.Clamp(elevation, -MathF.PI / 2, MathF.PI / 2);
    }

    public void Orbit(Vector2 delta) => SetAngles(Yaw - delta.X * 0.009f, Elevation + delta.Y * 0.007f);

    public void Zoom(float steps)
    {
        Distance = Math.Clamp(Distance * MathF.Pow(0.82f, steps), Radius + NearPlane * 4, _fitDistance * 8);
    }

    public CMU3DCameraFrame Frame(Vector2 viewport)
    {
        var direction = new Vector3(MathF.Cos(Yaw) * MathF.Cos(Elevation),
            MathF.Sin(Yaw) * MathF.Cos(Elevation), MathF.Sin(Elevation));
        var forward = -direction;
        var right = new Vector3(-MathF.Sin(Yaw), MathF.Cos(Yaw), 0);
        var up = Vector3.Cross(right, forward);
        return new CMU3DCameraFrame(Target + direction * Distance, forward, right, up,
            Math.Max(1, viewport.Y) * 0.5f / MathF.Tan(VerticalFieldOfView * 0.5f), viewport * 0.5f);
    }
}

public readonly record struct CMU3DCameraFrame(
    Vector3 Origin, Vector3 Forward, Vector3 Right, Vector3 Up, float FocalPixels, Vector2 ScreenCenter)
{
    public float Depth(Vector3 world) => Vector3.Dot(world - Origin, Forward);

    /// <summary>Conservative perspective-frustum test, including spheres crossing the near plane.</summary>
    public bool IntersectsSphere(Vector3 center, float radius)
    {
        var relative = center - Origin;
        var depth = Vector3.Dot(relative, Forward);
        if (depth + radius < CMU3DCamera.NearPlane) return false;
        var horizontal = ScreenCenter.X / FocalPixels;
        var vertical = ScreenCenter.Y / FocalPixels;
        return MathF.Abs(Vector3.Dot(relative, Right)) <= depth * horizontal + radius * MathF.Sqrt(1 + horizontal * horizontal) &&
            MathF.Abs(Vector3.Dot(relative, Up)) <= depth * vertical + radius * MathF.Sqrt(1 + vertical * vertical);
    }

    public bool TryProject(Vector3 world, out Vector2 pixel)
    {
        pixel = default;
        var relative = world - Origin;
        var depth = Vector3.Dot(relative, Forward);
        if (!float.IsFinite(depth) || depth <= CMU3DCamera.NearPlane)
            return false;
        pixel = ScreenCenter + new Vector2(Vector3.Dot(relative, Right), -Vector3.Dot(relative, Up)) * (FocalPixels / depth);
        return float.IsFinite(pixel.X) && float.IsFinite(pixel.Y);
    }

    public Vector3 RayDirection(Vector2 pixel)
    {
        var relative = (pixel - ScreenCenter) / FocalPixels;
        return Vector3.Normalize(Forward + Right * relative.X - Up * relative.Y);
    }
}
