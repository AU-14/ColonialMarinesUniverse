using System;
using System.Numerics;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using NUnit.Framework;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DPanelEndFitTest
{
    private static readonly CMU3DModelPart[] Pane =
    [new() { Min = new Vector3(-.49f,-.46f,.036f), Max = new Vector3(.49f,-.38f,1.5f) }];

    [TestCase(0,0)] [TestCase(1,0)] [TestCase(2,0)] [TestCase(3,0)]
    [TestCase(0,.7f)] [TestCase(1,.7f)] [TestCase(2,.7f)] [TestCase(3,.7f)]
    public void CurtainStaysInsideGlazingWithSameGapAtEveryFacing(int turn, float grid)
    {
        CMU3DModelPart[] curtain = [new() { Min = new Vector3(-.49f,-.505f,.08f), Max = new Vector3(.49f,-.36f,2.5f) }];
        var yaw = grid + turn * MathF.PI / 2;
        Assert.That(CMU3DSceneLayout.TryWindowMountOffset(curtain,yaw,Pane,yaw,Vector2.Zero,out var offset,inside:true),Is.True);
        var expected = new Vector2(-MathF.Sin(yaw),MathF.Cos(yaw)) * .145f;
        Assert.That(Vector2.Distance(offset,expected),Is.LessThan(.00001f));
        Assert.That(-.505f + offset.Length() - (-.38f),Is.EqualTo(.02f).Within(.00001f));
    }

    [TestCase(0)] [TestCase(.7f)] [TestCase(2.6f)]
    public void PerpendicularButtEndKeepsPaneAndClearance(float grid)
    {
        var ends = CMU3DSceneLayout.PanelEndLimits(Pane,grid + MathF.PI / 2,Pane,grid,Vector2.Zero);
        Assert.That(ends.X,Is.EqualTo(-.37f).Within(.00001f));
        Assert.That(ends.Y,Is.EqualTo(.49f).Within(.00001f));
        var fitted = CMU3DSceneLayout.FitPanelEnds(Pane,ends);
        Assert.That(fitted.Count,Is.EqualTo(Pane.Length));
        Assert.That(fitted[0].Min.X,Is.EqualTo(-.37f).Within(.00001f));
        Assert.That(fitted[0].Min.Y,Is.EqualTo(Pane[0].Min.Y));
        Assert.That(fitted[0].Max.Z,Is.EqualTo(Pane[0].Max.Z));
        Assert.That(Pane[0].Min.X,Is.EqualTo(-.49f));
    }

    [Test]
    public void FrontWallRibsCannotSqueezeBothPanelEnds()
    {
        CMU3DModelPart[] ribs =
        [
            new() { Min = new Vector3(-.5f,-.5f,0), Max = new Vector3(-.36f,.5f,2.6f) },
            new() { Min = new Vector3(.36f,-.5f,0), Max = new Vector3(.5f,.5f,2.6f) },
        ];
        Assert.That(CMU3DSceneLayout.PanelEndLimits(Pane,0,ribs,0,Vector2.Zero),Is.EqualTo(new Vector2(-.49f,.49f)));
    }

    [Test]
    public void InvalidOrExcessiveFitsPreserveOriginalGeometry()
    {
        foreach (var ends in new[] {new Vector2(-.1f,.1f),new Vector2(-.6f,.49f),new Vector2(-.49f,.6f),new Vector2(float.NaN,.49f)})
            Assert.That(CMU3DSceneLayout.FitPanelEnds(Pane,ends),Is.SameAs(Pane));
        Assert.That(CMU3DSceneLayout.PanelEndLimits(Pane,0,Pane,.2f,Vector2.Zero),Is.EqualTo(new Vector2(-.49f,.49f)));
    }
}
