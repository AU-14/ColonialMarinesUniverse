using System;
using System.Numerics;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Client.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DSceneLayoutTest
{
    [Test]
    public void RepeatedVehicleFramesKeepTheirPhysicalFacingAndResidualRotation()
    {
        var model = new CMU3DModelPrototype { SourceDirections = 4, SourceCardinalFacings = [0, 2, 2, 0], YawOffset = -90 };
        var angles = new[] { 0f, MathF.PI / 2, MathF.PI, -MathF.PI / 2 };
        var expectedFrontX = new[] { -1f, 1f, 1f, -1f };
        for (var index = 0; index < angles.Length; index++)
        {
            var yaw = CMU3DSceneLayout.RenderYaw(model, angles[index], true, false);
            Assert.That(MathF.Sin(yaw), Is.EqualTo(expectedFrontX[index]).Within(.00001));
            Assert.That(MathF.Cos(yaw), Is.Zero.Within(.00001));
        }
        var rotated = CMU3DSceneLayout.RenderYaw(model, MathF.PI / 2 + .1f, false, false);
        Assert.That(rotated, Is.EqualTo(MathF.PI / 2 + .1f).Within(.00001));
        Assert.That(CMU3DSceneLayout.RenderYaw(model, MathF.PI / 2 + .1f, true, false),
            Is.EqualTo(MathF.PI / 2).Within(.00001));
    }

    [Test]
    public void AliasedReferencesUseNearestPhysicalViewAndPreferRequestedSourceOnTies()
    {
        var model = new CMU3DModelPrototype { SourceDirections = 4, SourceCardinalFacings = [0, 2, 2, 0] };
        foreach (var direction in new[] { Direction.South, Direction.East, Direction.North, Direction.West })
            Assert.That(CMU3DSceneLayout.ReferenceDirection(model, direction), Is.EqualTo(direction));
        model.SourceCardinalFacings = [0, 0, 3, 3];
        Assert.That(CMU3DSceneLayout.ReferenceDirection(model, Direction.North), Is.EqualTo(Direction.North));
        Assert.That(CMU3DSceneLayout.ReferenceDirection(model, Direction.East), Is.EqualTo(Direction.East));
        model.SourceCardinalFacings = [3, 0, 0, 3];
        Assert.That(CMU3DSceneLayout.ReferenceDirection(model, Direction.North), Is.EqualTo(Direction.West));
    }

    [Test]
    public void NonRotationalCornerFramesJoinTheSavedNorthAndWestPipes()
    {
        var model = new CMU3DModelPrototype { SourceDirections = 4, SourceCardinalFacings = [0, 1, 3, 2] };
        var yaw = CMU3DSceneLayout.RenderYaw(model, -MathF.PI / 2, false, false);
        var pivot = new Vector2(105.5f, -45.5f);
        Vector2 Transform(Vector2 point) => pivot + new Vector2(
            MathF.Cos(yaw) * point.X - MathF.Sin(yaw) * point.Y,
            MathF.Sin(yaw) * point.X + MathF.Cos(yaw) * point.Y);
        Assert.That(Vector2.Distance(Transform(new Vector2(0, -.5f)), new Vector2(105.5f, -45)), Is.LessThan(.00001));
        Assert.That(Vector2.Distance(Transform(new Vector2(.5f, 0)), new Vector2(105, -45.5f)), Is.LessThan(.00001));
        Assert.That(CMU3DSceneLayout.RenderYaw(model, MathF.PI + .1f, false, false), Is.EqualTo(3 * MathF.PI / 2 + .1f).Within(.00001));
        Assert.That(CMU3DSceneLayout.ReferenceDirection(model, Direction.North), Is.EqualTo(Direction.West));
        Assert.That(CMU3DSceneLayout.ReferenceDirection(model, Direction.West), Is.EqualTo(Direction.North));
        Assert.That(CMU3DSceneLayout.ReferenceDirection(model, Direction.East), Is.EqualTo(Direction.East));
    }

    [Test]
    public void MonitorTargetsChooseTheVisibleWallSideAndRoomTileMount()
    {
        Assert.That(CMU3DSceneLayout.WallTargetYaw(MathF.PI, 0, true, 2, 12), Is.Zero);
        Assert.That(MathF.Abs(CMU3DSceneLayout.WallTargetYaw(0, 0, false, 2, 1)), Is.EqualTo(MathF.PI));
        Assert.That(CMU3DSceneLayout.WallTargetYaw(.2f, 0, true, 3, 12), Is.EqualTo(.2f));
        Assert.That(CMU3DSceneLayout.WallTargetYaw(.2f, 0, false, 2, 0), Is.EqualTo(.2f));
        Assert.That(CMU3DSceneLayout.WallTargetYaw(MathF.PI, MathF.PI / 2, true, 2, 12), Is.EqualTo(MathF.PI / 2));
    }

    [Test]
    public void VendorWindowBackingIsOptInAndDoesNotTreatDoorwaysAsBacking()
    {
        var model = new CMU3DModelPrototype { FaceAwayFromWall = true };
        Assert.That(CMU3DSceneLayout.IsApplianceBacking(model, "walls"), Is.True);
        Assert.That(CMU3DSceneLayout.IsApplianceBacking(model, "windows"), Is.False);
        model.FaceAwayFromWindows = true;
        Assert.That(CMU3DSceneLayout.IsApplianceBacking(model, "windows"), Is.True);
        Assert.That(CMU3DSceneLayout.IsApplianceBacking(model, "doors"), Is.False);
        // The saved zero-yaw vendors west-backed by glass must open east.
        Assert.That(CMU3DSceneLayout.FaceAwayFromWallYaw(0, 0, 8), Is.EqualTo(MathF.PI / 2));
        // Two opposing backing runs remain ambiguous, preserving the saved front.
        Assert.That(CMU3DSceneLayout.FaceAwayFromWallYaw(0, 0, 12), Is.Zero);
    }

    [Test]
    public void DirectionlessCounterAppliancesFaceTheRoomAndPreserveAmbiguousLayouts()
    {
        Assert.That(CMU3DSceneLayout.FaceAwayFromWallYaw(MathF.PI, 0, 8), Is.EqualTo(MathF.PI / 2));
        Assert.That(CMU3DSceneLayout.FaceAwayFromWallYaw(MathF.PI / 2, 0, 6), Is.EqualTo(-MathF.PI / 2));
        Assert.That(CMU3DSceneLayout.FaceAwayFromWallYaw(MathF.PI, 0, 1), Is.Zero);
        Assert.That(CMU3DSceneLayout.FaceAwayFromWallYaw(.2f, 0, 0), Is.EqualTo(.2f));
        Assert.That(CMU3DSceneLayout.FaceAwayFromWallYaw(MathF.PI / 2, 0, 12), Is.EqualTo(MathF.PI / 2));
        Assert.That(CMU3DSceneLayout.FaceAwayFromWallYaw(MathF.PI, MathF.PI / 2, 4), Is.Zero);
    }

    [Test]
    public void ReversedSourceSideFramesFaceOperatorsWithoutReversingResidualRotation()
    {
        var model = new CMU3DModelPrototype { SourceDirections = 4, SwapEastWest = true };
        Assert.That(CMU3DSceneLayout.RenderYaw(model, 0, false, false), Is.Zero);
        Assert.That(CMU3DSceneLayout.RenderYaw(model, MathF.PI, false, false), Is.EqualTo(MathF.PI));
        Assert.That(CMU3DSceneLayout.RenderYaw(model, -MathF.PI / 2, false, false), Is.EqualTo(MathF.PI / 2).Within(.00001));
        Assert.That(CMU3DSceneLayout.RenderYaw(model, MathF.PI / 2 + .1f, false, false),
            Is.EqualTo(3 * MathF.PI / 2 + .1f).Within(.00001));
        model.YawOffset = 90;
        Assert.That(CMU3DSceneLayout.RenderYaw(model, -MathF.PI / 2, false, false), Is.EqualTo(MathF.PI).Within(.00001));
    }

    [Test]
    public void ReferenceSideFramesFollowPhysicalReviewDirection()
    {
        var model = new CMU3DModelPrototype { SourceDirections = 4, SwapEastWest = true };
        Assert.That(CMU3DSceneLayout.ReferenceDirection(model, Direction.East), Is.EqualTo(Direction.West));
        Assert.That(CMU3DSceneLayout.ReferenceDirection(model, Direction.West), Is.EqualTo(Direction.East));
        Assert.That(CMU3DSceneLayout.ReferenceDirection(model, Direction.North), Is.EqualTo(Direction.North));
        Assert.That(CMU3DSceneLayout.ReferenceDirection(model, Direction.South), Is.EqualTo(Direction.South));
        model.SwapEastWest = false;
        Assert.That(CMU3DSceneLayout.ReferenceDirection(model, Direction.East), Is.EqualTo(Direction.East));
    }

    [Test]
    public void WallMountDepthCorrectionKeepsSpacingAlongWallAndRotatesWithGrid()
    {
        var offset = CMU3DSceneLayout.WallMountOffset(new Vector2(97.86334f, -46.53297f),
            new Vector2(97.5f, -46.5f), MathF.PI / 2);
        Assert.That(offset.X, Is.EqualTo(-.36334f).Within(.00001));
        Assert.That(offset.Y, Is.Zero.Within(.00001));
        var rotated = CMU3DSceneLayout.WallMountOffset(new Vector2(6.72f, 22.86f), new Vector2(6.5f,22.5f), MathF.PI);
        Assert.That(rotated.X, Is.Zero.Within(.00001));
        Assert.That(rotated.Y, Is.EqualTo(-.36f).Within(.00001));
    }

    [Test]
    public void AuthoredSourceTintIsAppliedOnceAndLiveChangesRemainRelative()
    {
        var tint = new Color(.5f, .75f, 1f, 1f);
        var model = new CMU3DModelPrototype { BakedSpriteTint = tint };
        Assert.That(CMU3DSceneLayout.PresentationTint(model, tint), Is.EqualTo(Color.White));
        Assert.That(CMU3DSceneLayout.PresentationTint(model, new Color(.25f, .375f, .5f, .5f)),
            Is.EqualTo(new Color(.5f, .5f, .5f, .5f)));
        Assert.That(CMU3DSceneLayout.PresentationTint(new CMU3DModelPrototype(), tint), Is.EqualTo(tint));
    }

    [Test]
    public void PhysicalCurtainAxisSurvivesBillboardCardinalSnapping()
    {
        var model = new CMU3DModelPrototype { UseEntityRotation = true };
        Assert.That(CMU3DSceneLayout.RenderYaw(model, MathF.PI / 2, false, true), Is.EqualTo(MathF.PI / 2));
        Assert.That(CMU3DSceneLayout.RenderYaw(model, MathF.PI, true, false), Is.EqualTo(MathF.PI));
        model.YawOffset = 90;
        Assert.That(CMU3DSceneLayout.RenderYaw(model, -MathF.PI / 2, false, true), Is.Zero.Within(.00001));
    }

    [Test]
    public void FixedSingleFrameDoesNotEraseDirectionalChairFacing()
    {
        var model = new CMU3DModelPrototype();
        Assert.That(CMU3DSceneLayout.RenderYaw(model, MathF.PI / 2, true, false), Is.Zero);
        Assert.That(CMU3DSceneLayout.RenderYaw(model, MathF.PI / 2 + .1f, false, true), Is.EqualTo(.1f).Within(.00001));
        model.SourceDirections = 4;
        Assert.That(CMU3DSceneLayout.RenderYaw(model, 1.4f, true, false), Is.EqualTo(MathF.PI / 2).Within(.00001));
        model.YawOffset = 90;
        Assert.That(CMU3DSceneLayout.RenderYaw(model, -MathF.PI / 2, false, false), Is.Zero.Within(.00001));
    }

    [Test]
    public void AdjacentWallFixtureStaysInsideRoomAndPreservesHeight()
    {
        CMU3DModelPart[] source = [new() { Min = new Vector3(-.4f, -.68f, 2.2f), Max = new Vector3(.4f, -.501f, 2.4f) }];
        var mounted = CMU3DSceneLayout.InsideWallParts(source)[0];
        Assert.That(mounted.Min.Y, Is.GreaterThan(-.5f));
        Assert.That(mounted.Max.Y, Is.EqualTo(-.32f).Within(.00001));
        Assert.That(mounted.Min.Z, Is.EqualTo(2.2f));
        Assert.That(mounted.Max.Z, Is.EqualTo(2.4f));
        Assert.That(source[0].Min.Y, Is.EqualTo(-.68f));
    }

    [Test]
    public void ConnectedPanelsFollowNorthSouthAndTurnAtCorners()
    {
        CMU3DModelPart[] source = [new() { Min = new Vector3(-.5f, -.08f, 0), Max = new Vector3(.5f, .08f, 2) }];
        var vertical = CMU3DSceneLayout.ConnectedParts(source, 3);
        Assert.That(vertical[0].Min, Is.EqualTo(new Vector3(-.08f, -.5f, 0)));
        Assert.That(vertical[0].Max, Is.EqualTo(new Vector3(.08f, .5f, 2)));
        var corner = CMU3DSceneLayout.ConnectedParts(source, 5);
        Assert.That(corner, Has.Length.EqualTo(2));
        foreach (var part in corner)
        {
            Assert.That(part.Valid, Is.True);
            Assert.That(part.Min.X, Is.GreaterThanOrEqualTo(-.08f));
            Assert.That(part.Min.Y, Is.GreaterThanOrEqualTo(-.08f));
        }
        Assert.That(source[0].Min, Is.EqualTo(new Vector3(-.5f, -.08f, 0)), "Shared prototype parts must stay unchanged.");
    }
}
