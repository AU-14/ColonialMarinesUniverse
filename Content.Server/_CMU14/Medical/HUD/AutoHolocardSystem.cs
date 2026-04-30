using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.Bones;
using Content.Shared._CMU14.Medical.Bones.Events;
using Content.Shared._CMU14.Medical.Organs;
using Content.Shared._CMU14.Medical.Organs.Events;
using Content.Shared._CMU14.Medical.Wounds;
using Content.Shared._RMC14.Medical.HUD;
using Content.Shared._RMC14.Medical.HUD.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.Server._CMU14.Medical.HUD;

/// <summary>
///     "Upgrade-only" rule: a higher-priority status (manually-set
///     Permadead, Emergency, Urgent) is never overwritten by a lower-byte
///     auto-status. Byte ordering of the appended enum values puts
///     Stable / Trauma / OrganFailure above manual statuses on the byte
///     axis, so we can't naively pick the higher byte — see <see cref="Priority"/>.
/// </summary>
public sealed class AutoHolocardSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;

    private bool _medicalEnabled;
    private bool _diagnosticsEnabled;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FractureComponent, ComponentStartup>(OnFractureSpawn);
        SubscribeLocalEvent<InternalBleedingComponent, ComponentStartup>(OnInternalBleedSpawn);
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

    private void OnOrganStageBroadcast(ref OrganStageChangedEvent args)
    {
        if (!IsEnabled())
            return;
        if (!args.New.IsAtLeast(OrganDamageStage.Failing))
            return;
        UpgradeHolocard(args.Body, HolocardStatus.OrganFailure);
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
