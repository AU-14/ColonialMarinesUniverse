using Content.Client.CMU14.Dropship.AttachmentPoint;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.CMU14.Dropship;

[TestFixture]
public sealed class CMUDoorGunDirectionTest
{
    [Test]
    public void KeepsDoorFacingFrameAcrossAimingSectorBoundaries()
    {
        Direction[] cardinals = [Direction.South, Direction.North, Direction.East, Direction.West];
        foreach (var mount in cardinals)
        foreach (var aim in new[] { -79, -46, 0, 46, 79 })
        {
            var frame = CMUDoorGunVisualSystem.GetGunFrame(mount.ToAngle(), Angle.FromDegrees(aim));
            Assert.That(frame.Direction, Is.EqualTo(mount), $"mount={mount}, aim={aim}");
            Assert.That(frame.Rotation.Degrees, Is.EqualTo(aim).Within(0.001));
        }
    }

    [TestCase(90, 90, Direction.North)]
    [TestCase(90, -90, Direction.South)]
    [TestCase(0, 90, Direction.East)]
    public void CameraRotationChoosesDoorFrameWithoutUsingCursorHeading(double mount, double camera, Direction expected)
    {
        var frame = CMUDoorGunVisualSystem.GetGunFrame(Angle.FromDegrees(mount + camera), Angle.FromDegrees(79));
        Assert.That(frame.Direction, Is.EqualTo(expected));
    }
}
