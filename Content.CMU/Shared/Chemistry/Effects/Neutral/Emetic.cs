/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Stomach;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.CMU14.Chemistry.Effects.Neutral;

public sealed partial class Emetic : RMCChemicalEffect
{
    private static readonly ProtoId<DamageTypePrototype> BluntType = "Blunt";

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => $"Has a [color=yellow]{MathF.Min(50f, ActualPotency * 5f)}%[/color] chance per tick to cause vomiting.\n" +
           $"Overdoses remove [color=red]{PotencyPerSecond * 5}[/color] nutrition.\n" +
           $"Critical overdoses deal [color=red]{PotencyPerSecond * 2}[/color] stomach damage.";

    protected override void Tick(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        if (system.Random.Prob(MathF.Min(0.5f, args.ActualPotency * 0.05f)))
            system.RaiseVomit(args.TargetEntity);
    }

    protected override void TickOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        if (system.TryGetSatiation(args.TargetEntity, out var satiation))
            system.Satiation.ModifyValue((args.TargetEntity, satiation), SatiationSystem.Hunger, (float)potency * -5f);
    }

    protected override void TickCriticalOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
        => system.ChemicalMedical.DamageOrgan<CMUStomachComponent>(args.TargetEntity, potency * 2f, BluntType);
}
