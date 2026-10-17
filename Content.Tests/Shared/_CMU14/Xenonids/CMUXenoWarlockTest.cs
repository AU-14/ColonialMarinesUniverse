using System;
using System.Linq;
using System.Numerics;
using Content.Shared.CMU14.Threats.Mobs.Xeno.Caste.Warlock;
using Content.Shared.FixedPoint;
using Content.Shared.Physics;
using NUnit.Framework;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;

namespace Content.Tests.Shared.CMU14.Xenonids;

[TestFixture]
public sealed class CMUXenoWarlockTest
{
    [Test]
    public void GetPsychicCrushDamageScalesWithCompletedPulses()
    {
        Assert.That(CMUXenoWarlockSystem.GetPsychicCrushDamage(1), Is.EqualTo(40));
        Assert.That(CMUXenoWarlockSystem.GetPsychicCrushDamage(5), Is.EqualTo(100));
    }

    [Test]
    public void GetPsychicCrushDamageClampsToValidPulseRange()
    {
        Assert.That(CMUXenoWarlockSystem.GetPsychicCrushDamage(-1), Is.EqualTo(25));
        Assert.That(CMUXenoWarlockSystem.GetPsychicCrushDamage(8), Is.EqualTo(100));
    }

    [Test]
    public void PsychicCrushParalyzesOnlyOnHighPulseCharges()
    {
        Assert.That(CMUXenoWarlockSystem.GetPsychicCrushParalyzeDuration(0), Is.EqualTo(TimeSpan.Zero));
        Assert.That(CMUXenoWarlockSystem.GetPsychicCrushParalyzeDuration(2), Is.EqualTo(TimeSpan.Zero));
        Assert.That(CMUXenoWarlockSystem.GetPsychicCrushParalyzeDuration(3), Is.EqualTo(TimeSpan.Zero));
        Assert.That(CMUXenoWarlockSystem.GetPsychicCrushParalyzeDuration(4).TotalSeconds, Is.EqualTo(1.5).Within(0.001));
        Assert.That(CMUXenoWarlockSystem.GetPsychicCrushParalyzeDuration(5).TotalSeconds, Is.EqualTo(1.5).Within(0.001));
        Assert.That(CMUXenoWarlockSystem.GetPsychicCrushParalyzeDuration(8).TotalSeconds, Is.EqualTo(1.5).Within(0.001));
    }

    [Test]
    public void GetPsychicCrushCostScalesAndClampsToMaxPulseCost()
    {
        Assert.That(CMUXenoWarlockSystem.GetPsychicCrushCost(1), Is.EqualTo(FixedPoint2.New(40)));
        Assert.That(CMUXenoWarlockSystem.GetPsychicCrushCost(8), Is.EqualTo(FixedPoint2.New(200)));
    }

    [Test]
    public void PsychicCrushDebuffsScaleWithCompletedPulses()
    {
        Assert.That(CMUXenoWarlockSystem.GetPsychicCrushStaggerDuration(5).TotalSeconds, Is.EqualTo(2.5).Within(0.001));
        Assert.That(CMUXenoWarlockSystem.GetPsychicCrushSlowDuration(5).TotalSeconds, Is.EqualTo(5).Within(0.001));
    }

    [Test]
    public void WarlockChannelParticlesRenderFromWarlockCenter()
    {
        Assert.That(CMUXenoWarlockSystem.GetWarlockParticleRenderOffset(CMUXenoWarlockParticleEffect.PsychicBlastCharge), Is.EqualTo(Vector2.Zero));
        Assert.That(CMUXenoWarlockSystem.GetWarlockParticleRenderOffset(CMUXenoWarlockParticleEffect.PsychicCrushCharge), Is.EqualTo(Vector2.Zero));
    }

