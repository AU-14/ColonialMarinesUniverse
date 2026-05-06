using System.Collections.Generic;
using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.Organs.Events;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.Organs.Kidneys;

public abstract class SharedKidneysSystem : EntitySystem
{
    [Dependency] protected readonly IConfigurationManager Cfg = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly INetManager Net = default!;
    [Dependency] protected readonly SharedBodySystem Body = default!;
    [Dependency] protected readonly SharedStatusEffectsSystem Status = default!;

    private static readonly EntProtoId RenalFailure = "StatusEffectCMURenalFailure";
    private const float SelfDamageScanInterval = 1f;
    private float _selfDamageScanAccumulator;

    private bool _medicalEnabled;
    private bool _organEnabled;

    private static readonly Dictionary<OrganDamageStage, float> FiltrationByStage = new()
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
        SubscribeLocalEvent<KidneysComponent, OrganStageChangedEvent>(OnStageChanged);
        SubscribeLocalEvent<KidneysComponent, ComponentStartup>(OnKidneysStartup);

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.OrganEnabled, v => _organEnabled = v, true);
    }

    private void OnKidneysStartup(Entity<KidneysComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.NextSelfDamageTick = Timing.CurTime + TimeSpan.FromSeconds(1);
    }

    private void OnStageChanged(Entity<KidneysComponent> ent, ref OrganStageChangedEvent args)
    {
        ent.Comp.WasteFiltration = FiltrationByStage[args.New];
        Dirty(ent);

        var body = args.Body;
        if (args.New.IsAtLeast(OrganDamageStage.Damaged))
            Status.TrySetStatusEffectDuration(body, RenalFailure, duration: null);
        else
            Status.TryRemoveStatusEffect(body, RenalFailure);
    }

    /// <summary>
    ///     Pair survival via <c>Math.Max</c> across all kidneys. Missing-kidney
    ///     bodies return 1.0 unchanged.
    /// </summary>
    public float GetClearanceMultiplier(EntityUid body)
    {
        var best = -1f;
        foreach (var (organId, _) in Body.GetBodyOrgans(body))
        {
            if (!TryComp<KidneysComponent>(organId, out var kidney))
                continue;
            if (kidney.WasteFiltration > best)
                best = kidney.WasteFiltration;
        }

        return best < 0f ? 1.0f : best;
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
        var query = EntityQueryEnumerator<KidneysComponent, OrganHealthComponent>();
        while (query.MoveNext(out var uid, out var kidneys, out var oh))
        {
            if (kidneys.NextSelfDamageTick > now)
                continue;
            kidneys.NextSelfDamageTick = now + TimeSpan.FromSeconds(1);

            if (!kidneys.ToxinPerSecond.TryGetValue(oh.Stage, out var rate) || rate <= FixedPoint2.Zero)
                continue;

            var body = GetBody(uid);
            if (body is null)
                continue;

            if (TryComp<MobStateComponent>(body.Value, out var mob) && mob.CurrentState == MobState.Dead)
                continue;

            ApplyToxin(body.Value, uid, rate);
        }
    }

    protected virtual void ApplyToxin(EntityUid body, EntityUid kidneys, FixedPoint2 amount)
    {
    }

    private EntityUid? GetBody(EntityUid organ)
        => TryComp<OrganComponent>(organ, out var organComp) ? organComp.Body : null;
}
