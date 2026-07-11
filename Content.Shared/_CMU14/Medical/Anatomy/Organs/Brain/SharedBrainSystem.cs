using Content.Shared._CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared.Body.Events;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Systems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared._CMU14.Medical.Core;

namespace Content.Shared._CMU14.Medical.Anatomy.Organs.Brain;

public abstract partial class SharedBrainSystem : EntitySystem
{
    [Dependency] protected SharedBodySystem Body = default!;
    [Dependency] protected BlurryVisionSystem BlurryVision = default!;
    [Dependency] protected IConfigurationManager Cfg = default!;
    [Dependency] protected IRobustRandom Rng = default!;
    [Dependency] protected SharedStatusEffectsSystem Status = default!;
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected RMCUnrevivableSystem Unrevivable = default!;

    private static readonly EntProtoId Concussed = "StatusEffectCMUConcussed";
    private static readonly EntProtoId TraumaticBrainInjury = "StatusEffectCMUTraumaticBrainInjury";
    private static readonly EntProtoId Unconscious = "StatusEffectCMUUnconscious";

    private const float BrainScanInterval = 1f;
    private float _brainScanAccumulator;

    private bool _medicalEnabled;
    private bool _organEnabled;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUBrainComponent, OrganStageChangedEvent>(OnStageChanged);
        SubscribeLocalEvent<CMUBrainComponent, OrganRemovedFromBodyEvent>(OnBrainRemovedFromBody);
        SubscribeLocalEvent<CMUBrainVisionImpairmentComponent, AfterAutoHandleStateEvent>(OnVisionStateHandled);
        SubscribeLocalEvent<CMUBrainVisionImpairmentComponent, GetBlurEvent>(OnGetBlur);

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.OrganEnabled, v => _organEnabled = v, true);
    }

    private void OnBrainRemovedFromBody(Entity<CMUBrainComponent> ent, ref OrganRemovedFromBodyEvent args)
    {
        if (!_medicalEnabled || !_organEnabled)
            return;
        if (TerminatingOrDeleted(args.OldBody))
            return;

        if (!ent.Comp.PermadeathApplied)
        {
            ent.Comp.PermadeathApplied = true;
            Dirty(ent);
        }

        ApplyPermadeath(args.OldBody);
    }

    private void OnStageChanged(Entity<CMUBrainComponent> ent, ref OrganStageChangedEvent args)
    {
        var body = args.Body;
        UpdateVisionImpairment(body, ent.Comp, args.New);
        switch (args.New)
        {
            case OrganDamageStage.Healthy:
                ent.Comp.ActionSpeedMultiplier = 1.0f;
                Status.TryRemoveStatusEffect(body, Concussed);
                Status.TryRemoveStatusEffect(body, TraumaticBrainInjury);
                ClearSlurredSpeech(body);
                break;
            case OrganDamageStage.Bruised:
                ent.Comp.ActionSpeedMultiplier = 0.9f;
                Status.TryRemoveStatusEffect(body, TraumaticBrainInjury);
                ClearSlurredSpeech(body);
                Status.TrySetStatusEffectDuration(body, Concussed, duration: null);
                break;
            case OrganDamageStage.Damaged:
                ent.Comp.ActionSpeedMultiplier = 0.75f;
                Status.TryRemoveStatusEffect(body, TraumaticBrainInjury);
                Status.TrySetStatusEffectDuration(body, Concussed, duration: null);
                ApplySlurredSpeech(body);
                break;
            case OrganDamageStage.Failing:
                ent.Comp.ActionSpeedMultiplier = 0.5f;
                Status.TrySetStatusEffectDuration(body, TraumaticBrainInjury, duration: null);
                ApplySlurredSpeech(body);
                break;
            case OrganDamageStage.Dead:
                // CM brain damage can continue past the scanner's "braindead"
                // reading without killing the patient. Removing the brain is
                // still immediately fatal through OnBrainRemovedFromBody.
                ent.Comp.ActionSpeedMultiplier = 0.5f;
                Status.TrySetStatusEffectDuration(body, TraumaticBrainInjury, duration: null);
                ApplySlurredSpeech(body);
                break;
        }
        Dirty(ent);
    }

    protected void UpdateServer(float frameTime)
    {
        if (!_medicalEnabled || !_organEnabled)
            return;

        _brainScanAccumulator += frameTime;
        if (_brainScanAccumulator < BrainScanInterval)
            return;
        _brainScanAccumulator = 0f;

        var now = Timing.CurTime;
        var query = EntityQueryEnumerator<CMUBrainComponent, OrganHealthComponent>();
        while (query.MoveNext(out var uid, out var brain, out var oh))
        {
            switch (oh.Stage)
            {
                case OrganDamageStage.Bruised:
                case OrganDamageStage.Damaged:
                    TickDisorientation((uid, brain), oh.Stage, now);
                    break;
                case OrganDamageStage.Failing:
                case OrganDamageStage.Dead:
                    TickDisorientation((uid, brain), oh.Stage, now);
                    TickFailingUnconscious((uid, brain), now);
                    break;
            }
        }
    }

    private void TickDisorientation(
        Entity<CMUBrainComponent> ent,
        OrganDamageStage stage,
        TimeSpan now)
    {
        if (ent.Comp.NextDisorientCheck > now)
            return;
        ent.Comp.NextDisorientCheck = now + ent.Comp.DisorientationCheckInterval;

        var chance = stage switch
        {
            OrganDamageStage.Bruised => ent.Comp.BruisedDisorientationChance,
            OrganDamageStage.Damaged => ent.Comp.DamagedDisorientationChance,
            OrganDamageStage.Failing => ent.Comp.FailingDisorientationChance,
            OrganDamageStage.Dead => ent.Comp.FailingDisorientationChance,
            _ => 0f,
        };
        if (!Rng.Prob(chance))
            return;

        var body = GetBody(ent);
        if (body is null)
            return;
        if (Unrevivable.IsUnrevivable(body.Value))
            return;
        ApplyDisorientation(body.Value, ent.Comp, stage);
    }

    private void TickFailingUnconscious(Entity<CMUBrainComponent> ent, TimeSpan now)
    {
        if (ent.Comp.NextUnconsciousCheck > now)
            return;
        ent.Comp.NextUnconsciousCheck = now + TimeSpan.FromSeconds(60);

        var body = GetBody(ent);
        if (body is null)
            return;
        if (Unrevivable.IsUnrevivable(body.Value))
            return;
        Status.TrySetStatusEffectDuration(body.Value, Unconscious, TimeSpan.FromSeconds(5));
    }

    protected virtual void ApplyPermadeath(EntityUid body)
    {
    }

    private void UpdateVisionImpairment(
        EntityUid body,
        CMUBrainComponent brain,
        OrganDamageStage stage)
    {
        var magnitude = stage switch
        {
            OrganDamageStage.Bruised => brain.BruisedVisionBlur,
            OrganDamageStage.Damaged => brain.DamagedVisionBlur,
            OrganDamageStage.Failing => brain.FailingVisionBlur,
            OrganDamageStage.Dead => brain.FailingVisionBlur,
            _ => 0f,
        };

        if (!TryComp<CMUBrainVisionImpairmentComponent>(body, out var impairment))
        {
            if (magnitude <= 0f)
                return;

            impairment = EnsureComp<CMUBrainVisionImpairmentComponent>(body);
        }

        if (MathF.Abs(impairment.Magnitude - magnitude) <= 0.001f)
            return;

        impairment.Magnitude = magnitude;
        Dirty(body, impairment);
        BlurryVision.UpdateBlurMagnitude(body);
    }

    private void OnVisionStateHandled(
        Entity<CMUBrainVisionImpairmentComponent> ent,
        ref AfterAutoHandleStateEvent args)
    {
        BlurryVision.UpdateBlurMagnitude(ent.Owner);
    }

    private void OnGetBlur(Entity<CMUBrainVisionImpairmentComponent> ent, ref GetBlurEvent args)
    {
        args.Blur = MathF.Max(args.Blur, ent.Comp.Magnitude);
    }

    protected virtual void ApplyDisorientation(
        EntityUid body,
        CMUBrainComponent brain,
        OrganDamageStage stage)
    {
    }

    protected virtual void ApplySlurredSpeech(EntityUid body)
    {
    }

    protected virtual void ClearSlurredSpeech(EntityUid body)
    {
    }

    protected EntityUid? GetBody(EntityUid organ)
        => TryComp<OrganComponent>(organ, out var organComp) ? organComp.Body : null;
}
