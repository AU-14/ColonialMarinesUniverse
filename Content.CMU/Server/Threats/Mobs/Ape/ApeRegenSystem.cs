using Content.Shared._RMC14.Damage;
using Content.Shared.CMU14.Threats.Mobs.Ape;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Threats.Mobs.Ape;

/// <summary>
///     Passive health regeneration for apes: 10% of max health per minute while alive.
///     Runs server-side only. The ape deliberately runs without the satiation system,
///     so healing no longer depends on food.
/// </summary>
public sealed partial class ApeRegenSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mob = default!;
    [Dependency] private MobThresholdSystem _mobThreshold = default!;
    [Dependency] private SharedRMCDamageableSystem _rmcDamageable = default!;
    [Dependency] private IGameTiming _timing = default!;
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    private const float HealFractionPerSecond = 0.1f / 60f;

    private TimeSpan _nextUpdate = TimeSpan.Zero;

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + UpdateInterval;

        var query = EntityQueryEnumerator<ApeComponent, DamageableComponent>();
        while (query.MoveNext(out EntityUid uid, out _, out DamageableComponent? damageable))
        {
            if (_mob.IsDead(uid))
                continue;

            if (!_mobThreshold.TryGetDeadThreshold(uid, out FixedPoint2? deadThreshold) ||
                deadThreshold == FixedPoint2.Zero)
                continue;

            FixedPoint2 maxHp = deadThreshold.Value;

            var healAmount = FixedPoint2.New(maxHp.Float() * HealFractionPerSecond);

            if (healAmount <= FixedPoint2.Zero)
                continue;

            DamageSpecifier healSpec = _rmcDamageable.DistributeTypes((uid, damageable), -healAmount);
            _damageable.TryChangeDamage(uid, healSpec, true, false);
        }
    }
}
