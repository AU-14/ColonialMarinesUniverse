using Content.Shared._CMU14.Medical.Foundation;
using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Systems;
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

namespace Content.Shared._CMU14.Medical.Human.Organs.Kidneys;

public abstract partial class SharedKidneysSystem : EntitySystem
{
    [Dependency] protected IConfigurationManager Cfg = default!;
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected INetManager Net = default!;
    [Dependency] protected SharedBodySystem Body = default!;
    [Dependency] protected SharedStatusEffectsSystem Status = default!;

    private static readonly EntProtoId RenalFailure = "StatusEffectCMURenalFailure";
    private const float SelfDamageScanInterval = 1f;
    private float _selfDamageScanAccumulator;

    private bool _medicalEnabled;
    private bool _organEnabled;

    private static readonly Dictionary<OrganDamageStatus, float> FiltrationByStatus = new()
    {
        { OrganDamageStatus.None, 1.0f },
        { OrganDamageStatus.LittleBruised, 0.85f },
        { OrganDamageStatus.Bruised, 0.6f },
        { OrganDamageStatus.Broken, 0.3f },
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HumanMedicalLedgerChangedEvent>(OnLedgerChanged);
        SubscribeLocalEvent<KidneysComponent, ComponentStartup>(OnKidneysStartup);

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.OrganEnabled, v => _organEnabled = v, true);
    }

    private void OnKidneysStartup(Entity<KidneysComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.NextSelfDamageTick = Timing.CurTime + TimeSpan.FromSeconds(1);
    }

    private void OnLedgerChanged(ref HumanMedicalLedgerChangedEvent args)
    {
        if (!HumanOrganLedgerUtility.OrgansChanged(args.Result))
            return;

        if (!TryComp(args.Body, out HumanMedicalComponent? medical))
            return;

        var status = HumanOrganLedgerUtility.EffectiveStatus(medical, OrganSlot.Kidneys);
        var multiplier = GetFiltrationMultiplier(status);
        foreach (var kidneys in Body.GetBodyOrganEntityComps<KidneysComponent>(args.Body))
        {
            kidneys.Comp1.WasteFiltration = multiplier;
            Dirty(kidneys.Owner, kidneys.Comp1);
        }

        if (status >= OrganDamageStatus.Bruised)
            Status.TrySetStatusEffectDuration(args.Body, RenalFailure, duration: null);
        else
            Status.TryRemoveStatusEffect(args.Body, RenalFailure);
    }

    public float GetClearanceMultiplier(EntityUid body)
    {
        if (TryComp<HumanMedicalComponent>(body, out var medical))
            return GetFiltrationMultiplier(HumanOrganLedgerUtility.EffectiveStatus(medical, OrganSlot.Kidneys));

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
            var status = HumanOrganLedgerUtility.EffectiveStatus(medical, OrganSlot.Kidneys);
            if (status == OrganDamageStatus.None)
                continue;

            foreach (var kidneys in Body.GetBodyOrganEntityComps<KidneysComponent>(uid))
            {
                if (kidneys.Comp1.NextSelfDamageTick > now)
                    continue;

                kidneys.Comp1.NextSelfDamageTick = now + TimeSpan.FromSeconds(1);
                Dirty(kidneys.Owner, kidneys.Comp1);

                if (!kidneys.Comp1.ToxinPerSecond.TryGetValue(status, out var rate) || rate <= FixedPoint2.Zero)
                    continue;
                if (TryComp<MobStateComponent>(uid, out var mob) && mob.CurrentState == MobState.Dead)
                    continue;

                ApplyToxin(uid, kidneys.Owner, rate);
            }
        }
    }

    protected virtual void ApplyToxin(EntityUid body, EntityUid kidneys, FixedPoint2 amount)
    {
    }

    private static float GetFiltrationMultiplier(OrganDamageStatus status)
    {
        return FiltrationByStatus.TryGetValue(status, out var multiplier)
            ? multiplier
            : 1.0f;
    }
}
