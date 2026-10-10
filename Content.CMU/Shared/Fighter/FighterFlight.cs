using System.Numerics;

namespace Content.Shared.CMU14.Fighter;

/// <summary>Route geometry, altitude and visibility shared by the server and cockpit displays.</summary>
public static class FighterFlight
{
    public const float StepSeconds = 1f / 30;
    public const float MarkLifetime = 60;
    public const float CloudSpacing = 60;
    public const float MinimumHeight = 150;
    public const float MaximumHeight = 1800;
    public const float CloudBase = 600;
    public const float CloudTop = 850;
    public const float CorridorHalfWidth = 8;

    public static Vector2 Forward(float heading) => new(MathF.Sin(heading), MathF.Cos(heading));
    public static bool InAirspace(FighterAircraftComponent a) => a.GroundState is FighterGroundState.Airborne or FighterGroundState.Returning or FighterGroundState.Crashing;
    public static bool GroundScene(FighterAircraftComponent a) => a.GroundEntity != null && !InAirspace(a);
    public static bool InAttackRun(FighterAircraftComponent a) => a.GroundState == FighterGroundState.Airborne &&
        a.Flying && a.Phase is FighterPhase.Approach or FighterPhase.Pass && a.Battlefield.Contains(a.Position);
    public static Vector2 HoldingPoint(FighterAircraftComponent a) => a.Home + new Vector2(.65f, -.65f) * a.AirspaceRadius;
    public static bool Finite(Vector2 p) => float.IsFinite(p.X) && float.IsFinite(p.Y);
    public static bool CanControl(bool pilot, FighterCommand command) => command switch
    {
        FighterCommand.FormUp or FighterCommand.Takeoff or FighterCommand.Launch or FighterCommand.Ascend or FighterCommand.Descend or FighterCommand.Return or FighterCommand.Flares or
            FighterCommand.PrepareRun or FighterCommand.QueueFire => pilot,
        FighterCommand.TrainingRelease => !pilot,
        FighterCommand.Mark or FighterCommand.Zoom or FighterCommand.Center or FighterCommand.Lock or FighterCommand.Fire => true,
        FighterCommand.VisionNormal or FighterCommand.VisionNight or FighterCommand.VisionThermal => true,
        FighterCommand.Laser => true,
        FighterCommand.SwapSeat or FighterCommand.LeaveSeat or FighterCommand.ConfirmEject or FighterCommand.CancelEject => true,
        _ => false,
    };

    public static bool TryPlan(FighterAircraftComponent a, Vector2 entry, Vector2 exit)
    {
        if (a.Phase != FighterPhase.Holding || !Finite(entry) || !Finite(exit) ||
            Vector2.DistanceSquared(entry, exit) < 40 * 40 ||
            Vector2.DistanceSquared(entry, a.Home) > a.AirspaceRadius * a.AirspaceRadius ||
            Vector2.DistanceSquared(exit, a.Home) > a.AirspaceRadius * a.AirspaceRadius)
            return false;
        var delta = exit - entry;
        var lower = 0f;
        var upper = 1f;
        // Clip the segment to the battlefield: routes wholly outside it are invalid.
        for (var axis = 0; axis < 2; axis++)
        {
            var origin = axis == 0 ? entry.X : entry.Y;
            var direction = axis == 0 ? delta.X : delta.Y;
            var min = axis == 0 ? a.Battlefield.Left : a.Battlefield.Bottom;
            var max = axis == 0 ? a.Battlefield.Right : a.Battlefield.Top;
            if (MathF.Abs(direction) < .001f)
            {
                if (origin < min || origin > max)
                    return false;
                continue;
            }
            var near = (min - origin) / direction;
            var far = (max - origin) / direction;
            lower = Math.Max(lower, Math.Min(near, far));
            upper = Math.Min(upper, Math.Max(near, far));
        }
        if (upper - lower < .02f)
            return false;
        a.Entry = entry;
        a.Exit = exit;
        return true;
    }

