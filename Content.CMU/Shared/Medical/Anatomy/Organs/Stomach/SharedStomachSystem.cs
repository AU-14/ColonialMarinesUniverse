using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.Body.Events;
using Content.Shared.Body.Organ;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.Anatomy.Organs.Stomach;

public abstract partial class SharedStomachSystem : EntitySystem
{
    [Dependency] protected IConfigurationManager Cfg = default!;
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected IRobustRandom Random = default!;
    [Dependency] protected SharedStatusEffectsSystem Status = default!;

    private static readonly EntProtoId Nausea = "StatusEffectCMUNausea";
    private const float StomachScanInterval = 1f;
    private float _stomachScanAccumulator;

    private bool _medicalEnabled;
    private bool _organEnabled;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUStomachComponent, OrganStageChangedEvent>(OnStageChanged);
        SubscribeLocalEvent<CMUStomachComponent, ComponentStartup>(OnStomachStartup);
        SubscribeLocalEvent<CMUStomachComponent, OrganRemovedFromBodyEvent>(OnStomachRemovedFromBody);
        SubscribeLocalEvent<CMUStomachComponent, OrganAddedToBodyEvent>(OnStomachAddedToBody);

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.OrganEnabled, v => _organEnabled = v, true);
    }

    private void OnStomachStartup(Entity<CMUStomachComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.NextVomitCheck = Timing.CurTime + ent.Comp.VomitCheckInterval;
    }

    private void OnStageChanged(Entity<CMUStomachComponent> ent, ref OrganStageChangedEvent args)
    {
        var body = args.Body;
        if (args.New.IsAtLeast(OrganDamageStage.Damaged))
            Status.TrySetStatusEffectDuration(body, Nausea, duration: null);
        else
            Status.TryRemoveStatusEffect(body, Nausea);
    }

    private void OnStomachRemovedFromBody(Entity<CMUStomachComponent> ent, ref OrganRemovedFromBodyEvent args)
    {
        if (!_medicalEnabled || !_organEnabled || TerminatingOrDeleted(args.OldBody))
            return;

        EnsureComp<MissingStomachComponent>(args.OldBody);
        Status.TrySetStatusEffectDuration(args.OldBody, Nausea, duration: null);
    }

    private void OnStomachAddedToBody(Entity<CMUStomachComponent> ent, ref OrganAddedToBodyEvent args)
    {
        RemComp<MissingStomachComponent>(args.Body);

        if (TryComp<OrganHealthComponent>(ent, out var health) &&
            health.Stage.IsAtLeast(OrganDamageStage.Damaged))
        {
            Status.TrySetStatusEffectDuration(args.Body, Nausea, duration: null);
        }
        else
        {
            Status.TryRemoveStatusEffect(args.Body, Nausea);
        }
    }

    protected void UpdateServer(float frameTime)
    {
        if (!_medicalEnabled || !_organEnabled)
            return;

        _stomachScanAccumulator += frameTime;
        if (_stomachScanAccumulator < StomachScanInterval)
            return;
        _stomachScanAccumulator = 0f;

        var now = Timing.CurTime;
        var query = EntityQueryEnumerator<CMUStomachComponent, OrganHealthComponent>();
        while (query.MoveNext(out var uid, out var stomach, out var oh))
        {
            if (stomach.NextVomitCheck > now)
                continue;
            stomach.NextVomitCheck = now + stomach.VomitCheckInterval;

            if (!stomach.VomitChance.TryGetValue(oh.Stage, out var chance) || chance <= 0f)
                continue;
            if (!Random.Prob(chance))
                continue;

            var body = GetBody(uid);
            if (body is null)
                continue;
            if (TryComp<MobStateComponent>(body.Value, out var mob) && mob.CurrentState == MobState.Dead)
                continue;

            ApplyVomit(body.Value);
        }

        var missingQuery = EntityQueryEnumerator<MissingStomachComponent>();
        while (missingQuery.MoveNext(out var uid, out _))
            Status.TrySetStatusEffectDuration(uid, Nausea, duration: null);
    }

    protected virtual void ApplyVomit(EntityUid body)
    {
    }

    private EntityUid? GetBody(EntityUid organ)
        => TryComp<OrganComponent>(organ, out var organComp) ? organComp.Body : null;
}
