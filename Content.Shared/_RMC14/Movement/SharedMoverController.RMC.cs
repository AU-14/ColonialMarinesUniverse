using Content.Shared.Movement.Components;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Movement.Systems;

public abstract partial class SharedMoverController
{
    private static void RMCApplyGroundedWalkSpeedFloor(
        MovementSpeedModifierComponent? movement,
        ref float walkSpeed,
        float sprintSpeed)
    {
        var baseWalkSpeed = movement?.BaseWalkSpeed ?? MovementSpeedModifierComponent.DefaultBaseWalkSpeed;

        // RMC walking stays at base walking speed while sprinting is still faster.
        // Once sprinting is slowed below that floor, walking is capped to the same speed.
        walkSpeed = baseWalkSpeed < sprintSpeed ? baseWalkSpeed : sprintSpeed;
    }
}
