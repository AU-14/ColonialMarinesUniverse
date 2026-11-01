using System.Collections.Immutable;
using System.Numerics;
using Robust.Shared.Maths;

namespace Content.Shared.CMU14.ZLevelBuilding;

/// <summary>Small immutable plans, shared by terrain sampling, survey previews and validation.</summary>
public sealed record CMUCavernRegionPlan(
    IReadOnlyList<CMUCavernChamber> Chambers,
    IReadOnlyList<CMUCavernPassage> Passages);

public sealed partial class CMUCavernField
{
    /// <summary>Region coordinates, not tile coordinates. This allocates no world terrain.</summary>
    public CMUCavernRegionPlan GetRegionPlan(Vector2i region, int depth)
    {
        if (!TryGetBand(depth, out var top, out var bottom))
            return new CMUCavernRegionPlan(Array.Empty<CMUCavernChamber>(), Array.Empty<CMUCavernPassage>());
        if (_regions.TryGetValue((region, top), out var cached))
            return cached;

        // Samplers are also used by the scanner and previews. Their plan cache must not grow with an
        // arbitrarily long survey; eviction is safe because plans depend only on seed and coordinates.
        if (_regions.Count >= 64)
            _regions.Clear();
        var size = Math.Clamp(_settings.RegionSize, 48, 160);
        var scale = size / 96f;
        var center = Center(region, top, bottom, size);
        var family = RegionVariant(region, top);
        var count = 2 + (int) (Hash(region, top, 111) % 3);
        var chambers = new CMUCavernChamber[count];
        for (var i = 0; i < count; i++)
        {
            var salt = 120 + i * 12;
            var angle = Sample(region, top, salt) * MathF.Tau;
            var position = center;
            var breadth = 0.48f + Sample(region, top, salt + 1) * 0.42f;
            if (i == 0 && Sample(region, top, salt + 2) < 0.12f)
                breadth = 1 + Sample(region, top, salt + 3) * 0.15f;
            if (i > 0)
            {
                // Satellite lobes have their own geology, elevation, scale and arbitrary orientation.
                var bearing = angle + i * MathF.Tau / count;
                var radius = (23 + Sample(region, top, salt + 2) * 14) * scale;
                position += new Vector3(MathF.Cos(bearing) * radius, MathF.Sin(bearing) * radius,
                    (Sample(region, top, salt + 3) - 0.5f) * 1.3f);
                breadth = 0.22f + Sample(region, top, salt + 1) * 0.27f;
            }
            var variant = i == 0 ? family : _settings.Variant ??
                (CMUCavernVariant) (((int) family + 1 + Hash(region, top, salt + 4) % 7) % 8);
            chambers[i] = new CMUCavernChamber(position,
                new Vector2(breadth, breadth * (0.7f + Sample(region, top, salt + 5) * 0.5f)) * scale,
                MathF.Cos(angle), MathF.Sin(angle),
                (top - bottom) * 0.47f * (0.8f + Sample(region, top, salt + 6) * 0.2f),
                Sample(region, top, salt + 7), variant,
                _settings.Layout ?? (CMUCavernLayout) (Hash(region, top, salt + 8) % 10));
        }

        var passages = new List<CMUCavernPassage>();
        for (var i = 1; i < count; i++)
            passages.Add(MakePassage(region, top, 180 + i * 9, center, chambers[i].Center, scale));
        if (count > 2 && Sample(region, top, 214) < 0.3f)
            passages.Add(MakePassage(region, top, 215, chambers[1].Center, chambers[^1].Center, scale));

        for (var axis = 0; axis < 2; axis++)
        {
            if (!HasRegionConnection(region, top, axis))
                continue;
            var target = region + (axis == 0 ? new Vector2i(1, 0) : new Vector2i(0, 1));
            var passage = MakePassage(region, top, 240 + axis * 20, center,
                Center(target, top, bottom, size), scale, target);
            passages.Add(passage);

            // A finite side branch has no destination chamber. Incidental intersections can form a
            // shortcut, but otherwise the rounded terminal is surrounded by ordinary mineable rock.
            if (Sample(region, top, 257 + axis * 20) < 0.8f)
            {
                var segment = Math.Max(1, passage.Points.Count / 2);
                var from = passage.Points[segment - 1];
                var to = passage.Points[segment];
                var start = (from + to) * 0.5f;
                var delta = to - from;
                var normal = new Vector2(-delta.Y, delta.X);
                normal = normal.LengthSquared() < 0.001f ? Vector2.UnitX : Vector2.Normalize(normal);
                normal *= (12 + Sample(region, top, 258 + axis * 20) * 16) * scale;
                if ((Hash(region, top, 259 + axis * 20) & 1) != 0)
                    normal = -normal;
                passages.Add(MakePassage(region, top, 280 + axis * 10, start,
                    start + new Vector3(normal, 0), scale * 0.65f, blind: true));
            }
        }

        var result = new CMUCavernRegionPlan(chambers.ToImmutableArray(), passages.ToImmutableArray());
        _regions[(region, top)] = result;
        return result;
    }

    private bool HasRegionConnection(Vector2i region, int top, int axis)
    {
        var block = new Vector2i(FloorDiv(region.X, 4), FloorDiv(region.Y, 4));
        var local = region - block * 4;
        // One independently seeded exit per macro boundary connects neighbouring mazes. The opposite
        // side reads this same owner, so there are no mismatched exits at negative coordinates.
        if (axis == 0 && local.X == 3)
            return local.Y == Hash(block, top, 301) % 4;
        if (axis == 1 && local.Y == 3)
            return local.X == Hash(block, top, 302) % 4;
        var index = axis == 0 ? local.Y * 3 + local.X : 12 + local.Y * 4 + local.X;
        return (GetNetwork(block, top) & (1u << index)) != 0;
    }

    private uint GetNetwork(Vector2i block, int top)
    {
        if (_networks.TryGetValue((block, top), out var cached))
            return cached;
        if (_networks.Count >= 16)
            _networks.Clear();

        // Kruskal's algorithm on sixteen geological regions: a connected tree first, then sparse
        // optional loops. No flood-fill of world tiles or exploration history is involved.
        var edges = new List<(int A, int B, int Index, uint Rank)>();
        for (var y = 0; y < 4; y++)
        for (var x = 0; x < 4; x++)
        {
            var a = y * 4 + x;
            if (x < 3)
            {
                var index = y * 3 + x;
                edges.Add((a, a + 1, index, Hash(block, top, 320 + index)));
            }
            if (y < 3)
            {
                var index = 12 + y * 4 + x;
                edges.Add((a, a + 4, index, Hash(block, top, 320 + index)));
            }
        }
        edges.Sort((a, b) => a.Rank != b.Rank ? a.Rank.CompareTo(b.Rank) : a.Index.CompareTo(b.Index));
        var groups = new int[16];
        for (var i = 0; i < groups.Length; i++)
            groups[i] = i;
        uint mask = 0;
        foreach (var edge in edges)
        {
            if (groups[edge.A] == groups[edge.B])
            {
                if (Sample(block, top, 350 + edge.Index) < 0.08f + Sample(block, top, 380) * 0.14f)
                    mask |= 1u << edge.Index;
                continue;
            }
            mask |= 1u << edge.Index;
            var old = groups[edge.B];
            var target = groups[edge.A];
            for (var i = 0; i < groups.Length; i++)
            {
                if (groups[i] == old)
                    groups[i] = target;
            }
        }
        _networks[(block, top)] = mask;
        return mask;
    }
}