    [Test]
    public void WarlockChannelParticlesMoveTowardTargetLikeTgmc()
    {
        var motion = CMUXenoWarlockSystem.GetWarlockDirectedParticleMotion(new Vector2(0, 0), new Vector2(10, 0), 7f);

        Assert.That(motion, Is.Not.Null);
        Assert.That(motion!.Value.Velocity.X, Is.EqualTo(3.5f).Within(0.001));
        Assert.That(motion.Value.Velocity.Y, Is.EqualTo(0).Within(0.001));
        Assert.That(motion.Value.Gravity.X, Is.EqualTo(7f).Within(0.001));
        Assert.That(motion.Value.Gravity.Y, Is.EqualTo(0).Within(0.001));
    }

    [Test]
    public void WarlockChannelParticlesDoNotMoveWithoutDirection()
    {
        Assert.That(CMUXenoWarlockSystem.GetWarlockDirectedParticleMotion(Vector2.Zero, Vector2.Zero, 7f), Is.Null);
    }

    [Test]
    public void PsychicBlastDoesNotPredictDeleteNetworkedProjectileAtMaxRange()
    {
        Assert.That(CMUXenoWarlockSystem.ShouldDeletePsychicBlastProjectileOnFixedDistanceStop(isClient: true, isClientSide: false), Is.False);
        Assert.That(CMUXenoWarlockSystem.ShouldDeletePsychicBlastProjectileOnFixedDistanceStop(isClient: false, isClientSide: false), Is.True);
        Assert.That(CMUXenoWarlockSystem.ShouldDeletePsychicBlastProjectileOnFixedDistanceStop(isClient: true, isClientSide: true), Is.True);
    }

    [Test]
    public void PsychicBlastLaserPassesThroughGlass()
    {
        Assert.That(CMUXenoWarlockSystem.ShouldPsychicBlastIgnoreCollisionLayer((int) CollisionGroup.GlassLayer), Is.True);
        Assert.That(CMUXenoWarlockSystem.ShouldPsychicBlastIgnoreCollisionLayer((int) CollisionGroup.GlassAirlockLayer), Is.True);
        Assert.That(CMUXenoWarlockSystem.ShouldPsychicBlastIgnoreCollisionLayer((int) CollisionGroup.WallLayer), Is.False);
        Assert.That(CMUXenoWarlockSystem.ShouldPsychicBlastIgnoreCollisionLayer((int) CollisionGroup.MobLayer), Is.False);
    }

    [Test]
    public void PsychicCrushEndEffectsUseSmoothCancelAndHardDetonate()
    {
        Assert.That(CMUXenoWarlockSystem.GetPsychicCrushEndEffectPrototype(false), Is.EqualTo("CMUXenoPsychicCrushSmooth"));
        Assert.That(CMUXenoWarlockSystem.GetPsychicCrushEndEffectPrototype(true), Is.EqualTo("CMUXenoPsychicCrushHard"));
    }

    [Test]
    public void PsychicCrushAffectedOffsetsGrowWithChannelDuration()
    {
        var start = CMUXenoWarlockSystem.GetPsychicCrushAffectedOffsets(0).ToArray();
        var firstPulse = CMUXenoWarlockSystem.GetPsychicCrushAffectedOffsets(1).ToArray();
        var fullChannel = CMUXenoWarlockSystem.GetPsychicCrushAffectedOffsets(2).ToArray();

        Assert.That(start, Has.Length.EqualTo(1));
        Assert.That(firstPulse, Has.Length.EqualTo(5));
        Assert.That(fullChannel, Has.Length.EqualTo(13));
        Assert.That(fullChannel, Has.Member(new Vector2i(0, 0)));
        Assert.That(fullChannel, Has.Member(new Vector2i(2, 0)));
        Assert.That(fullChannel, Has.Member(new Vector2i(-2, 0)));
        Assert.That(fullChannel, Has.Member(new Vector2i(0, 2)));
        Assert.That(fullChannel, Has.Member(new Vector2i(0, -2)));
        Assert.That(fullChannel, Has.No.Member(new Vector2i(2, 2)));
        Assert.That(fullChannel, Has.No.Member(new Vector2i(-2, -2)));
    }

