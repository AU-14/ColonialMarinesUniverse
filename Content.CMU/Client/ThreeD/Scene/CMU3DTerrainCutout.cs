using System.Numerics;
using Content.Shared.CMU14.ThreeD;

namespace Content.Client.CMU14.ThreeD.Scene;

/// <summary>Bounded subtraction of authored cardinal alcoves from untextured box terrain. Never changes collision.</summary>
public static class CMU3DTerrainCutout
{
    public const int MaximumParts = 128;
    public const int MaximumCuts = 16;
    public const float MaximumReach = 8;
    private const float Epsilon = .00001f;
    private const float MinimumSlabSize = .001f;

    public static bool Valid(CMU3DModelPrototype model) => model.TerrainCutoutTargets.Length > 0 &&
        BoundsValid(model.TerrainCutoutMin, model.TerrainCutoutMax) &&
        Within(model.TerrainCutoutMin, MaximumReach) && Within(model.TerrainCutoutMax, MaximumReach) &&
        model.Placement == "floor" && !model.ConnectToNeighbours && !model.WallMounted && !model.FaceAwayFromWall &&
        model.CornerSurfaces.Length == 0 && model.BackWallMountTargets.Length == 0 &&
        model.WindowMountTargets.Length == 0 && model.PanelEndTargets.Length == 0 &&
        model.OpeningFacingTargets.Length == 0 && model.DirectionalModels.Length == 0;

    public static bool TryWorldBounds(Vector3 min, Vector3 max, Vector2 position, float yaw, out CMU3DTerrainVolume volume)
    {
        volume = default;
        if (!BoundsValid(min, max) || !float.IsFinite(position.X) || !float.IsFinite(position.Y) || !TryTurn(yaw, out var turn))
            return false;
        RotateBounds(min, max, turn, out var low, out var high);
        var offset = new Vector3(position, 0);
        volume = new CMU3DTerrainVolume(low + offset, high + offset);
        return true;
    }

    /// <summary>Returns original parts on unsupported intersections or budget exhaustion. No partial entity mutation.</summary>
    public static bool TryClip(IReadOnlyList<CMU3DModelPart> parts, Vector2 position, float yaw,
        IReadOnlyList<CMU3DTerrainVolume> worldCuts, out IReadOnlyList<CMU3DModelPart> result)
    {
        result = parts;
        if (parts.Count > MaximumParts || !TryTurn(yaw, out var turn) ||
            !float.IsFinite(position.X) || !float.IsFinite(position.Y))
            return false;
        var cuts = new List<CMU3DTerrainVolume>();
        foreach (var cut in worldCuts)
        {
            if (!BoundsValid(cut.Min, cut.Max))
                return false;
            RotateBounds(cut.Min - new Vector3(position, 0), cut.Max - new Vector3(position, 0), -turn, out var low, out var high);
            var local = new CMU3DTerrainVolume(low, high);
            if (!cuts.Contains(local))
                cuts.Add(local);
        }
        if (cuts.Count > MaximumCuts)
            return false;
        var working = new List<CMU3DModelPart>(parts);
        var changed = false;
        foreach (var cut in cuts)
        {
            var next = new List<CMU3DModelPart>();
            foreach (var part in working)
            {
                part.Bounds(out var low, out var high);
                if (!Overlaps(low, high, cut.Min, cut.Max))
                {
                    next.Add(part);
                    continue;
                }
                // Cropping an atlas box would stretch its normalized UVs. Leave it intact until UV cropping is supported.
                if (!part.Valid || part.Shape != CMU3DPartShape.Box || part.Pitch != 0 || part.Surface != null ||
                    !TryTurn(part.YawRadians, out var partTurn))
                    return false;
                var center = (part.Min + part.Max) / 2;
                var half = (part.Max - part.Min) / 2;
                RotateBounds(cut.Min - center, cut.Max - center, -partTurn, out var cutLow, out var cutHigh);
                var insideLow = Vector3.Max(-half, cutLow);
                var insideHigh = Vector3.Min(half, cutHigh);
                if (!Overlaps(-half, half, cutLow, cutHigh))
                {
                    next.Add(part);
                    continue;
                }
                // Six disjoint slabs: X sides, then Y sides within the remaining X, then Z within remaining XY.
                var cursorLow = -half;
                var cursorHigh = half;
                for (var axis = 0; axis < 3; axis++)
                {
                    if (Coordinate(insideLow, axis) - Coordinate(cursorLow, axis) > Epsilon)
                    {
                        var slabHigh = WithCoordinate(cursorHigh, axis, Coordinate(insideLow, axis));
                        if (!Quantizable(cursorLow, slabHigh))
                            return false;
                        next.Add(Slab(part, cursorLow, slabHigh, center, partTurn));
                    }
                    if (Coordinate(cursorHigh, axis) - Coordinate(insideHigh, axis) > Epsilon)
                    {
                        var slabLow = WithCoordinate(cursorLow, axis, Coordinate(insideHigh, axis));
                        if (!Quantizable(slabLow, cursorHigh))
                            return false;
                        next.Add(Slab(part, slabLow, cursorHigh, center, partTurn));
                    }
                    cursorLow = WithCoordinate(cursorLow, axis, Coordinate(insideLow, axis));
                    cursorHigh = WithCoordinate(cursorHigh, axis, Coordinate(insideHigh, axis));
                }
                changed = true;
                if (next.Count > MaximumParts)
                    return false;
            }
            if (next.Count > MaximumParts)
                return false;
            working = next;
        }
        if (changed)
            result = working;
        return changed;
    }

