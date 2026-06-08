using System;
using Content.Shared._RMC14.TacticalMap;
using NUnit.Framework;

namespace Content.Tests.Shared._RMC14.TacticalMap;

[TestFixture]
public sealed class AreaInfoUpdateThrottleTest
{
    [Test]
    public void AllowsUpdateWhenIntervalElapsed()
    {
        Assert.That(
            AreaInfoUpdateThrottle.ShouldUpdate(
                TimeSpan.FromSeconds(3),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(1)),
            Is.True);
    }

    [Test]
    public void BlocksUpdateBeforeIntervalElapsed()
    {
        Assert.That(
            AreaInfoUpdateThrottle.ShouldUpdate(
                TimeSpan.FromSeconds(2.5),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(1)),
            Is.False);
    }
}
