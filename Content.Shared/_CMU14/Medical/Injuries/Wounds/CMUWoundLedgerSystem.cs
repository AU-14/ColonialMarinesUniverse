using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Injuries.Wounds.Events;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.FixedPoint;
using Robust.Shared.Network;

namespace Content.Shared._CMU14.Medical.Injuries.Wounds;

public readonly record struct CMUWoundCount(int Untreated, int Treated);

public readonly record struct CMUWorstUntreatedWound(WoundSize Size, WoundMechanism Mechanism, FixedPoint2 Damage);

/// <summary>
///     Owns the wound-ledger seam. Queries expose read-only rows while all
///     mutation is rejected on clients.
/// </summary>
public sealed partial class CMUWoundLedgerSystem : EntitySystem
{
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private INetManager _net = default!;

    /// <summary>
    ///     Returns coherent wound rows through a read-only collection contract.
    /// </summary>
    public IReadOnlyList<CMUWoundEntry> GetEntries(BodyPartWoundComponent wounds)
    {
        return wounds.Entries;
    }

    /// <summary>
    ///     Appends one authoritative row and returns its index; clients are rejected with -1.
    /// </summary>
    public int AddEntry(BodyPartWoundComponent wounds, CMUWoundEntry entry)
    {
        if (_net.IsClient)
            return -1;

        wounds.Entries.Add(entry);
        return wounds.Entries.Count - 1;
    }

    /// <summary>
    ///     Replaces one complete authoritative row without permitting partial-field misalignment.
    /// </summary>
    public bool TryUpdateEntry(BodyPartWoundComponent wounds, int index, CMUWoundEntry entry)
    {
        if (_net.IsClient || index < 0 || index >= wounds.Entries.Count)
            return false;

        wounds.Entries[index] = entry;
        return true;
    }

    /// <summary>
    ///     Removes one coherent authoritative row when the index is valid.
    /// </summary>
    public bool TryRemoveEntry(BodyPartWoundComponent wounds, int index)
    {
        if (_net.IsClient || index < 0 || index >= wounds.Entries.Count)
            return false;

        wounds.Entries.RemoveAt(index);
        return true;
    }

    /// <summary>
    ///     Clears all authoritative rows and reports whether state changed.
    /// </summary>
    public bool ClearEntries(BodyPartWoundComponent wounds)
    {
        if (_net.IsClient || wounds.Entries.Count == 0)
            return false;

        wounds.Entries.Clear();
        return true;
    }

    /// <summary>
    ///     Updates the public bleeding tier and publishes the normal wound-change seam.
    /// </summary>
    public bool TryUpdateExternalBleeding(
        EntityUid part,
        ExternalBleedTier tier,
        BodyPartWoundComponent? wounds = null)
    {
        if (_net.IsClient ||
            !Resolve(part, ref wounds, false) ||
            wounds.ExternalBleeding == tier)
        {
            return false;
        }

        wounds.ExternalBleeding = tier;
        var changed = new BodyPartWoundsChangedEvent(part, false);
        RaiseLocalEvent(ref changed);
        return true;
    }

    public CMUWoundCount CountWounds(BodyPartWoundComponent wounds)
    {
        var untreated = 0;
        var treated = 0;
        foreach (var entry in wounds.Entries)
        {
            if (entry.Wound.Treated)
                treated++;
            else
                untreated++;
        }

        return new CMUWoundCount(untreated, treated);
    }

    public int CountUntreatedWounds(BodyPartWoundComponent wounds)
    {
        var untreated = 0;
        foreach (var entry in wounds.Entries)
        {
            if (!entry.Wound.Treated)
                untreated++;
        }

        return untreated;
    }

    public CMUWorstUntreatedWound? GetWorstUntreatedWound(BodyPartWoundComponent wounds)
    {
        CMUWorstUntreatedWound? worst = null;
        var worstRank = -1;
        var worstDamage = 0f;
        foreach (var entry in wounds.Entries)
        {
            if (entry.Wound.Treated)
                continue;

            var damage = entry.Wound.Damage.Float();
            var rank = WoundSizeProfile.SeverityRank(entry.Size, damage);
            if (worst is not null &&
                (rank < worstRank || rank == worstRank && damage <= worstDamage))
            {
                continue;
            }

            worst = new CMUWorstUntreatedWound(entry.Size, entry.Mechanism, entry.Wound.Damage);
            worstRank = rank;
            worstDamage = damage;
        }

        return worst;
    }

    public bool BodyHasWoundOfType(EntityUid body, WoundType type)
    {
        foreach (var (partUid, _) in _medicalIndex.GetBodyParts(body))
        {
            if (!TryComp<BodyPartWoundComponent>(partUid, out var wounds))
                continue;

            foreach (var entry in wounds.Entries)
            {
                if (entry.Wound.Type == type)
                    return true;
            }
        }

        return false;
    }

    public bool CanUseBleedControl(
        EntityUid part,
        bool stopsArterial,
        out bool blockedByArterial,
        BodyPartWoundComponent? wounds = null)
    {
        blockedByArterial = false;
        if (!Resolve(part, ref wounds, false) ||
            wounds.ExternalBleeding == ExternalBleedTier.None)
        {
            return false;
        }

        if (wounds.ExternalBleeding != ExternalBleedTier.Arterial || stopsArterial)
            return true;

        blockedByArterial = true;
        return false;
    }

}
