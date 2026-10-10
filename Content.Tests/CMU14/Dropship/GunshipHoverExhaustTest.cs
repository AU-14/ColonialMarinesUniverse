using System.Numerics;
using Content.Shared.CMU14.Dropship.TacticalLand;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.CMU14.Dropship;

[TestFixture]
public sealed class GunshipHoverExhaustTest
{
    [TestCase(0, 0)]
    [TestCase(90, 0)]
    [TestCase(180, 0)]
    [TestCase(270, 90)]
    [TestCase(35, -80)]
    public void ExhaustOpposesThrustAndAlwaysReachesBelowTheNozzle(int shipDegrees, int eyeDegrees)
    {
        var ship = Angle.FromDegrees(shipDegrees);
        var eye = Angle.FromDegrees(eyeDegrees);
        foreach (var movement in new[] { Vector2.UnitX, Vector2.UnitY, -Vector2.UnitX, -Vector2.UnitY })
        foreach (var turn in new[] { -1f, 0f, 1f })
        {
            var force = GunshipHoverExhaust.NozzleThrust(movement, turn, new Vector2(3, -4.5f));
            var offset = GunshipHoverExhaust.GroundDeflection(force, ship, eye);
            var screenForce = (ship + eye).RotateVec(force);
            screenForce.Y = -screenForce.Y;
            Assert.That(Vector2.Dot(offset, screenForce), Is.LessThan(0), "Exhaust must push against the maneuver.");
            Assert.That(offset.Length(), Is.LessThanOrEqualTo(GunshipHoverExhaust.MaximumGroundDeflection + .00001f));
            var worldContact = GunshipHoverExhaust.GroundContactOffset(force, ship, eye);
            var viewContact = eye.RotateVec(worldContact);
            Assert.That(viewContact.Y, Is.LessThan(-.9f), "Combined turning and thrust must keep ground light below the nozzle.");
            Assert.That(Vector2.Distance(viewContact, new Vector2(offset.X,
                -GunshipHoverExhaust.GroundContactDistance - offset.Y)), Is.LessThan(.00001f),
                "The world light must coincide with the shader's ground impact for every camera and hull rotation.");
        }
    }

    [Test]
    public void TurnJetsProduceOpposingForcesAndReleaseReturnsToVertical()
    {
        var left = GunshipHoverExhaust.NozzleThrust(Vector2.Zero, 1, new Vector2(-3, 0));
        var right = GunshipHoverExhaust.NozzleThrust(Vector2.Zero, 1, new Vector2(3, 0));
        Assert.That(left + right, Is.EqualTo(Vector2.Zero), "A yaw pair must not add net translation.");
        Assert.That(left.Y, Is.LessThan(0));
        Assert.That(right.Y, Is.GreaterThan(0));
        Assert.That(GunshipHoverExhaust.NozzleThrust(Vector2.Zero, -1, new Vector2(-3, 0)), Is.EqualTo(-left));
        Assert.That(GunshipHoverExhaust.NozzleThrust(Vector2.Zero, 0, new Vector2(3, -4.5f)), Is.EqualTo(Vector2.Zero));
    }

    [Test]
    public void BrakingReversesTheExhaustInsteadOfFollowingCurrentVelocity()
    {
        var accelerate = GunshipHoverExhaust.NozzleThrust(Vector2.UnitY, 0, new Vector2(0, 4));
        var brake = GunshipHoverExhaust.NozzleThrust(-Vector2.UnitY, 0, new Vector2(0, 4));
        Assert.That(brake, Is.EqualTo(-accelerate));
    }
}
