using Content.Shared.Damage;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared._RMC14.Chemistry.Effects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.ChemicalIrritants;

public sealed partial class ReduceChemicalIrritant : RMCChemicalEffect
{
    [DataField]
    public float Amount = 1f;

    protected override string ReagentEffectGuidebookText(
        IPrototypeManager prototype,
        IEntitySystemManager entSys)
    {
        return "Treats toxin damage and neutralizes nerve gases.";
    }

    protected override void Tick(
        DamageableSystem damageable,
        FixedPoint2 potency,
        EntityEffectReagentArgs args)
    {
        var irritantSystem = args.EntityManager.System<SharedChemicalIrritantSystem>();
        float reduction = Amount * potency.Float();

        irritantSystem.ReduceIrritant(args.TargetEntity, reduction);
    }
}
