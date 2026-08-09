#nullable enable

using System.Collections.Generic;
using Content.Server.AU14.Scenario;
using NUnit.Framework;
using Robust.Shared.GameObjects;

namespace Content.Tests.Server._CMU14.Scenario;

[TestFixture]
public sealed class RoundWorldSpawnMarkerStoreTest
{
    [Test]
    public void CopiesTheSmallestCaseInsensitiveCandidateBucket()
    {
        var index = new RoundWorldSpawnMarkerStore();
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
        var index = new RoundWorldSpawnMarkerStore();
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
        var index = new RoundWorldSpawnMarkerStore();
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
        var index = new RoundWorldSpawnMarkerStore();
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

    [Test]
    public void CandidateCopyPreservesDiscoveryOrder()
    {
        var index = new RoundWorldSpawnMarkerStore();
        var first = new EntityUid(11);
        var second = new EntityUid(7);
        var third = new EntityUid(9);
        var candidates = new List<EntityUid>();

        index.AddTag(first, "force:third-party");
        index.AddTag(second, "force:third-party");
        index.AddTag(third, "force:third-party");

        Assert.That(index.TryCopyCandidates(["force:third-party"], candidates), Is.True);
        Assert.That(candidates, Is.EqualTo(new[] { first, second, third }));
    }

    [Test]
    public void MissingBucketClearsCallerOwnedDestination()
    {
        var index = new RoundWorldSpawnMarkerStore();
        var candidates = new List<EntityUid> { new(99) };

        Assert.That(index.TryCopyCandidates(["missing"], candidates), Is.False);
        Assert.That(candidates, Is.Empty);
    }

    [Test]
    public void RemovingManyMarkersPreservesSurvivorOrderAcrossCompaction()
    {
        var index = new RoundWorldSpawnMarkerStore();
        var candidates = new List<EntityUid>();
        var survivors = new List<EntityUid>();

        for (var i = 1; i <= 96; i++)
        {
            var uid = new EntityUid(i);
            index.AddTag(uid, "force:hostile");
            if (i % 3 == 0)
                survivors.Add(uid);
            else
                index.RemoveMarker(uid);
        }

        Assert.That(index.TryCopyCandidates(["force:hostile"], candidates), Is.True);
        Assert.That(candidates, Is.EqualTo(survivors));
    }
}
