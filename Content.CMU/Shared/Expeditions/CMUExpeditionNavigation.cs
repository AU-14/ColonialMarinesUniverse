using System;
using System.Collections.Generic;

namespace Content.Shared.CMU14.Expeditions;

public static partial class CMUExpeditionGenerator
{
    /// <summary>Dry corridors and possible short, bank-supported crossings, before any construction.</summary>
    private sealed class Navigation(int size)
    {
        public readonly bool[] Walkable = new bool[size * size];
        public readonly int[] Region = new int[size * size];
        public readonly int[] BlockedPrefix = new int[(size + 1) * (size + 1)];
        public readonly Dictionary<int, List<int>> Crossings = new();
    }

    private static Navigation BuildNavigation(CMUExpeditionPlan plan)
    {
        var size = plan.Size;
        var nav = new Navigation(size);
        var stride = size + 1;
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var blocked = plan.Terrain[plan.Index(x, y)] is CMUExpeditionTerrain.Water or CMUExpeditionTerrain.Cliff;
            nav.BlockedPrefix[(y + 1) * stride + x + 1] = (blocked ? 1 : 0) +
                nav.BlockedPrefix[y * stride + x + 1] + nav.BlockedPrefix[(y + 1) * stride + x] -
                nav.BlockedPrefix[y * stride + x];
        }
        for (var y = 3; y < size - 3; y++)
        for (var x = 3; x < size - 3; x++)
            nav.Walkable[plan.Index(x, y)] = IsDrySquare(plan, nav, new(x, y), 1);

        var occupied = new bool[size * size];
        for (var y = 3; y < size - 3; y++)
        for (var x = 3; x < size - 3; x++)
        {
            var from = plan.Index(x, y);
            if (!nav.Walkable[from] || occupied[from])
                continue;
            foreach (var (dx, dy) in new[] { (1, 0), (0, 1) })
            {
                if (nav.Walkable[plan.Index(x + dx, y + dy)])
                    continue;
                var wet = false;
                for (var span = 1; span <= CMUExpeditionPlan.MaximumBridgeSpan; span++)
                {
                    var nx = x + dx * span;
                    var ny = y + dy * span;
                    if (nx >= size - 3 || ny >= size - 3)
                        break;
                    var cliff = false;
                    for (var offset = -1; offset <= 1; offset++)
                    {
                        var terrain = plan.Terrain[plan.Index(nx + dy * offset, ny + dx * offset)];
                        wet |= terrain == CMUExpeditionTerrain.Water;
                        cliff |= terrain == CMUExpeditionTerrain.Cliff;
                    }
                    if (cliff)
                        break;
                    var to = plan.Index(nx, ny);
                    if (!nav.Walkable[to])
                        continue;
                    if (!wet || occupied[to])
                        break;
                    AddCrossing(nav, from, to);
                    AddCrossing(nav, to, from);
                    // Prevent parallel bridge candidates a tile apart from producing a wide causeway.
                    for (var sy = Math.Max(0, y - 5); sy <= Math.Min(size - 1, ny + 5); sy++)
                    for (var sx = Math.Max(0, x - 5); sx <= Math.Min(size - 1, nx + 5); sx++)
                        occupied[plan.Index(sx, sy)] = true;
                    break;
                }
            }
        }

