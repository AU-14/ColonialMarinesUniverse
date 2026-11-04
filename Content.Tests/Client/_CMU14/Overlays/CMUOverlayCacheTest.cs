using System;
using Content.Client.CMU14.Overlays;
using NUnit.Framework;

namespace Content.Tests.Client.CMU14.Overlays;

[TestFixture]
public sealed class CMUOverlayCacheTest
{
    [Test]
    public void OverflowRetainsRecentlyVisibleEntries()
    {
        var cache = new CMUOverlayCache<int, string>(3);
        cache.Set(1, "one");
        cache.Set(2, "two");
        cache.Set(3, "three");
        Assert.That(cache.TryGetValue(1, out _), Is.True);
        cache.Set(4, "four");

        Assert.Multiple(() =>
        {
            Assert.That(cache.Count, Is.EqualTo(3));
            Assert.That(cache.TryGetValue(2, out _), Is.False);
            Assert.That(cache.TryGetValue(1, out var one), Is.True);
            Assert.That(one, Is.EqualTo("one"));
            Assert.That(cache.TryGetValue(3, out var three), Is.True);
            Assert.That(three, Is.EqualTo("three"));
            Assert.That(cache.TryGetValue(4, out var four), Is.True);
            Assert.That(four, Is.EqualTo("four"));
        });
    }

    [Test]
    public void UpdatingAnEntryDoesNotConsumeAnotherSlot()
    {
        var cache = new CMUOverlayCache<int, string>(2);
        cache.Set(1, "old");
        cache.Set(2, "two");
        cache.Set(1, "new");
        cache.Set(3, "three");

        Assert.That(cache.Count, Is.EqualTo(2));
        Assert.That(cache.TryGetValue(1, out var value), Is.True);
        Assert.That(value, Is.EqualTo("new"));
        Assert.That(cache.TryGetValue(2, out _), Is.False);
    }

    [Test]
    public void WalkingPastNewEntitiesKeepsTheVisibleCrowdWarm()
    {
        const int capacity = 512;
        const int visible = 96;
        var cache = new CMUOverlayCache<int, int>(capacity);
        for (var i = 0; i < capacity; i++)
            cache.Set(i, i);

        for (var arrival = capacity; arrival < capacity + 2000; arrival++)
        {
            for (var i = 0; i < visible; i++)
            {
                Assert.That(cache.TryGetValue(i, out var value), Is.True,
                    "One arriving entity must not invalidate the remaining visible crowd.");
                Assert.That(value, Is.EqualTo(i));
            }
            cache.Set(arrival, arrival);
        }

        Assert.That(cache.Count, Is.EqualTo(capacity));
    }

    [Test]
    public void FullCacheReplacementsReuseBookkeeping()
    {
        var cache = new CMUOverlayCache<int, int>(32);
        for (var i = 0; i < 5000; i++)
        {
            cache.Set(i, i);
            cache.TryGetValue(i, out _);
        }

        // The counter also sees one-time runtime work on this thread (tiered JIT, on-stack replacement), which can
        // land inside a window depending on runner timing. Bookkeeping allocations would repeat in every window
        // (10000 replacements is ~480 KB of nodes), so one allocation-free window out of three proves reuse.
        var allocated = long.MaxValue;
        var next = 5000;
        for (var window = 0; window < 3 && allocated != 0; window++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var end = next + 10000; next < end; next++)
            {
                cache.Set(next, next);
                cache.TryGetValue(next, out _);
            }
            allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            TestContext.Progress.WriteLine($"CMU_OVERLAY_CACHE window={window} replacements=10000 allocated={allocated}");
        }

        Assert.That(allocated, Is.Zero);
        Assert.That(cache.Count, Is.EqualTo(32));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void RejectsNonpositiveCapacity(int capacity)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new CMUOverlayCache<int, int>(capacity));
}
