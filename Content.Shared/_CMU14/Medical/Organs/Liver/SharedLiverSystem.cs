using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Organs.Events;
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

namespace Content.Shared._CMU14.Medical.Organs.Liver;

public abstract class SharedLiverSystem : EntitySystem
{
    [Dependency] protected readonly IConfigurationManager Cfg = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly INetManager Net = default!;
    [Dependency] protected readonly SharedBodySystem Body = default!;
    [Dependency] protected readonly DamageableSystem Damageable = default!;
    [Dependency] protected readonly SharedStatusEffectsSystem Status = default!;

    private static readonly EntProtoId HepaticFailure = "StatusEffectCMUHepaticFailure";

    private const float SelfDamageScanInterval = 1f;
    private float _selfDamageScanAccumulator;

    private bool _medicalEnabled;
    private bool _organEnabled;

    private static readonly Dictionary<OrganDamageStage, float> ClearByStage = new()
    {
        { OrganDamageStage.Healthy, 1.0f },
        { OrganDamageStage.Bruised, 0.8f },
        { OrganDamageStage.Damaged, 0.5f },
        { OrganDamageStage.Failing, 0.2f },
        { OrganDamageStage.Dead,    0.0f },
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LiverComponent, OrganStageChangedEvent>(OnStageChanged);
        SubscribeLocalEvent<LiverComponent, ComponentStartup>(OnLiverStartup);

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.OrganEnabled, v => _organEnabled = v, true);
    }

    private void OnLiverStartup(Entity<LiverComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.NextSelfDamageTick = Timing.CurTime + TimeSpan.FromSeconds(1);
    }

    private void OnStageChanged(Entity<LiverComponent> ent, ref OrganStageChangedEvent args)
    {
        ent.Comp.ToxinClearMultiplier = ClearByStage[args.New];
        Dirty(ent);

        var body = args.Body;
        if (args.New.IsAtLeast(OrganDamageStage.Damaged))
            Status.TrySetStatusEffectDuration(body, HepaticFailure, duration: null);
        else
            Status.TryRemoveStatusEffect(body, HepaticFailure);
    }

    /// <summary>
    ///     Returns the worst-stage liver's clearance multiplier (1.0 if no liver
    ///     organs are present).
    /// </summary>
    public float GetClearanceMultiplier(EntityUid body)
    {
        var worst = -1f;
        foreach (var (organId, _) in Body.GetBodyOrgans(body))
        {
            if (!TryComp<LiverComponent>(organId, out var liver))
                continue;
            // Worst liver wins — a single failed liver poisons the system even
            // if a hypothetical second liver still works.
            if (worst < 0f || liver.ToxinClearMultiplier < worst)
                worst = liver.ToxinClearMultiplier;
        }

        return worst < 0f ? 1.0f : worst;
    }

    public void ApplyBloodstreamDirectDamage(EntityUid body, string group)
    {
        if (group != "Poison" && group != "Alcohol")
            return;

        foreach (var (organId, _) in Body.GetBodyOrgans(body))
        {
            if (!HasComp<LiverComponent>(organId))
                continue;
            ApplyBloodstreamDirectHit(body, organId, group);
        }
    }

    protected virtual void ApplyBloodstreamDirectHit(EntityUid body, EntityUid liver, string group)
    {
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (Net.IsClient)
            return;

        if (!_medicalEnabled || !_organEnabled)
            return;

        _selfDamageScanAccumulator += frameTime;
        if (_selfDamageScanAccumulator < SelfDamageScanInterval)
            return;
        _selfDamageScanAccumulator = 0f;

        var now = Timing.CurTime;
        var query = EntityQueryEnumerator<LiverComponent, OrganHealthComponent>();
        while (query.MoveNext(out var uid, out var liver, out var oh))
        {
            if (liver.NextSelfDamageTick > now)
                continue;
            liver.NextSelfDamageTick = now + TimeSpan.FromSeconds(1);

            if (!liver.ToxinPerSecond.TryGetValue(oh.Stage, out var rate) || rate <= FixedPoint2.Zero)
                continue;

            var body = GetBody(uid);
            if (body is null)
                continue;

            if (TryComp<MobStateComponent>(body.Value, out var mob) && mob.CurrentState == MobState.Dead)
                continue;

            ApplyToxin(body.Value, uid, rate);
        }
    }

    protected virtual void ApplyToxin(EntityUid body, EntityUid liver, FixedPoint2 amount)
    {
    }

    protected EntityUid? GetBody(EntityUid organ)
        => TryComp<OrganComponent>(organ, out var organComp) ? organComp.Body : null;
}
