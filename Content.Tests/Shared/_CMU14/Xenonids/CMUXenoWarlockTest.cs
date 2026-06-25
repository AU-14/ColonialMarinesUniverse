using System;
using System.Linq;
using System.Numerics;
using CMUDrawDepth = Content.Shared.DrawDepth.DrawDepth;
using Content.Shared.Actions;
using Content.Shared._CMU14.Threats.Mobs.Xeno.Caste.Warlock;
using Content.Shared.FixedPoint;
using Content.Shared.Physics;
using NUnit.Framework;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;

namespace Content.Tests.Shared._CMU14.Xenonids;

[TestFixture]
public sealed class CMUXenoWarlockTest
{
    [Test]
    public void GetPsychicCrushDamageScalesWithCompletedPulses()
    {
        Assert.That(CMUXenoWarlockSystem.GetPsychicCrushDamage(1), Is.EqualTo(40));
        Assert.That(CMUXenoWarlockSystem.GetPsychicCrushDamage(5), Is.EqualTo(80));
    }

    [Test]
    public void GetPsychicCrushDamageClampsToValidPulseRange()
    {
        Assert.That(CMUXenoWarlockSystem.GetPsychicCrushDamage(-1), Is.EqualTo(30));
        Assert.That(CMUXenoWarlockSystem.GetPsychicCrushDamage(8), Is.EqualTo(80));
    }

    [Test]
    public void GetPsychicCrushCostScalesAndClampsToMaxPulseCost()
    {
        Assert.That(CMUXenoWarlockSystem.GetPsychicCrushCost(1), Is.EqualTo(FixedPoint2.New(40)));
        Assert.That(CMUXenoWarlockSystem.GetPsychicCrushCost(8), Is.EqualTo(FixedPoint2.New(200)));
    }

    [Test]
    public void PsychicCrushTargetsTurfInsteadOfEntity()
    {
        Assert.That(new CMUXenoPsychicCrushActionEvent(), Is.InstanceOf<WorldTargetActionEvent>());
    }

    [Test]
    public void PsychicCrushTargetsFurtherThanBlast()
    {
        Assert.That(CMUXenoWarlockSystem.GetPsychicCrushTargetRange(), Is.EqualTo(9).Within(0.001));
    }

    [Test]
    public void PsychicCrushDebuffsScaleWithCompletedPulses()
    {
        Assert.That(CMUXenoWarlockSystem.GetPsychicCrushStaggerDuration(5).TotalSeconds, Is.EqualTo(10));
        Assert.That(CMUXenoWarlockSystem.GetPsychicCrushSlowDuration(5).TotalSeconds, Is.EqualTo(15));
    }

    [Test]
    public void PsychicBlastChannelsBeforeFiring()
    {
        Assert.That(CMUXenoWarlockSystem.GetPsychicBlastChargeDuration().TotalSeconds, Is.EqualTo(1).Within(0.001));
    }

    [Test]
    public void PsychicBlastModeSelectsBeamProjectile()
    {
        Assert.That(CMUXenoWarlockSystem.GetPsychicBlastBeamPrototype(CMUXenoPsychicBlastMode.Blast), Is.EqualTo("CMUXenoPsychicBlastProjectile"));
        Assert.That(CMUXenoWarlockSystem.GetPsychicBlastBeamPrototype(CMUXenoPsychicBlastMode.Lance), Is.EqualTo("CMUXenoPsychicLanceProjectile"));
    }

