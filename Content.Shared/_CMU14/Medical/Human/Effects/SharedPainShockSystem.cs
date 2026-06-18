using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Foundation;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared._CMU14.Medical.Human.Care;
using Content.Shared._CMU14.Medical.Chemistry;
using Content.Shared._CMU14.Medical.Human.Effects.Events;
using Content.Shared._RMC14.Synth;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.Human.Effects;

public abstract partial class SharedPainShockSystem : EntitySystem
{
    [Dependency] protected IConfigurationManager Cfg = default!;
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected INetManager Net = default!;
    [Dependency] protected IRobustRandom Random = default!;
    [Dependency] protected SharedStatusEffectsSystem Status = default!;

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
    private const float StabilizedOrganPainMultiplier = 0.35f;
    private const float PainReflectionLowMax = 10f;
    private const float PainReflectionMediumMax = 90f;
    private const float PainReflectionMinDelaySeconds = 12f;
    private const float PainReflectionMaxDelaySeconds = 30f;
    private const float PainReflectionDelaySecondsPerByondTick = 0.25f;
    private const float CustomPainMinDelaySeconds = 4f;
    private const float HighPainFumbleMinimum = 50f;
    private const float HighPainFumbleChancePerAmount = 0.0005f;
    private const string PainSuppressionStatus = "StatusEffectCMUPainSuppression";

    private float _painScanAccumulator;
    private readonly Dictionary<EntityUid, TimeSpan> _nextCustomPain = new();

    private bool _medicalEnabled;
    private bool _statusEffectsEnabled;
    private bool _painEnabled;
    private FixedPoint2 _painShockThreshold;
    private FixedPoint2 _painDecayPerSecond;
    private float _painTierHysteresis;

    public FixedPoint2 ShockThreshold => _painShockThreshold;

    public readonly record struct PainSourceSnapshot(FixedPoint2 Target, FixedPoint2 RiseRate);

    public readonly record struct PainReflectionContext(
        BodyRegion Region,
        FixedPoint2 Amount,
        bool Burning,
        PainReflectionSeverity Severity,
        bool CanFumble,
        float FumbleChance);

