using System.Numerics;
using Content.Shared.CMU14.ThreeD;

namespace Content.Client.CMU14.ThreeD.Scene;

/// <summary>Presentation-only support placement. Simulation transforms are never moved.</summary>
public static class CMU3DScenePlacement
{
    /// <summary>Fit at the actual supported height, then verify the corrected pivot still has that height.</summary>
    public static bool TryRearWallOffset(CMU3DModelPrototype model, EntityUid source, Vector2 position, float yaw,
        IReadOnlyList<CMU3DSceneSurface> surfaces, IReadOnlyList<CMU3DSceneRearWall> walls, out Vector3 offset)
    {
        offset = Vector3.Zero;
        var height = Offset(model, source, position, surfaces, yaw);
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var correction = Vector2.Zero;
            foreach (var wall in walls)
            {
                if (CMU3DSceneLayout.TryBackWallMountOffset(model.Parts, yaw, wall.Parts, wall.Yaw, wall.Delta,
                        out var candidate, heightOffset: height) && candidate.LengthSquared() > correction.LengthSquared())
                    correction = candidate;
            }
            var supportedHeight = Offset(model, source, position + correction, surfaces, yaw);
            if (MathF.Abs(supportedHeight - height) <= .00001f)
            {
                offset = new Vector3(correction, supportedHeight);
                return true;
            }
            height = supportedHeight;
        }
        // An oscillating support footprint must retain its original sprite, not an unsupported floating model.
        return false;
    }

    public static bool TrySurface(CMU3DModelPrototype model, EntityUid source, Vector2 position, float yaw,
        out CMU3DSceneSurface surface, IReadOnlyList<CMU3DModelPart>? resolvedParts = null)
    {
        surface = default;
        if (string.IsNullOrEmpty(model.SupportSurface) || model.SupportSurfaces.Length != 0)
            return false;
        return TryPartSurface(model.SupportSurface, resolvedParts ?? model.Parts, source, position, yaw, out surface);
    }

    public static void CollectSurfaces(CMU3DModelPrototype model, EntityUid source, Vector2 position, float yaw,
        List<CMU3DSceneSurface> surfaces, IReadOnlyList<CMU3DModelPart>? resolvedParts = null)
    {
        if (model.SupportSurfaces.Length == 0)
        {
            if (TrySurface(model, source, position, yaw, out var surface, resolvedParts))
                surfaces.Add(surface);
            return;
        }
        if (!string.IsNullOrEmpty(model.SupportSurface) || model.ConnectToNeighbours)
            return;
        var initialCount = surfaces.Count;
        var labels = new HashSet<string>();
        foreach (var label in model.SupportSurfaces)
        {
            if (string.IsNullOrEmpty(label) || !labels.Add(label) ||
                !TryPartSurface(label, resolvedParts ?? model.Parts, source, position, yaw, out var surface))
            {
                surfaces.RemoveRange(initialCount, surfaces.Count - initialCount);
                return;
            }
            surfaces.Add(surface);
        }
    }

    private static bool TryPartSurface(string label, IReadOnlyList<CMU3DModelPart> parts,
        EntityUid source, Vector2 position, float yaw, out CMU3DSceneSurface surface)
    {
        surface = default;
        CMU3DModelPart? selected = null;
        foreach (var part in parts)
        {
            if (part.Label != label)
                continue;
            if (selected != null || !part.Valid || part.Shape != CMU3DPartShape.Box || part.Yaw != 0 || part.Pitch != 0)
                return false;
            selected = part;
        }
        if (selected == null || selected.Max.Z <= 0)
            return false;
        surface = new CMU3DSceneSurface(source, position, yaw, selected.Min, selected.Max);
        return true;
    }

    public static float Offset(CMU3DModelPrototype model, EntityUid source, Vector2 position,
        IReadOnlyList<CMU3DSceneSurface> surfaces, float yaw = 0, bool allowProbe = true)
    {
        if (model.Placement != "surface")
            return 0;
        var distance = float.PositiveInfinity;
        CMU3DSceneSurface? selected = null;
        foreach (var surface in surfaces)
        {
            if (surface.Source == source)
                continue;
            var delta = position - surface.Position;
            var cosine = MathF.Cos(surface.Yaw);
            var sine = MathF.Sin(surface.Yaw);
            var local = new Vector2(cosine * delta.X + sine * delta.Y, -sine * delta.X + cosine * delta.Y);
            if (local.X < surface.Min.X || local.X > surface.Max.X || local.Y < surface.Min.Y || local.Y > surface.Max.Y)
                continue;
            var candidate = delta.LengthSquared();
            if (candidate > distance || candidate == distance && selected is { } previous &&
                (surface.Source.CompareTo(previous.Source) > 0 ||
                 surface.Source == previous.Source && surface.Max.Z <= previous.Max.Z))
                continue;
            selected = surface;
            distance = candidate;
        }
        if (selected == null)
        {
            if (allowProbe && float.IsFinite(yaw) && CMU3DSupportProbe.TryPoint(model, out var point))
            {
                var c = MathF.Cos(yaw);
                var s = MathF.Sin(yaw);
                var rotated = new Vector2(c * point.X - s * point.Y, s * point.X + c * point.Y);
                return Offset(model, source, position + rotated, surfaces, yaw, false);
            }
            return 0;
        }
        var bottom = float.PositiveInfinity;
        foreach (var part in model.Parts)
        {
            if (part.Valid)
            {
                part.Bounds(out var min, out _);
                bottom = Math.Min(bottom, min.Z);
            }
        }
        return float.IsFinite(bottom) ? selected.Value.Max.Z - bottom + 0.002f : 0;
    }
}

public readonly record struct CMU3DSceneSurface(EntityUid Source, Vector2 Position, float Yaw, Vector3 Min, Vector3 Max);

public readonly record struct CMU3DSceneRearWall(IReadOnlyList<CMU3DModelPart> Parts, float Yaw, Vector2 Delta);
