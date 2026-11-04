using Robust.Shared.GameStates;

namespace Content.Shared.Movement.Components;

/// <summary>
/// Movement contribution supplied by an attached leg organ.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MovementBodyPartComponent : Component
{
    [DataField("walkSpeed")]
    public float WalkSpeed = MovementSpeedModifierComponent.DefaultBaseWalkSpeed;

    [DataField("sprintSpeed")]
    public float SprintSpeed = MovementSpeedModifierComponent.DefaultBaseSprintSpeed;

    [DataField("acceleration")]
    public float Acceleration = MovementSpeedModifierComponent.DefaultAcceleration;
}
