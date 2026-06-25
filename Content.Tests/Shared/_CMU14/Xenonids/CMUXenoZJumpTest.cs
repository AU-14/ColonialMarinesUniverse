using System.Numerics;
using Content.Shared._CMU14.Threats.Mobs.Xeno.ZJump;
using NUnit.Framework;
using CMUXenoZJumpSystem = Content.Shared._CMU14.Threats.Mobs.Xeno.ZJump.CMUXenoZJumpSystem;

namespace Content.Tests.Shared._CMU14.Xenonids;

[TestFixture]
public sealed class CMUXenoZJumpTest
{
    [Test]
    public void ClampJumpVectorLimitsHorizontalRange()
    {
        Assert.That(CMUXenoZJumpSystem.ClampJumpVector(new Vector2(12, 0), 7), Is.EqualTo(new Vector2(7, 0)));
    }

    [Test]
    public void ClampJumpVectorKeepsZeroVector()
    {
        Assert.That(CMUXenoZJumpSystem.ClampJumpVector(Vector2.Zero, 7), Is.EqualTo(Vector2.Zero));
    }

    [Test]
    public void HasEnoughVerticalVelocityAllowsRequiredVelocity()
    {
        Assert.That(CMUXenoZJumpSystem.HasEnoughVerticalVelocity(6.5f, 6.5f), Is.True);
    }

    [Test]
    public void HasEnoughVerticalVelocityRejectsLowerVelocity()
    {
        Assert.That(CMUXenoZJumpSystem.HasEnoughVerticalVelocity(6.49f, 6.5f), Is.False);
    }

    [Test]
    public void PositiveZVelocityRequiresInAirPhysics()
    {
        Assert.That(CMUXenoZJumpSystem.ShouldSetInAirForUpwardMomentum(6.5f), Is.True);
        Assert.That(CMUXenoZJumpSystem.ShouldSetInAirForUpwardMomentum(0f), Is.False);
    }

    [Test]
    public void PositiveZVelocityRaisesTakeoffAboveGroundSnap()
    {
        Assert.That(CMUXenoZJumpSystem.ShouldRaiseZJumpTakeoffLocalPosition(6.5f, 0f), Is.True);
        Assert.That(CMUXenoZJumpSystem.ShouldRaiseZJumpTakeoffLocalPosition(6.5f, CMUXenoZJumpSystem.ZJumpTakeoffLocalPosition), Is.False);
        Assert.That(CMUXenoZJumpSystem.ShouldRaiseZJumpTakeoffLocalPosition(0f, 0f), Is.False);
        Assert.That(CMUXenoZJumpSystem.ZJumpTakeoffLocalPosition, Is.GreaterThan(0.05f));
    }

    [Test]
    public void ZJumpRequiresZMapWithUpperLevel()
    {
        Assert.That(CMUXenoZJumpSystem.CanUseZJumpMap(false, false), Is.False);
        Assert.That(CMUXenoZJumpSystem.CanUseZJumpMap(true, false), Is.False);
        Assert.That(CMUXenoZJumpSystem.CanUseZJumpMap(true, true), Is.True);
    }

    [Test]
    public void DefaultTrajectoryUsesVisibleTakeoff()
    {
        var component = new CMUXenoZJumpComponent();

        Assert.That(CMUXenoZJumpSystem.ZJumpTakeoffLocalPosition, Is.GreaterThanOrEqualTo(0.25f));
        Assert.That(CMUXenoZJumpSystem.HasEnoughVerticalVelocity(component.ZVelocity, 7.5f), Is.True);
    }

    [Test]
    public void ZJumpTakeoffRequiresClearUpperLevel()
    {
        Assert.That(CMUXenoZJumpSystem.CanStartZJumpTakeoff(true, false), Is.True);
        Assert.That(CMUXenoZJumpSystem.CanStartZJumpTakeoff(true, true), Is.False);
        Assert.That(CMUXenoZJumpSystem.CanStartZJumpTakeoff(false, false), Is.False);
    }

    [Test]
    public void TakeoffDashVectorCapsForwardMovement()
    {
        Assert.That(
            CMUXenoZJumpSystem.GetTakeoffDashVector(new Vector2(7, 0), 1f),
            Is.EqualTo(new Vector2(1, 0)));

        Assert.That(
            CMUXenoZJumpSystem.GetTakeoffDashVector(new Vector2(0.5f, 0), 1f),
            Is.EqualTo(new Vector2(0.5f, 0)));
    }

    [Test]
    public void DefaultTakeoffDashIsLight()
    {
        var component = new CMUXenoZJumpComponent();

        Assert.That(CMUXenoZJumpSystem.IsLightTakeoffDash(component.TakeoffDashDistance, component.TakeoffDashSpeed), Is.True);
    }

    [Test]
    public void DefaultWindupRequiresDoAfter()
    {
        var component = new CMUXenoZJumpComponent();

        Assert.That(CMUXenoZJumpSystem.ShouldUseWindupDoAfter(component.Windup), Is.True);
    }
}