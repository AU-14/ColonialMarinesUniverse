using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared._CMU14.Medical.Anatomy.Bones;
using Content.Shared._CMU14.Medical.Anatomy.Bones.Events;
using Content.Shared._CMU14.Medical.Treatment.FirstAid;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared._CMU14.Medical.Injuries.Pain.Events;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._CMU14.Medical.Injuries.Wounds.Events;
using Content.Shared._RMC14.Synth;
using Content.Shared.Body.Part;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.Injuries.Pain;

public abstract partial class SharedPainShockSystem : EntitySystem
{
    [Dependency] protected IConfigurationManager Cfg = default!;
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected INetManager Net = default!;
    [Dependency] protected IRobustRandom Random = default!;
    [Dependency] protected SharedPainSourceProfileSystem PainSources = default!;
    [Dependency] protected SharedStatusEffectsSystem Status = default!;

    private const float PainScanInterval = 0.5f;
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
        if (TryClearSynthPain(body, pain))
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

    private void OnSplintChanged(ref CMUSplintChangedEvent args)
        => OnPartRecomputeTrigger(args.Part);

    private void OnCastStartup(Entity<CMUCastComponent> ent, ref ComponentStartup args)
    {
        RaiseCastChanged(ent.Owner, false);
        OnPartRecomputeTrigger(ent.Owner);
    }

    private void OnCastRemove(Entity<CMUCastComponent> ent, ref ComponentRemove args)
    {
        RaiseCastChanged(ent.Owner, true);
        OnPartRecomputeTrigger(ent.Owner);
    }

    private void OnBodyPartDamaged(ref BodyPartDamagedEvent args)
        => OnRecomputeTrigger(args.Body);

    private void OnBodyPartPainThresholdCrossed(ref BodyPartPainThresholdCrossedEvent args)
        => OnRecomputeTrigger(args.Body);

    private void OnOrganStageChanged(ref OrganStageChangedEvent args)
        => OnRecomputeTrigger(args.Body);

    private void OnBodyPartHealed(ref BodyPartHealedEvent args)
        => OnRecomputeTrigger(args.Body);

    private void OnWoundsStartup(Entity<BodyPartWoundComponent> ent, ref ComponentStartup args)
    {
        RaiseWoundsChanged(ent.Owner, false);
        OnPartRecomputeTrigger(ent.Owner);
    }

    private void OnWoundsRemove(Entity<BodyPartWoundComponent> ent, ref ComponentRemove args)
    {
        RaiseWoundsChanged(ent.Owner, true);
        OnPartRecomputeTrigger(ent.Owner);
    }

    private void OnWoundTreated(ref WoundTreatedEvent args)
        => OnRecomputeTrigger(args.Body);

    private void OnEscharStartup(Entity<CMUEscharComponent> ent, ref ComponentStartup args)
    {
        RaiseEscharChanged(ent.Owner, false);
        OnPartRecomputeTrigger(ent.Owner);
    }

    private void OnEscharRemove(Entity<CMUEscharComponent> ent, ref ComponentRemove args)
    {
        RaiseEscharChanged(ent.Owner, true);
        OnPartRecomputeTrigger(ent.Owner);
    }

    private void OnInternalBleedChanged(ref InternalBleedingChangedEvent args)
        => OnRecomputeTrigger(args.Body);

    private void RaiseCastChanged(EntityUid part, bool removed)
    {
        var ev = new CMUCastChangedEvent(part, removed);
        RaiseLocalEvent(ref ev);
    }

    private void RaiseWoundsChanged(EntityUid part, bool removed)
    {
        var ev = new BodyPartWoundsChangedEvent(part, removed);
        RaiseLocalEvent(ref ev);
    }

    private void RaiseEscharChanged(EntityUid part, bool removed)
    {
        if (!TryComp<BodyPartComponent>(part, out var partComp) || partComp.Body is not { } body)
            return;

        var ev = new CMUEscharChangedEvent(body, part, removed);
        RaiseLocalEvent(ref ev);
    }

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
        if (TryClearSynthPain(args.Target, pain))
            return;

        ent.Comp.ActiveProfiles.Clear();
        ent.Comp.AccumulationSuppression = 0f;
        ent.Comp.TierSuppression = 0;
        ent.Comp.DecayBonus = 0f;
        Dirty(ent);

