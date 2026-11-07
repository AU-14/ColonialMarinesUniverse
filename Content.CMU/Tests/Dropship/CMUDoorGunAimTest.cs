using Content.Shared.CMU14.Dropship.AttachmentPoint;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.CMU14.Dropship;

[TestFixture]
public sealed class CMUDoorGunAimTest
{
    [TestCase(0, 0, true, 0)]
    [TestCase(0, 79, true, 79)]
    [TestCase(0, -79, true, -79)]
    [TestCase(0, 81, false, 81)]
    [TestCase(0, -81, false, -81)]
    [TestCase(350, 10, true, 20)]
    [TestCase(10, 350, true, -20)]
    [TestCase(90, 180, false, 90)]
    public void UsesExistingFiringArcAroundFixedMount(double facing, double cursor, bool allowed, double expectedOffset)
    {
        var result = CMUDoorGunAimSystem.TryGetAimOffset(
            Angle.FromDegrees(facing), Angle.FromDegrees(cursor), 160, out var offset);
        Assert.That(result, Is.EqualTo(allowed));
        Assert.That(offset.Degrees, Is.EqualTo(expectedOffset).Within(0.001));
    }

}
