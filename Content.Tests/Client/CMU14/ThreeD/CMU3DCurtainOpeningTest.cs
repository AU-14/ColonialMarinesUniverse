using System;
using System.Numerics;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using NUnit.Framework;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DCurtainOpeningTest
{
    [TestCase(0)] [TestCase(.7f)]
    public void OnlyBlockedOpeningUsesClearFixtureFacing(float grid)
    {
        Assert.That(CMU3DSceneLayout.OpeningFixtureYaw(grid,grid,2,grid+MathF.PI),Is.EqualTo(grid+MathF.PI));
        Assert.That(CMU3DSceneLayout.OpeningFixtureYaw(grid-MathF.PI/2,grid,10,grid+MathF.PI),Is.EqualTo(grid+MathF.PI));
        Assert.That(CMU3DSceneLayout.OpeningFixtureYaw(grid,grid,1,grid+MathF.PI),Is.EqualTo(grid));
        Assert.That(CMU3DSceneLayout.OpeningFixtureYaw(grid,grid,3,grid+MathF.PI),Is.EqualTo(grid));
        Assert.That(CMU3DSceneLayout.OpeningFixtureYaw(grid+.2f,grid,2,grid+MathF.PI),Is.EqualTo(grid+.2f));
        Assert.That(CMU3DSceneLayout.OpeningFixtureYaw(grid,grid,2,grid+.2f),Is.EqualTo(grid));
    }

    [Test]
    public void TexturedRailPostsStillDefinePhysicalEnds()
    {
        CMU3DModelPart[] curtain=[new(){Min=new Vector3(-.5f,-.505f,.08f),Max=new Vector3(.5f,-.36f,2.5f)}];
        CMU3DModelPart[] rail=[new(){Min=new Vector3(-.5f,-.5f,0),Max=new Vector3(.5f,-.40625f,1),Surface="OriginalRailArt"}];
        var ends=CMU3DSceneLayout.PanelEndLimits(curtain,MathF.PI,rail,-MathF.PI/2,Vector2.Zero);
        Assert.That(ends.X,Is.EqualTo(-.5f).Within(.00001f));
        Assert.That(ends.Y,Is.EqualTo(.39625f).Within(.00001f));
    }
}
