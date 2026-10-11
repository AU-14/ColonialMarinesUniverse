using System;
using System.Numerics;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using NUnit.Framework;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DWindowMountTest
{
    private static readonly CMU3DModelPart[] Shutter =
    [new() { Min = new Vector3(-.5f,-.13f,2.48f), Max = new Vector3(.5f,.13f,2.74f) }];
    private static readonly CMU3DModelPart[] Glass =
    [
        new() { Min = new Vector3(-.5f,-.15f,0), Max = new Vector3(.5f,.15f,.65f) },
        new() { Min = new Vector3(-.5f,-.035f,.65f), Max = new Vector3(.5f,.035f,2.6f) },
    ];

    [TestCase(0, 0)] [TestCase(1, 0)] [TestCase(2, 0)] [TestCase(3, 0)]
    [TestCase(0, .7f)] [TestCase(1, .7f)] [TestCase(2, .7f)] [TestCase(3, .7f)]
    public void SavedFacingRetainsAConsistentAssemblyGap(int turn, float gridYaw)
    {
        var yaw = gridYaw + turn * MathF.PI / 2;
        var normal = new Vector2(MathF.Sin(yaw), -MathF.Cos(yaw));
        var parts = CMU3DSceneLayout.ConnectedParts(Glass, turn % 2 == 0 ? 12 : 3);
        Assert.That(CMU3DSceneLayout.TryWindowMountOffset(Shutter, yaw, parts, gridYaw, normal * .1f, out var offset), Is.True);
        Assert.That(Vector2.Distance(offset, normal * .4f), Is.LessThan(.00001f));
        Assert.That(Vector2.Dot(offset, normal) - .13f - (.1f + .15f), Is.EqualTo(.02f).Within(.00001f));
        Assert.That(Glass[0].Min, Is.EqualTo(new Vector3(-.5f,-.15f,0)));
    }

    [TestCase(3)] [TestCase(5)] [TestCase(15)]
    public void PerpendicularOrCornerGlazingCannotSelectAMountingFace(int mask)
    {
        Assert.That(CMU3DSceneLayout.TryWindowMountOffset(Shutter, 0, CMU3DSceneLayout.ConnectedParts(Glass, mask),
            0, Vector2.Zero, out var offset), Is.False);
        Assert.That(offset, Is.EqualTo(Vector2.Zero));
    }

    [Test]
    public void EmptyOrAlreadyClearAssembliesDoNotMove()
    {
        Assert.That(CMU3DSceneLayout.TryWindowMountOffset([], 0, Glass, 0, Vector2.Zero, out _), Is.False);
        Assert.That(CMU3DSceneLayout.TryWindowMountOffset(Shutter, 0, Glass, 0, new Vector2(0,1), out var offset), Is.True);
        Assert.That(offset, Is.EqualTo(Vector2.Zero));
    }

    [TestCase(0, 0)] [TestCase(1, 0)] [TestCase(2, 0)] [TestCase(3, 0)]
    [TestCase(0, .7f)] [TestCase(1, .7f)] [TestCase(2, .7f)] [TestCase(3, .7f)]
    public void ThickObservationWindowUsesItsExplicitConnectedFace(int turn, float gridYaw)
    {
        var vertical = turn % 2 != 0;
        var body = new CMU3DModelPart[]
        {
            new() { Min = new Vector3(vertical ? -.515f : -.5f, vertical ? -.5f : -.515f, 0),
                    Max = new Vector3(vertical ? .515f : .5f, vertical ? .5f : .515f, 2.78f) },
        };
        var yaw = gridYaw + turn * MathF.PI / 2;
        var axis = CMU3DSceneLayout.ShutterExteriorAxis("RMCWindowPrisonCell", vertical ? 3 : 12);
        Assert.That(CMU3DSceneLayout.TryWindowMountOffset(Shutter, yaw, body, gridYaw, Vector2.Zero,
            out var offset, exteriorAxis: axis), Is.True);
        var distance = Vector2.Dot(offset, new Vector2(MathF.Sin(yaw), -MathF.Cos(yaw)));
        Assert.That(distance, Is.EqualTo(.665f).Within(.00001f));
        Assert.That(distance - .13f - .515f, Is.EqualTo(.02f).Within(.00001f));
    }

    [Test]
    public void ExplicitFaceCannotMountAnOffsetPerpendicularOrInsideObject()
    {
        Assert.That(CMU3DSceneLayout.TryWindowMountOffset(Shutter, MathF.PI / 2, Glass, 0, Vector2.Zero,
            out _, exteriorAxis: 1), Is.False);
        Assert.That(CMU3DSceneLayout.TryWindowMountOffset(Shutter, 0, Glass, 0, new Vector2(.01f, 0),
            out _, exteriorAxis: 1), Is.False);
        Assert.That(CMU3DSceneLayout.TryWindowMountOffset(Shutter, 0, Glass, 0, Vector2.Zero,
            out _, inside: true, exteriorAxis: 1), Is.False);
        foreach (var mask in new[] { 0, 5, 15 })
            Assert.That(CMU3DSceneLayout.ShutterExteriorAxis("RMCWindowPrisonCell", mask), Is.Null);
        Assert.That(CMU3DSceneLayout.IsShutterExteriorTarget("CMU3DHybrisaWindowShutter", "CMAirlockGlassHybrisa"), Is.True);
        Assert.That(CMU3DSceneLayout.IsShutterExteriorTarget("CMU3DHybrisaWindowShutter", "RMCWindowPrisonCell"), Is.False);
        Assert.That(CMU3DSceneLayout.IsShutterExteriorTarget("Curtain", "RMCWindowPrisonCell"), Is.False);
        Assert.That(CMU3DSceneLayout.IsShutterExteriorTarget("CMU3DHybrisaWindowShutter", "UnknownSquareWindow"), Is.False);
    }

    [Test]
    public void SupportingDoorEnvelopeKeepsTheMountStillAcrossBothPosesAndFrames()
    {
        var closed = new CMU3DModelPrototype
        {
            AlternateDoorModel = "Open",
            Parts = [new() { Min = new Vector3(-.5f, -.18f, 0), Max = new Vector3(.5f, .18f, 2.7f) }],
        };
        var opened = new CMU3DModelPrototype
        {
            AlternateDoorModel = "Closed",
            Parts = [new() { Min = new Vector3(-.5f, -.20f, 0), Max = new Vector3(-.4f, .20f, 2.7f) }],
            DoorSpriteStates = new()
            {
                ["opening"] = new()
                {
                    Frames = [new() { Parts = [new() { Min = new Vector3(-.5f, -.25f, 0), Max = new Vector3(-.4f, .25f, 2.7f) }] }],
                },
            },
        };
        typeof(CMU3DModelPrototype).GetProperty(nameof(CMU3DModelPrototype.ID))!.SetValue(closed, "Closed");
        typeof(CMU3DModelPrototype).GetProperty(nameof(CMU3DModelPrototype.ID))!.SetValue(opened, "Open");
        foreach (var (model, alternate) in new[] { (closed, opened), (opened, closed) })
        {
            Assert.That(CMU3DSceneLayout.TryShutterMountEnvelope(model, alternate, out var envelope), Is.True);
            Assert.That(CMU3DSceneLayout.TryWindowMountOffset(Shutter, 0, envelope, 0, Vector2.Zero,
                out var offset, exteriorAxis: 1), Is.True);
            Assert.That(offset.Y, Is.EqualTo(-.4f).Within(.00001f));
        }
        Assert.That(closed.Parts[0].Min.Y, Is.EqualTo(-.18f));
        Assert.That(opened.Parts[0].Min.Y, Is.EqualTo(-.20f));
        Assert.That(CMU3DSceneLayout.TryShutterMountEnvelope(closed, null, out _), Is.False);
        opened.YawOffset = 90;
        Assert.That(CMU3DSceneLayout.TryShutterMountEnvelope(closed, opened, out _), Is.False);
    }
}
