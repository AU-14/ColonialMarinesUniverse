using Content.Server.CMU14.Diagnostics.Performance;
using NUnit.Framework;

namespace Content.Tests.Server.CMU14.Diagnostics.Performance;

[TestFixture]
public sealed class CMUPerformanceRollingWindowTest
{
    [Test]
    public void CalculatesRatesFromIrregularScalarPoints()
    {
        var window = new CMUPerformanceRollingWindow(60);
        window.Add(10, Point(tick: 100, entities: 1000, created: 10, deleted: 5,
            components: 5000, added: 100, removed: 50, dirtied: 200));
        window.Add(15, Point(tick: 250, entities: 1010, created: 30, deleted: 10,
            components: 5100, added: 300, removed: 100, dirtied: 700));

        bool valid = window.TryGetRates(2, out CMUPerformanceRates rates);

        Assert.That(valid, Is.True);
        Assert.That(rates.SpanSeconds, Is.EqualTo(5));
        Assert.That(rates.TicksPerSecond, Is.EqualTo(30));
        Assert.That(rates.EntityGrowthPerSecond, Is.EqualTo(2));
        Assert.That(rates.EntityCreatesPerSecond, Is.EqualTo(4));
        Assert.That(rates.EntityDeletesPerSecond, Is.EqualTo(1));
        Assert.That(rates.EntityChurnPerSecond, Is.EqualTo(5));
        Assert.That(rates.ComponentGrowthPerSecond, Is.EqualTo(20));
        Assert.That(rates.ComponentAddsPerSecond, Is.EqualTo(40));
        Assert.That(rates.ComponentRemovesPerSecond, Is.EqualTo(10));
        Assert.That(rates.EntityDirtiesPerSecond, Is.EqualTo(100));
    }

    [Test]
    public void CounterResetDropsTheOldWindow()
    {
        var window = new CMUPerformanceRollingWindow(60);
        window.Add(0, Point(tick: 0, created: 100));
        window.Add(10, Point(tick: 300, created: 200));
        Assert.That(window.TryGetRates(1, out _), Is.True);

        window.Add(11, Point(tick: 301, created: 0));

        Assert.That(window.TryGetRates(1, out _), Is.False);
    }

    [Test]
    public void TimeResetDropsTheOldWindow()
    {
        var window = new CMUPerformanceRollingWindow(60);
        window.Add(10, Point(tick: 100));
        window.Add(20, Point(tick: 200));

        window.Add(5, Point(tick: 300));

        Assert.That(window.TryGetRates(1, out _), Is.False);
    }

    [Test]
    public void PartialTimeRegressionDropsTheOldWindow()
    {
        var window = new CMUPerformanceRollingWindow(60);
        window.Add(10, Point(tick: 100));
        window.Add(20, Point(tick: 200));

        window.Add(15, Point(tick: 300));

        Assert.That(window.TryGetRates(1, out _), Is.False);
    }

    private static CMUPerformanceCounterPoint Point(
        double tick = 0,
        int entities = 0,
        long created = 0,
        long deleted = 0,
        int components = 0,
        long added = 0,
        long removed = 0,
        long dirtied = 0)
    {
        return new(tick, entities, created, deleted, components, added, removed, dirtied);
    }
}
