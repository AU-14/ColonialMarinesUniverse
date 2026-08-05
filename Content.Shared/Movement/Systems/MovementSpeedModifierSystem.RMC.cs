using Content.Shared._RMC14.Standing;
using Content.Shared.Movement.Components;

namespace Content.Shared.Movement.Systems;

public sealed partial class MovementSpeedModifierSystem
{
    private void ApplyRMCRestingSpeed(
        Entity<MovementSpeedModifierComponent?> entity,
        RefreshMovementSpeedModifiersEvent args)
    {
        if (!TryComp(entity, out RMCRestComponent? rest) || !rest.Resting)
            return;

        var walk = rest.RestingSpeed;
        if (args.WalkSpeedModifier != 0f)
            walk /= args.WalkSpeedModifier;

        var sprint = rest.RestingSpeed;
        if (args.SprintSpeedModifier != 0f)
            sprint /= args.SprintSpeedModifier;

        // Preserve hard movement blockers while forcing all non-zero movement to the RMC resting modifier.
        args.ModifySpeed(walk, sprint);
    }

    /// <summary>
    /// Compatibility overload for RMC systems that hold a different component on the mover.
    /// </summary>
    public void RefreshMovementSpeedModifiers(EntityUid entity)
    {
        RefreshMovementSpeedModifiers((Entity<MovementSpeedModifierComponent?>) entity);
    }
}