    [Test]
    public void PsychicCrushWarningOffsetsExpandAsPlusShells()
    {
        var firstPulse = CMUXenoWarlockSystem.GetPsychicCrushWarningOffsets(1).ToArray();

        Assert.That(firstPulse, Has.Length.EqualTo(4));
        Assert.That(firstPulse, Has.Member(new Vector2i(1, 0)));
        Assert.That(firstPulse, Has.Member(new Vector2i(-1, 0)));
        Assert.That(firstPulse, Has.Member(new Vector2i(0, 1)));
        Assert.That(firstPulse, Has.Member(new Vector2i(0, -1)));
        Assert.That(firstPulse, Has.No.Member(new Vector2i(1, 1)));
        Assert.That(firstPulse, Has.No.Member(new Vector2i(-1, -1)));
    }

    [Test]
    public void PsychicCrushManualActivationRequiresTwoCompletedPulses()
    {
        Assert.That(CMUXenoWarlockSystem.CanTriggerPsychicCrush(0), Is.False);
        Assert.That(CMUXenoWarlockSystem.CanTriggerPsychicCrush(1), Is.False);
        Assert.That(CMUXenoWarlockSystem.CanTriggerPsychicCrush(2), Is.True);
        Assert.That(CMUXenoWarlockSystem.GetPsychicCrushResolvedPulses(2), Is.EqualTo(2));
        Assert.That(CMUXenoWarlockSystem.GetPsychicCrushResolvedPulses(5), Is.EqualTo(5));
    }

    [TestCase(CMUXenoWarlockChannelKind.PsychicCrush)]
    [TestCase(CMUXenoWarlockChannelKind.PsychicBlast)]
    [TestCase(CMUXenoWarlockChannelKind.PsychicShield)]
    public void WarlockAbilitiesShowOwnerChannelEffect(CMUXenoWarlockChannelKind kind)
    {
        Assert.That(CMUXenoWarlockSystem.ShouldShowWarlockChannelEffect(kind), Is.True);
    }

    [Test]
    public void PsychicShieldCreatesSingleHalfTileForwardCatcher()
    {
        var offsets = CMUXenoWarlockSystem.GetPsychicShieldOffsets(Direction.North).ToArray();

        Assert.That(offsets, Is.EqualTo(new[] { new Vector2(0, 1f) }));
        Assert.That(CMUXenoWarlockSystem.GetPsychicShieldCenterOffset(Direction.North), Is.EqualTo(new Vector2(0, 1f)));
    }

    [Test]
    public void PsychicShieldBreaksAtFrozenProjectileCapacity()
    {
        Assert.That(CMUXenoWarlockSystem.ShouldPsychicShieldBreakFromFrozenProjectiles(7, 8), Is.False);
        Assert.That(CMUXenoWarlockSystem.ShouldPsychicShieldBreakFromFrozenProjectiles(8, 8), Is.True);
        Assert.That(CMUXenoWarlockSystem.ShouldPsychicShieldBreakFromFrozenProjectiles(9, 8), Is.True);
        Assert.That(CMUXenoWarlockSystem.ShouldPsychicShieldBreakFromFrozenProjectiles(8, 0), Is.False);
    }

    [Test]
    public void PsychicShieldFadesWithRemainingIntegrity()
    {
        Assert.That(CMUXenoWarlockSystem.GetPsychicShieldAlpha(FixedPoint2.New(650), FixedPoint2.New(650)), Is.EqualTo(1).Within(0.001));
        Assert.That(CMUXenoWarlockSystem.GetPsychicShieldAlpha(FixedPoint2.New(325), FixedPoint2.New(650)), Is.EqualTo(0.5).Within(0.001));
        Assert.That(CMUXenoWarlockSystem.GetPsychicShieldAlpha(FixedPoint2.New(-25), FixedPoint2.New(650)), Is.EqualTo(0).Within(0.001));
        Assert.That(CMUXenoWarlockSystem.GetPsychicShieldAlpha(FixedPoint2.New(800), FixedPoint2.New(650)), Is.EqualTo(1).Within(0.001));
    }

