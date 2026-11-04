using Content.Shared.CMU14.Medical.Synth;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Medical.Synth;

public sealed partial class CMUSynthBloodFilterSystem : EntitySystem
{
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan FilterInterval = TimeSpan.FromSeconds(2);
    private static readonly FixedPoint2 FlushAll = FixedPoint2.New(1000);

    private TimeSpan _nextFilter;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextFilter)
            return;

        _nextFilter = now + FilterInterval;

        // synths never metabolize, so foreign stuff just squats in the bloodstream and eats transfusion room.
        // IV stands skip the hand-held pack's blood type check, so a human pack on a stand ends up here
        var query = EntityQueryEnumerator<CMUSynthCirculationComponent, BloodstreamComponent>();
        while (query.MoveNext(out var uid, out _, out var bloodstream))
        {
            _bloodstream.FlushChemicals((uid, bloodstream), FlushAll);
        }
    }
}
