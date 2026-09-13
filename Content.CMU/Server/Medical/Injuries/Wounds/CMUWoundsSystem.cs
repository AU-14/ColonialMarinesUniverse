using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Medical.Injuries.Wounds;

public sealed partial class CMUWoundsSystem : SharedCMUWoundsSystem
{
    [Dependency] private SharedRMCDamageableSystem _rmcDamageable = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;

    private static readonly ProtoId<DamageGroupPrototype> BruteGroup = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> BurnGroup = "Burn";

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateServer(frameTime);
    }

    protected override void ApplyInternalBleed(EntityUid body, EntityUid part, float amount)
    {
        if (amount <= 0f)
            return;

        DrainBlood(body, amount);
    }

    protected override void ApplyExternalBleed(EntityUid body, EntityUid part, ExternalBleedTier tier, float tickSeconds)
    {
        var rate = tier switch
        {
            ExternalBleedTier.Minor => 0.08f,
            ExternalBleedTier.Moderate => 0.18f,
            ExternalBleedTier.Severe => 0.35f,
            ExternalBleedTier.Arterial => 0.70f,
            _ => 0f,
        };

        if (rate <= 0f || tickSeconds <= 0f)
            return;

        if (TryComp<BloodstreamComponent>(body, out var bloodstream))
            _bloodstream.TryBleedOut((body, bloodstream), FixedPoint2.New(rate * tickSeconds));
    }

    private void DrainBlood(EntityUid body, float amount)
    {
        if (TryComp<BloodstreamComponent>(body, out var bloodstream))
            _bloodstream.TryRegulateBloodLevel((body, bloodstream), (FixedPoint2) amount, referenceFactor: 0f);
    }

    protected override void ApplyWoundHealingDamage(EntityUid body, EntityUid part, WoundType type, FixedPoint2 amount,
        FixedPoint2 remainingWoundDamage)
    {
        if (amount <= FixedPoint2.Zero)
            return;

        switch (type)
        {
            case WoundType.Brute:
                PartHealth.HealPartWoundDamage(body, part, BruteGroup, amount, remainingWoundDamage);
                break;
            case WoundType.Burn:
                PartHealth.HealPartWoundDamage(body, part, BurnGroup, amount, remainingWoundDamage);
                break;
        }
    }

    public bool TryApplyTreaterDamage(
        EntityUid body,
        EntityUid user,
        EntityUid tool,
        ProtoId<DamageGroupPrototype> group,
        FixedPoint2 damage,
        EntityUid? origin = null,
        FixedPoint2? partHealthCap = null,
        bool useLargestWoundCap = false)
    {
        if (damage == FixedPoint2.Zero)
            return false;

        damage = LimitHealingToWoundCap(damage, origin, partHealthCap, useLargestWoundCap);
        if (damage == FixedPoint2.Zero)
            return false;

        if (damage < FixedPoint2.Zero && origin is { } selectedPart)
        {
            var healed = PartHealth.HealPartDamage(body, selectedPart, group, -damage);
            if (healed > FixedPoint2.Zero)
                ClampTreaterPartHealth(origin, partHealthCap, useLargestWoundCap);
            return healed > FixedPoint2.Zero;
        }

        if (!TryComp<DamageableComponent>(body, out var damageable))
            return false;

        var spec = _rmcDamageable.DistributeDamageCached((body, damageable), group, damage);
        if (spec.Empty)
            return false;

        var changed = Damageable.TryChangeDamage(body,
            spec,
            ignoreResistances: true,
            interruptsDoAfters: false,
            damageable: damageable,
            origin: origin ?? user,
            tool: tool) is not null;

        if (changed)
            ClampTreaterPartHealth(origin, partHealthCap, useLargestWoundCap);

        return changed;
    }

    private FixedPoint2 LimitHealingToWoundCap(
        FixedPoint2 damage,
        EntityUid? origin,
        FixedPoint2? partHealthCap,
        bool useLargestWoundCap)
    {
        if (damage >= FixedPoint2.Zero || origin is not { } part)
            return damage;

        if (!TryComp<BodyPartHealthComponent>(part, out var health))
            return damage;

        var requestedHealing = -damage;
        var allowedHealing = requestedHealing;

        if (TryComp<BodyPartWoundComponent>(part, out var wounds))
        {
            var woundCapFraction = useLargestWoundCap
                ? ComputeLargestWoundFieldTreatmentCap(wounds)
                : ComputeFieldTreatmentCap(wounds);

            var cap = health.Max * (FixedPoint2) woundCapFraction;
            var room = cap - health.Current;
            if (room <= FixedPoint2.Zero)
                return FixedPoint2.Zero;

            allowedHealing = FixedPoint2.Min(allowedHealing, room);
        }

        if (partHealthCap is { } healthCap)
        {
            var room = healthCap - health.Current;
            if (room <= FixedPoint2.Zero)
                return FixedPoint2.Zero;

            allowedHealing = FixedPoint2.Min(allowedHealing, room);
        }

        return -allowedHealing;
    }

    private void ClampTreaterPartHealth(EntityUid? origin, FixedPoint2? partHealthCap, bool useLargestWoundCap)
    {
        if (origin is not { } part || !TryComp<BodyPartHealthComponent>(part, out var health))
            return;

        FixedPoint2? cap = null;
        if (TryComp<BodyPartWoundComponent>(part, out var wounds))
        {
            var woundCapFraction = useLargestWoundCap
                ? ComputeLargestWoundFieldTreatmentCap(wounds)
                : ComputeFieldTreatmentCap(wounds);

            cap = health.Max * (FixedPoint2) woundCapFraction;
        }

        if (partHealthCap is { } healthCap)
            cap = cap is { } woundCap ? FixedPoint2.Min(woundCap, healthCap) : healthCap;

        if (cap is not { } finalCap || health.Current <= finalCap)
            return;

        PartHealth.SetCurrent((part, health), finalCap);
    }
}
