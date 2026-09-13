namespace Content.Shared._RMC14.Xenonids.Despoiler;

[RegisterComponent]
public sealed partial class XenoDespoilerLingeringAcidComponent : Component
{
    [DataField]
    public TimeSpan MinLifetime = TimeSpan.FromSeconds(15);

    [DataField]
    public TimeSpan MaxLifetime = TimeSpan.FromSeconds(20);

    [DataField]
    public float CrossBurnDamage = 20f;

    // Damage attribution is only used on the server; clients do not need the caster entity.
    [DataField]
    public EntityUid? Caster;
}
