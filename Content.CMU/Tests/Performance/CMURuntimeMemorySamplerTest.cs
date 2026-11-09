using System;
using Content.Server.CMU14.Diagnostics.Performance;
using NUnit.Framework;

namespace Content.Tests.CMU14.Performance;

[TestFixture]
public sealed class CMURuntimeMemorySamplerTest
{
    [TestCase(100, 100, 5, null, "no-counter-progress")]
    [TestCase(100, 90, 5, null, "counter-reset")]
    [TestCase(100, 150, 0, null, "clock-reset")]
    [TestCase(100, 150, -5, null, "clock-reset")]
    [TestCase(100, 180, 4, 20.0, "observed")]
    public void CounterIntervalsPreserveAvailabilityAndRateUnits(
        long before, long after, double seconds, double? rate, string status)
    {
        var result = CMURuntimeMemorySampler.MeasureAllocationRate(before, after, TimeSpan.FromSeconds(seconds));
        Assert.That(result.Rate, Is.EqualTo(rate));
        Assert.That(result.Status, Is.EqualTo(status));
    }

    [Test]
    public void CounterResetRemainsUnavailableUntilThereIsAValidDelta()
    {
        using var sampler = new CMURuntimeMemorySampler();
        var initial = sampler.Sample(TimeSpan.Zero);
        var last = typeof(CMURuntimeMemorySampler).GetProperty(nameof(CMURuntimeMemorySampler.Last))!;
        last.SetValue(sampler, initial with { TotalAllocatedBytes = long.MaxValue });
        var payload = new byte[64 * 1024];
        var reset = sampler.Sample(TimeSpan.FromSeconds(5));
        GC.KeepAlive(payload);
        Assert.That(reset.AllocatedBytesPerSecond, Is.Null,
            "An invalid process counter must not claim zero allocation.");
        Assert.That(reset.AllocationRateStatus, Is.EqualTo("counter-reset"));
        Assert.That(reset.MainThreadAllocatedBytesPerSecond, Is.GreaterThanOrEqualTo(payload.Length / 5.0),
            "Game-thread allocation must remain measurable independently of the invalid process counter.");

        last.SetValue(sampler, reset with { TotalAllocatedBytes = 0 });
        var recovered = sampler.Sample(TimeSpan.FromSeconds(10));
        Assert.That(recovered.AllocatedBytesPerSecond, Is.GreaterThan(0),
            "A subsequent valid interval must resume measurement.");
        Assert.That(recovered.AllocationRateStatus, Is.EqualTo("observed"));
    }
}
