using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Care;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Medical.Chemistry.Effects;

[UsedImplicitly]
public sealed partial class HealOrganEffect : EntityEffect
{
    /// <summary>
    ///     Historical YAML organ name, e.g. <c>"Liver"</c> or <c>"Lungs"</c>.
    ///     The effect maps this to fixed ledger organ slots.
    /// </summary>
    [DataField(required: true)]
    public string OrganComponent = string.Empty;

    /// <summary>
    ///     Ledger damage repaired per metabolize cycle.
    /// </summary>
    [DataField]
    public FixedPoint2 Amount = 1;

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (args is not EntityEffectReagentArgs reagent)
            return;

        var entMan = args.EntityManager;
        if (!entMan.TryGetComponent<HumanMedicalComponent>(reagent.TargetEntity, out var medical))
            return;

        var treatment = entMan.System<HumanTreatmentSystem>();
        foreach (var slot in GetOrganSlots(OrganComponent))
        {
            treatment.TryApplyTreatment(
                reagent.TargetEntity,
                new TreatmentAttempt(
                    TreatmentKind.RepairOrgan,
                    BodyRegion.None,
                    slot,
                    Amount: Amount),
                medical);
        }
    }

    private static IEnumerable<OrganSlot> GetOrganSlots(string organ)
    {
        switch (organ)
        {
            case "Brain":
            case "CMUBrain":
                yield return OrganSlot.Brain;
                break;
            case "Heart":
                yield return OrganSlot.Heart;
                break;
            case "Lungs":
                yield return OrganSlot.LeftLung;
                yield return OrganSlot.RightLung;
                break;
            case "Liver":
                yield return OrganSlot.Liver;
                break;
            case "Kidneys":
                yield return OrganSlot.Kidneys;
                break;
            case "Stomach":
            case "CMUStomach":
                yield return OrganSlot.Stomach;
                break;
            case "Eyes":
                yield return OrganSlot.Eyes;
                break;
            case "Ears":
                yield return OrganSlot.Ears;
                break;
        }
    }

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("cmu-medical-heal-organ-guidebook", ("organ", OrganComponent), ("amount", Amount));
}
