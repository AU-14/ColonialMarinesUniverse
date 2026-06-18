using Content.Shared._CMU14.Medical.Foundation;
using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Systems;
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

namespace Content.Shared._CMU14.Medical.Human.Organs.Liver;

public abstract partial class SharedLiverSystem : EntitySystem
{
    [Dependency] protected IConfigurationManager Cfg = default!;
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected INetManager Net = default!;
    [Dependency] protected SharedBodySystem Body = default!;
    [Dependency] protected DamageableSystem Damageable = default!;
    [Dependency] protected SharedStatusEffectsSystem Status = default!;

    private static readonly EntProtoId HepaticFailure = "StatusEffectCMUHepaticFailure";

    private const float SelfDamageScanInterval = 1f;
    private float _selfDamageScanAccumulator;

    private bool _medicalEnabled;
    private bool _organEnabled;

    private static readonly Dictionary<OrganDamageStatus, float> ClearByStatus = new()
    {
        { OrganDamageStatus.None, 1.0f },
        { OrganDamageStatus.LittleBruised, 0.8f },
        { OrganDamageStatus.Bruised, 0.5f },
        { OrganDamageStatus.Broken, 0.2f },
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HumanMedicalLedgerChangedEvent>(OnLedgerChanged);
        SubscribeLocalEvent<LiverComponent, ComponentStartup>(OnLiverStartup);

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.OrganEnabled, v => _organEnabled = v, true);
    }

    private void OnLiverStartup(Entity<LiverComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.NextSelfDamageTick = Timing.CurTime + TimeSpan.FromSeconds(1);
    }

    private void OnLedgerChanged(ref HumanMedicalLedgerChangedEvent args)
    {
        if (!HumanOrganLedgerUtility.OrgansChanged(args.Result))
            return;

        if (!TryComp(args.Body, out HumanMedicalComponent? medical))
            return;

        var status = HumanOrganLedgerUtility.EffectiveStatus(medical, OrganSlot.Liver);
        var multiplier = GetClearanceMultiplier(status);
        foreach (var liver in Body.GetBodyOrganEntityComps<LiverComponent>(args.Body))
        {
            liver.Comp1.ToxinClearMultiplier = multiplier;
            Dirty(liver.Owner, liver.Comp1);
        }

        if (status >= OrganDamageStatus.Bruised)
            Status.TrySetStatusEffectDuration(args.Body, HepaticFailure, duration: null);
        else
            Status.TryRemoveStatusEffect(args.Body, HepaticFailure);
    }

    public float GetClearanceMultiplier(EntityUid body)
    {
        if (TryComp<HumanMedicalComponent>(body, out var medical))
            return GetClearanceMultiplier(HumanOrganLedgerUtility.EffectiveStatus(medical, OrganSlot.Liver));

        return 1.0f;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (Net.IsClient || !_medicalEnabled || !_organEnabled)
            return;

        _selfDamageScanAccumulator += frameTime;
        if (_selfDamageScanAccumulator < SelfDamageScanInterval)
            return;

        _selfDamageScanAccumulator = 0f;
        var now = Timing.CurTime;
        var query = EntityQueryEnumerator<HumanMedicalComponent, ActiveOrganSymptomsComponent>();
        while (query.MoveNext(out var uid, out var medical, out _))
        {
            var status = HumanOrganLedgerUtility.EffectiveStatus(medical, OrganSlot.Liver);
            if (status == OrganDamageStatus.None)
                continue;

            foreach (var liver in Body.GetBodyOrganEntityComps<LiverComponent>(uid))
            {
                if (liver.Comp1.NextSelfDamageTick > now)
                    continue;

                liver.Comp1.NextSelfDamageTick = now + TimeSpan.FromSeconds(1);
                Dirty(liver.Owner, liver.Comp1);

                if (!liver.Comp1.ToxinPerSecond.TryGetValue(status, out var rate) || rate <= FixedPoint2.Zero)
                    continue;
                if (TryComp<MobStateComponent>(uid, out var mob) && mob.CurrentState == MobState.Dead)
                    continue;

                ApplyToxin(uid, liver.Owner, rate);
            }
        }
    }

    protected virtual void ApplyToxin(EntityUid body, EntityUid liver, FixedPoint2 amount)
    {
    }

    private static float GetClearanceMultiplier(OrganDamageStatus status)
    {
        return ClearByStatus.TryGetValue(status, out var multiplier)
            ? multiplier
            : 1.0f;
    }
}
