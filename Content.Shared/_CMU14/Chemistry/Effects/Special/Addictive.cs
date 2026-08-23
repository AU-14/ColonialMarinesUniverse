/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
using Content.Shared._CMU14.Chemistry.Effects;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.Damage;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._CMU14.Chemistry.Effects.Special;

public sealed partial class Addictive : RMCChemicalEffect
{
    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => $"Has a [color=red]{MathF.Min(50f, ActualPotency * 5f)}%[/color] chance per metabolism tick to cause addiction. " +
           "Further doses satisfy the resulting craving.";

    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        if (args.Reagent is not { } reagent)
            return;

        var addictions = args.EntityManager.System<ChemicalAddictionSystem>();
        var id = reagent.ID;
        if (addictions.IsAddicted(args.TargetEntity, id))
        {
            addictions.AddOrSatisfy(args.TargetEntity, id);
            return;
        }

        var chance = MathF.Min(0.5f, ActualPotency * 0.05f);
        if (args.EntityManager.TryGetComponent<ChemicalAddictionTreatmentComponent>(args.TargetEntity, out var treatment))
            chance *= MathF.Max(0f, 1f - treatment.Strength * 0.25f);

        var random = IoCManager.Resolve<IRobustRandom>();
        if (random.Prob(chance))
            addictions.AddOrSatisfy(args.TargetEntity, id);
    }
}
