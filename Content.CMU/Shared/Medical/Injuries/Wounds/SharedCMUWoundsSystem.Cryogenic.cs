using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Body.Part;
using Content.Shared.FixedPoint;

namespace Content.Shared.CMU14.Medical.Injuries.Wounds;

public abstract partial class SharedCMUWoundsSystem
{
    /// <summary>
    /// Closes the selected wound category over successive metabolized doses, including
    /// wounds on dead patients whose ordinary recovery tick is inactive.
    /// </summary>
    public void CryogenicallyHealWounds(EntityUid body, WoundType type, FixedPoint2 amount)
    {
        if (Net.IsClient || !IsEnabled() || amount <= FixedPoint2.Zero)
            return;

        foreach (var (partUid, part) in MedicalIndex.GetBodyParts(body))
        {
            if (part.Body != body ||
                !TryComp<BodyPartWoundComponent>(partUid, out var wounds))
                continue;

            CryogenicallyHealPart(body, partUid, part, wounds, type, amount);
        }
    }

    private void CryogenicallyHealPart(EntityUid body, EntityUid partUid, BodyPartComponent part,
        BodyPartWoundComponent wounds, WoundType type, FixedPoint2 amount)
    {
        var remainingTypeDamage = FixedPoint2.Zero;
        foreach (var entry in wounds.Entries)
        {
            if (entry.Wound.Type == type)
                remainingTypeDamage += FixedPoint2.Max(FixedPoint2.Zero, entry.Wound.Damage - entry.Wound.Healed);
        }

        if (remainingTypeDamage <= FixedPoint2.Zero)
            return;

        var changed = false;
        for (var i = wounds.Entries.Count - 1; i >= 0; i--)
        {
            var entry = wounds.Entries[i];
            if (entry.Wound.Type != type)
                continue;

            var remaining = FixedPoint2.Max(FixedPoint2.Zero, entry.Wound.Damage - entry.Wound.Healed);
            var healing = FixedPoint2.Min(amount, remaining);
            if (healing <= FixedPoint2.Zero)
                continue;

            var wound = entry.Wound with
            {
                Healed = entry.Wound.Healed + healing,
                Treated = true,
                Bloodloss = 0f,
                StopBleedAt = Timing.CurTime,
            };
            var groupRemaining = remainingTypeDamage;
            remainingTypeDamage -= healing;
            if (wound.Healed >= wound.Damage)
                RemoveWoundAt(wounds, i);
            else
                WoundLedger.TryUpdateEntry(wounds, i, entry with
                {
                    Wound = wound,
                    Bandages = WoundSizeProfile.BandagesRequired(entry.Size, entry.Wound.Damage.Float()),
                    TreatmentQuality = WoundTreatmentQuality.Adequate,
                    Cleanup = WoundCleanupFlags.None,
                });

            changed = true;
            var revision = wounds.Revision;
            ApplyWoundHealingDamage(body, partUid, type, healing, groupRemaining);
            if (!IsCurrentWoundOwner(body, partUid, part, wounds) || wounds.Revision != revision)
                return;
        }

        if (!changed || !IsCurrentWoundOwner(body, partUid, part, wounds))
            return;

        if (type == WoundType.Brute)
            ClearExternalBleeding(wounds);

        if (type == WoundType.Burn && HasComp<CMUEscharComponent>(partUid))
        {
            var hasBurnWound = false;
            foreach (var entry in wounds.Entries)
                hasBurnWound |= entry.Wound.Type == WoundType.Burn;
            if (!hasBurnWound)
                RemComp<CMUEscharComponent>(partUid);
        }

        if (wounds.Entries.Count == 0 && wounds.ExternalBleeding == ExternalBleedTier.None)
        {
            OnPartWoundsCleared(body, partUid);
            RemComp<BodyPartWoundComponent>(partUid);
            RaiseWoundsChanged(partUid, true);
        }
        else
        {
            RaiseWoundsChanged(partUid, false);
        }
    }
}
