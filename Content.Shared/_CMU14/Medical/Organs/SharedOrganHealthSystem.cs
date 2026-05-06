using System.Collections.Generic;
using Content.Shared._CMU14.Medical.BodyPart;
using Content.Shared._CMU14.Medical.BodyPart.Events;
using Content.Shared._CMU14.Medical.Bones;
using Content.Shared._CMU14.Medical.Organs.Brain;
using Content.Shared._CMU14.Medical.Organs.Events;
using Content.Shared._CMU14.Medical.Organs.Heart;
using Content.Shared._CMU14.Medical.Organs.Lungs;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Body.Organ;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.Organs;

public abstract class SharedOrganHealthSystem : EntitySystem
{
    [Dependency] protected readonly IConfigurationManager Cfg = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly INetManager Net = default!;
    [Dependency] protected readonly IPrototypeManager Proto = default!;
    [Dependency] protected readonly IRobustRandom Random = default!;
    [Dependency] protected readonly RMCUnrevivableSystem Unrevivable = default!;

    private const float RegenScanInterval = 1f;
    private const float CompoundOrganPassThrough = 0.30f;
    private const float ComminutedOrganPassThrough = 0.50f;
    private const float NegativePartPassThroughBonus = 0.15f;
    private float _regenScanAccumulator;

    private bool _medicalEnabled;
    private bool _organEnabled;
    private FixedPoint2 _organPassiveHealMultiplier;
    private FixedPoint2 _organNativeRegenCap;

