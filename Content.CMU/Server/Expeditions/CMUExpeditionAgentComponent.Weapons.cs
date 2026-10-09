namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentComponent
{
    public EntityUid? PendingWeapon;
    public TimeSpan WeaponSwitchAt;
    public TimeSpan NextWeaponChoice;
    public int WeaponSwitches;
    public int WeaponBurstLimit = int.MaxValue;
    public string WeaponDecision = "primary";
    public TimeSpan NextRocket;
    public int RocketsFired;
    public string GrenadeDecision = "idle";
    public int SmokesThrown;
}

/// <summary>Selection preferences for AI-owned equipment; native guns still execute every shot.</summary>
[RegisterComponent]
public sealed partial class CMUExpeditionWeaponRoleComponent : Component
{
    [DataField] public float Priority = 20;
    [DataField] public float MinimumRange;
    [DataField] public float MaximumRange = 14;
    [DataField] public float CloseRange = 4;
    [DataField] public float ClosePriority;
    [DataField] public int BurstLimit = int.MaxValue;
    [DataField] public bool Rocket;
    [DataField] public float BlastRadius = 4;
}
