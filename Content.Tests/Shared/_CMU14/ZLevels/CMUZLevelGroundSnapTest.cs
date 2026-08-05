using System.Collections.Generic;
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
    public void UpwardGroundContactDoesNotBounce()
    {
        Assert.That(ShouldBounceOnGroundContact(0.9f), Is.False);
    }

    [Test]
    public void DownwardGroundContactBounces()
    {
        Assert.That(ShouldBounceOnGroundContact(-0.9f), Is.True);
    }

    [Test]
    public void VehicleUpwardGroundContactSettles()
    {
        Assert.That(ShouldSettleNonBouncingGroundContact(true, 0.9f), Is.True);
    }

    [Test]
    public void NonVehicleUpwardGroundContactDoesNotSettle()
    {
        Assert.That(ShouldSettleNonBouncingGroundContact(false, 0.9f), Is.False);
    }

    [Test]
    public void SettledStickyGroundSleepsAtStairHeight()
    {
        Assert.That(ShouldSleepZPhysics(0f, true, 0.5f, 0f), Is.True);
    }

    [Test]
    public void UnsettledStickyGroundStaysAwake()
    {
        Assert.That(ShouldSleepZPhysics(-0.2f, true, 0.5f, 0f), Is.False);
    }

    [Test]
    public void MovingStickyGroundStaysAwake()
    {
        Assert.That(ShouldSleepZPhysics(0f, true, 0.5f, 0.1f), Is.False);
    }

    [Test]
    public void NonStickyGroundOnlySleepsAtBaseHeight()
    {
        Assert.That(ShouldSleepZPhysics(0f, false, 0.5f, 0f), Is.False);
        Assert.That(ShouldSleepZPhysics(0f, false, 0f, 0f), Is.True);
    }

    [Test]
    public void DownTransitionOvershootClampsToCurrentFloor()
    {
        Assert.That(ShouldClampAfterDownTransition(-0.2f, -0.2f, false, -5f), Is.True);
    }

    [Test]
    public void DownTransitionDoesNotClampWhileStillAboveCurrentFloor()
    {
        Assert.That(ShouldClampAfterDownTransition(0.9f, 0.9f, false, -5f), Is.False);
    }

    [Test]
    public void DownTransitionThroughOpeningKeepsFallingTowardNextFloor()
    {
        Assert.That(ShouldClampAfterDownTransition(-0.2f, 0.8f, false, -5f), Is.False);
    }

    [Test]
    public void DownTransitionThroughOpeningIgnoresStickySupportStillBelow()
    {
        Assert.That(ShouldClampAfterDownTransition(-0.2f, 0.8f, true, -5f), Is.False);
    }

    [Test]
    public void DownTransitionOvershootPastStepWindowRequiresDownwardVelocity()
    {
        Assert.That(ShouldClampAfterDownTransition(-0.9f, -0.9f, false, 1f), Is.False);
        Assert.That(ShouldClampAfterDownTransition(-0.9f, -0.9f, false, -1f), Is.True);
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
    public void TinyNegativeLocalHeightDoesNotProcessDownBoundary()
    {
        Assert.That(ShouldProcessDownBoundary(-0.001f), Is.False);
    }

    [Test]
    public void NegativeLocalHeightPastToleranceProcessesDownBoundary()
    {
        Assert.That(ShouldProcessDownBoundary(-0.051f), Is.True);
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
        return CMUSharedZLevelsSystem.ShouldSnapToGround(distanceToGround, stickyGround);
    }

    private static float GetGroundSnapDistance(float distanceToGround, bool stickyGround)
    {
        return CMUSharedZLevelsSystem.GetGroundSnapDistance(distanceToGround, stickyGround);
    }

    private static float GetMoveGroundSnapDistance(float distanceToGround, bool stickyGround)
    {
        return CMUSharedZLevelsSystem.GetMoveGroundSnapDistance(distanceToGround, stickyGround);
    }

    private static bool ShouldTreatAsGroundContact(float distanceToGround, bool stickyGround)
    {
        return CMUSharedZLevelsSystem.ShouldTreatAsGroundContact(distanceToGround, stickyGround);
    }

    private static bool ShouldBounceOnGroundContact(float velocity)
    {
        return CMUSharedZLevelsSystem.ShouldBounceOnGroundContact(velocity);
    }

    private static bool ShouldSettleNonBouncingGroundContact(bool isVehicle, float velocity)
    {
        return CMUSharedZLevelsSystem.ShouldSettleNonBouncingGroundContact(isVehicle, velocity);
    }

    private static bool ShouldProcessDownBoundary(float localPosition)
    {
        return CMUSharedZLevelsSystem.ShouldProcessDownBoundary(localPosition);
    }

    private static bool ShouldSleepZPhysics(
        float distanceToGround,
        bool stickyGround,
        float localPosition,
        float velocity)
    {
        return CMUSharedZLevelsSystem.ShouldSleepZPhysics(
            distanceToGround,
            stickyGround,
            localPosition,
            velocity);
    }

    private static bool ShouldClampAfterDownTransition(
        float localPosition,
        float distanceToGround,
        bool stickyGround,
        float velocity)
    {
        return CMUSharedZLevelsSystem.ShouldClampAfterDownTransition(
            localPosition,
            distanceToGround,
            stickyGround,
            velocity);
    }

    private static bool ShouldProcessMoveSnapZLevelTransition(float localPosition, bool stickyGround)
    {
        return CMUSharedZLevelsSystem.ShouldProcessMoveSnapZLevelTransition(localPosition, stickyGround);
    }

    private static bool ShouldProcessImmediateMoveSnapZLevelTransition(
        bool isClient,
        float localPosition,
        bool stickyGround)
    {
        return CMUSharedZLevelsSystem.ShouldProcessImmediateMoveSnapZLevelTransition(
            isClient,
            localPosition,
            stickyGround);
    }

    private static bool ShouldAdvanceStickyMoveSnapToUpperBoundary(float localPosition, bool stickyGround)
    {
        return CMUSharedZLevelsSystem.ShouldAdvanceStickyMoveSnapToUpperBoundary(localPosition, stickyGround);
    }

    private static bool ShouldProcessMoveGroundSnap(bool isClient, bool applyingState)
    {
        return CMUSharedZLevelsSystem.ShouldProcessMoveGroundSnap(isClient, applyingState);
    }

    private static bool ShouldUseStickyGround(bool isCurrentTile, float velocity, bool stick)
    {
        return CMUSharedZLevelsSystem.ShouldUseStickyGround(
            isCurrentTile,
            velocity,
            new CMUZLevelHighGroundComponent
            {
                Stick = stick,
            });
    }

    private static bool ShouldReplaceHighGroundCandidate(
        bool isCurrentTile,
        float score,
        bool found,
        bool bestIsCurrentTile,
        float bestScore)
    {
        return CMUSharedZLevelsSystem.ShouldReplaceHighGroundCandidate(
            isCurrentTile,
            score,
            found,
            bestIsCurrentTile,
            bestScore);
    }

    private static bool ShouldUseSweptStickyHighGround(
        List<float> heightCurve,
        float oldT,
        float newT,
        float velocity)
    {
        return CMUSharedZLevelsSystem.ShouldUseSweptStickyHighGround(
            new CMUZLevelHighGroundComponent
            {
                Stick = true,
                HeightCurve = heightCurve,
            },
            oldT,
            newT,
            velocity);
    }

    private static bool ShouldUseSweptStickyMoveSnap(float candidateSnappedLocalPosition, float bestSnappedLocalPosition)
    {
        return CMUSharedZLevelsSystem.ShouldUseSweptStickyMoveSnap(
            candidateSnappedLocalPosition,
            bestSnappedLocalPosition);
    }
}
