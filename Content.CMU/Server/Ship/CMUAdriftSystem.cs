using Content.Shared.CMU14.Ship;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Ship;

/// <summary>
///     Tracks who is adrift: alive and floating over open space outside a <see cref="CMUZeroGSpaceComponent"/> ship.
///     Round-end checks leave them out until they get back aboard, so nobody can stall a round by drifting off into
///     space. <see cref="CMUAdriftChangedEvent"/> lets those checks re-run when someone drifts off or comes back.
/// </summary>
public sealed class CMUAdriftSystem : EntitySystem
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(1);

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private CMUZeroGSpaceSystem _zeroG = default!;

    private readonly HashSet<EntityUid> _adrift = new();
    private readonly List<EntityUid> _changed = new();
    private TimeSpan _nextCheck;

    /// <summary>
    ///     True if this mob is alive and adrift outside a zero-g ship, so it shouldn't count toward a round ending.
    /// </summary>
    public bool IsAdrift(EntityUid uid)
    {
        return !_mobState.IsDead(uid) && _zeroG.IsOverZeroGSpace(uid);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextCheck)
            return;

        _nextCheck = now + CheckInterval;
        _changed.Clear();

        var query = EntityQueryEnumerator<MobStateComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            var adrift = IsAdrift(uid);
            if (adrift == _adrift.Contains(uid))
                continue;

            if (adrift)
                _adrift.Add(uid);
            else
                _adrift.Remove(uid);

            _changed.Add(uid);
        }

        // Mobs that were adrift and are gone now (deleted, gibbed) no longer hold anything up either.
        _adrift.RemoveWhere(uid => TerminatingOrDeleted(uid));

        foreach (var uid in _changed)
        {
            var ev = new CMUAdriftChangedEvent(uid, _adrift.Contains(uid));
            RaiseLocalEvent(ref ev);
        }
    }
}

/// <summary>
///     Raised broadcast when a mob drifts off into space outside a zero-g ship, or makes it back aboard.
/// </summary>
[ByRefEvent]
public readonly record struct CMUAdriftChangedEvent(EntityUid Mob, bool Adrift);
