using System.Collections.Generic;
using System.Reflection;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using NUnit.Framework;

namespace Content.Tests.Shared._CMU14.ZLevels;

[TestFixture]
public sealed class CMUZLevelGroundSnapTest
{
    [Test]
    public void StickyGroundSnapsPastNormalStepHeight()
    {
        Assert.That(ShouldSnapToGround(0.9f, true), Is.True);
    }

    [Test]
    public void StickyGroundSnapsUpPastNormalStepHeight()
    {
        Assert.That(ShouldSnapToGround(-0.9f, true), Is.True);
    }

    [Test]
    public void NonStickyGroundDoesNotSnapPastNormalStepHeight()
    {
        Assert.That(ShouldSnapToGround(0.9f, false), Is.False);
    }

    [Test]
    public void NonStickyGroundDoesNotSnapUpPastNormalStepHeight()
    {
        Assert.That(ShouldSnapToGround(-0.9f, false), Is.False);
    }

    [Test]
    public void NonStickyGroundSnapsUpWithinNormalStepHeight()
    {
        Assert.That(ShouldSnapToGround(-0.4f, false), Is.True);
    }

    [Test]
    public void CloseNonStickyGroundSnaps()
    {
        Assert.That(ShouldSnapToGround(0.04f, false), Is.True);
    }

    [Test]
    public void StickyGroundKeepsPhysicsGroundedPastNormalStepHeight()
    {
        Assert.That(ShouldTreatAsGroundContact(0.9f, true), Is.True);
    }

    [Test]
    public void StickyGroundKeepsPhysicsGroundedWhenTooFarAbove()
    {
        Assert.That(ShouldTreatAsGroundContact(-0.9f, true), Is.True);
    }

    [Test]
    public void NonStickyGroundDoesNotKeepPhysicsGroundedPastNormalStepHeight()
    {
        Assert.That(ShouldTreatAsGroundContact(0.9f, false), Is.False);
    }

    [Test]
    public void NonStickyGroundDoesNotKeepPhysicsGroundedWhenTooFarAbove()
    {
        Assert.That(ShouldTreatAsGroundContact(-0.9f, false), Is.False);
    }

    [Test]
    public void CloseNonStickyGroundKeepsPhysicsGrounded()
    {
        Assert.That(ShouldTreatAsGroundContact(0.04f, false), Is.True);
    }

    [Test]
    public void StickyGroundLargeStepUpFollowsGroundHeightImmediately()
    {
        Assert.That(GetGroundSnapDistance(-0.9f, true), Is.EqualTo(-0.9f).Within(0.001f));
    }

    [Test]
    public void StickyGroundSmallStepUpSnapsImmediately()
    {
        Assert.That(GetGroundSnapDistance(-0.04f, true), Is.EqualTo(-0.04f).Within(0.001f));
    }

    [Test]
    public void StickyGroundStepDownSnapsImmediately()
    {
        Assert.That(GetGroundSnapDistance(0.9f, true), Is.EqualTo(0.9f).Within(0.001f));
    }

    [Test]
    public void StickyGroundMoveSnapAppliesLargeStepUp()
    {
        Assert.That(GetMoveGroundSnapDistance(-0.9f, true), Is.EqualTo(-0.9f).Within(0.001f));
    }

    [Test]
    public void StickyGroundMoveSnapAppliesLargeStepDown()
    {
        Assert.That(GetMoveGroundSnapDistance(0.9f, true), Is.EqualTo(0.9f).Within(0.001f));
    }

    [Test]
    public void NonStickyMoveSnapIgnoresLargeStepUp()
    {
        Assert.That(GetMoveGroundSnapDistance(-0.9f, false), Is.Zero);
    }

    [Test]
    public void StickyMoveSnapProcessesUpperZBoundary()
    {
        Assert.That(ShouldProcessMoveSnapZLevelTransition(1.05f, true), Is.True);
    }

    [Test]
    public void StickyMoveSnapProcessesNearUpperZBoundary()
    {
        Assert.That(ShouldProcessMoveSnapZLevelTransition(0.95f, true), Is.True);
    }

    [Test]
    public void StickyMoveSnapProcessesLowerZBoundary()
    {
        Assert.That(ShouldProcessMoveSnapZLevelTransition(-0.05f, true), Is.True);
    }

    [Test]
    public void StickyMoveSnapIgnoresNormalHeight()
    {
        Assert.That(ShouldProcessMoveSnapZLevelTransition(0.5f, true), Is.False);
    }

    [Test]
    public void NonStickyMoveSnapDoesNotProcessBoundaryImmediately()
    {
        Assert.That(ShouldProcessMoveSnapZLevelTransition(1.05f, false), Is.False);
    }

