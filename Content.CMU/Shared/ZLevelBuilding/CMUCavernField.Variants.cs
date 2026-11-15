using System.Numerics;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.ZLevelBuilding;

[Serializable, NetSerializable]
public enum CMUCavernVariant : byte
{
    BraidedGalleries,
    FloodedFork,
    PillarCathedral,
    LavaTubes,
    LeakingCavern,
    BiologicalDeadZone,
    ThermalSprings,
    VentLabyrinth,
}

public enum CMUCavernScenery : byte
{
    None,
    Water,
    Lava,
    Spring,
    Mineral,
    Crystal,
    Stalagmite,
    DeadRoots,
    DeadFungi,
    Vent,
    Pebbles,
    Shale,
    Calcite,
    Limestone,
    Silt,
    RootTangle,
    FungalMat,
    IronCrust,
    Scoria,
}

public sealed partial class CMUCavernField
{
    private CMUCavernVariant RegionVariant(Vector2i region, int top) =>
        _settings.Variant ?? (_biomeWeights == null
            ? (CMUCavernVariant) (Hash(region, top, 17) % 8)
            : WeightedBiome(region, top, 17));

    /// <summary>Geology follows local chambers, allowing several environments within one region.</summary>
    public CMUCavernVariant VariantAt(Vector2i tile, int depth)
    {
        TryGetBand(depth, out var top, out _);
        var size = Math.Clamp(_settings.RegionSize, 48, 160);
        var cell = new Vector2i(FloorDiv(tile.X, size), FloorDiv(tile.Y, size));
        var closest = float.MaxValue;
        var result = RegionVariant(cell, top);
        for (var x = -1; x <= 1; x++)
        for (var y = -1; y <= 1; y++)
        {
            var region = cell + new Vector2i(x, y);
            var chambers = GetRegionPlan(region, depth).Chambers;
            for (var i = 0; i < chambers.Count; i++)
            {
                var chamber = chambers[i];
                var delta = new Vector3(tile.X + 0.5f, tile.Y + 0.5f, depth) - chamber.Center;
                delta.Z *= 8;
                var distance = delta.LengthSquared();
                if (distance >= closest)
                    continue;
                closest = distance;
                result = chamber.Variant;
            }
        }
        return result;
    }

    /// <summary>Scenery only; the caller must require a natural floor and preserve existing entities.</summary>
    public CMUCavernScenery SceneryAt(Vector2i tile, int depth)
    {
        if (!TryGetBand(depth, out var top, out _))
            return CMUCavernScenery.None;

        var variant = VariantAt(tile, depth);
        var wet = _roughness.GetNoise(tile.X * 0.7f + 173, tile.Y * 0.7f - 97, top * 9f);
        // Preserve dry centres along the connecting galleries; pools follow geology, not chunk edges.
        var dry = OnGallerySpine(tile, depth);
        if (!dry)
        {
            if (variant == CMUCavernVariant.FloodedFork && wet > 0.12f ||
                variant == CMUCavernVariant.LeakingCavern && wet > 0.42f)
                return CMUCavernScenery.Water;
            if (variant == CMUCavernVariant.LavaTubes && wet > 0.38f)
                return CMUCavernScenery.Lava;
            if (variant == CMUCavernVariant.ThermalSprings && wet > 0.22f)
                return CMUCavernScenery.Spring;
        }

        // At most one prop per 4x4 geological cell, independent of generation order and chunk size.
        var cell = new Vector2i(FloorDiv(tile.X, 4), FloorDiv(tile.Y, 4));
        var slot = tile.X - cell.X * 4 + (tile.Y - cell.Y * 4) * 4;
        if (slot != Hash(cell, depth, 37) % 16)
            return CMUCavernScenery.None;

        var detail = Hash(cell, depth, 41) % 4;
        return variant switch
        {
            CMUCavernVariant.BraidedGalleries => detail switch
            {
                0 => CMUCavernScenery.Mineral,
                1 => CMUCavernScenery.Shale,
                2 => CMUCavernScenery.IronCrust,
                _ => CMUCavernScenery.Pebbles,
            },
            CMUCavernVariant.FloodedFork => detail switch
            {
                0 => CMUCavernScenery.Stalagmite,
                1 => CMUCavernScenery.Silt,
                2 => CMUCavernScenery.Limestone,
                _ => CMUCavernScenery.Pebbles,
            },
            CMUCavernVariant.PillarCathedral => detail switch
            {
                0 => CMUCavernScenery.Calcite,
                1 => CMUCavernScenery.Limestone,
                2 => CMUCavernScenery.Stalagmite,
                _ => CMUCavernScenery.Shale,
            },
            CMUCavernVariant.LavaTubes => detail switch
            {
                0 => CMUCavernScenery.Scoria,
                1 => CMUCavernScenery.Vent,
                2 => CMUCavernScenery.Shale,
                _ => CMUCavernScenery.Pebbles,
            },
            CMUCavernVariant.LeakingCavern => detail switch
            {
                0 => CMUCavernScenery.Stalagmite,
                1 => CMUCavernScenery.Calcite,
                2 => CMUCavernScenery.Silt,
                _ => CMUCavernScenery.Limestone,
            },
            CMUCavernVariant.BiologicalDeadZone => detail switch
            {
                0 => CMUCavernScenery.DeadRoots,
                1 => CMUCavernScenery.DeadFungi,
                2 => CMUCavernScenery.RootTangle,
                _ => CMUCavernScenery.FungalMat,
            },
            CMUCavernVariant.ThermalSprings => detail switch
            {
                0 => CMUCavernScenery.Mineral,
                1 => CMUCavernScenery.Calcite,
                2 => CMUCavernScenery.IronCrust,
                _ => CMUCavernScenery.Silt,
            },
            CMUCavernVariant.VentLabyrinth => detail switch
            {
                0 => CMUCavernScenery.Vent,
                1 => CMUCavernScenery.Scoria,
                2 => CMUCavernScenery.IronCrust,
                _ => CMUCavernScenery.Shale,
            },
            _ => CMUCavernScenery.None,
        };
    }