    public static bool TrySettings(FighterAircraftComponent a, float height, float speed)
    {
        if (!float.IsFinite(height) || !float.IsFinite(speed) || height < MinimumHeight || height > MaximumHeight ||
            speed < a.MinimumSpeed || speed > a.MaximumSpeed)
            return false;
        a.TargetHeight = height;
        a.TargetSpeed = speed;
        return true;
    }

    public static bool Launch(FighterAircraftComponent a)
    {
        if (a.GroundState != FighterGroundState.Airborne || a.ForcedRetreat || !TryPlan(a, a.Entry, a.Exit))
            return false;
        BeginLeg(a, FighterPhase.Approach, a.Entry);
        a.Flying = true;
        a.TrainingImpact = null;
        a.PassNumber++;
        return true;
    }

    public static void Abort(FighterAircraftComponent a)
    {
        if (a.Phase is FighterPhase.Approach or FighterPhase.Pass)
            BeginLeg(a, FighterPhase.Return, HoldingPoint(a));
    }

    private static void BeginLeg(FighterAircraftComponent a, FighterPhase phase, Vector2 end)
    {
        a.Phase = phase;
        a.Progress = 0;
        a.LegStart = a.Position;
        a.LegEnd = end;
        var distance = Vector2.Distance(a.Position, end) * .45f;
        var direction = Vector2.Normalize(a.Exit - a.Entry);
        a.LegControl1 = a.Position + Forward(a.Heading) * distance;
        a.LegControl2 = end - (phase == FighterPhase.Approach ? direction : Vector2.UnitX) * distance;
        a.Bank = 0;
        a.LegTravel = 0;
        a.LegDistances = new float[257];
        var previous = Curve(a, 0);
        for (var i = 1; i < a.LegDistances.Length; i++)
        {
            var point = Curve(a, i / 256f);
            a.LegDistances[i] = a.LegDistances[i - 1] + Vector2.Distance(previous, point);
            previous = point;
        }
    }

    public static Vector2 Curve(FighterAircraftComponent a, float t)
    {
        if (a.Phase == FighterPhase.Pass)
            return Vector2.Lerp(a.Entry, a.Exit, t);
        var u = 1 - t;
        return u * u * u * a.LegStart + 3 * u * u * t * a.LegControl1 + 3 * u * t * t * a.LegControl2 + t * t * t * a.LegEnd;
    }

    public static void Step(FighterAircraftComponent a, FighterInput input, float seconds)
    {
        if (!InAirspace(a) || a.GroundState == FighterGroundState.Crashing) return;
        if (!float.IsFinite(seconds) || seconds <= 0)
            return;
        seconds = Math.Min(seconds, StepSeconds);
        a.Height = Approach(a.Height, a.TargetHeight, 130 * seconds);
        a.Altitude = a.Height < CloudBase ? FighterAltitude.Low : a.Height <= CloudTop ? FighterAltitude.Cloud : FighterAltitude.High;
        a.TargetSpeed = Math.Clamp(a.TargetSpeed + Axis(input, FighterInput.Forward, FighterInput.Back) * 5 * seconds, a.MinimumSpeed, a.MaximumSpeed);
        a.Speed = Approach(a.Speed, a.TargetSpeed, a.Acceleration * seconds);
        if (!a.Flying)
            return;
        var old = a.Position;
        if (a.Phase == FighterPhase.Pass)
            a.Progress = Math.Min(1, a.Progress + a.Speed * seconds / Math.Max(Vector2.Distance(a.Entry, a.Exit), 1));
        else
        {
            // Advance by distance along the curve. Dividing by a point derivative can
            // jump at tight turns where that derivative approaches zero.
            a.LegTravel += a.Speed * seconds;
            var distances = a.LegDistances;
            // Array.BinarySearch<T> is not permitted by the native client content sandbox.
            var index = 0;
            var last = distances.Length - 1;
            while (index <= last)
            {
                var middle = index + (last - index) / 2;
                if (distances[middle] < a.LegTravel)
                    index = middle + 1;
                else if (distances[middle] > a.LegTravel)
                    last = middle - 1;
                else
                {
                    index = middle;
                    break;
                }
            }
            if (index >= distances.Length) a.Progress = 1;
            else if (index == 0) a.Progress = 0;
            else
            {
                var fraction = (a.LegTravel - distances[index - 1]) / Math.Max(.0001f, distances[index] - distances[index - 1]);
                a.Progress = (index - 1 + fraction) / (distances.Length - 1);
            }
        }
        a.Position = Curve(a, a.Progress);
        if (a.Phase == FighterPhase.Pass)
        {
            a.Bank = Math.Clamp(a.Bank + Axis(input, FighterInput.Right, FighterInput.Left) * seconds * 5, -CorridorHalfWidth, CorridorHalfWidth);
            var direction = Vector2.Normalize(a.Exit - a.Entry);
            var taper = Math.Min(1, Math.Min(a.Progress, 1 - a.Progress) * 10);
            a.Position += new Vector2(direction.Y, -direction.X) * a.Bank * taper;
        }
        var movement = a.Position - old;
        if (movement.LengthSquared() > .000001f)
            a.Heading = MathF.Atan2(movement.X, movement.Y);
        if (a.Progress < 1)
            return;
        switch (a.Phase)
        {
            case FighterPhase.Approach:
                BeginLeg(a, FighterPhase.Pass, a.Exit);
                break;
            case FighterPhase.Pass:
                BeginLeg(a, FighterPhase.Return, HoldingPoint(a));
                break;
            case FighterPhase.Return:
                a.Phase = FighterPhase.Holding;
                a.Flying = false;
                a.Progress = 0;
                break;
        }
    }

