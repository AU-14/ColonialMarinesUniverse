using Content.Server.Movement.Components;
using Robust.Shared.Map;

namespace Content.Server.Movement.Systems;

public sealed partial class LagCompensationSystem
{
    private void OnLagStartup(Entity<LagCompensationComponent> ent, ref ComponentStartup args)
    {
        var xform = Transform(ent);
        if (ent.Comp.Positions.Count == 0 && xform.Coordinates.EntityId.IsValid())
            ent.Comp.Positions.Enqueue((_timing.CurTime, xform.Coordinates, xform.LocalRotation));
    }

    private static void PruneHistory(LagCompensationComponent history, TimeSpan earliestTime)
    {
        while (history.Positions.Count > 1)
        {
            // Keep one anchor at/before the window, including the last sample at an equal timestamp.
            using var samples = history.Positions.GetEnumerator();
            samples.MoveNext();
            samples.MoveNext();
            if (samples.Current.Item1 > earliestTime)
                break;

            history.Positions.Dequeue();
        }
    }

    /// <summary>
    /// Gets the final known pose at or before a snapshot time. Sparse movement samples are not interpolated.
    /// Missing, future, or expired history falls back to the current pose rather than guessing another snapshot.
    /// </summary>
    public (EntityCoordinates Coordinates, Angle Angle) GetCoordinatesAngleAtTime(
        EntityUid uid,
        TimeSpan viewTime,
        TransformComponent? xform = null)
    {
        if (!Resolve(uid, ref xform))
            return (EntityCoordinates.Invalid, Angle.Zero);

        return TryGetHistoricalCoordinatesAngleAtTime(uid, viewTime, out var coordinates, out var angle)
            ? (coordinates, angle)
            : (xform.Coordinates, xform.LocalRotation);
    }

    /// <summary>
    /// Resolves an actual historical sample without substituting the current pose when history is unavailable.
    /// Use this when an additional hit allowance requires historical evidence.
    /// </summary>
    public bool TryGetHistoricalCoordinatesAngleAtTime(
        EntityUid uid,
        TimeSpan viewTime,
        out EntityCoordinates coordinates,
        out Angle angle)
    {
        coordinates = EntityCoordinates.Invalid;
        angle = Angle.Zero;
        if (viewTime > _timing.CurTime || _timing.CurTime - viewTime > BufferTime ||
            !TryComp<LagCompensationComponent>(uid, out var history))
        {
            return false;
        }

        foreach (var (time, position, rotation) in history.Positions)
        {
            if (time > viewTime)
                break;

            coordinates = position;
            angle = rotation;
        }

        return coordinates.EntityId.IsValid() && Exists(coordinates.EntityId);
    }
}
