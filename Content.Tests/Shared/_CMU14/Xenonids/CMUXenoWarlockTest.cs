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

}