        pain.NextUpdate = TimeSpan.Zero;
        UpdateTier(args.Target, pain, false);
    }

    private bool TryClearSynthPain(EntityUid body, PainShockComponent pain)
    {
        if (!HasComp<SynthComponent>(body))
            return false;

        if (Net.IsServer)
            ClearPainState(body, pain);

        return true;
    }

    private void ClearPainState(EntityUid body, PainShockComponent pain)
    {
        var changed = pain.Pain != FixedPoint2.Zero
            || pain.PainTarget != FixedPoint2.Zero
            || pain.CachedRiseRate != FixedPoint2.Zero
            || pain.AccumulationRateDirty
            || pain.RawTier != PainTier.None
            || pain.Tier != PainTier.None
            || pain.InShock
            || pain.NextUpdate != TimeSpan.Zero
            || pain.NextShockPulse != TimeSpan.Zero
            || pain.NextTierAlertRefresh != TimeSpan.Zero
            || pain.NextPainReflection != TimeSpan.Zero
            || pain.NextPainRelief != TimeSpan.Zero;

        pain.Pain = FixedPoint2.Zero;
        pain.PainTarget = FixedPoint2.Zero;
        pain.CachedRiseRate = FixedPoint2.Zero;
        pain.AccumulationRateDirty = false;
        pain.RawTier = PainTier.None;
        pain.Tier = PainTier.None;
        pain.InShock = false;
        pain.NextUpdate = TimeSpan.Zero;
        pain.NextShockPulse = TimeSpan.Zero;
        pain.NextTierAlertRefresh = TimeSpan.Zero;
        pain.NextPainReflection = TimeSpan.Zero;
        pain.NextPainRelief = TimeSpan.Zero;

        var removedStatus = TierStatusEffectId(PainTier.Shock) is { } shockStatus
            && Status.TryRemoveStatusEffect(body, shockStatus);

        if (changed || removedStatus)
            Dirty(body, pain);
    }

    protected void UpdateServer(float frameTime)
    {
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
            if (TryClearSynthPain(uid, pain))
                continue;

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
        if (TryClearSynthPain(ent.Owner, ent.Comp))
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
        if (TryClearSynthPain(body, pain))
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
        RaiseLocalEvent(body, ref ev, broadcast: true);

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
        if (HasComp<SynthComponent>(body))
            return PainTier.None;

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

    public CMUPainSourceSnapshot ComputePainSourceProfile(EntityUid body)
        => PainSources.ComputePainSourceProfile(body);

    public FixedPoint2 ComputeAccumulationRate(EntityUid body)
        => PainSources.ComputeAccumulationRate(body);

    public void AddPainSuppressionProfile(
        EntityUid body,
        float accumulationSuppression,
        int tierSuppression,
        float decayBonus,
        TimeSpan duration,
        float reductionDecreaseRate = 0f)
        => AddPainSuppressionProfile(
            body,
            accumulationSuppression,
            tierSuppression,
            decayBonus,
            duration,
            additive: false,
            reductionDecreaseRate);

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
            additive: true,
            reductionDecreaseRate: 0f);

    public void AddPainPulse(EntityUid body, FixedPoint2 amount)
    {
        if (Net.IsClient || amount <= FixedPoint2.Zero)
            return;
        if (!IsLayerEnabled())
            return;
        if (!TryComp<PainShockComponent>(body, out var pain))
            return;
        if (TryClearSynthPain(body, pain))
            return;

        pain.Pain = FixedPoint2.Min(
            pain.PainMax,
            pain.Pain + amount * (FixedPoint2)GetAccumulationMultiplier(body));
        pain.NextUpdate = TimeSpan.Zero;
        UpdateTier(body, pain, true);
    }

    private void AddPainSuppressionProfile(
        EntityUid body,
        float accumulationSuppression,
        int tierSuppression,
        float decayBonus,
        TimeSpan duration,
        bool additive,
        float reductionDecreaseRate)
    {
        if (Net.IsClient || duration <= TimeSpan.Zero)
            return;

        if (!Status.TryUpdateStatusEffectDuration(body, PainSuppressionStatus, out var effect, duration)
            || effect is not { } effectUid)
        {
            return;
        }

        var sup = EnsureComp<PainSuppressionComponent>(effectUid);
        ResolveSuppressionProfile(body, (effectUid, sup), dirty: false);
        var oldAccumulation = sup.AccumulationSuppression;
        var oldTier = sup.TierSuppression;
        var oldDecay = sup.DecayBonus;

        sup.ActiveProfiles.Add(new PainSuppressionEntry
        {
            AccumulationSuppression = Math.Clamp(accumulationSuppression, 0f, 1f),
            TierSuppression = Math.Max(0, tierSuppression),
            DecayBonus = Math.Max(0f, decayBonus),
            ReductionDecreaseRate = Math.Max(0f, reductionDecreaseRate),
            Additive = additive,
            ExpiresAt = Timing.CurTime + duration,
        });

        ResolveSuppressionProfile(body, (effectUid, sup));
        RefreshTier(body);

        if (TryComp<PainShockComponent>(body, out var pain))
        {
            pain.NextUpdate = TimeSpan.Zero;
            if (CMUPainSuppressionResolver.SuppressionImproved(sup, oldAccumulation, oldTier, oldDecay)
                && (pain.Pain > 0 || pain.PainTarget > 0 || pain.RawTier != PainTier.None))
            {
                SchedulePainRelief(pain);
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
            ResolveSuppressionProfile(body, (effect, sup));

        return sup.AccumulationSuppression > 0f || sup.TierSuppression > 0 || sup.DecayBonus > 0f;
    }

    private void ResolveSuppressionProfile(EntityUid body, Entity<PainSuppressionComponent> ent, bool dirty = true)
    {
        var result = CMUPainSuppressionResolver.ResolveAndPrune(
            ent.Comp,
            GetPainSuppressionPainFraction(body),
            Timing.CurTime);

        ent.Comp.AccumulationSuppression = result.AccumulationSuppression;
        ent.Comp.TierSuppression = result.TierSuppression;
        ent.Comp.DecayBonus = result.DecayBonus;

        if (dirty && result.Changed)
            Dirty(ent);
    }

    private float GetPainSuppressionPainFraction(EntityUid body)
    {
        if (!TryComp<PainShockComponent>(body, out var pain) || pain.PainMax <= FixedPoint2.Zero)
            return 0f;

        return Math.Clamp(pain.Pain.Float() / pain.PainMax.Float(), 0f, 1f);
    }

    private void SchedulePainRelief(PainShockComponent pain)
    {
        var now = Timing.CurTime;
        if (pain.NextPainRelief > now)
            return;

        pain.NextPainRelief = now + RandomPainReliefDelay();
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
            return;

        ApplyPainRelief(body, pain.Tier);
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
