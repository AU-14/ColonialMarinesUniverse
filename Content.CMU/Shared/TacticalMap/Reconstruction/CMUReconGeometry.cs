using System.Numerics;

namespace Content.Shared.CMU14.TacticalMap.Reconstruction;

/// <summary>
/// Bounded sector layout and reference ray query. Keep cell dimensions and heights in sync with
/// reconstruction.swsl. Orders use source tile coordinates, never pixels or generated mesh indices.
/// </summary>
public static class CMUReconGeometry
{
    public const int Size = 48;
    public const int ChunkSize = 16;
    public const int MaxSize = 1024;
    public const int MaxSurfaces = 4096;
    public const int MaxLevels = 8;
    public const float LevelHeight = 3f;
    public const float FloorHeight = 0.18f;
    public const int MaxSteps = 2080;

    public static int Index(int x, int y, int level, int width = Size, int height = Size) => (level * height + y) * width + x;

    public static float Height(byte material) => (CMUReconMaterial) material switch
    {
        CMUReconMaterial.Wall => 2.8f,
        CMUReconMaterial.Door or CMUReconMaterial.DoubleDoor or CMUReconMaterial.OpenDoor or CMUReconMaterial.OpenDoubleDoor => 2.6f,
        CMUReconMaterial.Water => 0.2f,
        CMUReconMaterial.Sprite => 1.6f,
        CMUReconMaterial.Glass => 2.4f,
        CMUReconMaterial.Barricade => 1.1f,
        CMUReconMaterial.Machinery => 1.6f,
        CMUReconMaterial.Furniture => 0.85f,
        CMUReconMaterial.Crate => 1.15f,
        CMUReconMaterial.Tree => 2.8f,
        CMUReconMaterial.Rock => 1.8f,
        CMUReconMaterial.Stairs => 0.65f,
        CMUReconMaterial.Railing => 1f,
        CMUReconMaterial.Floor or CMUReconMaterial.Ground => FloorHeight,
        _ => 0f,
    };

    public static bool CanOrder(byte material) => material is (byte) CMUReconMaterial.Floor or (byte) CMUReconMaterial.Ground;

    public static bool ValidDimensions(int width, int height, int levels) => levels is > 0 and <= MaxLevels &&
        width is > 0 and <= MaxSize && height is > 0 and <= MaxSize && width % ChunkSize == 0 && height % ChunkSize == 0;

    public static bool ValidCells(byte[] cells, int levels, int width = Size, int height = Size) =>
        ValidDimensions(width, height, levels) && cells.Length == width * height * levels;

    public static void Footprint(byte material, byte direction, out Vector2 min, out Vector2 max)
    {
        var half = (CMUReconMaterial) material switch
        {
            CMUReconMaterial.Door or CMUReconMaterial.OpenDoor => new Vector2(0.5f, 0.12f),
            CMUReconMaterial.DoubleDoor or CMUReconMaterial.OpenDoubleDoor => new Vector2(0.12f, 0.5f),
            CMUReconMaterial.Glass or CMUReconMaterial.Railing => new Vector2(0.5f, 0.08f),
            CMUReconMaterial.Barricade => new Vector2(0.5f, 0.18f),
            CMUReconMaterial.Machinery or CMUReconMaterial.Crate => new Vector2(0.42f),
            CMUReconMaterial.Furniture => new Vector2(0.42f, 0.36f),
            CMUReconMaterial.Tree => new Vector2(0.38f),
            _ => new Vector2(0.5f),
        };
        if ((direction & 1) != 0)
            half = new Vector2(half.Y, half.X);
        min = new Vector2(0.5f) - half;
        max = new Vector2(0.5f) + half;
    }