    [Test]
    public void PsychicBlastChannelUsesTgmcRedParticlesAndLight()
    {
        var profile = CMUXenoWarlockSystem.GetWarlockParticleProfile(CMUXenoWarlockParticleEffect.PsychicBlastCharge);

        Assert.That(CMUXenoWarlockSystem.GetWarlockChannelColor(CMUXenoWarlockChannelKind.PsychicBlast, CMUXenoPsychicBlastMode.Blast), Is.EqualTo("#970f0f"));
        Assert.That(CMUXenoWarlockSystem.GetWarlockChannelLightPrototype(CMUXenoWarlockChannelKind.PsychicBlast, CMUXenoPsychicBlastMode.Blast), Is.EqualTo("CMUXenoWarlockBlastChannelEffect"));
        Assert.That(CMUXenoWarlockSystem.ShouldSpawnWarlockChannelStream(CMUXenoWarlockChannelKind.PsychicBlast), Is.False);
        Assert.That(CMUXenoWarlockSystem.GetWarlockChannelParticlePrototype(CMUXenoWarlockChannelKind.PsychicBlast, CMUXenoPsychicBlastMode.Blast), Is.EqualTo("CMUXenoWarlockBlastParticles"));
        Assert.That(profile.Color, Is.EqualTo("#970f0f"));
        Assert.That(profile.Count, Is.EqualTo(300));
        Assert.That(profile.Spawning, Is.EqualTo(20));
        Assert.That(profile.Lifespan, Is.EqualTo(12));
        Assert.That(profile.HolderOffset, Is.EqualTo(new Vector2(16, 0)));
    }

    [Test]
    public void PsychicCrushChannelUsesTgmcPurpleParticlesAndLight()
    {
        var profile = CMUXenoWarlockSystem.GetWarlockParticleProfile(CMUXenoWarlockParticleEffect.PsychicCrushCharge);

        Assert.That(CMUXenoWarlockSystem.GetWarlockChannelColor(CMUXenoWarlockChannelKind.PsychicCrush, CMUXenoPsychicBlastMode.Blast), Is.EqualTo("#6a59b3"));
        Assert.That(CMUXenoWarlockSystem.GetWarlockChannelLightPrototype(CMUXenoWarlockChannelKind.PsychicCrush, CMUXenoPsychicBlastMode.Blast), Is.EqualTo("CMUXenoWarlockCrushChannelEffect"));
        Assert.That(CMUXenoWarlockSystem.ShouldSpawnWarlockChannelStream(CMUXenoWarlockChannelKind.PsychicCrush), Is.False);
        Assert.That(CMUXenoWarlockSystem.GetWarlockChannelParticlePrototype(CMUXenoWarlockChannelKind.PsychicCrush, CMUXenoPsychicBlastMode.Blast), Is.EqualTo("CMUXenoWarlockCrushParticles"));
        Assert.That(profile.Color, Is.EqualTo("#6a59b3"));
        Assert.That(profile.Count, Is.EqualTo(300));
        Assert.That(profile.Spawning, Is.EqualTo(15));
        Assert.That(profile.Lifespan, Is.EqualTo(8));
        Assert.That(profile.HolderOffset, Is.EqualTo(new Vector2(16, 5)));
    }

    [Test]
    public void PsychicLanceUsesTgmcMagentaParticles()
    {
        var profile = CMUXenoWarlockSystem.GetWarlockParticleProfile(CMUXenoWarlockParticleEffect.PsychicLanceCharge);

        Assert.That(CMUXenoWarlockSystem.GetWarlockChannelParticlePrototype(CMUXenoWarlockChannelKind.PsychicBlast, CMUXenoPsychicBlastMode.Lance), Is.EqualTo("CMUXenoWarlockLanceParticles"));
        Assert.That(profile.Color, Is.EqualTo("#CB0166"));
        Assert.That(profile.Spawning, Is.EqualTo(30));
    }

    [Test]
    public void WarlockChannelParticlesRenderFromWarlockCenter()
    {
        Assert.That(CMUXenoWarlockSystem.GetWarlockParticleRenderOffset(CMUXenoWarlockParticleEffect.PsychicBlastCharge), Is.EqualTo(Vector2.Zero));
        Assert.That(CMUXenoWarlockSystem.GetWarlockParticleRenderOffset(CMUXenoWarlockParticleEffect.PsychicLanceCharge), Is.EqualTo(Vector2.Zero));
        Assert.That(CMUXenoWarlockSystem.GetWarlockParticleRenderOffset(CMUXenoWarlockParticleEffect.PsychicCrushCharge), Is.EqualTo(Vector2.Zero));
    }