    [Test]
    public void StickyMoveSnapAdvancesNearUpperBoundary()
    {
        Assert.That(ShouldAdvanceStickyMoveSnapToUpperBoundary(0.95f, true), Is.True);
    }

    [Test]
    public void StickyMoveSnapDoesNotAdvancePastUpperBoundary()
    {
        Assert.That(ShouldAdvanceStickyMoveSnapToUpperBoundary(1.05f, true), Is.False);
    }

    [Test]
    public void NonStickyMoveSnapDoesNotAdvanceNearUpperBoundary()
    {
        Assert.That(ShouldAdvanceStickyMoveSnapToUpperBoundary(0.95f, false), Is.False);
    }

    [Test]
    public void ServerMoveSnapProcessesBoundaryImmediately()
    {
        Assert.That(ShouldProcessImmediateMoveSnapZLevelTransition(false, 0.95f, true), Is.True);
    }

    [Test]
    public void ClientMoveSnapProcessesUpperBoundaryImmediately()
    {
        Assert.That(ShouldProcessImmediateMoveSnapZLevelTransition(true, 0.95f, true), Is.True);
    }

    [Test]
    public void ClientMoveSnapDoesNotProcessLowerBoundaryImmediately()
    {
        Assert.That(ShouldProcessImmediateMoveSnapZLevelTransition(true, -0.05f, true), Is.False);
    }

    [Test]
    public void ServerMoveSnapRunsOutsidePrediction()
    {
        Assert.That(ShouldProcessMoveGroundSnap(false, false), Is.True);
    }

    [Test]
    public void ClientMoveSnapRunsReprediction()
    {
        Assert.That(ShouldProcessMoveGroundSnap(true, false), Is.True);
    }

    [Test]
    public void ClientMoveSnapSkipsStateApplication()
    {
        Assert.That(ShouldProcessMoveGroundSnap(true, true), Is.False);
    }

    [Test]
    public void NonStickyGroundStepUpSnapsImmediatelyWithinNormalStepHeight()
    {
        Assert.That(GetGroundSnapDistance(-0.4f, false), Is.EqualTo(-0.4f).Within(0.001f));
    }

    [Test]
    public void NonStickyGroundStepUpPastNormalStepHeightDoesNotSnap()
    {
        Assert.That(GetGroundSnapDistance(-0.9f, false), Is.Zero);
    }

    [Test]
    public void CurrentStickyHighGroundUsesStickyGround()
    {
        Assert.That(ShouldUseStickyGround(true, 0f, true), Is.True);
    }

    [Test]
    public void NeighborStickyHighGroundUsesStickyGround()
    {
        Assert.That(ShouldUseStickyGround(false, 0f, true), Is.True);
    }

    [Test]
    public void CurrentNonStickyHighGroundDoesNotUseStickyGround()
    {
        Assert.That(ShouldUseStickyGround(true, 0f, false), Is.False);
    }

    [Test]
    public void CurrentTileHighGroundBeatsCloserNeighborSupport()
    {
        Assert.That(
            ShouldReplaceHighGroundCandidate(
                true,
                0.4f,
                true,
                false,
                0.01f),
            Is.True);
    }

    [Test]
    public void NeighborSupportDoesNotBeatCurrentTileHighGround()
    {
        Assert.That(
            ShouldReplaceHighGroundCandidate(
                false,
                0.01f,
                true,
                true,
                0.4f),
            Is.False);
    }

    [Test]
    public void SweptStickyHighGroundUsesDescendingCurveWhenMovingTowardTop()
    {
        Assert.That(ShouldUseSweptStickyHighGround(new List<float> { 1.05f, 0.1f }, 0.8f, 0.1f, 0f), Is.True);
    }

    [Test]
    public void SweptStickyHighGroundIgnoresDescendingCurveWhenMovingAwayFromTop()
    {
        Assert.That(ShouldUseSweptStickyHighGround(new List<float> { 1.05f, 0.1f }, 0.1f, 0.8f, 0f), Is.False);
    }

    [Test]
    public void SweptStickyMoveSnapUsesUpperBoundaryCandidate()
    {
        Assert.That(ShouldUseSweptStickyMoveSnap(0.95f, 0f), Is.True);
    }

    [Test]
    public void SweptStickyMoveSnapIgnoresBelowBoundaryCandidate()
    {
        Assert.That(ShouldUseSweptStickyMoveSnap(0.9f, 0f), Is.False);
    }

