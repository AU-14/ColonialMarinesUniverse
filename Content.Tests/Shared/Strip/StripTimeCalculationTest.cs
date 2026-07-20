using System;
using Content.Shared.Strip.Components;
using NUnit.Framework;

namespace Content.Tests.Shared.Strip;

[TestFixture]
[TestOf(typeof(BaseBeforeStripEvent))]
public sealed class StripTimeCalculationTest
{
    [Test]
    public void UsesCompleteInitialAndAdditiveDurations()
    {
        var ev = new BeforeStripEvent(TimeSpan.FromSeconds(90.5))
        {
            Multiplier = 0.5f,
            Additive = TimeSpan.FromSeconds(1.25),
        };

        Assert.That(ev.Time, Is.EqualTo(TimeSpan.FromSeconds(46.5)));
    }

    [Test]
    public void ClampsNegativeDurationToZero()
    {
        var ev = new BeforeStripEvent(TimeSpan.FromSeconds(1))
        {
            Additive = TimeSpan.FromSeconds(-2),
        };

        Assert.That(ev.Time, Is.EqualTo(TimeSpan.Zero));
    }
}
