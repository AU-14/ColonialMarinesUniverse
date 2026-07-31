using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.Destructible;
using Content.Shared._CMU14.Medical.Treatment.Surgery;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DragDrop;
using Content.Shared.FixedPoint;
using Content.Shared.Movement.Events;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Medical.Treatment.Surgery;

using RMCSurgerySystem = Content.Server._RMC14.Medical.Surgery.CMSurgerySystem;

public sealed partial class CMUAutodocSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private CMUSurgeryDispatchSystem _dispatch = default!;
    [Dependency] private SharedCMUSurgeryFlowSystem _flow = default!;
    [Dependency] private CMUSurgerySessionSystem _sessions = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private CMUMedicalPatientBaySystem _patientBay = default!;
    [Dependency] private SharedBodyPartHealthSystem _partHealth = default!;
    [Dependency] private SharedRMCDamageableSystem _rmcDamageable = default!;
    [Dependency] private RMCSurgerySystem _rmcSurgery = default!;
    [Dependency] private CMUMedicalSchedulerSystem _scheduler = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private SharedCMUWoundsSystem _wounds = default!;
    [Dependency] private CMUWoundLedgerSystem _woundLedger = default!;

    private static readonly EntProtoId<SkillDefinitionComponent> SurgerySkill = "RMCSkillSurgery";
    private const string AutodocWoundRepairId = "CMUAutodocRepairWounds";
    private const string AutodocWoundRepairCategory = "wound_repair";
    private const float DefaultProcedureSeconds = 45f;
    private static readonly CMUMedicalWorkKey ProcedureStepWork = new("autodoc-procedure-step");
    private static readonly ProtoId<DamageGroupPrototype> BruteGroup = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> BurnGroup = "Burn";

    private static readonly FrozenDictionary<string, SoundSpecifier> ProcedureSounds =
        new Dictionary<string, SoundSpecifier>
        {
            [AutodocWoundRepairCategory] = new SoundCollectionSpecifier("RMCSurgeryScalpel"),
            ["bleed"] = new SoundCollectionSpecifier("RMCSurgeryHemostat"),
            ["fracture"] = new SoundCollectionSpecifier("RMCSurgerySplint"),
            ["head_organ"] = new SoundCollectionSpecifier("RMCSurgeryOrgan"),
            ["suture"] = new SoundCollectionSpecifier("RMCSurgeryOrgan"),
        }.ToFrozenDictionary();

    private readonly HashSet<EntityUid> _openConsoles = new();
    private readonly List<EntityUid> _staleConsoles = new();
    private float _uiAccumulator;

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
        if (!CanControl(msg.Actor))
            return;

        if (!TryFindLinkedPod(ent.Owner, ent.Comp, out var pod, out var podComp)
            || !TryGetPatient(pod, out var patient))
        {
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
                    GetProcedureDurationSeconds(surgery)));
                RefreshUi(ent.Owner, ent.Comp);
                return;
            }
        }
    }

    private void OnRemoveQueueStep(Entity<CMUAutodocConsoleComponent> ent, ref CMUAutodocRemoveQueueStepMessage msg)
    {
        if (!CanControl(msg.Actor))
            return;

        if (!TryFindLinkedPod(ent.Owner, ent.Comp, out var pod, out var podComp))
            return;

        if (msg.Index < 0 || msg.Index >= podComp.Queue.Count)
            return;

        podComp.Queue.RemoveAt(msg.Index);
        if (podComp.Queue.Count == 0)
            StopPod(pod, podComp);
        else if (podComp.IsRunning && msg.Index == 0)
        {
            if (TryGetPatient(pod, out var patient))
                StartProcedureTimer(pod, patient, podComp, podComp.Queue[0]);
            else
                StopPod(pod, podComp);
        }
        RefreshUi(ent.Owner, ent.Comp);
    }

    private void OnClearQueue(Entity<CMUAutodocConsoleComponent> ent, ref CMUAutodocClearQueueMessage msg)
    {
        if (!CanControl(msg.Actor))
            return;

        if (!TryFindLinkedPod(ent.Owner, ent.Comp, out var pod, out var podComp))
            return;

        StopPod(pod, podComp);
        podComp.Queue.Clear();
        RefreshUi(ent.Owner, ent.Comp);
    }

    private void OnStart(Entity<CMUAutodocConsoleComponent> ent, ref CMUAutodocStartMessage msg)
    {
        if (!CanControl(msg.Actor))
            return;

        if (!TryFindLinkedPod(ent.Owner, ent.Comp, out var pod, out var podComp)
            || podComp.Queue.Count == 0
            || !TryGetPatient(pod, out var patient))
        {
            return;
        }

        podComp.Operator = msg.Actor;
        podComp.IsRunning = true;
        StartProcedureTimer(pod, patient, podComp, podComp.Queue[0]);
        _appearance.SetData(pod, CMUAutodocVisuals.Operating, true);
        RefreshUi(ent.Owner, ent.Comp);
    }

    private void OnStop(Entity<CMUAutodocConsoleComponent> ent, ref CMUAutodocStopMessage msg)
    {
        if (!CanControl(msg.Actor))
            return;

        if (!TryFindLinkedPod(ent.Owner, ent.Comp, out var pod, out var podComp))
            return;

        StopPod(pod, podComp);
        RefreshUi(ent.Owner, ent.Comp);
    }

    private void OnEjectPatient(Entity<CMUAutodocConsoleComponent> ent, ref CMUAutodocEjectPatientMessage msg)
    {
        if (!CanControl(msg.Actor))
            return;

        if (!TryFindLinkedPod(ent.Owner, ent.Comp, out var pod, out var podComp)
            || podComp.BodyContainer.ContainedEntity is not { } patient)
        {
            return;
        }

        EjectPatient(pod, podComp);
        RefreshUi(ent.Owner, ent.Comp);
    }

    private void ProcessPod(EntityUid pod, CMUAutodocPodComponent comp)
    {
        if (comp.Queue.Count == 0 || !comp.Operator.IsValid() || !TryGetPatient(pod, out var patient))
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
        comp.CurrentStep = FormatQueuedStep(queued);

        if (!TryApplyAutomatedProcedure(patient, comp.Operator, queued))
        {
            StopPod(pod, comp);
            RefreshLinkedConsoles(pod);
            return;
        }

        comp.Queue.RemoveAt(0);
        comp.CurrentStep = comp.Queue.Count > 0
            ? FormatQueuedStep(comp.Queue[0])
            : null;

        if (comp.Queue.Count == 0)
        {
            EjectPatient(pod, comp);
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

    private bool TryApplyAutomatedProcedure(EntityUid patient, EntityUid operatorUid, CMUAutodocQueuedStep queued)
    {
        var targetPart = ResolveQueuedPart(patient, queued);
        if (!targetPart.IsValid())
            return false;

        if (queued.SurgeryId == AutodocWoundRepairId)
            return TryApplyAutodocWoundRepair(patient, targetPart);

        if (!IsAutodocAllowedCategory(queued.Category))
            return false;

        var surgeryId = new EntProtoId(queued.SurgeryId);
        if (_rmcSurgery.GetSingleton(surgeryId) is not { } surgeryEnt ||
            !TryComp<CMSurgeryComponent>(surgeryEnt, out var surgery))
        {
            return false;
        }

        var tools = new List<EntityUid>();
        foreach (var stepId in surgery.Steps)
        {
            if (_rmcSurgery.GetSingleton(stepId) is not { } stepEnt)
                return false;

            var stepEvent = new CMSurgeryStepEvent(operatorUid, patient, targetPart, tools);
            RaiseLocalEvent(stepEnt, ref stepEvent);
        }

        var completeEv = new CMSurgeryCompleteEvent(patient, operatorUid, surgeryId);
        RaiseLocalEvent(patient, ref completeEv);
        return true;
    }

    private EntityUid ResolveQueuedPart(EntityUid patient, CMUAutodocQueuedStep queued)
    {
        // Queue entries describe a logical body site, not an enduring entity
        // capability. Resolve through the current patient's body index so a
        // stale UID can never make work spill onto a previous occupant.
        return _medicalIndex.TryGetBodyPart(
            patient,
            new CMUMedicalBodyPartKey(queued.Type, queued.Symmetry),
            out var indexedPart)
            ? indexedPart
            : EntityUid.Invalid;
    }

    private bool TryApplyAutodocWoundRepair(EntityUid patient, EntityUid part)
    {
        var changed = false;
        var bruteHeal = FixedPoint2.Zero;
        var burnHeal = FixedPoint2.Zero;
        var hadBruteDamage = false;
        var hadBurnDamage = HasComp<CMUEscharComponent>(part);

        if (TryComp<BodyPartWoundComponent>(part, out var wounds))
        {
            foreach (var entry in _woundLedger.GetEntries(wounds))
            {
                var wound = entry.Wound;
                var remaining = wound.Damage - wound.Healed;
                if (remaining <= FixedPoint2.Zero)
                    continue;

                switch (wound.Type)
                {
                    case WoundType.Brute:
                        hadBruteDamage = true;
                        bruteHeal += remaining;
                        break;
                    case WoundType.Burn:
                        hadBurnDamage = true;
                        burnHeal += remaining;
                        break;
                }
            }

            _wounds.ClearAllWounds((part, wounds));
            RemComp<BodyPartWoundComponent>(part);
            changed = true;
        }

        if (HasComp<CMUEscharComponent>(part))
        {
            RemComp<CMUEscharComponent>(part);
            burnHeal += FixedPoint2.New(20);
            changed = true;
        }

        if (TryComp<BodyPartHealthComponent>(part, out var health) && health.Current < health.Max)
        {
            var missing = health.Max - health.Current;
            _partHealth.SetCurrent((part, health), health.Max);
            AddMissingPartHeal(patient, missing, hadBruteDamage, hadBurnDamage, ref bruteHeal, ref burnHeal);
            changed = true;
        }

        HealDamageGroup(patient, part, BruteGroup, bruteHeal);
        HealDamageGroup(patient, part, BurnGroup, burnHeal);
        return changed || !NeedsAutodocWoundRepair(part);
    }

    private void AddMissingPartHeal(
        EntityUid patient,
        FixedPoint2 missing,
        bool hadBruteDamage,
        bool hadBurnDamage,
        ref FixedPoint2 bruteHeal,
        ref FixedPoint2 burnHeal)
    {
        if (missing <= FixedPoint2.Zero)
            return;

        if (hadBruteDamage && !hadBurnDamage)
        {
            bruteHeal += missing;
            return;
        }

        if (hadBurnDamage && !hadBruteDamage)
        {
            burnHeal += missing;
            return;
        }

        var bruteDamage = GetDamageGroupAmount(patient, BruteGroup);
        var burnDamage = GetDamageGroupAmount(patient, BurnGroup);
        var totalDamage = bruteDamage + burnDamage;

        if (totalDamage <= FixedPoint2.Zero)
        {
            if (hadBurnDamage)
                burnHeal += missing;
            else
                bruteHeal += missing;
            return;
        }

        var bruteShare = FixedPoint2.Min(missing, bruteDamage);
        if (bruteDamage > FixedPoint2.Zero && burnDamage > FixedPoint2.Zero)
        {
            var bruteRatio = bruteDamage.Float() / totalDamage.Float();
            bruteShare = FixedPoint2.New(missing.Float() * bruteRatio);
        }

        bruteHeal += bruteShare;
        burnHeal += missing - bruteShare;
    }

    private FixedPoint2 GetDamageGroupAmount(EntityUid patient, ProtoId<DamageGroupPrototype> group)
    {
        if (!TryComp<DamageableComponent>(patient, out var damageable))
            return FixedPoint2.Zero;

        return damageable.DamagePerGroup.GetValueOrDefault(group.Id);
    }

    private void HealDamageGroup(EntityUid patient, EntityUid origin, ProtoId<DamageGroupPrototype> group, FixedPoint2 amount)
    {
        if (amount <= FixedPoint2.Zero)
            return;

        if (!TryComp<DamageableComponent>(patient, out var damageable))
            return;

        var spec = _rmcDamageable.DistributeHealing((patient, damageable), group, amount);
        _damageable.TryChangeDamage(
            patient,
            spec,
            ignoreResistances: true,
            interruptsDoAfters: false,
            damageable: damageable,
            origin: origin);
    }

    /// <summary>
    ///     Stops only machine-owned work. Manual surgery sessions are
    ///     patient-scoped and are never claimed by the pod or its operator.
    /// </summary>
    private void StopPod(EntityUid pod, CMUAutodocPodComponent comp)
    {
        _scheduler.Cancel(pod, ProcedureStepWork);
        comp.IsRunning = false;
        comp.CurrentStep = null;
        comp.NextStepAt = TimeSpan.Zero;
        _appearance.SetData(pod, CMUAutodocVisuals.Operating, false);
        _patientBay.UpdatePodAppearance(pod, comp.BodyContainer);
    }

    private bool CanControl(EntityUid user)
    {
        return _skills.HasSkill(user, SurgerySkill, 2);
    }

    private List<CMUSurgeryPartEntry> BuildAutodocPartEntries(EntityUid patient, EntityUid viewer)
    {
        var source = _dispatch.BuildPartEntries(patient, viewer, ignoreSkillRequirements: true);
        var result = new List<CMUSurgeryPartEntry>(source.Count);
        var listedParts = new HashSet<EntityUid>();

        foreach (var part in source)
        {
            var surgeries = new List<CMUSurgeryEntry>();
            var partUid = GetEntity(part.Part);
            if (partUid.IsValid())
                listedParts.Add(partUid);

            if (NeedsAutodocWoundRepair(partUid, part.Type, part.Symmetry))
                surgeries.Add(BuildAutodocWoundRepairEntry());

            foreach (var surgery in part.EligibleSurgeries)
            {
                if (!IsAutodocAllowedCategory(surgery.Category))
                    continue;

                surgeries.Add(surgery);
            }

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
            AutodocWoundRepairCategory);
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
            or AutodocWoundRepairCategory;
    }

    private static float GetProcedureDurationSeconds(CMUSurgeryEntry surgery)
    {
        if (surgery.SurgeryId == AutodocWoundRepairId)
            return 30f;

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
        var state = BuildState(console, comp, viewer);
        _ui.ServerSendUiMessage(
            console,
            CMUAutodocUIKey.Key,
            new CMUAutodocStateMessage(state),
            viewer);
    }

    private CMUAutodocBuiState BuildState(EntityUid console, CMUAutodocConsoleComponent comp, EntityUid? viewer)
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
            podLinked ? BuildQueueEntries(podComp) : []);
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
                queued.DurationSeconds));
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

        return _patientBay.TryGetPatient(comp.BodyContainer, out patient);
    }

    private void OnPodInit(Entity<CMUAutodocPodComponent> ent, ref ComponentInit args)
    {
        ent.Comp.BodyContainer = _patientBay.EnsureBodyContainer(ent.Owner, CMUAutodocPodComponent.BodyContainerId);
        _patientBay.UpdatePodAppearance(ent.Owner, ent.Comp.BodyContainer);
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

        if (_patientBay.TryGetPatient(ent.Comp.BodyContainer, out _))
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Act = () => EjectPatient(ent.Owner, ent.Comp),
                Category = VerbCategory.Eject,
                Text = Loc.GetString("medical-scanner-verb-noun-occupant"),
                Priority = 1,
            });
            return;
        }

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

        EnsureComp<CMUAutodocContainedPatientComponent>(patient);
        RefreshLinkedConsoles(pod);
        return true;
    }

    private EntityUid? EjectPatient(EntityUid pod, CMUAutodocPodComponent comp)
    {
        if (!_patientBay.TryGetPatient(comp.BodyContainer, out var patient))
            return null;

        StopPod(pod, comp);
        comp.Queue.Clear();
        comp.Operator = EntityUid.Invalid;
        RemCompDeferred<CMUAutodocContainedPatientComponent>(patient);
        _patientBay.TryEjectPatient(pod, comp.BodyContainer, patient);
        RefreshLinkedConsoles(pod);
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
