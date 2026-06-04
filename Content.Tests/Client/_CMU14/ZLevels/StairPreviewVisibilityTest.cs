using System.Numerics;
using Content.Client._CMU14.ZLevels.Core;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client._CMU14.ZLevels;

[TestFixture]
public sealed class StairPreviewVisibilityTest
{
    [Test]
    public void TargetInFrontOfStairIsVisible()
    {
        var viewer = new Vector2(0f, -2f);
        var stair = Vector2.Zero;
        var target = new Vector2(0f, 3f);

        Assert.That(CMUZLevelStairPreviewVisibility.IsInFrontOfStair(viewer, stair, target), Is.True);
    }

    [Test]
    public void TargetBehindStairIsHidden()
    {
        var viewer = new Vector2(0f, -2f);
        var stair = Vector2.Zero;
        var target = new Vector2(0f, -3f);

        Assert.That(CMUZLevelStairPreviewVisibility.IsInFrontOfStair(viewer, stair, target), Is.False);
    }

    [Test]
    public void SideTargetAtStairPlaneIsVisible()
    {
        var viewer = new Vector2(0f, -2f);
        var stair = Vector2.Zero;
        var target = new Vector2(3f, 0f);

        Assert.That(CMUZLevelStairPreviewVisibility.IsInFrontOfStair(viewer, stair, target), Is.True);
    }

    [Test]
    public void ViewerOnStairKeepsExistingOmnidirectionalPreview()
    {
        var viewer = Vector2.Zero;
        var stair = Vector2.Zero;
        var target = new Vector2(0f, -3f);

        Assert.That(CMUZLevelStairPreviewVisibility.IsInFrontOfStair(viewer, stair, target), Is.True);
    }

    [Test]
    public void ViewerInsideStairFootprintKeepsOmnidirectionalPreview()
    {
        var viewer = new Vector2(0f, 0.45f);
        var stair = Vector2.Zero;
        var target = new Vector2(0f, 3f);

        Assert.That(CMUZLevelStairPreviewVisibility.IsInFrontOfStair(viewer, stair, target), Is.True);
    }

    [Test]
    public void ProjectedBoundsEntirelyInFrontOfStairAreVisible()
    {
        var viewer = new Vector2(0f, -2f);
        var stair = Vector2.Zero;
        var bounds = new Box2(-0.4f, 1.0f, 0.4f, 2.0f);

        Assert.That(CMUZLevelStairPreviewVisibility.ProjectedBoundsStayInFrontOfStair(viewer, stair, bounds, Vector2.Zero), Is.True);
    }

    [Test]
    public void ProjectedBoundsCrossingBehindStairAreHidden()
    {
        var viewer = new Vector2(0f, -2f);
        var stair = Vector2.Zero;
        var bounds = new Box2(-0.4f, 0.2f, 0.4f, 1.2f);
        var renderOffset = new Vector2(0f, 0.7f);

        Assert.That(CMUZLevelStairPreviewVisibility.ProjectedBoundsStayInFrontOfStair(viewer, stair, bounds, renderOffset), Is.False);
    }

    [Test]
    public void ProjectedBoundsCanCrossStairPlaneWhenViewerIsInsideStairFootprint()
    {
        var viewer = new Vector2(0f, -0.45f);
        var stair = Vector2.Zero;
        var bounds = new Box2(-0.4f, 0.2f, 0.4f, 1.2f);
        var renderOffset = new Vector2(0f, 0.7f);

        Assert.That(CMUZLevelStairPreviewVisibility.ProjectedBoundsStayInFrontOfStair(viewer, stair, bounds, renderOffset), Is.True);
    }
}
