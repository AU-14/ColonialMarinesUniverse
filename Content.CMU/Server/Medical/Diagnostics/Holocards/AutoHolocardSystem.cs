using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Anatomy.Bones;
using Content.Shared._CMU14.Medical.Anatomy.Organs;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Medical.HUD;
using Content.Shared._RMC14.Medical.HUD.Components;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Body.Part;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;

namespace Content.Server._CMU14.Medical.Diagnostics.Holocards;

/// <summary>
///     Automatically upgrades holocards for severe injuries without replacing
///     higher-priority statuses. Automatic injury statuses clear once the patient
///     has no remaining fracture, internal bleeding, or organ failure.
/// </summary>
public sealed partial class AutoHolocardSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;

    private const CMUMedicalChangeFlags IndicatorChanges =
        CMUMedicalChangeFlags.Anatomy |
        CMUMedicalChangeFlags.Fractures |
        CMUMedicalChangeFlags.Organs |
        CMUMedicalChangeFlags.Wounds;

    private bool _medicalEnabled;
    private bool _diagnosticsEnabled;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FractureComponent, ComponentStartup>(OnFractureSpawn);
        SubscribeLocalEvent<InternalBleedingComponent, ComponentStartup>(OnInternalBleedSpawn);
        SubscribeLocalEvent<VictimInfectedComponent, ComponentStartup>(OnInfectedSpawn);
        SubscribeLocalEvent<CMUMedicalChangedEvent>(OnMedicalChanged);
        // Broadcast subscription — <OrganHealthComponent, OrganStageChangedEvent>
        // is already owned by SharedCMUWoundsSystem and SS14's directed bus
        // enforces one handler per (component, event). Broadcast delivery
        // is a separate slot and accepts multiple subscribers.
        SubscribeLocalEvent<OrganStageChangedEvent>(OnOrganStageBroadcast);

        _cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        _cfg.OnValueChanged(CMUMedicalCCVars.DiagnosticsEnabled, v => _diagnosticsEnabled = v, true);
    }

    private void OnFractureSpawn(Entity<FractureComponent> ent, ref ComponentStartup args)
    {
        if (!IsEnabled())
            return;
        if (TryGetBodyForPart(ent.Owner) is { } body)
            UpgradeHolocard(body, HolocardStatus.Trauma);
    }

    private void OnInternalBleedSpawn(Entity<InternalBleedingComponent> ent, ref ComponentStartup args)
    {
        if (!IsEnabled())
            return;
        if (TryGetBodyForPart(ent.Owner) is { } body)
            UpgradeHolocard(body, HolocardStatus.Trauma);
    }

    private void OnInfectedSpawn(Entity<VictimInfectedComponent> ent, ref ComponentStartup args)
    {
        if (!IsEnabled())
            return;
        UpgradeHolocard(ent.Owner, HolocardStatus.Xeno);
    }

    private void OnOrganStageBroadcast(ref OrganStageChangedEvent args)
    {
        if (!IsEnabled())
            return;
        if (!args.New.IsAtLeast(OrganDamageStage.Failing))
            return;
        UpgradeHolocard(args.Body, HolocardStatus.OrganFailure);
    }

    private void OnMedicalChanged(ref CMUMedicalChangedEvent args)
    {
        if (!IsEnabled() || (args.Changes & IndicatorChanges) == CMUMedicalChangeFlags.None)
            return;

        var status = GetAutomaticStatus(args.Body);
        if (status != HolocardStatus.None)
        {
            UpgradeHolocard(args.Body, status);
            return;
        }

        ClearAutomaticHolocard(args.Body);
    }

    private HolocardStatus GetAutomaticStatus(EntityUid body)
    {
        foreach (var (organUid, _) in _medicalIndex.GetOrgans(body))
        {
            if (TryComp<OrganHealthComponent>(organUid, out var organ) &&
                organ.Stage.IsAtLeast(OrganDamageStage.Failing))
            {
                return HolocardStatus.OrganFailure;
            }
        }

        foreach (var (partUid, _) in _medicalIndex.GetBodyParts(body))
        {
            if (HasComp<FractureComponent>(partUid) || HasComp<InternalBleedingComponent>(partUid))
                return HolocardStatus.Trauma;
        }

        return HolocardStatus.None;
    }

    private void ClearAutomaticHolocard(EntityUid body)
    {
        if (!TryComp<HolocardStateComponent>(body, out var holocard) ||
            holocard.HolocardStatus is not (HolocardStatus.Trauma or HolocardStatus.OrganFailure))
        {
            return;
        }

        holocard.HolocardStatus = HolocardStatus.None;
        Dirty(body, holocard);
    }

    private EntityUid? TryGetBodyForPart(EntityUid part)
    {
        if (TryComp<BodyPartComponent>(part, out var partComp))
            return partComp.Body;
        return null;
    }

    private void UpgradeHolocard(EntityUid body, HolocardStatus newStatus)
    {
        if (!HasComp<CMUHumanMedicalComponent>(body))
            return;
        if (!TryComp<HolocardStateComponent>(body, out var hc))
            return;

        if (Priority(newStatus) <= Priority(hc.HolocardStatus))
            return;

        hc.HolocardStatus = newStatus;
        Dirty(body, hc);
    }

    /// <summary>
    ///     Clinical-severity ordering. The enum's byte order is
    ///     append-driven (None=0, Urgent=1, Emergency=2, Xeno=3,
    ///     Permadead=4, Stable=5, Trauma=6, OrganFailure=7) — that's not
    ///     the upgrade ladder we want, so map explicitly.
    /// </summary>
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