    private bool OnGallerySpine(Vector2i tile, int depth)
    {
        var size = Math.Clamp(_settings.RegionSize, 48, 160);
        var cell = new Vector2i(FloorDiv(tile.X, size), FloorDiv(tile.Y, size));
        var point = new Vector2(tile.X + 0.5f, tile.Y + 0.5f);
        for (var x = -1; x <= 1; x++)
        for (var y = -1; y <= 1; y++)
        {
            var region = cell + new Vector2i(x, y);
            var passages = GetRegionPlan(region, depth).Passages;
            for (var p = 0; p < passages.Count; p++)
            {
                var passage = passages[p];
                for (var i = 1; i < passage.Points.Count; i++)
                {
                    var from = passage.Points[i - 1];
                    var to = passage.Points[i];
                    var a = new Vector2(from.X, from.Y);
                    var line = new Vector2(to.X, to.Y) - a;
                    var along = line.LengthSquared() < 0.001f ? 0 :
                        Math.Clamp(Vector2.Dot(point - a, line) / line.LengthSquared(), 0, 1);
                    var width = Math.Min(2.5f, passage.Widths[i - 1] * 0.6f);
                    if (Vector2.DistanceSquared(point, a + along * line) < width * width)
                        return true;
                }
            }
        }
        return false;
    }

