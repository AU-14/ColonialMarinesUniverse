using Content.Shared._CMU14.Threats;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Xenonids;
using Robust.Shared.Player;

namespace Content.Server._CMU14.Chat;

// Colony-wide announcements are human-side comms; no threat player receives them.
// Xeno/Yautja stay as species-level nets for spawn paths outside ThreatSystem
// (mid-round larva bursts, admin spawns); ThreatComponent covers the threat force proper.
public static class ColonyAnnouncements
{
    public static Filter Recipients(IEntityManager entities) =>
        Filter.Empty().AddWhereAttachedEntity(e =>
            !entities.HasComponent<ThreatComponent>(e)
            && !entities.HasComponent<XenoComponent>(e)
            && !entities.HasComponent<YautjaComponent>(e));
}
