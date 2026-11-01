using System.Numerics;
using Robust.Shared.Maths;
using Robust.Shared.Noise;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.CMU14.ZLevelBuilding;

/// <summary>Dimensions of the continuous underground, independent of the chunks used to materialize it.</summary>
[Serializable, NetSerializable, DataDefinition]
public sealed partial class CMUCavernSettings
{
    [DataField]
    public int StartDepth = -3;

    [DataField]
    public int RegionSize = 96;

    /// <summary>Includes one solid separator below each group of connected vertical chambers.</summary>
    [DataField]
    public int BandHeight = 7;

    /// <summary>Null mixes geological regions. An override is useful for authored maps and previews.</summary>
    [DataField]
    public CMUCavernVariant? Variant;
}

/// <summary>
/// Samples the same chamber/tunnel volume at every depth. No entities, chunk history or random stream are
/// consulted, so discovering a different entrance cannot change an existing cavern or its continuation.
/// </summary>
public sealed partial class CMUCavernField
{
    private readonly int _seed;
    private readonly CMUCavernSettings _settings;
    private readonly FastNoiseLite _roughness;

    public CMUCavernField(int seed, CMUCavernSettings settings)
    {
        _seed = seed;
        _settings = settings;
        _roughness = new FastNoiseLite(seed);
        _roughness.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        _roughness.SetFrequency(0.045f);
    }

    public bool TryGetBand(int depth, out int top, out int bottom)
    {
        top = bottom = depth;
        if (depth > _settings.StartDepth)
            return false;

        var height = Math.Clamp(_settings.BandHeight, 4, 9);
        var band = (_settings.StartDepth - depth) / height;
        top = _settings.StartDepth - band * height;
        bottom = top - height + 1;
        return true;
    }

    public bool IsOpen(Vector2i tile, int depth)
    {
        if (!TryGetBand(depth, out var top, out var bottom) || depth == bottom)
            return false;

        var size = Math.Clamp(_settings.RegionSize, 48, 160);
        var cell = new Vector2i(FloorDiv(tile.X, size), FloorDiv(tile.Y, size));
        var point = new Vector3(tile.X + 0.5f, tile.Y + 0.5f, depth);
        var roughness = 1f + _roughness.GetNoise(point.X, point.Y, depth * 5f) * 0.12f;

        // A chamber cannot extend beyond its neighbouring regions. Each region links east and north,
        // producing connected galleries without flooding or pre-generating an entire cave graph.
        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                var region = cell + new Vector2i(x, y);
                var center = Center(region, top, bottom, size);
                var offset = point - center;
                var scale = size / 96f;
                var local = new Vector3(offset.X / scale, offset.Y / scale, offset.Z);
                if ((Hash(region, top, 9) & 1) != 0)
                    local = new Vector3(local.Y, local.X, local.Z);
                if ((Hash(region, top, 9) & 2) != 0)
                    local.X = -local.X;
                if (MathF.Abs(local.X) < 50 && MathF.Abs(local.Y) < 50 &&
                    InRegion(local, RegionVariant(region, top), (top - bottom) * 0.47f, roughness,
                        Sample(region, top, 3)))
                    return true;

                if (InTunnel(point, center, Center(region + new Vector2i(1, 0), top, bottom, size), size) ||
                    InTunnel(point, center, Center(region + new Vector2i(0, 1), top, bottom, size), size))
                    return true;
            }
        }

        return false;
    }

    /// <summary>Coordinate-based variation for decoration; does not advance a shared random stream.</summary>
    public int TileSeed(Vector2i tile, int depth) => (int) (Hash(tile, depth, 91) & int.MaxValue);

    private Vector3 Center(Vector2i region, int top, int bottom, int size)
    {
        return new Vector3(
            (region.X + 0.35f + Sample(region, top, 0) * 0.3f) * size,
            (region.Y + 0.35f + Sample(region, top, 1) * 0.3f) * size,
            (top + bottom + 1) * 0.5f + (Sample(region, top, 2) - 0.5f) * 0.5f);
    }

    private static bool InTunnel(Vector3 point, Vector3 from, Vector3 to, int size)
    {
        // Vertical scale makes galleries broad enough to transport equipment while leaving rock between bands.
        var scale = new Vector3(1f, 1f, size * 0.045f);
        point *= scale;
        from *= scale;
        to *= scale;
        var line = to - from;
        var along = Math.Clamp(Vector3.Dot(point - from, line) / line.LengthSquared(), 0f, 1f);
        return Vector3.DistanceSquared(point, from + along * line) < size * size * 0.004f;
    }

    private float Sample(Vector2i region, int band, int salt) => (Hash(region, band, salt) & 0xFFFFFF) / 16777216f;

    private uint Hash(Vector2i position, int depth, int salt)
    {
        unchecked
        {
            var hash = (uint) _seed ^ (uint) position.X * 0x9E3779B9u ^
                       (uint) position.Y * 0x85EBCA6Bu ^ (uint) depth * 0xC2B2AE35u ^ (uint) salt * 0x27D4EB2Fu;
            hash = (hash ^ (hash >> 16)) * 0x7FEB352Du;
            hash = (hash ^ (hash >> 15)) * 0x846CA68Bu;
            return hash ^ (hash >> 16);
        }
    }

    private static int FloorDiv(int value, int size) => (int) Math.Floor((double) value / size);
}
