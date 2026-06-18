namespace Content.Shared._CMU14.Medical.Foundation;

public static class MedicalActionRules
{
    public static bool IsFieldTreatment(MedicalActionKind kind)
    {
        return kind is
            MedicalActionKind.ApplyGauze or
            MedicalActionKind.ApplySalve or
            MedicalActionKind.ApplySplint or
            MedicalActionKind.ApplyCast or
            MedicalActionKind.ApplySuture or
            MedicalActionKind.ApplyClamp or
            MedicalActionKind.ApplyTourniquet or
            MedicalActionKind.RemoveTourniquet or
            MedicalActionKind.ApplySyntheticGraft or
            MedicalActionKind.ApplySurgicalLine;
    }

    public static bool IsSurgery(MedicalActionKind kind)
    {
        return kind is
            MedicalActionKind.Incise or
            MedicalActionKind.Retract or
            MedicalActionKind.CloseIncision or
            MedicalActionKind.RepairInternalBleeding or
            MedicalActionKind.RepairOrgan or
            MedicalActionKind.SetBone or
            MedicalActionKind.Amputate or
            MedicalActionKind.ReattachLimb or
            MedicalActionKind.FitProsthetic or
            MedicalActionKind.DebrideEschar or
            MedicalActionKind.RemoveShrapnel or
            MedicalActionKind.RemoveOrgan or
            MedicalActionKind.InsertOrgan;
    }

    public static bool IsDiagnostic(MedicalActionKind kind)
    {
        return kind is
            MedicalActionKind.Scan or
            MedicalActionKind.Stethoscope;
    }

    public static bool RequiresRegion(MedicalActionKind kind)
    {
        return kind is not
            MedicalActionKind.None and not
            MedicalActionKind.Scan and not
            MedicalActionKind.Defibrillate and not
            MedicalActionKind.StabilizeOrgan;
    }

    public static MedicalActionFlags GetDefaultFlags(MedicalActionKind kind)
    {
        return kind switch
        {
            MedicalActionKind.ApplyGauze =>
                FieldTreatmentFlags | MedicalActionFlags.StopsBleeding,
            MedicalActionKind.ApplySalve =>
                FieldTreatmentFlags,
            MedicalActionKind.ApplySplint =>
                FieldTreatmentFlags | MedicalActionFlags.RequiresFollowupTreatment,
            MedicalActionKind.ApplyCast =>
                FieldTreatmentFlags,
            MedicalActionKind.ApplySuture =>
                FieldTreatmentFlags | MedicalActionFlags.StopsBleeding,
            MedicalActionKind.ApplyClamp =>
                FieldTreatmentFlags | MedicalActionFlags.SuppressesBleeding | MedicalActionFlags.RequiresFollowupTreatment,
            MedicalActionKind.ApplyTourniquet =>
                FieldTreatmentFlags | MedicalActionFlags.SuppressesBleeding | MedicalActionFlags.RequiresFollowupTreatment,
            MedicalActionKind.RemoveTourniquet =>
                FieldTreatmentFlags | MedicalActionFlags.RequiresFollowupTreatment,
            MedicalActionKind.ApplySyntheticGraft =>
                FieldTreatmentFlags | MedicalActionFlags.SuppressesBleeding | MedicalActionFlags.RequiresFollowupTreatment,
            MedicalActionKind.ApplySurgicalLine =>
                FieldTreatmentFlags | MedicalActionFlags.SuppressesBleeding | MedicalActionFlags.RequiresFollowupTreatment,
            MedicalActionKind.SetBone =>
                SurgeryFlags | MedicalActionFlags.RequiresFollowupTreatment,
            MedicalActionKind.Incise or
            MedicalActionKind.Retract or
            MedicalActionKind.CloseIncision =>
                SurgeryFlags,
            MedicalActionKind.RepairInternalBleeding =>
                DeepSurgeryFlags | MedicalActionFlags.StopsBleeding,
            MedicalActionKind.RepairOrgan or
            MedicalActionKind.RemoveOrgan or
            MedicalActionKind.InsertOrgan =>
                DeepSurgeryFlags,
            MedicalActionKind.Amputate or
            MedicalActionKind.ReattachLimb or
            MedicalActionKind.FitProsthetic =>
                DeepSurgeryFlags | MedicalActionFlags.RequiresFollowupTreatment,
            MedicalActionKind.DebrideEschar or
            MedicalActionKind.RemoveShrapnel =>
                SurgeryFlags,
            MedicalActionKind.Scan or
            MedicalActionKind.Stethoscope =>
                MedicalActionFlags.None,
            MedicalActionKind.Defibrillate or
            MedicalActionKind.StabilizeOrgan =>
                MedicalActionFlags.RequiresDoAfter | MedicalActionFlags.DirtySummary,
            _ => MedicalActionFlags.None,
        };
    }

    public static MedicalActionResult Accept(MedicalActionKind kind)
    {
        return new MedicalActionResult(
            MedicalActionOutcome.Accepted,
            kind,
            GetDefaultFlags(kind));
    }

    public static MedicalActionResult RequiresDoAfter(MedicalActionKind kind)
    {
        return new MedicalActionResult(
            MedicalActionOutcome.RequiresDoAfter,
            kind,
            GetDefaultFlags(kind));
    }

    public static MedicalActionResult Reject(
        MedicalActionKind kind,
        MedicalActionOutcome outcome,
        string failureReason)
    {
        return new MedicalActionResult(
            outcome,
            kind,
            GetDefaultFlags(kind),
            failureReason);
    }

    private const MedicalActionFlags FieldTreatmentFlags =
        MedicalActionFlags.RequiresBodyPartPicker |
        MedicalActionFlags.RequiresDoAfter |
        MedicalActionFlags.DirtySummary;

    private const MedicalActionFlags SurgeryFlags =
        MedicalActionFlags.RequiresDoAfter |
        MedicalActionFlags.DirtySummary;

    private const MedicalActionFlags DeepSurgeryFlags =
        SurgeryFlags |
        MedicalActionFlags.RequiresDeepAccess;
}
