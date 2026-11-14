using Content.Shared.CMU14.Threats;
using Content.Shared.CMU14.Yautja;
using Content.Shared.NPC.Prototypes;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Yautja.BadBlood;

// the one place that answers "is this a Bad Blood". new code asks here instead of repeating faction or job literals
public sealed partial class BadBloodSystem : EntitySystem
{
    public static readonly ProtoId<NpcFactionPrototype> BadBloodFaction = "CMUYautjaBadBlood";

    private static readonly ProtoId<JobPrototype> ThreatLeaderJob = "AU14JobThreatLeader";

    public bool IsBadBlood(EntityUid uid)
    {
        return HasComp<YautjaBadBloodComponent>(uid);
    }

    /// <summary>
    /// The clan's Leader: whoever holds <see cref="BadBloodLeaderComponent"/>. While nobody holds it (no Bad Blood
    /// rule running, or before it has picked a Leader) this falls back to the threat objective job.
    /// </summary>
    public bool IsBadBloodLeader(EntityUid uid)
    {
        if (!IsBadBlood(uid))
            return false;

        if (HasComp<BadBloodLeaderComponent>(uid))
            return true;

        // someone else leads, so an old objective job means nothing
        var leaders = EntityQueryEnumerator<BadBloodLeaderComponent>();
        if (leaders.MoveNext(out _, out _))
            return false;

        return TryComp(uid, out ThreatComponent? threat) &&
               threat.ObjectiveJob == ThreatLeaderJob;
    }
}
