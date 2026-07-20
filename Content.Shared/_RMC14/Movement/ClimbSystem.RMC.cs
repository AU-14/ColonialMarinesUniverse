using Content.Shared._RMC14.Movement;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Climbing.Systems;

public sealed partial class ClimbSystem
{
    [Dependency] private RMCMovementSystem _rmcMovement = default!;

    private bool RMCPreflightClimb(EntityUid user, EntityUid entityToMove, EntityUid climbable)
    {
        // The current climb path raises AttemptClimbEvent on the target itself below.
        // Check intervening RMC obstacles here without raising the target event twice.
        return _rmcMovement.CanClimbOver(user, entityToMove, climbable, includeTarget: false);
    }
}
