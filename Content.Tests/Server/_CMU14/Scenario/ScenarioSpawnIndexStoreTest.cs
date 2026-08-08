#nullable enable

using System.Collections.Generic;
using Content.Server.AU14.Scenario;
using NUnit.Framework;
using Robust.Shared.GameObjects;

namespace Content.Tests.Server._CMU14.Scenario;

[TestFixture]
public sealed class ScenarioSpawnIndexStoreTest
{
    [Test]
    public void CopiesTheSmallestCaseInsensitiveCandidateBucket()
    {
        var index = new ScenarioSpawnIndexStore();
        var commonOnly = new EntityUid(1);
        var rare = new EntityUid(2);
        var candidates = new List<EntityUid>();

        index.AddMarker(commonOnly);
        index.AddTag(commonOnly, "force:hostile");
        index.AddMarker(rare);
        index.AddTag(rare, "force:hostile");
        index.AddTag(rare, "bucket:Leader");

        Assert.That(
            index.TryCopyCandidates(["FORCE:HOSTILE", "bucket:leader"], candidates),
            Is.True);
        Assert.That(candidates, Is.EqualTo(new[] { rare }));
    }

    [Test]
    public void EmptyRequirementsReturnEveryMarkerOnce()
    {
        var index = new ScenarioSpawnIndexStore();
        var first = new EntityUid(1);
        var second = new EntityUid(2);
        var candidates = new List<EntityUid>();

        index.AddMarker(first);
        index.AddMarker(first);
        index.AddMarker(second);

        Assert.That(index.TryCopyCandidates([], candidates), Is.True);
        Assert.That(candidates, Is.EquivalentTo(new[] { first, second }));
    }

    [Test]
    public void ClearingDropsAllBuckets()
    {
        var index = new ScenarioSpawnIndexStore();
        var marker = new EntityUid(1);
        var candidates = new List<EntityUid>();

        index.AddMarker(marker);
        index.AddTag(marker, "force:hostile");
        index.Clear();

        Assert.That(index.TryCopyCandidates(["force:hostile"], candidates), Is.False);
        Assert.That(candidates, Is.Empty);
    }

    [Test]
    public void RemovingAMarkerDropsItFromEveryBucket()
    {
        var index = new ScenarioSpawnIndexStore();
        var removed = new EntityUid(1);
        var retained = new EntityUid(2);
        var candidates = new List<EntityUid>();

        index.AddMarker(removed);
        index.AddTag(removed, "force:hostile");
        index.AddTag(removed, "bucket:leader");
        index.AddMarker(retained);
        index.AddTag(retained, "force:hostile");

        Assert.That(index.RemoveMarker(removed), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(index.TryCopyCandidates(["bucket:leader"], candidates), Is.False);
            Assert.That(index.TryCopyCandidates(["force:hostile"], candidates), Is.True);
            Assert.That(candidates, Is.EqualTo(new[] { retained }));
        });
    }
}
