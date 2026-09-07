using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.Destructible;
using Content.Shared.CMU14.Medical.Treatment.Surgery;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DragDrop;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Movement.Events;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Medical.Treatment.Surgery;

public sealed partial class CMUAutodocSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private CMUSurgerySystem _cmuSurgery = default!;
    [Dependency] private CMUSurgeryDispatchSystem _dispatch = default!;
    [Dependency] private CMUSurgeryFlowSystem _flow = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private CMUSurgerySessionSystem _sessions = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private CMUMedicalPatientBaySystem _patientBay = default!;
    [Dependency] private SharedBodyPartHealthSystem _partHealth = default!;
    [Dependency] private CMUSurgeryRulebookSystem _rulebook = default!;
    [Dependency] private CMUMedicalSchedulerSystem _scheduler = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private SharedCMUWoundsSystem _wounds = default!;
    [Dependency] private CMUWoundLedgerSystem _woundLedger = default!;

    private static readonly EntProtoId<SkillDefinitionComponent> SurgerySkill = "RMCSkillSurgery";
    private const string AutodocLimbRegenerationId = "CMUAutodocRegenerateLimb";
    private const string AutodocLimbRegenerationCategory = "limb_regeneration";
    private const string AutodocWoundRepairId = "CMUAutodocRepairWounds";
    private const string AutodocWoundRepairCategory = "wound_repair";
    private const float DefaultProcedureSeconds = 45f;
    private const float LimbRegenerationSeconds = 90f;
    private static readonly CMUMedicalWorkKey ProcedureStepWork = new("autodoc-procedure-step");
    private static readonly ProtoId<DamageGroupPrototype> BruteGroup = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> BurnGroup = "Burn";

    private static readonly FrozenDictionary<string, SoundSpecifier> ProcedureSounds =
        new Dictionary<string, SoundSpecifier>
        {
            [AutodocWoundRepairCategory] = new SoundCollectionSpecifier("RMCSurgeryScalpel"),
            [AutodocLimbRegenerationCategory] = new SoundCollectionSpecifier("RMCSurgeryOrgan"),
            ["bleed"] = new SoundCollectionSpecifier("RMCSurgeryHemostat"),
            ["fracture"] = new SoundCollectionSpecifier("RMCSurgerySplint"),
            ["head_organ"] = new SoundCollectionSpecifier("RMCSurgeryOrgan"),
            ["suture"] = new SoundCollectionSpecifier("RMCSurgeryOrgan"),
        }.ToFrozenDictionary();

    private readonly HashSet<EntityUid> _openConsoles = new();
    private readonly List<EntityUid> _staleConsoles = new();
    private float _uiAccumulator;
    private ulong _nextOccupantGeneration;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<CMUAutodocConsoleComponent>(CMUAutodocUIKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<BoundUIClosedEvent>(OnUiClosed);
            subs.Event<CMUAutodocQueueStepMessage>(OnQueueStep);
            subs.Event<CMUAutodocRemoveQueueStepMessage>(OnRemoveQueueStep);
            subs.Event<CMUAutodocClearQueueMessage>(OnClearQueue);
            subs.Event<CMUAutodocStartMessage>(OnStart);
            subs.Event<CMUAutodocStopMessage>(OnStop);
            subs.Event<CMUAutodocEjectPatientMessage>(OnEjectPatient);
        });

        SubscribeLocalEvent<CMUAutodocPodComponent, ComponentInit>(OnPodInit);
        SubscribeLocalEvent<CMUAutodocPodComponent, ComponentShutdown>(OnPodShutdown);
        SubscribeLocalEvent<CMUAutodocPodComponent, EntInsertedIntoContainerMessage>(OnPatientInserted);
        SubscribeLocalEvent<CMUAutodocPodComponent, EntRemovedFromContainerMessage>(OnPatientRemoved);
        SubscribeLocalEvent<CMUAutodocPodComponent, DestructionEventArgs>(OnPodDestroyed);
        SubscribeLocalEvent<CMUAutodocPodComponent, DragDropTargetEvent>(OnPodDragDrop);
        SubscribeLocalEvent<CMUAutodocPodComponent, GetVerbsEvent<AlternativeVerb>>(OnPodAlternativeVerbs);
        SubscribeLocalEvent<CMUAutodocPodComponent, ContainerRelayMovementEntityEvent>(OnPodRelayMovement);
        SubscribeLocalEvent<CMUAutodocPodComponent, CMUMedicalPodInsertDoAfterEvent>(OnPodInsertDoAfter);
        SubscribeLocalEvent<CMUAutodocPodComponent, CMUMedicalWorkDueEvent>(OnProcedureStepDue);
        SubscribeLocalEvent<CMUAutodocConsoleComponent, ComponentShutdown>(OnConsoleShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _uiAccumulator += frameTime;
        if (_uiAccumulator < 1f)
            return;

        _uiAccumulator = 0f;
        _staleConsoles.Clear();
        foreach (var console in _openConsoles)
        {
            if (!TryComp<CMUAutodocConsoleComponent>(console, out var comp) ||
                !_ui.IsUiOpen(console, CMUAutodocUIKey.Key))
            {
                _staleConsoles.Add(console);
                continue;
            }

            RefreshUi(console, comp);
        }

        foreach (var console in _staleConsoles)
            _openConsoles.Remove(console);
    }

    private void OnProcedureStepDue(
        Entity<CMUAutodocPodComponent> ent,
        ref CMUMedicalWorkDueEvent args)
    {
        if (args.Key != ProcedureStepWork || !ent.Comp.IsRunning || ent.Comp.NextStepAt == TimeSpan.Zero)
            return;

        if (ent.Comp.NextStepAt > _timing.CurTime)
        {
            _scheduler.Schedule(ent.Owner, ProcedureStepWork, ent.Comp.NextStepAt);
            return;
        }

        ProcessPod(ent.Owner, ent.Comp);
    }

    private void OnUiOpened(Entity<CMUAutodocConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        _openConsoles.Add(ent.Owner);
        RefreshUi(ent.Owner, ent.Comp, args.Actor);
    }

    private void OnUiClosed(Entity<CMUAutodocConsoleComponent> ent, ref BoundUIClosedEvent args)
    {
        if (!_ui.IsUiOpen(ent.Owner, CMUAutodocUIKey.Key))
            _openConsoles.Remove(ent.Owner);
    }

    private void OnConsoleShutdown(Entity<CMUAutodocConsoleComponent> ent, ref ComponentShutdown args)
    {
        _openConsoles.Remove(ent.Owner);
    }

    private void OnQueueStep(Entity<CMUAutodocConsoleComponent> ent, ref CMUAutodocQueueStepMessage msg)
    {
        if (!TryValidateCommand(ent, msg.Actor, msg.Context, out var pod, out var podComp, out var patient))
            return;

        // Bound both stored work and the replicated queue before computing eligibility.
        if (podComp.Queue.Count >= CMUAutodocPodComponent.MaximumQueueEntries)
            return;

        foreach (var queued in podComp.Queue)
        {
            if (queued.Type == msg.TargetPartType && queued.Symmetry == msg.TargetSymmetry &&
                queued.SurgeryId == msg.SurgeryId)
                return;
        }

        var parts = BuildAutodocPartEntries(patient, msg.Actor);
        foreach (var part in parts)
        {
            if (part.Part != msg.Part || part.Type != msg.TargetPartType || part.Symmetry != msg.TargetSymmetry)
                continue;

            foreach (var surgery in part.EligibleSurgeries)
            {
                if (surgery.SurgeryId != msg.SurgeryId || surgery.NextStepIndex != msg.StepIndex)
                    continue;

                var targetPart = GetEntity(msg.Part);
                if (!HasComp<BodyPartComponent>(targetPart))
                    targetPart = patient;

                EntityUid? anchor = null;
                string? slot = null;
                if (surgery.SurgeryId == AutodocLimbRegenerationId)
                {
                    if (!_cmuSurgery.TryGetMissingPartSite(patient, part.Type, part.Symmetry, out var missingAnchor, out var missingSlot))
                        return;
                    anchor = missingAnchor;
                    slot = missingSlot;
                }

                podComp.Queue.Add(new CMUAutodocQueuedStep(
                    targetPart,
                    msg.TargetPartType,
                    msg.TargetSymmetry,
                    surgery.SurgeryId,
                    surgery.DisplayName,
                    surgery.Category,
                    surgery.NextStepIndex,
                    "cmu-autodoc-automated-step-label",
                    part.DisplayName,
                    surgery.AutodocDurationSeconds ?? GetProcedureDurationSeconds(surgery),
                    ++podComp.NextQueueEntryId,
                    _rulebook.GetProcedureOrgan(targetPart, surgery.SurgeryId),
                    anchor,
                    slot));
                podComp.StateRevision++;
                RefreshLinkedConsoles(pod);
                return;
            }
        }
    }

    private void OnRemoveQueueStep(Entity<CMUAutodocConsoleComponent> ent, ref CMUAutodocRemoveQueueStepMessage msg)
    {
        if (!TryValidateCommand(ent, msg.Actor, msg.Context, out var pod, out var podComp, out var patient))
            return;

        var index = -1;
        for (var i = 0; i < podComp.Queue.Count; i++)
        {
            if (podComp.Queue[i].Id != msg.EntryId)
                continue;

            index = i;
            break;
        }
        if (index < 0)
            return;

        podComp.Queue.RemoveAt(index);
        podComp.StateRevision++;
        if (podComp.Queue.Count == 0)
            StopPod(pod, podComp);
        else if (podComp.IsRunning && index == 0)
            StartProcedureTimer(pod, patient, podComp, podComp.Queue[0]);
        RefreshLinkedConsoles(pod);
    }

    private void OnClearQueue(Entity<CMUAutodocConsoleComponent> ent, ref CMUAutodocClearQueueMessage msg)
    {
        if (!TryValidateCommand(ent, msg.Actor, msg.Context, out var pod, out var podComp, out _))
            return;

        StopPod(pod, podComp);
        if (podComp.Queue.Count > 0)
        {
            podComp.Queue.Clear();
            podComp.StateRevision++;
        }
        RefreshLinkedConsoles(pod);
    }

    private void OnStart(Entity<CMUAutodocConsoleComponent> ent, ref CMUAutodocStartMessage msg)
    {
        if (!TryValidateCommand(ent, msg.Actor, msg.Context, out var pod, out var podComp, out var patient)
            || podComp.IsRunning || podComp.Queue.Count == 0)
        {
            return;
        }

        podComp.Operator = msg.Actor;
        podComp.IsRunning = true;
        podComp.StateRevision++;
        StartProcedureTimer(pod, patient, podComp, podComp.Queue[0]);
        _appearance.SetData(pod, CMUAutodocVisuals.Operating, true);
        RefreshLinkedConsoles(pod);
    }

    private void OnStop(Entity<CMUAutodocConsoleComponent> ent, ref CMUAutodocStopMessage msg)
    {
        if (!TryValidateCommand(ent, msg.Actor, msg.Context, out var pod, out var podComp, out _))
            return;

        StopPod(pod, podComp);
        RefreshLinkedConsoles(pod);
    }

    private void OnEjectPatient(Entity<CMUAutodocConsoleComponent> ent, ref CMUAutodocEjectPatientMessage msg)
    {
        if (!TryValidateCommand(ent, msg.Actor, msg.Context, out var pod, out var podComp, out _))
        {
            return;
        }

        EjectPatient(pod, podComp);
        RefreshLinkedConsoles(pod);
    }

    private bool TryValidateCommand(
        Entity<CMUAutodocConsoleComponent> console,
        EntityUid actor,
        CMUAutodocCommandContext context,
        out EntityUid pod,
        out CMUAutodocPodComponent podComp,
        out EntityUid patient)
    {
        pod = default;
        podComp = default!;
        patient = default;
        if (!CanControl(actor) || !_interaction.InRangeAndAccessible(actor, console.Owner))
            return false;

        if (TryFindLinkedPod(console.Owner, console.Comp, out pod, out podComp) && !TerminatingOrDeleted(pod) &&
            TryGetPatient(pod, out patient) && podComp.Patient == patient &&
            context.Pod == GetNetEntity(pod) && context.Patient == GetNetEntity(patient) &&
            context.OccupantGeneration == podComp.OccupantGeneration &&
            context.StateRevision == podComp.StateRevision)
            return true;

        // A delayed or concurrent command never targets the replacement occupant or row.
        RefreshUi(console.Owner, console.Comp, actor);
        return false;
    }

    private void ProcessPod(EntityUid pod, CMUAutodocPodComponent comp)
    {
        if (comp.Queue.Count == 0 || !CanControl(comp.Operator) ||
            !TryGetPatient(pod, out var patient) || comp.Patient != patient)
        {
            StopPod(pod, comp);
            RefreshLinkedConsoles(pod);
            return;
        }

        if (_sessions.TryGetSession(patient, out _)
            || HasComp<CMUSurgeryArmedStepComponent>(patient)
            || HasComp<CMUSurgeryInProgressComponent>(patient))
        {
            // Automated work does not claim or erase a live manual session.
            StopPod(pod, comp);
            RefreshLinkedConsoles(pod);
            return;
        }

        var queued = comp.Queue[0];
        var occupantGeneration = comp.OccupantGeneration;
        var operatorUid = comp.Operator;
        var deadline = comp.NextStepAt;
        var revision = comp.StateRevision;
        comp.CurrentStep = FormatQueuedStep(queued);

        bool OwnsWork()
        {
            return !TerminatingOrDeleted(pod) && !TerminatingOrDeleted(patient) && comp.IsRunning &&
                comp.Patient == patient && comp.BodyContainer.ContainedEntity == patient &&
                comp.OccupantGeneration == occupantGeneration && comp.StateRevision == revision &&
                comp.Operator == operatorUid && comp.NextStepAt == deadline &&
                comp.Queue.Count > 0 && comp.Queue[0].Id == queued.Id;
        }

        bool IsCurrent()
        {
            return OwnsWork() && CanControl(operatorUid) && !_sessions.TryGetSession(patient, out _) &&
                !HasComp<CMUSurgeryArmedStepComponent>(patient) && !HasComp<CMUSurgeryInProgressComponent>(patient);
        }

        var succeeded = TryApplyAutomatedProcedure(patient, operatorUid, queued, IsCurrent);
        // Effects raise local events. Their subscribers can remove the patient,
        // replace work, or delete the pod before this call returns.
        if (!OwnsWork())
            return;

        if (!succeeded || !IsCurrent())
        {
            StopPod(pod, comp);
            RefreshLinkedConsoles(pod);
            return;
        }

        comp.Queue.RemoveAt(0);
        comp.StateRevision++;
        comp.CurrentStep = comp.Queue.Count > 0
            ? FormatQueuedStep(comp.Queue[0])
            : null;

        if (comp.Queue.Count == 0)
        {
            StopPod(pod, comp);
            EjectPatient(pod, comp);
            RefreshLinkedConsoles(pod);
            return;
        }

        StartProcedureTimer(pod, patient, comp, comp.Queue[0]);
        RefreshLinkedConsoles(pod);
    }

    private void StartProcedureTimer(
        EntityUid pod,
        EntityUid patient,
        CMUAutodocPodComponent comp,
        CMUAutodocQueuedStep queued)
    {
        comp.CurrentStep = FormatQueuedStep(queued);
        comp.NextStepAt = _timing.CurTime + GetProcedureDelay(comp, queued);
        _scheduler.Schedule(pod, ProcedureStepWork, comp.NextStepAt);
        PlayProcedureSound(patient, queued);
    }

    private TimeSpan GetProcedureDelay(CMUAutodocPodComponent comp, CMUAutodocQueuedStep queued)
    {
        var seconds = queued.DurationSeconds > 0f ? queued.DurationSeconds : comp.StepDelay;
        return TimeSpan.FromSeconds(MathF.Max(1f, seconds));
    }

    private string FormatQueuedStep(CMUAutodocQueuedStep queued)
    {
        var step = ResolveAutodocStepLabel(queued.StepLabel);
        return Loc.GetString(
            "cmu-autodoc-current-step-detail",
            ("surgery", queued.SurgeryDisplayName),
            ("part", queued.PartDisplayName),
            ("step", step));
    }

    private string ResolveLabel(string label)
    {
        return Loc.TryGetString(label, out var localized) ? localized : label;
    }

    private string ResolveAutodocStepLabel(string label)
    {
        var step = ResolveLabel(label);
        if (step.Contains("scalpel", StringComparison.OrdinalIgnoreCase) ||
            step.Contains("hemostat", StringComparison.OrdinalIgnoreCase) ||
            step.Contains("retractor", StringComparison.OrdinalIgnoreCase) ||
            step.Contains("cauter", StringComparison.OrdinalIgnoreCase))
        {
            return Loc.GetString("cmu-autodoc-automated-step-label");
        }

        return step;
    }

    private bool TryApplyAutomatedProcedure(EntityUid patient, EntityUid operatorUid, CMUAutodocQueuedStep queued,
        Func<bool> isCurrent)
    {
        if (!_cmuSurgery.IsSurgeryEnabled() || !isCurrent())
            return false;

        if (queued.SurgeryId == AutodocLimbRegenerationId)
        {
            return queued.TargetAnchor is { } anchor && queued.TargetSlot is { } slot &&
                _cmuSurgery.TryRegenerateLimb(patient, queued.Type, queued.Symmetry, anchor, slot, isCurrent);
        }

        var targetPart = ResolveQueuedPart(patient, queued);
        if (!targetPart.IsValid())
            return false;

        bool IsSiteCurrent()
        {
            return isCurrent() && ResolveQueuedPart(patient, queued) == targetPart &&
                _rulebook.GetProcedureOrgan(targetPart, queued.SurgeryId) == queued.TargetOrgan;
        }

        if (queued.SurgeryId == AutodocWoundRepairId)
            return TryApplyAutodocWoundRepair(patient, targetPart, IsSiteCurrent);

        if (!IsAutodocAllowedCategory(queued.Category) ||
            !_rulebook.IsProcedureEligible(patient, targetPart, operatorUid, queued.SurgeryId,
                ignoreSkillRequirements: true) ||
            _rulebook.GetProcedureOrgan(targetPart, queued.SurgeryId) != queued.TargetOrgan)
            return false;

        return _flow.TryExecuteAutomatedProcedure(patient, targetPart, operatorUid, queued.SurgeryId, IsSiteCurrent)
            == CMUSurgeryStepOutcome.Succeeded;
    }

    private EntityUid ResolveQueuedPart(EntityUid patient, CMUAutodocQueuedStep queued)
    {
        // A transplanted replacement requires a new selection even at the same site.
        return _medicalIndex.TryGetBodyPart(
            patient,
            new CMUMedicalBodyPartKey(queued.Type, queued.Symmetry),
            out var indexedPart) && indexedPart == queued.Part
            ? indexedPart
            : EntityUid.Invalid;
    }

    private bool TryApplyAutodocWoundRepair(EntityUid patient, EntityUid part, Func<bool> isCurrent)
    {
        if (!isCurrent() || !TryComp<BodyPartComponent>(part, out var anatomy) || anatomy.Body != patient)
            return false;

        if (TryComp<BodyPartWoundComponent>(part, out var wounds))
        {
            _wounds.ClearAllWounds((part, wounds));
            if (!isCurrent())
                return false;
            RemComp<BodyPartWoundComponent>(part);
        }

        if (!isCurrent())
            return false;
        RemComp<CMUEscharComponent>(part);
        if (!isCurrent())
            return false;
        if (TryComp<BodyPartHealthComponent>(part, out var health))
        {
            bool IsHealthCurrent() => isCurrent() &&
                TryComp<BodyPartHealthComponent>(part, out var currentHealth) && ReferenceEquals(health, currentHealth);

            // Wounds and structural HP describe the same injury. Only the selected
            // site's authoritative typed debt can reduce aggregate body damage.
            var remaining = _partHealth.GetOutstandingBodyDamage(part);
            _partHealth.HealPartDamage(patient, part, BruteGroup, remaining, healPart: false);
            if (!IsHealthCurrent())
                return false;
            _partHealth.HealPartDamage(patient, part, BurnGroup, remaining, healPart: false);
            if (!IsHealthCurrent())
                return false;
            _partHealth.SetCurrent((part, health), health.Max);
            if (!IsHealthCurrent())
                return false;
        }

        return isCurrent() && !NeedsAutodocWoundRepair(part);
    }
    /// <summary>
    ///     Stops only machine-owned work. Manual surgery sessions are
    ///     patient-scoped and are never claimed by the pod or its operator.
    /// </summary>
    private void StopPod(EntityUid pod, CMUAutodocPodComponent comp)
    {
        _scheduler.Cancel(pod, ProcedureStepWork);
        if (comp.IsRunning || comp.CurrentStep != null || comp.NextStepAt != TimeSpan.Zero || comp.Operator.IsValid())
            comp.StateRevision++;
        comp.IsRunning = false;
        comp.Operator = EntityUid.Invalid;
        comp.CurrentStep = null;
        comp.NextStepAt = TimeSpan.Zero;
        if (!TerminatingOrDeleted(pod))
        {
            _appearance.SetData(pod, CMUAutodocVisuals.Operating, false);
            _patientBay.UpdatePodAppearance(pod, comp.BodyContainer);
        }
    }

    private bool CanControl(EntityUid user)
    {
        return user.IsValid() && !TerminatingOrDeleted(user) && _skills.HasSkill(user, SurgerySkill, 2);
    }

    private List<CMUSurgeryPartEntry> BuildAutodocPartEntries(EntityUid patient, EntityUid viewer)
    {
        var source = _dispatch.BuildPartEntries(patient, viewer, ignoreSkillRequirements: true);
        var result = new List<CMUSurgeryPartEntry>(source.Count);
        var listedParts = new HashSet<EntityUid>();

        foreach (var part in source)
        {
            var surgeries = new List<CMUSurgeryEntry>();
            var canRegenerateLimb = false;
            var partUid = GetEntity(part.Part);
            if (partUid.IsValid())
                listedParts.Add(partUid);

            if (NeedsAutodocWoundRepair(partUid, part.Type, part.Symmetry))
                surgeries.Add(BuildAutodocWoundRepairEntry());

            foreach (var surgery in part.EligibleSurgeries)
            {
                if (surgery.Category == "reattach")
                {
                    canRegenerateLimb = true;
                    continue;
                }

                if (!IsAutodocAllowedCategory(surgery.Category))
                    continue;

                surgeries.Add(surgery with { AutodocDurationSeconds = GetProcedureDurationSeconds(surgery) });
            }

            if (canRegenerateLimb)
                surgeries.Add(BuildAutodocLimbRegenerationEntry());

            result.Add(new CMUSurgeryPartEntry(
                part.Part,
                part.Type,
                part.Symmetry,
                part.DisplayName,
                part.ConditionSummary,
                part.IsInFlightHere,
                part.LockedByOtherPart,
                surgeries));
        }

        AddWoundRepairOnlyPartEntries(patient, result, listedParts);
        return result;
    }

    private void AddWoundRepairOnlyPartEntries(
        EntityUid patient,
        List<CMUSurgeryPartEntry> result,
        HashSet<EntityUid> listedParts)
    {
        foreach (var (partUid, part) in _medicalIndex.GetBodyParts(patient))
        {
            if (listedParts.Contains(partUid) || !NeedsAutodocWoundRepair(partUid))
                continue;

            result.Add(new CMUSurgeryPartEntry(
                GetNetEntity(partUid),
                part.PartType,
                part.Symmetry,
                SharedCMUSurgeryFlowSystem.FormatPartName(part.PartType, part.Symmetry),
                BuildAutodocWoundRepairConditionSummary(partUid),
                false,
                false,
                [BuildAutodocWoundRepairEntry()]));
        }
    }

    private string BuildAutodocWoundRepairConditionSummary(EntityUid part)
    {
        if (HasComp<CMUEscharComponent>(part))
            return Loc.GetString("cmu-medical-surgery-condition-eschar");

        if (TryComp<BodyPartWoundComponent>(part, out var wounds) && _woundLedger.GetEntries(wounds).Count > 0)
            return Loc.GetString("cmu-medical-surgery-condition-wounds");

        return Loc.GetString("cmu-medical-surgery-condition-damaged");
    }

    private CMUSurgeryEntry BuildAutodocWoundRepairEntry()
    {
        return new CMUSurgeryEntry(
            AutodocWoundRepairId,
            Loc.GetString("cmu-autodoc-repair-wounds-surgery"),
            "cmu-autodoc-automated-step-label",
            "scalpel_or_burn_kit",
            0,
            1,
            null,
            AutodocWoundRepairCategory,
            AutodocDurationSeconds: 30f);
    }

    private CMUSurgeryEntry BuildAutodocLimbRegenerationEntry()
    {
        return new CMUSurgeryEntry(
            AutodocLimbRegenerationId,
            Loc.GetString("cmu-autodoc-regenerate-limb-surgery"),
            "cmu-autodoc-automated-step-label",
            AutodocLimbRegenerationCategory,
            0,
            1,
            null,
            AutodocLimbRegenerationCategory,
            AutodocDurationSeconds: LimbRegenerationSeconds);
    }

    private bool NeedsAutodocWoundRepair(EntityUid part, BodyPartType type, BodyPartSymmetry symmetry)
    {
        if (!part.IsValid() ||
            !TryComp<BodyPartComponent>(part, out var partComp) ||
            partComp.PartType != type ||
            partComp.Symmetry != symmetry)
        {
            return false;
        }

        if (HasComp<CMUEscharComponent>(part))
            return true;

        if (TryComp<BodyPartWoundComponent>(part, out var wounds) && _woundLedger.GetEntries(wounds).Count > 0)
            return true;

        return TryComp<BodyPartHealthComponent>(part, out var health) && health.Current < health.Max;
    }

    private bool NeedsAutodocWoundRepair(EntityUid part)
    {
        if (!part.IsValid())
            return false;

        if (HasComp<CMUEscharComponent>(part))
            return true;

        if (TryComp<BodyPartWoundComponent>(part, out var wounds) && _woundLedger.GetEntries(wounds).Count > 0)
            return true;

        return TryComp<BodyPartHealthComponent>(part, out var health) && health.Current < health.Max;
    }

    private static bool IsAutodocAllowedCategory(string category)
    {
        return category is "fracture"
            or "bleed"
            or "suture"
            or "head_organ"
            or AutodocLimbRegenerationCategory
            or AutodocWoundRepairCategory;
    }

    private static float GetProcedureDurationSeconds(CMUSurgeryEntry surgery)
    {
        if (surgery.SurgeryId == AutodocWoundRepairId)
            return 30f;

        if (surgery.SurgeryId == AutodocLimbRegenerationId)
            return LimbRegenerationSeconds;

        if (surgery.SurgeryId.Contains("Shattered", StringComparison.OrdinalIgnoreCase))
            return 60f;

        if (surgery.SurgeryId.Contains("Compound", StringComparison.OrdinalIgnoreCase))
            return 50f;

        if (surgery.SurgeryId.Contains("Simple", StringComparison.OrdinalIgnoreCase))
            return 35f;

        return surgery.Category switch
        {
            "fracture" => 45f,
            "bleed" => 35f,
            "suture" => 55f,
            "head_organ" => 60f,
            _ => DefaultProcedureSeconds,
        };
    }

    private void PlayProcedureSound(EntityUid patient, CMUAutodocQueuedStep queued)
    {
        if (!ProcedureSounds.TryGetValue(queued.Category, out var sound))
            return;

        _audio.PlayPvs(sound, patient);
    }

    private void RefreshUi(EntityUid console, CMUAutodocConsoleComponent comp, EntityUid? viewer = null)
    {
        if (viewer is { } target && target.IsValid())
        {
            if (_ui.IsUiOpen(console, CMUAutodocUIKey.Key, target))
                SendState(console, comp, target);
            return;
        }

        foreach (var actor in _ui.GetActors(console, CMUAutodocUIKey.Key))
            SendState(console, comp, actor);
    }

    private void SendState(EntityUid console, CMUAutodocConsoleComponent comp, EntityUid viewer)
    {
        var state = BuildStateForViewer(console, comp, viewer);
        _ui.ServerSendUiMessage(
            console,
            CMUAutodocUIKey.Key,
            new CMUAutodocStateMessage(state),
            viewer);
    }

    public CMUAutodocBuiState BuildStateForViewer(EntityUid console, CMUAutodocConsoleComponent comp, EntityUid? viewer)
    {
        var podLinked = TryFindLinkedPod(console, comp, out var pod, out var podComp);
        EntityUid patient = default;
        var hasPatient = podLinked && TryGetPatient(pod, out patient);
        var canQueue = viewer is { } user && CanControl(user) && hasPatient;
        var parts = new List<CMUSurgeryPartEntry>();
        if (canQueue && viewer is { } queueViewer)
            parts = BuildAutodocPartEntries(patient, queueViewer);

        var status = !podLinked
            ? Loc.GetString("cmu-autodoc-status-no-pod")
            : !hasPatient
                ? Loc.GetString("cmu-autodoc-status-empty")
                : podComp.IsRunning
                    ? Loc.GetString("cmu-autodoc-status-running")
                    : Loc.GetString("cmu-autodoc-status-ready");

        return new CMUAutodocBuiState(
            podLinked ? GetNetEntity(pod) : null,
            hasPatient ? GetNetEntity(patient) : null,
            hasPatient ? Name(patient) : Loc.GetString("cmu-autodoc-no-patient"),
            podLinked,
            canQueue,
            podLinked && podComp.IsRunning,
            status,
            podLinked ? podComp.CurrentStep : null,
            podLinked && podComp.NextStepAt > TimeSpan.Zero ? podComp.NextStepAt : null,
            parts,
            podLinked ? BuildQueueEntries(podComp) : [])
        {
            CommandContext = hasPatient
                ? new CMUAutodocCommandContext(GetNetEntity(pod), GetNetEntity(patient),
                    podComp.OccupantGeneration, podComp.StateRevision)
                : null,
        };
    }

    private List<CMUAutodocQueueEntry> BuildQueueEntries(CMUAutodocPodComponent pod)
    {
        var entries = new List<CMUAutodocQueueEntry>();
        for (var i = 0; i < pod.Queue.Count; i++)
        {
            var queued = pod.Queue[i];
            entries.Add(new CMUAutodocQueueEntry(
                i,
                GetNetEntity(queued.Part),
                queued.Type,
                queued.Symmetry,
                queued.PartDisplayName,
                queued.SurgeryId,
                queued.SurgeryDisplayName,
                queued.Category,
                queued.StepIndex,
                queued.StepLabel,
                queued.DurationSeconds,
                queued.Id));
        }

        return entries;
    }

    private bool TryFindLinkedPod(
        EntityUid console,
        CMUAutodocConsoleComponent comp,
        out EntityUid pod,
        out CMUAutodocPodComponent podComp)
    {
        return _patientBay.TryFindNearestPod(console, comp.LinkRange, out pod, out podComp);
    }

    private bool TryGetPatient(EntityUid pod, out EntityUid patient)
    {
        patient = default;
        if (!TryComp<CMUAutodocPodComponent>(pod, out var comp))
            return false;

        return _patientBay.TryGetPatient(comp.BodyContainer, out patient) && !TerminatingOrDeleted(patient);
    }

    private void OnPodInit(Entity<CMUAutodocPodComponent> ent, ref ComponentInit args)
    {
        ent.Comp.BodyContainer = _patientBay.EnsureBodyContainer(ent.Owner, CMUAutodocPodComponent.BodyContainerId);
        SynchronizeOccupant(ent);
        _patientBay.UpdatePodAppearance(ent.Owner, ent.Comp.BodyContainer);
    }

    private void OnPatientInserted(Entity<CMUAutodocPodComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container == ent.Comp.BodyContainer)
            SynchronizeOccupant(ent);
    }

    private void OnPatientRemoved(Entity<CMUAutodocPodComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container == ent.Comp.BodyContainer)
            SynchronizeOccupant(ent);
    }

    private void SynchronizeOccupant(Entity<CMUAutodocPodComponent> ent)
    {
        var patient = ent.Comp.BodyContainer.ContainedEntity;
        if (ent.Comp.Patient == patient)
            return;

        ClearOccupant(ent);
        ent.Comp.Patient = patient;
        if (patient is { } current && !TerminatingOrDeleted(current))
            EnsureComp<CMUAutodocContainedPatientComponent>(current).Pod = ent.Owner;
        _patientBay.UpdatePodAppearance(ent.Owner, ent.Comp.BodyContainer);
        RefreshLinkedConsoles(ent.Owner);
    }

    private void ClearOccupant(Entity<CMUAutodocPodComponent> ent)
    {
        if (ent.Comp.Patient is { } previous && !TerminatingOrDeleted(previous) &&
            TryComp<CMUAutodocContainedPatientComponent>(previous, out var marker) && marker.Pod == ent.Owner)
            RemComp<CMUAutodocContainedPatientComponent>(previous);

        StopPod(ent.Owner, ent.Comp);
        ent.Comp.Queue.Clear();
        ent.Comp.Patient = null;
        ent.Comp.OccupantGeneration = ++_nextOccupantGeneration;
        ent.Comp.StateRevision++;
    }

    private void OnPodShutdown(Entity<CMUAutodocPodComponent> ent, ref ComponentShutdown args)
    {
        ClearOccupant(ent);
    }

    private void OnPodDestroyed(Entity<CMUAutodocPodComponent> ent, ref DestructionEventArgs args)
    {
        EjectPatient(ent.Owner, ent.Comp);
    }

    private void OnPodDragDrop(Entity<CMUAutodocPodComponent> ent, ref DragDropTargetEvent args)
    {
        if (args.Handled || !_patientBay.CanInsertPatient(ent.Comp.BodyContainer, args.Dragged))
            return;

        StartInsertDoAfter(ent.Owner, ent.Comp, args.User, args.Dragged);
        args.Handled = true;
    }

    private void OnPodAlternativeVerbs(Entity<CMUAutodocPodComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;

        // Occupant ejection requires the console's visit-bound command context.
        // Generic verbs are reconstructed at execution and cannot bind that visit.
        if (!_patientBay.CanInsertPatient(ent.Comp.BodyContainer, user))
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Act = () => StartInsertDoAfter(ent.Owner, ent.Comp, user, user),
            Text = Loc.GetString("medical-scanner-verb-enter"),
            Priority = 2,
        });
    }

    private void OnPodRelayMovement(Entity<CMUAutodocPodComponent> ent, ref ContainerRelayMovementEntityEvent args)
    {
        if (!_patientBay.ContainsPatient(ent.Comp.BodyContainer, args.Entity))
            return;

        EjectPatient(ent.Owner, ent.Comp);
    }

    private void StartInsertDoAfter(EntityUid pod, CMUAutodocPodComponent comp, EntityUid user, EntityUid target)
    {
        _patientBay.StartInsertDoAfter(pod, user, target, comp.EntryDelay);
    }

    private void OnPodInsertDoAfter(Entity<CMUAutodocPodComponent> ent, ref CMUMedicalPodInsertDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target is not { } target)
            return;

        InsertPatient(ent.Owner, ent.Comp, target);
        args.Handled = true;
    }

    private bool InsertPatient(EntityUid pod, CMUAutodocPodComponent comp, EntityUid patient)
    {
        if (!_patientBay.TryInsertPatient(pod, comp.BodyContainer, patient))
            return false;

        return true;
    }

    private EntityUid? EjectPatient(EntityUid pod, CMUAutodocPodComponent comp)
    {
        if (!_patientBay.TryGetPatient(comp.BodyContainer, out var patient))
            return null;

        if (!_patientBay.TryEjectPatient(pod, comp.BodyContainer, patient))
            return null;

        return patient;
    }

    private void RefreshLinkedConsoles(EntityUid pod)
    {
        var query = EntityQueryEnumerator<CMUAutodocConsoleComponent>();
        while (query.MoveNext(out var console, out var consoleComp))
        {
            if (!TryFindLinkedPod(console, consoleComp, out var linkedPod, out _)
                || linkedPod != pod)
            {
                continue;
            }

            RefreshUi(console, consoleComp);
        }
    }
}
