namespace Content.Client.CMU14.DroneOperator;

[RegisterComponent]
public sealed partial class CMUCombatDroneMuzzleFlashComponent : Component
{
    public EntityUid Drone;
    public Angle RotationOffset;
}
