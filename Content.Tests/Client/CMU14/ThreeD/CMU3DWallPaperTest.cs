using System;
using System.Linq;
using System.Numerics;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DWallPaperTest
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "RA0039", Justification = "CMU14: This test supplies isolated mutable fixture data; prototype registration and inheritance are not exercised.")]
    private static CMU3DModelPrototype Model() => new()
    {
        WallPaper = true, WallMounted = true, FitInsideWall = true, BackWallMountTargets = ["Wall"],
        ReferenceRsi = "paper.rsi", ReferenceState = "print",
        Parts = [new() { Min = new(-.2f, -.511f, 1), Max = new(.2f, -.501f, 1.6f) }],
    };

    private static CMU3DWallPaperPose Pose(int id, float sourceY, float x = 0, float yaw = 0, bool inside = false)
    {
        Assert.That(CMU3DWallPaper.TryPose(Model(), new EntityUid(id), new Vector2(0, sourceY), -1, 0,
            new Vector2(MathF.Cos(yaw) * x, MathF.Sin(yaw) * x), yaw, inside, out var pose), Is.True);
        return pose;
    }

    [TestCase(0f, false)]
    [TestCase(1.57079633f, false)]
    [TestCase(3.14159265f, false)]
    [TestCase(-1.57079633f, false)]
    [TestCase(.37f, false)]
    [TestCase(.37f, true)]
    public void SourceLaterPaperMovesTowardResolvedFrontWithSolidGap(float yaw, bool inside)
    {
        var a = Pose(90, 2, 0, yaw, inside);
        var b = Pose(3, 1, .1f, yaw, inside);
        Assert.That(CMU3DWallPaper.TryOffsets([b, a], out var offsets), Is.True);
        Assert.That(offsets[a.Uid], Is.EqualTo(Vector2.Zero));
        var normal = new Vector2(MathF.Sin(b.FrontYaw), -MathF.Cos(b.FrontYaw));
        Assert.That(Vector2.Dot(offsets[b.Uid], normal), Is.EqualTo(.014f).Within(.00001));
        Assert.That(b.Rear + Vector2.Dot(offsets[b.Uid], normal) - a.Front, Is.EqualTo(.004f).Within(.00001));
    }

    [Test]
    public void DrawDepthAndRenderOrderPrecedeSourceYAndUidBreaksTies()
    {
        var a = Pose(1, 2) with { DrawDepth = 10 };
        var b = Pose(2, 1);
        Assert.That(CMU3DWallPaper.TryOffsets([a, b], out var offsets), Is.True);
        Assert.That(offsets[a.Uid].Length(), Is.GreaterThan(0));
        a = a with { DrawDepth = -1, RenderOrder = 3 };
        Assert.That(CMU3DWallPaper.TryOffsets([a, b], out offsets), Is.True);
        Assert.That(offsets[a.Uid].Length(), Is.GreaterThan(0));
        a = Pose(1, 1);
        Assert.That(CMU3DWallPaper.TryOffsets([a, b], out offsets), Is.True);
        Assert.That(offsets[b.Uid].Length(), Is.GreaterThan(0));
    }

    [Test]
    public void OverlapChainUsesPredecessorDepthAndIsInputOrderIndependent()
    {
        var papers = new[] { Pose(1, 3, -.3f), Pose(2, 2), Pose(3, 1, .3f) };
        Assert.That(CMU3DWallPaper.Overlaps(papers[0], papers[2]), Is.False);
        Assert.That(CMU3DWallPaper.TryOffsets(papers, out var offsets), Is.True);
        Assert.That(offsets[new EntityUid(3)].Length(), Is.EqualTo(.028f).Within(.00001));
        Assert.That(CMU3DWallPaper.TryOffsets(papers.Reverse().ToArray(), out var reversed), Is.True);
        Assert.That(reversed, Is.EquivalentTo(offsets));
    }

    [Test]
    public void DifferentFacesWallsAndTouchingEdgesDoNotStack()
    {
        var a = Pose(1, 3);
        foreach (var b in new[] { Pose(2, 1, .4f), Pose(2, 1, 0, MathF.PI), Pose(2, 1) with { Rear = 3, Front = 3.01f } })
        {
            Assert.That(CMU3DWallPaper.Overlaps(a, b), Is.False);
            Assert.That(CMU3DWallPaper.TryOffsets([a, b], out var offsets), Is.True);
            Assert.That(offsets.Values.All(v => v == Vector2.Zero), Is.True);
        }
    }

    [Test]
    public void BudgetsAndInvalidInputsFailAtomically()
    {
        var seventeen = Enumerable.Range(1, 17).Select(i => Pose(i, -i)).ToArray();
        Assert.That(CMU3DWallPaper.TryOffsets(seventeen, out var offsets), Is.False);
        Assert.That(offsets, Is.Empty);
        var a = Pose(1, 1);
        Assert.That(CMU3DWallPaper.TryOffsets([a, a], out offsets), Is.False);
        Assert.That(offsets, Is.Empty);
        Assert.That(CMU3DWallPaper.TryOffsets([a with { Rear = float.NaN }], out offsets), Is.False);
        Assert.That(offsets, Is.Empty);
    }

    [Test]
    public void UnsupportedGeometryAndStatefulModelsAreRejected()
    {
        var model = Model();
        Assert.That(CMU3DWallPaper.ValidModel(model), Is.True);
        model.Parts[0].Pitch = 1;
        Assert.That(CMU3DWallPaper.ValidModel(model), Is.False);
        model.Parts[0].Pitch = 0;
        model.GroundOffset = Vector2.One;
        Assert.That(CMU3DWallPaper.ValidModel(model), Is.False);
        model.GroundOffset = Vector2.Zero;
        model.DoorSpriteStates["open"] = new();
        Assert.That(CMU3DWallPaper.ValidModel(model), Is.False);
    }
}
