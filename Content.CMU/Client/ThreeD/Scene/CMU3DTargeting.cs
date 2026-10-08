using System.Numerics;

namespace Content.Client.CMU14.ThreeD.Scene;

public static class CMU3DTargeting
{
    public static bool IntersectBillboard(Vector3 origin, Vector3 ray, Vector3 center, Vector2 size,
        Vector3 right, float occlusionDistance, out float distance, out Vector2 local)
    {
        distance = 0;
        local = default;
        var normal = new Vector3(-right.Y, right.X, 0);
        var denominator = Vector3.Dot(ray, normal);
        if (MathF.Abs(denominator) < .00001f) return false;
        distance = Vector3.Dot(center - origin, normal) / denominator;
        if (!float.IsFinite(distance) || distance < CMU3DCamera.NearPlane || distance >= occlusionDistance) return false;
        var delta = origin + ray * distance - center;
        local = new Vector2(Vector3.Dot(delta, right), delta.Z);
        return MathF.Abs(local.X) <= size.X / 2 && MathF.Abs(local.Y) <= size.Y / 2;
    }
}
