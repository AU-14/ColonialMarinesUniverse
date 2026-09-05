using System;
using System.Collections.Generic;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Trauma;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Medical.Stasis;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared._RMC14.Synth;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Medical.Anatomy.BodyParts;

public abstract partial class SharedBodyPartHealthSystem : EntitySystem
{
    [Dependency] protected IConfigurationManager Cfg = default!;
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected SharedHitLocationSystem HitLocation = default!;
    [Dependency] protected CMUMedicalBodyIndexSystem MedicalIndex = default!;
    [Dependency] protected SharedCMUTraumaSystem Trauma = default!;
    [Dependency] protected RMCUnrevivableSystem Unrevivable = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private CMUWoundLedgerSystem _woundLedger = default!;

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
        // Read the state that existed before this hit so a catastrophic hit against a living target remains possible.
        SubscribeLocalEvent<HitLocationComponent, DamageChangedEvent>(OnDamageChanged, before: [typeof(MobThresholdSystem)]);
        SubscribeLocalEvent<HitLocationComponent, BodyPartAddedEvent>(OnBodyPartAdded);
        SubscribeLocalEvent<HitLocationComponent, BodyPartRemovedEvent>(OnBodyPartRemoved);
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
        if (args.BodyDamageOnly)
            return;
        if (args.DamageDelta is null || args.DamageDelta.Empty)
        {
            // Explicit aggregate overwrites can lower individual types without an applied delta.
            // Clamp attribution now so later regional repair cannot spend obsolete debt on a new injury.
            var available = _damageable.GetAllDamage(ent.Owner);
            foreach (var (part, _) in MedicalIndex.GetBodyParts(ent.Owner))
            {
                if (!TryComp<BodyPartHealthComponent>(part, out var health))
                    continue;
                foreach (var (type, amount) in health.BodyDamage.DamageDict)
                {
                    var balance = available.DamageDict.GetValueOrDefault(type);
                    var retained = FixedPoint2.Min(balance, amount);
                    health.BodyDamage.DamageDict[type] = retained;
                    available.DamageDict[type] = balance - retained;
                }
            }
        }
        if (!ShouldProcessDamageChanged(_medicalEnabled, _bodyPartEnabled, Timing.ApplyingState, args.DamageDelta))
            return;

        var delta = args.DamageDelta!;
        var positive = DamageSpecifier.GetPositive(delta);
        var localizable = ExtractLocalizableDamage(positive);
        if (!localizable.Empty)
            ApplyPartDamage(ent, localizable, args);

