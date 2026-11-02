using System.Linq;
using Content.Shared.CMU14.CaveSurvey;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.CMU.CaveSurvey;

[TestFixture]
public sealed class CMUCaveSurveyDataTest
{
    [Test]
    public void ReadingOrderCannotReplaceNewerExcavationWithOldRock()
    {
        var tile = new Vector2i(-1, -5);
        var first = new CMUCaveSurveyData();
        first.Record([new(tile, -3, CMUReconMaterial.Rock), new(tile + new Vector2i(1, 0), -3, CMUReconMaterial.Ground)], 1);
        var second = new CMUCaveSurveyData();
        second.Record([new(tile, -3, CMUReconMaterial.Empty)], 2);
        var forward = first.Snapshot();
        forward.Merge(second);
        var reverse = second.Snapshot();
        reverse.Merge(first);
        foreach (var data in new[] { forward, reverse })
        {
            Assert.That(data.Get(tile, -3), Is.EqualTo(new CMUCaveSurveyCell(2, CMUReconMaterial.Empty)),
                "An observed shaft must erase older rock, even when the old map is read last.");
            Assert.That(data.Get(tile + new Vector2i(1, 0), -3).Material, Is.EqualTo(CMUReconMaterial.Ground));
            Assert.That(data.Get(tile, -4).Revision, Is.Zero, "Knowledge cannot leak between depths.");
            var version = data.Version;
            data.Merge(first);
            data.Merge(second);
            Assert.That(data.Version, Is.EqualTo(version), "Reading the same maps again must be idempotent.");
        }
    }

    [Test]
    public void WrittenMapAndReaderStayFrozenWhenExplorerRevisits()
    {
        var tile = new Vector2i(3, -4);
        var explorer = new CMUCaveSurveyData();
        explorer.Record([new(tile, -6, CMUReconMaterial.Rock)], 1);
        var map = explorer.Snapshot();
        var reader = new CMUCaveSurveyData();
        reader.Merge(map);
        explorer.Record([new(tile, -6, CMUReconMaterial.Ground), new(tile + new Vector2i(1, 0), -6, CMUReconMaterial.Stairs)], 2);
        Assert.That(map.Get(tile, -6).Material, Is.EqualTo(CMUReconMaterial.Rock));
        Assert.That(reader.Get(tile, -6).Material, Is.EqualTo(CMUReconMaterial.Rock));
        Assert.That(reader.Get(tile + new Vector2i(1, 0), -6).Revision, Is.Zero);
        map.Merge(explorer);
        Assert.That(reader.Get(tile, -6).Material, Is.EqualTo(CMUReconMaterial.Rock), "Rewriting a physical map must not remotely update its past readers.");
        reader.Merge(map);
        Assert.That(reader.Get(tile, -6), Is.EqualTo(new CMUCaveSurveyCell(2, CMUReconMaterial.Ground)));
        Assert.That(reader.Get(tile + new Vector2i(1, 0), -6).Material, Is.EqualTo(CMUReconMaterial.Stairs));
    }

    [Test]
    public void SectionClipsAtNegativeCoordinatesAndDoesNotInventUnknownCells()
    {
        var data = new CMUCaveSurveyData();
        data.Record([
            new(new Vector2i(-65, -1), -8, CMUReconMaterial.Rock),
            new(new Vector2i(-64, -1), -8, CMUReconMaterial.Stairs),
            new(new Vector2i(-1, -1), -1, CMUReconMaterial.Empty),
            new(new Vector2i(0, -1), -1, CMUReconMaterial.Ground),
            new(new Vector2i(-1, -1), -9, CMUReconMaterial.Water),
        ], 3);
        var section = data.Section(new Vector2i(-64, -64), 64, -8, 8).ToArray();
        Assert.That(section, Is.EquivalentTo(new[]
        {
            new CMUCaveSurveySample(new Vector2i(-64, -1), -8, CMUReconMaterial.Stairs),
            new CMUCaveSurveySample(new Vector2i(-1, -1), -1, CMUReconMaterial.Empty),
        }));
    }
}