    public static bool Overlaps(Vector3 low, Vector3 high, Vector3 otherLow, Vector3 otherHigh) =>
        MathF.Min(high.X, otherHigh.X) - MathF.Max(low.X, otherLow.X) > Epsilon &&
        MathF.Min(high.Y, otherHigh.Y) - MathF.Max(low.Y, otherLow.Y) > Epsilon &&
        MathF.Min(high.Z, otherHigh.Z) - MathF.Max(low.Z, otherLow.Z) > Epsilon;

    public static bool TryTurn(float yaw, out int turn)
    {
        turn = 0;
        if (!float.IsFinite(yaw))
            return false;
        var normalized = MathF.IEEERemainder(yaw, MathF.Tau);
        turn = (int) MathF.Round(normalized / (MathF.PI / 2));
        return MathF.Abs(normalized - turn * (MathF.PI / 2)) <= Epsilon;
    }

    private static CMU3DModelPart Slab(CMU3DModelPart source, Vector3 min, Vector3 max, Vector3 center, int turn)
    {
        var slabCenter = center + Rotate((min + max) / 2, turn);
        var half = (max - min) / 2;
        return new CMU3DModelPart
        {
            Label = source.Label,
            Shape = source.Shape,
            Min = slabCenter - half,
            Max = slabCenter + half,
            Yaw = source.Yaw,
            Color = source.Color,
            Surface = source.Surface,
            SurfaceAxis = source.SurfaceAxis,
            SurfaceFlipU = source.SurfaceFlipU,
            OmitWhenConnected = source.OmitWhenConnected,
        };
    }

    private static void RotateBounds(Vector3 low, Vector3 high, int turn, out Vector3 min, out Vector3 max)
    {
        var a = Rotate(low, turn);
        var b = Rotate(high, turn);
        min = Vector3.Min(a, b);
        max = Vector3.Max(a, b);
    }

    private static Vector3 Rotate(Vector3 value, int turn) => ((turn % 4 + 4) % 4) switch
    {
        1 => new Vector3(-value.Y, value.X, value.Z),
        2 => new Vector3(-value.X, -value.Y, value.Z),
        3 => new Vector3(value.Y, -value.X, value.Z),
        _ => value,
    };

    private static bool BoundsValid(Vector3 min, Vector3 max) => Within(min, float.MaxValue) && Within(max, float.MaxValue) &&
        min.X < max.X && min.Y < max.Y && min.Z < max.Z;
    private static bool Quantizable(Vector3 min, Vector3 max) => max.X - min.X >= MinimumSlabSize &&
        max.Y - min.Y >= MinimumSlabSize && max.Z - min.Z >= MinimumSlabSize;
    private static bool Within(Vector3 value, float limit) => float.IsFinite(value.X) && float.IsFinite(value.Y) &&
        float.IsFinite(value.Z) && MathF.Abs(value.X) <= limit && MathF.Abs(value.Y) <= limit && MathF.Abs(value.Z) <= limit;
    private static float Coordinate(Vector3 value, int axis) => axis == 0 ? value.X : axis == 1 ? value.Y : value.Z;
    private static Vector3 WithCoordinate(Vector3 value, int axis, float coordinate) => axis == 0
        ? new Vector3(coordinate, value.Y, value.Z) : axis == 1 ? new Vector3(value.X, coordinate, value.Z) : new Vector3(value.X, value.Y, coordinate);
}

public readonly record struct CMU3DTerrainVolume(Vector3 Min, Vector3 Max);