    public override void Initialize()
    {
        base.Initialize();
        // Bone system already subscribes <BoneComponent, BodyPartDamagedEvent>; SS14's
        // event bus enforces one handler per (component, event) pair so we cannot
        // re-subscribe on BoneComponent. We pick BodyPartHealthComponent — present
        // on every CMU part alongside BoneComponent — and order it `after:` the
        // bone system so any fracture spawn / integrity update from the same hit
        // is visible when we read the shielding flag.
        SubscribeLocalEvent<BodyPartHealthComponent, BodyPartDamagedEvent>(OnPartDamaged, after: new[] { typeof(SharedBoneSystem) });
        SubscribeLocalEvent<OrganHealthComponent, OrganDamagedEvent>(OnOrganDamaged);
        SubscribeLocalEvent<OrganHealthComponent, ComponentStartup>(OnOrganStartup);

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.OrganEnabled, v => _organEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.OrganPassiveHealMultiplier, v => _organPassiveHealMultiplier = (FixedPoint2)v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.OrganNativeRegenCap, v => _organNativeRegenCap = (FixedPoint2)v, true);
    }

    private void OnOrganStartup(Entity<OrganHealthComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.NextRegenTick = Timing.CurTime + TimeSpan.FromSeconds(10);
    }

    private void OnPartDamaged(Entity<BodyPartHealthComponent> ent, ref BodyPartDamagedEvent args)
    {
        if (!_medicalEnabled || !_organEnabled)
            return;

        if (args.ContainedOrgans.Count == 0)
            return;

        var passThrough = 1f;
        var heavyOrganHit = false;

        // Bone shielding: while a part's BoneShieldsOrgans flag is on, the
        // rib cage / skull / etc. limits pass-through by fracture severity and
        // part condition. Parts with no BoneComponent (V2 cybernetics, etc.)
        // skip the shielding step and route damage through unconditionally.
        if (ent.Comp.BoneShieldsOrgans && HasComp<BoneComponent>(ent))
        {
            var severity = TryComp<FractureComponent>(ent, out var fracture)
                ? fracture.Severity
                : FractureSeverity.None;
            if (!severity.IsAtLeast(FractureSeverity.Compound))
                return;

            passThrough = ComputeOrganPassThrough(ent.Comp, severity, args.NewCurrent);
            if (passThrough <= 0f)
                return;

            heavyOrganHit = severity.IsAtLeast(FractureSeverity.Comminuted) ||
                            args.NewCurrent < FixedPoint2.Zero;
        }

        DistributeOrganDamage(args.Body, args.Delta, args.ContainedOrgans, passThrough, heavyOrganHit);
    }

    public void DistributeOrganDamage(
        EntityUid body,
        DamageSpecifier delta,
        IReadOnlyList<EntityUid> organs,
        float passThrough = 1f,
        bool heavyOrganHit = false)
    {
        if (organs.Count == 0 || passThrough <= 0f)
            return;

        var affected = PickAffectedOrgans(organs, heavyOrganHit);
        foreach (var (organ, oh) in affected)
        {
            var weighted = WeightDamage(delta, oh.DamageWeight, organs.Count, passThrough);
            if (weighted.GetTotal() <= FixedPoint2.Zero)
                continue;
            var ev = new OrganDamagedEvent(body, organ, weighted, OrganDamageSource.PartDistribution);
            RaiseLocalEvent(organ, ref ev);
        }
    }

    private List<(EntityUid Organ, OrganHealthComponent Health)> PickAffectedOrgans(
        IReadOnlyList<EntityUid> organs,
        bool heavyOrganHit)
    {
        var candidates = new List<(EntityUid Organ, OrganHealthComponent Health, float Weight)>();
        foreach (var organ in organs)
        {
            if (!TryComp<OrganHealthComponent>(organ, out var oh))
                continue;

            candidates.Add((organ, oh, GetOrganSelectionWeight(organ)));
        }

        var count = Math.Min(heavyOrganHit ? 2 : 1, candidates.Count);
        var selected = new List<(EntityUid Organ, OrganHealthComponent Health)>(count);
        for (var i = 0; i < count; i++)
        {
            var total = 0f;
            foreach (var candidate in candidates)
                total += candidate.Weight;

            var roll = Random.NextFloat() * total;
            for (var j = 0; j < candidates.Count; j++)
            {
                var candidate = candidates[j];
                if ((roll -= candidate.Weight) > 0f)
                    continue;

                selected.Add((candidate.Organ, candidate.Health));
                candidates.RemoveAt(j);
                break;
            }
        }

        return selected;
    }

    private float GetOrganSelectionWeight(EntityUid organ)
    {
        var weight = 1f;
        if (HasComp<HeartComponent>(organ))
            weight += 2f;
        if (HasComp<LungsComponent>(organ))
            weight += 1.5f;
        if (HasComp<CMUBrainComponent>(organ))
            weight += 3f;

        return weight;
    }

    private void OnOrganDamaged(Entity<OrganHealthComponent> ent, ref OrganDamagedEvent args)
    {
        if (!_medicalEnabled || !_organEnabled)
            return;

        var total = args.Damage.GetTotal();
        if (total <= FixedPoint2.Zero)
            return;

        var previousStage = ComputeStage(ent.Comp);
        if (IsTraumaSource(args.Source))
        {
            total = ApplyTraumaDamageCap(ent.Comp, total);
            total = ApplyRepeatTraumaDampening(ent.Comp, total);
        }

        var nextCurrent = FixedPoint2.Max(FixedPoint2.Zero, ent.Comp.Current - total);
        nextCurrent = ApplyTraumaDeathGate(ent.Comp, args.Source, previousStage, nextCurrent);
        ent.Comp.Current = nextCurrent;

        var newStage = ComputeStage(ent.Comp);
        if (newStage == ent.Comp.Stage)
        {
            Dirty(ent);
            return;
        }

        var old = ent.Comp.Stage;
        ent.Comp.Stage = newStage;
        Dirty(ent);

        var stageEv = new OrganStageChangedEvent(args.Body, args.Organ, old, newStage);
        RaiseLocalEvent(ent, ref stageEv);
        // Also broadcast — some systems subscribe to the broadcast path to
        // avoid directed slot collisions.
        RaiseLocalEvent(ref stageEv);
    }

    public void SetStasisExpire(EntityUid organ, TimeSpan expireAt)
    {
        var stasis = EnsureComp<OrganStasisComponent>(organ);
        stasis.ExpireAt = expireAt;
        Dirty(organ, stasis);
    }

    public void HealOrgan(Entity<OrganHealthComponent?> ent, EntityUid body, FixedPoint2 amount)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, logMissing: false))
            return;
        if (amount <= FixedPoint2.Zero)
            return;
        var newCurrent = FixedPoint2.Min(ent.Comp.Max, ent.Comp.Current + amount);
        if (newCurrent == ent.Comp.Current)
            return;

        ent.Comp.Current = newCurrent;
        Dirty(ent.Owner, ent.Comp);
        RecomputeStage((ent.Owner, ent.Comp), body);
    }

    public void RecomputeStage(Entity<OrganHealthComponent> ent, EntityUid body)
    {
        var newStage = ComputeStage(ent.Comp);
        if (newStage == ent.Comp.Stage)
            return;

        var old = ent.Comp.Stage;
        ent.Comp.Stage = newStage;
        Dirty(ent);

        var stageEv = new OrganStageChangedEvent(body, ent.Owner, old, newStage);
        RaiseLocalEvent(ent, ref stageEv);
        RaiseLocalEvent(ref stageEv);
    }

    /// <summary>
    ///     Walk descending: lowest threshold first wins. With the default
    ///     <c>{Dead:0, Failing:5, Damaged:20, Bruised:35, Healthy:50}</c>:
    ///     Current=10 → Damaged (10 ≤ 20). Current=0 → Dead.
    /// </summary>
    private static OrganDamageStage ComputeStage(OrganHealthComponent c)
    {
        var v = c.Current;
        if (c.StageThresholds.TryGetValue(OrganDamageStage.Dead, out var d) && v <= d)
            return OrganDamageStage.Dead;
        if (c.StageThresholds.TryGetValue(OrganDamageStage.Failing, out var f) && v <= f)
            return OrganDamageStage.Failing;
        if (c.StageThresholds.TryGetValue(OrganDamageStage.Damaged, out var dm) && v <= dm)
            return OrganDamageStage.Damaged;
        if (c.StageThresholds.TryGetValue(OrganDamageStage.Bruised, out var b) && v <= b)
            return OrganDamageStage.Bruised;
        return OrganDamageStage.Healthy;
    }

    private static FixedPoint2 ApplyTraumaDamageCap(OrganHealthComponent organ, FixedPoint2 damage)
    {
        if (organ.TraumaDamageCapFraction <= 0f || organ.Max <= FixedPoint2.Zero)
            return damage;

        var cap = organ.Max * (FixedPoint2)organ.TraumaDamageCapFraction;
        return FixedPoint2.Min(damage, cap);
    }

    private FixedPoint2 ApplyRepeatTraumaDampening(OrganHealthComponent organ, FixedPoint2 damage)
    {
        var now = Timing.CurTime;
        var window = MathF.Max(0f, organ.RepeatTraumaWindowSeconds);
        if (organ.LastTraumaAt > TimeSpan.Zero &&
            window > 0f &&
            now - organ.LastTraumaAt <= TimeSpan.FromSeconds(window))
        {
            var multiplier = Math.Clamp(organ.RepeatTraumaDamageMultiplier, 0f, 1f);
            damage *= (FixedPoint2)multiplier;
        }

        organ.LastTraumaAt = now;
        return damage;
    }

    private static FixedPoint2 ApplyTraumaDeathGate(
        OrganHealthComponent organ,
        OrganDamageSource source,
        OrganDamageStage oldStage,
        FixedPoint2 nextCurrent)
    {
        if (!organ.TraumaRequiresFailingToDie || !IsTraumaSource(source))
            return nextCurrent;

        if (oldStage.IsAtLeast(OrganDamageStage.Failing))
            return nextCurrent;

        if (!organ.StageThresholds.TryGetValue(OrganDamageStage.Dead, out var deadThreshold) ||
            nextCurrent > deadThreshold)
        {
            return nextCurrent;
        }

        if (!organ.StageThresholds.TryGetValue(OrganDamageStage.Failing, out var failingThreshold))
            return nextCurrent;

        return FixedPoint2.Max(nextCurrent, failingThreshold);
    }

    private static bool IsTraumaSource(OrganDamageSource source)
        => source is OrganDamageSource.PartDistribution or OrganDamageSource.RibFracture;

    private static float ComputeOrganPassThrough(
        BodyPartHealthComponent part,
        FractureSeverity severity,
        FixedPoint2 current)
    {
        var scale = severity switch
        {
            FractureSeverity.Comminuted => ComminutedOrganPassThrough,
            FractureSeverity.Compound => CompoundOrganPassThrough,
            _ => 0f,
        };

        var partMax = part.Max;
        if (current < FixedPoint2.Zero && partMax > FixedPoint2.Zero)
        {
            var overflow = Math.Clamp((FixedPoint2.Zero - current).Float() / partMax.Float(), 0f, 1f);
            scale += overflow * NegativePartPassThroughBonus;
        }

        return Math.Clamp(scale, 0f, 1f);
    }

    private DamageSpecifier WeightDamage(
        DamageSpecifier delta,
        Dictionary<ProtoId<DamageGroupPrototype>, float> weight,
        int totalOrgans,
        float passThrough)
    {
        var result = new DamageSpecifier();
        if (weight.Count == 0 || totalOrgans <= 0 || passThrough <= 0f)
            return result;

        // Each entry in `weight` is a damage GROUP -> multiplier. The DamageSpecifier
        // dict is keyed by damage TYPE; we expand the group to its types and assign
        // each type its share. Result: for {Brute: 1.0, totalOrgans: 5}, a delta of
        // {Blunt: 50} produces {Blunt: 10}.
        foreach (var (group, w) in weight)
        {
            if (!Proto.TryIndex(group, out var groupProto))
                continue;
            if (!delta.TryGetDamageInGroup(groupProto, out var groupTotal))
                continue;
            if (groupTotal <= FixedPoint2.Zero)
                continue;

            var share = (FixedPoint2)(w * passThrough / totalOrgans);
            foreach (var type in groupProto.DamageTypes)
            {
                if (!delta.DamageDict.TryGetValue(type, out var typeAmount) || typeAmount <= FixedPoint2.Zero)
                    continue;
                var add = typeAmount * share;
                if (result.DamageDict.TryGetValue(type, out var existing))
                    result.DamageDict[type] = existing + add;
                else
                    result.DamageDict[type] = add;
            }
        }
        return result;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (Net.IsClient)
            return;

        if (!_medicalEnabled || !_organEnabled)
            return;

        _regenScanAccumulator += frameTime;
        if (_regenScanAccumulator < RegenScanInterval)
            return;
        _regenScanAccumulator = 0f;

        var now = Timing.CurTime;
        var globalMult = _organPassiveHealMultiplier;
        var capCVar = _organNativeRegenCap;

        var query = EntityQueryEnumerator<OrganHealthComponent, OrganComponent>();
        while (query.MoveNext(out var uid, out var oh, out var organ))
        {
            if (oh.NextRegenTick > now)
                continue;
            oh.NextRegenTick = now + TimeSpan.FromSeconds(10);

            if (organ.Body is not { } body || Unrevivable.IsUnrevivable(body))
                continue;

            if (oh.Stage.IsAtLeast(OrganDamageStage.Damaged))
                continue;
            if (HasComp<OrganStasisComponent>(uid))
                continue;
            if (oh.NativeRegenPerTick <= FixedPoint2.Zero)
                continue;

            // Per-organ override beats the CCVar when stricter.
            var capFraction = (FixedPoint2)MathF.Min((float)capCVar, oh.NativeRegenCap);
            var ceiling = oh.Max * capFraction;
            if (oh.Current >= ceiling)
                continue;

            oh.Current = FixedPoint2.Min(ceiling, oh.Current + oh.NativeRegenPerTick * globalMult);
            Dirty(uid, oh);
        }
    }
}
