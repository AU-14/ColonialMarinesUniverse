using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Organs.Events;
using Content.Shared._CMU14.Medical.Organs.Lungs.Events;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.Organs.Lungs;

public abstract class SharedLungsSystem : EntitySystem
{
    [Dependency] protected readonly IConfigurationManager Cfg = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly INetManager Net = default!;
    [Dependency] protected readonly SharedBodySystem Body = default!;
    [Dependency] protected readonly DamageableSystem Damageable = default!;
    [Dependency] protected readonly SharedStatusEffectsSystem Status = default!;

    private static readonly EntProtoId PulmonaryEdema = "StatusEffectCMUPulmonaryEdema";

    private const float AsphyxScanInterval = 1f;
    private float _asphyxScanAccumulator;

    private bool _medicalEnabled;
    private bool _organEnabled;

    private static readonly Dictionary<OrganDamageStage, float> EfficiencyByStage = new()
    {
        { OrganDamageStage.Healthy, 1.0f  },
        { OrganDamageStage.Bruised, 0.85f },
        { OrganDamageStage.Damaged, 0.6f  },
        { OrganDamageStage.Failing, 0.3f  },
        { OrganDamageStage.Dead,    0.0f  },
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LungsComponent, OrganStageChangedEvent>(OnStageChanged);
        SubscribeLocalEvent<LungsComponent, ComponentStartup>(OnLungsStartup);
        SubscribeLocalEvent<CMUHumanMedicalComponent, LungEfficiencyMultiplyEvent>(OnEfficiencyMultiply);

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.OrganEnabled, v => _organEnabled = v, true);
    }

    private void OnLungsStartup(Entity<LungsComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.NextAsphyxTick = Timing.CurTime + TimeSpan.FromSeconds(1);
    }

    private void OnStageChanged(Entity<LungsComponent> ent, ref OrganStageChangedEvent args)
    {
        ent.Comp.Efficiency = EfficiencyByStage[args.New];
        Dirty(ent);

        var body = args.Body;
        if (args.New.IsAtLeast(OrganDamageStage.Damaged))
            Status.TrySetStatusEffectDuration(body, PulmonaryEdema, duration: null);
        else
            Status.TryRemoveStatusEffect(body, PulmonaryEdema);
    }

    private void OnEfficiencyMultiply(Entity<CMUHumanMedicalComponent> ent, ref LungEfficiencyMultiplyEvent args)
    {
        if (!_medicalEnabled || !_organEnabled)
            return;

        var best = -1f;
        foreach (var (organId, _) in Body.GetBodyOrgans(ent))
        {
            if (!TryComp<LungsComponent>(organId, out var lungs))
                continue;
            if (lungs.Efficiency > best)
                best = lungs.Efficiency;
        }

        if (best < 0f)
            return;

        args.Multiplier *= best;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (Net.IsClient)
            return;

        if (!_medicalEnabled || !_organEnabled)
            return;

        _asphyxScanAccumulator += frameTime;
        if (_asphyxScanAccumulator < AsphyxScanInterval)
            return;
        _asphyxScanAccumulator = 0f;

        var now = Timing.CurTime;
        var query = EntityQueryEnumerator<LungsComponent, OrganHealthComponent>();
        while (query.MoveNext(out var uid, out var lungs, out var oh))
        {
            if (lungs.NextAsphyxTick > now)
                continue;
            lungs.NextAsphyxTick = now + TimeSpan.FromSeconds(1);

            if (!lungs.AsphyxPerSecond.TryGetValue(oh.Stage, out var rate) || rate <= FixedPoint2.Zero)
                continue;

            var body = GetBody(uid);
            if (body is null)
                continue;

            if (TryComp<MobStateComponent>(body.Value, out var mob) && mob.CurrentState == MobState.Dead)
                continue;

            ApplyAsphyx(body.Value, uid, rate);
        }
    }

    protected virtual void ApplyAsphyx(EntityUid body, EntityUid lung, FixedPoint2 amount)
    {
    }

    protected EntityUid? GetBody(EntityUid organ)
        => TryComp<OrganComponent>(organ, out var organComp) ? organComp.Body : null;
}
