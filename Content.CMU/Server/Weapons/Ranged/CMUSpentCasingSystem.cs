using Content.Shared.CMU14.Weapons.Ranged;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Weapons.Ranged;

/// <summary>
/// Periodic sweeper for ejected spent casings: strips their physics a few seconds
/// after landing (they become free-floating decals) and despawns them after
/// <see cref="CCVars.SpentCasingDespawnTime"/>. One shared sweep instead of
/// per-entity timers; contained casings (held, stored) are left alone.
/// </summary>
public sealed class CMUSpentCasingSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(5);

    /// <summary>Delay before the physics strip, so the ejection throw always finishes first.</summary>
    private static readonly TimeSpan StripDelay = TimeSpan.FromSeconds(3);

    private TimeSpan _nextSweep;

    public override void Initialize() => _nextSweep = _timing.CurTime + SweepInterval;

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextSweep)
            return;

        _nextSweep += SweepInterval;

        var despawn = TimeSpan.FromSeconds(_cfg.GetCVar(CCVars.SpentCasingDespawnTime));
        if (despawn <= TimeSpan.Zero)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUSpentCasingComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_containers.IsEntityInContainer(uid))
                continue;

            var age = now - comp.EjectedAt;
            if (age >= despawn)
                QueueDel(uid);
            else if (age >= StripDelay && !HasComp<CMUZFallingComponent>(uid))
                RemComp<PhysicsComponent>(uid);
        }
    }
}