    public enum PainReflectionSeverity : byte
    {
        None = 0,
        Low,
        Medium,
        High,
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanMedicalStartupEvent>(OnHumanMedicalStartup);
        SubscribeLocalEvent<HumanMedicalShutdownEvent>(OnHumanMedicalShutdown);
        SubscribeLocalEvent<HumanMedicalLedgerChangedEvent>(OnLedgerChanged);
        SubscribeLocalEvent<HumanTreatmentAppliedEvent>(OnTreatmentApplied);
        SubscribeLocalEvent<PainShockComponent, ComponentStartup>(OnPainStartup);
        SubscribeLocalEvent<PainShockComponent, ComponentShutdown>(OnPainShutdown);
        SubscribeLocalEvent<PainSuppressionComponent, StatusEffectRemovedEvent>(OnPainSuppressionRemoved);

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.StatusEffectsEnabled, v => _statusEffectsEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.PainEnabled, v => _painEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.PainShockThreshold, v => _painShockThreshold = (FixedPoint2) v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.PainDecayPerSecond, v => _painDecayPerSecond = (FixedPoint2) v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.PainTierHysteresis, v => _painTierHysteresis = v, true);
    }

    public bool IsLayerEnabled()
    {
        return _medicalEnabled && _statusEffectsEnabled && _painEnabled;
    }

    private void OnHumanMedicalStartup(ref HumanMedicalStartupEvent args)
    {
        OnRecomputeTrigger(args.Body);
    }

    private void OnHumanMedicalShutdown(ref HumanMedicalShutdownEvent args)
    {
        _nextCustomPain.Remove(args.Body);
    }

    private void OnLedgerChanged(ref HumanMedicalLedgerChangedEvent args)
    {
        OnRecomputeTrigger(args.Body);
    }

    private void OnTreatmentApplied(ref HumanTreatmentAppliedEvent args)
    {
        OnRecomputeTrigger(args.Patient);
    }

    private void OnPainStartup(Entity<PainShockComponent> ent, ref ComponentStartup args)
    {
        OnRecomputeTrigger(ent.Owner);

        var ev = new PainShockStartupEvent(ent.Owner, ent.Comp);
        RaiseLocalEvent(ref ev);
    }

    private void OnPainShutdown(Entity<PainShockComponent> ent, ref ComponentShutdown args)
    {
        _nextCustomPain.Remove(ent.Owner);

        var ev = new PainShockShutdownEvent(ent.Owner, ent.Comp);
        RaiseLocalEvent(ref ev);
    }

    public void OnRecomputeTrigger(EntityUid body)
    {
        if (!IsLayerEnabled())
            return;

        if (!TryComp<PainShockComponent>(body, out var pain))
            return;

        if (!HasComp<HumanMedicalComponent>(body))
            return;

        if (TryClearSynthPain(body, pain))
            return;

        if (TryComp<MobStateComponent>(body, out var mob) && mob.CurrentState == MobState.Dead)
            return;

        pain.AccumulationRateDirty = true;
        pain.NextUpdate = TimeSpan.Zero;
        pain.LastEventRecompute = Timing.CurTime;
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
        _nextCustomPain.Remove(body);

        var removedStatus = TierStatusEffectId(PainTier.Shock) is { } shockStatus
            && Status.TryRemoveStatusEffect(body, shockStatus);

        if (changed || removedStatus)
            Dirty(body, pain);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (Net.IsClient || !IsLayerEnabled())
            return;

        _painScanAccumulator += frameTime;
        if (_painScanAccumulator < PainScanInterval)
            return;

        _painScanAccumulator = 0f;
        var now = Timing.CurTime;
        var query = EntityQueryEnumerator<PainShockComponent, HumanMedicalComponent, MobStateComponent>();
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

        if (!HasComp<HumanMedicalComponent>(ent.Owner))
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
            var rise = pain.CachedRiseRate * (FixedPoint2) GetAccumulationMultiplier(uid);
            newPain = FixedPoint2.Min(target, newPain + rise);
        }
        else if (newPain > target)
        {
            var decay = _painDecayPerSecond + (FixedPoint2) GetDecayBonus(uid);
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
        RaiseLocalEvent(ref ev);

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
        if (pain.NextPainReflection > now)
            return;

        if (!TryBuildPainReflectionContext(body, tier, out var context))
            return;

        ApplyPainReflection(body, tier, context);
        pain.NextPainReflection = now + PainReflectionDelay(context.Amount);
        Dirty(body, pain);
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

    public bool TryCustomPain(
        EntityUid body,
        string locKey,
        int flashStrength = 0,
        params (string, object)[] locArgs)
    {
        if (Net.IsClient ||
            string.IsNullOrWhiteSpace(locKey) ||
            !CanShowCustomPain(body))
        {
            return false;
        }

        var now = Timing.CurTime;
        if (_nextCustomPain.TryGetValue(body, out var next) && next > now)
            return false;

        _nextCustomPain[body] = now + TimeSpan.FromSeconds(CustomPainMinDelaySeconds);
        ApplyCustomPain(body, locKey, flashStrength, locArgs);
        return true;
    }

    private bool CanShowCustomPain(EntityUid body)
    {
        if (!IsLayerEnabled())
            return false;

        if (!HasComp<HumanMedicalComponent>(body) || HasComp<SynthComponent>(body))
            return false;

        if (TryComp<MobStateComponent>(body, out var mob) &&
            mob.CurrentState is MobState.Dead or MobState.Critical)
        {
            return false;
        }

        return GetTierSuppression(body) < 2 && GetAccumulationSuppression(body) < 0.75f;
    }

    private PainTier ApplySuppressionToTier(EntityUid body, PainTier rawTier)
    {
        var supLevels = GetTierSuppression(body);
        if (supLevels <= 0)
            return rawTier;

        var effective = Math.Max(0, (int) rawTier - supLevels);
        return (PainTier) effective;
    }

    public PainSourceSnapshot ComputePainSourceProfile(EntityUid body)
    {
        if (HasComp<SynthComponent>(body))
            return new PainSourceSnapshot(FixedPoint2.Zero, FixedPoint2.Zero);

        if (!TryComp<HumanMedicalComponent>(body, out var medical))
            return new PainSourceSnapshot(FixedPoint2.Zero, FixedPoint2.Zero);

        return CalculateLedgerPainSourceProfile(medical);
    }

    public static PainSourceSnapshot CalculateLedgerPainSourceProfile(HumanMedicalComponent medical)
    {
        var sourceCount = 0;
        var highest = 0f;
        var total = 0f;
        var riseRate = 0f;

        for (var i = 1; i < medical.Regions.Length; i++)
        {
            var region = medical.Regions[i];
            AddPainSource(ref sourceCount, ref highest, ref total, ref riseRate, RegionDamagePainTarget(region));
            AddPainSource(ref sourceCount, ref highest, ref total, ref riseRate, SkeletalPainTarget(region.Skeletal));
            AddPainSource(ref sourceCount, ref highest, ref total, ref riseRate, IncisionPainTarget(region.Incision));
        }

        for (var i = 0; i < medical.Injuries.Count; i++)
        {
            var injury = medical.Injuries[i];
            AddPainSource(ref sourceCount, ref highest, ref total, ref riseRate, InjuryPainTarget(injury));
        }

        for (var i = 0; i < medical.BleedSources.Count; i++)
        {
            var bleed = medical.BleedSources[i];
            AddPainSource(ref sourceCount, ref highest, ref total, ref riseRate, BleedPainTarget(bleed));
        }

        for (var i = 0; i < medical.ForeignObjects.Count; i++)
        {
            var foreignObject = medical.ForeignObjects[i];
            AddPainSource(ref sourceCount, ref highest, ref total, ref riseRate, ForeignObjectPainTarget(foreignObject));
        }

        for (var i = 1; i < medical.Organs.Length; i++)
        {
            var organ = medical.Organs[i];
            AddPainSource(ref sourceCount, ref highest, ref total, ref riseRate, OrganPainTarget(organ));
        }

        if (sourceCount == 0)
            return new PainSourceSnapshot(FixedPoint2.Zero, FixedPoint2.Zero);

        var painTarget = MathF.Min(PainTargetCap, highest + SourceStackMultiplier * (total - highest));
        return new PainSourceSnapshot(
            (FixedPoint2) painTarget,
            (FixedPoint2) MathF.Min(PainRiseRateCap, riseRate));
    }

    public bool TryBuildPainReflectionContext(
        EntityUid body,
        PainTier tier,
        out PainReflectionContext context)
    {
        context = default;

        if (tier == PainTier.None ||
            !TryComp<HumanMedicalComponent>(body, out var medical))
        {
            return false;
        }

        return TryBuildPainReflectionContext(medical, out context);
    }

    public static bool TryBuildPainReflectionContext(
        HumanMedicalComponent medical,
        out PainReflectionContext context)
    {
        context = default;
        var bestRegion = BodyRegion.None;
        var bestAmount = 0f;
        var bestBurning = false;

        for (var i = 1; i < medical.Regions.Length; i++)
        {
            var region = medical.Regions[i];
            if (region.Region == BodyRegion.None ||
                region.Presence is LimbPresence.Missing or LimbPresence.Detached)
            {
                continue;
            }

            TryUsePainCandidate(
                region.Region,
                region.BruteDamage.Float() + SkeletalReflectionBonus(region.Skeletal),
                burning: false,
                ref bestRegion,
                ref bestAmount,
                ref bestBurning);

            TryUsePainCandidate(
                region.Region,
                region.BurnDamage.Float(),
                burning: true,
                ref bestRegion,
                ref bestAmount,
                ref bestBurning);
        }

        for (var i = 0; i < medical.Injuries.Count; i++)
        {
            var injury = medical.Injuries[i];
            TryUsePainCandidate(
                injury.Region,
                InjuryPainTarget(injury),
                injury.Kind == InjuryKind.Burn,
                ref bestRegion,
                ref bestAmount,
                ref bestBurning);
        }

        for (var i = 0; i < medical.BleedSources.Count; i++)
        {
            var bleed = medical.BleedSources[i];
            TryUsePainCandidate(
                bleed.Region,
                BleedPainTarget(bleed),
                burning: false,
                ref bestRegion,
                ref bestAmount,
                ref bestBurning);
        }

        for (var i = 0; i < medical.ForeignObjects.Count; i++)
        {
            var foreignObject = medical.ForeignObjects[i];
            TryUsePainCandidate(
                foreignObject.Region,
                ForeignObjectPainTarget(foreignObject),
                burning: false,
                ref bestRegion,
                ref bestAmount,
                ref bestBurning);
        }

        for (var i = 1; i < medical.Organs.Length; i++)
        {
            var organ = medical.Organs[i];
            TryUsePainCandidate(
                organ.Region,
                OrganPainTarget(organ),
                burning: false,
                ref bestRegion,
                ref bestAmount,
                ref bestBurning);
        }

        if (bestRegion == BodyRegion.None || bestAmount <= 0f)
            return false;

        var amount = (FixedPoint2) bestAmount;
        context = new PainReflectionContext(
            bestRegion,
            amount,
            bestBurning,
            GetPainReflectionSeverity(bestAmount),
            bestAmount > HighPainFumbleMinimum,
            Math.Clamp(bestAmount * HighPainFumbleChancePerAmount, 0f, 1f));
        return true;
    }

    private static void TryUsePainCandidate(
        BodyRegion region,
        float amount,
        bool burning,
        ref BodyRegion bestRegion,
        ref float bestAmount,
        ref bool bestBurning)
    {
        if (region == BodyRegion.None || amount <= bestAmount)
            return;

        bestRegion = region;
        bestAmount = amount;
        bestBurning = burning;
    }

    private static float SkeletalReflectionBonus(SkeletalState skeletal)
    {
        if (!skeletal.Broken)
            return 0f;

        return skeletal.Splinted ? 15f : 25f;
    }

    private static PainReflectionSeverity GetPainReflectionSeverity(float amount)
    {
        if (amount <= 0f)
            return PainReflectionSeverity.None;

        if (amount <= PainReflectionLowMax)
            return PainReflectionSeverity.Low;

        return amount <= PainReflectionMediumMax
            ? PainReflectionSeverity.Medium
            : PainReflectionSeverity.High;
    }

    private static void AddPainSource(
        ref int count,
        ref float highest,
        ref float total,
        ref float riseRate,
        float target)
    {
        if (target <= 0f)
            return;

        count++;
        highest = MathF.Max(highest, target);
        total += target;
        riseRate += target * PainRiseRatePerTarget;
    }

    public FixedPoint2 ComputeAccumulationRate(EntityUid body)
        => ComputePainSourceProfile(body).RiseRate;

    private static float RegionDamagePainTarget(RegionState region)
    {
        var damage = (region.BruteDamage + region.BurnDamage).Float();
        if (damage >= 80f)
            return 30f;

        if (damage >= 50f)
            return 18f;

        return damage >= 25f ? 8f : 0f;
    }

    private static float SkeletalPainTarget(SkeletalState skeletal)
    {
        if (!skeletal.Broken)
            return 0f;

        return skeletal.Splinted ? 14f : 32f;
    }

    private static float IncisionPainTarget(IncisionDepth incision) => incision switch
    {
        IncisionDepth.OpenSkin => 6f,
        IncisionDepth.Retracted => 10f,
        IncisionDepth.DeepAccess => 16f,
        _ => 0f,
    };

    private static float InjuryPainTarget(in InjuryRecord injury)
    {
        if (injury.Flags.HasFlag(InjuryFlags.Closed) ||
            injury.Flags.HasFlag(InjuryFlags.Sutured))
        {
            return 0f;
        }

        var pain = injury.Kind switch
        {
            InjuryKind.Stump => injury.IsOpenStump ? 55f : 18f,
            InjuryKind.InternalBleed => 20f,
            InjuryKind.Burn => BurnPainTarget(injury.Stage),
            InjuryKind.SurgicalIncision => 8f,
            _ => WoundPainTarget(injury.Stage),
        };

        if (injury.Flags.HasFlag(InjuryFlags.Bandaged) ||
            injury.Flags.HasFlag(InjuryFlags.Salved))
        {
            pain *= 0.5f;
        }

        return pain;
    }

    private static float WoundPainTarget(InjuryStage stage) => stage switch
    {
        InjuryStage.Tiny => 2f,
        InjuryStage.Small => 5f,
        InjuryStage.Moderate => 10f,
        InjuryStage.Large => 15f,
        InjuryStage.Deep => 18f,
        InjuryStage.Flesh => 24f,
        InjuryStage.Gaping => 30f,
        InjuryStage.GapingBig => 38f,
        InjuryStage.Massive => 50f,
        InjuryStage.Huge => 58f,
        InjuryStage.Monumental => 65f,
        InjuryStage.Severe => 65f,
        _ => 0f,
    };

    private static float BurnPainTarget(InjuryStage stage) => stage switch
    {
        InjuryStage.Tiny => 3f,
        InjuryStage.Small => 7f,
        InjuryStage.Moderate => 14f,
        InjuryStage.Large => 22f,
        InjuryStage.Deep => 30f,
        InjuryStage.Flesh => 40f,
        InjuryStage.Carbonised => 55f,
        _ => WoundPainTarget(stage),
    };

    private static float BleedPainTarget(BleedSource bleed)
    {
        if (!bleed.Active)
            return 0f;

        return bleed.Kind switch
        {
            BleedKind.Internal => 35f,
            BleedKind.Stump => 45f,
            BleedKind.External when bleed.Flags.HasFlag(BleedFlags.Arterial) => 12f,
            _ => 0f,
        };
    }

    private static float ForeignObjectPainTarget(ForeignObjectRecord foreignObject)
    {
        if (!foreignObject.Active || foreignObject.Severity <= 0f)
            return 0f;

        var fragmentPressure = 4f + foreignObject.Fragments * 3.5f;
        return MathF.Min(70f, MathF.Max(foreignObject.Severity, fragmentPressure));
    }

    private static float OrganPainTarget(OrganState organ)
    {
        if (organ.Missing || organ.Status == OrganDamageStatus.None)
            return 0f;

        var pain = organ.Slot switch
        {
            OrganSlot.Brain or OrganSlot.Heart or OrganSlot.LeftLung or OrganSlot.RightLung
                => VitalOrganPainTarget(organ.Status),
            OrganSlot.Liver or OrganSlot.Kidneys
                => MetabolicOrganPainTarget(organ.Status),
            OrganSlot.Stomach
                => StomachPainTarget(organ.Status),
            OrganSlot.Eyes or OrganSlot.Ears
                => SensoryOrganPainTarget(organ.Status),
            _ => FallbackOrganPainTarget(organ.Status),
        };

        if (organ.Flags.HasFlag(OrganFlags.Stasis))
            pain *= StabilizedOrganPainMultiplier;

        return pain;
    }

    private static float VitalOrganPainTarget(OrganDamageStatus status) => status switch
    {
        OrganDamageStatus.LittleBruised => 10f,
        OrganDamageStatus.Bruised => 32f,
        OrganDamageStatus.Broken => 65f,
        _ => 0f,
    };

    private static float MetabolicOrganPainTarget(OrganDamageStatus status) => status switch
    {
        OrganDamageStatus.LittleBruised => 6f,
        OrganDamageStatus.Bruised => 20f,
        OrganDamageStatus.Broken => 50f,
        _ => 0f,
    };

    private static float StomachPainTarget(OrganDamageStatus status) => status switch
    {
        OrganDamageStatus.LittleBruised => 4f,
        OrganDamageStatus.Bruised => 12f,
        OrganDamageStatus.Broken => 35f,
        _ => 0f,
    };

    private static float SensoryOrganPainTarget(OrganDamageStatus status) => status switch
    {
        OrganDamageStatus.LittleBruised => 2f,
        OrganDamageStatus.Bruised => 8f,
        OrganDamageStatus.Broken => 25f,
        _ => 0f,
    };

    private static float FallbackOrganPainTarget(OrganDamageStatus status) => status switch
    {
        OrganDamageStatus.LittleBruised => 10f,
        OrganDamageStatus.Bruised => 25f,
        OrganDamageStatus.Broken => 65f,
        _ => 0f,
    };

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

        if (!HasComp<HumanMedicalComponent>(body))
            return;

        if (!TryComp<PainShockComponent>(body, out var pain))
            return;

        if (TryClearSynthPain(body, pain))
            return;

        pain.Pain = FixedPoint2.Min(
            pain.PainMax,
            pain.Pain + amount * (FixedPoint2) GetAccumulationMultiplier(body));
        pain.NextUpdate = TimeSpan.Zero;
        UpdateTier(body, pain, true);
        Dirty(body, pain);
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
            ResolveSuppressionProfile(body, (effect, sup));

        return sup.AccumulationSuppression > 0f || sup.TierSuppression > 0 || sup.DecayBonus > 0f;
    }

    private void ResolveSuppressionProfile(EntityUid body, Entity<PainSuppressionComponent> ent, bool dirty = true)
    {
        var now = Timing.CurTime;
        var removed = ent.Comp.ActiveProfiles.RemoveAll(entry => entry.ExpiresAt <= now) > 0;
        var painFraction = GetPainSuppressionPainFraction(body);

        var bestAccumulation = 0f;
        var bestTier = 0;
        var bestDecay = 0f;
        var additiveAccumulation = 0f;
        var additiveTier = 0;
        var additiveDecay = 0f;
        foreach (var entry in ent.Comp.ActiveProfiles)
        {
            var effectiveness = GetPainSuppressionEffectiveness(entry, painFraction);
            var accumulation = entry.AccumulationSuppression * effectiveness;
            var tier = (int) MathF.Floor(entry.TierSuppression * effectiveness + 0.001f);
            var decay = entry.DecayBonus * effectiveness;

            if (entry.Additive)
            {
                additiveAccumulation += accumulation;
                additiveTier += tier;
                additiveDecay += decay;
                continue;
            }

            if (IsProfileStronger(accumulation, tier, decay, bestAccumulation, bestTier, bestDecay))
            {
                bestAccumulation = accumulation;
                bestTier = tier;
                bestDecay = decay;
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

    private float GetPainSuppressionPainFraction(EntityUid body)
    {
        if (!TryComp<PainShockComponent>(body, out var pain) || pain.PainMax <= FixedPoint2.Zero)
            return 0f;

        return Math.Clamp(pain.Pain.Float() / pain.PainMax.Float(), 0f, 1f);
    }

    private static float GetPainSuppressionEffectiveness(PainSuppressionEntry entry, float painFraction)
    {
        if (entry.ReductionDecreaseRate <= 0f || painFraction <= 0f)
            return 1f;

        return Math.Clamp(1f - painFraction * entry.ReductionDecreaseRate, 0f, 1f);
    }

    private static bool IsProfileStronger(
        float accumulation,
        int tier,
        float decay,
        float bestAccumulation,
        int bestTier,
        float bestDecay)
    {
        if (tier != bestTier)
            return tier > bestTier;

        if (MathF.Abs(accumulation - bestAccumulation) > 0.001f)
            return accumulation > bestAccumulation;

        return decay > bestDecay;
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

    private TimeSpan PainReflectionDelay(FixedPoint2 amount)
    {
        var seconds = Math.Clamp(
            (100f - amount.Float()) * PainReflectionDelaySecondsPerByondTick,
            PainReflectionMinDelaySeconds,
            PainReflectionMaxDelaySeconds);
        return TimeSpan.FromSeconds(seconds);
    }

    protected virtual void ApplyShockEntryEffect(EntityUid body) { }
    protected virtual void ApplyPeriodicShockKnockdown(EntityUid body) { }
    protected virtual void ApplyPainReflection(EntityUid body, PainTier tier, PainReflectionContext context) { }
    protected virtual void ApplyCustomPain(
        EntityUid body,
        string locKey,
        int flashStrength,
        (string, object)[] locArgs) { }
    protected virtual void ApplyPainRelief(EntityUid body, PainTier tier) { }
}

[ByRefEvent]
public readonly record struct PainShockStartupEvent(
    EntityUid Body,
    PainShockComponent Pain);

[ByRefEvent]
public readonly record struct PainShockShutdownEvent(
    EntityUid Body,
    PainShockComponent Pain);