    [Test]
    public void PsychicCrushWarningUsesTgmcWarningParticles()
    {
        var profile = CMUXenoWarlockSystem.GetWarlockParticleProfile(CMUXenoWarlockParticleEffect.CrushWarning);

        Assert.That(profile.Color, Is.EqualTo("#4b3f7e"));
        Assert.That(profile.Count, Is.EqualTo(50));
        Assert.That(profile.Spawning, Is.EqualTo(5));
        Assert.That(profile.Lifespan, Is.EqualTo(8));
        Assert.That(profile.Fade, Is.EqualTo(10));
        Assert.That(profile.Grow, Is.EqualTo(-0.04f).Within(0.001));
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
    public void PsychicBlastImpactUsesShockwaveEffect()
    {
        Assert.That(CMUXenoWarlockSystem.GetPsychicBlastImpactEffectPrototype(), Is.EqualTo("CMUXenoPsychicBlastShockwave"));
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
        Assert.That(CMUXenoWarlockSystem.GetPsychicCrushEndEffectCount(false, 5), Is.EqualTo(1));
        Assert.That(CMUXenoWarlockSystem.GetPsychicCrushEndEffectCount(true, 5), Is.EqualTo(1));
    }

    [Test]
    public void PsychicCrushOrbDrawsAboveSameTileMobs()
    {
        Assert.That(CMUXenoWarlockSystem.GetPsychicCrushOrbDrawDepth(), Is.EqualTo(CMUDrawDepth.Overlays));
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

    [Test]
    public void PsychicCrushUsesTgmcWindupBeforeChannelLoop()
    {
        Assert.That(CMUXenoWarlockSystem.GetPsychicCrushWindupDuration().TotalSeconds, Is.EqualTo(0.8).Within(0.001));
        Assert.That(CMUXenoWarlockSystem.GetPsychicCrushPulseInterval().TotalSeconds, Is.EqualTo(1.75).Within(0.001));
        Assert.That(CMUXenoWarlockSystem.GetPsychicCrushChannelDuration().TotalSeconds, Is.EqualTo(3.5).Within(0.001));
    }

    [Test]
    public void PsychicCrushShowsActionCooldownAfterResolution()
    {
        Assert.That(CMUXenoWarlockSystem.ShouldPsychicCrushShowActionCooldown(), Is.True);
        Assert.That(CMUXenoWarlockSystem.GetPsychicCrushCooldownDuration().TotalSeconds, Is.EqualTo(15).Within(0.001));
    }

    [Test]
    public void WarlockRetriggerableActionsDeferCooldownUntilAfterActionCleanup()
    {
        Assert.That(CMUXenoWarlockSystem.ShouldDeferWarlockActionCooldownUntilAfterActionPerformed(), Is.True);
    }

    [Test]
    public void PsychicCrushMovementCancellationResolvesBuiltUpDamage()
    {
        Assert.That(CMUXenoWarlockSystem.ShouldPsychicCrushCancellationResolve(), Is.True);
        Assert.That(CMUXenoWarlockSystem.ShouldSpawnPsychicCrushTileBlur(false), Is.True);
        Assert.That(CMUXenoWarlockSystem.ShouldSpawnPsychicCrushTileBlur(true), Is.True);
        Assert.That(CMUXenoWarlockSystem.GetPsychicCrushBlurPrototype(), Is.EqualTo("CMUXenoPsychicCrushBlur"));
        Assert.That(CMUXenoWarlockSystem.GetPsychicCrushBlurDuration().TotalSeconds, Is.EqualTo(1).Within(0.001));
        Assert.That(CMUXenoWarlockSystem.GetPsychicCrushOwnerSlowDuration().TotalSeconds, Is.EqualTo(1).Within(0.001));
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
        Assert.That(CMUXenoWarlockSystem.ShouldPsychicShieldUseUnanchoredWorldPlacement(), Is.True);
        Assert.That(CMUXenoWarlockSystem.ShouldPsychicShieldSnapToGrid(), Is.False);
    }

    [Test]
    public void PsychicShieldVisualRendersAtCatcherPosition()
    {
        Assert.That(CMUXenoWarlockSystem.GetPsychicShieldVisualOffset(Direction.North), Is.EqualTo(Vector2.Zero));
        Assert.That(CMUXenoWarlockSystem.GetPsychicShieldVisualOffset(Direction.South), Is.EqualTo(Vector2.Zero));
        Assert.That(CMUXenoWarlockSystem.GetPsychicShieldVisualOffset(Direction.East), Is.EqualTo(Vector2.Zero));
        Assert.That(CMUXenoWarlockSystem.GetPsychicShieldVisualOffset(Direction.West), Is.EqualTo(Vector2.Zero));
        Assert.That(CMUXenoWarlockSystem.ShouldOffsetPsychicShieldSpriteWithoutMovingCollision(), Is.False);
    }

    [Test]
    public void PsychicShieldUsesTgmcTimingsCostsAndIntegrity()
    {
        Assert.That(CMUXenoWarlockSystem.GetPsychicShieldCost(), Is.EqualTo(FixedPoint2.New(200)));
        Assert.That(CMUXenoWarlockSystem.GetPsychicShieldDetonationCost(), Is.EqualTo(FixedPoint2.New(200)));
        Assert.That(CMUXenoWarlockSystem.GetPsychicShieldDuration().TotalSeconds, Is.EqualTo(6).Within(0.001));
        Assert.That(CMUXenoWarlockSystem.GetPsychicShieldCooldownDuration().TotalSeconds, Is.EqualTo(10).Within(0.001));
        Assert.That(CMUXenoWarlockSystem.GetPsychicShieldIntegrity(), Is.EqualTo(FixedPoint2.New(650)));
        Assert.That(CMUXenoWarlockSystem.GetPsychicShieldBreakStunDuration().TotalSeconds, Is.EqualTo(1).Within(0.001));
        Assert.That(CMUXenoWarlockSystem.GetPsychicShieldMaxFrozenProjectiles(), Is.EqualTo(8));
    }

    [Test]
    public void PsychicShieldUsesTgmcOwnerGlowInsteadOfDuplicateShieldSprite()
    {
        Assert.That(CMUXenoWarlockSystem.GetWarlockChannelColor(CMUXenoWarlockChannelKind.PsychicShield, CMUXenoPsychicBlastMode.Blast), Is.EqualTo("#5999b3"));
        Assert.That(CMUXenoWarlockSystem.GetWarlockChannelLightPrototype(CMUXenoWarlockChannelKind.PsychicShield, CMUXenoPsychicBlastMode.Blast), Is.EqualTo("CMUXenoWarlockShieldChannelEffect"));
        Assert.That(CMUXenoWarlockSystem.ShouldPsychicShieldOwnerChannelDrawShieldSprite(), Is.False);
    }

    [Test]
    public void PsychicShieldUsesTgmcProjectileStateTransitions()
    {
        Assert.That(CMUXenoWarlockSystem.ShouldPsychicShieldFreezeIncomingProjectiles(), Is.True);
        Assert.That(CMUXenoWarlockSystem.ShouldPsychicShieldReleaseProjectilesOnCancel(), Is.True);
        Assert.That(CMUXenoWarlockSystem.ShouldPsychicShieldReflectProjectilesOnManualDetonation(), Is.True);
        Assert.That(CMUXenoWarlockSystem.ShouldPsychicShieldReleaseProjectilesAndStunOwnerOnBreak(), Is.True);
        Assert.That(CMUXenoWarlockSystem.ShouldPsychicShieldRestoreOriginalProjectileOnBreak(), Is.True);
        Assert.That(CMUXenoWarlockSystem.ShouldPsychicShieldRootOwnerWhileActive(), Is.True);
        Assert.That(CMUXenoWarlockSystem.GetPsychicShieldOwnerMoveSpeedMultiplier(), Is.EqualTo(0).Within(0.001));
        Assert.That(CMUXenoWarlockSystem.ShouldPlayPsychicShieldReflectSoundAtShield(), Is.True);
        Assert.That(CMUXenoWarlockSystem.ShouldPsychicShieldRequireClearForwardTile(), Is.True);
        Assert.That(CMUXenoWarlockSystem.ShouldPsychicShieldDisableFrozenProjectileCollision(), Is.True);
        Assert.That(CMUXenoWarlockSystem.ShouldPsychicShieldRestoreFrozenProjectileCollision(), Is.True);
        Assert.That(CMUXenoWarlockSystem.ShouldPsychicShieldUseHardProjectileCollision(), Is.True);
        Assert.That(CMUXenoWarlockSystem.ShouldPsychicShieldCatchProjectilesBeforeProjectileSystems(), Is.True);
        Assert.That(CMUXenoWarlockSystem.ShouldPsychicShieldSubscribeToProjectilePreventCollide(), Is.False);
        Assert.That(CMUXenoWarlockSystem.ShouldPsychicShieldSuspendDeleteOnCollideComponent(), Is.True);
        Assert.That(CMUXenoWarlockSystem.ShouldPsychicShieldSuspendFixedDistanceProjectileLifetime(), Is.True);
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
    public void PsychicShieldShowsActionCooldownAfterEnding()
    {
        Assert.That(CMUXenoWarlockSystem.ShouldPsychicShieldShowActionCooldown(), Is.True);
        Assert.That(CMUXenoWarlockSystem.GetPsychicShieldCooldownDuration().TotalSeconds, Is.EqualTo(10).Within(0.001));
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
    public void PsychicBlastUsesTgmcSoundStagesAsAuthoritativeWorldAudio()
    {
        Assert.That(CMUXenoWarlockSystem.GetPsychicBlastFireSoundPath(), Is.EqualTo("/Audio/_CMU14/Xeno/Warlock/volkite_4.ogg"));
        Assert.That(CMUXenoWarlockSystem.GetPsychicBlastImpactSoundPath(), Is.EqualTo("/Audio/_CMU14/Xeno/Warlock/EMPulse.ogg"));
        Assert.That(CMUXenoWarlockSystem.ShouldPsychicBlastPlayFireSoundFromWarlockSystem(), Is.True);
        Assert.That(CMUXenoWarlockSystem.ShouldPsychicBlastUsePvsAudio(), Is.True);
    }

    [Test]
    public void PsychicBlastKnocksBackAffectedTargets()
    {
        Assert.That(CMUXenoWarlockSystem.ShouldPsychicBlastKnockbackAffectedTargets(), Is.True);
        Assert.That(CMUXenoWarlockSystem.GetPsychicBlastKnockbackSpeed(), Is.EqualTo(8).Within(0.001));
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
        var reflected = CMUXenoWarlockSystem.ReflectProjectileVelocity(new Vector2(0, -10), Direction.North, Angle.Zero);

        Assert.That(reflected.X, Is.EqualTo(0).Within(0.001));
        Assert.That(reflected.Y, Is.EqualTo(10).Within(0.001));
    }

    [Test]
    public void PsychicShieldManualReflectionAllowsEightyDegreeCone()
    {
        var reflected = CMUXenoWarlockSystem.ReflectProjectileVelocity(
            new Vector2(0, -10),
            Direction.North,
            Angle.FromDegrees(40));

        Assert.That(CMUXenoWarlockSystem.GetPsychicShieldReflectionSpreadDegrees(), Is.EqualTo(80).Within(0.001));
        Assert.That(reflected.Length(), Is.EqualTo(10).Within(0.001));
        Assert.That(reflected.Y, Is.GreaterThan(0));
        Assert.That(reflected.X, Is.LessThan(0));
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
            FixedPoint2.New(1625),
            FixedPoint2.New(1700));

        Assert.That(amount, Is.EqualTo(FixedPoint2.New(75)));
    }

    [Test]
    public void GetPlasmaTransferAmountClampsToDonorAvailablePlasma()
    {
        var amount = CMUXenoWarlockSystem.GetPlasmaTransferAmount(
            FixedPoint2.New(250),
            FixedPoint2.New(60),
            FixedPoint2.New(1000),
            FixedPoint2.New(1700));

        Assert.That(amount, Is.EqualTo(FixedPoint2.New(60)));
    }

    [Test]
    public void GetPlasmaTransferAmountReturnsZeroWhenTargetIsFull()
    {
        var amount = CMUXenoWarlockSystem.GetPlasmaTransferAmount(
            FixedPoint2.New(250),
            FixedPoint2.New(500),
            FixedPoint2.New(1700),
            FixedPoint2.New(1700));

        Assert.That(amount, Is.EqualTo(FixedPoint2.Zero));
    }
}
