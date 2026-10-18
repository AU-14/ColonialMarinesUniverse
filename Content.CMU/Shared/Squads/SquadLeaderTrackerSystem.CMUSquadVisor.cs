using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Tracker.SquadLeader;

// CMU14: the squad optic grants a tracker. Lives in a partial because SetMode is private.
public sealed partial class SquadLeaderTrackerSystem
{
    private static readonly ProtoId<TrackerModePrototype> CMUSquadVisorDefaultMode = "SquadLeader";

    private static readonly HashSet<ProtoId<TrackerModePrototype>> CMUSquadVisorModes = new()
    {
        "SquadLeader",
        "FireteamLeader",
    };

    /// <summary>Gives a squad leader / fireteam leader tracker to a mob that doesn't already have a tracker.</summary>
    /// <returns>True if a tracker was added.</returns>
    public bool CMUTryGrantSquadVisorTracker(EntityUid uid)
    {
        if (HasComp<SquadLeaderTrackerComponent>(uid))
            return false;

        var tracker = EnsureComp<SquadLeaderTrackerComponent>(uid);
        tracker.TrackerModes = new HashSet<ProtoId<TrackerModePrototype>>(CMUSquadVisorModes);
        tracker.ManualMode = false;
        SetMode((uid, tracker), CMUSquadVisorDefaultMode);
        Dirty(uid, tracker);
        return true;
    }

    /// <summary>Takes away a tracker the squad optic gave, unless worn gear still grants one.</summary>
    public void CMURemoveSquadVisorTracker(EntityUid uid)
    {
        if (_inventory.TryGetInventoryEntity<GrantSquadLeaderTrackerComponent>(uid, out _))
            return;

        RemCompDeferred<SquadLeaderTrackerComponent>(uid);
    }
}
