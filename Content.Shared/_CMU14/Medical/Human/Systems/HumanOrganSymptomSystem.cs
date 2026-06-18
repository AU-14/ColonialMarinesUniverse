using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Organs;

namespace Content.Shared._CMU14.Medical.Human.Systems;

public sealed class HumanOrganSymptomSystem : EntitySystem
{
    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<HumanMedicalComponent, ActiveOrganSymptomsComponent>();
        while (query.MoveNext(out var uid, out var medical, out _))
        {
            var tick = CalculateOrganSymptomTick(medical);
            if (!tick.HasSymptoms)
                continue;

            var ev = new HumanOrganSymptomsTickEvent(
                uid,
                tick.WorstOrgan,
                tick.WorstStatus,
                tick.SymptomaticOrgans,
                frameTime);
            RaiseLocalEvent(uid, ref ev);
        }
    }

    public static HumanOrganSymptomTick CalculateOrganSymptomTick(HumanMedicalComponent medical)
    {
        var worstOrgan = OrganSlot.None;
        var worstStatus = OrganDamageStatus.None;
        var symptomaticOrgans = 0;

        foreach (var organ in medical.Organs)
        {
            var status = HumanOrganLedgerUtility.EffectiveStatus(organ);
            if (status == OrganDamageStatus.None)
                continue;

            symptomaticOrgans++;
            if (status <= worstStatus)
                continue;

            worstOrgan = organ.Slot;
            worstStatus = status;
        }

        return new HumanOrganSymptomTick(worstOrgan, worstStatus, symptomaticOrgans);
    }
}

public readonly record struct HumanOrganSymptomTick(
    OrganSlot WorstOrgan,
    OrganDamageStatus WorstStatus,
    int SymptomaticOrgans)
{
    public bool HasSymptoms => SymptomaticOrgans > 0 && WorstStatus != OrganDamageStatus.None;
}

[ByRefEvent]
public readonly record struct HumanOrganSymptomsTickEvent(
    EntityUid Body,
    OrganSlot WorstOrgan,
    OrganDamageStatus WorstStatus,
    int SymptomaticOrgans,
    float FrameTime);