    [Test]
    public void PsychicShieldSegmentSyncsRuntimeFreezeState()
    {
        Assert.That(typeof(CMUXenoPsychicShieldSegmentComponent).IsDefined(typeof(NetworkedComponentAttribute), false), Is.True);
    }

    [Test]
    public void PsychicShieldDoesNotCancelOnFacingChangeOnly()
    {
        Assert.That(CMUXenoWarlockSystem.ShouldPsychicShieldCancelOnMove(Vector2.Zero, Vector2.Zero, false), Is.False);
        Assert.That(CMUXenoWarlockSystem.ShouldPsychicShieldCancelOnMove(Vector2.Zero, new Vector2(0.25f, 0), false), Is.True);
        Assert.That(CMUXenoWarlockSystem.ShouldPsychicShieldCancelOnMove(Vector2.Zero, Vector2.Zero, true), Is.True);
    }

    [Test]
    public void PsychicShieldMovementCancelIsServerAuthoritativeAndAllowsStartupGrace()
    {
        Assert.That(CMUXenoWarlockSystem.ShouldPsychicShieldApplyMoveCancel(isClient: true), Is.False);
        Assert.That(CMUXenoWarlockSystem.ShouldPsychicShieldApplyMoveCancel(isClient: false), Is.True);

        Assert.That(
            CMUXenoWarlockSystem.ShouldPsychicShieldCancelOnMove(
                Vector2.Zero,
                new Vector2(0.25f, 0),
                false,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(2)),
            Is.False);

        Assert.That(
            CMUXenoWarlockSystem.ShouldPsychicShieldCancelOnMove(
                Vector2.Zero,
                new Vector2(0.25f, 0),
                false,
                TimeSpan.FromSeconds(3),
                TimeSpan.FromSeconds(2)),
            Is.True);
    }

    [Test]
    public void PsychicBlastKnocksBackAffectedTargets()
    {
        Assert.That(CMUXenoWarlockSystem.GetPsychicBlastKnockbackDirection(Vector2.Zero, new Vector2(2, 0), Vector2.Zero), Is.EqualTo(Vector2.UnitX));
        Assert.That(CMUXenoWarlockSystem.GetPsychicBlastKnockbackDirection(Vector2.Zero, Vector2.Zero, new Vector2(0, -8)), Is.EqualTo(-Vector2.UnitY));
    }

    [Test]
    public void PsychicShieldBlastCoversThreeByTwoForwardArea()
    {
        var offsets = CMUXenoWarlockSystem.GetPsychicShieldBlastOffsets(Direction.North).ToArray();

        Assert.That(offsets, Has.Member(new Vector2i(-1, 1)));
        Assert.That(offsets, Has.Member(new Vector2i(1, 2)));
        Assert.That(offsets, Has.Length.EqualTo(6));
    }

    [Test]
    public void PsychicShieldReflectsProjectileAcrossShieldFace()
    {
        var reflected = CMUXenoWarlockSystem.ReflectProjectileVelocity(new Vector2(0, -10), Direction.North);

        Assert.That(reflected.X, Is.EqualTo(0).Within(0.001));
        Assert.That(reflected.Y, Is.EqualTo(10).Within(0.001));
    }

    [Test]
    public void PsychicShieldReflectionKeepsSpeedAndAddsNoScatter()
    {
        var reflected = CMUXenoWarlockSystem.ReflectProjectileVelocity(new Vector2(6, -8), Direction.North);

        Assert.That(reflected.Length(), Is.EqualTo(10).Within(0.001));
        Assert.That(reflected.X, Is.EqualTo(6).Within(0.001));
        Assert.That(reflected.Y, Is.EqualTo(8).Within(0.001));
    }

