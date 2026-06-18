using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Rules;
using Content.Shared._CMU14.Medical.Human.Effects;

namespace Content.Shared._CMU14.Medical.Human.Diagnostics;

public static class MedicalSummaryBuilder
{
    public static MedicalSummary Build(HumanMedicalComponent medical)
    {
        var summary = new MedicalSummary
        {
            Revision = medical.Revision,
            HudStatus = HudStatus.Healthy,
            WalkingSlowdownPoints = SharedCMUMedicalSpeedSystem.CalculateLedgerMedicalMovementSlowdownPoints(
                medical,
                wheelchair: false),
            WheelchairSlowdownPoints = SharedCMUMedicalSpeedSystem.CalculateLedgerMedicalMovementSlowdownPoints(
                medical,
                wheelchair: true),
        };

        foreach (var bleed in medical.BleedSources)
        {
            if (bleed.Kind == BleedKind.Internal && !IsBleedRepaired(bleed))
            {
                summary.HasInternalBleeding = true;
                summary.Alerts |= MedicalAlertFlags.InternalBleeding;
            }

            if (IsBleedSuppressedButUnrepaired(bleed))
            {
                summary.HasSuppressedBleeding = true;
                summary.Alerts |= MedicalAlertFlags.SuppressedBleedingNeedsSurgery;
            }

            if (bleed.Active)
            {
                summary.Alerts |= MedicalAlertFlags.ActiveBleeding;

                var severity = BleedingRules.GetSeverity(bleed.Rate);
                if (severity > summary.WorstBleed)
                    summary.WorstBleed = severity;
            }
        }

        foreach (var injury in medical.Injuries)
        {
            if (injury.Kind == InjuryKind.InternalBleed)
            {
                summary.HasInternalBleeding = true;
                summary.Alerts |= MedicalAlertFlags.InternalBleeding;
            }

            if (injury.IsOpenStump)
            {
                summary.HasOpenStump = true;
                summary.Alerts |= MedicalAlertFlags.OpenStump;
            }

            if (IsSevereBurn(injury))
            {
                summary.HasSevereBurn = true;
                summary.Alerts |= MedicalAlertFlags.SevereBurn;
            }
        }

        foreach (var region in medical.Regions)
        {
            if (region.Presence == LimbPresence.Missing || region.Presence == LimbPresence.Detached)
                summary.Alerts |= MedicalAlertFlags.MissingLimb;

            if (region.Skeletal.Broken && !region.Skeletal.Splinted)
            {
                summary.HasBrokenUnsplintedLimb = true;
                summary.Alerts |= MedicalAlertFlags.BrokenUnsplintedLimb;
            }

            if (region.Skeletal.Broken && IsCoreRegion(region.Region))
            {
                summary.HasCoreFracture = true;
                summary.Alerts |= MedicalAlertFlags.CoreFracture;
            }

            if (region.Incision != IncisionDepth.Closed)
            {
                summary.HasOpenIncision = true;
                summary.Alerts |= MedicalAlertFlags.OpenIncision;
            }

            if (region.Tourniquet.Applied)
            {
                summary.HasTourniquet = true;
                summary.Alerts |= MedicalAlertFlags.Tourniquet;
            }

            if (region.Tourniquet.Necrotic)
            {
                summary.HasNecroticRegion = true;
                summary.Alerts |= MedicalAlertFlags.NecroticRegion;
            }
        }

        foreach (var organ in medical.Organs)
        {
            if (organ.Status == OrganDamageStatus.None || organ.Missing)
                continue;

            summary.HasOrganDamage = true;
            summary.Alerts |= MedicalAlertFlags.OrganDamage;
        }

        summary.HudStatus = GetHudStatus(summary);
        if (summary.HudStatus == HudStatus.Critical)
            summary.Alerts |= MedicalAlertFlags.Critical;

        return summary;
    }

    private static HudStatus GetHudStatus(MedicalSummary summary)
    {
        if (summary.WorstBleed >= BleedSeverity.Heavy ||
            summary.HasInternalBleeding && summary.HasOrganDamage)
        {
            return HudStatus.Critical;
        }

        if (summary.WorstBleed >= BleedSeverity.Moderate ||
            summary.HasOrganDamage ||
            summary.HasBrokenUnsplintedLimb ||
            summary.HasCoreFracture ||
            summary.HasOpenStump ||
            summary.HasNecroticRegion ||
            summary.HasSevereBurn)
        {
            return HudStatus.Serious;
        }

        if (summary.WorstBleed > BleedSeverity.None ||
            summary.HasOpenIncision ||
            summary.Alerts.HasFlag(MedicalAlertFlags.MissingLimb))
        {
            return HudStatus.Wounded;
        }

        return summary.Alerts == MedicalAlertFlags.None
            ? HudStatus.Healthy
            : HudStatus.Stable;
    }

    private static bool IsBleedRepaired(BleedSource bleed)
    {
        return bleed.Treatment.HasFlag(TreatmentFlags.Closed) ||
            bleed.Treatment.HasFlag(TreatmentFlags.Sutured);
    }

    private static bool IsBleedSuppressedButUnrepaired(BleedSource bleed)
    {
        if (IsBleedRepaired(bleed))
            return false;

        return bleed.Treatment.HasFlag(TreatmentFlags.Clamped) ||
            bleed.Treatment.HasFlag(TreatmentFlags.TemporarilySuppressed) ||
            bleed.Treatment.HasFlag(TreatmentFlags.Tourniquetted);
    }

    private static bool IsSevereBurn(InjuryRecord injury)
    {
        return injury.Kind == InjuryKind.Burn &&
            !injury.Flags.HasFlag(InjuryFlags.Debrided) &&
            (injury.Stage is InjuryStage.Severe or InjuryStage.Carbonised ||
             injury.Flags.HasFlag(InjuryFlags.Necrotic));
    }

    private static bool IsCoreRegion(BodyRegion region)
    {
        return region is BodyRegion.Head or BodyRegion.Chest or BodyRegion.Groin;
    }
}
