using Content.Shared.CMU14.Ambassador;

namespace Content.Server._RMC14.Marines;

public sealed partial class MarineAnnounceSystem
{
    // ambassadors wear a side's Marine faction for IFF but shouldn't hear the CO's broadcasts
    private bool IsCMUAnnouncementExcluded(EntityUid uid)
    {
        return HasComp<AmbassadorComponent>(uid);
    }
}
