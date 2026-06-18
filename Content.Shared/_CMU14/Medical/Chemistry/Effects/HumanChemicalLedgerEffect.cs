using Content.Shared._CMU14.Medical.Chemistry.Data;
using Content.Shared._CMU14.Medical.Chemistry.Systems;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Medical.Chemistry.Effects;

[UsedImplicitly]
public sealed partial class HumanChemicalLedgerEffect : EntityEffect
{
    public override void Effect(EntityEffectBaseArgs args)
    {
        if (args is not EntityEffectReagentArgs { Reagent: { } reagent } reagentArgs)
            return;

        var totalQuantity = reagentArgs.Source?.GetTotalPrototypeQuantity(reagent.ID) ?? reagentArgs.Quantity;
        var tick = new HumanChemicalTick(reagent.ID, reagentArgs.Scale, totalQuantity);
        var ledger = args.EntityManager.System<SharedHumanChemicalLedgerSystem>();
        ledger.TryApplyChemicalTick(reagentArgs.TargetEntity, tick);
    }

    protected override string? ReagentEffectGuidebookText(
        IPrototypeManager prototype,
        IEntitySystemManager entSys)
    {
        return Loc.GetString("cmu-medical-human-chemical-ledger-guidebook");
    }
}

[UsedImplicitly]
public sealed partial class CMUHumanMedicalLedgerCondition : EntityEffectCondition
{
    public override bool Condition(EntityEffectBaseArgs args)
    {
        var ledger = args.EntityManager.System<SharedHumanChemicalLedgerSystem>();
        HumanMedicalComponent? medical = null;
        return ledger.CanMutateHumanLedger(args.TargetEntity, ref medical);
    }

    public override string GuidebookExplanation(IPrototypeManager prototype)
    {
        return Loc.GetString("cmu-medical-human-ledger-condition");
    }
}

[UsedImplicitly]
public sealed partial class CMUNonHumanMedicalLedgerCondition : EntityEffectCondition
{
    public override bool Condition(EntityEffectBaseArgs args)
    {
        var ledger = args.EntityManager.System<SharedHumanChemicalLedgerSystem>();
        HumanMedicalComponent? medical = null;
        return !ledger.CanMutateHumanLedger(args.TargetEntity, ref medical);
    }

    public override string GuidebookExplanation(IPrototypeManager prototype)
    {
        return Loc.GetString("cmu-medical-non-human-ledger-condition");
    }
}
