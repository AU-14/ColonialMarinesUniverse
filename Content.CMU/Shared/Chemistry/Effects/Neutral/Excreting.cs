/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Kidneys;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Effects.Neutral;

public sealed partial class Excreting : RMCChemicalEffect
{
    private static readonly ProtoId<DamageTypePrototype> PoisonType = "Poison";

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => $"Removes [color=yellow]{PotencyPerSecond * 2}[/color] units of every other chemical from the bloodstream.\n" +
           $"Overdoses strain the kidneys for [color=red]{PotencyPerSecond}[/color] damage.\n" +
           $"Critical overdoses deal [color=red]{PotencyPerSecond * 3}[/color] kidney damage.";

    protected override void Tick(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        var target = args.TargetEntity;
        if (!system.RMCBloodstream.TryGetChemicalSolution(target, out _, out var solution))
            return;
        foreach (var quantity in solution.Contents)
        {
            if (quantity.Reagent.Prototype == args.Reagent.ID)
                continue;
            system.RMCBloodstream.RemoveBloodstreamChemical(target, quantity.Reagent.Prototype, potency * 2f);
        }
    }

    protected override void TickOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
        => system.ChemicalMedical.DamageOrgan<KidneysComponent>(args.TargetEntity, potency, PoisonType);

    protected override void TickCriticalOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
        => system.ChemicalMedical.DamageOrgan<KidneysComponent>(args.TargetEntity, potency * 3f, PoisonType);
}
