using System.Linq;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Robust.Shared.Map;

// ReSharper disable CheckNamespace
namespace Content.Shared._RMC14.MotionDetector;

public sealed partial class MotionDetectorSystem
{
    [Dependency] private CMUSharedZLevelsSystem _cmuZLevels = default!;

    private static readonly int[] CMUOtherLevels = [1, -1];
    private readonly HashSet<Entity<MotionDetectorTrackedComponent>> _cmuOtherLevelTracked = new();

    /// <summary>
    ///     Whether something is moving on the level directly above or below the detector, within its range. The
    ///     detector picks it up and pings, but has no way to place it on the screen, so it shows no blip.
    /// </summary>
    private bool CMUDetectsOtherLevels(EntityUid detector, float range, TimeSpan time, TimeSpan moveTime, EntityUid user)
    {
        var xform = Transform(detector);
        if (xform.MapUid is not { } map || !HasComp<CMUZLevelMapComponent>(map))
            return false;

        var position = _transform.GetWorldPosition(xform);
        _userFactions.Clear();
        var hasFaction = _gunIFF.TryGetFactions((user, null), _userFactions);

        foreach (var offset in CMUOtherLevels)
        {
            if (!_cmuZLevels.TryProjectToZMap(map, offset, position, out var coords, out _))
                continue;

            _cmuOtherLevelTracked.Clear();
            _entityLookup.GetEntitiesInRange(coords, range, _cmuOtherLevelTracked, LookupFlags.Uncontained);
            foreach (var tracked in _cmuOtherLevelTracked)
            {
                if (tracked.Owner == user || tracked.Comp.LastMove < time - moveTime)
                    continue;

                if (hasFaction && _userFactions.Any(f => _gunIFF.IsInFaction(tracked.Owner, f)))
                    continue;

                return true;
            }
        }

        return false;
    }
}
