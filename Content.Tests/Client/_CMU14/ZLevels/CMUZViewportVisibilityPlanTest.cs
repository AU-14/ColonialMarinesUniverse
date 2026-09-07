using System;
using System.Numerics;
using Content.Client.CMU14.ZLevels.Core;
using NUnit.Framework;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Tests.Client.CMU14.ZLevels;

[TestFixture]
public sealed class CMUZViewportVisibilityPlanTest
{
    private static readonly MapId BaseMap = new(1);
    private static readonly MapId LowerMap = new(2);
    private static readonly Box2 ViewBounds = new(-20f, -20f, 20f, 20f);

    [TestCase(0, -1)]
    [TestCase(90, -1)]
    [TestCase(180, -1)]
    [TestCase(37, -1)]
    [TestCase(0, -2)]
    [TestCase(90, -2)]
    [TestCase(180, -2)]
    [TestCase(37, -2)]
    [TestCase(0, -8)]
    [TestCase(90, -8)]
    [TestCase(180, -8)]
    [TestCase(37, -8)]
    public void ProjectedMaskMatchesActualEyeMatrix(int degrees, int depth)
    {
        var rotation = Angle.FromDegrees(degrees);
        var displacement = (-rotation).ToWorldVec() * 0.75f * depth;
        var aperture = new Box2(2f, 3f, 3f, 4f);
        var source = new CMUZVisibilityMask();
        source.SetOpenings(BaseMap, ViewBounds, new[] { aperture }, complete: true);
        var lower = new CMUZVisibilityMask();
        lower.SetProjected(source, LowerMap, displacement);

        var baseEye = new Eye
        {
            Position = new MapCoordinates(new Vector2(5f, 7f), BaseMap),
            Offset = new Vector2(1f, -2f),
            Scale = new Vector2(0.4f, 0.7f),
            Rotation = rotation,
        };
        var lowerEye = new Eye
        {
            Position = new MapCoordinates(baseEye.Position.Position, LowerMap),
            Offset = baseEye.Offset + displacement,
            Scale = baseEye.Scale,
            Rotation = rotation,
        };
        baseEye.GetViewMatrix(out var baseMatrix, new Vector2(2f, 3f));
        lowerEye.GetViewMatrix(out var lowerMatrix, new Vector2(2f, 3f));
        var point = aperture.Center;
        var projectedPoint = point + displacement;

        Assert.Multiple(() =>
        {
            Assert.That(Vector2.Distance(Vector2.Transform(point, baseMatrix),
                Vector2.Transform(projectedPoint, lowerMatrix)), Is.LessThan(0.00001f));
            Assert.That(lower.ClassifyBounds(Box2.CenteredAround(projectedPoint, new Vector2(0.1f))),
                Is.EqualTo(CMUZVisibility.Unknown));
            Assert.That(lower.WorldBounds, Is.EqualTo(ViewBounds.Translated(displacement)));
            Assert.That(source.Bounds[0], Is.EqualTo(aperture));
        });
    }

