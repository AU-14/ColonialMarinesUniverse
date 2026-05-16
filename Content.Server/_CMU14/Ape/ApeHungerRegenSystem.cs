using Content.Shared._CMU14.Ape;
using Content.Shared.Damage;
// using Content.Shared.Damage.Components; // unused
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Content.Shared._RMC14.Damage;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Ape;

/// <summary>
/// Grants passive health regeneration to apes when they are sufficiently fed.
/// Heals 10% of max health per minute while hunger is >= 50%.
/// Runs server-side only.
/// </summary>
public sealed partial class ApeHungerRegenSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private HungerSystem _hunger = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mob = default!;
    [Dependency] private MobThresholdSystem _mobThreshold = default!;
    [Dependency] private SharedRMCDamageableSystem _rmcDamageable = default!;

    // Heal rate: 10% of max health per minute -> 0.1 per 60s
    private const float HealFractionPerSecond = 0.1f / 60f;

    // Minimum hunger fraction required to trigger regen (50% -> use hunger thresholds mapping)
    private const float MinimumHungerFraction = 0.5f;

    private TimeSpan _nextUpdate = TimeSpan.Zero;
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + UpdateInterval;

        var query = EntityQueryEnumerator<ApeDestroyComponent, HungerComponent>();
        while (query.MoveNext(out var uid, out var _, out var hungerComp))
        {
            // Only run on alive mobs
            if (_mob.IsDead(uid))
                continue;

            // Check hunger value as fraction of maximum hunger (Overfed threshold)
            var currentHunger = _hunger.GetHunger(hungerComp);
            var maxHunger = hungerComp.Thresholds[HungerThreshold.Overfed];
            if (maxHunger <= 0)
                continue;

            var hungerFraction = currentHunger / maxHunger;
            if (hungerFraction < MinimumHungerFraction)
                continue;

            // Determine max HP using the mob dead threshold from MobThresholdSystem.
            if (!_mobThreshold.TryGetDeadThreshold(uid, out var deadThreshold) || deadThreshold == FixedPoint2.Zero)
                continue;

            var maxHp = deadThreshold.Value;
            if (maxHp <= FixedPoint2.Zero)
                continue;

            // Heal amount per second as FixedPoint2
            var healAmount = FixedPoint2.New(maxHp.Float() * HealFractionPerSecond);
            if (healAmount <= FixedPoint2.Zero)
                continue;

            if (!TryComp<DamageableComponent>(uid, out var damageable))
                continue;

            // Distribute healing across damage types (expects negative amount for healing)
            var healSpec = _rmcDamageable.DistributeTypes((uid, damageable), -healAmount);
            _damageable.TryChangeDamage(uid, healSpec, true, false);
        }
    }
}



