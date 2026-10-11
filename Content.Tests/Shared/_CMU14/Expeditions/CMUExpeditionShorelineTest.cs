using System;
using System.Linq;
using Content.Shared.CMU14.Expeditions;
using NUnit.Framework;

namespace Content.Tests.Shared._CMU14.Expeditions;

[TestFixture]
public sealed class CMUExpeditionShorelineTest
{
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void BanksSelectTheMatchingRmcEdgeAndBothCornerShapes(int turns)
    {
        var plan = Ocean();
        Bank(plan, 0, -1, turns);
        Assert.That(CMUExpeditionGenerator.GetWaterTile(plan, 64, 64),
            Is.EqualTo(new CMUExpeditionWaterTile(CMUExpeditionWaterKind.Edge, turns)));

        plan = Ocean();
        Bank(plan, 1, -1, turns);
        Assert.That(CMUExpeditionGenerator.GetWaterTile(plan, 64, 64),
            Is.EqualTo(new CMUExpeditionWaterTile(CMUExpeditionWaterKind.Corner, turns)));

        plan = Ocean();
        Bank(plan, 0, -1, turns);
        Bank(plan, 1, 0, turns);
        Assert.That(CMUExpeditionGenerator.GetWaterTile(plan, 64, 64),
            Is.EqualTo(new CMUExpeditionWaterTile(CMUExpeditionWaterKind.InnerCorner, turns)));
    }

    [Test]
    public void NarrowChannelsBridgesAndMapEdgesDoNotGetFalseBanks()
    {
        var plan = Ocean();
        Assert.That(CMUExpeditionGenerator.GetWaterTile(plan, 0, 0).Kind, Is.EqualTo(CMUExpeditionWaterKind.Deep));
        Bank(plan, -1, 0, 0);
        Bank(plan, 1, 0, 0);
        Assert.That(CMUExpeditionGenerator.GetWaterTile(plan, 64, 64).Kind, Is.EqualTo(CMUExpeditionWaterKind.Shallow));

        plan = Ocean();
        plan.Terrain[plan.Index(64, 63)] = CMUExpeditionTerrain.Deck;
        Assert.That(CMUExpeditionGenerator.GetWaterTile(plan, 64, 64).Kind, Is.EqualTo(CMUExpeditionWaterKind.Deep));
        Assert.That(CMUExpeditionGenerator.GetWaterTile(plan, 64, 63).Kind, Is.EqualTo(CMUExpeditionWaterKind.None));
        plan.Terrain[plan.Index(64, 64)] = CMUExpeditionTerrain.Ground;
        Assert.That(CMUExpeditionGenerator.GetWaterTile(plan, 64, 64).Kind, Is.EqualTo(CMUExpeditionWaterKind.None));
    }

    [Test]
    public void EveryNeighbourPatternRotatesConsistently()
    {
        var offsets = new[] { (0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1) };
        for (var mask = 0; mask < 256; mask++)
        {
            var reference = default(CMUExpeditionWaterTile);
            for (var turns = 0; turns < 4; turns++)
            {
                var plan = Ocean();
                for (var n = 0; n < offsets.Length; n++)
                    if ((mask & (1 << n)) != 0)
                        Bank(plan, offsets[n].Item1, offsets[n].Item2, turns);
                var tile = CMUExpeditionGenerator.GetWaterTile(plan, 64, 64);
                if (turns == 0)
                    reference = tile;
                Assert.That(tile.Kind, Is.EqualTo(reference.Kind), $"mask {mask}, rotation {turns}");
                if (tile.Kind is CMUExpeditionWaterKind.Edge or CMUExpeditionWaterKind.Corner or CMUExpeditionWaterKind.InnerCorner)
                    Assert.That(tile.QuarterTurns, Is.EqualTo((reference.QuarterTurns + turns) % 4));
            }
        }
    }

    [Test]
    public void GeneratedRiverUsesCornersAndScatteredFlowerPatches()
    {
        var plan = CMUExpeditionGenerator.Generate(42, CMUExpeditionBiome.Woodland,
            CMUExpeditionLandform.RiverValley, CMUExpeditionStory.CrashRecovery);
        var kinds = Enumerable.Range(0, plan.Terrain.Length)
            .Select(i => CMUExpeditionGenerator.GetWaterTile(plan, i % plan.Size, i / plan.Size).Kind).ToArray();
        Assert.That(kinds, Does.Contain(CMUExpeditionWaterKind.Edge));
        Assert.That(kinds, Does.Contain(CMUExpeditionWaterKind.Corner));
        Assert.That(kinds, Does.Contain(CMUExpeditionWaterKind.InnerCorner));
        Assert.That(plan.Details.Count(d => d == CMUExpeditionDetail.Flowers), Is.InRange(20, 1200));
        Assert.That(plan.Details.Where((d, i) => d == CMUExpeditionDetail.Flowers &&
            (plan.Paths[i] || plan.Terrain[i] is CMUExpeditionTerrain.Water or CMUExpeditionTerrain.Deck)), Is.Empty);
    }

    private static CMUExpeditionPlan Ocean()
    {
        var plan = new CMUExpeditionPlan(128, 0, 0, 0, 0);
        Array.Fill(plan.Terrain, CMUExpeditionTerrain.Water);
        Array.Fill(plan.BaseTerrain, CMUExpeditionTerrain.Water);
        return plan;
    }

    private static void Bank(CMUExpeditionPlan plan, int x, int y, int turns)
    {
        for (var n = 0; n < turns; n++)
            (x, y) = (-y, x);
        var i = plan.Index(64 + x, 64 + y);
        plan.Terrain[i] = CMUExpeditionTerrain.Ground;
        plan.BaseTerrain[i] = CMUExpeditionTerrain.Ground;
    }
}
