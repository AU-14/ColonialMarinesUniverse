using System.Collections.Generic;
using Content.Shared._CMU14.Medical.BodyPart.Events;
using Content.Shared._CMU14.Medical.Wounds;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.BodyPart;

public abstract class SharedBodyPartHealthSystem : EntitySystem
{
    [Dependency] protected readonly IConfigurationManager Cfg = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly INetManager Net = default!;
    [Dependency] protected readonly SharedBodySystem Body = default!;
    [Dependency] protected readonly SharedHitLocationSystem HitLocation = default!;
    [Dependency] protected readonly RMCUnrevivableSystem Unrevivable = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private const float HealScanInterval = 1f;
    private static readonly ProtoId<DamageGroupPrototype> BruteGroup = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> BurnGroup = "Burn";

    private float _healScanAccumulator;

    private bool _medicalEnabled;
    private bool _bodyPartEnabled;
    private float _bodyPartDamagePropagation;
    private bool _severanceHeadDisabled;
    private bool _severanceTorsoDisabled;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HitLocationComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<BodyPartHealthComponent, ComponentStartup>(OnPartStartup);

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.BodyPartEnabled, v => _bodyPartEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.BodyPartDamagePropagation, v => _bodyPartDamagePropagation = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.SeveranceHeadDisabled, v => _severanceHeadDisabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.SeveranceTorsoDisabled, v => _severanceTorsoDisabled = v, true);
    }

    private void OnPartStartup(Entity<BodyPartHealthComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.NextHealTick = Timing.CurTime + ent.Comp.HealInterval;
    }

    private void OnDamageChanged(Entity<HitLocationComponent> ent, ref DamageChangedEvent args)
    {
        if (!_medicalEnabled || !_bodyPartEnabled)
            return;

        if (args.DamageDelta is not { } delta)
            return;

        var positive = DamageSpecifier.GetPositive(delta);
        var localizable = ExtractLocalizableDamage(positive);
        if (!localizable.Empty)
            ApplyPartDamage(ent, localizable);

        var healing = GetHealingInGroup(delta, BruteGroup) + GetHealingInGroup(delta, BurnGroup);
        if (healing > FixedPoint2.Zero)
            HealDamagedParts(ent.Owner, healing * (FixedPoint2)_bodyPartDamagePropagation);
    }

    private void ApplyPartDamage(Entity<HitLocationComponent> ent, DamageSpecifier damage)
    {
        // No mob-state gate: dead bodies still take new wounds, fractures, organ
        // damage, and severance from external hits (overkill, desecration). The
        // rotting-pipeline perf concern that justified an earlier dead-skip
        // doesn't apply here since this codebase has no rotting damage source.
        if (!HitLocation.TryConsumePendingHit(ent.Owner, out var resolved))
            return;

        if (resolved.ResolvedPartEntity is not { } partUid)
            return;

        TryApplyPartDamage(ent.Owner, partUid, damage);
    }

    public bool TryApplyPartDamage(EntityUid body, EntityUid partUid, DamageSpecifier damage, float scale = 1f)
    {
        if (!_medicalEnabled || !_bodyPartEnabled)
            return false;

        if (scale <= 0f)
            return false;

        var localizable = ExtractLocalizableDamage(DamageSpecifier.GetPositive(damage));
        if (localizable.Empty)
            return false;

        if (scale != 1f)
            localizable *= scale;

        return TryApplyPartDamageToPart(body, partUid, localizable);
    }

    private bool TryApplyPartDamageToPart(EntityUid body, EntityUid partUid, DamageSpecifier damage)
    {
        if (!TryComp<BodyPartHealthComponent>(partUid, out var health))
            return false;

        var modified = ApplyResistance(damage, health.Resistance);
        var total = (float)modified.GetTotal();
        if (total <= 0)
            return false;

        var deduction = FixedPoint2.New(total * _bodyPartDamagePropagation);

        health.Current -= deduction;
        Dirty(partUid, health);

        var organs = CollectOrgans(partUid);
        var partType = TryComp<BodyPartComponent>(partUid, out var partComp) ? partComp.PartType : BodyPartType.Other;
        var damaged = new BodyPartDamagedEvent(body, partUid, partType, modified, health.Current, organs);
        RaiseLocalEvent(partUid, ref damaged);

        if (health.Current <= -health.SeveranceThreshold && !IsSeveranceLocked(partType))
        {
            var severed = new BodyPartSeveredEvent(body, partUid, partType);
            RaiseLocalEvent(partUid, ref severed);
        }

        return true;
    }

    private DamageSpecifier ExtractLocalizableDamage(DamageSpecifier damage)
    {
        var result = new DamageSpecifier();
        AddPositiveGroupDamage(result, damage, BruteGroup);
        AddPositiveGroupDamage(result, damage, BurnGroup);
        return result;
    }

    private void AddPositiveGroupDamage(DamageSpecifier dest, DamageSpecifier src, ProtoId<DamageGroupPrototype> groupId)
    {
        if (!_prototypes.TryIndex(groupId, out var group))
            return;

        foreach (var type in group.DamageTypes)
        {
            if (src.DamageDict.TryGetValue(type, out var amount) && amount > FixedPoint2.Zero)
                dest.DamageDict[type] = amount;
        }
    }

    private FixedPoint2 GetHealingInGroup(DamageSpecifier delta, ProtoId<DamageGroupPrototype> groupId)
    {
        if (!_prototypes.TryIndex(groupId, out var group))
            return FixedPoint2.Zero;

        var total = FixedPoint2.Zero;
        foreach (var type in group.DamageTypes)
        {
            if (!delta.DamageDict.TryGetValue(type, out var amount) || amount >= FixedPoint2.Zero)
                continue;

            total -= amount;
        }

        return total;
    }

    private void HealDamagedParts(EntityUid body, FixedPoint2 amount)
    {
        if (amount <= FixedPoint2.Zero)
            return;

        var damaged = new List<(EntityUid Uid, BodyPartComponent Part, BodyPartHealthComponent Health)>();
        foreach (var (partUid, part) in Body.GetBodyChildren(body))
        {
            if (!TryComp<BodyPartHealthComponent>(partUid, out var health))
                continue;

            if (health.Current >= health.Max)
                continue;

            damaged.Add((partUid, part, health));
        }

        damaged.Sort(static (a, b) =>
            (b.Health.Max - b.Health.Current).CompareTo(a.Health.Max - a.Health.Current));

        var remaining = amount;
        foreach (var (partUid, part, health) in damaged)
        {
            if (remaining <= FixedPoint2.Zero)
                break;

            var missing = health.Max - health.Current;
            if (missing <= FixedPoint2.Zero)
                continue;

            var prev = health.Current;
            var healed = FixedPoint2.Min(missing, remaining);
            var next = prev + healed;

            health.Current = next;
            Dirty(partUid, health);
            RaiseHealedThresholdEvent(body, partUid, part.PartType, health, prev, next);

            remaining -= healed;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (Net.IsClient)
            return;

        if (!_medicalEnabled || !_bodyPartEnabled)
            return;

        _healScanAccumulator += frameTime;
        if (_healScanAccumulator < HealScanInterval)
            return;
        _healScanAccumulator = 0f;

        var now = Timing.CurTime;
        var query = EntityQueryEnumerator<BodyPartHealthComponent, BodyPartComponent>();
        while (query.MoveNext(out var uid, out var health, out var part))
        {
            if (health.NextHealTick > now)
                continue;

            health.NextHealTick = now + health.HealInterval;

            if (part.Body is not { } body || Unrevivable.IsUnrevivable(body))
                continue;

            if (health.Current >= health.Max)
                continue;

            if (health.PassiveHealMultiplier <= 0)
                continue;

            // Hand-rolled HasComp by name to avoid a forward reference to the wounds layer.
            if (health.BlockedByOpenWound && HasOpenWound(uid))
                continue;

            var prev = health.Current;
            var next = FixedPoint2.Min(health.Max, prev + (FixedPoint2)health.PassiveHealMultiplier);
            if (next == prev)
                continue;

            health.Current = next;
            Dirty(uid, health);

            RaiseHealedThresholdEvent(part.Body, uid, part.PartType, health, prev, next);
        }
    }

    private const float HealedThresholdFraction = 0.10f;
    private static readonly float[] PainThresholdFractions = { 0.10f, 0.25f };

    private void RaiseHealedThresholdEvent(
        EntityUid? body,
        EntityUid part,
        BodyPartType type,
        BodyPartHealthComponent health,
        FixedPoint2 prev,
        FixedPoint2 next)
    {
        if (body is not { } bodyUid || health.Max <= FixedPoint2.Zero)
            return;

        var maxFloat = health.Max.Float();
        var prevFraction = prev.Float() / maxFloat;
        var nextFraction = next.Float() / maxFloat;
        RaisePainThresholdEvents(bodyUid, part, type, prevFraction, nextFraction);

        // Raise BodyPartHealedEvent on the upward edge through 10% of Max so
        // semi-permanent injury triggers don't spam at every regen step.
        if (prevFraction >= HealedThresholdFraction || nextFraction < HealedThresholdFraction)
            return;

        var healed = new BodyPartHealedEvent(bodyUid, part, type, prevFraction, nextFraction, HealedThresholdFraction);
        RaiseLocalEvent(part, ref healed);
    }

    private void RaisePainThresholdEvents(
        EntityUid body,
        EntityUid part,
        BodyPartType type,
        float prevFraction,
        float nextFraction)
    {
        foreach (var threshold in PainThresholdFractions)
        {
            var wasBelow = prevFraction < threshold;
            var isBelow = nextFraction < threshold;
            if (wasBelow == isBelow)
                continue;

            var ev = new BodyPartPainThresholdCrossedEvent(body, part, type, prevFraction, nextFraction, threshold);
            RaiseLocalEvent(part, ref ev);
        }
    }

    /// <summary>
    ///     An eschar from a third-degree burn blocks passive heal — the part is
    ///     closed-skin but biologically dead until debrided.
    /// </summary>
    protected virtual bool HasOpenWound(EntityUid partUid)
        => HasComp<BodyPartWoundComponent>(partUid) || HasComp<CMUEscharComponent>(partUid);

    private bool IsSeveranceLocked(BodyPartType type) => type switch
    {
        BodyPartType.Head => _severanceHeadDisabled,
        BodyPartType.Torso => _severanceTorsoDisabled,
        _ => false,
    };

    private DamageSpecifier ApplyResistance(DamageSpecifier d, Dictionary<Robust.Shared.Prototypes.ProtoId<Content.Shared.Damage.Prototypes.DamageGroupPrototype>, float> resistance)
    {
        if (resistance.Count == 0)
            return d;

        var coefficients = new Dictionary<string, float>(resistance.Count);
        foreach (var (group, multiplier) in resistance)
            coefficients[group.Id] = multiplier;

        var modifier = new DamageModifierSet
        {
            Coefficients = coefficients,
        };
        return DamageSpecifier.ApplyModifierSet(d, modifier);
    }

    private IReadOnlyList<EntityUid> CollectOrgans(EntityUid partUid)
    {
        List<EntityUid>? list = null;
        foreach (var organ in Body.GetPartOrgans(partUid))
        {
            list ??= new List<EntityUid>();
            list.Add(organ.Id);
        }
        if (list is null)
            return System.Array.Empty<EntityUid>();

        return list;
    }

    /// <summary>
    ///     Direct assignment bypasses the heal tick. Used by reattach surgery.
    /// </summary>
    public void SetCurrent(Entity<BodyPartHealthComponent?> part, FixedPoint2 newCurrent)
    {
        if (!Resolve(part.Owner, ref part.Comp, logMissing: false))
            return;
        if (newCurrent > part.Comp.Max)
            newCurrent = part.Comp.Max;
        var prev = part.Comp.Current;
        part.Comp.Current = newCurrent;
        Dirty(part.Owner, part.Comp);

        if (part.Comp.Max <= FixedPoint2.Zero)
            return;
        if (!TryComp<BodyPartComponent>(part.Owner, out var partBody) || partBody.Body is not { } body)
            return;

        var prevFraction = prev.Float() / part.Comp.Max.Float();
        var nextFraction = newCurrent.Float() / part.Comp.Max.Float();
        RaisePainThresholdEvents(body, part.Owner, partBody.PartType, prevFraction, nextFraction);
    }
}
