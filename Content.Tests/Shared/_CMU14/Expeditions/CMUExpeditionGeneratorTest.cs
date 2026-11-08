using System;
using System.Collections.Generic;
using System.Linq;
using Content.Shared.CMU14.Expeditions;
using NUnit.Framework;

namespace Content.Tests.Shared._CMU14.Expeditions;

[TestFixture]
public sealed class CMUExpeditionGeneratorTest
{
    public static IEnumerable<TestCaseData> Variants()
    {
        foreach (var biome in Enum.GetValues<CMUExpeditionBiome>())
        foreach (var landform in Enum.GetValues<CMUExpeditionLandform>())
        foreach (var story in Enum.GetValues<CMUExpeditionStory>())
            yield return new TestCaseData(biome, landform, story);
    }

    [TestCaseSource(nameof(Variants))]
    public void LandingAndAllSitesRemainReachable(CMUExpeditionBiome biome, CMUExpeditionLandform landform, CMUExpeditionStory story)
    {
        foreach (var size in new[] { 128, 140, 196 })
        foreach (var seed in new[] { int.MinValue, -1, 42, int.MaxValue })
        {
            var context = $"seed={seed}, size={size}, {biome}/{landform}/{story}";
            CMUExpeditionPlan plan = null;
            Assert.DoesNotThrow(() => plan = CMUExpeditionGenerator.Generate(seed, biome, landform, story, size), context);
            Assert.That(plan.Terrain, Has.Length.EqualTo(size * size), context);
            Assert.That(plan.Sites.Count, Is.InRange(5, 9), context);
            Assert.That(plan.Props.Count(p => p == CMUExpeditionProp.Recovery), Is.EqualTo(1), context);
            Assert.That(plan.Terrain.Where((terrain, i) =>
                terrain is CMUExpeditionTerrain.Deck or CMUExpeditionTerrain.Structure &&
                plan.Props[i] is CMUExpeditionProp.Tree or CMUExpeditionProp.Rock or CMUExpeditionProp.Boulder), Is.Empty,
                $"Natural obstacles must not intersect boardwalks or site interiors: {context}");
            var lz = plan.LandingZone;
            for (var y = -CMUExpeditionPlan.LandingRadius; y <= CMUExpeditionPlan.LandingRadius; y++)
            for (var x = -CMUExpeditionPlan.LandingRadius; x <= CMUExpeditionPlan.LandingRadius; x++)
            {
                var index = plan.Index(lz.X + x, lz.Y + y);
                Assert.That(plan.Props[index], Is.EqualTo(CMUExpeditionProp.None), context);
                Assert.That(plan.Terrain[index], Is.Not.EqualTo(CMUExpeditionTerrain.Water), context);
                Assert.That(plan.Terrain[index], Is.Not.EqualTo(CMUExpeditionTerrain.Cliff), context);
                Assert.That(plan.Reserved[index], Is.True, context);
            }

            // Independent four-neighbor flood fill: water and every collider are treated as blocked.
            // This proves equipment can travel on dry paths, even though the water can also be forded.
            var visited = Flood(plan);
            foreach (var site in plan.Sites.Skip(1))
            {
                var center = site.Center;
                // Leave room to approach the target from all four directions, not just see it.
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    Assert.That(visited[plan.Index(center.X + dx, center.Y + dy)], Is.True, context);
            }
            var target = plan.Objective;
            var distance = Math.Sqrt(Math.Pow(lz.X - target.X, 2) + Math.Pow(lz.Y - target.Y, 2));
            Assert.That(distance, Is.GreaterThanOrEqualTo(size * 0.52f), context);
            Assert.That(plan.Routes.Count(r => r.From == 1 || r.To == 1), Is.GreaterThanOrEqualTo(2), context);
            for (var i = 0; i < size; i++)
            {
                foreach (var edge in new[] { plan.Index(i, 0), plan.Index(i, size - 1), plan.Index(0, i), plan.Index(size - 1, i) })
                    Assert.That(plan.Props[edge], Is.EqualTo(plan.Terrain[edge] == CMUExpeditionTerrain.Cliff
                        ? CMUExpeditionProp.Rock : CMUExpeditionProp.Boundary), context);
            }
            AssertTerrainIsPreserved(plan, context);
            var cliffs = plan.BaseTerrain.Count(t => t == CMUExpeditionTerrain.Cliff);
            Assert.That(plan.Props.Count(p => p != CMUExpeditionProp.None),
                Is.LessThan(cliffs + (size * size - cliffs) / 4 + size * 4 + 300), context);
        }
    }

