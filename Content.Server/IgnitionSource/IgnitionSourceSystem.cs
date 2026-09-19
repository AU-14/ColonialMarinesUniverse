using Content.Server.Atmos.EntitySystems;
using Content.Shared.IgnitionSource;
using Robust.Shared.Timing; // CMU14

namespace Content.Server.IgnitionSource;

public sealed partial class IgnitionSourceSystem : SharedIgnitionSourceSystem
{
    // CMU14: sustained sources only need to re-light their tile about once per second;
    // per-tick exposes multiplied hotspot work by the tick rate.
    private static readonly TimeSpan ExposeInterval = TimeSpan.FromSeconds(1);

    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!; // CMU14

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<IgnitionSourceComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (!comp.Ignited)
                continue;

            // CMU14: throttle per-source exposes to the sustain cadence.
            if (_timing.CurTime < comp.NextExpose)
                continue;

            if (xform.GridUid is { } gridUid)
            {
                var position = _transform.GetGridOrMapTilePosition(uid, xform);
                // TODO: Should this be happening every single tick?
                _atmosphere.HotspotExpose(gridUid, position, comp.Temperature, 50, uid, true);
                // CMU14
                comp.NextExpose = _timing.CurTime + ExposeInterval;
            }
        }
    }
}
