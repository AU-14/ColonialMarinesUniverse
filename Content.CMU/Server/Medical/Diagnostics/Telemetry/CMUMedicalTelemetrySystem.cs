using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Anatomy.Bones.Events;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.CMU14.Medical.Injuries.Shrapnel;
using Content.Shared.CMU14.Medical.Injuries.Pain;
using Content.Shared.CMU14.Medical.Treatment.Surgery;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Medical.Defibrillator;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared.Body.Part;
using Content.Shared.Damage.Components;
using Content.Shared.GameTicking;
using Robust.Shared.GameObjects;

namespace Content.Server.CMU14.Medical.Diagnostics.Telemetry;

public sealed partial class CMUMedicalTelemetrySystem : EntitySystem
{
    private int _bonesBroken;
    private int _surgeries;
    private int _organCrises;
    private int _painShockEntries;
    private int _defibAttempts;
    private int _severedLimbs;
    private int _internalBleedsStarted;
    private int _internalBleedsStopped;
    private int _shrapnelEmbedded;
    private int _shrapnelExtracted;
    private int _limbsReattached;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FractureSeverityChangedEvent>(OnFractureChanged);
        SubscribeLocalEvent<OrganStageChangedEvent>(OnOrganStage);
        SubscribeLocalEvent<CMSurgeryTargetComponent, CMSurgeryCompleteEvent>(OnSurgeryDone);
        SubscribeLocalEvent<DamageableComponent, RMCDefibrillatorAttemptEvent>(OnDefibAttempt);
        SubscribeLocalEvent<CMUPainShockStatusComponent, ComponentStartup>(OnPainShockEntered);
        SubscribeLocalEvent<BodyPartSeveredEvent>(OnBodyPartSevered);
        SubscribeLocalEvent<InternalBleedingChangedEvent>(OnInternalBleedingChanged);
        SubscribeLocalEvent<Content.Shared.CMU14.Medical.Anatomy.BodyParts.BodyPartHealthComponent, CMUShrapnelChangedEvent>(OnShrapnelChanged);
        SubscribeLocalEvent<RoundEndSummaryStatsEvent>(OnRoundEndStats);

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundEnd);
    }

    private void OnFractureChanged(ref FractureSeverityChangedEvent args)
    {
        // Escalation and recovery belong to the same fracture episode.
        if (args.Old == FractureSeverity.None && args.New != FractureSeverity.None)
            _bonesBroken++;
    }

    private void OnOrganStage(ref OrganStageChangedEvent args)
    {
        if (args.Old < OrganDamageStage.Failing && args.New >= OrganDamageStage.Failing)
            _organCrises++;
    }

    private void OnSurgeryDone(Entity<CMSurgeryTargetComponent> ent, ref CMSurgeryCompleteEvent args)
    {
        _surgeries++;

        if (SharedCMUSurgeryFlowSystem.IsReattachSurgeryId(args.Surgery.Id))
            _limbsReattached++;
    }

    private void OnDefibAttempt(Entity<DamageableComponent> ent, ref RMCDefibrillatorAttemptEvent ev)
    {
        _defibAttempts++;
    }

    private void OnPainShockEntered(Entity<CMUPainShockStatusComponent> ent, ref ComponentStartup args)
    {
        _painShockEntries++;
    }

    private void OnBodyPartSevered(ref BodyPartSeveredEvent args)
    {
        if (args.Type is BodyPartType.Arm or BodyPartType.Leg)
            _severedLimbs++;
    }

    private void OnInternalBleedingChanged(ref InternalBleedingChangedEvent args)
    {
        if (args.Removed)
            _internalBleedsStopped++;
        else
            _internalBleedsStarted++;
    }

    private void OnShrapnelChanged(Entity<Content.Shared.CMU14.Medical.Anatomy.BodyParts.BodyPartHealthComponent> ent, ref CMUShrapnelChangedEvent args)
    {
        if (args.Removed)
            _shrapnelExtracted++;
        else
            _shrapnelEmbedded++;
    }

    private void OnRoundEndStats(RoundEndSummaryStatsEvent ev)
    {
        ev.AddInjuryStat(
            "round-end-summary-window-stat-bones-broken",
            "round-end-summary-window-stat-bones-broken-detail",
            _bonesBroken,
            RoundEndSummaryStatColor.Red);
        ev.AddInjuryStat(
            "round-end-summary-window-stat-surgeries",
            "round-end-summary-window-stat-surgeries-detail",
            _surgeries,
            RoundEndSummaryStatColor.Cyan);
        ev.AddInjuryStat(
            "round-end-summary-window-stat-pain-shock",
            "round-end-summary-window-stat-pain-shock-detail",
            _painShockEntries,
            RoundEndSummaryStatColor.Gold);
        ev.AddInjuryStat(
            "round-end-summary-window-stat-organ-crises",
            "round-end-summary-window-stat-organ-crises-detail",
            _organCrises,
            RoundEndSummaryStatColor.Purple);
        ev.AddInjuryStat(
            "round-end-summary-window-stat-defibs",
            "round-end-summary-window-stat-defibs-detail",
            _defibAttempts,
            RoundEndSummaryStatColor.Green);

        ev.AddOddityStat(
            "round-end-summary-window-stat-limbs-stolen",
            "round-end-summary-window-stat-limbs-stolen-detail",
            _severedLimbs,
            RoundEndSummaryStatColor.Purple);
        ev.AddOddityStat(
            "round-end-summary-window-stat-bleeds-started",
            "round-end-summary-window-stat-bleeds-started-detail",
            _internalBleedsStarted,
            RoundEndSummaryStatColor.Red);
        ev.AddOddityStat(
            "round-end-summary-window-stat-limbs-reattached",
            "round-end-summary-window-stat-limbs-reattached-detail",
            _limbsReattached,
            RoundEndSummaryStatColor.Green);
        ev.AddOddityStat(
            "round-end-summary-window-stat-shrapnel-extracted",
            "round-end-summary-window-stat-shrapnel-extracted-detail",
            _shrapnelExtracted,
            RoundEndSummaryStatColor.Gold);
        ev.AddOddityStat(
            "round-end-summary-window-stat-shrapnel-embedded",
            "round-end-summary-window-stat-shrapnel-embedded-detail",
            _shrapnelEmbedded,
            RoundEndSummaryStatColor.Cyan);
        ev.AddOddityStat(
            "round-end-summary-window-stat-bleeds-stopped",
            "round-end-summary-window-stat-bleeds-stopped-detail",
            _internalBleedsStopped,
            RoundEndSummaryStatColor.Blue);
    }

    private void OnRoundEnd(RoundRestartCleanupEvent ev)
    {
        _bonesBroken = 0;
        _surgeries = 0;
        _organCrises = 0;
        _painShockEntries = 0;
        _defibAttempts = 0;
        _severedLimbs = 0;
        _internalBleedsStarted = 0;
        _internalBleedsStopped = 0;
        _shrapnelEmbedded = 0;
        _shrapnelExtracted = 0;
        _limbsReattached = 0;
    }

}
