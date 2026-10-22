using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// Scales the damage a vehicle takes from crashing into things with how fast it hit: the slowest
/// impact that hurts at all costs <see cref="MinDamageFraction"/> of the full damage, a crash at top
/// speed all of it. Each crash is raised on the vehicle as a <see cref="CMUVehicleCrashedEvent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUVehicleCrashComponent : Component
{
    [DataField]
    public float MinDamageFraction = 1f / 3f;

    /// <summary>
    /// Crashes, into walls or people, wear only the wheels, never the guns or other hardpoints.
    /// </summary>
    [DataField]
    public bool WheelsOnly = true;

    /// <summary>
    /// The share of full crash damage taken at an impact speed: <paramref name="minFraction"/> at
    /// <paramref name="minSpeed"/>, rising evenly to all of it at <paramref name="maxSpeed"/>.
    /// </summary>
    public static float Scale(float speed, float minSpeed, float maxSpeed, float minFraction)
    {
        if (maxSpeed <= minSpeed)
            return 1f;

        var t = Math.Clamp((speed - minSpeed) / (maxSpeed - minSpeed), 0f, 1f);
        return minFraction + (1f - minFraction) * t;
    }

    /// <summary>
    /// Whether an obstacle touches a vehicle's front: of the vehicle's faces, in its own space (front
    /// is -y), the one the obstacle reaches least past. A wide obstacle met bumper first counts.
    /// </summary>
    public static bool IsFrontContact(Vector2 position, Angle rotation, Box2 local, Box2 obstacle)
    {
        var toLocal = -rotation;
        var min = new Vector2(float.MaxValue);
        var max = new Vector2(float.MinValue);
        foreach (var corner in new[] { obstacle.BottomLeft, obstacle.BottomRight, obstacle.TopLeft, obstacle.TopRight })
        {
            var point = toLocal.RotateVec(corner - position);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }

        var front = max.Y - local.Bottom;
        var rear = local.Top - min.Y;
        var left = max.X - local.Left;
        var right = local.Right - min.X;
        return front <= MathF.Min(rear, MathF.Min(left, right));
    }

    /// <summary>
    /// Where an obstacle meets a vehicle, in the vehicle's own space (front is -y): the middle of
    /// where the two overlap, or of the vehicle's nearest edge if they only touch.
    /// </summary>
    public static Vector2 ContactPoint(Vector2 position, Angle rotation, Box2 local, Box2 obstacle)
    {
        var toLocal = -rotation;
        var min = new Vector2(float.MaxValue);
        var max = new Vector2(float.MinValue);
        foreach (var corner in new[] { obstacle.BottomLeft, obstacle.BottomRight, obstacle.TopLeft, obstacle.TopRight })
        {
            var point = toLocal.RotateVec(corner - position);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }

        static float Middle(float min, float max, float lo, float hi)
        {
            var from = Math.Clamp(min, lo, hi);
            var to = Math.Clamp(max, lo, hi);
            return (from + to) / 2f;
        }

        return new Vector2(Middle(min.X, max.X, local.Left, local.Right), Middle(min.Y, max.Y, local.Bottom, local.Top));
    }
}

/// <summary>
/// A vehicle crashed into something and its hull took <see cref="HullDamage"/>. <see cref="Frontal"/>
/// when it drove forward into something ahead of its front; <see cref="Speed"/> is how fast it hit,
/// as a share of its top speed; <see cref="Contact"/>, where it was hit, in its own space with it
/// facing south (front is -y), if known.
/// </summary>
[ByRefEvent]
public readonly record struct CMUVehicleCrashedEvent(float HullDamage, bool Frontal, float Speed = 0f, Vector2? Contact = null);