    public static float SensorRange(FighterAircraftComponent a)
    {
        var altitude = Math.Clamp((a.Height - MinimumHeight) / (MaximumHeight - MinimumHeight), 0, 1);
        return a.MinimumSensorRange + (a.MaximumSensorRange - a.MinimumSensorRange) * altitude;
    }

    public static Vector2 SensorPosition(FighterAircraftComponent a, FighterSeatComponent? observer) =>
        observer?.SensorLock ?? observer?.SensorFocus ??
        a.Position + ClampSensorOffset(a, Forward(a.Heading) * 14 + (observer?.Aim ?? Vector2.Zero));

    public static bool SensorInRange(FighterAircraftComponent a, Vector2 point)
    {
        var range = SensorRange(a);
        return Finite(point) && Vector2.DistanceSquared(a.Position, point) <= range * range + .01f;
    }

    public static float DesignationRange(FighterAircraftComponent a) => a.AirspaceRadius *
        (a.MinimumDesignationRangeFactor + (a.MaximumDesignationRangeFactor - a.MinimumDesignationRangeFactor) *
            Math.Clamp((a.Height - MinimumHeight) / (MaximumHeight - MinimumHeight), 0, 1));

    public static bool DesignationInRange(FighterAircraftComponent a, Vector2 point) =>
        Finite(point) && a.Battlefield.Contains(point) && Vector2.DistanceSquared(a.Position, point) <= MathF.Pow(DesignationRange(a), 2);

    public static bool SensorInRange(FighterAircraftComponent a, Vector2 point, FighterSeatComponent? sensor) =>
        sensor is { Target: not null, TargetPosition: { } flare }
            ? DesignationInRange(a, point) && Vector2.DistanceSquared(point, flare) <= MathF.Pow(SensorRange(a), 2)
            : SensorInRange(a, point);

    // Reconnaissance continues on approach and return while the ground point is in reach.
    // The exterior view and weapon release retain their separate pass restriction.
    public static bool SensorAvailable(FighterAircraftComponent a, Vector2 point, FighterSeatComponent? sensor = null) =>
        InAirspace(a) && (a.Flying && a.Phase != FighterPhase.Holding || sensor?.Target != null) && a.Battlefield.Contains(point) && SensorInRange(a, point, sensor);

    public static bool GroundAvailable(FighterAircraftComponent a, Vector2 point) => a.Phase == FighterPhase.Pass && a.Battlefield.Contains(point);

    // The exterior follows the airframe through approach, pass and departure.
    // Include the wide viewport's edges before revealing terrain through the clouds.
    // This is presentation only; weapon release still uses GroundAvailable.
    public static bool ExteriorAvailable(FighterAircraftComponent a, Vector2 point) =>
        InAirspace(a) && a.Battlefield.Enlarged(80).Contains(point);

