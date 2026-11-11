/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.CMU14.Chemistry.Effects.Positive;

public sealed partial class Photosensitive : RMCChemicalEffect
{
    private static readonly ProtoId<DamageTypePrototype> HeatType = "Heat";
    private static readonly EntProtoId Flashed = "StatusEffectFlashed";

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => $"Reacts with light inside the body for [color=red]{PotencyPerSecond * 0.5f}[/color] burn damage. Not safe to administer.\n" +
           $"Overdoses flash the eyes.\n" +
           $"Critical overdoses have a [color=red]10%[/color] chance to scar the eyes.";

    protected override void Tick(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[HeatType] = potency * 0.5f;
        damageable.TryChangeDamage(args.TargetEntity, damage, true, interruptsDoAfters: false);
    }

    protected override void TickOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
        => system.StatusEffects.TrySetStatusEffectDuration(args.TargetEntity, Flashed, TimeSpan.FromSeconds(2));

    protected override void TickCriticalOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        if (system.Random.Prob(0.1f))
            system.Blindable.AdjustEyeDamage(args.TargetEntity, 1);
    }
}
