using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds;
using Content.Server.Destructible.Thresholds.Behaviors;
using Content.Server.Destructible.Thresholds.Triggers;
using Content.Shared._CMU14.Destruction;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Destruction;

/// <summary>
/// Converts an obstruction's remaining effective durability into impact-speed
/// cost. Dropships and ground vehicles share this so both preserve only the
/// momentum left after doing the damage actually needed to clear an obstacle.
/// </summary>
public sealed partial class DestructionMomentumSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DestructionMomentumQueryEvent>(OnMomentumQuery);
    }

    private void OnMomentumQuery(ref DestructionMomentumQueryEvent args)
    {
        args.HasRemovalThreshold = TryGetRemovalThreshold(args.Target, out var damageable, out var remainingDamage);
        if (!args.HasRemovalThreshold)
            return;

        if (remainingDamage <= 0f)
        {
            args.CanDestroy = true;
            return;
        }

        if (args.AvailableSpeed <= 0f || args.DamageMultiplier <= 0f)
            return;

        if (GetEffectiveDamage(damageable,
                args.AvailableSpeed * args.AvailableSpeed * args.DamageMultiplier) < remainingDamage)
        {
            return;
        }

        var low = 0f;
        var high = args.AvailableSpeed;
        for (var i = 0; i < 12; i++)
        {
            var middle = (low + high) * 0.5f;
            var rawDamage = middle * middle * args.DamageMultiplier;
            if (GetEffectiveDamage(damageable, rawDamage) >= remainingDamage)
                high = middle;
            else
                low = middle;
        }

        args.CanDestroy = true;
        args.RequiredSpeed = high;
    }

    public bool TryGetBreakCost(
        EntityUid obstruction,
        float availableSpeed,
        float damageMultiplier,
        out float requiredSpeed)
    {
        var query = new DestructionMomentumQueryEvent(obstruction, availableSpeed, damageMultiplier);
        OnMomentumQuery(ref query);
        requiredSpeed = query.RequiredSpeed;
        return query.CanDestroy;
    }

    /// <summary>
    /// Resolves the physical cost of an obstruction independently of the
    /// caller's current budget. This is used to batch simultaneous contacts.
    /// </summary>
    public bool TryGetRequiredBreakSpeed(
        EntityUid obstruction,
        float damageMultiplier,
        out float requiredSpeed)
    {
        requiredSpeed = 0f;
        if (damageMultiplier <= 0f ||
            !TryGetRemovalThreshold(obstruction, out var damageable, out var remainingDamage))
        {
            return false;
        }

        if (remainingDamage <= 0f)
            return true;

        var low = 0f;
        var high = 1f;
        while (high < 1_000_000f &&
               GetEffectiveDamage(damageable, high * high * damageMultiplier) < remainingDamage)
        {
            low = high;
            high *= 2f;
        }

        if (GetEffectiveDamage(damageable, high * high * damageMultiplier) < remainingDamage)
            return false;

        for (var i = 0; i < 20; i++)
        {
            var middle = (low + high) * 0.5f;
            if (GetEffectiveDamage(damageable, middle * middle * damageMultiplier) >= remainingDamage)
                high = middle;
            else
                low = middle;
        }

        requiredSpeed = high;
        return true;
    }

    private bool TryGetRemovalThreshold(
        EntityUid obstruction,
        out DamageableComponent damageable,
        out float remainingDamage)
    {
        remainingDamage = 0f;
        if (!TryComp(obstruction, out damageable!) ||
            !TryComp(obstruction, out DestructibleComponent? destructible))
        {
            return false;
        }

        var destroyedAt = GetRemovalThreshold(destructible);
        if (destroyedAt == FixedPoint2.MaxValue)
            return false;

        remainingDamage = destroyedAt.Float() - damageable.TotalDamage.Float();
        return true;
    }

    /// <summary>
    /// Prefer actual destruction to breakage. RMC walls commonly break into a
    /// still-solid girder before their later destruction threshold.
    /// </summary>
    private static FixedPoint2 GetRemovalThreshold(DestructibleComponent destructible)
    {
        var destructionAt = FixedPoint2.MaxValue;
        var breakageAt = FixedPoint2.MaxValue;

        foreach (var threshold in destructible.Thresholds)
        {
            if (threshold.Trigger is not DamageTrigger trigger)
                continue;

            foreach (var behavior in threshold.Behaviors)
            {
                if (behavior is not DoActsBehavior acts)
                    continue;

                if (acts.HasAct(ThresholdActs.Destruction))
                    destructionAt = FixedPoint2.Min(destructionAt, FixedPoint2.New(trigger.Damage));
                else if (acts.HasAct(ThresholdActs.Breakage))
                    breakageAt = FixedPoint2.Min(breakageAt, FixedPoint2.New(trigger.Damage));
            }
        }

        return destructionAt != FixedPoint2.MaxValue ? destructionAt : breakageAt;
    }

    private float GetEffectiveDamage(DamageableComponent damageable, float rawDamage)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict["Blunt"] = FixedPoint2.New(rawDamage);
        if (damageable.DamageModifierSetId != null &&
            _prototypes.TryIndex<DamageModifierSetPrototype>(damageable.DamageModifierSetId, out var modifierSet))
        {
            damage = DamageSpecifier.ApplyModifierSet(damage, modifierSet);
        }

        damage = _damageable.ApplyUniversalAllModifiers(damage);
        return MathF.Max(0f, damage.GetTotal().Float());
    }
}
