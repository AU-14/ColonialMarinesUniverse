using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.BodyPart;
using Content.Shared._CMU14.Medical.BodyPart.Events;
using Content.Shared._CMU14.Medical.Bones;
using Content.Shared._CMU14.Medical.Bones.Events;
using Content.Shared._CMU14.Medical.Organs;
using Content.Shared._CMU14.Medical.Organs.Events;
using Content.Shared._CMU14.Medical.StatusEffects.Events;
using Content.Shared._CMU14.Medical.Wounds;
using Content.Shared._CMU14.Medical.Wounds.Events;
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
    private float _painScanAccumulator;

    private bool _medicalEnabled;
    private bool _statusEffectsEnabled;
    private bool _painEnabled;
    private FixedPoint2 _painShockThreshold;
    private FixedPoint2 _painDecayPerSecond;
    private float _painTierHysteresis;
    private int _painSuppressionLevelsPerStep;

    public FixedPoint2 ShockThreshold => _painShockThreshold;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BoneFracturedEvent>(OnBoneFractured);
        SubscribeLocalEvent<BodyPartDamagedEvent>(OnBodyPartDamaged);
        SubscribeLocalEvent<OrganStageChangedEvent>(OnOrganStageChanged);
        SubscribeLocalEvent<BodyPartHealedEvent>(OnBodyPartHealed);
        SubscribeLocalEvent<WoundTreatedEvent>(OnWoundTreated);

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.StatusEffectsEnabled, v => _statusEffectsEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.PainEnabled, v => _painEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.PainShockThreshold, v => _painShockThreshold = (FixedPoint2)v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.PainDecayPerSecond, v => _painDecayPerSecond = (FixedPoint2)v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.PainTierHysteresis, v => _painTierHysteresis = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.PainSuppressionLevelsPerStep, v => _painSuppressionLevelsPerStep = v, true);
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
        pain.LastEventRecompute = Timing.CurTime;
    }

    private void OnBoneFractured(ref BoneFracturedEvent args)
        => OnRecomputeTrigger(args.Body);

    private void OnBodyPartDamaged(ref BodyPartDamagedEvent args)
        => OnRecomputeTrigger(args.Body);

    private void OnOrganStageChanged(ref OrganStageChangedEvent args)
        => OnRecomputeTrigger(args.Body);

    private void OnBodyPartHealed(ref BodyPartHealedEvent args)
        => OnRecomputeTrigger(args.Body);

    private void OnWoundTreated(WoundTreatedEvent args)
        => OnRecomputeTrigger(args.Body);

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
                RefreshAccumulationRate(uid, pain);

            if (pain.Tier == PainTier.None
                && pain.CachedAccumulationRate <= 0
                && pain.Pain <= 0)
                continue;

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
            RefreshAccumulationRate(ent.Owner, ent.Comp);
        TickOne(ent.Owner, ent.Comp);
    }

    private void RefreshAccumulationRate(EntityUid body, PainShockComponent pain)
    {
        var newRate = ComputeAccumulationRate(body);
        pain.AccumulationRateDirty = false;
        pain.LastEventRecompute = Timing.CurTime;

        if (pain.CachedAccumulationRate == newRate)
            return;

        pain.CachedAccumulationRate = newRate;
        Dirty(body, pain);
    }

    private void TickOne(EntityUid uid, PainShockComponent pain)
    {
        var supMult = (FixedPoint2)GetSuppressionMultiplier(uid);
        var net = pain.CachedAccumulationRate * supMult;

        var oldPain = pain.Pain;
        var newPain = pain.Pain + net - _painDecayPerSecond;
        if (newPain < FixedPoint2.Zero)
            newPain = FixedPoint2.Zero;
        if (newPain > pain.PainMax)
            newPain = pain.PainMax;
        pain.Pain = newPain;

        UpdateTier(uid, pain, newPain != oldPain);
    }

    private void UpdateTier(EntityUid body, PainShockComponent pain, bool painChanged)
    {
        var oldTier = pain.Tier;
        var newTier = PainTierThresholds.Get(oldTier, pain.Pain, _painTierHysteresis);
        if (newTier == oldTier)
        {
            // Pain may have moved without crossing a tier — still flush so
            // the client overlay's Pain ratio stays in sync.
            if (painChanged)
                Dirty(body, pain);
            return;
        }

        pain.Tier = newTier;
        pain.InShock = newTier == PainTier.Shock;

        SwapTierAlerts(body, oldTier, newTier);

        var ev = new PainTierChangedEvent(body, oldTier, newTier);
        RaiseLocalEvent(body, ref ev);

        Dirty(body, pain);
    }

    private void SwapTierAlerts(EntityUid body, PainTier oldTier, PainTier newTier)
    {
        var oldId = TierStatusEffectId(oldTier);
        var newId = TierStatusEffectId(newTier);
        if (oldId == newId)
            return;
        if (oldId is not null)
            Status.TryRemoveStatusEffect(body, oldId);
        if (newId is not null)
            Status.TryAddStatusEffectDuration(body, newId, TimeSpan.FromSeconds(60));
    }

    private static string? TierStatusEffectId(PainTier tier) => tier switch
    {
        PainTier.None => null,
        PainTier.Mild => "StatusEffectCMUPainMild",
        PainTier.Moderate => "StatusEffectCMUPainModerate",
        PainTier.Severe => "StatusEffectCMUPainSevere",
        PainTier.Shock => "StatusEffectCMUPainShock",
        _ => null,
    };

    /// <summary>
    ///     Tier seen by downstream readers. Re-derives from
    ///     <see cref="PainShockComponent.Pain"/> via
    ///     <see cref="PainTierThresholds.Get"/> (so a stale persisted Tier
    ///     can't lie to readers when Pain has been written directly), then
    ///     subtracts painkiller-suppression levels per
    ///     <c>cmu.medical.pain.suppression_levels_per_step</c>.
    /// </summary>
    public PainTier GetEffectiveTier(EntityUid body, PainShockComponent pain)
    {
        var rawTier = PainTierThresholds.Get(pain.Tier, pain.Pain, _painTierHysteresis);

        var supMult = GetSuppressionMultiplier(body);
        var quarterSteps = (int)Math.Round((1f - supMult) / 0.25f);
        if (quarterSteps <= 0)
            return rawTier;
        var supLevels = quarterSteps * Math.Max(0, _painSuppressionLevelsPerStep);
        var effective = Math.Max(0, (int)rawTier - supLevels);
        return (PainTier)effective;
    }

    /// <summary>
    ///     Sum every CMU pain source on the body. Fracture severity is
    ///     read through <see cref="SharedFractureSystem.GetEffectiveSeverity"/>
    ///     so splints and casts suppress correctly.
    /// </summary>
    public FixedPoint2 ComputeAccumulationRate(EntityUid body)
    {
        FixedPoint2 rate = FixedPoint2.Zero;

        foreach (var (partUid, _) in Body.GetBodyChildren(body))
        {
            if (TryComp<FractureComponent>(partUid, out var frac))
            {
                var sev = Fracture.GetEffectiveSeverity((partUid, frac));
                rate += FractureProfile.Get(sev).PainPerSecond;
            }

            if (TryComp<BodyPartHealthComponent>(partUid, out var ph) &&
                ph.Max > FixedPoint2.Zero &&
                ph.Current / ph.Max < (FixedPoint2)0.25f)
            {
                rate += (FixedPoint2)0.5f;
            }

            if (TryComp<BodyPartWoundComponent>(partUid, out var pw))
            {
                var untreated = 0;
                foreach (var w in pw.Wounds)
                {
                    if (!w.Treated)
                        untreated++;
                }
                if (untreated > 5)
                    untreated = 5;
                rate += (FixedPoint2)untreated * (FixedPoint2)0.5f;
            }
        }

        foreach (var organ in Body.GetBodyOrgans(body))
        {
            if (!TryComp<OrganHealthComponent>(organ.Id, out var oh))
                continue;
            rate += oh.Stage switch
            {
                OrganDamageStage.Bruised => (FixedPoint2)0.5f,
                OrganDamageStage.Damaged => (FixedPoint2)1f,
                OrganDamageStage.Failing => (FixedPoint2)2f,
                _ => FixedPoint2.Zero,
            };
        }

        return rate;
    }

    /// <summary>
    ///     Strongest active painkiller wins. Returns the suppression
    ///     multiplier in <c>[0, 1]</c> — lower = more suppression.
    /// </summary>
    public float GetSuppressionMultiplier(EntityUid body)
    {
        if (!Status.TryGetStatusEffect(body, "StatusEffectCMUPainSuppression", out var effectUid))
            return 1f;
        if (!TryComp<PainSuppressionComponent>(effectUid.Value, out var sup))
            return 1f;
        return Math.Clamp(1f - sup.Percent, 0f, 1f);
    }

    protected virtual void ApplyShockEntryEffect(EntityUid body) { }
    protected virtual void ApplyPeriodicShockKnockdown(EntityUid body) { }
}
