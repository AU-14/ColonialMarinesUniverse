using Content.Shared._RMC14.Medical.Stasis;
using Content.Shared._RMC14.Synth;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.FixedPoint;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Medical.Injuries.Wounds;

public sealed class CMUOpenStumpSystem : SharedCMUOpenStumpSystem
{
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedCMUWoundsSystem _wounds = default!;

    /// <summary>Blood lost per second from each open stump; the same as an arterial wound.</summary>
    private const float BleedPerSecond = 0.70f;

    private static readonly TimeSpan BleedInterval = TimeSpan.FromSeconds(1);

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUOpenStumpComponent, BodyPartComponent>();
        while (query.MoveNext(out var uid, out var stumps, out var part))
        {
            if (now < stumps.NextBleed)
                continue;

            stumps.NextBleed = now + BleedInterval;

            // synths don't bleed from wounds and never refill on their own, so an open stump
            // used to drain a dead synth to zero before anyone could weld it back together
            if (part.Body is not { } body ||
                HasComp<SynthComponent>(body) ||
                HasComp<CMInStasisComponent>(body) ||
                _wounds.IsBloodFlowOccluded(uid) ||
                !TryComp<BloodstreamComponent>(body, out var bloodstream))
            {
                continue;
            }

            var open = 0;
            foreach (var stump in stumps.Stumps)
            {
                if (!stump.Clamped)
                    open++;
            }

            if (open > 0)
                _bloodstream.TryBleedOut((body, bloodstream), FixedPoint2.New(BleedPerSecond * open));
        }
    }
}
