using Content.Shared.CMU14.SSDTimer;
using Content.Shared.Mind.Components;
using Content.Shared.SSDIndicator;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.SSDTimer;

/// <summary>
///     Notes when a mob goes SSD, for <see cref="CMUSSDTimerComponent"/>.
/// </summary>
public sealed class CMUSSDTimerSystem : EntitySystem
{
    private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(1);

    [Dependency] private IGameTiming _timing = default!;

    private TimeSpan _nextCheck;

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextCheck)
            return;

        _nextCheck = now + CheckEvery;

        var query = EntityQueryEnumerator<SSDIndicatorComponent, MindContainerComponent>();
        while (query.MoveNext(out var uid, out var ssd, out var mind))
        {
            // Only a player who disconnected still has their mind in the body; ghosts and never-played mobs don't.
            var disconnected = ssd.IsSSD && mind.HasMind;
            var timed = HasComp<CMUSSDTimerComponent>(uid);
            if (disconnected && !timed)
            {
                var timer = AddComp<CMUSSDTimerComponent>(uid);
                timer.Since = now;
                Dirty(uid, timer);
            }
            else if (!disconnected && timed)
            {
                RemComp<CMUSSDTimerComponent>(uid);
            }
        }
    }
}
