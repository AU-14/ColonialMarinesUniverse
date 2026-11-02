using Content.Shared.CMU14.Yautja;

namespace Content.Server._RMC14.Announce;

public sealed partial class MarinePresenceAnnounceSystem
{
    // yautja jobs have no department radio, so the late join notice fell back to marine common and APOLLO
    // announced a stranded hunter waking up
    private bool IsCMULateJoinAnnouncementExcluded(EntityUid mob)
    {
        return HasComp<YautjaComponent>(mob);
    }
}
