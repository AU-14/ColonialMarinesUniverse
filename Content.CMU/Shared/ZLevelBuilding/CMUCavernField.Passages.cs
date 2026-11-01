using System.Numerics;
using Robust.Shared.Maths;

namespace Content.Shared.CMU14.ZLevelBuilding;

public enum CMUCavernPassageKind : byte
{
    Gallery,
    Crawl,
    Meander,
    Switchback,
    Chasm,
    Keyhole,
    Split,
    Shaft,
}

public sealed record CMUCavernPassage(
    CMUCavernPassageKind Kind,
    IReadOnlyList<Vector3> Points,
    IReadOnlyList<float> Widths,
    float Height,
    Vector2i? Destination,
    bool Blind);

public sealed partial class CMUCavernField
{
    private CMUCavernPassage MakePassage(Vector2i region, int top, int salt,
        Vector3 a, Vector3 b, float scale, Vector2i? destination = null, bool blind = false)
    {
        var kind = (CMUCavernPassageKind) (Hash(region, top, salt) % 8);
        if (blind && kind == CMUCavernPassageKind.Split)
            kind = CMUCavernPassageKind.Meander;
        var delta = b - a;
        var normal = new Vector3(-delta.Y, delta.X, 0);
        normal = Vector3.Normalize(normal);
        var bend = Math.Min(new Vector2(delta.X, delta.Y).Length() * 0.25f, 19 * scale);
        if ((Hash(region, top, salt + 1) & 1) != 0)
            normal = -normal;
        var width = (3.4f + Sample(region, top, salt + 2) * 3.4f) * scale;
        var height = 1.1f + Sample(region, top, salt + 3) * 0.5f;
        var points = new List<Vector3> { a };
        Vector3 At(float t, float offset = 0, float z = 0) => Vector3.Lerp(a, b, t) + normal * offset + new Vector3(0, 0, z);
        switch (kind)
        {
            case CMUCavernPassageKind.Crawl:
                width = Math.Max(1.6f, 2.2f * scale);
                height = 0.95f;
                points.Add(At(0.5f, bend * 0.3f));
                break;
            case CMUCavernPassageKind.Meander:
                for (var i = 1; i < 6; i++)
                    points.Add(At(i / 6f, MathF.Sin(i * MathF.PI / 3) * bend));
                break;
            case CMUCavernPassageKind.Switchback:
                points.Add(At(0.25f, bend));
                points.Add(At(0.5f, bend));
                points.Add(At(0.5f, -bend));
                points.Add(At(0.75f, -bend));
                break;
            case CMUCavernPassageKind.Chasm:
                width = Math.Max(1.8f, 2.8f * scale);
                height = 2.5f;
                points.Add(At(0.4f, bend * 0.6f));
                break;
            case CMUCavernPassageKind.Keyhole:
                points.Add(At(0.35f, bend * 0.4f));
                points.Add(At(0.65f, bend * 0.4f));
                break;
            case CMUCavernPassageKind.Split:
                points.Add(At(0.5f, bend));
                points.Add(b);
                points.Add(At(0.5f, -bend));
                points.Add(a);
                break;
            case CMUCavernPassageKind.Shaft:
                points.Add(At(0.45f, bend * 0.4f, 0.75f));
                points.Add(At(0.45f, bend * 0.4f, -1.2f));
                height = 1;
                break;
        }
        if (kind != CMUCavernPassageKind.Split)
            points.Add(b);
        TryGetBand(top, out _, out var bottom);
        // A short configured band must not bury the lower bend of a shaft in its solid separator.
        for (var i = 0; i < points.Count; i++)
            points[i] = new Vector3(points[i].X, points[i].Y, Math.Clamp(points[i].Z, bottom + 0.75f, top - 0.25f));
        var widths = new float[points.Count - 1];
        for (var i = 0; i < widths.Length; i++)
            widths[i] = kind == CMUCavernPassageKind.Keyhole && i == 1 ? Math.Max(1.6f, 1.9f * scale) : width;
        return new CMUCavernPassage(kind, points.AsReadOnly(), Array.AsReadOnly(widths), height, destination, blind);
    }

    private static bool InPassage(Vector3 point, CMUCavernPassage passage)
    {
        for (var i = 1; i < passage.Points.Count; i++)
        {
            var a = passage.Points[i - 1];
            var b = passage.Points[i];
            var width = passage.Widths[i - 1];
            if (point.X < Math.Min(a.X, b.X) - width || point.X > Math.Max(a.X, b.X) + width ||
                point.Y < Math.Min(a.Y, b.Y) - width || point.Y > Math.Max(a.Y, b.Y) + width ||
                point.Z < Math.Min(a.Z, b.Z) - passage.Height || point.Z > Math.Max(a.Z, b.Z) + passage.Height)
                continue;
            if (Gallery(point, a, b, width, passage.Height))
                return true;
        }
        return false;
    }
}
