using Content.Shared._CMU14.Medical.Foundation;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared.Body.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.Human.Organs.Stomach;

public abstract partial class SharedStomachSystem : EntitySystem
{
    [Dependency] protected IConfigurationManager Cfg = default!;
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected INetManager Net = default!;
    [Dependency] protected IRobustRandom Random = default!;
    [Dependency] protected SharedBodySystem Body = default!;
    [Dependency] protected SharedStatusEffectsSystem Status = default!;

    private static readonly EntProtoId Nausea = "StatusEffectCMUNausea";
    private const float StomachScanInterval = 1f;
    private float _stomachScanAccumulator;

    private bool _medicalEnabled;
    private bool _organEnabled;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HumanMedicalLedgerChangedEvent>(OnLedgerChanged);
        SubscribeLocalEvent<CMUStomachComponent, ComponentStartup>(OnStomachStartup);

        Cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        Cfg.OnValueChanged(CMUMedicalCCVars.OrganEnabled, v => _organEnabled = v, true);
    }

    private void OnStomachStartup(Entity<CMUStomachComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.NextVomitCheck = Timing.CurTime + ent.Comp.VomitCheckInterval;
    }

    private void OnLedgerChanged(ref HumanMedicalLedgerChangedEvent args)
    {
        if (!HumanOrganLedgerUtility.OrgansChanged(args.Result))
            return;

        if (!TryComp(args.Body, out HumanMedicalComponent? medical))
            return;

        var status = HumanOrganLedgerUtility.EffectiveStatus(medical, OrganSlot.Stomach);
        if (status >= OrganDamageStatus.Bruised)
            Status.TrySetStatusEffectDuration(args.Body, Nausea, duration: null);
        else
            Status.TryRemoveStatusEffect(args.Body, Nausea);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (Net.IsClient || !_medicalEnabled || !_organEnabled)
            return;

        _stomachScanAccumulator += frameTime;
        if (_stomachScanAccumulator < StomachScanInterval)
            return;

        _stomachScanAccumulator = 0f;
        var now = Timing.CurTime;
        var query = EntityQueryEnumerator<HumanMedicalComponent, ActiveOrganSymptomsComponent>();
        while (query.MoveNext(out var uid, out var medical, out _))
        {
            var status = HumanOrganLedgerUtility.EffectiveStatus(medical, OrganSlot.Stomach);
            if (status == OrganDamageStatus.None)
                continue;

            foreach (var stomach in Body.GetBodyOrganEntityComps<CMUStomachComponent>(uid))
            {
                if (stomach.Comp1.NextVomitCheck > now)
                    continue;

                stomach.Comp1.NextVomitCheck = now + stomach.Comp1.VomitCheckInterval;
                Dirty(stomach.Owner, stomach.Comp1);

                if (!stomach.Comp1.VomitChance.TryGetValue(status, out var chance) || chance <= 0f)
                    continue;
                if (!Random.Prob(chance))
                    continue;
                if (TryComp<MobStateComponent>(uid, out var mob) && mob.CurrentState == MobState.Dead)
                    continue;

                ApplyVomit(uid);
            }
        }
    }

    protected virtual void ApplyVomit(EntityUid body)
    {
    }
}
