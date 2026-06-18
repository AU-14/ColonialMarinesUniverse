using Content.Shared._CMU14.Medical.Foundation;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Care;

namespace Content.Shared._CMU14.Medical.Human.Care;

public static class MedicalTreatmentActionRules
{
    public static bool TryCreateHumanTreatmentAttempt(
        MedicalActionRequest request,
        out TreatmentAttempt attempt)
    {
        attempt = default;

        if (!MedicalActionRules.IsFieldTreatment(request.Kind) ||
            request.Region == BodyRegion.None)
        {
            return false;
        }

        if (!TryMapTreatmentKind(request.Kind, out var treatmentKind))
            return false;

        attempt = new TreatmentAttempt(
            treatmentKind,
            request.Region,
            request.OrganSlot);

        return true;
    }

    private static bool TryMapTreatmentKind(
        MedicalActionKind action,
        out TreatmentKind treatment)
    {
        switch (action)
        {
            case MedicalActionKind.ApplyGauze:
                treatment = TreatmentKind.Gauze;
                return true;
            case MedicalActionKind.ApplySalve:
                treatment = TreatmentKind.Salve;
                return true;
            case MedicalActionKind.ApplySplint:
                treatment = TreatmentKind.Splint;
                return true;
            case MedicalActionKind.ApplyCast:
                treatment = TreatmentKind.Cast;
                return true;
            case MedicalActionKind.ApplySuture:
                treatment = TreatmentKind.Suture;
                return true;
            case MedicalActionKind.ApplyClamp:
                treatment = TreatmentKind.ClampBleed;
                return true;
            case MedicalActionKind.ApplyTourniquet:
                treatment = TreatmentKind.ApplyTourniquet;
                return true;
            case MedicalActionKind.RemoveTourniquet:
                treatment = TreatmentKind.RemoveTourniquet;
                return true;
            case MedicalActionKind.ApplySyntheticGraft:
                treatment = TreatmentKind.SyntheticGraft;
                return true;
            case MedicalActionKind.ApplySurgicalLine:
                treatment = TreatmentKind.SurgicalLine;
                return true;
            default:
                treatment = default;
                return false;
        }
    }
}
