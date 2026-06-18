using Content.Shared._CMU14.Medical.Foundation;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Care;

namespace Content.Shared._CMU14.Medical.Human.Care;

public static class MedicalBleedControlRules
{
    public static bool TryCreateBleedControlAttempt(
        HumanMedicalComponent medical,
        BodyRegion aimedRegion,
        bool stopsArterialBleeding,
        out TreatmentAttempt attempt,
        out bool blockedByArterial)
    {
        blockedByArterial = false;

        if (aimedRegion == BodyRegion.None)
        {
            attempt = default;
            return false;
        }

        if (TryCreateBleedControlAttemptForRegion(
                medical,
                aimedRegion,
                stopsArterialBleeding,
                out attempt,
                ref blockedByArterial))
        {
            return true;
        }

        attempt = default;
        return false;
    }

    private static bool TryCreateBleedControlAttemptForRegion(
        HumanMedicalComponent medical,
        BodyRegion region,
        bool stopsArterialBleeding,
        out TreatmentAttempt attempt,
        ref bool blockedByArterial)
    {
        BleedSource? regularBleed = null;

        foreach (var source in medical.BleedSources)
        {
            if (source.Region != region ||
                !source.Active ||
                !IsSurfaceBleed(source.Kind))
            {
                continue;
            }

            if (!source.Flags.HasFlag(BleedFlags.Arterial))
            {
                regularBleed ??= source;
                continue;
            }

            if (!stopsArterialBleeding)
            {
                blockedByArterial = true;
                continue;
            }

            return TryCreateGauzeAttempt(source.Region, source.Id, out attempt);
        }

        if (regularBleed is { } bleed)
            return TryCreateGauzeAttempt(bleed.Region, bleed.Id, out attempt);

        if (TryFindBandageableInjury(medical, region, out var injury))
            return TryCreateGauzeAttempt(injury.Region, bleedSourceId: 0, injuryId: injury.Id, out attempt);

        attempt = default;
        return false;
    }

    private static bool TryCreateGauzeAttempt(
        BodyRegion region,
        int bleedSourceId,
        int injuryId,
        out TreatmentAttempt attempt)
    {
        var request = new MedicalActionRequest(
            default,
            default,
            null,
            MedicalActionKind.ApplyGauze,
            MedicalActionSourceKind.HandItem,
            MedicalActionTargetKind.Region,
            region);

        if (!MedicalTreatmentActionRules.TryCreateHumanTreatmentAttempt(request, out attempt))
            return false;

        attempt = new TreatmentAttempt(
            attempt.Kind,
            region,
            InjuryId: injuryId,
            BleedSourceId: bleedSourceId);
        return true;
    }

    private static bool TryCreateGauzeAttempt(
        BodyRegion region,
        int bleedSourceId,
        out TreatmentAttempt attempt)
    {
        return TryCreateGauzeAttempt(region, bleedSourceId, injuryId: 0, out attempt);
    }

    private static bool TryFindBandageableInjury(
        HumanMedicalComponent medical,
        BodyRegion region,
        out InjuryRecord injury)
    {
        foreach (var candidate in medical.Injuries)
        {
            if (candidate.Region != region ||
                candidate.Flags.HasFlag(InjuryFlags.Bandaged) ||
                candidate.Flags.HasFlag(InjuryFlags.Closed) ||
                candidate.Flags.HasFlag(InjuryFlags.Sutured) ||
                !IsBandageable(candidate.Kind))
            {
                continue;
            }

            injury = candidate;
            return true;
        }

        injury = default;
        return false;
    }

    private static bool IsSurfaceBleed(BleedKind kind)
    {
        return kind is BleedKind.External or BleedKind.Stump;
    }

    private static bool IsBandageable(InjuryKind kind)
    {
        return kind is InjuryKind.Cut or InjuryKind.Puncture or InjuryKind.Bruise or InjuryKind.Stump;
    }
}
