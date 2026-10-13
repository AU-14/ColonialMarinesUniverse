using Content.Shared._RMC14.Inventory;
using Content.Shared._RMC14.MotionDetector;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared.CMU14.Visor;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Visor;

/// <summary>
/// Links a squad optic to the motion trackers of nearby members of its wearer's squad, so its wearer sees what they pick up.
/// </summary>
public sealed class CMUSquadMotionLinkSystem : EntitySystem
{
    [Dependency] private SharedCMInventorySystem _rmcInventory = default!;
    [Dependency] private SquadSystem _squad = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.25);

    private readonly List<(EntityUid User, IDetectorComponent Detector)> _scans = new();
    private TimeSpan _nextUpdate;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;

        _nextUpdate = now + UpdateInterval;

        var stale = EntityQueryEnumerator<CMUSquadMotionLinkComponent>();
        while (stale.MoveNext(out var uid, out _))
        {
            if (!HasComp<CMUSquadVisorWearerComponent>(uid))
                RemCompDeferred<CMUSquadMotionLinkComponent>(uid);
        }

        // Every tracker that has scanned recently, and who is carrying it.
        _scans.Clear();
        var detectors = EntityQueryEnumerator<MotionDetectorComponent>();
        while (detectors.MoveNext(out var uid, out var comp))
        {
            IDetectorComponent detector = comp;
            if (now > detector.LastScan + detector.ScanDuration ||
                !_rmcInventory.TryGetUserHoldingOrStoringItem(uid, out var user))
            {
                continue;
            }

            _scans.Add((user, detector));
        }

        var wearers = EntityQueryEnumerator<CMUSquadVisorWearerComponent>();
        while (wearers.MoveNext(out var wearer, out _))
        {
            var link = EnsureComp<CMUSquadMotionLinkComponent>(wearer);
            var latest = TimeSpan.Zero;
            var any = false;

            if (_squad.TryGetMemberSquad(wearer, out _))
            {
                var wearerPosition = _transform.GetMapCoordinates(wearer);
                foreach (var (user, detector) in _scans)
                {
                    if (user == wearer ||
                        !_transform.GetMapCoordinates(user).InRange(wearerPosition, link.Range) ||
                        !_squad.AreInSameSquad(user, wearer))
                    {
                        continue;
                    }

                    any = true;
                    if (detector.LastScan > latest)
                        latest = detector.LastScan;
                }
            }

            // Only send a new picture when a linked tracker has scanned since the last one.
            if (!any || latest <= link.LastScan)
                continue;

            link.Blips.Clear();
            var position = _transform.GetMapCoordinates(wearer);
            foreach (var (user, detector) in _scans)
            {
                if (user == wearer ||
                    !_transform.GetMapCoordinates(user).InRange(position, link.Range) ||
                    !_squad.AreInSameSquad(user, wearer))
                {
                    continue;
                }

                link.Blips.AddRange(detector.Blips);
            }

            link.LastScan = latest;
            Dirty(wearer, link);
        }
    }
}
