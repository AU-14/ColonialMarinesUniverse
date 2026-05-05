using System.Collections.Generic;
using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.BodyPart;
using Content.Shared._CMU14.Medical.BodyPart.Events;
using Content.Shared._CMU14.Medical.Bones;
using Content.Shared._CMU14.Medical.Bones.Events;
using Content.Shared._CMU14.Medical.Items;
using Content.Shared._CMU14.Medical.Organs;
using Content.Shared._CMU14.Medical.Organs.Events;
using Content.Shared._CMU14.Medical.StatusEffects.Events;
using Content.Shared._CMU14.Medical.Wounds;
using Content.Shared._CMU14.Medical.Wounds.Events;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.StatusEffects;

public abstract class SharedPainShockSystem : EntitySystem
{
    [Dependency] protected readonly IConfigurationManager Cfg = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly INetManager Net = default!;
    [Dependency] protected readonly IRobustRandom Random = default!;
    [Dependency] protected readonly SharedBodySystem Body = default!;
    [Dependency] protected readonly SharedFractureSystem Fracture = default!;
    [Dependency] protected readonly SharedStatusEffectsSystem Status = default!;

    private const float PainScanInterval = 0.5f;
    private const float SourceStackMultiplier = 0.30f;
    private const float PainTargetCap = 95f;
    private const float PainRiseRateCap = 4.0f;
    private const float PainRiseRatePerTarget = 0.05f;
    private const float ShockStatusRefreshSeconds = 2.5f;
    private const float ShockStatusRefreshThrottleSeconds = 1.75f;
    private const float IdlePainSleepSeconds = 30f;
    private const float ShockPulseMinSeconds = 25f;
    private const float ShockPulseMaxSeconds = 35f;
    private const float PainReliefMinSeconds = 3f;
    private const float PainReliefMaxSeconds = 5f;
    private const string PainSuppressionStatus = "StatusEffectCMUPainSuppression";

    private float _painScanAccumulator;

    private bool _medicalEnabled;
    private bool _statusEffectsEnabled;
    private bool _painEnabled;
    private FixedPoint2 _painShockThreshold;
    private FixedPoint2 _painDecayPerSecond;
    private float _painTierHysteresis;

    public FixedPoint2 ShockThreshold => _painShockThreshold;

