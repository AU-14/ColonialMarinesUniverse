using Content.Shared.Body.Systems;
using Content.Shared._RMC14.Medical.Wounds;

namespace Content.Shared._CMU14.Medical.Injuries.Wounds;

public readonly record struct CMUWoundCount(int Untreated, int Treated);

public readonly record struct CMUWorstUntreatedWound(WoundSize Size, WoundMechanism Mechanism);

public sealed partial class CMUWoundLedgerSystem : EntitySystem
{
    [Dependency] private SharedBodySystem _body = default!;

    public CMUWoundCount CountWounds(BodyPartWoundComponent wounds)
    {
        var untreated = 0;
        var treated = 0;
        foreach (var wound in wounds.Wounds)
        {
            if (wound.Treated)
                treated++;
            else
                untreated++;
        }

        return new CMUWoundCount(untreated, treated);
    }

    public int CountUntreatedWounds(BodyPartWoundComponent wounds)
    {
        var untreated = 0;
        foreach (var wound in wounds.Wounds)
        {
            if (!wound.Treated)
                untreated++;
        }

        return untreated;
    }

    public CMUWorstUntreatedWound? GetWorstUntreatedWound(BodyPartWoundComponent wounds)
    {
        CMUWorstUntreatedWound? worst = null;
        for (var i = 0; i < wounds.Wounds.Count; i++)
        {
            var wound = wounds.Wounds[i];
            if (wound.Treated)
                continue;

            var size = i < wounds.Sizes.Count ? wounds.Sizes[i] : WoundSize.Deep;
            if (worst is not null && (byte) size <= (byte) worst.Value.Size)
                continue;

            var mechanism = i < wounds.Mechanisms.Count
                ? wounds.Mechanisms[i]
                : LegacyMechanismFor(wound.Type);
            worst = new CMUWorstUntreatedWound(size, mechanism);
        }

        return worst;
    }

    public bool BodyHasWoundOfType(EntityUid body, WoundType type)
    {
        foreach (var (partUid, _) in _body.GetBodyChildren(body))
        {
            if (!TryComp<BodyPartWoundComponent>(partUid, out var wounds))
                continue;

            foreach (var wound in wounds.Wounds)
            {
                if (wound.Type == type)
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

    public static WoundMechanism LegacyMechanismFor(WoundType type)
    {
        return type switch
        {
            WoundType.Burn => WoundMechanism.Burn,
            WoundType.Surgery => WoundMechanism.Surgical,
            _ => WoundMechanism.Generic,
        };
    }
}
