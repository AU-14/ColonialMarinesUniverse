using System;
using System.Numerics;
using Robust.Shared.Maths;

namespace Content.Shared.Vehicle;

public static class GridVehicleMotionSimulator
{
    private const float MinimumMovingSpeed = 0.01f;

    public readonly record struct DriveProfile(
        float MaxSpeed,
        float MaxReverseSpeed,
        float Acceleration,
        float ReverseAcceleration,
        float Deceleration);

    public readonly record struct DriveSpeedResult(
        float CurrentSpeed,
        bool ReversingInput,
        bool ChangingDirection);

    public readonly record struct AdvanceResult(
        Vector2 Position,
        Vector2i CurrentTile,
        float RemainingDistance,
        bool ReachedTarget);

    public static float StepIdleSpeed(float currentSpeed, float deceleration, float frameTime)
    {
        if (currentSpeed > 0f)
            return MathF.Max(0f, currentSpeed - deceleration * frameTime);

        if (currentSpeed < 0f)
            return MathF.Min(0f, currentSpeed + deceleration * frameTime);

        return 0f;
    }

    public static float GetEffectiveSteering(float steering, float currentSpeed, float throttle)
    {
        // Keep steering tied to the direction the vehicle is actually travelling.
        // At rest, use reverse throttle so the first reverse-turn frame is also inverted.
        var reversing = currentSpeed < -MinimumMovingSpeed ||
                        (MathF.Abs(currentSpeed) <= MinimumMovingSpeed && throttle < 0f);
        return reversing ? -steering : steering;
    }

    public static bool CanSteer(bool turnInPlace, float currentSpeed)
    {
        return turnInPlace || MathF.Abs(currentSpeed) > MinimumMovingSpeed;
    }

    /// <summary>
    /// Returns whether a movement step carries a vehicle away from an obstacle
    /// that already overlaps its starting pose. This is used only to let an
    /// embedded vehicle back out; movement further into the obstacle remains
    /// blocked.
    /// </summary>
    public static bool IsMovingAwayFromObstacle(
        Vector2 moveDelta,
        Vector2 vehicleCenter,
        Vector2 obstacleCenter)
    {
        if (moveDelta.LengthSquared() <= 0.000001f)
            return false;

        return Vector2.Dot(moveDelta, obstacleCenter - vehicleCenter) < -0.000001f;
    }

    /// <summary>
    /// Returns whether an obstacle lies against the chassis' forward face.
    /// Normalized local coordinates partition corner contacts consistently for
    /// rectangular vehicles, including while the chassis is freely rotated.
    /// </summary>
    public static bool IsFrontImpact(
        Vector2 vehicleWorldPosition,
        Angle vehicleWorldRotation,
        Box2 vehicleLocalBounds,
        Box2 obstacleWorldBounds)
    {
        var boundsCenter = vehicleWorldPosition + vehicleWorldRotation.RotateVec(vehicleLocalBounds.Center);
        var offset = obstacleWorldBounds.Center - boundsCenter;
        var forward = vehicleWorldRotation.ToWorldVec();
        var right = new Vector2(-forward.Y, forward.X);
        var forwardOffset = Vector2.Dot(offset, forward);
        var lateralOffset = MathF.Abs(Vector2.Dot(offset, right));
        var halfWidth = MathF.Max(vehicleLocalBounds.Width * 0.5f, 0.001f);
        var halfHeight = MathF.Max(vehicleLocalBounds.Height * 0.5f, 0.001f);

        // Robust world angles face south (local -Y) at zero rotation. Match the
        // same ToWorldVec convention used to advance the vehicle; treating local
        // +X as forward incorrectly classified head-on impacts as side impacts.
        return forwardOffset > 0f && forwardOffset / halfHeight > lateralOffset / halfWidth;
    }

    public static float GetPoweredDemolitionDamage(
        float damagePerSecond,
        float damageInterval,
        float plowPerformance)
    {
        return MathF.Max(0f, damagePerSecond) *
               MathF.Max(0f, damageInterval) *
               Math.Clamp(plowPerformance, 0f, 1f);
    }

    public static float StepPushSpeed(
        float currentSpeed,
        float maxSpeed,
        float acceleration,
        float deceleration,
        bool hasInput,
        bool isCommittedToMove,
        float frameTime)
    {
        var hasInputForSpeed = hasInput || isCommittedToMove;
        float targetSpeed;
        float accel;

        if (!hasInputForSpeed)
        {
            targetSpeed = 0f;
            accel = deceleration;
        }
        else
        {
            targetSpeed = maxSpeed;
            accel = acceleration;
        }

        return StepTowardsTargetSpeed(currentSpeed, targetSpeed, accel, frameTime);
    }

    public static DriveSpeedResult StepDriveSpeed(
        float currentSpeed,
        DriveProfile profile,
        Vector2i facing,
        Vector2i inputDir,
        bool hasInput,
        bool isCommittedToMove,
        float frameTime)
    {
        var hasInputForSpeed = hasInput || isCommittedToMove;
        var reversing = hasInput && facing != Vector2i.Zero && inputDir == -facing;

        float targetSpeed;
        float accel;

        if (!hasInputForSpeed)
        {
            targetSpeed = 0f;
            accel = profile.Deceleration;
        }
        else if (reversing)
        {
            if (currentSpeed > 0f)
            {
                targetSpeed = 0f;
                accel = profile.Deceleration;
            }
            else
            {
                targetSpeed = -profile.MaxReverseSpeed;
                accel = profile.ReverseAcceleration;
            }
        }
        else
        {
            if (currentSpeed < 0f && hasInputForSpeed)
            {
                targetSpeed = 0f;
                accel = profile.Deceleration;
            }
            else
            {
                targetSpeed = profile.MaxSpeed;
                accel = profile.Acceleration;
            }
        }

        var steppedSpeed = StepTowardsTargetSpeed(currentSpeed, targetSpeed, accel, frameTime);
        var changingDirection =
            MathF.Abs(steppedSpeed) > MinimumMovingSpeed &&
            ((reversing && steppedSpeed > 0f) ||
             (!reversing && steppedSpeed < 0f));

        return new DriveSpeedResult(steppedSpeed, reversing, changingDirection);
    }

    public static AdvanceResult AdvanceToTarget(
        Vector2 position,
        Vector2i currentTile,
        Vector2i targetTile,
        Vector2 targetPosition,
        float travelDistance)
    {
        var toTarget = targetPosition - position;
        var distToTarget = toTarget.Length();

        if (distToTarget <= 0.0001f || travelDistance >= distToTarget)
        {
            return new AdvanceResult(
                targetPosition,
                targetTile,
                MathF.Max(0f, travelDistance - distToTarget),
                true);
        }

        var dir = toTarget / distToTarget;
        return new AdvanceResult(
            position + dir * travelDistance,
            currentTile,
            0f,
            false);
    }

    private static float StepTowardsTargetSpeed(
        float currentSpeed,
        float targetSpeed,
        float accelerateTowardTarget,
        float frameTime)
    {
        if (currentSpeed < targetSpeed)
            return MathF.Min(currentSpeed + accelerateTowardTarget * frameTime, targetSpeed);

        if (currentSpeed > targetSpeed)
            return MathF.Max(currentSpeed - accelerateTowardTarget * frameTime, targetSpeed);

        return currentSpeed;
    }
}