        ApplyAttributedHealing(ent.Owner, delta);
    }

    private static bool ShouldProcessDamageChanged(
        bool medicalEnabled,
        bool bodyPartEnabled,
        bool applyingState,
        DamageSpecifier? damageDelta)
    {
        return medicalEnabled &&
            bodyPartEnabled &&
            !applyingState &&
            damageDelta is not null;
    }

    private void ApplyPartDamage(Entity<HitLocationComponent> ent, DamageSpecifier damage, DamageChangedEvent args)
    {
        // No mob-state gate: dead bodies still take new wounds, fractures, organ
        // damage, and severance from external hits (overkill, desecration). The
        // rotting-pipeline perf concern that justified an earlier dead-skip
        // doesn't apply here since this codebase has no rotting damage source.
        if (args.TargetPartEntity is not { } partUid)
            return;

        TrackBodyDamage(ent.Owner, partUid, damage);
        TryApplyPartDamage(ent.Owner, partUid, damage, tool: args.Tool, origin: args.Origin, impact: args.Impact, targetZone: args.TargetZone);
    }

    /// <summary>Records the exact applied aggregate share before regional injury can trigger severance.</summary>
    public void TrackBodyDamage(EntityUid body, EntityUid part, DamageSpecifier damage)
    {
        if (TryComp<BodyPartComponent>(part, out var anatomy) && anatomy.Body == body &&
            TryComp<BodyPartHealthComponent>(part, out var health))
        {
            health.BodyDamage += ExtractLocalizableDamage(damage);
        }
    }

    public FixedPoint2 GetAttributedDamage(EntityUid part, ProtoId<DamageTypePrototype> type)
        => TryComp<BodyPartHealthComponent>(part, out var health)
            ? health.BodyDamage.DamageDict.GetValueOrDefault(type)
            : FixedPoint2.Zero;

    /// <summary>Returns this site's outstanding contribution to aggregate brute/burn damage.</summary>
    public FixedPoint2 GetOutstandingBodyDamage(EntityUid part)
        => TryComp<BodyPartHealthComponent>(part, out var health) ? health.BodyDamage.GetTotal() : FixedPoint2.Zero;

    /// <summary>Heals only the selected part's outstanding contribution of the requested group.</summary>
    public FixedPoint2 HealPartDamage(EntityUid body, EntityUid part, ProtoId<DamageGroupPrototype> group, FixedPoint2 amount, bool healPart = true)
    {
        if (amount <= FixedPoint2.Zero || !TryComp<BodyPartComponent>(part, out var anatomy) || anatomy.Body != body ||
            !TryComp<BodyPartHealthComponent>(part, out var health) || !_prototypes.TryIndex(group, out var prototype) ||
            !TryComp<DamageableComponent>(body, out var bodyDamage))
            return FixedPoint2.Zero;

        var remaining = amount;
        var delta = new DamageSpecifier();
        var aggregate = _damageable.GetAllDamage((body, bodyDamage));
        foreach (var type in prototype.DamageTypes)
        {
            var tracked = health.BodyDamage.DamageDict.GetValueOrDefault(type);
            var healed = FixedPoint2.Min(remaining, FixedPoint2.Min(tracked, aggregate.DamageDict.GetValueOrDefault(type)));
            if (healed <= FixedPoint2.Zero)
                continue;

            health.BodyDamage.DamageDict[type] = tracked - healed;
            delta.DamageDict[type] = -healed;
            remaining -= healed;
        }

        var total = amount - remaining;
        if (total <= FixedPoint2.Zero)
            return total;

        if (healPart)
            SetCurrent((part, health), health.Current + total * (FixedPoint2)_bodyPartDamagePropagation);
        _damageable.ApplyBodyDamageProjection(body, delta);
        return total;
    }

    /// <summary>
    /// Advances this site's pooled wound recovery. Wound burden is resistance-adjusted while
    /// aggregate attribution is not, so recovery spends the same fraction of the remaining group debt.
    /// Structural recovery remains possible after aggregate healing or for anatomy-only injuries.
    /// </summary>
    public FixedPoint2 HealPartWoundDamage(EntityUid body, EntityUid part, ProtoId<DamageGroupPrototype> group,
        FixedPoint2 amount, FixedPoint2 remainingWoundDamage)
    {
        if (amount <= FixedPoint2.Zero || remainingWoundDamage <= FixedPoint2.Zero ||
            !TryComp<BodyPartComponent>(part, out var anatomy) || anatomy.Body != body ||
            !TryComp<BodyPartHealthComponent>(part, out var health) || !_prototypes.TryIndex(group, out var prototype))
            return FixedPoint2.Zero;

        amount = FixedPoint2.Min(amount, remainingWoundDamage);
        var outstanding = FixedPoint2.Zero;
        foreach (var type in prototype.DamageTypes)
            outstanding += health.BodyDamage.DamageDict.GetValueOrDefault(type);

        // Consume the final fixed-point residual on the last step, without touching other sites or groups.
        var aggregateHealing = amount == remainingWoundDamage
            ? outstanding
            : FixedPoint2.Min(outstanding, FixedPoint2.New(outstanding.Float() * amount.Float() / remainingWoundDamage.Float()));
        var wounds = CompOrNull<BodyPartWoundComponent>(part);
        var woundRevision = wounds == null ? 0 : _woundLedger.GetRevision(wounds);
        var bodyDamage = CompOrNull<DamageableComponent>(body);
        var healed = HealPartDamage(body, part, group, aggregateHealing, healPart: false);
        // Aggregate observers may remove, transplant or replace this tissue. The
        // accepted aggregate change cannot authorize healing a different instance
        // or a new injury created after recovery reset the same tissue.
        if (TerminatingOrDeleted(body) || TerminatingOrDeleted(part) ||
            EntityManager.IsQueuedForDeletion(body) || EntityManager.IsQueuedForDeletion(part) ||
            !TryComp<BodyPartComponent>(part, out var currentAnatomy) || !ReferenceEquals(currentAnatomy, anatomy) ||
            currentAnatomy.Body != body || !TryComp<BodyPartHealthComponent>(part, out var currentHealth) ||
            !ReferenceEquals(currentHealth, health) ||
            !ReferenceEquals(CompOrNull<DamageableComponent>(body), bodyDamage) ||
            !ReferenceEquals(CompOrNull<BodyPartWoundComponent>(part), wounds) ||
            wounds != null && _woundLedger.GetRevision(wounds) != woundRevision)
            return healed;
        var structuralHealing = amount * (FixedPoint2)_bodyPartDamagePropagation;
        HealOneDamagedPart(body, part, anatomy, health, ref structuralHealing);
        return healed;
    }

    private void OnBodyPartRemoved(Entity<HitLocationComponent> body, ref BodyPartRemovedEvent args)
    {
        if (TerminatingOrDeleted(body) || !TryComp<BodyPartHealthComponent>(args.Part.Owner, out var health))
            return;

        // Retain the injury on the detached material so transplantation transfers the same debt.
        _damageable.ApplyBodyDamageProjection(body.Owner, -health.BodyDamage);
    }

    private void OnBodyPartAdded(Entity<HitLocationComponent> body, ref BodyPartAddedEvent args)
    {
        if (!TerminatingOrDeleted(body) && TryComp<BodyPartHealthComponent>(args.Part.Owner, out var health))
            _damageable.ApplyBodyDamageProjection(body.Owner, health.BodyDamage);
    }

    private void ApplyAttributedHealing(EntityUid body, DamageSpecifier delta)
    {
        _prototypes.TryIndex(BruteGroup, out var brute);
        _prototypes.TryIndex(BurnGroup, out var burn);
        foreach (var (type, amount) in delta.DamageDict)
        {
            if (amount >= FixedPoint2.Zero ||
                brute?.DamageTypes.Contains(type) != true && burn?.DamageTypes.Contains(type) != true)
                continue;

            var remaining = -amount;
            foreach (var (part, anatomy) in MedicalIndex.GetBodyParts(body))
            {
                if (remaining <= FixedPoint2.Zero)
                    break;
                if (HasComp<CMURoboticLimbComponent>(part) && !HasComp<SynthComponent>(body))
                    continue;
                if (!TryComp<BodyPartHealthComponent>(part, out var health))
                    continue;

                var tracked = health.BodyDamage.DamageDict.GetValueOrDefault(type);
                var healed = FixedPoint2.Min(tracked, remaining);
                if (healed <= FixedPoint2.Zero)
                    continue;

                health.BodyDamage.DamageDict[type] = tracked - healed;
                remaining -= healed;
                var regionalHealing = healed * (FixedPoint2)_bodyPartDamagePropagation;
                HealOneDamagedPart(body, part, anatomy, health, ref regionalHealing);
            }
        }
    }

    public bool TryApplyPartDamage(
        EntityUid body,
        EntityUid partUid,
        DamageSpecifier damage,
        float scale = 1f,
        EntityUid? tool = null,
        CMUTraumaMechanism? mechanism = null,
        EntityUid? origin = null,
        DamageImpact impact = default,
        TargetBodyZone? targetZone = null,
        MobState? stateAtImpact = null)
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

        return TryApplyPartDamageToPart(body, partUid, localizable, origin, tool, mechanism, impact, targetZone, stateAtImpact);
    }

    private bool TryApplyPartDamageToPart(
        EntityUid body,
        EntityUid partUid,
        DamageSpecifier damage,
        EntityUid? origin,
        EntityUid? tool,
        CMUTraumaMechanism? mechanism,
        DamageImpact impact,
        TargetBodyZone? targetZone,
        MobState? stateAtImpact)
    {
        if (!TryComp<BodyPartHealthComponent>(partUid, out var health) ||
            !TryComp<BodyPartComponent>(partUid, out var partComp) || partComp.Body != body)
            return false;

        var modified = ApplyResistance(damage, health.Resistance);
        var total = (float)modified.GetTotal();
        if (total <= 0)
            return false;

        var deduction = FixedPoint2.New(total * _bodyPartDamagePropagation);
        var partType = partComp.PartType;
        var canAccumulateSeverance = CanAutomaticallySever(body, partType, impact, stateAtImpact);
        var severanceDeduction = canAccumulateSeverance
            ? DamageImpactSeverance.Calculate(modified, impact) * (FixedPoint2)_bodyPartDamagePropagation
            : FixedPoint2.Zero;

        health.Current -= deduction;
        if (severanceDeduction > FixedPoint2.Zero)
            health.SeveranceDamage += severanceDeduction;
        Dirty(partUid, health);

        var organs = CollectOrgans(partUid);
        var trauma = Trauma.CreateContactResult(partType, modified, organs.Count > 0, origin, tool, impact, mechanism, targetZone);
        var damaged = new BodyPartDamagedEvent(body, partUid, partType, modified, health.Current, organs, tool, impact, trauma, targetZone);
        RaiseLocalEvent(partUid, ref damaged, broadcast: true);

        if (health.SeveranceDamage >= health.Max + health.SeveranceThreshold &&
            !IsSeveranceLocked(partType) &&
            CanAutomaticallySever(body, partType, impact, stateAtImpact))
        {
            var severed = new BodyPartSeverAttemptEvent(body, partUid, partType);
            RaiseLocalEvent(partUid, ref severed, broadcast: true);
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

    private void HealOneDamagedPart(
        EntityUid body,
        EntityUid partUid,
        BodyPartComponent part,
        BodyPartHealthComponent health,
        ref FixedPoint2 remaining)
    {
        if (remaining <= FixedPoint2.Zero)
            return;

        var missing = health.Max - health.Current;
        if (missing <= FixedPoint2.Zero)
            return;

        var prev = health.Current;
        var healed = FixedPoint2.Min(missing, remaining);
        var next = prev + healed;

        health.Current = next;
        health.SeveranceDamage = FixedPoint2.Max(FixedPoint2.Zero, health.SeveranceDamage - healed);
        Dirty(partUid, health);
        RaiseHealedThresholdEvent(body, partUid, part.PartType, health, prev, next);

        remaining -= healed;
    }

    protected void UpdateServer(float frameTime)
    {
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
            // Most parts do not opt into native recovery. Skip them before body queries.
            if (health.PassiveHealMultiplier <= 0 || health.Current >= health.Max || health.NextHealTick > now)
                continue;

            if (part.Body is not { } body || TerminatingOrDeleted(body) || TerminatingOrDeleted(uid) ||
                EntityManager.IsQueuedForDeletion(body) || EntityManager.IsQueuedForDeletion(uid) ||
                HasComp<CMInStasisComponent>(body) || MetaData(body).EntityPaused || Unrevivable.IsUnrevivable(body))
                continue;

            if (HasComp<CMURoboticLimbComponent>(uid))
                continue;

            health.NextHealTick = now + health.HealInterval;

            // Native structural recovery waits until the wound ledger has closed.
            if (health.BlockedByOpenWound && HasOpenWound(uid))
                continue;

            // Spend only the accepted HP quantum on severance recovery, just as
            // ordinary structural healing does. Aggregate attribution is separate.
            var healing = (FixedPoint2)health.PassiveHealMultiplier;
            HealOneDamagedPart(body, uid, part, health, ref healing);
        }
    }

    private const float HealedThresholdFraction = 0.10f;
    private static readonly float[] PainThresholdFractions = [0.10f, 0.25f];

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
        RaiseLocalEvent(part, ref healed, broadcast: true);
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
    ///     Passive repair waits on open wounds. Eschar remains visible and
    ///     surgical, but no longer blocks the simple field-treatment loop.
    /// </summary>
    protected virtual bool HasOpenWound(EntityUid partUid)
        => HasComp<BodyPartWoundComponent>(partUid);

    private bool IsSeveranceLocked(BodyPartType type) => type switch
    {
        BodyPartType.Head => _severanceHeadDisabled,
        BodyPartType.Torso => _severanceTorsoDisabled,
        _ => false,
    };

    private bool CanAutomaticallySever(EntityUid body, BodyPartType type, DamageImpact impact, MobState? stateAtImpact = null)
    {
        if (type != BodyPartType.Head)
            return true;

        var state = stateAtImpact ?? (TryComp<MobStateComponent>(body, out var mobState)
            ? mobState.CurrentState
            : (MobState?) null);
        return CanAutomaticallySeverHead(
            impact,
            state,
            HasComp<RMCRevivableComponent>(body),
            Unrevivable.IsUnrevivable(body));
    }

    /// <summary>
    /// Determines whether ordinary damage may contribute to or trigger head severance.
    /// Direct severance events used by surgery and explicit mechanics bypass this policy.
    /// </summary>
    public static bool CanAutomaticallySeverHead(
        DamageImpact impact,
        MobState? state,
        bool revivable,
        bool unrevivable)
    {
        if (impact.Delivery == DamageImpactDelivery.Projectile)
            return false;

        if (state == MobState.Critical)
            return false;

        return state != MobState.Dead || !revivable || unrevivable;
    }

    private DamageSpecifier ApplyResistance(DamageSpecifier d, Dictionary<ProtoId<DamageGroupPrototype>, float> resistance)
    {
        if (resistance.Count == 0)
            return d;

        var result = new DamageSpecifier();
        result.DamageDict.EnsureCapacity(d.DamageDict.Count);
        foreach (var (type, amount) in d.DamageDict)
        {
            if (amount == FixedPoint2.Zero)
                continue;

            if (amount < FixedPoint2.Zero)
            {
                result.DamageDict[type] = amount;
                continue;
            }

            var multiplier = 1f;
            foreach (var (groupId, groupMultiplier) in resistance)
            {
                if (!_prototypes.TryIndex(groupId, out var group)
                    || !group.DamageTypes.Contains(type))
                {
                    continue;
                }

                multiplier *= groupMultiplier;
            }

            var modified = FixedPoint2.New(amount.Float() * multiplier);
            if (modified != FixedPoint2.Zero)
                result.DamageDict[type] = modified;
        }

        return result;
    }

    private IReadOnlyList<EntityUid> CollectOrgans(EntityUid partUid)
    {
        List<EntityUid>? list = null;
        foreach (var organ in MedicalIndex.GetPartOrgans(partUid))
        {
            list ??= new List<EntityUid>();
            list.Add(organ.Owner);
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
        part.Comp.SeveranceDamage = FixedPoint2.Min(
            part.Comp.SeveranceDamage,
            FixedPoint2.Max(FixedPoint2.Zero, part.Comp.Max - newCurrent));
        Dirty(part.Owner, part.Comp);

        if (part.Comp.Max <= FixedPoint2.Zero)
            return;
        if (!TryComp<BodyPartComponent>(part.Owner, out var partBody) || partBody.Body is not { } body)
            return;

        var prevFraction = prev.Float() / part.Comp.Max.Float();
        var nextFraction = newCurrent.Float() / part.Comp.Max.Float();
        RaisePainThresholdEvents(body, part.Owner, partBody.PartType, prevFraction, nextFraction);
    }

    public void RestoreToFractionCap(Entity<BodyPartHealthComponent?> part, float capFraction)
    {
        if (!Resolve(part.Owner, ref part.Comp, logMissing: false))
            return;

        if (part.Comp.Max <= FixedPoint2.Zero)
            return;

        capFraction = Math.Clamp(capFraction, 0f, 1f);
        var cap = FixedPoint2.New(part.Comp.Max.Float() * capFraction);
        if (part.Comp.Current >= cap)
            return;

        SetCurrent((part.Owner, part.Comp), cap);
    }
}
