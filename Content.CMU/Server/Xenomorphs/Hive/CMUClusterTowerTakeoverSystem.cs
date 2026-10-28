using Content.Shared._RMC14.Xenonids.ManageHive.Boons;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Xenomorphs.Hive;

// BUG-1057: clusters already sitting on a tower turn into a pylon once the takeover time passes
public sealed partial class CMUClusterTowerTakeoverSystem : EntitySystem
{
    [Dependency] private HiveBoonSystem _hiveBoon = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(5);
    private TimeSpan _nextCheck;

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextCheck)
            return;

        _nextCheck = _timing.CurTime + CheckInterval;
        _hiveBoon.RetryClusterTowerTakeovers();
    }
}
