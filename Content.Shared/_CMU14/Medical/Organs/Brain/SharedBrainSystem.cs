using Content.Shared._CMU14.Medical.Organs.Events;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Systems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.Organs.Brain;

public abstract class SharedBrainSystem : EntitySystem
{
    [Dependency] protected readonly IConfigurationManager Cfg = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly INetManager Net = default!;
    [Dependency] protected readonly IRobustRandom Rng = default!;
    [Dependency] protected readonly SharedBodySystem Body = default!;
    [Dependency] protected readonly SharedStatusEffectsSystem Status = default!;
    [Dependency] protected readonly RMCUnrevivableSystem Unrevivable = default!;

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

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.OrganEnabled, v => _organEnabled = v, true);
    }

    private void OnStageChanged(Entity<CMUBrainComponent> ent, ref OrganStageChangedEvent args)
    {
        var body = args.Body;
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
                ent.Comp.ActionSpeedMultiplier = 0f;
                if (!ent.Comp.PermadeathApplied)
                {
                    ent.Comp.PermadeathApplied = true;
                    ApplyPermadeath(body);
                }
                break;
        }
        Dirty(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (Net.IsClient)
            return;

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
                    TickDisorientation((uid, brain), now);
                    break;
                case OrganDamageStage.Failing:
                    TickFailingUnconscious((uid, brain), now);
                    break;
            }
        }
    }

    private void TickDisorientation(Entity<CMUBrainComponent> ent, TimeSpan now)
    {
        if (ent.Comp.NextDisorientCheck > now)
            return;
        ent.Comp.NextDisorientCheck = now + TimeSpan.FromMinutes(1);
        Dirty(ent);

        if (!Rng.Prob(ent.Comp.DisorientationChancePerMinute))
            return;

        var body = GetBody(ent);
        if (body is null)
            return;
        if (Unrevivable.IsUnrevivable(body.Value))
            return;
        ApplyDisorientation(body.Value);
    }

    private void TickFailingUnconscious(Entity<CMUBrainComponent> ent, TimeSpan now)
    {
        if (ent.Comp.NextUnconsciousCheck > now)
            return;
        ent.Comp.NextUnconsciousCheck = now + TimeSpan.FromSeconds(60);
        Dirty(ent);

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

    protected virtual void ApplyDisorientation(EntityUid body)
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
