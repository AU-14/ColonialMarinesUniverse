using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Roles;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Yautja;

// one extra hunt slot only Ancients (and staff, even deadminned) can take, so someone can always
// come aboard to deal with a misbehaving pred IC without fighting the normal slot cap
public sealed partial class YautjaPredatorRoundSystem
{
    public static readonly ProtoId<JobPrototype> AncientJob = "CMUYautjaAncient";

    public bool IsAncientSlotEligible(ICommonSession session)
    {
        if (_admin.GetAdminData(session, includeDeAdmin: true) != null)
            return true;

        var entitlement = _rankManager.ResolveProfileCapabilitiesCached(session.UserId);
        return entitlement.CanUseCouncilStatus || entitlement.Rank == YautjaRank.Ancient;
    }

    private YautjaProfileCapabilities GetAncientSlotCapabilities(ICommonSession session)
    {
        // no council/leader status flags on purpose: ApplyProfile re-resolves the rank from the
        // character's status, and a Normal status with council rights drops you to Blooded
        var entitlement = _rankManager.ResolveProfileCapabilitiesCached(session.UserId);
        return new YautjaProfileCapabilities(
            YautjaRank.Ancient,
            true,
            entitlement.CanUseLegacy);
    }

    private void OnIsRoleAllowed(ref IsRoleAllowedEvent ev)
    {
        if (ev.Cancelled || ev.Jobs == null)
            return;

        foreach (var job in ev.Jobs)
        {
            if (job != AncientJob)
                continue;

            if (!IsAncientSlotEligible(ev.Player))
                ev.Cancelled = true;

            return;
        }
    }

    private void SetAncientSlots(YautjaPredatorRoundComponent rule, int slots)
    {
        // only on the ship's station, same reason as SetSlots
        foreach (var station in GetPredatorStations(rule.PredatorJob, rule.HunterShipMap))
        {
            _stationJobs.TrySetJobSlot(station, AncientJob.Id, slots, true);
            break;
        }
    }
}
