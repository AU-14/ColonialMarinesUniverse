using System.Numerics;
using Content.Shared._RMC14.Vehicle;
using Robust.Shared.Maths;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class VehicleExactCardinalDirectionTest
{
    [TestCase(0, Direction.South)]
    [TestCase(67.499, Direction.South)]
    [TestCase(67.5, Direction.East)]
    [TestCase(90, Direction.East)]
    [TestCase(112.5, Direction.East)]
    [TestCase(112.501, Direction.North)]
    [TestCase(180, Direction.North)]
    [TestCase(247.499, Direction.North)]
    [TestCase(247.5, Direction.West)]
    [TestCase(270, Direction.West)]
    [TestCase(292.5, Direction.West)]
    [TestCase(292.501, Direction.South)]
    [TestCase(359.999, Direction.South)]
    [TestCase(-90, Direction.West)]
    [TestCase(450, Direction.East)]
    public void ExactCardinalCutoffs(double degrees, Direction expected)
    {
        var actual = VehicleTurretDirectionHelpers.GetRenderAlignedCardinalDir(Angle.FromDegrees(degrees));
        Assert.That(actual, Is.EqualTo(expected));
    }

    [TestCase(45)]
    [TestCase(80)]
    [TestCase(135)]
    [TestCase(225)]
    [TestCase(315)]
    public void DirectionalOffsetPreservesResidualChassisRotation(double degrees)
    {
        var facing = Angle.FromDegrees(degrees);
        var offset = new Vector2(3f, 11f);
        var direction = VehicleTurretDirectionHelpers.GetRenderAlignedCardinalDir(facing);
        var localOffset = VehicleTurretDirectionHelpers.GetLocalOffsetForRenderDirection(offset, facing);

        var renderedOffset = facing.RotateVec(localOffset);
        var expected = (facing - direction.ToAngle()).RotateVec(offset);

        Assert.Multiple(() =>
        {
            Assert.That(renderedOffset.X, Is.EqualTo(expected.X).Within(0.0001f));
            Assert.That(renderedOffset.Y, Is.EqualTo(expected.Y).Within(0.0001f));
        });
    }
}
