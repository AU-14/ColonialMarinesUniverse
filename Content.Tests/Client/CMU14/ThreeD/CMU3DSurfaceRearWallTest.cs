using System;
using System.Numerics;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using NUnit.Framework;
using Robust.Shared.GameObjects;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DSurfaceRearWallTest
{
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void RaisedWallTrimIsClearedAtTheTableHeight(int facing)
    {
        var yaw = facing * MathF.PI / 2;
        var model = Model();
        var wall = Wall(yaw, 1.1f);
        Assert.That(CMU3DSceneLayout.TryBackWallMountOffset(model.Parts, yaw, wall.Parts, yaw,
            Vector2.Zero, out _), Is.False, "The regression misses elevated trim when checked on the floor.");
        Assert.That(CMU3DScenePlacement.TryRearWallOffset(model, new EntityUid(1), Vector2.Zero, yaw,
            [Surface(2, -.8f, .8f, .86f)], [wall], out var offset), Is.True);
        Assert.That(offset.X, Is.EqualTo(.26f * MathF.Sin(yaw)).Within(.00001));
        Assert.That(offset.Y, Is.EqualTo(-.26f * MathF.Cos(yaw)).Within(.00001));
        Assert.That(offset.Z, Is.EqualTo(.862f).Within(.00001));
        Assert.That(model.Parts[0].Min.Z, Is.Zero, "The prototype remains unchanged.");
    }

    [Test]
    public void MovingOntoAnotherSupportRecomputesHeightBeforeAcceptingTheMount()
    {
        Assert.That(CMU3DScenePlacement.TryRearWallOffset(Model(), new EntityUid(1), Vector2.Zero, 0,
            [Surface(2, -.05f, .05f, .86f), Surface(3, -.4f, -.1f, .4f)], [Wall(0, .8f)], out var offset), Is.True);
        Assert.That(Vector3.Distance(offset, new Vector3(0, -.26f, .402f)), Is.LessThan(.00001f));
    }

    [Test]
    public void AHeightAndFootprintCycleRejectsTheWholeMount()
    {
        Assert.That(CMU3DScenePlacement.TryRearWallOffset(Model(), new EntityUid(1), Vector2.Zero, 0,
            [Surface(2, -.05f, .05f, .86f)], [Wall(0, 1.1f)], out var offset), Is.False);
        Assert.That(offset, Is.EqualTo(Vector3.Zero));
    }

    [Test]
    public void ASourceWithoutAWallKeepsItsOrdinarySupportHeight()
    {
        Assert.That(CMU3DScenePlacement.TryRearWallOffset(Model(), new EntityUid(1), Vector2.Zero, 0,
            [Surface(2, -.8f, .8f, .86f)], [], out var offset), Is.True);
        Assert.That(offset.X, Is.Zero);
        Assert.That(offset.Y, Is.Zero);
        Assert.That(offset.Z, Is.EqualTo(.862f).Within(.00001));
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "RA0039", Justification = "CMU14: This test supplies isolated mutable fixture data; prototype registration and inheritance are not exercised.")]
    private static CMU3DModelPrototype Model() => new()
    {
        Placement = "surface",
        Parts = [new() { Min = new Vector3(-.2f, -.15f, 0), Max = new Vector3(.2f, .15f, .5f) }],
    };

    private static CMU3DSceneRearWall Wall(float yaw, float trimBottom) => new(
        [new() { Min = new Vector3(-1, .3f, 0), Max = new Vector3(1, .6f, 2) },
         new() { Min = new Vector3(-1, -.1f, trimBottom), Max = new Vector3(1, .3f, 1.3f) }], yaw, Vector2.Zero);

    private static CMU3DSceneSurface Surface(int uid, float minY, float maxY, float top) =>
        new(new EntityUid(uid), Vector2.Zero, 0, new Vector3(-.8f, minY, top - .1f), new Vector3(.8f, maxY, top));
}
