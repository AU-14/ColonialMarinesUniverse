using Content.Shared._RMC14.Body;
using Content.Shared._RMC14.Stun;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Drunk;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.StatusEffect;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._RMC14.Chemistry.Effects.Neutral;

public sealed partial class Ketogenic : RMCChemicalEffect
{
    private static readonly ProtoId<DamageTypePrototype> PoisonType = "Poison";
    private static readonly ProtoId<StatusEffectPrototype> Unconscious = "Unconscious";

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return $"Removes [color=red]{PotencyPerSecond * 5}[/color] nutrients, causing hunger over time.\n" +
               $"Increases alcohol metabolism rate by [color=green]{PotencyPerSecond}[/color] units.\n" +
               $"Overdoses cause [color=red]{PotencyPerSecond * 5}[/color] nutrition loss, [color=red]{PotencyPerSecond}[/color] toxin damage, and a [color=red]{ActualPotency * 2.5}%[/color] chance of vomiting.\n" +
               $"Critical overdoses will knock you unconscious for [color=red]10[/color] seconds";
    }

    protected override void Tick(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        var target = args.TargetEntity;
        if (system.TryGetSatiation(target, out var satiation))
            system.Satiation.ModifyValue((target, satiation), SatiationSystem.Hunger, args.PotencyPerSecond * -5);
        // TODO RMC14 M.overeatduration = 0

        var bloodstream = system.RMCBloodstream;
        var alcoholRemoved = bloodstream.RemoveBloodstreamAlcohols(args.TargetEntity, potency);

        if (!alcoholRemoved)
            return;
        var drunkSystem = system.Drunk;
        drunkSystem.TryApplyDrunkenness(args.TargetEntity, TimeSpan.FromSeconds(args.PotencyPerSecond * 5));
    }

    protected override void TickOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        var target = args.TargetEntity;
        if (system.TryGetSatiation(target, out var satiation))
            system.Satiation.ModifyValue((target, satiation), SatiationSystem.Hunger, args.PotencyPerSecond * -5);

        var damage = new DamageSpecifier();
        damage.DamageDict[PoisonType] = potency;
        damageable.TryChangeDamage(target, damage, true, interruptsDoAfters: false);

        var random = system.Random;
        if (!random.Prob(0.025f * args.ActualPotency))
            return;
        system.RaiseVomit(target);
    }

    protected override void TickCriticalOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        var status = system.StatusEffectQuery;
        status.TryAddStatusEffect<RMCUnconsciousComponent>(
            args.TargetEntity,
            Unconscious,
            TimeSpan.FromSeconds(40),
            true
        );
    }
}
