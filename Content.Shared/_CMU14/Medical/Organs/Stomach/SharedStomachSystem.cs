using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.Organs.Events;
using Content.Shared.Body.Organ;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.Organs.Stomach;

public abstract class SharedStomachSystem : EntitySystem
{
    [Dependency] protected readonly IConfigurationManager Cfg = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly INetManager Net = default!;
    [Dependency] protected readonly IRobustRandom Random = default!;
    [Dependency] protected readonly SharedStatusEffectsSystem Status = default!;

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

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (Net.IsClient)
            return;

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
            Dirty(uid, stomach);

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
    }

    protected virtual void ApplyVomit(EntityUid body)
    {
    }

    private EntityUid? GetBody(EntityUid organ)
        => TryComp<OrganComponent>(organ, out var organComp) ? organComp.Body : null;
}