    public static bool TryPick(byte[] cells, int levels, int cutLevel, Vector3 origin, Vector3 direction,
        out Vector3 hit, out Vector2i tile, out int level, float wallScale = 1f, bool isolate = false,
        int width = Size, int height = Size, uint[]? appearance = null, byte[]? directions = null)
    {
        hit = default;
        tile = default;
        level = default;
        if (!ValidCells(cells, levels, width, height) || !Finite(origin) || !Finite(direction) || direction.LengthSquared() < 0.0001f ||
            appearance is { Length: > 0 } && appearance.Length != cells.Length || directions is { Length: > 0 } && directions.Length != cells.Length ||
            !float.IsFinite(wallScale) || wallScale < 0.1f || wallScale > 1f)
            return false;

        direction = Vector3.Normalize(direction);
        var visible = Math.Clamp(cutLevel + 1, 1, levels);
        var first = isolate ? visible - 1 : 0;
        if (!IntersectBox(origin, direction, new Vector3(0, 0, first * LevelHeight), new Vector3(width, height, visible * LevelHeight), out var enter, out var leave))
            return false;

        var distance = Math.Max(0, enter) + 0.0001f;
        for (var step = 0; step < MaxSteps && distance <= leave; step++)
        {
            var position = origin + direction * distance;
            var x = (int) MathF.Floor(position.X);
            var y = (int) MathF.Floor(position.Y);
            var z = (int) MathF.Floor(position.Z / LevelHeight);
            if (x < 0 || y < 0 || z < first || x >= width || y >= height || z >= visible)
                break;

            var index = Index(x, y, z, width, height);
            var material = cells[index];
            var cellHeight = Height(material);
            if (z == visible - 1 && (material is >= (byte) CMUReconMaterial.Wall and <= (byte) CMUReconMaterial.Barricade ||
                                    material is >= (byte) CMUReconMaterial.DoubleDoor and <= (byte) CMUReconMaterial.OpenDoubleDoor))
                cellHeight *= wallScale;
            var min = new Vector3(x, y, z * LevelHeight);
            Footprint(material, directions is { Length: > 0 } ? directions[index] : (byte) 0, out var footprintMin, out var footprintMax);
            var nearest = float.PositiveInfinity;
            if (cellHeight > 0 && IntersectBox(origin, direction, min + new Vector3(footprintMin, 0),
                    min + new Vector3(footprintMax, cellHeight), out var near, out var far) && far >= distance - 0.001f)
                nearest = Math.Max(near, 0);
            // Props sit on an independently textured floor; their reduced footprint must not create holes.
            if (appearance is { Length: > 0 } && (appearance[index] & 0xffff) != 0 &&
                IntersectBox(origin, direction, min, min + new Vector3(1, 1, FloorHeight), out near, out far) && far >= distance - 0.001f)
                nearest = Math.Min(nearest, Math.Max(near, 0));
            if (float.IsFinite(nearest))
            {
                hit = origin + direction * nearest;
                tile = new Vector2i(x, y);
                level = z;
                return true;
            }

            var next = Math.Min(NextBoundary(position.X, direction.X, x, 1),
                Math.Min(NextBoundary(position.Y, direction.Y, y, 1), NextBoundary(position.Z, direction.Z, z, LevelHeight)));
            distance += next + 0.0001f;
        }

        return false;
    }

    private static float NextBoundary(float position, float direction, int cell, float size) => Math.Abs(direction) < 0.000001f
        ? float.PositiveInfinity
        : Math.Max(0, ((cell + (direction > 0 ? 1 : 0)) * size - position) / direction);

    private static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);

    public static bool IntersectBox(Vector3 origin, Vector3 direction, Vector3 min, Vector3 max, out float enter, out float leave)
    {
        enter = float.NegativeInfinity;
        leave = float.PositiveInfinity;
        for (var axis = 0; axis < 3; axis++)
        {
            var o = axis == 0 ? origin.X : axis == 1 ? origin.Y : origin.Z;
            var d = axis == 0 ? direction.X : axis == 1 ? direction.Y : direction.Z;
            var lo = axis == 0 ? min.X : axis == 1 ? min.Y : min.Z;
            var hi = axis == 0 ? max.X : axis == 1 ? max.Y : max.Z;
            if (Math.Abs(d) < 0.000001f)
            {
                if (o < lo || o >= hi)
                    return false;
                continue;
            }

            var a = (lo - o) / d;
            var b = (hi - o) / d;
            enter = Math.Max(enter, Math.Min(a, b));
            leave = Math.Min(leave, Math.Max(a, b));
        }

        return leave >= Math.Max(enter, 0);
    }
}
