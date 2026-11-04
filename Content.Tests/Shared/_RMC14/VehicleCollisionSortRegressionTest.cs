using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.Shared.Vehicle;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Tests.Shared._RMC14;

[TestFixture]
public sealed class VehicleCollisionSortRegressionTest
{
    private delegate void SortHits(
        List<EntityUid> hits,
        Vector2 start,
        Vector2 target,
        Vector2 halfExtents,
        Func<EntityUid, Box2> getBounds);

    [TestCaseSource(nameof(ContactOrderingCases))]
    public void PreservesContactAndDeterministicTieBreakOrder(Box2 first, Box2 second, Vector2 target)
    {
        var earlier = new EntityUid(2);
        var later = new EntityUid(1);
        var hits = new List<EntityUid> { later, earlier };

        CreateSorter()(hits, Vector2.Zero, target, new Vector2(0.5f),
            uid => uid == earlier ? first : second);

        Assert.That(hits, Is.EqualTo(new[] { earlier, later }),
            "Geometric order must take priority over entity ID and broadphase enumeration order.");
    }

    private static IEnumerable<TestCaseData> ContactOrderingCases()
    {
        yield return new TestCaseData(
                new Box2(2, -0.5f, 14, 0.5f), new Box2(4, -0.5f, 5, 0.5f), new Vector2(10, 0))
            .SetName("FirstContactWinsEvenWhenItsCenterIsFartherAway");
        yield return new TestCaseData(
                new Box2(4, -0.5f, 5, 0.5f), new Box2(0, 5, 1, 6), new Vector2(10, 0))
            .SetName("SweptContactPrecedesNearbyNonContact");
        yield return new TestCaseData(
                new Box2(-0.5f, -0.5f, 1.5f, 0.5f), new Box2(-0.5f, -0.5f, 2.5f, 0.5f), new Vector2(10, 0))
            .SetName("EqualContactTimesUseProjectedCenterOrder");
        yield return new TestCaseData(
                new Box2(1, -0.25f, 1.5f, 0.25f), new Box2(2, -0.25f, 2.5f, 0.25f), Vector2.Zero)
            .SetName("StationaryChecksUseDistanceFromCenter");
        yield return new TestCaseData(
                new Box2(-2, -2, 2, 2), new Box2(-1, -2, 1, 2), new Vector2(10, 0))
            .SetName("EqualContactKeysUseLeftBound");
        yield return new TestCaseData(
                new Box2(-1, -2, 1, 2), new Box2(-1, -1, 1, 1), new Vector2(10, 0))
            .SetName("EqualLeftBoundsUseBottomBound");
        yield return new TestCaseData(
                new Box2(-1, -1, 1, 1), new Box2(-1, -1, 2, 1), new Vector2(0, 10))
            .SetName("EqualBottomBoundsUseRightBound");
        yield return new TestCaseData(
                new Box2(-1, -1, 1, 1), new Box2(-1, -1, 1, 2), new Vector2(10, 0))
            .SetName("EqualRightBoundsUseTopBound");
    }

    [Test]
    public void IdenticalBoundsUseEntityIdRegardlessOfInputOrder()
    {
        var hits = new List<EntityUid> { new(9), new(2), new(5) };
        CreateSorter()(hits, Vector2.Zero, Vector2.UnitX, Vector2.One,
            _ => new Box2(-1, -1, 1, 1));

        Assert.That(hits, Is.EqualTo(new[] { new EntityUid(2), new EntityUid(5), new EntityUid(9) }));
    }

    [Test]
    public void DensePassReadsEachBoundsOnce()
    {
        const int count = 256;
        var hits = Enumerable.Range(1, count).Reverse().Select(id => new EntityUid(id)).ToList();
        var reads = new Dictionary<EntityUid, int>();

        CreateSorter()(hits, Vector2.Zero, new Vector2(count + 1, 0), new Vector2(0.25f), uid =>
        {
            reads[uid] = reads.GetValueOrDefault(uid) + 1;
            return new Box2(uid.Id, -0.5f, uid.Id + 0.5f, 0.5f);
        });

        Assert.That(hits.Select(uid => uid.Id), Is.EqualTo(Enumerable.Range(1, count)));
        Assert.That(reads, Has.Count.EqualTo(count));
        Assert.That(reads.Values, Is.All.EqualTo(1),
            "Dense vehicle contacts must not rebuild world bounds in the sort comparator.");
    }

    [Test]
    public void LaterPassRefreshesMovedBoundsAndReplacesPreviousCandidates()
    {
        var sort = CreateSorter();
        var first = new EntityUid(1);
        var second = new EntityUid(2);
        var third = new EntityUid(3);
        var bounds = new Dictionary<EntityUid, Box2>
        {
            [first] = new(2, -1, 3, 1),
            [second] = new(6, -1, 7, 1),
            [third] = new(3, -1, 4, 1),
        };
        var hits = new List<EntityUid> { second, first };
        sort(hits, Vector2.Zero, new Vector2(10, 0), Vector2.One, uid => bounds[uid]);
        Assert.That(hits, Is.EqualTo(new[] { first, second }));

        bounds[first] = new Box2(8, -1, 9, 1);
        bounds[second] = new Box2(1, -1, 2, 1);
        sort(hits, Vector2.Zero, new Vector2(10, 0), Vector2.One, uid => bounds[uid]);
        Assert.That(hits, Is.EqualTo(new[] { second, first }));

        hits.Clear();
        hits.Add(first);
        hits.Add(third);
        sort(hits, Vector2.Zero, new Vector2(10, 0), Vector2.One, uid => bounds[uid]);
        Assert.That(hits, Is.EqualTo(new[] { third, first }));
    }

    [TestCase(0)]
    [TestCase(1)]
    public void PassWithoutComparisonsDoesNotReadBounds(int count)
    {
        var hits = Enumerable.Range(1, count).Select(id => new EntityUid(id)).ToList();
        CreateSorter()(hits, Vector2.Zero, Vector2.UnitX, Vector2.One,
            _ => throw new InvalidOperationException("This pass does not need sorting."));

        Assert.That(hits, Has.Count.EqualTo(count));
    }

    [Test]
    public void SeparateDepthBuffersPreserveOuterPassDuringBoundsCallback()
    {
        var outerSort = CreateSorter();
        var innerSort = CreateSorter();
        var outerHits = new List<EntityUid> { new(3), new(2), new(1) };
        var innerHits = new List<EntityUid> { new(10), new(20) };

        outerSort(outerHits, Vector2.Zero, new Vector2(10, 0), Vector2.One, uid =>
        {
            if (uid.Id == 2)
            {
                innerSort(innerHits, Vector2.Zero, new Vector2(10, 0), Vector2.One,
                    inner => new Box2(30 - inner.Id, -1, 31 - inner.Id, 1));
            }

            return new Box2(uid.Id, -1, uid.Id + 1, 1);
        });

        Assert.That(outerHits.Select(uid => uid.Id), Is.EqualTo(new[] { 1, 2, 3 }));
        Assert.That(innerHits.Select(uid => uid.Id), Is.EqualTo(new[] { 20, 10 }));
    }

    private static SortHits CreateSorter()
    {
        // Exercise the production buffer without making its implementation part
        // of the public gameplay API or requiring a running ECS world.
        var type = typeof(GridVehicleMoverSystem).GetNestedType("CollisionHitSortBuffer", BindingFlags.NonPublic)!;
        var buffer = Activator.CreateInstance(type, nonPublic: true)!;
        return type.GetMethod("Sort")!.CreateDelegate<SortHits>(buffer);
    }
}
