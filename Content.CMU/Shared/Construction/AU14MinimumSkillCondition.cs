using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Construction;
using Content.Shared.Construction.Conditions;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Construction;

/// <summary>
/// Construction recipe condition: the builder needs at least <see cref="Level"/> in <see cref="Skill"/>.
/// </summary>
[DataDefinition]
public sealed partial class AU14MinimumSkill : IConstructionCondition
{
    [DataField(required: true)]
    public EntProtoId<SkillDefinitionComponent> Skill;

    [DataField]
    public int Level = 1;

    public ConstructionGuideEntry GenerateGuideEntry()
    {
        var prototypes = IoCManager.Resolve<IPrototypeManager>();
        var skillName = prototypes.TryIndex<EntityPrototype>(Skill.Id, out var skill) && skill != null ? skill.Name : Skill.Id;

        return new ConstructionGuideEntry
        {
            Localization = "cmu-construction-condition-minimum-skill",
            Arguments = new (string, object)[] { ("skill", skillName), ("level", Level) },
        };
    }

    public bool Condition(EntityUid user, EntityCoordinates location, Direction direction)
    {
        var entities = IoCManager.Resolve<IEntityManager>();
        return entities.System<SkillsSystem>().HasSkill(user, Skill, Level);
    }
}
