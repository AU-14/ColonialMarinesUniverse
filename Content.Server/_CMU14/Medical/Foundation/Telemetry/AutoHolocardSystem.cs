using Content.Shared._CMU14.Medical.Foundation;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared._RMC14.Medical.HUD;
using Content.Shared._RMC14.Medical.HUD.Components;
using Content.Shared._RMC14.Xenonids.Parasite;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;

namespace Content.Server._CMU14.Medical.Foundation.Telemetry;

/// <summary>
///     Upgrade-only rule: a higher-priority status is never overwritten by a lower one.
/// </summary>
public sealed partial class AutoHolocardSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;

    private bool _medicalEnabled;
    private bool _diagnosticsEnabled;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanMedicalStartupEvent>(OnMedicalStartup);
        SubscribeLocalEvent<HumanMedicalLedgerChangedEvent>(OnLedgerChanged);
        SubscribeLocalEvent<VictimInfectedComponent, ComponentStartup>(OnInfectedSpawn);

        _cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        _cfg.OnValueChanged(CMUMedicalCCVars.DiagnosticsEnabled, v => _diagnosticsEnabled = v, true);
    }

    private void OnMedicalStartup(ref HumanMedicalStartupEvent args)
    {
        ApplyLedgerStatus((args.Body, args.Medical));
    }

    private void OnLedgerChanged(ref HumanMedicalLedgerChangedEvent args)
    {
        if (!TryComp(args.Body, out HumanMedicalComponent? medical))
            return;

        ApplyLedgerStatus((args.Body, medical));
    }

    private void OnInfectedSpawn(Entity<VictimInfectedComponent> ent, ref ComponentStartup args)
    {
        if (!IsEnabled())
            return;

        UpgradeHolocard(ent.Owner, HolocardStatus.Xeno);
    }

    private void ApplyLedgerStatus(Entity<HumanMedicalComponent> patient)
    {
        if (!IsEnabled())
            return;

        if (HasOrganFailure(patient.Comp))
        {
            UpgradeHolocard(patient.Owner, HolocardStatus.OrganFailure);
            return;
        }

        if (HasTrauma(patient.Comp))
            UpgradeHolocard(patient.Owner, HolocardStatus.Trauma);
    }

    private static bool HasOrganFailure(HumanMedicalComponent medical)
    {
        for (var i = 1; i < medical.Organs.Length; i++)
        {
            var organ = medical.Organs[i];
            if (!organ.Missing && organ.Status == OrganDamageStatus.Broken)
                return true;
        }

        return false;
    }

    private static bool HasTrauma(HumanMedicalComponent medical)
    {
        for (var i = 1; i < medical.Regions.Length; i++)
        {
            var region = medical.Regions[i];
            if (region.Presence is LimbPresence.Missing or LimbPresence.Detached)
                return true;

            if (region.Skeletal.Broken && !region.Skeletal.Splinted)
                return true;

            if (region.BruteDamage + region.BurnDamage >= 40)
                return true;
        }

        for (var i = 0; i < medical.Injuries.Count; i++)
        {
            var injury = medical.Injuries[i];
            if (injury.IsOpenStump || injury.Damage >= 25)
                return true;
        }

        for (var i = 0; i < medical.BleedSources.Count; i++)
        {
            if (medical.BleedSources[i].Active)
                return true;
        }

        return false;
    }

    private void UpgradeHolocard(EntityUid body, HolocardStatus newStatus)
    {
        if (!HasComp<HumanMedicalComponent>(body))
            return;

        if (!TryComp<HolocardStateComponent>(body, out var hc))
            return;

        if (Priority(newStatus) <= Priority(hc.HolocardStatus))
            return;

        hc.HolocardStatus = newStatus;
        Dirty(body, hc);
    }

    private static int Priority(HolocardStatus status) => status switch
    {
        HolocardStatus.None => 0,
        HolocardStatus.Stable => 1,
        HolocardStatus.Urgent => 2,
        HolocardStatus.Trauma => 3,
        HolocardStatus.OrganFailure => 4,
        HolocardStatus.Emergency => 5,
        HolocardStatus.Xeno => 6,
        HolocardStatus.Permadead => 7,
        _ => 0,
    };

    private bool IsEnabled()
    {
        return _medicalEnabled && _diagnosticsEnabled;
    }
}