    public readonly record struct PainSourceSnapshot(FixedPoint2 Target, FixedPoint2 RiseRate);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BoneFracturedEvent>(OnBoneFractured);
        SubscribeLocalEvent<FractureSeverityChangedEvent>(OnFractureSeverityChanged);
        SubscribeLocalEvent<CMUSplintChangedEvent>(OnSplintChanged);
        SubscribeLocalEvent<CMUCastComponent, ComponentStartup>(OnCastStartup);
        SubscribeLocalEvent<CMUCastComponent, ComponentRemove>(OnCastRemove);
        SubscribeLocalEvent<BodyPartDamagedEvent>(OnBodyPartDamaged);
        SubscribeLocalEvent<BodyPartPainThresholdCrossedEvent>(OnBodyPartPainThresholdCrossed);
        SubscribeLocalEvent<OrganStageChangedEvent>(OnOrganStageChanged);
        SubscribeLocalEvent<BodyPartHealedEvent>(OnBodyPartHealed);
        SubscribeLocalEvent<BodyPartWoundComponent, ComponentStartup>(OnWoundsStartup);
        SubscribeLocalEvent<BodyPartWoundComponent, ComponentRemove>(OnWoundsRemove);
        SubscribeLocalEvent<WoundTreatedEvent>(OnWoundTreated);
        SubscribeLocalEvent<CMUEscharComponent, ComponentStartup>(OnEscharStartup);
        SubscribeLocalEvent<CMUEscharComponent, ComponentRemove>(OnEscharRemove);
        SubscribeLocalEvent<InternalBleedingChangedEvent>(OnInternalBleedChanged);
        SubscribeLocalEvent<PainSuppressionComponent, StatusEffectRemovedEvent>(OnPainSuppressionRemoved);

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.StatusEffectsEnabled, v => _statusEffectsEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.PainEnabled, v => _painEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.PainShockThreshold, v => _painShockThreshold = (FixedPoint2)v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.PainDecayPerSecond, v => _painDecayPerSecond = (FixedPoint2)v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.PainTierHysteresis, v => _painTierHysteresis = v, true);
    }

    public bool IsLayerEnabled()
    {
        return _medicalEnabled && _statusEffectsEnabled && _painEnabled;
    }

    public void OnRecomputeTrigger(EntityUid body)
    {
        if (!IsLayerEnabled())
            return;
        if (!TryComp<PainShockComponent>(body, out var pain))
            return;
        if (!HasComp<CMUHumanMedicalComponent>(body))
            return;

        if (TryComp<MobStateComponent>(body, out var mob) && mob.CurrentState == MobState.Dead)
            return;

        pain.AccumulationRateDirty = true;
        pain.NextUpdate = TimeSpan.Zero;
        pain.LastEventRecompute = Timing.CurTime;
    }

    private void OnBoneFractured(ref BoneFracturedEvent args)
        => OnRecomputeTrigger(args.Body);

    private void OnFractureSeverityChanged(ref FractureSeverityChangedEvent args)
        => OnRecomputeTrigger(args.Body);

    private void OnSplintChanged(CMUSplintChangedEvent args)
        => OnPartRecomputeTrigger(args.Part);

    private void OnCastStartup(Entity<CMUCastComponent> ent, ref ComponentStartup args)
        => OnPartRecomputeTrigger(ent.Owner);

    private void OnCastRemove(Entity<CMUCastComponent> ent, ref ComponentRemove args)
        => OnPartRecomputeTrigger(ent.Owner);

    private void OnBodyPartDamaged(ref BodyPartDamagedEvent args)
        => OnRecomputeTrigger(args.Body);

    private void OnBodyPartPainThresholdCrossed(ref BodyPartPainThresholdCrossedEvent args)
        => OnRecomputeTrigger(args.Body);

    private void OnOrganStageChanged(ref OrganStageChangedEvent args)
        => OnRecomputeTrigger(args.Body);

    private void OnBodyPartHealed(ref BodyPartHealedEvent args)
        => OnRecomputeTrigger(args.Body);

    private void OnWoundsStartup(Entity<BodyPartWoundComponent> ent, ref ComponentStartup args)
        => OnPartRecomputeTrigger(ent.Owner);

    private void OnWoundsRemove(Entity<BodyPartWoundComponent> ent, ref ComponentRemove args)
        => OnPartRecomputeTrigger(ent.Owner);

    private void OnWoundTreated(WoundTreatedEvent args)
        => OnRecomputeTrigger(args.Body);

    private void OnEscharStartup(Entity<CMUEscharComponent> ent, ref ComponentStartup args)
        => OnPartRecomputeTrigger(ent.Owner);

    private void OnEscharRemove(Entity<CMUEscharComponent> ent, ref ComponentRemove args)
        => OnPartRecomputeTrigger(ent.Owner);

    private void OnInternalBleedChanged(InternalBleedingChangedEvent args)
        => OnRecomputeTrigger(args.Body);

    private void OnPartRecomputeTrigger(EntityUid part)
    {
        if (!TryComp<BodyPartComponent>(part, out var partComp) || partComp.Body is not { } body)
            return;
        OnRecomputeTrigger(body);
    }

    private void OnPainSuppressionRemoved(Entity<PainSuppressionComponent> ent, ref StatusEffectRemovedEvent args)
    {
        if (Net.IsClient)
            return;
        if (!TryComp<PainShockComponent>(args.Target, out var pain))
            return;

        ent.Comp.ActiveProfiles.Clear();
        ent.Comp.AccumulationSuppression = 0f;
        ent.Comp.TierSuppression = 0;
        ent.Comp.DecayBonus = 0f;
        Dirty(ent);

        pain.NextUpdate = TimeSpan.Zero;
        UpdateTier(args.Target, pain, false);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (Net.IsClient)
            return;

        if (!IsLayerEnabled())
            return;

        _painScanAccumulator += frameTime;
        if (_painScanAccumulator < PainScanInterval)
            return;
        _painScanAccumulator = 0f;

        var now = Timing.CurTime;
        var query = EntityQueryEnumerator<PainShockComponent, CMUHumanMedicalComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var pain, out _, out var mob))
        {
            if (mob.CurrentState == MobState.Dead || pain.NextUpdate > now)
                continue;
            pain.NextUpdate = now + TimeSpan.FromSeconds(1);

            if (pain.AccumulationRateDirty)
                RefreshPainSources(uid, pain);

            if (pain.RawTier == PainTier.None
                && pain.Tier == PainTier.None
                && pain.PainTarget <= 0
                && pain.CachedRiseRate <= 0
                && pain.NextPainRelief == TimeSpan.Zero
                && pain.Pain <= 0)
            {
                pain.NextUpdate = now + TimeSpan.FromSeconds(IdlePainSleepSeconds);
                continue;
            }

            TickOne(uid, pain);
        }
    }

    public void TickOne(Entity<PainShockComponent?> ent, bool refreshCache = true)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, logMissing: false))
            return;
        if (!HasComp<CMUHumanMedicalComponent>(ent.Owner))
            return;
        if (refreshCache)
            RefreshPainSources(ent.Owner, ent.Comp);
        TickOne(ent.Owner, ent.Comp);
    }

    private void RefreshPainSources(EntityUid body, PainShockComponent pain)
    {
        var source = ComputePainSourceProfile(body);
        pain.AccumulationRateDirty = false;
        pain.LastEventRecompute = Timing.CurTime;

        if (pain.PainTarget == source.Target && pain.CachedRiseRate == source.RiseRate)
            return;

        pain.PainTarget = source.Target;
        pain.CachedRiseRate = source.RiseRate;
    }

    private void TickOne(EntityUid uid, PainShockComponent pain)
    {
        var oldPain = pain.Pain;
        var newPain = pain.Pain;
        var target = FixedPoint2.Min(pain.PainTarget, pain.PainMax);

        if (newPain < target)
        {
            var rise = pain.CachedRiseRate * (FixedPoint2)GetAccumulationMultiplier(uid);
            newPain = FixedPoint2.Min(target, newPain + rise);
        }
        else if (newPain > target)
        {
            var decay = _painDecayPerSecond + (FixedPoint2)GetDecayBonus(uid);
            var decayed = newPain - decay;
            newPain = decayed < target ? target : decayed;
        }

        if (newPain < FixedPoint2.Zero)
            newPain = FixedPoint2.Zero;
        if (newPain > pain.PainMax)
            newPain = pain.PainMax;
        pain.Pain = newPain;

        UpdateTier(uid, pain, newPain != oldPain);
        TryShowPainRelief(uid, pain);
        TryApplyRecurringShockPulse(uid, pain);
    }

    public void RefreshTier(EntityUid body)
    {
        if (Net.IsClient)
            return;
        if (!TryComp<PainShockComponent>(body, out var pain))
            return;

        UpdateTier(body, pain, false);
    }

    private void UpdateTier(EntityUid body, PainShockComponent pain, bool painChanged)
    {
        var oldTier = pain.Tier;
        var oldRawTier = pain.RawTier;
        var rawTier = PainTierThresholds.Get(oldRawTier, pain.Pain, _painTierHysteresis, _painShockThreshold);
        var newTier = ApplySuppressionToTier(body, rawTier);

        pain.RawTier = rawTier;
        pain.Tier = newTier;
        pain.InShock = newTier == PainTier.Shock;

        if (newTier == oldTier)
        {
            RefreshTierStatus(body, pain, newTier);
            TryShowPainReflection(body, pain, newTier);

            if (newTier != PainTier.Shock)
                pain.NextShockPulse = TimeSpan.Zero;

            return;
        }

        SwapTierStatuses(body, pain, oldTier, newTier);

        var ev = new PainTierChangedEvent(body, oldTier, newTier);
        RaiseLocalEvent(body, ref ev);

        if (newTier == PainTier.Shock && oldTier != PainTier.Shock)
            TriggerShockEntry(body, pain);
        else if (newTier != PainTier.Shock)
            pain.NextShockPulse = TimeSpan.Zero;

        if (newTier == PainTier.None)
            pain.NextPainReflection = TimeSpan.Zero;
        else
            TryShowPainReflection(body, pain, newTier, force: true);

        Dirty(body, pain);
    }

    private void SwapTierStatuses(EntityUid body, PainShockComponent pain, PainTier oldTier, PainTier newTier)
    {
        var oldId = TierStatusEffectId(oldTier);
        var newId = TierStatusEffectId(newTier);
        if (oldId == newId)
        {
            RefreshTierStatus(body, pain, newTier, force: true);
            return;
        }
        if (oldId is not null)
            Status.TryRemoveStatusEffect(body, oldId);
        RefreshTierStatus(body, pain, newTier, force: true);
    }

    private void RefreshTierStatus(EntityUid body, PainShockComponent pain, PainTier tier, bool force = false)
    {
        if (Net.IsClient)
            return;
        if (TierStatusEffectId(tier) is not { } id)
            return;

        var now = Timing.CurTime;
        if (!force && pain.NextTierAlertRefresh > now)
            return;

        Status.TryUpdateStatusEffectDuration(body, id, TimeSpan.FromSeconds(ShockStatusRefreshSeconds));
        pain.NextTierAlertRefresh = now + TimeSpan.FromSeconds(ShockStatusRefreshThrottleSeconds);
    }

    private static string? TierStatusEffectId(PainTier tier) => tier switch
    {
        PainTier.Shock => "StatusEffectCMUPainShock",
        _ => null,
    };

    private void TryShowPainReflection(EntityUid body, PainShockComponent pain, PainTier tier, bool force = false)
    {
        if (Net.IsClient || tier == PainTier.None)
            return;

        var now = Timing.CurTime;
        if (!force && pain.NextPainReflection > now)
            return;

        ApplyPainReflection(body, tier);
        pain.NextPainReflection = now + RandomPainReflectionDelay(tier);
    }

    public PainTier GetRawTier(PainShockComponent pain)
        => PainTierThresholds.Get(pain.RawTier, pain.Pain, _painTierHysteresis, _painShockThreshold);

    public PainTier GetEffectiveTier(EntityUid body, PainShockComponent pain)
    {
        var rawTier = GetRawTier(pain);
        return ApplySuppressionToTier(body, rawTier);
    }

    public bool IsPainRiskSuppressed(EntityUid body, PainShockComponent pain)
        => GetRawTier(pain) > GetEffectiveTier(body, pain);

    private PainTier ApplySuppressionToTier(EntityUid body, PainTier rawTier)
    {
        var supLevels = GetTierSuppression(body);
        if (supLevels <= 0)
            return rawTier;
        var effective = Math.Max(0, (int)rawTier - supLevels);
        return (PainTier)effective;
    }

    public PainSourceSnapshot ComputePainSourceProfile(EntityUid body)
    {
        var sources = new List<float>();
        var riseRate = 0f;

        void AddSource(float target)
        {
            if (target <= 0f)
                return;
            sources.Add(target);
            riseRate += target * PainRiseRatePerTarget;
        }

        foreach (var (partUid, _) in Body.GetBodyChildren(body))
        {
            if (TryComp<FractureComponent>(partUid, out var frac))
                AddSource(FracturePainTarget(Fracture.GetEffectiveSeverity((partUid, frac))));

            if (TryComp<BodyPartHealthComponent>(partUid, out var ph) &&
                ph.Max > FixedPoint2.Zero)
            {
                var current = ph.Current;
                var max = ph.Max;
                var fraction = current.Float() / max.Float();
                if (fraction < 0.10f)
                    AddSource(30f);
                else if (fraction < 0.25f)
                    AddSource(15f);
            }

            if (TryComp<BodyPartWoundComponent>(partUid, out var pw))
            {
                for (var i = 0; i < pw.Wounds.Count; i++)
                {
                    if (pw.Wounds[i].Treated)
                        continue;
                    var size = i < pw.Sizes.Count ? pw.Sizes[i] : WoundSize.Deep;
                    AddSource(WoundPainTarget(size));
                }
            }

            if (HasComp<CMUEscharComponent>(partUid))
                AddSource(55f);

            if (HasComp<InternalBleedingComponent>(partUid))
                AddSource(35f);
        }

        foreach (var organ in Body.GetBodyOrgans(body))
        {
            if (!TryComp<OrganHealthComponent>(organ.Id, out var oh))
                continue;
            AddSource(OrganPainTarget(oh.Stage));
        }

        if (sources.Count == 0)
            return new PainSourceSnapshot(FixedPoint2.Zero, FixedPoint2.Zero);

        var highest = 0f;
        var total = 0f;
        foreach (var source in sources)
        {
            highest = MathF.Max(highest, source);
            total += source;
        }

        var target = MathF.Min(PainTargetCap, highest + SourceStackMultiplier * (total - highest));
        return new PainSourceSnapshot(
            (FixedPoint2)target,
            (FixedPoint2)MathF.Min(PainRiseRateCap, riseRate));
    }

    public FixedPoint2 ComputeAccumulationRate(EntityUid body)
        => ComputePainSourceProfile(body).RiseRate;

    private static float FracturePainTarget(FractureSeverity sev) => sev switch
    {
        FractureSeverity.Hairline => 10f,
        FractureSeverity.Simple => 25f,
        FractureSeverity.Compound => 45f,
        FractureSeverity.Comminuted => 65f,
        _ => 0f,
    };

    private static float WoundPainTarget(WoundSize size) => size switch
    {
        WoundSize.Small => 5f,
        WoundSize.Deep => 15f,
        WoundSize.Gaping => 30f,
        WoundSize.Massive => 50f,
        _ => 0f,
    };

    private static float OrganPainTarget(OrganDamageStage stage) => stage switch
    {
        OrganDamageStage.Bruised => 10f,
        OrganDamageStage.Damaged => 25f,
        OrganDamageStage.Failing => 45f,
        OrganDamageStage.Dead => 65f,
        _ => 0f,
    };

    public void AddPainSuppressionProfile(
        EntityUid body,
        float accumulationSuppression,
        int tierSuppression,
        float decayBonus,
        TimeSpan duration)
        => AddPainSuppressionProfile(
            body,
            accumulationSuppression,
            tierSuppression,
            decayBonus,
            duration,
            additive: false);

    public void AddAdditivePainSuppressionProfile(
        EntityUid body,
        float accumulationSuppression,
        int tierSuppression,
        float decayBonus,
        TimeSpan duration)
        => AddPainSuppressionProfile(
            body,
            accumulationSuppression,
            tierSuppression,
            decayBonus,
            duration,
            additive: true);

    private void AddPainSuppressionProfile(
        EntityUid body,
        float accumulationSuppression,
        int tierSuppression,
        float decayBonus,
        TimeSpan duration,
        bool additive)
    {
        if (Net.IsClient || duration <= TimeSpan.Zero)
            return;

        if (!Status.TryUpdateStatusEffectDuration(body, PainSuppressionStatus, out var effect, duration)
            || effect is not { } effectUid)
        {
            return;
        }

        var sup = EnsureComp<PainSuppressionComponent>(effectUid);
        ResolveSuppressionProfile((effectUid, sup), dirty: false);
        var oldAccumulation = sup.AccumulationSuppression;
        var oldTier = sup.TierSuppression;
        var oldDecay = sup.DecayBonus;

        sup.ActiveProfiles.Add(new PainSuppressionEntry
        {
            AccumulationSuppression = Math.Clamp(accumulationSuppression, 0f, 1f),
            TierSuppression = Math.Max(0, tierSuppression),
            DecayBonus = Math.Max(0f, decayBonus),
            Additive = additive,
            ExpiresAt = Timing.CurTime + duration,
        });

        ResolveSuppressionProfile((effectUid, sup));
        RefreshTier(body);

        if (TryComp<PainShockComponent>(body, out var pain))
        {
            pain.NextUpdate = TimeSpan.Zero;
            if (SuppressionImproved(sup, oldAccumulation, oldTier, oldDecay)
                && (pain.Pain > 0 || pain.PainTarget > 0 || pain.RawTier != PainTier.None))
            {
                SchedulePainRelief(body, pain);
            }
        }
    }

    public float GetAccumulationSuppression(EntityUid body)
    {
        if (!TryGetPainSuppression(body, out var sup))
            return 0f;
        return Math.Clamp(sup.AccumulationSuppression, 0f, 1f);
    }

    public float GetAccumulationMultiplier(EntityUid body)
        => Math.Clamp(1f - GetAccumulationSuppression(body), 0f, 1f);

    public float GetSuppressionMultiplier(EntityUid body)
        => GetAccumulationMultiplier(body);

    public int GetTierSuppression(EntityUid body)
    {
        if (!TryGetPainSuppression(body, out var sup))
            return 0;
        return Math.Max(0, sup.TierSuppression);
    }

    public float GetDecayBonus(EntityUid body)
    {
        if (!TryGetPainSuppression(body, out var sup))
            return 0f;
        return Math.Max(0f, sup.DecayBonus);
    }

    private bool TryGetPainSuppression(EntityUid body, out PainSuppressionComponent sup)
    {
        sup = default!;
        if (!Status.TryGetStatusEffect(body, PainSuppressionStatus, out var effectUid)
            || effectUid is not { } effect
            || !TryComp<PainSuppressionComponent>(effect, out var suppression))
        {
            return false;
        }

        sup = suppression;
        if (Net.IsServer)
            ResolveSuppressionProfile((effect, sup));

        return sup.AccumulationSuppression > 0f || sup.TierSuppression > 0 || sup.DecayBonus > 0f;
    }

    private void ResolveSuppressionProfile(Entity<PainSuppressionComponent> ent, bool dirty = true)
    {
        var now = Timing.CurTime;
        var removed = ent.Comp.ActiveProfiles.RemoveAll(entry => entry.ExpiresAt <= now) > 0;

        var bestAccumulation = 0f;
        var bestTier = 0;
        var bestDecay = 0f;
        var additiveAccumulation = 0f;
        var additiveTier = 0;
        var additiveDecay = 0f;
        foreach (var entry in ent.Comp.ActiveProfiles)
        {
            if (entry.Additive)
            {
                additiveAccumulation += entry.AccumulationSuppression;
                additiveTier += entry.TierSuppression;
                additiveDecay += entry.DecayBonus;
                continue;
            }

            if (IsProfileStronger(entry, bestAccumulation, bestTier, bestDecay))
            {
                bestAccumulation = entry.AccumulationSuppression;
                bestTier = entry.TierSuppression;
                bestDecay = entry.DecayBonus;
            }
        }

        bestAccumulation = Math.Clamp(bestAccumulation + additiveAccumulation, 0f, 1f);
        bestTier = Math.Max(0, bestTier + additiveTier);
        bestDecay = Math.Max(0f, bestDecay + additiveDecay);

        var changed = removed
            || MathF.Abs(ent.Comp.AccumulationSuppression - bestAccumulation) > 0.001f
            || ent.Comp.TierSuppression != bestTier
            || MathF.Abs(ent.Comp.DecayBonus - bestDecay) > 0.001f;

        ent.Comp.AccumulationSuppression = bestAccumulation;
        ent.Comp.TierSuppression = bestTier;
        ent.Comp.DecayBonus = bestDecay;

        if (dirty && changed)
            Dirty(ent);
    }

    private static bool IsProfileStronger(
        PainSuppressionEntry entry,
        float bestAccumulation,
        int bestTier,
        float bestDecay)
    {
        if (entry.TierSuppression != bestTier)
            return entry.TierSuppression > bestTier;
        if (MathF.Abs(entry.AccumulationSuppression - bestAccumulation) > 0.001f)
            return entry.AccumulationSuppression > bestAccumulation;
        return entry.DecayBonus > bestDecay;
    }

    private static bool SuppressionImproved(
        PainSuppressionComponent sup,
        float oldAccumulation,
        int oldTier,
        float oldDecay)
    {
        return sup.TierSuppression > oldTier
            || sup.AccumulationSuppression > oldAccumulation + 0.001f
            || sup.DecayBonus > oldDecay + 0.001f;
    }

    private void SchedulePainRelief(EntityUid body, PainShockComponent pain)
    {
        var now = Timing.CurTime;
        if (pain.NextPainRelief > now)
            return;

        pain.NextPainRelief = now + RandomPainReliefDelay();
        Dirty(body, pain);
    }

    private void TryShowPainRelief(EntityUid body, PainShockComponent pain)
    {
        if (Net.IsClient || pain.NextPainRelief == TimeSpan.Zero)
            return;

        var now = Timing.CurTime;
        if (pain.NextPainRelief > now)
            return;

        pain.NextPainRelief = TimeSpan.Zero;
        if (!TryGetPainSuppression(body, out _))
        {
            Dirty(body, pain);
            return;
        }

        ApplyPainRelief(body, pain.Tier);
        Dirty(body, pain);
    }

    private void TriggerShockEntry(EntityUid body, PainShockComponent pain)
    {
        pain.ShockPulseSerial++;
        pain.NextShockPulse = Timing.CurTime + RandomShockPulseDelay();
        ApplyShockEntryEffect(body);
    }

    private void TryApplyRecurringShockPulse(EntityUid body, PainShockComponent pain)
    {
        if (pain.Tier != PainTier.Shock)
            return;

        var now = Timing.CurTime;
        if (pain.NextShockPulse == TimeSpan.Zero)
        {
            pain.NextShockPulse = now + RandomShockPulseDelay();
            Dirty(body, pain);
            return;
        }

        if (pain.NextShockPulse > now)
            return;

        pain.ShockPulseSerial++;
        pain.NextShockPulse = now + RandomShockPulseDelay();
        ApplyPeriodicShockKnockdown(body);
        Dirty(body, pain);
    }

    private TimeSpan RandomShockPulseDelay()
        => TimeSpan.FromSeconds(Random.NextFloat(ShockPulseMinSeconds, ShockPulseMaxSeconds));

    private TimeSpan RandomPainReliefDelay()
        => TimeSpan.FromSeconds(Random.NextFloat(PainReliefMinSeconds, PainReliefMaxSeconds));

    private TimeSpan RandomPainReflectionDelay(PainTier tier)
    {
        var (min, max) = tier switch
        {
            PainTier.Mild => (45f, 75f),
            PainTier.Moderate => (35f, 55f),
            PainTier.Severe => (14f, 24f),
            PainTier.Shock => (7f, 13f),
            _ => (45f, 75f),
        };

        return TimeSpan.FromSeconds(Random.NextFloat(min, max));
    }

    protected virtual void ApplyShockEntryEffect(EntityUid body) { }
    protected virtual void ApplyPeriodicShockKnockdown(EntityUid body) { }
    protected virtual void ApplyPainReflection(EntityUid body, PainTier tier) { }
    protected virtual void ApplyPainRelief(EntityUid body, PainTier tier) { }
}
