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

public sealed partial class Crystallization : RMCChemicalEffect
{
    private static readonly ProtoId<DamageTypePrototype> BluntType = "Blunt";

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => $"Crystallizes in the bloodstream for [color=red]{PotencyPerSecond * 0.5f}[/color] brute damage. Not safe to administer.\n" +
           $"Overdoses have a [color=red]{MathF.Min(25f, ActualPotency * 5f)}%[/color] chance to lodge a crystal shard in a random limb.\n" +
           $"Critical overdoses deal [color=red]{PotencyPerSecond * 2}[/color] brute damage.";

    protected override void Tick(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[BluntType] = potency * 0.5f;
        damageable.TryChangeDamage(args.TargetEntity, damage, true, interruptsDoAfters: false);
    }

    protected override void TickOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        if (!system.Random.Prob(MathF.Min(0.25f, args.ActualPotency * 0.05f)))
            return;
        var parts = new List<EntityUid>();
        foreach (var part in system.MedicalBodyIndex.GetBodyParts(args.TargetEntity))
            parts.Add(part.Owner);
        if (parts.Count > 0)
            system.Shrapnel.AddShrapnel(system.Random.Pick(parts), 1, 0.3f);
    }

    protected override void TickCriticalOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[BluntType] = potency * 2f;
        damageable.TryChangeDamage(args.TargetEntity, damage, true, interruptsDoAfters: false);
    }
}
