using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Damage;
using Content.Shared.Humanoid;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Yautja;

public sealed partial class YautjaHumanMeleeSystem : EntitySystem
{
    [Dependency] private SkillsSystem _skills = default!;

    private static readonly EntProtoId<SkillDefinitionComponent> MeleeSkill = "RMCSkillMeleeWeapons";

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaHumanMeleeComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<YautjaHumanMeleeComponent, CMGetArmorPiercingEvent>(OnGetArmorPiercing);
    }

    private void OnMeleeHit(Entity<YautjaHumanMeleeComponent> ent, ref MeleeHitEvent args)
    {
        // pred weapons don't wide swing, so the one target decides it
        if (!args.IsHit || args.HitEntities.Count != 1 || !IsHuman(args.HitEntities[0]))
            return;

        // SkillsSystem already multiplied the swing, take it back off
        var skill = _skills.GetSkill(args.User, MeleeSkill);
        if (skill <= 0)
            return;

        var coefficient = 1f / (1f + 0.25f * skill);
        var undo = new DamageModifierSet();
        foreach (var type in args.BaseDamage.DamageDict.Keys)
        {
            undo.Coefficients[type] = coefficient;
        }

        args.ModifiersList.Add(undo);
    }

    private void OnGetArmorPiercing(Entity<YautjaHumanMeleeComponent> ent, ref CMGetArmorPiercingEvent args)
    {
        // thrown hits come through here too, cm gave those the same sharp/edge penetration
        if (IsHuman(args.Target))
            args.Piercing += ent.Comp.Penetration;
    }

    private bool IsHuman(EntityUid target)
    {
        return HasComp<HumanoidProfileComponent>(target) && !HasComp<XenoComponent>(target);
    }
}
