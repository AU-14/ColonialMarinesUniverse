using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.ZLevelBuilding;

/// <summary>Ten geometric arrangements for each of the eight geological families: eighty recipes.</summary>
[Serializable, NetSerializable]
public enum CMUCavernLayout : byte
{
    Vault,
    Twin,
    Crescent,
    Ring,
    Clover,
    Terrace,
    Rift,
    Honeycomb,
    Spiral,
    BrokenGallery,
}

public readonly record struct CMUCavernChamber(
    Vector3 Center,
    Vector2 Scale,
    float Cos,
    float Sin,
    float Height,
    float Variation,
    CMUCavernVariant Variant,
    CMUCavernLayout Layout);

public sealed partial class CMUCavernField
{
    private static bool InChamber(Vector3 point, CMUCavernChamber chamber, float roughness)
    {
        var delta = point - chamber.Center;
        if (MathF.Abs(delta.Z) > chamber.Height + 1)
            return false;
        var p = new Vector3(
            (delta.X * chamber.Cos + delta.Y * chamber.Sin) / chamber.Scale.X,
            (-delta.X * chamber.Sin + delta.Y * chamber.Cos) / chamber.Scale.Y,
            delta.Z);
        if (MathF.Abs(p.X) > 54 || MathF.Abs(p.Y) > 54)
            return false;

        var family = chamber.Variant;
        var h = chamber.Height;
        var v = chamber.Variation;
        bool Kernel(Vector3 center, float sx, float sy, float height = 1) =>
            InRegion(new Vector3((p.X - center.X) / sx, (p.Y - center.Y) / sy, p.Z - center.Z),
                family, h * height, roughness, v);
        bool Link(Vector3 a, Vector3 b, float width = 4) => Gallery(p, a, b, width, Math.Min(1.3f, h));

        // A small vestibule anchors passage endpoints even in annular or strongly asymmetric recipes.
        if (Ellipsoid(p, 4, 4, Math.Min(h, 1.3f)))
            return true;

        switch (chamber.Layout)
        {
            case CMUCavernLayout.Vault:
                return Kernel(Vector3.Zero, 1, 1);
            case CMUCavernLayout.Twin:
                return Kernel(new(-18, -6, -0.2f), 0.55f, 0.8f) ||
                       Kernel(new(18, 6, 0.2f), 0.55f, 0.8f) || Link(new(-18, -6, 0), new(18, 6, 0));
            case CMUCavernLayout.Crescent:
                return Kernel(Vector3.Zero, 1, 1) &&
                       !Ellipsoid(p - new Vector3(8, 14, 0), 29, 24, h * 3) ||
                       Link(Vector3.Zero, new(0, -22, 0));
            case CMUCavernLayout.Ring:
                var radius = MathF.Sqrt(p.X * p.X + p.Y * p.Y);
                return Ellipsoid(new Vector3(radius - 27, 0, p.Z), 5, 1, Math.Min(h, 1.4f)) ||
                       Kernel(new(26, 0, 0), 0.4f, 0.55f) || Link(Vector3.Zero, new(27, 0, 0));
            case CMUCavernLayout.Clover:
                return Kernel(new(-22, -15, 0), 0.5f, 0.5f) ||
                       Kernel(new(22, -15, 0), 0.5f, 0.5f) || Kernel(new(0, 24, 0.3f), 0.55f, 0.5f) ||
                       Link(new(-22, -15, 0), Vector3.Zero) || Link(new(22, -15, 0), Vector3.Zero) ||
                       Link(Vector3.Zero, new(0, 24, 0.3f));
            case CMUCavernLayout.Terrace:
                return Kernel(new(-24, -12, 0.7f), 0.48f, 0.55f, 0.65f) ||
                       Kernel(Vector3.Zero, 0.45f, 0.55f, 0.7f) ||
                       Kernel(new(24, 12, -0.7f), 0.48f, 0.55f, 0.65f) ||
                       Link(new(-24, -12, 0.7f), Vector3.Zero) || Link(Vector3.Zero, new(24, 12, -0.7f));
            case CMUCavernLayout.Rift:
                return Kernel(Vector3.Zero, 0.36f, 1.2f) ||
                       Ellipsoid(p, 4 + v * 2, 42, h) ||
                       Kernel(new(22, -16, -0.4f), 0.4f, 0.35f) || Link(Vector3.Zero, new(22, -16, -0.4f));
            case CMUCavernLayout.Honeycomb:
                for (var i = 0; i < 6; i++)
                {
                    var angle = i * MathF.PI / 3;
                    var a = new Vector3(MathF.Cos(angle) * 27, MathF.Sin(angle) * 27, (i % 2) * 0.4f);
                    if (Kernel(a, 0.3f, 0.3f, 0.7f) || Link(Vector3.Zero, a, 2.5f))
                        return true;
                }
                return false;
            case CMUCavernLayout.Spiral:
                var theta = MathF.Atan2(p.Y, p.X);
                if (theta < 0)
                    theta += MathF.Tau;
                var spiral = MathF.Sqrt(p.X * p.X + p.Y * p.Y) - (9 + theta * 4.5f);
                return Ellipsoid(new Vector3(spiral, 0, p.Z), 3.5f, 1, Math.Min(h, 1.4f)) ||
                       Kernel(new(37, -5, 0), 0.3f, 0.38f) || Link(Vector3.Zero, new(10, 0, 0), 3);
            case CMUCavernLayout.BrokenGallery:
                return Kernel(new(-28, -17, 0), 0.4f, 0.4f) ||
                       Kernel(new(25, 16, 0), 0.4f, 0.4f) ||
                       Link(new(-28, -17, 0), new(0, -17, 0), 3) ||
                       Link(new(0, -17, 0), new(0, 16, 0), 3) ||
                       Link(new(0, 16, 0), new(25, 16, 0), 3) ||
                       Link(new(0, 4, 0), new(-19, 4, 0), 2.5f);
            default:
                return false;
        }
    }
}
