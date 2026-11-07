using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.Damage;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Weapons.Ranged.IFF;

[Prototype("cmuIFFRemoval")]
public sealed partial class CMUIFFRemovalPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<EntProtoId<IFFFactionComponent>> EligibleFactions = new();

    [DataField(required: true)]
    public ProtoId<TagPrototype> KnifeTag;

    [DataField(required: true)]
    public EntProtoId<SkillDefinitionComponent> SurgerySkill;

    [DataField(required: true)]
    public int SurgerySkillLevel;

    [DataField(required: true)]
    public DamageSpecifier SelfDamage = new();

    [DataField(required: true)]
    public DamageSpecifier SurgeryDamage = new();
}
