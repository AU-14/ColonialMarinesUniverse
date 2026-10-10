using Content.Shared.Damage;

namespace Content.Shared.CMU14.Xenos.Despoiler;

[DataDefinition]
public sealed partial class CMULingeringAcidData
{
    [DataField(required: true)]
    public DamageSpecifier Damage = new();

    [DataField(required: true)]
    public TimeSpan Duration;

    [DataField]
    public int ArmorPiercing = 40;
}
