using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Organs;
using Content.Shared._CMU14.Medical.Human.Rules;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared.FixedPoint;

namespace Content.Shared._CMU14.Medical.Human.Diagnostics;

public static class MedicalActivityClassifier
{
    public static MedicalActivityFlags Classify(HumanMedicalComponent medical)
    {
        var flags = MedicalActivityFlags.None;

        foreach (var bleed in medical.BleedSources)
        {
            if (!bleed.Active)
                continue;

            flags |= MedicalActivityFlags.ActiveBleeding;
            break;
        }

        foreach (var organ in medical.Organs)
        {
            if (HumanOrganLedgerUtility.EffectiveStatus(organ) == OrganDamageStatus.None)
                continue;

            flags |= MedicalActivityFlags.ActiveOrganSymptoms;
            break;
        }

        foreach (var region in medical.Regions)
        {
            if (!region.Skeletal.Knitting)
                continue;

            flags |= MedicalActivityFlags.ActiveBoneKnitting;
            break;
        }

        if (HumanMovementDebuffRules.HasUnsplintedFractureRisk(medical))
            flags |= MedicalActivityFlags.ActiveUnsplintedFractureRisk;

        foreach (var foreignObject in medical.ForeignObjects)
        {
            if (!foreignObject.Active)
                continue;

            flags |= MedicalActivityFlags.ActiveEmbeddedObjectMovement;
            break;
        }

        foreach (var region in medical.Regions)
        {
            if (!region.Tourniquet.Applied ||
                region.Tourniquet.Necrotic ||
                region.Tourniquet.NecrosisSecondsRemaining <= FixedPoint2.Zero)
            {
                continue;
            }

            flags |= MedicalActivityFlags.ActiveTourniquet;
            break;
        }

        foreach (var injury in medical.Injuries)
        {
            if (!HumanMedicalLedger.CanTreatedInjuryRecover(injury))
                continue;

            flags |= MedicalActivityFlags.ActiveTreatedWoundHealing;
            break;
        }

        if (medical.DirtyFlags.HasFlag(MedicalDirtyFlags.Summary))
            flags |= MedicalActivityFlags.ActiveMedicalSummaryDirty;

        return flags;
    }
}
