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

    /// <summary>Optional structural recipe override; null mixes all ten layouts within each region.</summary>
    [DataField]
    public CMUCavernLayout? Layout;
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
    private readonly Dictionary<(Vector2i Region, int Top), CMUCavernRegionPlan> _regions = new();
    private readonly Dictionary<(Vector2i Block, int Top), uint> _networks = new();

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

        // Plans contain geometry only. Visiting a region never creates tiles or entities, and each
        // passage has one coordinate-based owner so discovering its far end cannot reroll it.
        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                var plan = GetRegionPlan(cell + new Vector2i(x, y), depth);
                for (var i = 0; i < plan.Chambers.Count; i++)
                {
                    if (InChamber(point, plan.Chambers[i], roughness))
                        return true;
                }
                for (var i = 0; i < plan.Passages.Count; i++)
                {
                    if (InPassage(point, plan.Passages[i]))
                        return true;
                }
            }
        }

        return false;
    }

    /// <summary>Coordinate-based variation for decoration; does not advance a shared random stream.</summary>
    public int TileSeed(Vector2i tile, int depth) => (int) (Hash(tile, depth, 91) & int.MaxValue);

    private Vector3 Center(Vector2i region, int top, int bottom, int size)
    {
        return new Vector3(
            (region.X + 0.38f + Sample(region, top, 0) * 0.24f) * size,
            (region.Y + 0.38f + Sample(region, top, 1) * 0.24f) * size,
            (top + bottom + 1) * 0.5f + (Sample(region, top, 2) - 0.5f) * 0.6f);
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