    [Test]
    public void SeedReplaysExactlyAndOtherSeedsChangeGeography()
    {
        var first = Make(42);
        Make(999); // Unrelated generation must not influence the next replay.
        var replay = Make(42);
        Assert.That(replay.Terrain, Is.EqualTo(first.Terrain));
        Assert.That(replay.BaseTerrain, Is.EqualTo(first.BaseTerrain));
        Assert.That(replay.TerrainAttempt, Is.EqualTo(first.TerrainAttempt));
        Assert.That(replay.Bridges, Is.EqualTo(first.Bridges));
        Assert.That(replay.Props, Is.EqualTo(first.Props));
        Assert.That(replay.Details, Is.EqualTo(first.Details));
        Assert.That(replay.WaterDepth, Is.EqualTo(first.WaterDepth));
        Assert.That(replay.Sites, Is.EqualTo(first.Sites));
        Assert.That(replay.Reserved, Is.EqualTo(first.Reserved));
        Assert.That(replay.Paths, Is.EqualTo(first.Paths));
        Assert.That(replay.Routes, Is.EqualTo(first.Routes));
        Assert.That(replay.Scorched, Is.EqualTo(first.Scorched));
        Assert.That(replay.WreckObjects, Is.EquivalentTo(first.WreckObjects));
        Assert.That(replay.WreckFloors, Is.EquivalentTo(first.WreckFloors));
        Assert.That(replay.Features, Is.EqualTo(first.Features));
        Assert.That(replay.FirePockets, Is.EqualTo(first.FirePockets));
        var different = Make(43);
        Assert.That(first.Terrain.Where((t, i) => t != different.Terrain[i]).Count(), Is.GreaterThan(first.Terrain.Length / 5));
    }

    private static CMUExpeditionPlan Make(int seed) => CMUExpeditionGenerator.Generate(seed,
        CMUExpeditionBiome.Woodland, CMUExpeditionLandform.RiverValley, CMUExpeditionStory.CrashRecovery);

    private static void AssertTerrainIsPreserved(CMUExpeditionPlan plan, string context)
    {
        var deck = new HashSet<int>();
        foreach (var bridge in plan.Bridges)
        {
            Assert.That(bridge.From.X == bridge.To.X ^ bridge.From.Y == bridge.To.Y, Is.True,
                $"Bridges must be straight and nonzero: {context}");
            var dx = Math.Sign(bridge.To.X - bridge.From.X);
            var dy = Math.Sign(bridge.To.Y - bridge.From.Y);
            var span = Math.Abs(bridge.To.X - bridge.From.X) + Math.Abs(bridge.To.Y - bridge.From.Y);
            Assert.That(span, Is.InRange(1, CMUExpeditionPlan.MaximumBridgeSpan), context);
            foreach (var bank in new[] { bridge.From, bridge.To })
            for (var y = -1; y <= 1; y++)
            for (var x = -1; x <= 1; x++)
                Assert.That(plan.BaseTerrain[plan.Index(bank.X + x, bank.Y + y)],
                    Is.Not.AnyOf(CMUExpeditionTerrain.Water, CMUExpeditionTerrain.Cliff), $"Unsupported bridge bank: {context}");
            var water = 0;
            for (var step = 0; step <= span; step++)
            for (var offset = -1; offset <= 1; offset++)
            {
                var index = plan.Index(bridge.From.X + step * dx + offset * dy, bridge.From.Y + step * dy + offset * dx);
                deck.Add(index);
                water += plan.BaseTerrain[index] == CMUExpeditionTerrain.Water ? 1 : 0;
                Assert.That(plan.Terrain[index], Is.EqualTo(CMUExpeditionTerrain.Deck), context);
                Assert.That(plan.Props[index], Is.EqualTo(CMUExpeditionProp.None), context);
                Assert.That(plan.Paths[index], Is.True, context);
            }
            Assert.That(water, Is.GreaterThan(0), $"A bridge must cross water: {context}");
        }
        Assert.That(plan.Bridges.Distinct().Count(), Is.EqualTo(plan.Bridges.Count), context);
        var illegal = new List<int>();
        for (var i = 0; i < plan.Terrain.Length; i++)
        {
            var terrain = plan.Terrain[i];
            var original = plan.BaseTerrain[i];
            var prop = plan.Props[i];
            if (original == CMUExpeditionTerrain.Water &&
                (terrain != (deck.Contains(i) ? CMUExpeditionTerrain.Deck : CMUExpeditionTerrain.Water) ||
                 prop is not (CMUExpeditionProp.None or CMUExpeditionProp.Boundary)) ||
                original == CMUExpeditionTerrain.Cliff &&
                (terrain != CMUExpeditionTerrain.Cliff || prop != CMUExpeditionProp.Rock) ||
                terrain == CMUExpeditionTerrain.Deck && !deck.Contains(i) ||
                plan.Paths[i] && terrain is CMUExpeditionTerrain.Water or CMUExpeditionTerrain.Cliff)
                illegal.Add(i);
        }
        Assert.That(illegal, Is.Empty, $"Roads, sites, props, or bridges changed protected terrain: {context}");
    }

    private static bool[] Flood(CMUExpeditionPlan plan)
    {
        var visited = new bool[plan.Terrain.Length];
        var queue = new Queue<CMUExpeditionPoint>();
        queue.Enqueue(plan.LandingZone);
        visited[plan.Index(plan.LandingZone.X, plan.LandingZone.Y)] = true;
        while (queue.TryDequeue(out var p))
        {
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var x = p.X + dx;
                var y = p.Y + dy;
                if (x < 0 || y < 0 || x >= plan.Size || y >= plan.Size)
                    continue;
                var index = plan.Index(x, y);
                if (visited[index] || plan.Terrain[index] == CMUExpeditionTerrain.Water || plan.Props[index] != CMUExpeditionProp.None)
                    continue;
                visited[index] = true;
                queue.Enqueue(new(x, y));
            }
        }
        return visited;
    }
}
