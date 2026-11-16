namespace Content.Shared._RMC14.Tracker.Xeno;

public sealed partial class HiveTrackerSystem
{
    private void CMUOnTrackableShutdown(Entity<RMCTrackableComponent> target, ref ComponentShutdown args)
    {
        if (!_net.IsServer)
            return;

        // The periodic direction refresh is too late if deletion happens before PVS runs.
        var query = EntityQueryEnumerator<HiveTrackerComponent>();
        while (query.MoveNext(out var uid, out var tracker))
        {
            if (tracker.Target == target.Owner && !TerminatingOrDeleted(uid))
                SetTarget((uid, tracker), null);
        }
    }
}