    [Test]
    public void DisconnectedAperturesCannotReachSolidCornerOnIntermediateFloor()
    {
        var upper = new CMUZVisibilityMask();
        upper.SetOpenings(BaseMap, ViewBounds,
            new[] { new Box2(0f, 0f, 1f, 3f), new Box2(1f, 2f, 3f, 3f) }, complete: true);
        var firstPass = new CMUZVisibilityMask();
        var displacement = new Vector2(0f, -0.75f);
        firstPass.SetProjected(upper, LowerMap, displacement);
        var chain = new CMUZVisibilityMask();
        chain.SetProjected(firstPass, LowerMap, Vector2.Zero);
        var missingCorner = new Box2(1.2f, 0.2f, 2.8f, 1.8f).Translated(displacement);
        chain.IntersectOpenings(new[] { missingCorner }, complete: true, maxFragments: 512);
        var deeper = new CMUZVisibilityMask();
        deeper.SetProjected(chain, new MapId(3), displacement);

        Assert.Multiple(() =>
        {
            Assert.That(firstPass.Visibility, Is.EqualTo(CMUZVisibility.Unknown));
            Assert.That(chain.Visibility, Is.EqualTo(CMUZVisibility.Hidden));
            Assert.That(deeper.Visibility, Is.EqualTo(CMUZVisibility.Hidden));
            Assert.That(firstPass.Bounds, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void TruncatedOpeningsKeepUncheckedRegionUntilACompleteFloorBlocksIt()
    {
        var mask = new CMUZVisibilityMask();
        mask.SetOpenings(BaseMap, ViewBounds, new[] { new Box2(-15f, -15f, -14f, -14f) }, complete: false);
        var uncheckedAperture = new Box2(10f, 10f, 11f, 11f);
        Assert.That(mask.ClassifyBounds(uncheckedAperture), Is.EqualTo(CMUZVisibility.Unknown));
        Assert.That(mask.Visibility, Is.EqualTo(CMUZVisibility.Unknown));

        Assert.That(mask.IntersectOpenings(Array.Empty<Box2>(), complete: true, maxFragments: 1), Is.True);
        Assert.That(mask.ClassifyBounds(uncheckedAperture), Is.EqualTo(CMUZVisibility.Hidden));
        Assert.That(mask.Visibility, Is.EqualTo(CMUZVisibility.Hidden));
    }

    [Test]
    public void CompleteFragmentedViewRetainsAperturesBeyondOldSampleBudget()
    {
        var openings = new Box2[64];
        for (var i = 0; i < openings.Length; i++)
            openings[i] = new Box2(i * 2f, 0f, i * 2f + 1f, 1f);
        var mask = new CMUZVisibilityMask();
        mask.SetOpenings(BaseMap, new Box2(0f, 0f, 128f, 2f), openings, complete: true);

        for (var i = 0; i < openings.Length; i++)
            Assert.That(mask.ClassifyBounds(openings[i]), Is.EqualTo(CMUZVisibility.Unknown), $"Opening {i}");
    }

    [Test]
    public void IntersectionBudgetDoesNotDiscardUncheckedBranches()
    {
        var mask = new CMUZVisibilityMask();
        mask.SetOpenings(BaseMap, ViewBounds, new[] { ViewBounds }, complete: true);
        mask.ConfirmVisible();
        var branches = new[] { new Box2(-10f, -10f, -9f, -9f), new Box2(9f, 9f, 10f, 10f) };

        Assert.That(mask.IntersectOpenings(branches, complete: true, maxFragments: 1), Is.False);
        Assert.Multiple(() =>
        {
            Assert.That(mask.Visibility, Is.EqualTo(CMUZVisibility.Unknown));
            Assert.That(mask.ClassifyBounds(branches[0]), Is.EqualTo(CMUZVisibility.Unknown));
            Assert.That(mask.ClassifyBounds(branches[1]), Is.EqualTo(CMUZVisibility.Unknown));
        });
    }

    [Test]
    public void BlurSupportAccumulatesAcrossRenderedFloors()
    {
        var first = new CMUZVisibilityMask();
        first.SetOpenings(BaseMap, ViewBounds, new[] { new Box2(0f, 0f, 1f, 1f) }, complete: true);
        var second = new CMUZVisibilityMask();
        second.SetProjected(first, LowerMap, new Vector2(0f, -0.75f), filterMargin: 0.1f);
        var third = new CMUZVisibilityMask();
        third.SetProjected(second, new MapId(3), new Vector2(0f, -0.75f), filterMargin: 0.1f);

        Assert.That(third.ClassifyBounds(new Box2(-0.19f, -1.5f, -0.18f, -1.4f)),
            Is.EqualTo(CMUZVisibility.Unknown));
        Assert.That(third.ClassifyBounds(new Box2(-0.3f, -1.5f, -0.25f, -1.4f)),
            Is.EqualTo(CMUZVisibility.Hidden));
    }

    [Test]
    public void CameraPlansAndLifecycleResetAreIndependent()
    {
        var first = new CMUZViewportRenderPlan();
        var second = new CMUZViewportRenderPlan();
        first.BaseOpenings.SetOpenings(BaseMap, ViewBounds, new[] { new Box2(0f, 0f, 1f, 1f) }, complete: true);
        second.BaseOpenings.SetOpenings(BaseMap, ViewBounds, new[] { new Box2(10f, 10f, 11f, 11f) }, complete: true);
        first.LowerPass(-1).SetProjected(first.BaseOpenings, LowerMap, Vector2.Zero);
        second.LowerPass(-1).SetProjected(second.BaseOpenings, LowerMap, Vector2.Zero);
        first.Reset();

        Assert.Multiple(() =>
        {
            Assert.That(first.FindLowerPass(-1, LowerMap), Is.Null);
            Assert.That(second.FindLowerPass(-1, LowerMap), Is.Not.Null);
            Assert.That(second.LowerPass(-1).ClassifyBounds(new Box2(10f, 10f, 11f, 11f)), Is.EqualTo(CMUZVisibility.Unknown));
            Assert.That(second.LowerPass(-1).ClassifyBounds(new Box2(0f, 0f, 1f, 1f)), Is.EqualTo(CMUZVisibility.Hidden));
            Assert.That(second.FindLowerPass(-1, BaseMap), Is.Null);
        });
    }

    [Test]
    public void RotatedStairTileUsesActualCornersForFrontHalfPlane()
    {
        var tile = new CMUZViewportRenderPlan.StairTile(new Vector2(0.8f, 1.3f), new Vector2(1.3f, 1.8f),
            new Vector2(1.8f, 1.3f), new Vector2(1.3f, 0.8f));

        Assert.Multiple(() =>
        {
            Assert.That(CMUZLevelStairPreviewVisibility.ProjectedCornersStayInFrontOfStair(
                Vector2.Zero, Vector2.One, tile.BottomLeft, tile.TopLeft, tile.TopRight, tile.BottomRight, Vector2.Zero), Is.True);
            Assert.That(CMUZLevelStairPreviewVisibility.ProjectedBoundsStayInFrontOfStair(
                Vector2.Zero, Vector2.One, tile.Bounds, Vector2.Zero), Is.False);
        });
    }
}