        var region = 0;
        var queue = new Queue<int>();
        for (var start = 0; start < nav.Walkable.Length; start++)
        {
            if (!nav.Walkable[start] || nav.Region[start] != 0)
                continue;
            nav.Region[start] = ++region;
            queue.Enqueue(start);
            while (queue.TryDequeue(out var index))
            {
                var x = index % size;
                var y = index / size;
                foreach (var (dx, dy) in Neighbors)
                {
                    var next = plan.Index(x + dx, y + dy);
                    if (nav.Walkable[next] && nav.Region[next] == 0)
                    {
                        nav.Region[next] = region;
                        queue.Enqueue(next);
                    }
                }
                if (!nav.Crossings.TryGetValue(index, out var crossings))
                    continue;
                foreach (var next in crossings)
                {
                    if (nav.Region[next] != 0)
                        continue;
                    nav.Region[next] = region;
                    queue.Enqueue(next);
                }
            }
        }
        return nav;
    }

    private static void AddCrossing(Navigation nav, int from, int to)
    {
        if (!nav.Crossings.TryGetValue(from, out var targets))
        {
            targets = new List<int>();
            nav.Crossings.Add(from, targets);
        }
        targets.Add(to);
    }

    private static bool IsDrySquare(CMUExpeditionPlan plan, Navigation nav, CMUExpeditionPoint p, int radius)
    {
        var left = p.X - radius;
        var bottom = p.Y - radius;
        var right = p.X + radius + 1;
        var top = p.Y + radius + 1;
        if (left < 1 || bottom < 1 || right >= plan.Size || top >= plan.Size)
            return false;
        var stride = plan.Size + 1;
        return nav.BlockedPrefix[top * stride + right] - nav.BlockedPrefix[bottom * stride + right] -
            nav.BlockedPrefix[top * stride + left] + nav.BlockedPrefix[bottom * stride + left] == 0;
    }

    private static bool TryPlaceSites(CMUExpeditionPlan plan, Navigation nav)
    {
        var candidates = new List<CMUExpeditionPoint>();
        for (var y = 15; y < plan.Size - 15; y += 3)
        for (var x = 15; x < plan.Size - 15; x += 3)
        {
            var point = new CMUExpeditionPoint(x, y);
            if (IsDrySquare(plan, nav, point, 6))
                candidates.Add(point);
        }
        CMUExpeditionPoint? landing = null;
        var bestScore = float.MaxValue;
        foreach (var point in candidates)
        {
            if (!IsDrySquare(plan, nav, point, CMUExpeditionPlan.LandingRadius))
                continue;
            var region = nav.Region[plan.Index(point.X, point.Y)];
            var distance = 0f;
            foreach (var target in candidates)
                if (nav.Region[plan.Index(target.X, target.Y)] == region)
                    distance = MathF.Max(distance, Distance(point, target));
            if (distance < plan.Size * 0.52f)
                continue;
            var score = Unit(plan.Seed, point.X, point.Y, 33) - distance / plan.Size * 0.25f;
            if (score >= bestScore)
                continue;
            bestScore = score;
            landing = point;
        }
        if (landing is not { } lz)
            return false;
        plan.Sites.Add(new(CMUExpeditionSiteKind.LandingZone, lz));
        var mainRegion = nav.Region[plan.Index(lz.X, lz.Y)];
        CMUExpeditionPoint? objective = null;
        bestScore = float.MinValue;
        foreach (var candidate in candidates)
        {
            var distance = Distance(candidate, lz);
            if (nav.Region[plan.Index(candidate.X, candidate.Y)] != mainRegion ||
                distance < plan.Size * 0.52f || !Fits(plan, candidate))
                continue;
            var score = Unit(plan.Seed, candidate.X, candidate.Y, 37) + distance / plan.Size;
            if (plan.Story == CMUExpeditionStory.CrashRecovery && plan.Biome == CMUExpeditionBiome.Mountain &&
                FindImpactTerrain(plan, candidate, CMUExpeditionTerrain.Cliff) != null)
                score += 3;
            if (plan.Story == CMUExpeditionStory.CrashRecovery && plan.Biome == CMUExpeditionBiome.Beach &&
                FindImpactTerrain(plan, candidate, CMUExpeditionTerrain.Water) != null)
                score += 3;
            if (score <= bestScore)
                continue;
            objective = candidate;
            bestScore = score;
        }
        if (objective is not { } recovery)
            return false;
        AddSite(plan, CMUExpeditionSiteKind.Recovery, recovery);
        var desired = 5 + (int) (Hash(plan.Seed, (int) plan.Story, 0, 38) % 5);
        for (var index = 2; index < desired; index++)
        {
            CMUExpeditionPoint? chosen = null;
            bestScore = float.MinValue;
            foreach (var candidate in candidates)
            {
                if (nav.Region[plan.Index(candidate.X, candidate.Y)] != mainRegion || !Fits(plan, candidate))
                    continue;
                var score = Unit(plan.Seed, candidate.X, candidate.Y, 40 + index);
                if (score <= bestScore)
                    continue;
                chosen = candidate;
                bestScore = score;
            }
            if (chosen is not { } center)
                break;
            // These are wilderness landmarks, not a chain of outposts around the recovery site.
            var kind = (CMUExpeditionSiteKind) ((int) CMUExpeditionSiteKind.Grove +
                Hash(plan.Seed, center.X, center.Y, 80 + (int) plan.Story) % 4);
            AddSite(plan, kind, center);
        }
        return plan.Sites.Count >= 5;
    }

    private static void StampBridge(CMUExpeditionPlan plan, CMUExpeditionPoint from, CMUExpeditionPoint to)
    {
        if (from.X > to.X || from.Y > to.Y)
            (from, to) = (to, from);
        var bridge = new CMUExpeditionBridge(from, to);
        if (plan.Bridges.Contains(bridge))
            return;
        plan.Bridges.Add(bridge);
        var dx = Math.Sign(to.X - from.X);
        var dy = Math.Sign(to.Y - from.Y);
        var length = Math.Abs(to.X - from.X) + Math.Abs(to.Y - from.Y);
        for (var step = 0; step <= length; step++)
        for (var offset = -1; offset <= 1; offset++)
        {
            var i = plan.Index(from.X + step * dx + offset * dy, from.Y + step * dy + offset * dx);
            plan.Terrain[i] = CMUExpeditionTerrain.Deck;
            plan.Paths[i] = true;
            plan.Reserved[i] = true;
        }
    }
}
