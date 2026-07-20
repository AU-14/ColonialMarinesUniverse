using Content.Shared.Movement.Components;

namespace Content.Shared.Movement.Systems;

public sealed partial class MovementSpeedModifierSystem
{
    /// <summary>
    /// Compatibility overload for RMC systems that hold a different component on the mover.
    /// </summary>
    public void RefreshMovementSpeedModifiers(EntityUid entity)
    {
        RefreshMovementSpeedModifiers((Entity<MovementSpeedModifierComponent?>) entity);
    }
}
