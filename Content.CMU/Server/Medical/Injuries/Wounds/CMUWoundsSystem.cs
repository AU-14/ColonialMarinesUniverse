using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Medical.Injuries.Wounds;

public sealed partial class CMUWoundsSystem : SharedCMUWoundsSystem
{
    [Dependency] private SharedRMCDamageableSystem _rmcDamageable = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private CMUWoundLedgerSystem _woundLedger = default!;

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
            _bloodstream.TryModifyBloodLevel((body, bloodstream), FixedPoint2.New(-(rate * tickSeconds)));
    }

    private void DrainBlood(EntityUid body, float amount)
    {
        if (!TryComp<BloodstreamComponent>(body, out var bloodstream))
            return;

        if (!_solutions.ResolveSolution(body, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out var bloodSolution))
            return;

        var drain = FixedPoint2.Min((FixedPoint2) amount, bloodSolution.Volume);
        if (drain <= FixedPoint2.Zero)
            return;

        var removed = bloodSolution.RemoveReagent(bloodstream.BloodReagent, drain, ignoreReagentData: true);
        if (removed > FixedPoint2.Zero)
            _solutions.UpdateChemicals(bloodstream.BloodSolution.Value);
    }

    protected override void ApplyWoundHealingDamage(EntityUid body, EntityUid part, WoundType type, FixedPoint2 amount)
    {
        if (amount <= FixedPoint2.Zero)
            return;

        switch (type)
        {
            case WoundType.Brute:
                ApplyWoundHealingDamage(body, part, BruteGroup, amount);
                break;
            case WoundType.Burn:
                ApplyWoundHealingDamage(body, part, BurnGroup, amount);
                break;
        }
    }

    private void ApplyWoundHealingDamage(
        EntityUid body,
        EntityUid part,
        ProtoId<DamageGroupPrototype> group,
        FixedPoint2 amount)
    {
        if (!TryComp<DamageableComponent>(body, out var damageable))
            return;

        var spec = _rmcDamageable.DistributeHealing((body, damageable), group, amount);
        Damageable.TryChangeDamage(body,
            spec,
            ignoreResistances: true,
            interruptsDoAfters: false,
            damageable: damageable,
            origin: part);
    }

    protected override void OnPartWoundsCleared(EntityUid body, EntityUid part)
    {
        if (TryComp<BodyPartHealthComponent>(part, out var health))
            PartHealth.SetCurrent((part, health), health.Max);

        if (!HasRemainingWounds(body, WoundType.Brute))
            HealRemainingDamageGroup(body, part, BruteGroup);
        if (!HasRemainingWounds(body, WoundType.Burn))
            HealRemainingDamageGroup(body, part, BurnGroup);
    }

    private bool HasRemainingWounds(EntityUid body, WoundType type)
    {
        return _woundLedger.BodyHasWoundOfType(body, type);
    }

    private void HealRemainingDamageGroup(EntityUid body, EntityUid part, ProtoId<DamageGroupPrototype> group)
    {
        if (!TryComp<DamageableComponent>(body, out var damageable))
            return;
        if (!Proto.TryIndex(group, out var groupProto))
            return;
        if (!damageable.Damage.TryGetDamageInGroup(groupProto, out var amount) || amount <= FixedPoint2.Zero)
            return;

        ApplyWoundHealingDamage(body, part, group, amount);
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
