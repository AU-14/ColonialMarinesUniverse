using System.Diagnostics.CodeAnalysis;
using Content.Shared._RMC14.Marines.Skills;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Preferences.Loadouts.Effects;

/// <summary>
/// Adds skill levels to the entity's existing job skills when its loadout is equipped.
/// </summary>
public sealed partial class SkillLoadoutEffect : LoadoutEffect
{
    [DataField(required: true)]
    public EntProtoId<SkillDefinitionComponent> Skill;

    [DataField]
    public int Amount = 1;

    public override bool Validate(
        HumanoidCharacterProfile profile,
        RoleLoadout loadout,
        ICommonSession? session,
        IDependencyCollection collection,
        [NotNullWhen(false)] out FormattedMessage? reason)
    {
        var prototypeManager = collection.Resolve<IPrototypeManager>();
        if (Amount > 0 &&
            prototypeManager.TryIndex(Skill, out var skillPrototype) &&
            skillPrototype.TryComp<SkillDefinitionComponent>(out _, collection.Resolve<IComponentFactory>()))
        {
            reason = null;
            return true;
        }

        reason = FormattedMessage.FromUnformatted(Loc.GetString("loadouts-skill-upgrade-invalid"));
        return false;
    }

    public override void ApplyToEntity(EntityUid entity, IEntityManager entityManager, IPrototypeManager prototypeManager)
    {
        entityManager.System<SkillsSystem>().IncrementSkill(entity, Skill, Amount);
    }
}
