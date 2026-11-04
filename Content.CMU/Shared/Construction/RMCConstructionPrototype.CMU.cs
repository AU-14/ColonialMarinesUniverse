using Content.Shared._RMC14.Marines.Skills;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Construction.Prototypes;

public sealed partial class RMCConstructionPrototype
{
    [AlwaysPushInheritance, DataField]
    public EntProtoId<SkillDefinitionComponent>? AlternativeSkill;

    [AlwaysPushInheritance, DataField]
    public int AlternativeSkillLevel = 1;
}
