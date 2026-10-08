namespace Content.Client.CMU14.ThreeD.Scene;

/// <summary>Presentation metadata on the existing client gun effects; never changes ballistics.</summary>
[RegisterComponent]
public sealed partial class CMU3DCombatVisualComponent : Component
{
    public const float WeaponHeight = 1.1f;
    public bool AlongTrajectory;
}
