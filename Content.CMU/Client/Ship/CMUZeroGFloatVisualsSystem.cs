using Content.Client.Gravity;
using Content.Shared.Gravity;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Ship;

/// <summary>
///     Starts the zero-g bobbing for anyone the server says is floating. The upstream visualizer only starts it when
///     this client sees weightlessness change itself, which misses mobs whose floating arrives from the server, so
///     on a zero-g ship nobody bobbed.
/// </summary>
public sealed partial class CMUZeroGFloatVisualsSystem : EntitySystem
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(0.25);

    [Dependency] private AnimationPlayerSystem _animation = default!;
    [Dependency] private FloatingVisualizerSystem _floating = default!;
    [Dependency] private IGameTiming _timing = default!;

    private TimeSpan _nextCheck;

    public override void FrameUpdate(float frameTime)
    {
        var now = _timing.RealTime;
        if (now < _nextCheck)
            return;

        _nextCheck = now + CheckInterval;

        var query = EntityQueryEnumerator<FloatingVisualsComponent, GravityAffectedComponent>();
        while (query.MoveNext(out var uid, out var floating, out var gravity))
        {
            // Stopping is already handled: the running animation checks CanFloat each time it finishes.
            if (!floating.CanFloat && !gravity.Weightless)
                continue;

            if (!_animation.HasRunningAnimation(uid, floating.AnimationKey))
                _floating.FloatAnimation(uid, floating.Offset, floating.AnimationKey, floating.AnimationTime);
        }
    }
}
