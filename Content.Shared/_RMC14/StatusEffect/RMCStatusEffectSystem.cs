using Content.Shared.CMU14.Yautja;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.StatusEffect;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.StatusEffect;

public sealed partial class RMCStatusEffectSystem : EntitySystem
{
    [Dependency] private SkillsSystem _skills = default!;

    private static readonly EntProtoId<SkillDefinitionComponent> EnduranceSkill = "RMCSkillEndurance";
    private static readonly ProtoId<StatusEffectPrototype> Knockdown = "KnockedDown";
    private static readonly ProtoId<StatusEffectPrototype> Stun = "Stun";
    private static readonly ProtoId<StatusEffectPrototype> Unconscious = "Unconscious";
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SkillsComponent, RMCStatusEffectTimeEvent>(OnSkillsStatusEffectTime);
        SubscribeLocalEvent<XenoComponent, RMCStatusEffectTimeEvent>(OnXenoStatusEffectTime);
        SubscribeLocalEvent<RMCStunResistanceComponent, RMCStatusEffectTimeEvent>(OnStunResistanceStatusEffectTime);
    }

    private void OnSkillsStatusEffectTime(Entity<SkillsComponent> ent, ref RMCStatusEffectTimeEvent args)
    {
        // CMU14: CM-SS13 applies the skill term to everyone, Yautja included -
        // life_helpers.dm:177-183 does "final_reduction = (1 - skill_resistance) / species.stun_reduction"
        // (and the same for knock_down_reduction), so a high-Endurance hunter shrugs stuns and knockdowns off
        // faster than the bare 1.5 divisor. The old Yautja early-return here made them take longer stuns.
        if (args.Key != Knockdown && args.Key != Stun && args.Key != Unconscious)
            return;

        var endurance = _skills.GetSkill((ent, ent), EnduranceSkill);
        if (endurance < 1)
            return;

        var skill = (endurance - 1) * 0.08;
        var multiplier = 1 - skill; // TODO RMC14 hunter/synthetic/monkey/zombie/human hero multiplier
        args.Duration *= multiplier;
    }

    private void OnXenoStatusEffectTime(Entity<XenoComponent> ent, ref RMCStatusEffectTimeEvent args)
    {
        if (args.Key != Knockdown && args.Key != Stun && args.Key != Unconscious)
            return;

        args.Duration *= 0.667;
    }

    private void OnStunResistanceStatusEffectTime(Entity<RMCStunResistanceComponent> ent, ref RMCStatusEffectTimeEvent args)
    {
        if (args.Key != Knockdown && args.Key != Stun && args.Key != Unconscious)
            return;

        args.Duration /= ent.Comp.Resistance;
    }

    public void GiveStunResistance(EntityUid target, float resistance)
    {
        var resistanceComp = EnsureComp<RMCStunResistanceComponent>(target);
        resistanceComp.Resistance = resistance;
        Dirty(target, resistanceComp);
    }
}
