using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Prototypes;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared._RMC14.Body;
using Content.Shared._RMC14.Movement;
using Robust.Shared.Prototypes;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Stunnable;

namespace Content.Shared._RMC14.Chemistry.Effects.Positive;

public sealed partial class Hemogenic : RMCChemicalEffect
{
    private static readonly ProtoId<DamageTypePrototype> BluntType = "Blunt";
    private static readonly ProtoId<DamageTypePrototype> PoisonType = "Poison";
    private static readonly ProtoId<DamageTypePrototype> AsphyxiationType = "Asphyxiation";
    private const float MinimumHungerSatiation = 200f;

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        var baseText = $"Restores [color=green]{PotencyPerSecond}[/color]cl of blood while not hungry; Yautja ignore the nutrient requirement.\n" +
                       $"Causes [color=red]{PotencyPerSecond}[/color] nutrient loss per second.\n" +
                       $"Overdoses cause [color=red]{PotencyPerSecond}[/color] toxin damage.\n" +
                       $"Critical overdoses cause [color=red]{PotencyPerSecond * 5}[/color] additional nutrient loss";

        return ActualPotency > 3
            ? $"Deals [color=red]{PotencyPerSecond}[/color] brute, [color=red]{PotencyPerSecond * 2}[/color] airloss damage, and slows you down.\n{baseText}"
            : baseText;
    }

    protected override void Tick(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        var target = args.TargetEntity;
        var yautja = system.HasYautja(target);

        if (!yautja)
        {
            if (!system.TryGetSatiation(target, out var satiation) ||
                system.Satiation.GetValueOrNull((target, satiation), SatiationSystem.Hunger) is not { } hunger ||
                hunger < MinimumHungerSatiation)
            {
                return;
            }

            system.Satiation.ModifyValue((target, satiation), SatiationSystem.Hunger, -(float)potency);
        }

        if (system.TryGetBloodstream(target, out var bloodstream))
            system.Bloodstream.TryModifyBloodLevel((target, bloodstream), potency);

        var shouldApplyDamage = !yautja && args.ActualPotency > 3 &&
                                system.RMCBloodstream.TryGetBloodReadout(target, out var currentBlood, out _) &&
                                currentBlood > 570;
        if (!shouldApplyDamage)
            return;
        var damage = new DamageSpecifier();
        damage.DamageDict[BluntType] = potency;
        damage.DamageDict[AsphyxiationType] = potency * 2;
        damageable.TryChangeDamage(args.TargetEntity, damage, true, interruptsDoAfters: false);
        var modifiers = new List<TemporarySpeedModifierSet>(1);
        modifiers.Add(new(TimeSpan.FromSeconds(2), 0.9f, 0.9f));
        system.TemporarySpeedModifiers.ModifySpeed(target, modifiers);
    }

    protected override void TickOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[PoisonType] = potency;
        damageable.TryChangeDamage(args.TargetEntity, damage, true, interruptsDoAfters: false);
    }

    protected override void TickCriticalOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        var target = args.TargetEntity;
        if (!system.TryGetSatiation(target, out var satiation))
            return;

        system.Satiation.ModifyValue((target, satiation), SatiationSystem.Hunger, (float)potency * -5f);
    }
}
