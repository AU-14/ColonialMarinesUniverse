using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Rules;
using Content.Shared._CMU14.Medical.Human.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Shared._CMU14.Medical.Human.Care;

public sealed partial class HumanTreatmentSystem : EntitySystem
{
    [Dependency] private SharedHumanMedicalSystem _medical = default!;

    public TreatmentResult TryApplyTreatment(
        EntityUid patient,
        TreatmentAttempt attempt,
        HumanMedicalComponent? medical = null)
    {
        if (!Resolve(patient, ref medical))
            return new TreatmentResult(false, default, "Target has no human medical ledger.");

        var result = TryApplyTreatment(medical, attempt);
        if (!result.Applied)
            return result;

        _medical.RefreshActiveMarkers(patient, medical);

        var ev = new HumanTreatmentAppliedEvent(patient, attempt, result);
        RaiseLocalEvent(ref ev);

        return result;
    }

    public static TreatmentResult TryApplyTreatment(
        HumanMedicalComponent medical,
        TreatmentAttempt attempt)
    {
        var plan = TreatmentRules.TryCreateTreatmentPlan(medical, attempt);
        return HumanMedicalLedger.ApplyTreatmentPlan(medical, plan);
    }
}

[ByRefEvent]
public readonly record struct HumanTreatmentAppliedEvent(
    EntityUid Patient,
    TreatmentAttempt Attempt,
    TreatmentResult Result);
