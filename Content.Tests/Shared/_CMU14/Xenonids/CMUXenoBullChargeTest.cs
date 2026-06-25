using System;
using Content.Shared._CMU14.Threats.Mobs.Xeno.Caste.Bull;
using NUnit.Framework;

namespace Content.Tests.Shared._CMU14.Xenonids;

[TestFixture]
public sealed class CMUXenoBullChargeTest
{
    [Test]
    public void PlowKeepsChargingAfterImpact()
    {
        Assert.That(CMUXenoBullChargeSystem.ShouldStopAfterImpact(CMUXenoBullChargeMode.Plow), Is.False);
    }

    [Test]
    public void HeadbuttStopsChargingAfterImpact()
    {
        Assert.That(CMUXenoBullChargeSystem.ShouldStopAfterImpact(CMUXenoBullChargeMode.Headbutt), Is.True);
    }

    [Test]
    public void GoreStopsChargingAfterImpact()
    {
        Assert.That(CMUXenoBullChargeSystem.ShouldStopAfterImpact(CMUXenoBullChargeMode.Gore), Is.True);
    }

    [TestCase(CMUXenoBullChargeMode.Plow)]
    [TestCase(CMUXenoBullChargeMode.Headbutt)]
    [TestCase(CMUXenoBullChargeMode.Gore)]
    public void BullImpactMarksCollisionHandled(CMUXenoBullChargeMode mode)
    {
        Assert.That(CMUXenoBullChargeSystem.ShouldHandleImpact(mode), Is.True);
    }

    [TestCase(CMUXenoBullChargeMode.Plow)]
    [TestCase(CMUXenoBullChargeMode.Headbutt)]
    [TestCase(CMUXenoBullChargeMode.Gore)]
    public void BullImpactPlaysImpactSound(CMUXenoBullChargeMode mode)
    {
        Assert.That(CMUXenoBullChargeSystem.ShouldPlayImpactSound(mode), Is.True);
    }

    [Test]
    public void GoreSpraySoundOnlyPlaysWhenInjectionSucceeds()
    {
        Assert.That(CMUXenoBullChargeSystem.ShouldPlayGoreSpraySound(true), Is.True);
        Assert.That(CMUXenoBullChargeSystem.ShouldPlayGoreSpraySound(false), Is.False);
    }

    [Test]
    public void StageScaledSecondsReturnsZeroAtNoStage()
    {
        Assert.That(CMUXenoBullChargeSystem.GetStageScaledSeconds(TimeSpan.FromSeconds(2), 0, 8), Is.EqualTo(0));
    }

    [Test]
    public void StageScaledSecondsScalesLinearly()
    {
        Assert.That(CMUXenoBullChargeSystem.GetStageScaledSeconds(TimeSpan.FromSeconds(2), 4, 8), Is.EqualTo(1).Within(0.001));
    }

    [Test]
    public void GoreStaggerScalesWithChargeStage()
    {
        Assert.That(CMUXenoBullChargeSystem.GetGoreStaggerSeconds(4, 8, TimeSpan.FromSeconds(2)), Is.EqualTo(1).Within(0.001));
    }
}
