using System;
using Content.Server.CMU14.Diagnostics.Performance;
using NUnit.Framework;

namespace Content.Tests.Server.CMU14.Diagnostics.Performance;

[TestFixture]
public sealed class CMURuntimeMemorySamplerTest
{
    [Test]
    public void FrequentStallReportsReuseMemoryCountersUntilTheNextSample()
    {
        using var sampler = new CMURuntimeMemorySampler();
        var first = sampler.Sample(TimeSpan.Zero);
        var allocation = new byte[2 * 1024 * 1024];
        Assert.That(sampler.Sample(TimeSpan.FromSeconds(1)), Is.EqualTo(first),
            "Incident frames must not cause per-frame Process.Refresh or GC memory sampling.");
        var second = sampler.Sample(TimeSpan.FromSeconds(5));
        GC.KeepAlive(allocation);
        Assert.Multiple(() =>
        {
            Assert.That(second.TotalAllocatedBytes - first.TotalAllocatedBytes, Is.GreaterThanOrEqualTo(allocation.Length));
            Assert.That(second.AllocatedBytesPerSecond, Is.GreaterThan(0));
            Assert.That(second.ManagedBytes, Is.GreaterThan(0));
            Assert.That(second.RssBytes, Is.GreaterThan(0).Or.EqualTo(-1));
            Assert.That(second.Gen2Collections, Is.GreaterThanOrEqualTo(first.Gen2Collections));
            Assert.That(second.At, Is.EqualTo(TimeSpan.FromSeconds(5)));
        });
    }
}
