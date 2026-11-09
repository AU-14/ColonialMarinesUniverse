using System.Numerics;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Maths;

namespace Content.Shared.Vehicle;

public sealed partial class GridVehicleMoverSystem
{
    /// <summary>
    /// Networked fields changed by a movement step. Configuration changes and collision
    /// callbacks keep their own Dirty calls; unchanged simulation steps need no new state.
    /// </summary>
    private readonly record struct MovementNetworkState(
        Vector2i CurrentTile,
        Vector2i TargetTile,
        Vector2 Position,
        Vector2 TargetPosition,
        Vector2i CurrentDirection,
        Vector2i PushDirection,
        float CurrentSpeed,
        float AngularVelocityDegrees,
        TimeSpan NextPushTime,
        TimeSpan NextTurnTime,
        TimeSpan InPlaceTurnBlockUntil,
        bool IsCommittedToMove,
        bool IsPushMove,
        bool IsMoving,
        bool IsPoweredDemolishing,
        float SmashSlowdownMultiplier,
        TimeSpan SmashSlowdownUntil,
        TimeSpan NextMobCollisionSelfDamageAt,
        TimeSpan ImmobileUntil)
    {
        public MovementNetworkState(GridVehicleMoverComponent mover) : this(
            mover.CurrentTile,
            mover.TargetTile,
            mover.Position,
            mover.TargetPosition,
            mover.CurrentDirection,
            mover.PushDirection,
            mover.CurrentSpeed,
            mover.AngularVelocityDegrees,
            mover.NextPushTime,
            mover.NextTurnTime,
            mover.InPlaceTurnBlockUntil,
            mover.IsCommittedToMove,
            mover.IsPushMove,
            mover.IsMoving,
            mover.IsPoweredDemolishing,
            mover.SmashSlowdownMultiplier,
            mover.SmashSlowdownUntil,
            mover.NextMobCollisionSelfDamageAt,
            mover.ImmobileUntil)
        {
        }
    }
}