    [Test]
    public void PsychicShieldOnlyCatchesProjectilesFromFront()
    {
        Assert.That(CMUXenoWarlockSystem.IsProjectileIncomingFromFront(new Vector2(0, -10), Direction.North), Is.True);
        Assert.That(CMUXenoWarlockSystem.IsProjectileIncomingFromFront(new Vector2(0, 10), Direction.North), Is.False);
    }

    [Test]
    public void PsychicShieldFreezesProjectilesOnTheOuterShieldFace()
    {
        var north = CMUXenoWarlockSystem.GetPsychicShieldFrozenProjectilePosition(
            Vector2.Zero,
            new Vector2(0.25f, -0.25f),
            Direction.North);
        var west = CMUXenoWarlockSystem.GetPsychicShieldFrozenProjectilePosition(
            Vector2.Zero,
            new Vector2(0.25f, 0.25f),
            Direction.West);

        Assert.That(north, Is.EqualTo(new Vector2(0.25f, 0.6f)));
        Assert.That(west, Is.EqualTo(new Vector2(-0.6f, 0.25f)));
    }

    [Test]
    public void PsychicShieldFreezePositionClampsToShieldWidth()
    {
        var stop = CMUXenoWarlockSystem.GetPsychicShieldFrozenProjectilePosition(
            Vector2.Zero,
            new Vector2(5f, -0.25f),
            Direction.North);

        Assert.That(stop, Is.EqualTo(new Vector2(1.5f, 0.6f)));
    }

    [Test]
    public void PsychicShieldCenterOffsetLeavesNearEdgeInFrontOfWarlock()
    {
        Assert.That(CMUXenoWarlockSystem.GetPsychicShieldCenterOffset(Direction.North), Is.EqualTo(new Vector2(0f, 1f)));
        Assert.That(CMUXenoWarlockSystem.GetPsychicShieldCenterOffset(Direction.South), Is.EqualTo(new Vector2(0f, -1f)));
        Assert.That(CMUXenoWarlockSystem.GetPsychicShieldCenterOffset(Direction.East), Is.EqualTo(new Vector2(1f, 0f)));
        Assert.That(CMUXenoWarlockSystem.GetPsychicShieldCenterOffset(Direction.West), Is.EqualTo(new Vector2(-1f, 0f)));
    }

    [Test]
    public void PsychicShieldFreezeSideEffectsAreAuthoritativeOnly()
    {
        Assert.That(CMUXenoWarlockSystem.ShouldPsychicShieldApplyAuthoritativeFreezeSideEffects(isClient: false), Is.True);
        Assert.That(CMUXenoWarlockSystem.ShouldPsychicShieldApplyAuthoritativeFreezeSideEffects(isClient: true), Is.False);
    }

    [Test]
    public void GetPlasmaTransferAmountClampsToTargetMissingPlasma()
    {
        var amount = CMUXenoWarlockSystem.GetPlasmaTransferAmount(
            FixedPoint2.New(250),
            FixedPoint2.New(500),
            FixedPoint2.New(625),
            FixedPoint2.New(700));

        Assert.That(amount, Is.EqualTo(FixedPoint2.New(75)));
    }

    [Test]
    public void GetPlasmaTransferAmountClampsToDonorAvailablePlasma()
    {
        var amount = CMUXenoWarlockSystem.GetPlasmaTransferAmount(
            FixedPoint2.New(250),
            FixedPoint2.New(60),
            FixedPoint2.New(300),
            FixedPoint2.New(700));

        Assert.That(amount, Is.EqualTo(FixedPoint2.New(60)));
    }

    [Test]
    public void GetPlasmaTransferAmountReturnsZeroWhenTargetIsFull()
    {
        var amount = CMUXenoWarlockSystem.GetPlasmaTransferAmount(
            FixedPoint2.New(250),
            FixedPoint2.New(500),
            FixedPoint2.New(700),
            FixedPoint2.New(700));

        Assert.That(amount, Is.EqualTo(FixedPoint2.Zero));
    }
}
