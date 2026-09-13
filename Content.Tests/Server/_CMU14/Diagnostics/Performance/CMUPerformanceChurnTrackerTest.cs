using System.Collections.Generic;
using System.Linq;
using Content.Server.CMU14.Diagnostics.Performance;
using NUnit.Framework;

namespace Content.Tests.Server.CMU14.Diagnostics.Performance;

[TestFixture]
public sealed class CMUPerformanceChurnTrackerTest
{
    [Test]
    public void AttributesNetPrototypeAndComponentGrowthWithoutEntityIds()
    {
        var tracker = new CMUPerformanceChurnTracker();
        tracker.Reset(
            10,
            new Dictionary<string, int> { ["Existing"] = 5 },
            new Dictionary<string, int> { ["Transform"] = 10 });
        CMUPerformanceChurnSnapshot baseline = tracker.Snapshot();

        tracker.EntityCreated("Existing", "1");
        tracker.EntityDeleted("Existing");
        tracker.EntityCreated("LeakingPrototype", "1");
        tracker.EntityCreated("LeakingPrototype", "1");
        tracker.ComponentAdded("LeakingComponent");
        tracker.ComponentAdded("LeakingComponent");
        tracker.ComponentRemoved("LeakingComponent");
        tracker.EntityDirtied();

        CMUPerformanceChurnSnapshot current = tracker.Snapshot();
        CMUPerformanceChurnRow prototype = CMUPerformanceChurnTracker
            .GetPrototypeRows(baseline, current, 10)
            .Single(row => row.Name == "LeakingPrototype");
        CMUPerformanceChurnRow component = CMUPerformanceChurnTracker
            .GetComponentRows(baseline, current, 10)
            .Single(row => row.Name == "LeakingComponent");
        CMUPerformanceChurnRow map = CMUPerformanceChurnTracker
            .GetMapCreationRows(baseline, current, 10)
            .Single(row => row.Name == "1");

        Assert.That(prototype.CreatedOrAdded, Is.EqualTo(2));
        Assert.That(prototype.DeletedOrRemoved, Is.Zero);
        Assert.That(prototype.Net, Is.EqualTo(2));
        Assert.That(prototype.Current, Is.EqualTo(2));
        Assert.That(component.CreatedOrAdded, Is.EqualTo(2));
        Assert.That(component.DeletedOrRemoved, Is.EqualTo(1));
        Assert.That(component.Net, Is.EqualTo(1));
        Assert.That(component.Current, Is.EqualTo(1));
        Assert.That(map.CreatedOrAdded, Is.EqualTo(3));
        Assert.That(current.EntitiesDirtied, Is.EqualTo(1));
        Assert.That(current.ComponentCount, Is.EqualTo(11));
    }

    [Test]
    public void ResetPreservesOnlyCurrentBoundedCounts()
    {
        var tracker = new CMUPerformanceChurnTracker();
        tracker.Reset(0, new Dictionary<string, int>(), new Dictionary<string, int>());
        tracker.EntityCreated("Prototype", "2");
        tracker.ComponentAdded("Component");
        CMUPerformanceChurnSnapshot beforeReset = tracker.Snapshot();

        tracker.Reset(beforeReset.ComponentCount, beforeReset.PrototypeCounts, beforeReset.ComponentCounts);
        CMUPerformanceChurnSnapshot afterReset = tracker.Snapshot();

        Assert.That(afterReset.PrototypeCounts["Prototype"], Is.EqualTo(1));
        Assert.That(afterReset.ComponentCounts["Component"], Is.EqualTo(1));
        Assert.That(afterReset.EntitiesCreated, Is.Zero);
        Assert.That(afterReset.ComponentsAdded, Is.Zero);
        Assert.That(afterReset.PrototypeCreates, Is.Empty);
        Assert.That(afterReset.ComponentAdds, Is.Empty);
        Assert.That(afterReset.MapCreates, Is.Empty);
    }
}
