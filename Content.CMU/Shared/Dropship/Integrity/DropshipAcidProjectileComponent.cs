namespace Content.Shared.CMU14.Dropship.Integrity;

/// <summary>Acid deposited on a dropship hull by a direct projectile hit.</summary>
[RegisterComponent]
public sealed partial class DropshipAcidProjectileComponent : Component
{
    [DataField]
    public float DamagePerSecond = 20;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(12);
}