    private static bool InRegion(Vector3 p, CMUCavernVariant variant, float h, float roughness, float variation)
    {
        var bend = (variation - 0.5f) * 8;
        var breadth = MathF.Sqrt(roughness);
        bool Room(float x, float y, float z, float rx, float ry, float height) =>
            Ellipsoid(p - new Vector3(x, y, z), rx * breadth, ry * breadth, height);
        bool Passage(Vector3 a, Vector3 b, float width = 5, float height = 1.5f) =>
            Gallery(p, a, b, width * breadth, Math.Min(height, h));

        switch (variant)
        {
            case CMUCavernVariant.BraidedGalleries:
                var wave = MathF.Sin(p.X * 0.075f + variation) * (5 + variation * 3);
                return MathF.Abs(p.X) < 39 && (
                           Ellipsoid(new Vector3(0, p.Y - wave - 15, p.Z - 0.35f), 1, 4.5f * breadth, h * 0.65f) ||
                           Ellipsoid(new Vector3(0, p.Y + wave, p.Z), 1, 5 * breadth, h * 0.65f) ||
                           Ellipsoid(new Vector3(0, p.Y - wave + 15, p.Z + 0.35f), 1, 5.5f * breadth, h * 0.65f)) ||
                       Passage(new(-25, -19, 0), new(-20, 19, 0), 5) ||
                       Passage(new(24, -19, 0), new(29, 19, 0), 5);

            case CMUCavernVariant.FloodedFork:
                return Passage(new(-38, 0, 0), Vector3.Zero, 7, h * 0.75f) ||
                       Passage(Vector3.Zero, new(25, 23 + bend, -0.3f), 7, h * 0.75f) ||
                       Passage(Vector3.Zero, new(27, -24 + bend, 0.3f), 6, h * 0.75f) ||
                       Room(26, 23 + bend, -0.3f, 16, 13, h * 0.8f) ||
                       Room(27, -24 + bend, 0.3f, 13, 16, h * 0.8f) ||
                       Passage(new(15, 14, 0), new(4, 34, 0), 4);

            case CMUCavernVariant.PillarCathedral:
                if (!Room(0, 0, 0, 39 + variation * 4, 35, h))
                    return false;
                for (var x = -18; x <= 18; x += 18)
                for (var y = -14; y <= 14; y += 28)
                    if (Ellipsoid(new Vector3(p.X - x - bend * 0.4f, p.Y - y, 0), 3.5f, 5, 1))
                        return false;
                return true;

            case CMUCavernVariant.LavaTubes:
                var winding = MathF.Sin(p.X * 0.085f + variation) * 19;
                return MathF.Abs(p.X) < 40 &&
                       Ellipsoid(new Vector3(0, p.Y - winding, p.Z - MathF.Sin(p.X * 0.06f) * 0.5f), 1, 7 * breadth, h * 0.62f) ||
                       Room(-20, -18, 0, 12, 11, h * 0.7f) ||
                       Passage(new(18, 18, 0), new(28, 35, -0.4f), 5);

            case CMUCavernVariant.LeakingCavern:
                return Passage(new(-39, -21, 0.4f), new(18, -18 + bend, 0.4f), 5, h * 0.65f) ||
                       Passage(new(-26, -20, 0.4f), new(-26, 0, 0), 5) ||
                       Passage(new(-31, 0, 0), new(30, bend, 0), 6) ||
                       Passage(new(23, 0, 0), new(23, 22, -0.4f), 5) ||
                       Passage(new(-14, 24 + bend, -0.4f), new(39, 20, -0.4f), 5, h * 0.65f) ||
                       Passage(new(-3, 23, -0.4f), new(-8, 37, -0.7f), 4);

            case CMUCavernVariant.BiologicalDeadZone:
                for (var x = -24; x <= 24; x += 24)
                {
                    var y = MathF.Sin(x * 0.08f + variation) * 12;
                    if (Room(x, y, 0, 14 + variation * 3, 13, h * 0.8f) ||
                        Room(x + 5, y + 20, 0.2f, 10, 12, h * 0.75f) ||
                        Passage(new(x, y, 0), new(x + 5, y + 20, 0.2f), 4))
                        return true;
                }
                return Passage(new(-28, -8, 0), new(28, 8, 0), 5);

            case CMUCavernVariant.ThermalSprings:
                var basin = Room(-23, -5, 0, 19, 23, h * 0.7f) ||
                            Room(0, 10 + bend, -0.3f, 22, 23, h * 0.8f) ||
                            Room(24, -8, 0.3f, 18, 24, h * 0.7f);
                return basin && !Ellipsoid(new Vector3(p.X + 10, p.Y - 25, 0), 5, 15, 1) &&
                       !Ellipsoid(new Vector3(p.X - 26, p.Y + 27, 0), 6, 11, 1);

            case CMUCavernVariant.VentLabyrinth:
                return Passage(new(-32, -23, 0), new(31, -23, 0), 4.5f) ||
                       Passage(new(31, -23, 0), new(31, 22, 0), 4.5f) ||
                       Passage(new(31, 22, 0), new(-30, 22, 0), 4.5f) ||
                       Passage(new(-30, 22, 0), new(-30, -23, 0), 4.5f) ||
                       Passage(new(-30, -4, 0), new(8, -4, 0), 5) ||
                       Passage(new(8, -4, 0), new(8, 22, 0), 5) ||
                       Room(31, 22, 0, 7, 7, h);
            default:
                return false;
        }
    }

    private static bool Ellipsoid(Vector3 p, float x, float y, float z) =>
        p.X * p.X / (x * x) + p.Y * p.Y / (y * y) + p.Z * p.Z / (z * z) < 1;

    private static bool Gallery(Vector3 p, Vector3 a, Vector3 b, float width, float height)
    {
        var scale = new Vector3(1, 1, width / height);
        p *= scale;
        a *= scale;
        b *= scale;
        var line = b - a;
        var along = Math.Clamp(Vector3.Dot(p - a, line) / line.LengthSquared(), 0, 1);
        return Vector3.DistanceSquared(p, a + along * line) < width * width;
    }
}