    private static bool ShouldSnapToGround(float distanceToGround, bool stickyGround)
    {
        var method = typeof(CMUSharedZLevelsSystem).GetMethod(
            "ShouldSnapToGround",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);
        return (bool) method!.Invoke(null, new object[] { distanceToGround, stickyGround })!;
    }

    private static float GetGroundSnapDistance(float distanceToGround, bool stickyGround)
    {
        var method = typeof(CMUSharedZLevelsSystem).GetMethod(
            "GetGroundSnapDistance",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);
        return (float) method!.Invoke(null, new object[] { distanceToGround, stickyGround })!;
    }

    private static float GetMoveGroundSnapDistance(float distanceToGround, bool stickyGround)
    {
        var method = typeof(CMUSharedZLevelsSystem).GetMethod(
            "GetMoveGroundSnapDistance",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);
        return (float) method!.Invoke(null, new object[] { distanceToGround, stickyGround })!;
    }

    private static bool ShouldTreatAsGroundContact(float distanceToGround, bool stickyGround)
    {
        var method = typeof(CMUSharedZLevelsSystem).GetMethod(
            "ShouldTreatAsGroundContact",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);
        return (bool) method!.Invoke(null, new object[] { distanceToGround, stickyGround })!;
    }

    private static bool ShouldProcessMoveSnapZLevelTransition(float localPosition, bool stickyGround)
    {
        var method = typeof(CMUSharedZLevelsSystem).GetMethod(
            "ShouldProcessMoveSnapZLevelTransition",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);
        return (bool) method!.Invoke(null, new object[] { localPosition, stickyGround })!;
    }

    private static bool ShouldProcessImmediateMoveSnapZLevelTransition(
        bool isClient,
        float localPosition,
        bool stickyGround)
    {
        var method = typeof(CMUSharedZLevelsSystem).GetMethod(
            "ShouldProcessImmediateMoveSnapZLevelTransition",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);
        return (bool) method!.Invoke(null, new object[] { isClient, localPosition, stickyGround })!;
    }

    private static bool ShouldAdvanceStickyMoveSnapToUpperBoundary(float localPosition, bool stickyGround)
    {
        var method = typeof(CMUSharedZLevelsSystem).GetMethod(
            "ShouldAdvanceStickyMoveSnapToUpperBoundary",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);
        return (bool) method!.Invoke(null, new object[] { localPosition, stickyGround })!;
    }

    private static bool ShouldProcessMoveGroundSnap(bool isClient, bool applyingState)
    {
        var method = typeof(CMUSharedZLevelsSystem).GetMethod(
            "ShouldProcessMoveGroundSnap",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);
        return (bool) method!.Invoke(null, new object[] { isClient, applyingState })!;
    }

    private static bool ShouldUseStickyGround(bool isCurrentTile, float velocity, bool stick)
    {
        var method = typeof(CMUSharedZLevelsSystem).GetMethod(
            "ShouldUseStickyGround",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);
        return (bool) method!.Invoke(
            null,
            new object[]
            {
                isCurrentTile,
                velocity,
                new CMUZLevelHighGroundComponent
                {
                    Stick = stick,
                },
            })!;
    }

    private static bool ShouldReplaceHighGroundCandidate(
        bool isCurrentTile,
        float score,
        bool found,
        bool bestIsCurrentTile,
        float bestScore)
    {
        var method = typeof(CMUSharedZLevelsSystem).GetMethod(
            "ShouldReplaceHighGroundCandidate",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);
        return (bool) method!.Invoke(
            null,
            new object[]
            {
                isCurrentTile,
                score,
                found,
                bestIsCurrentTile,
                bestScore,
            })!;
    }

    private static bool ShouldUseSweptStickyHighGround(
        List<float> heightCurve,
        float oldT,
        float newT,
        float velocity)
    {
        var method = typeof(CMUSharedZLevelsSystem).GetMethod(
            "ShouldUseSweptStickyHighGround",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);
        return (bool) method!.Invoke(
            null,
            new object[]
            {
                new CMUZLevelHighGroundComponent
                {
                    Stick = true,
                    HeightCurve = heightCurve,
                },
                oldT,
                newT,
                velocity,
            })!;
    }

    private static bool ShouldUseSweptStickyMoveSnap(float candidateSnappedLocalPosition, float bestSnappedLocalPosition)
    {
        var method = typeof(CMUSharedZLevelsSystem).GetMethod(
            "ShouldUseSweptStickyMoveSnap",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);
        return (bool) method!.Invoke(
            null,
            new object[]
            {
                candidateSnappedLocalPosition,
                bestSnappedLocalPosition,
            })!;
    }
}
