using System.Numerics;

namespace Content.Client.CMU14.ThreeD.Scene;

/// <summary>Exact leaf picking for the same open spray used by the export and GPU renderer.</summary>
internal static partial class CMU3DFoliage
{
    public static bool Intersect(Vector3 origin, Vector3 ray, out float distance)
    {
        distance = float.MaxValue;
        foreach (var leaf in Leaves)
        {
            var relative = origin - leaf.Center;
            var o = new Vector3(Vector3.Dot(relative, leaf.X), Vector3.Dot(relative, leaf.Y), Vector3.Dot(relative, leaf.Z));
            var d = new Vector3(Vector3.Dot(ray, leaf.X), Vector3.Dot(ray, leaf.Y), Vector3.Dot(ray, leaf.Z));
            var a = Vector3.Dot(d, d);
            var b = Vector3.Dot(o, d);
            var discriminant = b * b - a * (Vector3.Dot(o, o) - 1);
            if (a <= 0 || discriminant < 0)
                continue;

            var root = MathF.Sqrt(discriminant);
            var near = (-b - root) / a;
            var candidate = near >= .0001f ? near : (-b + root) / a;
            if (candidate >= .0001f && candidate < distance)
                distance = candidate;
        }

        return distance < float.MaxValue;
    }
}
