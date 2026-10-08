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

    [Test]
    public void SeedsVarySiteCountsNetworkTopologyAndLandingPositions()
    {
        var siteCounts = new HashSet<int>();
        var networks = new HashSet<string>();
        var landing = new HashSet<CMUExpeditionPoint>();
        var leaves = false;
        var junctions = false;
        foreach (var seed in Enumerable.Range(0, 24))
        {
            var plan = Make(seed);
            siteCounts.Add(plan.Sites.Count);
            landing.Add(plan.LandingZone);
            var degrees = new int[plan.Sites.Count];
            foreach (var route in plan.Routes)
            {
                degrees[route.From]++;
                degrees[route.To]++;
            }
            leaves |= degrees.Contains(1);
            junctions |= degrees.Any(d => d >= 3);
            // Sort degrees so merely renumbering or rotating the same graph cannot pass.
            networks.Add(string.Join(",", degrees.OrderBy(d => d)));
            Assert.That(plan.Sites.Select(s => s.Center).Distinct().Count(), Is.EqualTo(plan.Sites.Count));
            var lz = plan.LandingZone;
            var natural = 0;
            for (var y = -CMUExpeditionPlan.LandingRadius; y <= CMUExpeditionPlan.LandingRadius; y++)
            for (var x = -CMUExpeditionPlan.LandingRadius; x <= CMUExpeditionPlan.LandingRadius; x++)
                if (plan.Terrain[plan.Index(lz.X + x, lz.Y + y)] is
                    CMUExpeditionTerrain.Ground or CMUExpeditionTerrain.Scrub or CMUExpeditionTerrain.Mud or
                    CMUExpeditionTerrain.Stone or CMUExpeditionTerrain.Beach)
                    natural++;
            var side = CMUExpeditionPlan.LandingRadius * 2 + 1;
            Assert.That(natural, Is.GreaterThan(side * side * 0.7), "LZ must retain natural ground, not a dirt square.");
        }
        Assert.That(siteCounts.Count, Is.GreaterThanOrEqualTo(3));
        Assert.That(networks.Count, Is.GreaterThanOrEqualTo(8));
        Assert.That(landing.Count, Is.GreaterThanOrEqualTo(20));
        Assert.That(leaves && junctions, Is.True, "The maps must support branches, not only cycles.");
    }

    [Test]
    public void WaterShapesDifferBeyondRotationOrMirroring()
    {
        var reference = Make(0);
        for (var seed = 1; seed <= 12; seed++)
        {
            var other = Make(seed);
            var bestOverlap = 0f;
            // Compare water silhouettes under every square symmetry: a rotated copy is not variety.
            for (var symmetry = 0; symmetry < 8; symmetry++)
            {
                var intersection = 0;
                var union = 0;
                for (var y = 0; y < reference.Size; y++)
                for (var x = 0; x < reference.Size; x++)
                {
                    var tx = symmetry >= 4 ? reference.Size - 1 - x : x;
                    var ty = y;
                    for (var turn = 0; turn < symmetry % 4; turn++)
                        (tx, ty) = (reference.Size - 1 - ty, tx);
                    var a = reference.Terrain[reference.Index(x, y)] == CMUExpeditionTerrain.Water;
                    var b = other.Terrain[other.Index(tx, ty)] == CMUExpeditionTerrain.Water;
                    if (a && b)
                        intersection++;
                    if (a || b)
                        union++;
                }
                Assert.That(union, Is.GreaterThan(300));
                bestOverlap = Math.Max(bestOverlap, intersection / (float) union);
            }
            Assert.That(bestOverlap, Is.LessThan(0.75f), $"Seed {seed} repeats the same water silhouette.");
        }
    }

    [Test]
    public void BiomesLandformsAndStoriesChangePhysicalLayout()
    {
        var woodland = Make(42);
        var tundra = CMUExpeditionGenerator.Generate(42, CMUExpeditionBiome.Tundra, woodland.Landform, woodland.Story);
        Assert.That(tundra.Props.Count(p => p == CMUExpeditionProp.Tree), Is.LessThan(woodland.Props.Count(p => p == CMUExpeditionProp.Tree) / 2));
        foreach (var landform in Enum.GetValues<CMUExpeditionLandform>().Where(l => l != woodland.Landform))
        {
            var variant = CMUExpeditionGenerator.Generate(42, woodland.Biome, landform, woodland.Story);
            Assert.That(variant.Terrain.Where((t, i) => t != woodland.Terrain[i]).Count(), Is.GreaterThan(500), landform.ToString());
        }
        foreach (var story in Enum.GetValues<CMUExpeditionStory>().Where(s => s != woodland.Story))
        {
            var variant = CMUExpeditionGenerator.Generate(42, woodland.Biome, woodland.Landform, story);
            Assert.That(variant.Sites, Is.Not.EqualTo(woodland.Sites), story.ToString());
            Assert.That(variant.Props, Is.Not.EqualTo(woodland.Props), story.ToString());
        }
    }

    [Test]
    public void BackwoodsStayVegetatedWithoutBecomingASettlement()
    {
        foreach (var seed in Enumerable.Range(0, 12))
        {
            var plan = Make(seed);
            Assert.That(plan.Sites.Skip(2).All(s => s.Kind >= CMUExpeditionSiteKind.Grove), Is.True);
            Assert.That(plan.Terrain.Where((t, i) => t == CMUExpeditionTerrain.Structure && !plan.WreckFloors.ContainsKey(i)),
                Is.Empty, "Structural flooring belongs to the crashed ship, not surrounding settlements.");
            var dry = 0;
            var dressed = 0;
            for (var i = 0; i < plan.Terrain.Length; i++)
            {
                var terrain = plan.Terrain[i];
                if (terrain is CMUExpeditionTerrain.Water or CMUExpeditionTerrain.Cliff or CMUExpeditionTerrain.Deck or CMUExpeditionTerrain.Structure)
                    Assert.That(plan.Details[i], Is.EqualTo(CMUExpeditionDetail.None), $"Floating or misplaced vegetation at {i}, seed {seed}");
                else
                {
                    dry++;
                    if (plan.Details[i] != CMUExpeditionDetail.None)
                        dressed++;
                }
                if (plan.Props[i] == CMUExpeditionProp.Rock)
                    Assert.That(terrain, Is.EqualTo(CMUExpeditionTerrain.Cliff), "Loose boulders must not become wall tiles.");
                if (terrain == CMUExpeditionTerrain.Structure || plan.Props[i] is CMUExpeditionProp.Hull or CMUExpeditionProp.Supply or CMUExpeditionProp.Relay)
                {
                    Assert.That(Math.Abs(i % plan.Size - plan.Objective.X), Is.LessThanOrEqualTo(14));
                    Assert.That(Math.Abs(i / plan.Size - plan.Objective.Y), Is.LessThanOrEqualTo(14));
                }
                if (terrain == CMUExpeditionTerrain.Water)
                    Assert.That(plan.WaterDepth[i], Is.InRange(1, 3));
                else
                    Assert.That(plan.WaterDepth[i], Is.Zero, "No water beneath paths, bridges or dry ground.");
                if (plan.Paths[i])
                    Assert.That(plan.Details[i], Is.AnyOf(CMUExpeditionDetail.None, CMUExpeditionDetail.Grass,
                        CMUExpeditionDetail.Pebbles, CMUExpeditionDetail.Litter, CMUExpeditionDetail.Ash));
            }
            Assert.That(dressed / (float) dry, Is.InRange(0.3f, 0.6f), "Break up the carpet while retaining a dressed forest floor.");
            Assert.That(plan.Details.Distinct().Count(), Is.GreaterThanOrEqualTo(7), "Use multiple vegetation layers and debris types.");
            Assert.That(plan.WaterDepth, Does.Contain((byte) 1));
            Assert.That(plan.WaterDepth, Does.Contain((byte) 3));
        }
    }

    [Test]
    public void CrashFamiliesAndWildfireScarsStayPhysicalDryAndAccessible()
    {
        var names = new HashSet<string>();
        var mountainImpacts = 0;
        foreach (var seed in Enumerable.Range(0, 24))
        {
            var plan = seed % 2 == 0 ? Make(seed) : CMUExpeditionGenerator.Generate(seed,
                CMUExpeditionBiome.Mountain, CMUExpeditionLandform.Highlands, CMUExpeditionStory.CrashRecovery);
            names.Add(plan.WreckName);
            Assert.That(plan.WreckObjects.Count, Is.GreaterThan(8), $"Recognizable hull pieces: {seed}");
            Assert.That(plan.WreckFloors.Count, Is.GreaterThan(12), $"Walkable ship interior: {seed}");
            Assert.That(plan.Scorched.Count(s => s), Is.GreaterThan(150), "The impact must affect the wider area.");
            foreach (var i in plan.WreckFloors.Keys.Concat(plan.WreckObjects.Keys))
                Assert.That(plan.BaseTerrain[i], Is.Not.AnyOf(CMUExpeditionTerrain.Water, CMUExpeditionTerrain.Cliff));
            if (plan.CrashImpact == CMUExpeditionCrashImpact.CliffStrike)
            {
                mountainImpacts++;
                Assert.That(plan.BaseTerrain.Where((t, i) => t == CMUExpeditionTerrain.Cliff &&
                    Math.Abs(i % plan.Size - plan.Objective.X) <= 12 && Math.Abs(i / plan.Size - plan.Objective.Y) <= 12), Is.Not.Empty);
            }
            Assert.That(plan.Features.Count, Is.GreaterThanOrEqualTo(4));
            Assert.That(plan.Props.Any(p => p is CMUExpeditionProp.FallenLog or CMUExpeditionProp.Campfire or
                CMUExpeditionProp.BurntFrame or CMUExpeditionProp.DiscardedPack), Is.True);
            foreach (var fire in plan.FirePockets)
            {
                Assert.That(Math.Abs(fire.X - plan.LandingZone.X) > CMUExpeditionPlan.LandingRadius + 3 ||
                    Math.Abs(fire.Y - plan.LandingZone.Y) > CMUExpeditionPlan.LandingRadius + 3, Is.True);
                Assert.That(plan.Props[plan.Index(fire.X, fire.Y)], Is.EqualTo(CMUExpeditionProp.None));
                Assert.That(plan.Paths[plan.Index(fire.X, fire.Y)], Is.False, "Hot spots must leave the equipment route clear.");
                Assert.That(plan.Terrain[plan.Index(fire.X, fire.Y)], Is.Not.AnyOf(CMUExpeditionTerrain.Water, CMUExpeditionTerrain.Cliff));
            }
            AssertTerrainIsPreserved(plan, $"crash seed {seed}");
        }
        Assert.That(names.Count, Is.EqualTo(4), "The original MULE carrier replaces the three stretched Fallujah layouts; gunship variants remain.");
        Assert.That(mountainImpacts, Is.GreaterThanOrEqualTo(10));
    }

    [TestCase(CMUExpeditionBiome.Woodland, CMUExpeditionLandform.RiverValley)]
    [TestCase(CMUExpeditionBiome.Mountain, CMUExpeditionLandform.Highlands)]
    [TestCase(CMUExpeditionBiome.Beach, CMUExpeditionLandform.Coast)]
    public void MuleKeepsACargoAisleAndRecognizableFuselage(CMUExpeditionBiome biome, CMUExpeditionLandform landform)
    {
        var carriers = 0;
        var rotations = new HashSet<int>();
        foreach (var seed in Enumerable.Range(36, 12))
        {
            var plan = CMUExpeditionGenerator.Generate(seed, biome, landform, CMUExpeditionStory.CrashRecovery);
            if (plan.WreckName != "cmu-expedition-wreck-mule")
                continue;
            carriers++;
            var site = plan.Sites[1];
            rotations.Add(site.Rotation);
            var context = $"MULE: {biome}, seed {seed}, turn {site.Rotation}";
            var reachable = Flood(plan);
            Assert.That(plan.WreckFloors.Count, Is.GreaterThan(90), context);
            Assert.That(plan.WreckObjects.Values.Count(p => p.Prototype.StartsWith("CMUExpeditionWreckCMAlamoWall")),
                Is.GreaterThan(40), context);
            Assert.That(plan.WreckObjects.Values.Count(p => p.Prototype == "CMUExpeditionMuleCargo"),
                Is.GreaterThanOrEqualTo(4), "The cargo compartment must retain its equipment pallets.");
            for (var y = -6; y <= 3; y++)
            for (var x = -1; x <= 1; x++)
            {
                var i = Index(x, y);
                Assert.That(plan.WreckFloors.ContainsKey(i), Is.True, $"Broken cargo keel: {context}");
                Assert.That(plan.Reserved[i], Is.True, context);
                Assert.That(plan.Props[i], Is.EqualTo(x == 0 && y == 0 ? CMUExpeditionProp.Recovery : CMUExpeditionProp.None), context);
                if (x != 0 || y != 0)
                    Assert.That(reachable[i], Is.True, $"Equipment cannot be extracted: {context}");
            }
            foreach (var side in new[] { -1, 1 })
            for (var y = 3; y <= 4; y++)
            {
                var i = Index(side * 4, y);
                Assert.That(plan.Props[i], Is.EqualTo(CMUExpeditionProp.None), $"Blocked personnel exit: {context}");
                Assert.That(reachable[i], Is.True, context);
            }
            foreach (var side in new[] { -1, 1 })
            for (var x = 5; x <= 6; x++)
            {
                var i = Index(side * x, 3);
                Assert.That(plan.Props[i], Is.EqualTo(CMUExpeditionProp.None), $"The exit must lead past the engine supports: {context}");
                Assert.That(reachable[i], Is.True, context);
            }
            foreach (var i in plan.WreckObjects.Keys)
                Assert.That(plan.Paths[i], Is.False, $"Trails should use designated openings: {context}");
            AssertTerrainIsPreserved(plan, context);

            int Index(int x, int y)
            {
                for (var turn = 0; turn < site.Rotation; turn++)
                    (x, y) = (-y, x);
                return plan.Index(site.Center.X + x, site.Center.Y + y);
            }
        }
        Assert.That(carriers, Is.GreaterThanOrEqualTo(3));
        Assert.That(rotations.Count, Is.GreaterThanOrEqualTo(2));
    }

    [TestCase(127)]
    [TestCase(197)]
    [TestCase(int.MaxValue)]
    public void InvalidSizesAreRejectedBeforeAllocation(int size)
    {
        Assert.Throws<ArgumentException>(() => CMUExpeditionGenerator.Generate(1, CMUExpeditionBiome.Woodland,
            CMUExpeditionLandform.RiverValley, CMUExpeditionStory.CrashRecovery, size));
    }

    [Test]
    public void UndefinedVariantsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => CMUExpeditionGenerator.Generate(1, (CMUExpeditionBiome) 99, 0, 0));
        Assert.Throws<ArgumentException>(() => CMUExpeditionGenerator.Generate(1, 0, (CMUExpeditionLandform) 99, 0));
        Assert.Throws<ArgumentException>(() => CMUExpeditionGenerator.Generate(1, 0, 0, (CMUExpeditionStory) 99));
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