    public static bool ToggleSensorLock(FighterAircraftComponent a, FighterSeatComponent observer)
    {
        if (observer.SensorLock is { } locked)
        {
            observer.SensorFocus = locked;
            observer.SensorLock = null;
            observer.Target = null;
            observer.TargetPosition = null;
            return true;
        }

        var point = SensorPosition(a, observer);
        if (!SensorAvailable(a, point, observer))
            return false;
        observer.SensorLock = point;
        return true;
    }

    public static bool CanRelease(FighterAircraftComponent a, TimeSpan now)
    {
        if (a.TrainingImpact != null || a.MarkPass != a.PassNumber || a.Mark is not { } mark ||
            !GroundAvailable(a, mark) || !SensorInRange(a, mark) || FighterOptics.CloudsBlock(a, mark, now))
            return false;
        var forward = Vector2.Normalize(a.Exit - a.Entry);
        var delta = mark - a.Position;
        var distance = Vector2.Dot(delta, forward);
        var lateral = MathF.Abs(Vector2.Dot(delta, new Vector2(forward.Y, -forward.X)));
        return distance is >= 4 and <= 35 && lateral <= CorridorHalfWidth;
    }

    public static void StepSensor(FighterAircraftComponent a, FighterSeatComponent observer, FighterInput input, float seconds)
    {
        if (!float.IsFinite(seconds) || seconds <= 0)
            return;
        seconds = Math.Min(seconds, StepSeconds);
        var delta = new Vector2(Axis(input, FighterInput.Right, FighterInput.Left), Axis(input, FighterInput.Forward, FighterInput.Back));
        if (delta.LengthSquared() > 1)
            delta = Vector2.Normalize(delta);
        delta *= Math.Max(12, SensorRange(a) * .25f) * seconds;
        if (observer.SensorLock is { } locked)
        {
            // Never pull a ground lock along with the aircraft at the range limit.
            // Keep the point so it can be reacquired by climbing or approaching again.
            if (delta != Vector2.Zero && SensorInRange(a, locked + delta, observer))
                observer.SensorLock = locked + delta;
            return;
        }

        if (delta == Vector2.Zero) return;
        var current = SensorPosition(a, observer);
        var next = current + delta;
        // Keep an out-of-range point fixed, but permit manually panning back into reach.
        if (SensorInRange(a, next, observer) ||
            Vector2.DistanceSquared(a.Position, next) < Vector2.DistanceSquared(a.Position, current))
            observer.SensorFocus = next;
    }

    private static Vector2 ClampSensorOffset(FighterAircraftComponent a, Vector2 offset)
    {
        var range = SensorRange(a);
        return offset.LengthSquared() > range * range ? Vector2.Normalize(offset) * range : offset;
    }

    public static Vector2 CloudCenter(int x, int y) => new(x * CloudSpacing + 12 * MathF.Sin(y * 1.7f), y * CloudSpacing + 9 * MathF.Cos(x * 2.1f));
    public static bool CloudBlocks(Vector2 position, FighterAltitude altitude)
    {
        if (altitude == FighterAltitude.Low)
            return false;
        if (altitude == FighterAltitude.Cloud)
            return true;
        var cellX = (int) MathF.Round(position.X / CloudSpacing);
        var cellY = (int) MathF.Round(position.Y / CloudSpacing);
        for (var x = cellX - 1; x <= cellX + 1; x++)
        for (var y = cellY - 1; y <= cellY + 1; y++)
        {
            var delta = (position - CloudCenter(x, y)) / new Vector2(22, 15);
            if (delta.LengthSquared() <= 1)
                return true;
        }
        return false;
    }

    public static int Axis(FighterInput input, FighterInput positive, FighterInput negative) =>
        ((input & positive) != 0 ? 1 : 0) - ((input & negative) != 0 ? 1 : 0);
    private static float Approach(float value, float target, float amount) => value + Math.Clamp(target - value, -amount, amount);
}
