using System.Numerics;
using Content.Client.CMU14.ThreeD;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DElevationTest
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "RA0039", Justification = "CMU14: This test supplies isolated mutable fixture data; prototype registration and inheritance are not exercised.")]
    [Test]
    public void RaisedLandingAndCameraFollowTheSameStairAscent()
    {
        var profile = new CMU3DElevationPrototype
        {
            Regions = [new CMU3DElevationRegion { Bounds = new Box2(0, 1, 2, 3), Height = .39f }],
            Ramps = [new CMU3DElevationRamp
            {
                Tile = new Vector2i(0, 0), Direction = new Vector2i(0, 1),
                Bottom = 0, Top = .39f, SourcePrototype = "Stairs",
            }],
        };
        var field = new CMU3DElevationField(profile);
        Assert.That(field.Height(new Vector2(.5f, 0)), Is.EqualTo(0));
        Assert.That(field.Height(new Vector2(.5f, .5f)), Is.EqualTo(.195f).Within(.00001f));
        Assert.That(field.Height(new Vector2(.5f, 1)), Is.EqualTo(.39f));
        var camera = CMU3DFirstPersonCamera.Frame(new Vector2(.5f, 1), 0, 0, new Vector2(800, 600),
            groundHeight: field.Height(new Vector2(.5f, 1)));
        Assert.That(camera.Origin.Z, Is.EqualTo(CMU3DFirstPersonCamera.EyeHeight + .39f).Within(.00001f));
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "RA0039", Justification = "CMU14: This test supplies isolated mutable fixture data; prototype registration and inheritance are not exercised.")]
    [Test]
    public void NegativeCoordinatesAndRecessedLandingRemainContinuous()
    {
        var field = new CMU3DElevationField(new CMU3DElevationPrototype
        {
            Regions = [new CMU3DElevationRegion { Bounds = new Box2(-2, -1, 0, 0), Height = -.39f }],
            Ramps = [new CMU3DElevationRamp
            {
                Tile = new Vector2i(-1, -1), Direction = new Vector2i(1, 0),
                Bottom = -.39f, Top = 0, SourcePrototype = "Stairs",
            }],
        });
        Assert.That(field.Height(new Vector2(-1.001f, -.5f)), Is.EqualTo(-.39f));
        Assert.That(field.Height(new Vector2(-1, -.5f)), Is.EqualTo(-.39f));
        Assert.That(field.Height(new Vector2(-.5f, -.5f)), Is.EqualTo(-.195f).Within(.00001f));
        Assert.That(field.Height(new Vector2(-.00001f, -.5f)), Is.EqualTo(0).Within(.00001f));
        Assert.That(field.Height(new Vector2(0, -.5f)), Is.EqualTo(0));
    }
}
