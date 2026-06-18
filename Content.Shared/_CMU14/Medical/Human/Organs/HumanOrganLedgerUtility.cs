using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Systems;

namespace Content.Shared._CMU14.Medical.Human.Organs;

public static class HumanOrganLedgerUtility
{
    public static bool OrgansChanged(MedicalTransactionResult result)
    {
        return result.DirtyFlags.HasFlag(MedicalDirtyFlags.Organs);
    }

    public static OrganDamageStatus EffectiveStatus(HumanMedicalComponent medical, OrganSlot slot)
    {
        var organ = HumanMedicalLedger.GetOrgan(medical, slot);
        return EffectiveStatus(organ);
    }

    public static OrganDamageStatus EffectiveStatus(OrganState organ)
    {
        if (organ.Missing)
            return OrganDamageStatus.Broken;

        if (organ.Flags.HasFlag(OrganFlags.Stasis))
            return OrganDamageStatus.None;

        return organ.Status;
    }

    public static bool IsMissing(HumanMedicalComponent medical, OrganSlot slot)
    {
        return HumanMedicalLedger.GetOrgan(medical, slot).Missing;
    }

    public static OrganDamageStatus BestStatus(
        HumanMedicalComponent medical,
        OrganSlot first,
        OrganSlot second)
    {
        var firstStatus = EffectiveStatus(medical, first);
        var secondStatus = EffectiveStatus(medical, second);
        return firstStatus <= secondStatus ? firstStatus : secondStatus;
    }

    public static OrganDamageStatus WorstStatus(HumanMedicalComponent medical)
    {
        var worst = OrganDamageStatus.None;
        foreach (var organ in medical.Organs)
        {
            if (organ.Slot == OrganSlot.None)
                continue;

            var status = EffectiveStatus(organ);
            if (status <= worst)
                continue;

            worst = status;
        }

        return worst;
    }
}
