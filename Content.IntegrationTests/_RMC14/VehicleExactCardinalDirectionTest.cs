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
}
