using System.Numerics;
using Content.Shared.CMU14.Fighter;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.CMU14.Fighter;

public sealed partial class FighterSystem
{
    private Vector2 GroundRollOffset(EntityCoordinates site, Angle heading, float distance) =>
        (heading - _transform.GetWorldRotation(site.EntityId)).RotateVec(new Vector2(0, -distance));

    private float FindGroundRoll(EntityUid hull, EntityCoordinates site, float maximum, bool landing)
    {
        var heading = landing && TryComp(hull, out FighterGroundComponent? ground)
            ? ground.LaunchRotation : _transform.GetWorldRotation(hull);
        var clear = 0f;
        // Adapt to the available space instead of demanding a marked runway,
        // a particular heading, or a minimum pilot-controlled taxi speed.
        for (var distance = .25f; distance <= maximum; distance += .25f)
        {
            var candidate = site.Offset(GroundRollOffset(site, heading, landing ? -distance : distance));
            if (!GroundSiteClear(hull, candidate)) break;
            clear = distance;
        }
        return clear;
    }

    private bool MoveGroundRoll(Entity<FighterGroundComponent> ground, float progress)
    {
        if (ground.Comp.TransitionStart is not { } start || !start.IsValid(EntityManager)) return false;
        var landing = ground.Comp.State == FighterGroundState.Landing;
        var distance = ground.Comp.TransitionDistance * CMUFighterTransition.Roll(progress, landing, Airframe(ground));
        var target = start.Offset(GroundRollOffset(start, ground.Comp.LaunchRotation, distance));
        var from = _transform.GetWorldPosition(ground);
        var to = _transform.ToMapCoordinates(target);
        var steps = Math.Max(1, (int) MathF.Ceiling(Vector2.Distance(from, to.Position) / .2f));
        for (var i = 1; i <= steps; i++)
        {
            var point = new MapCoordinates(Vector2.Lerp(from, to.Position, i / (float) steps), to.MapId);
            if (!GroundSiteClear(ground, _transform.ToCoordinates(start.EntityId, point))) return false;
        }
        _transform.SetCoordinates(ground, target);
        // The taxi controller still maintains its stationary grid position
        // without an operator. Keep that state in sync with the authoritative
        // roll so its next update cannot snap the hull back to the launch site.
        if (TryComp(ground, out GridVehicleMoverComponent? mover) && Transform(ground).GridUid is { } grid &&
            TryComp(grid, out MapGridComponent? gridComp))
        {
            var coordinates = _transform.WithEntityId(target, grid);
            mover.Position = mover.TargetPosition = coordinates.Position;
            mover.CurrentTile = mover.TargetTile = _map.TileIndicesFor(grid, gridComp, coordinates);
            Dirty(ground, mover);
        }
        return true;
    }
}
