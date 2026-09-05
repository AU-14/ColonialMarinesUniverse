using Content.Shared.CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared.Body.Events;
using Content.Shared._RMC14.Medical.Stasis;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Rejuvenate;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Chemistry.Effects;

namespace Content.Shared.CMU14.Medical.Anatomy.Organs.Brain;

public abstract partial class SharedBrainSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] protected SharedBodySystem Body = default!;
    [Dependency] protected BlurryVisionSystem BlurryVision = default!;
    [Dependency] protected IConfigurationManager Cfg = default!;
    [Dependency] protected IRobustRandom Rng = default!;
    [Dependency] protected StatusEffectsSystem Status = default!;
    [Dependency] protected CMStasisBagSystem Stasis = default!;
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
        SubscribeLocalEvent<CMUBrainComponent, OrganAddedToBodyEvent>(OnBrainAddedToBody,
            after: new[] { typeof(CMUMedicalBodyIndexSystem) });
        SubscribeLocalEvent<CMUBrainComponent, OrganRemovedFromBodyEvent>(OnBrainRemovedFromBody,
            after: new[] { typeof(CMUMedicalBodyIndexSystem) });
        SubscribeLocalEvent<CMUBrainVisionImpairmentComponent, AfterAutoHandleStateEvent>(OnVisionStateHandled);
        SubscribeLocalEvent<CMUBrainVisionImpairmentComponent, GetBlurEvent>(OnGetBlur);
        SubscribeLocalEvent<CMUBrainSpeechImpairmentComponent, RejuvenateEvent>(OnSpeechRejuvenate);

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.OrganEnabled, v => _organEnabled = v, true);
    }

    private void OnBrainAddedToBody(Entity<CMUBrainComponent> ent, ref OrganAddedToBodyEvent args)
    {
        if (_net.IsClient)
            return;

        if (TryComp<OrganHealthComponent>(ent.Owner, out var health))
            UpdateActionSpeed(ent, health.Stage);
        ReconcileSpeechImpairment(args.Body);
    }

    private void OnBrainRemovedFromBody(Entity<CMUBrainComponent> ent, ref OrganRemovedFromBodyEvent args)
    {
        if (_net.IsClient || TerminatingOrDeleted(args.OldBody))
            return;

        ReconcileSpeechImpairment(args.OldBody);
        if (!_medicalEnabled || !_organEnabled)
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
        if (_net.IsClient)
            return;

        // Donor-local state must still recover while the organ is detached.
        UpdateActionSpeed(ent, args.New);
        if (GetBody(ent.Owner) is not { } body || TerminatingOrDeleted(body))
            return;

        UpdateVisionImpairment(body, ent.Comp, args.New);
        ReconcileSpeechImpairment(body);
        switch (args.New)
        {
            case OrganDamageStage.Healthy:
                Status.TryRemoveStatusEffect(body, Concussed);
                Status.TryRemoveStatusEffect(body, TraumaticBrainInjury);
                break;
            case OrganDamageStage.Bruised:
                Status.TryRemoveStatusEffect(body, TraumaticBrainInjury);
                Status.TrySetStatusEffectDuration(body, Concussed, duration: null);
                break;
            case OrganDamageStage.Damaged:
                Status.TryRemoveStatusEffect(body, TraumaticBrainInjury);
                Status.TrySetStatusEffectDuration(body, Concussed, duration: null);
                break;
            case OrganDamageStage.Failing:
                Status.TrySetStatusEffectDuration(body, TraumaticBrainInjury, duration: null);
                break;
            case OrganDamageStage.Dead:
                // CM brain damage can continue past the scanner's "braindead"
                // reading without killing the patient. Removing the brain is
                // still immediately fatal through OnBrainRemovedFromBody.
                Status.TrySetStatusEffectDuration(body, TraumaticBrainInjury, duration: null);
                break;
        }
    }

    private void UpdateActionSpeed(Entity<CMUBrainComponent> ent, OrganDamageStage stage)
    {
        var multiplier = stage switch
        {
            OrganDamageStage.Bruised => 0.9f,
            OrganDamageStage.Damaged => 0.75f,
            OrganDamageStage.Failing or OrganDamageStage.Dead => 0.5f,
            _ => 1f,
        };
        if (ent.Comp.ActionSpeedMultiplier == multiplier)
            return;

        ent.Comp.ActionSpeedMultiplier = multiplier;
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
        if (body is null || !Stasis.CanBodyMetabolize(body.Value))
            return;
        if (HasComp<ChemicalNeurocryogenicComponent>(body.Value))
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
        if (body is null || !Stasis.CanBodyMetabolize(body.Value))
            return;
        if (HasComp<ChemicalNeurocryogenicComponent>(body.Value))
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

    private void ReconcileSpeechImpairment(EntityUid body)
    {
        if (TerminatingOrDeleted(body))
            return;

        foreach (var organ in _medicalIndex.GetOrgans<CMUBrainComponent>(body))
        {
            // A stale index entry or a donor being detached must not contribute.
            if (organ.Comp2.Body != body || TerminatingOrDeleted(organ.Owner) ||
                !TryComp<ChildOrganComponent>(organ.Owner, out var child) || child.Parent is not { } part ||
                !TryComp<BodyPartComponent>(part, out var parent) || parent.Body != body ||
                !TryComp<OrganHealthComponent>(organ.Owner, out var health) || !health.Stage.IsAtLeast(OrganDamageStage.Damaged))
            {
                continue;
            }

            EnsureComp<CMUBrainSpeechImpairmentComponent>(body);
            return;
        }

        RemComp<CMUBrainSpeechImpairmentComponent>(body);
    }

    private void OnSpeechRejuvenate(Entity<CMUBrainSpeechImpairmentComponent> ent, ref RejuvenateEvent args)
    {
        if (!_net.IsClient && !TerminatingOrDeleted(ent.Owner))
            RemComp<CMUBrainSpeechImpairmentComponent>(ent.Owner);
    }

    protected EntityUid? GetBody(EntityUid organ)
        => TryComp<OrganComponent>(organ, out var organComp) ? organComp.Body : null;
}
