using System.Numerics;

namespace Content.Shared.CMU14.TacticalMap.Reconstruction;

/// <summary>Continuous map annotations, independent of terrain clearance.</summary>
public static class CMUReconRoutes
{
    public const int MaxPoints = 512;
    public const int MaxAnnotations = 32;
    public const int MaxTextLength = 120;

    public static bool ValidAnnotation(CMUReconAnnotation annotation, Vector2i origin, int width, int height, int minDepth, int levels)
    {
        if ((long) annotation.Depth - minDepth < 0 || (long) annotation.Depth - minDepth >= levels ||
            !float.IsFinite(annotation.Width) || annotation.Width < 1 || annotation.Width > 8 ||
            !ValidStroke(annotation.Points, origin, width, height, annotation.Ink)) return false;
        if (annotation.Text == null) return true;
        if (annotation.Points.Length != 1 || string.IsNullOrWhiteSpace(annotation.Text) || annotation.Text.Length > MaxTextLength) return false;
        foreach (var character in annotation.Text)
            if (char.IsControl(character)) return false;
        return true;
    }

    public static bool ValidStroke(IReadOnlyList<Vector2>? points, Vector2i origin, int width, int height, CMUReconInk ink)
    {
        if (points == null || points.Count is < 1 or > MaxPoints || ink > CMUReconInk.Purple)
            return false;
        foreach (var point in points)
        {
            var local = point - (Vector2) origin;
            if (!float.IsFinite(local.X) || !float.IsFinite(local.Y) ||
                local.X < 0 || local.Y < 0 || local.X > width || local.Y > height)
                return false;
        }
        return true;
    }

    /// <summary>Keep drawing when the sample buffer fills, retaining the full stroke at lower resolution.</summary>
    public static void AddPoint(List<Vector2> points, Vector2 point)
    {
        if (points.Count > 0 && Vector2.DistanceSquared(points[^1], point) < 0.0001f)
            return;
        if (points.Count == MaxPoints)
        {
            for (var i = 1; i < MaxPoints / 2; i++) points[i] = points[i * 2];
            points.RemoveRange(MaxPoints / 2, MaxPoints / 2);
        }
        points.Add(point);
    }
}
